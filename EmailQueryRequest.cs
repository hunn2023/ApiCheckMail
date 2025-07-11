namespace ApiCheckMail
{
    public class EmailQueryRequest
    {
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

    public class CustomerUsageJsonLog
    {
        public string customer_api_key { get; set; }
        public string date { get; set; }
        public int email_check { get; set; }
    }

}
