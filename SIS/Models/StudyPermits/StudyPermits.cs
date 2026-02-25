using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudyPermits
{
    public class StudyPermit : AuditClass
    {
        public int Id { get; set; }

        // Foreign key to Student
        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        [Required]
        public string PermitNumber { get; set; } = null!;

        [Required]
        public DateTime? IssueDate { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        public string? IssuingAuthority { get; set; }

        public string? PermitDocumentPath { get; set; }

        public bool IsActive { get; set; } = true;

        // Status tracking
        public PermitStatus Status { get; set; } = PermitStatus.Valid;

        // Last notification sent
        public DateTime? LastNotificationSent { get; set; }
    }

    public enum PermitStatus
    {
        Valid,
        ExpiringSoon,
        Expired,
        Renewed,
        Approved,
        Deleted,
        Uploaded
    }

    public class StudyPermitConfig
    {
        public int Id { get; set; }

        // Days before expiry to send notification
        public int DaysBeforeExpiryReminder { get; set; } = 30;

        // Whether auto-reminders are enabled
        public bool EnableEmailReminders { get; set; } = true;
        public bool EnableSmsReminders { get; set; } = false;

        // Optional: CC admin or registrar email for notifications
        public string? AdminEmailCc { get; set; }

        // Optional: Message templates
        public string? EmailTemplate { get; set; }
        public string? SmsTemplate { get; set; }
    }

    public class StudyPermitNotificationLog : AuditClass
    {
        public int Id { get; set; }
        public int StudyPermitId { get; set; }
        public StudyPermit StudyPermit { get; set; } = null!;
        public DateTime NotificationDate { get; set; }
        public string NotificationType { get; set; } = null!; // "Email", "SMS"
        public string Message { get; set; } = null!;
        public bool Success { get; set; }
    }
}
