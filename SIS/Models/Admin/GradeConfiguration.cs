using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Admin
{
    public class GradeConfiguration : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(3)]
        [Display(Name = "Grade Letter")]
        public string GradeLetter { get; set; }

        [Required]
        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        [Display(Name = "Minimum Score")]
        public decimal MinScore { get; set; }

        [Required]
        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        [Display(Name = "Maximum Score")]
        public decimal MaxScore { get; set; }

        [Required]
        [Range(0, 4.0)]
        [Column(TypeName = "decimal(3,2)")]
        [Display(Name = "GPA Value")]
        public decimal GPAValue { get; set; }

        [StringLength(200)]
        public string Description { get; set; }

        [Display(Name = "Is Passing Grade")]
        public bool IsPassingGrade { get; set; }

        [Required]
        [Display(Name = "Status")]
        public bool IsActive { get; set; }
        public int? SchoolId { get; set; }
        public int? AcademicYearId { get; set; }
    }
}