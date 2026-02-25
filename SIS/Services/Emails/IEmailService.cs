using SIS.Models;
using SIS.Models.StudentApplication;

namespace SIS.Services.Emails
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);
        Task<bool> SendApplicationSubmissionEmailAsync(string applicantName, string applicantEmail,
            string programmeName, string schoolName, string referenceNumber,
            decimal paymentAmount, string transactionReference);


        // Admission process emails
        Task<bool> SendAdmissionEmailAsync(Student student, byte[] admissionLetterPdf);
        Task<bool> SendRejectionEmailAsync(string applicantName, string applicantEmail, string programmeName, string schoolName, string reason);
        Task<bool> SendWaitlistEmailAsync(string applicantName, string applicantEmail, string programmeName, string schoolName, DateTime academicYearStart);


        // User management emails
        Task<bool> SendUserCreationEmailAsync(string fullName, string email, string role, string password, string loginUrl = "https://ecampus.edenuniversity.edu.zm/Account/Login");

        // Password reset email
        Task<bool> SendPasswordResetEmailAsync(string fullName, string email, string newPassword, string loginUrl = "https://ecampus.edenuniversity.edu.zm/Account/Login");

        Task<bool> NotifyApprovalActionAsync(WorkflowInstance wi, WorkflowApproval wa);
        Task<bool> NotifyApprovalRequestAsync(WorkflowInstance wi, WorkflowApproval wa);
        Task<bool> NotifyWorkflowRejectionAsync(WorkflowInstance wi, WorkflowApproval wa);
        Task<bool> NotifyWorkflowCompletionAsync(WorkflowInstance wi);
        Task<bool> NotifyDelegationAsync(int workflowInstanceId, string fromApproverId, string toApproverId, string reason);
    }

}
