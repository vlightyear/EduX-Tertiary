using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Appeals
{
    /// <summary>
    /// Represents a student's appeal for results review
    /// </summary>
    public class ResultAppeal : AuditClass
    {
        [Key]
        public int Id { get; set; }

        // Student Information
        [Required(ErrorMessage = "Student is required.")]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public Student? Student { get; set; }

        // Course Information
        [Required(ErrorMessage = "Course is required.")]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }

        // Academic Period
        [Required(ErrorMessage = "Academic Year is required.")]
        public int AcademicYearId { get; set; }

        [ForeignKey("AcademicYearId")]
        public AcademicYear? AcademicYear { get; set; }

        [Display(Name = "Academic Period")]
        public int? YearPeriodId { get; set; }

        [ForeignKey(nameof(YearPeriodId))]
        public virtual AcademicYearPeriod? YearPeriod { get; set; }

        // Appeal Details
        [Required(ErrorMessage = "Appeal type is required.")]
        [StringLength(50)]
        public string AppealType { get; set; } = string.Empty;
        // Types: Remark, Review, Recalculation, MissingMarks, GradeDispute, Other

        [Required(ErrorMessage = "Reason for appeal is required.")]
        [StringLength(2000)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(500)]
        public string? SupportingDocuments { get; set; } // File paths or references

        // Fee Information (for Remark appeals)
        [Column(TypeName = "decimal(18,2)")]
        public decimal AppealFee { get; set; } = 0;

        public bool FeePaid { get; set; } = false;

        public DateTime? FeePaymentDate { get; set; }

        [StringLength(100)]
        public string? PaymentReference { get; set; }

        // Original and Revised Marks
        [Column(TypeName = "decimal(5,2)")]
        public decimal? OriginalMark { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? RevisedMark { get; set; }

        [StringLength(10)]
        public string? OriginalGrade { get; set; }

        [StringLength(10)]
        public string? RevisedGrade { get; set; }

        // Status Tracking
        // Statuses: Pending, UnderReview, Approved, Rejected, Completed, Cancelled
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        public DateTime SubmissionDate { get; set; } = DateTime.Now.AddHours(2);

        // Response Information
        [StringLength(2000)]
        public string? Response { get; set; }

        public string? ResponseBy { get; set; }

        [ForeignKey("ResponseBy")]
        public ApplicationUser? Responder { get; set; }

        public DateTime? ResponseDate { get; set; }

        // Final Decision
        [StringLength(2000)]
        public string? FinalDecision { get; set; }

        public string? DecisionBy { get; set; }

        [ForeignKey("DecisionBy")]
        public ApplicationUser? DecisionMaker { get; set; }

        public DateTime? DecisionDate { get; set; }

        // Escalation (if needed)
        public bool IsEscalated { get; set; } = false;

        [StringLength(500)]
        public string? EscalationReason { get; set; }

        public DateTime? EscalatedDate { get; set; }

        // Soft delete
        public DateTime? DeletedAt { get; set; }

        // Computed property for display
        [NotMapped]
        public string StatusDisplay => Status switch
        {
            "Pending" => "Pending Review",
            "UnderReview" => "Under Review",
            "Approved" => "Approved",
            "Rejected" => "Rejected",
            "Completed" => "Completed",
            "Cancelled" => "Cancelled",
            _ => Status
        };

        [NotMapped]
        public bool RequiresFee => AppealType == "Remark";

        [NotMapped]
        public bool CanProceed => !RequiresFee || FeePaid;
    }

    /// <summary>
    /// Appeal type configuration including fees
    /// </summary>
    public class AppealTypeConfig
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string TypeCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string TypeName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Fee { get; set; } = 0;

        public bool RequiresFee { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; } = 0;
    }

    /// <summary>
    /// Tracks the history of appeal status changes
    /// </summary>
    public class AppealStatusHistory : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AppealId { get; set; }

        [ForeignKey("AppealId")]
        public ResultAppeal? Appeal { get; set; }

        [Required]
        [StringLength(50)]
        public string FromStatus { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string ToStatus { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Comments { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.Now.AddHours(2);

        public string? ChangedBy { get; set; }

        [ForeignKey("ChangedBy")]
        public ApplicationUser? ChangedByUser { get; set; }
    }
}