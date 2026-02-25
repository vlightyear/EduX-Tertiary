using SIS.Models.StudentApplication;

namespace SIS.Services.Payment
{
    public interface IPaymentService
    {
        void AddApplicationPayment(Applicant applicant);
        void UpdateApplicationPayment(Applicant applicant);
    }
}
