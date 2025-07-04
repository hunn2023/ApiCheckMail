using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;
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


        [HttpGet("get-emails")]
        public async Task<IActionResult> GetEmailAsync(int customerId = 1, string fullName = "Sono Hiroyuki", string domain = "https://www.e-petlife.co.jp/", string? domainCraw = "", CancellationToken token = default)

        {
            var stopwatch = Stopwatch.StartNew();
            //1.Check xem customerId có tồn tại  không?
            if (customerId == 0)
            {
                return Unauthorized("Missing API key");
            }

            //2.Check xem customerId còn hạn mức không ?
            var db = _redis.GetDatabase();
            string customerKey = "customer:1";

            bool exists = await db.KeyExistsAsync(customerKey);
            if (!exists)
            {
                return NotFound("Customer not found");
            }

            HashEntry[] hashEntries = await db.HashGetAllAsync(customerKey);
            var hash = hashEntries.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
            if (!hash.ContainsKey("email") || !hash.ContainsKey("api_key") ||
                !hash.ContainsKey("quota_total") || !hash.ContainsKey("quota_used"))
            {
                return BadRequest("Missing required fields in customer data");
            }

            int quotaTotal = int.Parse(hash["quota_total"]);
            int quotaUsed = int.Parse(hash["quota_used"]);

            if (quotaUsed >= quotaTotal)
            {
                return Unauthorized("API key quota exceeded");
            }

            //3 tạo danh sách email từ fullName và domain
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(domain))
            {
                return BadRequest("Full name and domain are required");
            }
            var names = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = names.Length > 0 ? names[0] : string.Empty;
            var lastName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : string.Empty;
            var emails = GenerateEmails(firstName, lastName, domain, domainCraw);


            //4.Lấy ra danh sách apikey có isactive = 1,usage_count < max_limit
            if (emails.Count == 0)
            {
                return BadRequest("No valid emails generated");
            }
            var emailResults = new List<object>();

            var selectedKeys = new List<(string apiKey, string realKey, List<string> emailsToCheck)>();
            var allKeys = await db.ListRangeAsync("apikey:list");
            int emailIndex = 0;

            foreach (var key in allKeys)
            {
                var hashKey = $"apikey:{key}";
                var keyData = await db.HashGetAllAsync(hashKey);
                var dict = keyData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                if (!dict.TryGetValue("isactive", out var isActive) || isActive != "1") continue;
                if (!dict.TryGetValue("key", out var realKey)) continue;

                int usage = int.Parse(dict.GetValueOrDefault("usage_count", "0"));
                int max = int.Parse(dict.GetValueOrDefault("max_limit", "0"));
                int canUse = max - usage;

                if (canUse <= 0) continue;

                int remaining = emails.Count - emailIndex;
                int assignCount = Math.Min(canUse, remaining);
                var assignedEmails = emails.Skip(emailIndex).Take(assignCount).ToList();

                selectedKeys.Add((key, realKey, assignedEmails));
                emailIndex += assignCount;

                if (emailIndex >= emails.Count) break;
            }

            if (emailIndex < emails.Count)
                return BadRequest("Not enough API quota available");

            var results = new List<object>();
            foreach (var (apiKey, realKey, emailsToCheck) in selectedKeys)
            {
                var semaphore = new SemaphoreSlim(13);
                var tasks = emailsToCheck.Select(async email =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        var json = await CheckEmailAsync(email, realKey, token);
                        lock (results)
                        {
                            results.Add(new { email, response = json });
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                        await Task.Delay(80); // để giữ tốc độ ≤ 15 req/s
                    }
                }).ToList();

                await Task.WhenAll(tasks);
                await db.HashIncrementAsync($"apikey:{apiKey}", "usage_count", emailsToCheck.Count);
            }

            await db.HashIncrementAsync(customerKey, "quota_used", emails.Count);

            stopwatch.Stop();

            return Ok(new
            {
                totalChecked = emails.Count,
                usedKeys = selectedKeys.Count,
                durationSeconds = stopwatch.Elapsed.TotalSeconds,
                results
            });
        }

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
            domainCraw = domainCraw.ToLower();

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
