using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.StudentResults
{
    /// <summary>
    /// Represents individual assessment scores for students
    /// Replaces JSON storage with proper relational structure
    /// </summary>
    public class StudentAssessmentScore
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public int AcademicYearId { get; set; }

        [Required]
        public int AssessmentId { get; set; }

        /// <summary>
        /// The actual score achieved by the student
        /// Range: 0-100 (percentage or marks out of 100)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal Score { get; set; }

        /// <summary>
        /// Maximum possible score for this assessment
        /// Default is 100, but can be customized
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal MaxScore { get; set; } = 100;

        /// <summary>
        /// Weight of this assessment in final grade calculation
        /// Range: 0-100 (percentage)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal WeightPercentage { get; set; }

        /// <summary>
        /// User ID of the person who recorded this score
        /// </summary>
        [Required]
        [MaxLength(450)]
        public string RecordedBy { get; set; }

        /// <summary>
        /// Timestamp when the score was first recorded
        /// </summary>
        [Required]
        public DateTime RecordedAt { get; set; }

        /// <summary>
        /// User ID of the person who last modified this score
        /// Nullable - only set if score has been modified
        /// </summary>
        [MaxLength(450)]
        public string? ModifiedBy { get; set; }

        /// <summary>
        /// Timestamp of last modification
        /// Nullable - only set if score has been modified
        /// </summary>
        public DateTime? ModifiedAt { get; set; }

        /// <summary>
        /// SHA256 hash for data integrity verification
        /// Format: SHA256(StudentId|CourseId|AssessmentId|Score|WeightPercentage|Salt)
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string ScoreHash { get; set; }

        /// <summary>
        /// Soft delete flag - allows logical deletion without losing audit trail
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Semester in which this assessment was taken (1 or 2)
        /// </summary>
        [Required]
        [Range(1, 2)]
        public int Semester { get; set; }
        
        public int? rsbId { get; set; }
        [ForeignKey("rsbId")]
        public ResultSubmissionBatch ResultSubmissionBatch { get; set; }
        public int? Attempt { get; set; } = 1;
        public int? YearOfStudy { get; set; } = 0;

        /// <summary>
        /// Optional remarks or notes about this score
        /// e.g., "Adjusted for illness", "Makeup exam"
        /// </summary>
        [MaxLength(500)]
        public string? Remarks { get; set; }

        // Navigation Properties
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [ForeignKey("AcademicYearId")]
        public virtual AcademicYear AcademicYear { get; set; }

        [ForeignKey("AssessmentId")]
        public virtual Assessment Assessment { get; set; }

        [ForeignKey("RecordedBy")]
        public virtual ApplicationUser RecordedByUser { get; set; }

        [ForeignKey("ModifiedBy")]
        public virtual ApplicationUser? ModifiedByUser { get; set; }
    }
}