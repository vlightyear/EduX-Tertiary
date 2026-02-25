using SIS.Models.Payments;

namespace SIS.Services.Payment
{
    public interface IPaymentReconciliationService
    {
        Task<PaymentReconciliationResult> ReconcileUnprocessedPaymentsAsync();
    }

    public class PaymentReconciliationResult
    {
        public int TotalUnreconciledPayments { get; set; }
        public int SuccessfulReconciliations { get; set; }
        public int FailedReconciliations { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
        public TimeSpan ProcessingTime { get; set; }
        public bool Success => FailedReconciliations == 0 && TotalUnreconciledPayments > 0;
    }
}