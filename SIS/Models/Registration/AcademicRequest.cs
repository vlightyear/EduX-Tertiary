using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Registration
{
    public class AcademicRequest : AuditClass
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }

        public int ProgrammeId { get; set; } = 0;

        public int SchoolId { get; set; } = 0;

        [Required]
        [StringLength(100)]
        public string RequestType { get; set; }  // Withdrawal, Programme Change, Exemption, Supplementary Exam, Deferred Exam

        public DateTime RequestDate { get; set; } = DateTime.Now;

        public Status Status { get; set; } = Status.Pending;

        [Required]
        [StringLength(2000)]
        public string Description { get; set; }

        public string? AdminNotes { get; set; }

        // Navigation Properties
        public virtual School? School { get; set; }
        public virtual Programme? Programme { get; set; }
        public virtual Student Student { get; set; }
        public virtual ICollection<AcademicRequestDocument> Documents { get; set; } = new List<AcademicRequestDocument>();
    }
}