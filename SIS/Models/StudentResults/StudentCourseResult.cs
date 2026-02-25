using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using SIS.Models.StudentResults;

namespace SIS.Models.StudentResults
{
    /// <summary>
    /// Represents the calculated final result for a student in a specific course
    /// Stores computed totals, grades, and pass/fail status
    /// </summary>
    public class StudentCourseResult
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public int AcademicYearId { get; set; }

        /// <summary>
        /// Semester in which this course was taken (1 or 2)
        /// </summary>
        [Required]
        [Range(1, 2)]
        public int Semester { get; set; }

        /// <summary>
        /// Weighted total score before normalization
        /// Sum of (Score × Weight) for all assessments
        /// Range: 0-100
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal WeightedTotal { get; set; }

        /// <summary>
        /// Normalized total score (adjusted to 100 if weights don't sum to 100)
        /// This is the final score used for grading
        /// Range: 0-100
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal NormalizedTotal { get; set; }

        /// <summary>
        /// Letter grade based on NormalizedTotal and GradeConfiguration
        /// e.g., A+, A, B+, B, C, D, F
        /// </summary>
        [Required]
        [MaxLength(5)]
        public string GradeLetter { get; set; }

        /// <summary>
        /// GPA points for this course (e.g., 4.0 for A, 3.5 for B+)
        /// Range: 0-5
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(3,2)")]
        [Range(0, 5)]
        public decimal GradePoints { get; set; }

        /// <summary>
        /// Whether the student passed this course
        /// Based on course-specific pass mark
        /// </summary>
        [Required]
        public bool IsPassed { get; set; }

        /// <summary>
        /// Number of credits for this course
        /// Typically 3, but can vary
        /// </summary>
        [Required]
        [Range(1, 10)]
        public int Credits { get; set; }

        /// <summary>
        /// Credits earned (0 if failed, Credits value if passed)
        /// </summary>
        [Required]
        [Range(0, 10)]
        public int CreditsEarned { get; set; }

        /// <summary>
        /// Current status of this result
        /// Draft: Being calculated/entered
        /// Published: Released to students
        /// Archived: Historical record
        /// </summary>
        [Required]
        public Status Status { get; set; } = Status.Draft;

        /// <summary>
        /// SHA256 hash for data integrity verification
        /// Format: SHA256(StudentId|CourseId|NormalizedTotal|GradeLetter|Credits|Salt)
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string ResultHash { get; set; }

        /// <summary>
        /// User ID of the person who published this result
        /// Nullable - only set when status changes to Published
        /// </summary>
        [MaxLength(450)]
        public string? PublishedBy { get; set; }

        /// <summary>
        /// Timestamp when result was published
        /// Nullable - only set when status changes to Published
        /// </summary>
        public DateTime? PublishedAt { get; set; }

        /// <summary>
        /// Timestamp when this result was calculated
        /// </summary>
        [Required]
        public DateTime CalculatedAt { get; set; }

        /// <summary>
        /// User ID of the person/system that calculated this result
        /// </summary>
        [Required]
        [MaxLength(450)]
        public string CalculatedBy { get; set; }

        /// <summary>
        /// Pass mark for this course (from Course configuration)
        /// Stored here for historical accuracy even if course config changes
        /// Range: 0-100
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal PassMark { get; set; }

        /// <summary>
        /// Total sum of all assessment weights (should typically be 100)
        /// Stored for verification and debugging
        /// Range: 0-100
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal TotalWeightPercentage { get; set; }

        /// <summary>
        /// Number of assessments that contributed to this result
        /// For validation and audit purposes
        /// </summary>
        [Required]
        [Range(1, 20)]
        public int AssessmentCount { get; set; }

        /// <summary>
        /// Optional remarks or notes about this result
        /// e.g., "Special consideration applied", "Reassessment"
        /// </summary>
        [MaxLength(1000)]
        public string? Remarks { get; set; }

        /// <summary>
        /// Whether this is a carryover/repeat attempt
        /// </summary>
        public bool IsCarryover { get; set; } = false;

        /// <summary>
        /// If this is a repeat attempt, the attempt number (1, 2, 3, etc.)
        /// </summary>
        [Range(0, 5)]
        public int AttemptNumber { get; set; } = 1;

        // Navigation Properties
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [ForeignKey("AcademicYearId")]
        public virtual AcademicYear AcademicYear { get; set; }

        [ForeignKey("PublishedBy")]
        public virtual ApplicationUser? PublishedByUser { get; set; }

        [ForeignKey("CalculatedBy")]
        public virtual ApplicationUser CalculatedByUser { get; set; }

        // Collection of individual assessment scores that make up this result
        [NotMapped]
        public virtual ICollection<StudentAssessmentScore> AssessmentScores { get; set; }
    }
}