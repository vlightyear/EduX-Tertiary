namespace SIS.Models.Accounting
{
    public class RegistrationFeeRequest
    {
        public string CCode { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<RegistrationFeeItem> Items { get; set; } = new();
    }

    public class RegistrationFeeItem
    {
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CreditNCode { get; set; } = string.Empty;
        public string DebitNCode { get; set; } = string.Empty;
    }

    // Models/Accounting/AccountingApiResponse.cs
    public class AccountingApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
    }
}
