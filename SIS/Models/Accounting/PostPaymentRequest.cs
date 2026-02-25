namespace SIS.Models.Accounting
{
    public class PostPaymentRequest
    {
        public string CCode { get; set; } = string.Empty;
        public string CreditNCode { get; set; } = string.Empty;
        public string DebitNCode { get; set; } = string.Empty;
        public decimal PaymentAmount { get; set; }
    }
}
