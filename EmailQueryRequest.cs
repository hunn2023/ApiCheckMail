using Newtonsoft.Json;

namespace ApiCheckMail
{
    public class EmailQueryRequest
    {
        public string sheetName { get; set; }

        public string CustomerApiKey { get; set; }  

        public List<string> Names { get; set; }

        public string Domain { get; set; }

        public string DomainCraw { get; set; }
    }

    public class ApiKeyInfo
    {
        public string KeyId { get; set; }
        public string RealKey { get; set; }
        public int CanUse { get; set; }
    }

    public class ApiKeyResult
    {
        public int ValidCount => ValidKeys?.Count ?? 0;
        public List<ApiKeyInfo> ValidKeys { get; set; }
        public int ErrorCount { get; set; }
        public int TotalKeysScanned { get; set; }
    }

    public class CustomerInfo
    {
        public string email { get; set; }
        public string api_key { get; set; }
        public string isActive { get; set; }
        public int quota_total { get; set; }
        public int quota_used { get; set; }
    }

    public class CustomerDailyUsageLog
    {
        public string customer_api_key { get; set; }
        public Dictionary<string, CustomerUsageDetail> logs { get; set; } = new();
    }

    public class CustomerUsageDetail
    {
        public int total_checked { get; set; }
        public int total_ok { get; set; }
    }


    public class QuotaSummary
    {
        public int Total { get; set; }
        public int Used { get; set; }
        public int Remaining { get; set; }
    }

    public class UsageLogPerUser
    {
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("quota_total")]
        public int QuotaTotal { get; set; }

        [JsonProperty("total_checked")]
        public int TotalChecked { get; set; }

        [JsonProperty("total_ok")]
        public int TotalOk { get; set; }
    }

    public class KeyUsageStat
    {
        public string Name { get; set; }
        public int? Daily { get; set; }
        public int? Weekly { get; set; }
        public int? Monthly { get; set; }
    }

}
