using SIS.Controllers;

namespace SIS.Models.Payments
{
    public class PaymentHistoryViewModel
    {
        public string StudentName { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public List<PaymentRecord> Payments { get; set; } = new List<PaymentRecord>();
        public decimal TotalPaid { get; set; }
        public List<UnifiedTransactionDto> FinancialStatement { get; set; }
    }

    public class PaymentRecord
    {
        public string TransactionReference { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public decimal OutstandingAmount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
