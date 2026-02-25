using SIS.Models.StudentApplication;

namespace SIS.Services.Emails
{
    public interface IBackgroundEmailService
    {
        void QueueApplicationSubmissionEmail(string applicantName, string applicantEmail, string programmeName, string schoolName, string referenceNumber, decimal paymentAmount, string transactionReference);
        void QueueAdmissionEmail(Student student);
        void QueueRejectionEmail(string applicantName, string applicantEmail, string programmeName, string schoolName, string reason);
        void QueueWaitlistEmail(string applicantName, string applicantEmail, string programmeName, string schoolName, DateTime academicYearStart);
    }
}
