using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SIS.Models.Admin;
using SIS.Models.Registration;

namespace SIS.Models.Fees
{
    public class OtherFees
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string FeeName { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public bool AppliesOnlyToForeignStudents { get; set; }

        [Range(1, 2, ErrorMessage = "Semester must be either 1 or 2")]
        public int? Semester { get; set; }

        [StringLength(20)]
        public string? CreditNCode { get; set; }

        [StringLength(20)]
        public string? DebitNCode { get; set; }

        // Foreign Keys
        [ForeignKey("AcademicYear")]
        public int? AcademicYearId { get; set; }

        [ForeignKey("School")]
        public int? SchoolId { get; set; }

        [ForeignKey("Programme")]
        public int? ProgrammeId { get; set; }

        [ForeignKey("ModeOfStudy")]
        public int? ModeOfStudyId { get; set; }

        [ForeignKey("ProgramLevel")]
        public int? ProgramLevelId { get; set; }

        // Navigation Properties
        public virtual AcademicYear? AcademicYear { get; set; }
        public virtual School? School { get; set; }
        public virtual Programme? Programme { get; set; }
        public virtual ModeOfStudy? ModeOfStudy { get; set; }
        public virtual ProgramLevel? ProgramLevel { get; set; }

        // Audit Fields
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}