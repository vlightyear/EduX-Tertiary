using SIS.Models.Accounting;
using SIS.Models.Fees;

namespace SIS.Services.Accounting
{
    public interface IAccountingService
    {
        Task<AccountingApiResponse> PostRegistrationFeeAsync(decimal amount, string studentReference);
        Task<AccountingApiResponse> CreateCustomerAsync(string studentId, string fullName, string address, string email, string phone);
        Task<AccountingApiResponse> PostStudentInvoiceAsync(string studentId, string studentName, string address, string email, string phone, decimal totalAmount, List<FeeConfiguration> fees);
        Task<AccountingApiResponse> PostPaymentAsync(string studentId, decimal paymentAmount); 
    }
}
