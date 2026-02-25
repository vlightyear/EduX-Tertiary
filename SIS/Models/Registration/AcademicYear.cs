using SIS.Models.Accounts;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Registration
{
    public enum AcademicType
    {
        Annual,
        Semester
    }

    public class AcademicYear
    {
        [Key]
        public int YearId { get; set; } // Primary Key

        [Required]
        public string YearValue { get; set; } // e.g., 2024 or 2024/2025

        [NotMapped]
        public string YearName => YearValue;

        [Required]
        public AcademicType AcademicType { get; set; } = AcademicType.Annual;

        [Required]
        public DateTime StartDate { get; set; } // Overall start date of the academic year

        [Required]
        public DateTime EndDate { get; set; } // Overall end date of the academic year

        public bool IsActive { get; set; }

        [Range(0, 100, ErrorMessage = "Registration payment percentage must be between 0 and 100")]
        [Display(Name = "Minimum Payment % for Registration")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MinRegistrationPaymentPercentage { get; set; }

        [Range(0, 100, ErrorMessage = "Exam payment percentage must be between 0 and 100")]
        [Display(Name = "Minimum Payment % for Exams")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MinExamPaymentPercentage { get; set; }

        // Semester 1 dates (required when AcademicType = Semester)
        [Display(Name = "Semester 1 Start Date")]
        public DateTime? Semester1StartDate { get; set; }

        [Display(Name = "Semester 1 End Date")]
        public DateTime? Semester1EndDate { get; set; }

        // Semester 2 dates (required when AcademicType = Semester)
        [Display(Name = "Semester 2 Start Date")]
        public DateTime? Semester2StartDate { get; set; }

        [Display(Name = "Semester 2 End Date")]
        public DateTime? Semester2EndDate { get; set; }

        // Optional registration period dates
        [Display(Name = "Registration Start Date")]
        public DateTime? RegistrationStartDate { get; set; }

        [Display(Name = "Registration End Date")]
        public DateTime? RegistrationEndDate { get; set; }

        // Optional final exam period dates
        [Display(Name = "Final Exam Start Date")]
        public DateTime? FinalExamStartDate { get; set; }

        [Display(Name = "Final Exam End Date")]
        public DateTime? FinalExamEndDate { get; set; }

        // Optional grade submission period dates
        [Display(Name = "Grade Submission Start Date")]
        public DateTime? GradeSubmissionStartDate { get; set; }

        [Display(Name = "Grade Submission End Date")]
        public DateTime? GradeSubmissionEndDate { get; set; }

        // Next Academic Year for student progression
        [Display(Name = "Next Academic Year")]
        public int? NextAcademicYearId { get; set; }

        [ForeignKey("NextAcademicYearId")]
        public AcademicYear? NextAcademicYear { get; set; }

        // Inverse navigation - academic years that point to this one as their next year
        [InverseProperty("NextAcademicYear")]
        public ICollection<AcademicYear> PreviousAcademicYears { get; set; } = new HashSet<AcademicYear>();

        // Deprecated fields - keeping for backward compatibility during migration
        public int? SemesterId { get; set; } // Will be removed in future version
        public int? ModeId { get; set; } // Will be removed in future version

        // Navigation properties
        public ICollection<FinancialStatement> FinancialStatements { get; set; } = new HashSet<FinancialStatement>();
    }
}