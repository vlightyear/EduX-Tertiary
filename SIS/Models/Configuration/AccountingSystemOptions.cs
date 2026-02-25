using SIS.Models.Accounting;

namespace SIS.Models.Configuration
{
    public class AccountingSystemOptions
    {
        public const string SectionName = "AccountingSystem";

        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string DefaultCCode { get; set; } = string.Empty;
        public RegistrationFeeOptions RegistrationFee { get; set; } = new();
        public CustomerDefaults CustomerDefaults { get; set; } = new();
        public InvoiceDefaults InvoiceDefaults { get; set; } = new();

        public PaymentDefaults PaymentDefaults { get; set; } = new();
    }

    public class RegistrationFeeOptions
    {
        public string CreditNCode { get; set; } = string.Empty;
        public string DebitNCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class CustomerDefaults
    {
        public string TaxRegistrationNumber { get; set; } = "XXXX-XXXX";
        public decimal CreditScore { get; set; } = 0;
        public decimal CBalance { get; set; } = 0;
        public decimal CBalanceC { get; set; } = 0;
        public BankDetail DefaultBankDetails { get; set; } = new();
    }

    public class InvoiceDefaults
    {
        public string CreditNCode { get; set; } = "10000001";
        public string DebitNCode { get; set; } = "60000001";
        public string InvoicePrefix { get; set; } = "INV";
    }

    public class PaymentDefaults
    {
        public string CreditNCode { get; set; } = "60000001";
        public string DebitNCode { get; set; } = "60000017";
    }
}
