using ApiCheckMail;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
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
    [Route("api/v1/emails")]
    public class EmailCheckerController : ControllerBase
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IHttpClientFactory _httpClientFactory;

        public EmailCheckerController(IConnectionMultiplexer redis, IHttpClientFactory httpClientFactory)
        {
            _redis = redis;
            _httpClientFactory = httpClientFactory;
        }


        //[HttpPost("query")]
        //public async Task<IActionResult> QueryEmails([FromBody] EmailQueryRequest request, CancellationToken token)
        //{
        //    var stopwatch = Stopwatch.StartNew();
        //    List<EmailQueryRequest> users = new List<EmailQueryRequest>()
        //    {
        //        new EmailQueryRequest
        //        {
        //            sheetName = "hadtt",
        //            CustomerApiKey = "9a7f8b10-8c3d-437b-9482-362b56a4106a",
        //        },
        //        new EmailQueryRequest
        //        {
        //             sheetName = "tranght",
        //            CustomerApiKey = "bd427722-ebba-4da2-99e3-d091065f9ec5",
        //        },
        //        new EmailQueryRequest
        //        {
        //            sheetName = "hoahp",
        //            CustomerApiKey = "313f2ed9-e776-47b3-acc1-511a021b8b7c",
        //        },
        //         new EmailQueryRequest
        //        {
        //            sheetName = "huyennk",
        //            CustomerApiKey = "81f3116c-c19e-4c96-82ca-a44d339b3141",
        //        },
        //    };



        //    var user = users.FirstOrDefault(x => x.sheetName == request.sheetName);
        //    if (user == null)
        //        return BadRequest("Invalid user");

        //    request.CustomerApiKey = user.CustomerApiKey;


        //    if (string.IsNullOrWhiteSpace(request.CustomerApiKey))
        //        return Unauthorized("Missing API key");

        //    var db = _redis.GetDatabase();

        //    string customerKey = "customer:data";
        //    string customerField = $"customer_{request.CustomerApiKey}";

        //    var customerJson = await db.HashGetAsync(customerKey, customerField);
        //    if (customerJson.IsNullOrEmpty)
        //        return NotFound("Customer not found");

        //    var customer = JsonConvert.DeserializeObject<CustomerInfo>(customerJson!);

        //    if (customer.isActive != "1")
        //        return Unauthorized("Customer is not active");

        //    if (customer.quota_used >= customer.quota_total)
        //        return Unauthorized("API key quota exceeded");


        //    if (request.Names == null || request.Names.Count == 0 || string.IsNullOrWhiteSpace(request.Domain))
        //        return BadRequest("Full name and domain are required");



        //    var emails = new List<string>();
        //    foreach (var fullName in request.Names)
        //    {
        //        if (string.IsNullOrWhiteSpace(fullName))
        //            return BadRequest("Full name cannot be empty");

        //        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        //        var lastName = parts.Length > 0 ? parts[0] : "";
        //        var firstName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";

        //        emails.AddRange(GenerateEmails(firstName, lastName, request.Domain, request.DomainCraw));
        //    }

        //    emails = emails.Distinct().ToList();
        //    if (emails.Count == 0)
        //        return BadRequest("No valid emails generated");

        //    // 1. Lấy danh sách các key còn quota bằng Lua Script
        //    var luaGetKeys = @"
        //                    local rawKeys = redis.call('HVALS', 'apikey:data')
        //                    local result = {}

        //                    for i = 1, #rawKeys do
        //                        local data = cjson.decode(rawKeys[i])
        //                        local usage = tonumber(data['usage_count']) or 0
        //                        local max = tonumber(data['max_limit']) or 0
        //                        local isactive = data['isactive']
        //                        if isactive == '1' and usage < max then
        //                            table.insert(result, cjson.encode({
        //                                keyId = data['key_id'],
        //                                realKey = data['key'],
        //                                canUse = max - usage
        //                            }))
        //                        end
        //                    end
        //                    return result
        //                ";

        //    var rawKeys = await db.ScriptEvaluateAsync(luaGetKeys);
        //    var validKeys = ((RedisResult[])rawKeys)
        //        .Select(r => JsonConvert.DeserializeObject<ApiKeyInfo>((string)(RedisValue)r))
        //        .OrderByDescending(k => k.CanUse)
        //        .ToList();

        //    if (validKeys.Count == 0)
        //        return BadRequest("No valid API keys found");

        //    // 2. Phân phối email theo quota
        //    var selectedKeys = new List<(string KeyId, string RealKey, List<string> AssignedEmails)>();
        //    int emailIndex = 0;

        //    foreach (var key in validKeys)
        //    {
        //        int assignCount = Math.Min(key.CanUse, emails.Count - emailIndex);
        //        if (assignCount <= 0) break;

        //        var assignedEmails = emails.Skip(emailIndex).Take(assignCount).ToList();
        //        selectedKeys.Add((key.KeyId, key.RealKey, assignedEmails));
        //        emailIndex += assignCount;

        //        if (emailIndex >= emails.Count) break;
        //    }

        //    if (emailIndex < emails.Count)
        //        return BadRequest("Not enough API quota available to process all emails.");

        //    // 3. Kiểm tra email song song
        //    var results = new ConcurrentBag<object>();
        //    var semaphore = new SemaphoreSlim(13);
        //    var allTasks = new List<Task>();

        //    foreach (var (keyId, realKey, emailsToCheck) in selectedKeys)
        //    {
        //        foreach (var email in emailsToCheck)
        //        {
        //            allTasks.Add(Task.Run(async () =>
        //            {
        //                await semaphore.WaitAsync(token);
        //                try
        //                {
        //                    var json = await CheckEmailAsync(email, realKey, token);
        //                    results.Add(new { email, response = json });
        //                }
        //                finally
        //                {
        //                    semaphore.Release();
        //                }
        //            }));
        //        }
        //    }

        //    await Task.WhenAll(allTasks);

        //    var validEmails = results
        //         .Cast<object>()
        //         .Select(r =>
        //         {
        //             var props = r.GetType().GetProperties();
        //             var email = props.First(p => p.Name == "email").GetValue(r)?.ToString();
        //             var responseStr = props.First(p => p.Name == "response").GetValue(r)?.ToString();

        //             if (responseStr == "ok")
        //             {
        //                 return email;
        //             }
        //             return null;
        //         })
        //         .Where(email => !string.IsNullOrWhiteSpace(email))
        //         .ToList();


        //    // 4. Cập nhật usage cho từng key bằng Lua
        //    var updateUsageScript = @"
        //                            for i = 1, #KEYS do
        //                                local keyId = KEYS[i]
        //                                local inc = tonumber(ARGV[i])
        //                                local json = redis.call('HGET', 'apikey:data', keyId)
        //                                local data = cjson.decode(json)

        //                                data['usage_count'] = (tonumber(data['usage_count']) or 0) + inc
        //                                redis.call('HSET', 'apikey:data', keyId, cjson.encode(data))
        //                            end
        //                            return 'OK'
        //                        ";

        //    var redisKeys = selectedKeys.Select(k => (RedisKey)k.KeyId).ToArray();
        //    var usageCounts = selectedKeys.Select(k => (RedisValue)k.AssignedEmails.Count).ToArray();
        //    await db.ScriptEvaluateAsync(updateUsageScript, redisKeys, usageCounts);
        //    // 5. Cập nhật quota_used cho customer
        //    customer.quota_used += emails.Count;
        //    await db.HashSetAsync(customerKey, customerField, JsonConvert.SerializeObject(customer));


        //    // 6. Ghi log sử dụng theo ngày, chỉ cập nhật nếu đúng ngày hiện tại
        //    string logKey = "customer:usage:log";
        //    string logField = $"customer_log_{request.CustomerApiKey}";
        //    string today = DateTime.UtcNow.ToString("dd/MM/yyyy");

        //    var rawLog = await db.HashGetAsync(logKey, logField);

        //    CustomerDailyUsageLog usageLog;

        //    if (rawLog.HasValue)
        //    {
        //        usageLog = JsonConvert.DeserializeObject<CustomerDailyUsageLog>(rawLog!) ?? new();
        //    }
        //    else
        //    {
        //        usageLog = new CustomerDailyUsageLog
        //        {
        //            customer_api_key = request.CustomerApiKey,
        //            logs = new Dictionary<string, CustomerUsageDetail>()
        //        };
        //    }

        //    // cập nhật hoặc tạo mới theo ngày
        //    if (usageLog.logs.TryGetValue(today, out var detail))
        //    {
        //        detail.total_checked += emails.Count;
        //        detail.total_ok += validEmails.Count;
        //    }
        //    else
        //    {
        //        usageLog.logs[today] = new CustomerUsageDetail
        //        {
        //            total_checked = emails.Count,
        //            total_ok = validEmails.Count
        //        };
        //    }

        //    // lưu lại
        //    await db.HashSetAsync(logKey, logField, JsonConvert.SerializeObject(usageLog));

        //    stopwatch.Stop();
        //    return Ok(new
        //    {
        //        totalChecked = emails.Count,
        //        usedKeys = selectedKeys.Count,
        //        durationSeconds = stopwatch.Elapsed.TotalSeconds,
        //        results = results.ToList()
        //    });
        //}


        [HttpPost("query")]
        public async Task<IActionResult> QueryEmails([FromBody] EmailQueryRequest request, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            List<EmailQueryRequest> users = new List<EmailQueryRequest>
                {
                    new EmailQueryRequest { sheetName = "hadtt", CustomerApiKey = "9a7f8b10-8c3d-437b-9482-362b56a4106a" },
                    new EmailQueryRequest { sheetName = "tranght", CustomerApiKey = "bd427722-ebba-4da2-99e3-d091065f9ec5" },
                    new EmailQueryRequest { sheetName = "hoahp", CustomerApiKey = "313f2ed9-e776-47b3-acc1-511a021b8b7c" },
                    new EmailQueryRequest { sheetName = "huyennk", CustomerApiKey = "81f3116c-c19e-4c96-82ca-a44d339b3141" },
                };

            var user = users.FirstOrDefault(x => x.sheetName == request.sheetName);
            if (user == null)
                return BadRequest("Invalid user");
            request.CustomerApiKey = user.CustomerApiKey;

            if (string.IsNullOrWhiteSpace(request.CustomerApiKey))
                return Unauthorized("Missing API key");

            var db = _redis.GetDatabase();
            string customerKey = "customer:data";
            string customerField = $"customer_{request.CustomerApiKey}";

            var customerJson = await db.HashGetAsync(customerKey, customerField);
            if (customerJson.IsNullOrEmpty)
                return NotFound("Customer not found");

            var customer = JsonConvert.DeserializeObject<CustomerInfo>(customerJson!)!;
            if (customer.isActive != "1")
                return Unauthorized("Customer is not active");

            if (request.Names == null || request.Names.Count == 0 || string.IsNullOrWhiteSpace(request.Domain))
                return BadRequest("Full name and domain are required");

            var emails = new List<string>();
            foreach (var fullName in request.Names)
            {
                if (string.IsNullOrWhiteSpace(fullName))
                    return BadRequest("Full name cannot be empty");

                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var lastName = parts.Length > 0 ? parts[0] : "";
                var firstName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";

                emails.AddRange(GenerateEmails(firstName, lastName, request.Domain, request.DomainCraw));
            }

            emails = emails.Distinct().ToList();
            if (emails.Count == 0)
                return BadRequest("No valid emails generated");

            // 1. Lấy danh sách các key còn quota bằng Lua Script
            var luaGetKeys = @"
        local rawKeys = redis.call('HVALS', 'apikey:data')
        local result = {}

        for i = 1, #rawKeys do
            local data = cjson.decode(rawKeys[i])
            local usage = tonumber(data['usage_count']) or 0
            local max = tonumber(data['max_limit']) or 0
            local isactive = data['isactive']
            if isactive == '1' and usage < max then
                table.insert(result, cjson.encode({
                    keyId = data['key_id'],
                    realKey = data['key'],
                    canUse = max - usage
                }))
            end
        end
        return result
    ";

            var rawKeys = await db.ScriptEvaluateAsync(luaGetKeys);
            var validKeys = ((RedisResult[])rawKeys)
                .Select(r => JsonConvert.DeserializeObject<ApiKeyInfo>((string)(RedisValue)r))
                .OrderByDescending(k => k.CanUse)
                .ToList();

            if (validKeys.Count == 0)
                return BadRequest("No valid API keys found");

            // 2. Phân phối email theo quota
            var selectedKeys = new List<(string KeyId, string RealKey, List<string> AssignedEmails)>();
            int emailIndex = 0;

            foreach (var key in validKeys)
            {
                int assignCount = Math.Min(key.CanUse, emails.Count - emailIndex);
                if (assignCount <= 0) break;

                var assignedEmails = emails.Skip(emailIndex).Take(assignCount).ToList();
                selectedKeys.Add((key.KeyId, key.RealKey, assignedEmails));
                emailIndex += assignCount;

                if (emailIndex >= emails.Count) break;
            }

            if (emailIndex < emails.Count)
                return BadRequest("Not enough API quota available to process all emails.");

            var distributedEmails = selectedKeys.SelectMany(k => k.AssignedEmails).ToList();

            // 3. Cập nhật quota_used bằng Lua (an toàn cho đa luồng)
            var quotaUpdateScript = @"
            local customerJson = redis.call('HGET', KEYS[1], KEYS[2])
            if not customerJson then
                return {err='Customer not found'}
            end
            local data = cjson.decode(customerJson)
            local current = tonumber(data['quota_used']) or 0
            local total = tonumber(data['quota_total']) or 0
            local increment = tonumber(ARGV[1])

            if current + increment > total then
                return {err='Quota exceeded'}
            end

            data['quota_used'] = current + increment
            redis.call('HSET', KEYS[1], KEYS[2], cjson.encode(data))
            return data['quota_used']
             ";

            try
            {
                var quotaUpdateResult = await db.ScriptEvaluateAsync(quotaUpdateScript,
                    new RedisKey[] { customerKey, customerField },
                    new RedisValue[] { distributedEmails.Count });

                customer.quota_used = (int)(long)quotaUpdateResult;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("Quota exceeded"))
            {
                return Unauthorized("API key quota exceeded");
            }

            // 4. Kiểm tra email song song
            var results = new ConcurrentBag<object>();
            var semaphore = new SemaphoreSlim(13);
            var allTasks = new List<Task>();

            foreach (var (keyId, realKey, emailsToCheck) in selectedKeys)
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
            }

            await Task.WhenAll(allTasks);

            var validEmails = results
                .Cast<object>()
                .Select(r =>
                {
                    var props = r.GetType().GetProperties();
                    var email = props.First(p => p.Name == "email").GetValue(r)?.ToString();
                    var responseStr = props.First(p => p.Name == "response").GetValue(r)?.ToString();

                    return responseStr == "ok" ? email : null;
                })
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .ToList();

            // 5. Cập nhật usage từng key
            var updateUsageScript = @"
                for i = 1, #KEYS do
                    local keyId = KEYS[i]
                    local inc = tonumber(ARGV[i])
                    local json = redis.call('HGET', 'apikey:data', keyId)
                    local data = cjson.decode(json)

                    data['usage_count'] = (tonumber(data['usage_count']) or 0) + inc
                    redis.call('HSET', 'apikey:data', keyId, cjson.encode(data))
                end
                return 'OK'
            ";

            var redisKeys = selectedKeys.Select(k => (RedisKey)k.KeyId).ToArray();
            var usageCounts = selectedKeys.Select(k => (RedisValue)k.AssignedEmails.Count).ToArray();
            await db.ScriptEvaluateAsync(updateUsageScript, redisKeys, usageCounts);

            // 6. Ghi log sử dụng theo ngày
            var logUpdateScript = @"
                        local logJson = redis.call('HGET', KEYS[1], KEYS[2])
                        local usageLog = {}
                        if logJson then usageLog = cjson.decode(logJson) end
                        if not usageLog['customer_api_key'] then usageLog['customer_api_key'] = string.sub(KEYS[2], 14) end
                        if not usageLog['logs'] then usageLog['logs'] = {} end

                        local today = ARGV[1]
                        local checked = tonumber(ARGV[2])
                        local ok = tonumber(ARGV[3])

                        if not usageLog['logs'][today] then
                            usageLog['logs'][today] = { total_checked = 0, total_ok = 0, total_domain = 0 }
                        end

                        usageLog['logs'][today]['total_checked'] = usageLog['logs'][today]['total_checked'] + checked
                        usageLog['logs'][today]['total_ok'] = usageLog['logs'][today]['total_ok'] + ok
                        usageLog['logs'][today]['total_domain'] = (usageLog['logs'][today]['total_domain'] or 0) + 1

                        redis.call('HSET', KEYS[1], KEYS[2], cjson.encode(usageLog))
                        return 'OK'

                        ";

            string logKey = "customer:usage:log";
            string logField = $"customer_log_{request.CustomerApiKey}";
            string today = DateTime.UtcNow.ToString("dd/MM/yyyy");

            await db.ScriptEvaluateAsync(logUpdateScript,
                new RedisKey[] { logKey, logField },
                new RedisValue[] { today, distributedEmails.Count, validEmails.Count });


            stopwatch.Stop();

            return Ok(new
            {
                totalChecked = distributedEmails.Count,
                usedKeys = selectedKeys.Count,
                currentQuotaUsed = customer.quota_used,
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


        //[HttpGet("valid-keys")]
        //public async Task<IActionResult> GetValidRedisKeysAsync()
        //{
        //    var validKeys = new List<ApiKeyInfo>();
        //    int errorCount = 0;
        //    int totalKeysScanned = 0;

        //    try
        //    {
        //        var db = _redis.GetDatabase();
        //        long cursor = 0;

        //        do
        //        {
        //            var result = await db.ExecuteAsync("SCAN", cursor.ToString(), "MATCH", "apikey:*", "COUNT", 5000);
        //            var scanResult = (RedisResult[])result;

        //            cursor = long.Parse((string)scanResult[0]);
        //            var keys = (RedisResult[])scanResult[1];

        //            if (keys.Length == 0) continue;

        //            var keyStrings = keys.Select(k => (string)k).ToArray();
        //            totalKeysScanned += keyStrings.Length;

        //            var tasks = keyStrings.Select(key => db.HashGetAllAsync(key)).ToArray();
        //            var hashResults = await Task.WhenAll(tasks);

        //            for (int i = 0; i < keyStrings.Length; i++)
        //            {
        //                try
        //                {
        //                    var key = keyStrings[i];
        //                    var hash = hashResults[i];

        //                    int maxLimit = 0, usageCount = 0, isActive = 0;
        //                    string value = null;
        //                    foreach (var entry in hash)
        //                    {
        //                        switch (entry.Name.ToString())
        //                        {
        //                            case "key":
        //                                value = entry.Value;
        //                                break;
        //                            case "max_limit":
        //                                int.TryParse(entry.Value, out maxLimit);
        //                                break;
        //                            case "usage_count":
        //                                int.TryParse(entry.Value, out usageCount);
        //                                break;
        //                            case "isactive":
        //                                int.TryParse(entry.Value, out isActive);
        //                                break;
        //                        }
        //                    }

        //                    if (isActive == 1 && usageCount < maxLimit)
        //                    {
        //                        validKeys.Add(new ApiKeyInfo
        //                        {
        //                            Key = key,
        //                            Value = value,
        //                            MaxLimit = maxLimit,
        //                            UsageCount = usageCount,
        //                            IsActive = isActive
        //                        });
        //                    }
        //                }
        //                catch (Exception exKey)
        //                {
        //                    errorCount++;
        //                    Console.WriteLine($"Lỗi xử lý key: {keyStrings[i]}, chi tiết: {exKey.Message}");
        //                    continue;
        //                }
        //            }

        //        } while (cursor != 0);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Lỗi tổng thể khi truy cập Redis: {ex.Message}");
        //    }

        //    return Ok(new ApiKeyResult
        //    {
        //        ValidKeys = validKeys,
        //        ErrorCount = errorCount,
        //        TotalKeysScanned = totalKeysScanned
        //    });
        //}



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


        [HttpGet("usage-stats")]
        public async Task<IActionResult> GetCustomerUsageStats([FromQuery] int customerId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (customerId <= 0)
                return BadRequest("Invalid customerId");

            if (fromDate > toDate)
                return BadRequest("Invalid date range");

            var db = _redis.GetDatabase();
            var days = (toDate.Date - fromDate.Date).Days + 1;

            var keys = new List<RedisKey>();
            var dateList = new List<string>();

            for (int i = 0; i < days; i++)
            {
                var date = fromDate.Date.AddDays(i);
                var redisKey = $"customer:usage:daily:{customerId}:{date:yyyyMMdd}";
                keys.Add(redisKey);
                dateList.Add(date.ToString("yyyy-MM-dd"));
            }

            var values = await db.StringGetAsync(keys.ToArray());

            var result = new List<object>();
            for (int i = 0; i < values.Length; i++)
            {
                int count = 0;
                if (values[i].HasValue && int.TryParse(values[i], out var val))
                {
                    count = val;
                }

                result.Add(new
                {
                    date = dateList[i],
                    usedKeys = count
                });
            }

            return Ok(new
            {
                customerId,
                from = fromDate.ToString("yyyy-MM-dd"),
                to = toDate.ToString("yyyy-MM-dd"),
                stats = result
            });
        }


        // API: Trả về quota của toàn bộ customer
        [HttpGet("quotas")]
        public async Task<IActionResult> GetAllCustomerQuotas()
        {
            var db = _redis.GetDatabase();
            var keys = await db.HashKeysAsync("customer:data");
            var result = new List<object>();

            foreach (var key in keys)
            {
                var raw = await db.HashGetAsync("customer:data", key);
                if (!raw.IsNullOrEmpty)
                {
                    var customer = JsonConvert.DeserializeObject<CustomerInfo>(raw);
                    result.Add(new
                    {
                        customer_api_key = customer.api_key,
                        quota_total = customer.quota_total,
                        quota_used = customer.quota_used
                    });
                }
            }

            return Ok(result);
        }

        // API: Trả về quota của 1 customer
        [HttpGet("quota/{apiKey}")]
        public async Task<IActionResult> GetQuota(string apiKey)
        {
            var db = _redis.GetDatabase();
            var field = $"customer_{apiKey}";
            var raw = await db.HashGetAsync("customer:data", field);
            if (raw.IsNullOrEmpty) return NotFound();

            var customer = JsonConvert.DeserializeObject<CustomerInfo>(raw);
            return Ok(new
            {
                customer_api_key = customer.api_key,
                quota_total = customer.quota_total,
                quota_used = customer.quota_used
            });
        }

        // API: Thống kê sử dụng theo ngày của tất cả customer
        [HttpGet("logs")]
        public async Task<IActionResult> GetAllLogs()
        {
            var db = _redis.GetDatabase();
            var keys = await db.HashKeysAsync("customer:usage:log");
            var result = new List<object>();

            foreach (var key in keys)
            {
                var raw = await db.HashGetAsync("customer:usage:log", key);
                if (!raw.IsNullOrEmpty)
                {
                    var log = JsonConvert.DeserializeObject<CustomerDailyUsageLog>(raw);
                    result.Add(log);
                }
            }

            return Ok(result);
        }

        // API: Thống kê theo ngày của 1 customer
        [HttpGet("logs/{apiKey}")]
        public async Task<IActionResult> GetLogByKey(string apiKey)
        {
            var db = _redis.GetDatabase();
            var field = $"customer_log_{apiKey}";
            var raw = await db.HashGetAsync("customer:usage:log", field);
            if (raw.IsNullOrEmpty) return NotFound();

            var log = JsonConvert.DeserializeObject<CustomerDailyUsageLog>(raw);
            return Ok(log);
        }


        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var lua = @"
                        local hash_key = KEYS[1]
                        local fields = redis.call('HKEYS', hash_key)
                        local total = 0
                        local used = 0

                        for _, field in ipairs(fields) do
                            local val = redis.call('HGET', hash_key, field)
                            local ok, data = pcall(cjson.decode, val)
                            if ok then
                                total = total + (tonumber(data['quota_total']) or 0)
                                used = used + (tonumber(data['quota_used']) or 0)
                            end
                        end

                        return {total, used, total - used}
                    ";

                var db = _redis.GetDatabase();
                var result = await db.ScriptEvaluateAsync(lua, new RedisKey[] { "customer:data" });
                var redisResults = (RedisResult[])result;

                return Ok(new QuotaSummary
                {
                    Total = (int)redisResults[0],
                    Used = (int)redisResults[1],
                    Remaining = (int)redisResults[2]
                });

            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("log/used-today")]
        public async Task<IActionResult> GetTotalUsedToday()
        {
            try
            {
                var today = DateTime.UtcNow.ToString("dd/MM/yyyy"); // Hoặc dùng local time nếu cần

                var lua = @"
            local hash_key = KEYS[1]
            local today = ARGV[1]
            local fields = redis.call('HKEYS', hash_key)
            local total = 0

            for _, field in ipairs(fields) do
                local val = redis.call('HGET', hash_key, field)
                local ok, data = pcall(cjson.decode, val)
                if ok and data['logs'] and data['logs'][today] then
                    total = total + (tonumber(data['logs'][today]['total_checked']) or 0)
                end
            end

            return total
        ";

                var db = _redis.GetDatabase();
                var result = await db.ScriptEvaluateAsync(lua,
                    new RedisKey[] { "customer:usage:log" },
                    new RedisValue[] { today });

                int totalUsedToday = (int)result;

                return Ok(new { Date = today, TotalUsedToday = totalUsedToday });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Redis log error: {ex.Message}");
            }
        }
        [HttpGet("log/usage-detail")]
        public async Task<IActionResult> GetUsageDetailPerUser()
        {
            try
            {
                var db = _redis.GetDatabase();
                var today = DateTime.UtcNow.ToString("dd/MM/yyyy");

                var luaScript = @"
                            local function safe_tonumber(value)
                            if type(value) == 'number' then
                                return value
                            elseif type(value) == 'string' then
                                local n = tonumber(value)
                                if n then return n else return 0 end
                            else
                                return 0
                            end
                        end

                        local log_hash_key = KEYS[1]
                        local profile_hash_key = KEYS[2]
                        local today = ARGV[1]

                        local log_fields = redis.call('HKEYS', log_hash_key)
                        local result = {}

                        for _, field in ipairs(log_fields) do
                            local log_val = redis.call('HGET', log_hash_key, field)
                            local ok1, log_data = pcall(cjson.decode, log_val)
                            local api_key = string.gsub(field, 'customer_log_', '')
                            local profile_val = redis.call('HGET', profile_hash_key, 'customer_' .. api_key)
                            local ok2, profile_data = pcall(cjson.decode, profile_val)

                            local total_checked = 0
                            local total_ok = 0
                            local total_domain = 0

                            if ok1 and log_data['logs'] and log_data['logs'][today] then
                                total_checked = safe_tonumber(log_data['logs'][today]['total_checked'])
                                total_ok = safe_tonumber(log_data['logs'][today]['total_ok'])
                                total_domain = safe_tonumber(log_data['logs'][today]['total_domain']) -- thêm dòng này
                            end

                            if ok2 then
                                local email = profile_data['email'] or ''
                                local quota = safe_tonumber(profile_data['quota_total'])

                                table.insert(result, cjson.encode({
                                    email = email,
                                    quota_total = quota,
                                    total_checked = total_checked,
                                    total_ok = total_ok,
                                    total_domain = total_domain -- thêm dòng này
                                }))
                            end
                        end

                        return result

                                ";

                var redisResult = await db.ScriptEvaluateAsync(luaScript,
                    new RedisKey[] { "customer:usage:log", "customer:data" },
                    new RedisValue[] { today });

                var resultArray = (RedisResult[])redisResult;

                var list = resultArray
                    .Select(r => JsonConvert.DeserializeObject<UsageLogPerUser>(r.ToString()))
                    .ToList();

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Redis script error: {ex.Message}");
            }
        }

        [HttpGet("log/key-usage-optimized")]
        public async Task<IActionResult> GetKeyUsageOptimized()
        {
            try
            {
                var db = _redis.GetDatabase();

                var lua = @"local key = KEYS[1]
                            local data = redis.call('HVALS', key)

                            local result = {}

                            for _, raw in ipairs(data) do
                                local ok, obj = pcall(cjson.decode, raw)
                                if ok and obj['logs'] then
                                    for date_str, v in pairs(obj['logs']) do
                                        local total = tonumber(v['total_checked']) or 0
                                        table.insert(result, cjson.encode({ date = date_str, total = total }))
                                    end
                                end
                            end

                            return result
                            ";
                var result = await db.ScriptEvaluateAsync(lua, new RedisKey[] { "customer:usage:log" });

                var rawLogs = ((RedisResult[])result)
                    .Select(x => JsonConvert.DeserializeObject<LogRaw>(x.ToString()))
                    .ToList();

                var daily = new Dictionary<string, int>();
                var weekly = new Dictionary<string, int>();
                var monthly = new Dictionary<string, int>();

                foreach (var log in rawLogs)
                {
                    if (!DateTime.TryParseExact(log.Date, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var date))
                        continue;

                    var dayKey = date.ToString("dd/MM");
                    var weekKey = FirstDayOfWeek(date).ToString("dd/MM");
                    var monthKey = new DateTime(date.Year, date.Month, 1).ToString("dd/MM");

                    daily[dayKey] = daily.GetValueOrDefault(dayKey, 0) + log.Total;
                    weekly[weekKey] = weekly.GetValueOrDefault(weekKey, 0) + log.Total;
                    monthly[monthKey] = monthly.GetValueOrDefault(monthKey, 0) + log.Total;
                }

                var final = new List<KeyUsageStat>();

                final.AddRange(daily.Select(kv => new KeyUsageStat { Name = kv.Key, Daily = kv.Value }));
                final.AddRange(weekly.Select(kv => new KeyUsageStat { Name = kv.Key, Weekly = kv.Value }));
                final.AddRange(monthly.Select(kv => new KeyUsageStat { Name = kv.Key, Monthly = kv.Value }));

                final = final.OrderBy(x => x.Name).ToList();

                return Ok(final);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Redis script error: {ex.Message}");
            }
        }

        [HttpGet("stats/staff")]
        public async Task<IActionResult> GetStaffUsageToday()
        {
            var db = _redis.GetDatabase();

            var luaScript = @"
                    local customers = redis.call('HGETALL', KEYS[1])
                    local logs = redis.call('HGETALL', KEYS[2])
                    local today = ARGV[1]

                    local result = {}

                    for i = 1, #customers, 2 do
                        local field = customers[i]
                        local rawCustomer = customers[i + 1]
                        local customer = cjson.decode(rawCustomer)

                        local apiKey = string.gsub(field, 'customer_', '')
                        local logField = 'customer_log_' .. apiKey
                        local rawLog = nil

                        for j = 1, #logs, 2 do
                            if logs[j] == logField then
                                rawLog = logs[j + 1]
                                break
                            end
                        end

                        local checkedToday = 0
                        local okToday = 0
                        local domainToday = 0

                        if rawLog then
                            local logData = cjson.decode(rawLog)
                            local log = logData.logs and logData.logs[today]
                            if log then
                                checkedToday = log.total_checked or 0
                                okToday = log.total_ok or 0
                                domainToday = log.total_domain or 0 -- 👈 Thêm dòng này
                            end
                        end

                        table.insert(result, cjson.encode({
                            apiKey = apiKey,
                            quotaTotal = tonumber(customer.quota_total) or 0,
                            quotaUsed = tonumber(customer.quota_used) or 0,
                            checkedToday = checkedToday,
                            okToday = okToday,
                            totalDomain = domainToday -- 👈 Thêm dòng này
                        }))
                    end

                    return result
                ";

            var today = DateTime.UtcNow.ToString("dd/MM/yyyy");
            var redisResult = await db.ScriptEvaluateAsync(luaScript,
                new RedisKey[] { "customer:data", "customer:usage:log" },
                new RedisValue[] { today });

            var rawList = (RedisResult[])redisResult;

            // ánh xạ apiKey -> tên sheet
            var apiKeyToName = new Dictionary<string, string>
            {
                ["9a7f8b10-8c3d-437b-9482-362b56a4106a"] = "hadtt",
                ["bd427722-ebba-4da2-99e3-d091065f9ec5"] = "tranght",
                ["313f2ed9-e776-47b3-acc1-511a021b8b7c"] = "hoahp",
                ["81f3116c-c19e-4c96-82ca-a44d339b3141"] = "huyennk"
            };

            var results = rawList
                .Select(r => JsonConvert.DeserializeObject<StaffUsageResult>((string)(RedisValue)r))
                .Select(r =>
                {
                    double percent = r.quotaTotal == 0 ? 0 : Math.Round((double)r.quotaUsed / r.quotaTotal * 100, 2);
                    string status;
                    if (percent == 0)
                        status = "🔹 Chưa sử dụng";
                    else if (percent < 50)
                        status = "🟢 Còn nhiều quota";
                    else if (percent < 90)
                        status = "🟡 Gần hết quota";
                    else if (percent < 100)
                        status = "🟠 Sắp hết quota";
                    else
                        status = "❌ Hết quota";
                    return new
                    {
                        sheetName = apiKeyToName.TryGetValue(r.apiKey, out var name) ? name : r.apiKey,
                        r.checkedToday,
                        r.okToday,
                        r.quotaTotal,
                        r.quotaUsed,
                        r.totalDomain,
                        quotaRemaining = r.quotaTotal - r.quotaUsed,
                        percentUsed = $"{percent}%",
                        status = status
                    };
                })
                .ToList();

            return Ok(results);
        }



        [HttpGet("stats/all-days")]
        public async Task<IActionResult> GetAllUsageByDay()
        {
            var db = _redis.GetDatabase();

            // Bước 1: Gọi Lua script lấy toàn bộ usage
            var luaScript = @"
                        local customers = redis.call('HGETALL', KEYS[1])
                        local logs = redis.call('HGETALL', KEYS[2])

                        local result = {}

                        for i = 1, #customers, 2 do
                            local field = customers[i]
                            local rawCustomer = customers[i + 1]
                            local customer = cjson.decode(rawCustomer)

                            local apiKey = string.gsub(field, 'customer_', '')
                            local logField = 'customer_log_' .. apiKey
                            local rawLog = nil

                            for j = 1, #logs, 2 do
                                if logs[j] == logField then
                                    rawLog = logs[j + 1]
                                    break
                                end
                            end

                            local usageByDate = {}

                            if rawLog then
                                local logData = cjson.decode(rawLog)
                                for date, log in pairs(logData.logs or {}) do
                                    table.insert(usageByDate, {
                                        date = date,
                                        ok = log.total_ok or 0,
                                        checked = log.total_checked or 0,
                                        domain = log.total_domain or 0
                                    })
                                end
                            end

                            table.insert(result, cjson.encode({
                                apiKey = apiKey,
                                usageByDate = usageByDate
                            }))
                        end

                        return result
                    ";

            var usageRawList = await db.ScriptEvaluateAsync(luaScript,
                new RedisKey[] { "customer:data", "customer:usage:log" },
                Array.Empty<RedisValue>());

            var usageList = ((RedisResult[])usageRawList)
                .Select(r => JsonConvert.DeserializeObject<StaffDailyUsageResult>((string)(RedisValue)r))
                .ToList();

            // Bước 2: ánh xạ API key -> tên nhân viên
            var apiKeyToName = new Dictionary<string, string>
            {
                ["9a7f8b10-8c3d-437b-9482-362b56a4106a"] = "hadtt",
                ["bd427722-ebba-4da2-99e3-d091065f9ec5"] = "tranght",
                ["313f2ed9-e776-47b3-acc1-511a021b8b7c"] = "hoahp",
                ["81f3116c-c19e-4c96-82ca-a44d339b3141"] = "huyennk"
            };

            var now = DateTime.UtcNow; // hoặc DateTime.Now nếu theo giờ VN
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);


            // Bước 3: Gom nhóm theo ngày
            var groupedByDate = usageList
             .SelectMany(user =>
                 user.usageByDate.Select(day => new
                 {
                     date = day.date,
                     parsedDate = DateTime.TryParseExact(day.date, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var dt) ? dt : (DateTime?)null,
                     record = new
                     {
                         name = apiKeyToName.TryGetValue(user.apiKey, out var name) ? name : user.apiKey,
                         companyChecked = day.domain,
                         emailsOk = day.ok,
                         creditsUsed = day.checkedCount
                     }
                 }))
             .Where(x => x.parsedDate != null && x.parsedDate >= startOfMonth && x.parsedDate <= endOfMonth)
             .GroupBy(x => x.date)
             .OrderBy(g => DateTime.ParseExact(g.Key, "dd/MM/yyyy", null))
             .Select(g =>
             {
                 var records = g.Select(x => x.record).ToList();
                 var totalCompanyChecked = records.Sum(r => r.companyChecked);

                 return new
                 {
                     date = g.Key,
                     totalCompanyChecked,
                     records
                 };
             })
             .ToList();

            return Ok(groupedByDate);
        }



        [HttpGet("keys/available")]
        public async Task<IActionResult> GetAvailableKeys()
        {
            var db = _redis.GetDatabase();

            var luaScript = @"
                        local rawKeys = redis.call('HVALS', KEYS[1])
                        local result = {}

                        for i = 1, #rawKeys do
                            local data = cjson.decode(rawKeys[i])
                            local isActive = data['isactive']
                            local usage = tonumber(data['usage_count']) or 0
                            local max = tonumber(data['max_limit']) or 0

                            if isActive == '1' and usage < max then
                                table.insert(result, cjson.encode({
                                    key_id = data['key_id'],
                                    real_key = data['key'],
                                    remaining = max - usage
                                }))
                            end
                        end

                        return result
                    ";

            var redisResult = await db.ScriptEvaluateAsync(luaScript,
                new RedisKey[] { "apikey:data" }, new RedisValue[] { });

            var keys = ((RedisResult[])redisResult)
                .Select(r => JsonConvert.DeserializeObject<AvailableKey>((string)(RedisValue)r))
                .ToList();

            keys = keys.OrderByDescending(x => x.key_index)
                                 .ToList();
            var totalremaining = keys.Sum(x => x.remaining);

            return Ok(keys);
        }

        

    //    [Microsoft.AspNetCore.Mvc.HttpDelete("keys/expired")]
    //    public async Task<IActionResult> DeleteExpiredKeys()
    //    {
    //        var db = _redis.GetDatabase();

    //        var luaScript = @"
    //    local allKeys = redis.call('HGETALL', KEYS[1])
    //    local deletedCount = 0

    //    for i = 1, #allKeys, 2 do
    //        local field = allKeys[i]
    //        local rawJson = allKeys[i + 1]
    //        local ok, data = pcall(cjson.decode, rawJson)

    //        if ok then
    //            local isactive = data['isactive']
    //            local usage = tonumber(data['usage_count']) or 0
    //            local max = tonumber(data['max_limit']) or 0

    //            if isactive ~= '1' or usage >= max then
    //                redis.call('HDEL', KEYS[1], field)
    //                deletedCount = deletedCount + 1
    //            end
    //        end
    //    end

    //    return deletedCount
    //";

    //        var deleted = await db.ScriptEvaluateAsync(luaScript, new RedisKey[] { "apikey:data" }, Array.Empty<RedisValue>());
    //        return Ok(new { deleted = (int)(long)deleted });
    //    }



        private static DateTime FirstDayOfWeek(DateTime dt)
        {
            int diff = dt.DayOfWeek - DayOfWeek.Sunday;
            if (diff < 0) diff += 7;
            return dt.AddDays(-1 * diff).Date;
        }

    }
}
