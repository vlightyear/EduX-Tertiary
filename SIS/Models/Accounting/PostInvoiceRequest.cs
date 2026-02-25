namespace SIS.Models.Accounting
{
    public class PostInvoiceRequest
    {
        public string CCode { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Names { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public List<InvoiceItem> Items { get; set; } = new();
    }

    public class InvoiceItem
    {
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CreditNCode { get; set; } = string.Empty;
        public string DebitNCode { get; set; } = string.Empty;
    }
}
