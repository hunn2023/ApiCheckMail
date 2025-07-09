using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Http;
using FromBodyAttribute = Microsoft.AspNetCore.Mvc.FromBodyAttribute;
using HttpGetAttribute = Microsoft.AspNetCore.Mvc.HttpGetAttribute;
using HttpPostAttribute = Microsoft.AspNetCore.Mvc.HttpPostAttribute;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace EmailChecked.Controllers
{

    [ApiController]
    [Route("api/v1/email")]
    public class EmailCheckerController : ControllerBase
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IHttpClientFactory _httpClientFactory;

        public EmailCheckerController(IConnectionMultiplexer redis, IHttpClientFactory httpClientFactory)
        {
            _redis = redis;
            _httpClientFactory = httpClientFactory;
        }


        [HttpPost("get")]
        public async Task<IActionResult> GetEmailAsync(
                            int customerId,
                            List<string> fullNames,
                            string domain,
                            string? domainCraw,
                            CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();

            // 1. Validate customer ID
            if (customerId == 0)
                return Unauthorized("Missing API key");

            var db = _redis.GetDatabase();
            string customerKey = $"customer:{customerId}";

            if (!await db.KeyExistsAsync(customerKey))
                return NotFound("Customer not found");

            var customerData = (await db.HashGetAllAsync(customerKey))
                                .ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());

            if (!customerData.TryGetValue("quota_total", out var quotaTotalStr) ||
                !customerData.TryGetValue("quota_used", out var quotaUsedStr))
                return BadRequest("Missing required fields in customer data");

            if (!int.TryParse(quotaTotalStr, out var quotaTotal) ||
                !int.TryParse(quotaUsedStr, out var quotaUsed))
                return BadRequest("Invalid quota values");

            if (quotaUsed >= quotaTotal)
                return Unauthorized("API key quota exceeded");

            // 2. Validate input data
            if (fullNames == null || fullNames.Count == 0 || string.IsNullOrWhiteSpace(domain))
                return BadRequest("Full name and domain are required");

            // 3. Generate emails
            var emails = new List<string>();
            foreach (var item in fullNames)
            {
                if (string.IsNullOrWhiteSpace(item))
                    return BadRequest("Full name cannot be empty");

                var names = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var lastName = names.Length > 0 ? names[0] : string.Empty;
                var firstName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : string.Empty;

                emails.AddRange(GenerateEmails(firstName, lastName, domain, domainCraw));
            }
            emails = emails.Distinct().ToList();
            if (emails.Count == 0)
                return BadRequest("No valid emails generated");

            // 4. Distribute emails to active API keys
            var selectedKeys = new List<(string redisKey, string realKey, List<string> assignedEmails)>();
            var allKeys = await db.ListRangeAsync("apikey:list");

            var semaphoreKey = new SemaphoreSlim(50); // Giới hạn 50 request đồng thời
            var keyTasks = allKeys.Select(async key =>
            {
                await semaphoreKey.WaitAsync();
                try
                {
                    string keyRedis = $"apikey:{key}";
                    var hashEntries = await db.HashGetAllAsync(keyRedis);
                    var keyData = hashEntries.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());

                    if (!keyData.TryGetValue("isactive", out var isActive) || isActive != "1") return (ValueTuple<string, string, int>?)null;
                    if (!keyData.TryGetValue("key", out var realKey)) return (ValueTuple<string, string, int>?)null;

                    int usage = int.TryParse(keyData.GetValueOrDefault("usage_count", "0"), out var u) ? u : 0;
                    int max = int.TryParse(keyData.GetValueOrDefault("max_limit", "0"), out var m) ? m : 0;
                    int canUse = max - usage;

                    if (canUse <= 0) return (ValueTuple<string, string, int>?)null; ;

                    return (keyRedis, realKey, canUse);
                }
                finally
                {
                    semaphoreKey.Release();
                }
            });

            var keyResults = await Task.WhenAll(keyTasks);
            List<(string keyRedis, string realKey, int canUse)> validKeys = keyResults
             .Where(x => x != null)
             .Select(x => x.Value)
             .ToList();

            int emailIndex = 0;
            foreach (var (keyRedis, realKey, canUse) in validKeys)
            {
                int remaining = emails.Count - emailIndex;
                int assignCount = Math.Min(canUse, remaining);
                if (assignCount <= 0) break;

                var assignedEmails = emails.Skip(emailIndex).Take(assignCount).ToList();
                selectedKeys.Add((keyRedis, realKey, assignedEmails));
                emailIndex += assignCount;

                if (emailIndex >= emails.Count) break;
            }

            if (emailIndex < emails.Count)
            {
                return BadRequest("Not enough API quota available");
            }

            // 5. Check emails in parallel (across all keys)
            var results = new ConcurrentBag<object>();
            var semaphore = new SemaphoreSlim(13);
            var allTasks = new List<Task>();

            foreach (var (redisKey, realKey, emailsToCheck) in selectedKeys)
            {
                foreach (var email in emailsToCheck)
                {
                    allTasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync(token);
                        try
                        {
                            var json = await CheckEmailAsync(email, realKey, token);
                            results.Add(new { email, response = json });
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }

                // Cập nhật usage của từng API key (sau khi assign email)
                await db.HashIncrementAsync(redisKey, "usage_count", emailsToCheck.Count);
            }

            await Task.WhenAll(allTasks);

            // 6. Cập nhật quota_used cho customer
            await db.HashIncrementAsync(customerKey, "quota_used", emails.Count);

            stopwatch.Stop();

            return Ok(new
            {
                totalChecked = emails.Count,
                usedKeys = selectedKeys.Count,
                durationSeconds = stopwatch.Elapsed.TotalSeconds,
                results = results.ToList()
            });
        }

        //[HttpPost("get-emails")]
        //public async Task<IActionResult> GetEmailAsync(int customerId, List<string> fullNames, string domain, string? domainCraw, CancellationToken token)

        //{
        //    var stopwatch = Stopwatch.StartNew();
        //    List<string> emails = new List<string>();
        //    //1.Check xem customerId có tồn tại  không?
        //    if (customerId == 0)
        //    {
        //        return Unauthorized("Missing API key");
        //    }

        //    //2.Check xem customerId còn hạn mức không ?
        //    var db = _redis.GetDatabase();
        //    string customerKey = "customer:1";

        //    bool exists = await db.KeyExistsAsync(customerKey);
        //    if (!exists)
        //    {
        //        return NotFound("Customer not found");
        //    }

        //    HashEntry[] hashEntries = await db.HashGetAllAsync(customerKey);
        //    var hash = hashEntries.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
        //    if (!hash.ContainsKey("email") || !hash.ContainsKey("api_key") ||
        //        !hash.ContainsKey("quota_total") || !hash.ContainsKey("quota_used"))
        //    {
        //        return BadRequest("Missing required fields in customer data");
        //    }

        //    int quotaTotal = int.Parse(hash["quota_total"]);
        //    int quotaUsed = int.Parse(hash["quota_used"]);

        //    if (quotaUsed >= quotaTotal)
        //    {
        //        return Unauthorized("API key quota exceeded");
        //    }

        //    //3 tạo danh sách email từ fullName và domain
        //    if (fullNames == null || string.IsNullOrWhiteSpace(domain))
        //    {
        //        return BadRequest("Full name and domain are required");
        //    }

        //    foreach (var item in fullNames)
        //    {
        //        if (string.IsNullOrWhiteSpace(item))
        //        {
        //            return BadRequest("Full name cannot be empty");
        //        }

        //        var names = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        //        var firstName = names.Length > 0 ? names[0] : string.Empty;
        //        var lastName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : string.Empty;
        //        var email = GenerateEmails(firstName, lastName, domain, domainCraw);
        //        emails.AddRange(email);
        //    }
        //    //4.Lấy ra danh sách apikey có isactive = 1,usage_count < max_limit
        //    if (emails.Count == 0)
        //    {
        //        return BadRequest("No valid emails generated");
        //    }
        //    var emailResults = new List<object>();

        //    var selectedKeys = new List<(string apiKey, string realKey, List<string> emailsToCheck)>();
        //    var allKeys = await db.ListRangeAsync("apikey:list");
        //    int emailIndex = 0;

        //    foreach (var key in allKeys)
        //    {
        //        var hashKey = $"apikey:{key}";
        //        var keyData = await db.HashGetAllAsync(hashKey);
        //        var dict = keyData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

        //        if (!dict.TryGetValue("isactive", out var isActive) || isActive != "1") continue;
        //        if (!dict.TryGetValue("key", out var realKey)) continue;

        //        int usage = int.Parse(dict.GetValueOrDefault("usage_count", "0"));
        //        int max = int.Parse(dict.GetValueOrDefault("max_limit", "0"));
        //        int canUse = max - usage;

        //        if (canUse <= 0) continue;

        //        int remaining = emails.Count - emailIndex;
        //        int assignCount = Math.Min(canUse, remaining);
        //        var assignedEmails = emails.Skip(emailIndex).Take(assignCount).ToList();

        //        selectedKeys.Add((key, realKey, assignedEmails));
        //        emailIndex += assignCount;

        //        if (emailIndex >= emails.Count) break;
        //    }

        //    if (emailIndex < emails.Count)
        //        return BadRequest("Not enough API quota available");

        //    var results = new List<object>();
        //    foreach (var (apiKey, realKey, emailsToCheck) in selectedKeys)
        //    {
        //        var semaphore = new SemaphoreSlim(13);
        //        var tasks = emailsToCheck.Select(async email =>
        //        {
        //            await semaphore.WaitAsync(token);
        //            try
        //            {
        //                var json = await CheckEmailAsync(email, realKey, token);
        //                lock (results)
        //                {
        //                    results.Add(new { email, response = json });
        //                }
        //            }
        //            finally
        //            {
        //                semaphore.Release();
        //            }
        //        }).ToList();

        //        await Task.WhenAll(tasks);
        //        await db.HashIncrementAsync($"apikey:{apiKey}", "usage_count", emailsToCheck.Count);
        //    }

        //    await db.HashIncrementAsync(customerKey, "quota_used", emails.Count);

        //    stopwatch.Stop();

        //    return Ok(new
        //    {
        //        totalChecked = emails.Count,
        //        usedKeys = selectedKeys.Count,
        //        durationSeconds = stopwatch.Elapsed.TotalSeconds,
        //        results
        //    });
        //}

        [HttpGet("check-email")]
        public async Task<string> CheckEmailAsync(string email, string key, CancellationToken token)
        {
            string url = $"https://apps.emaillistverify.com/api/verifyEmail?secret={key}&email={email}";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(url, token);
                    if (!response.IsSuccessStatusCode) return "error";

                    string json = await response.Content.ReadAsStringAsync();
                    return json;
                }
                catch
                {
                    return "call api error";
                }
            }
        }

        private List<string> GenerateEmails(string firstName, string lastName, string domain, string domainCraw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(domain))
                return result;

            firstName = firstName.ToLower();
            lastName = lastName.ToLower();
            domain = domain.ToLower();
            domainCraw = domainCraw?.ToLower();

            result = GenerateTemplateEmails(firstName, lastName, domain);
            if (!string.IsNullOrWhiteSpace(domainCraw))
            {
                var crawEmails = GenerateTemplateEmails(firstName, lastName, domainCraw);
                result.AddRange(crawEmails);
            }
            return result.Distinct().ToList();

        }
        private List<string> GenerateTemplateEmails(string firstname, string lastname, string domain)
        {

            List<string> result = new List<string>();
            string f = firstname.Substring(0, 1).ToLower();
            string l = lastname.Substring(0, 1).ToLower();
            string domainFromUrl = GetDomain(domain);
            string at = "@" + domainFromUrl;
            result.Add($"{f}-{lastname}{at}");
            result.Add($"{l}-{firstname}{at}");
            result.Add($"{firstname}-{l}{at}");
            result.Add($"{lastname}-{f}{at}");
            result.Add($"{firstname}-{lastname}{at}");
            result.Add($"{lastname}-{firstname}{at}");
            result.Add($"{f}_{lastname}{at}");
            result.Add($"{l}_{firstname}{at}");
            result.Add($"{firstname}_{l}{at}");
            result.Add($"{lastname}_{f}{at}");
            result.Add($"{firstname}_{lastname}{at}");
            result.Add($"{lastname}_{firstname}{at}");
            result.Add($"{f}.{lastname}{at}");
            result.Add($"{l}.{firstname}{at}");
            result.Add($"{firstname}.{l}{at}");
            result.Add($"{lastname}.{f}{at}");
            result.Add($"{lastname}.{firstname}{at}");
            result.Add($"{firstname}.{lastname}{at}");
            result.Add($"{lastname}{at}");
            result.Add($"{firstname}{at}");
            result.Add($"{f}{lastname}{at}");
            result.Add($"{l}{firstname}{at}");
            result.Add($"{lastname}{f}{at}");
            result.Add($"{firstname}{l}{at}");
            result.Add($"{firstname}{lastname}{at}");
            result.Add($"{lastname}{firstname}{at}");
            result.Add($"{f}{l}{at}");
            result.Add($"{l}{f}{at}");


            return result;
        }
        private string GetDomain(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return string.Empty;


                var match = Regex.Match(url, @"^(?:https?:\/\/)?(?:www\.)?([^\/]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }
    }
}
