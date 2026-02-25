using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.StudentResults
{
    /// <summary>
    /// Audit log for all changes to assessment scores and course results
    /// Provides complete audit trail for compliance and troubleshooting
    /// </summary>
    public class ResultAuditLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Type of entity being audited
        /// Values: "AssessmentScore", "CourseResult"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; }

        /// <summary>
        /// ID of the specific entity being audited
        /// References either StudentAssessmentScore.Id or StudentCourseResult.Id
        /// </summary>
        [Required]
        public int EntityId { get; set; }

        /// <summary>
        /// Student ID for quick filtering and reporting
        /// </summary>
        [Required]
        public int StudentId { get; set; }

        /// <summary>
        /// Course ID for quick filtering and reporting
        /// </summary>
        [Required]
        public int CourseId { get; set; }

        /// <summary>
        /// Academic Year ID for historical tracking
        /// </summary>
        [Required]
        public int AcademicYearId { get; set; }

        /// <summary>
        /// Type of action performed
        /// Values: "Created", "Updated", "Published", "Archived", "Deleted"
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string ActionType { get; set; }

        /// <summary>
        /// JSON representation of the previous state (before change)
        /// Null for "Created" actions
        /// Format: {"score": 75, "grade": "B", ...}
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? OldValue { get; set; }

        /// <summary>
        /// JSON representation of the new state (after change)
        /// Format: {"score": 80, "grade": "A-", ...}
        /// </summary>
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string NewValue { get; set; }

        /// <summary>
        /// User ID of the person who made the change
        /// </summary>
        [Required]
        [MaxLength(450)]
        public string ChangedBy { get; set; }

        /// <summary>
        /// Timestamp when the change occurred
        /// </summary>
        [Required]
        public DateTime ChangedAt { get; set; }

        /// <summary>
        /// IP address from which the change was made
        /// Optional - for security auditing
        /// </summary>
        [MaxLength(45)] // IPv6 max length
        public string? IPAddress { get; set; }

        /// <summary>
        /// Reason for the change (if provided)
        /// e.g., "Correction after remarking", "Data entry error"
        /// </summary>
        [MaxLength(500)]
        public string? Reason { get; set; }

        /// <summary>
        /// User agent/browser information
        /// Optional - for tracking automated vs manual changes
        /// </summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Session ID for grouping related changes
        /// Useful for bulk operations
        /// </summary>
        [MaxLength(100)]
        public string? SessionId { get; set; }

        /// <summary>
        /// Whether this change was part of a batch operation
        /// </summary>
        public bool IsBatchOperation { get; set; } = false;

        /// <summary>
        /// Batch ID if this is part of a batch operation
        /// Links multiple audit entries from the same batch
        /// </summary>
        [MaxLength(100)]
        public string? BatchId { get; set; }

        /// <summary>
        /// Hash of the old value for integrity verification
        /// </summary>
        [MaxLength(64)]
        public string? OldValueHash { get; set; }

        /// <summary>
        /// Hash of the new value for integrity verification
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string NewValueHash { get; set; }

        // Navigation Properties
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [ForeignKey("AcademicYearId")]
        public virtual AcademicYear AcademicYear { get; set; }

        [ForeignKey("ChangedBy")]
        public virtual ApplicationUser ChangedByUser { get; set; }
    }

    /// <summary>
    /// Enum for audit action types
    /// </summary>
    public enum AuditActionType
    {
        Created,
        Updated,
        Published,
        Archived,
        Deleted,
        Recalculated,
        Verified,
        Corrected
    }

    /// <summary>
    /// Enum for entity types being audited
    /// </summary>
    public enum AuditEntityType
    {
        AssessmentScore,
        CourseResult
    }
}