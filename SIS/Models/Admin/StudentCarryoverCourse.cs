using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;

namespace SIS.Models.Admin
{
    public class StudentCarryoverCourse : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [Required]
        public int OriginalAcademicYearId { get; set; }

        [ForeignKey("OriginalAcademicYearId")]
        public virtual AcademicYear OriginalAcademicYear { get; set; }

        public int? OriginalSemester { get; set; } // For semester-based programs

        [Required]
        [StringLength(50)]
        public string Reason { get; set; } // "Failed", "Incomplete", "Repeat"

        [Required]
        public bool IsActive { get; set; } // False when course is successfully retaken

        public DateTime CarryoverDate { get; set; } // When the carryover was created

        [StringLength(200)]
        public string? Notes { get; set; } // Optional notes about the carryover
    }
}