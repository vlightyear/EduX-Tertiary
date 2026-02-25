using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SIS.Models.Admin
{
    public class Assessment : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Assessment name is required")]
        [StringLength(100)]
        public string Name { get; set; }  // e.g., "Final Exam", "Midterm", "Assignment 1"

        [Required(ErrorMessage = "Assessment type is required")]
        [StringLength(50)]
        public string Type { get; set; }  // e.g., "Exam", "Assignment", "Project", "Quiz"

        [Required(ErrorMessage = "Weight percentage is required")]
        [Range(1, 100, ErrorMessage = "Weight must be between 1 and 100")]
        public int WeightPercentage { get; set; }  // The percentage this assessment contributes to final grade


        [Required(ErrorMessage = "Pass mark is required")]
        [Range(1, 100, ErrorMessage = "Pass mark must be between 1 and 100")]
        [Precision(4, 1)]
        public decimal PassMark { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        // Submission related properties
        public bool RequiresSubmission { get; set; }  // Whether this assessment requires file/document submission

        public DateTime? DueDate { get; set; }  // Optional due date for submissions(Not in use)

        [StringLength(200)]
        public string? SubmissionInstructions { get; set; }

        // Resit/Supplementary properties
        public bool AllowResit { get; set; }  // Whether this assessment can be retaken if failed

        [Range(0, 100, ErrorMessage = "Maximum resit mark must be between 0 and 100")]
        public int? MaximumResitMark { get; set; }  // Maximum mark possible in resit (often capped at 40 or 50)

        // Navigation property for courses using join table
        public virtual ICollection<CourseAssessment> CourseAssessments { get; set; } = new List<CourseAssessment>();

    }
}
