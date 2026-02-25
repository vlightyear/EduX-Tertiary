// Services/Accounting/IStudentInvoiceService.cs
using SIS.Models.Accounting;
using SIS.Models.Fees;
using SIS.Models.StudentApplication;

namespace SIS.Services.Accounting
{
    public interface IStudentInvoiceService
    {
        Task<AccountingApiResponse> GenerateStudentInvoiceAsync(int studentId);
        Task<AccountingApiResponse> GenerateStudentInvoiceAsync(Student student); // Add overload for efficiency
    }
}