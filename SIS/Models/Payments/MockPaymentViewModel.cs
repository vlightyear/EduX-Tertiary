namespace SIS.Models.Payments
{
    public class MockPaymentViewModel
    {
        public string TransactionReference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal MinRegistrationPayment { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsRegistered { get; set; }

        public decimal OutstandingBalance { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalFees { get; set; }
        public decimal RemainingForRegistration { get; set; }
    }
}
