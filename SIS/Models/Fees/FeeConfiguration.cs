using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SIS.Models.Admin;
using SIS.Models.Registration;

namespace SIS.Models.Fees
{
    public class FeeConfiguration : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("AcademicYear")]
        public int? AcademicYearId { get; set; } // Foreign key to AcademicYear

        [ForeignKey("School")]
        public int? SchoolId { get; set; } // Made nullable

        [ForeignKey("Programme")]
        public int? ProgrammeId { get; set; } // Made nullable

        [ForeignKey("ModeOfStudy")]
        public int? ModeOfStudyId { get; set; } // Made nullable

        public int? YearOfStudy { get; set; } // Made nullable

        [ForeignKey("FeeType")]
        [Required]
        public int FeeTypeId { get; set; } // Keeping required as each fee must have a type

        [ForeignKey("ProgramLevel")]
        public int? ProgramLevelId { get; set; } // Made nullable

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        [Required]
        public bool AppliesOnlyToAccommodated { get; set; }

        [Required]
        public bool AppliesUniversally { get; set; } 

        [Required]
        public bool AppliesOnlyToForeignStudents { get; set; }

        [Required]
        public bool AppliesOnlyToLocalStudents { get; set; }

        public bool IsActive { get; set; } = true;
        public decimal RegistrationPaymentRequired { get; set; } = 75;

        [Range(1, 2, ErrorMessage = "Semester must be either 1 or 2")]
        public int? Semester { get; set; }

        [Required(ErrorMessage = "Credit N-Code is required.")]
        [StringLength(20)]
        public required string CreditNCode { get; set; }

        [Required(ErrorMessage = "Debit N-Code is required.")]
        [StringLength(20)]
        public required string DebitNCode { get; set; }

        // Navigation properties
        public virtual AcademicYear AcademicYear { get; set; }
        public virtual School School { get; set; }
        public virtual Programme Programme { get; set; }
        public virtual ModeOfStudy ModeOfStudy { get; set; }
        public virtual FeeType FeeType { get; set; }
        public virtual ProgramLevel ProgramLevel { get; set; }
    }
}