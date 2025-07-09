namespace ApiCheckMail
{
    public class EmailQueryRequest
    {
        public int CustomerId { get; set; }

        public List<string> Names { get; set; }


        public string Domain { get; set; }

        public string DomainCraw { get; set; }
    }
}
