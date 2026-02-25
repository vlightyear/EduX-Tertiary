namespace SIS.Models.Accounting
{
    public class CreateCustomerRequest
    {
        public string CCode { get; set; } = string.Empty;
        public string CName { get; set; } = string.Empty;
        public string CAddress { get; set; } = string.Empty;
        public string CEmail { get; set; } = string.Empty;
        public string CPhone { get; set; } = string.Empty;
        public string CTexRegistrationNumber { get; set; } = string.Empty;
        public decimal CreditScore { get; set; }
        public decimal CBalance { get; set; }
        public decimal CBalanceC { get; set; }
        public List<BankDetail> BankDetails { get; set; } = new();
    }

    public class BankDetail
    {
        public string BankName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string BankId { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string SortCode { get; set; } = string.Empty;
        public string SwiftCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
    }
}
