using SIS.Models.Registration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Admin
{
    public class ProgressionRule : AuditClass
    {
        [Key]
        public int Id { get; set; } // Unique identifier for each rule

        [Required]
        [StringLength(100)]
        public string Name { get; set; } // e.g., "Clear Pass", "Repeat Year"

        [Required]
        public int PercentFailedOfCourseLoad { get; set; } // Maximum number of failed courses for this status to apply

        [StringLength(500)]
        public string Description { get; set; } // Optional detailed description of the rule
        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        [Required]
        public bool IsActive { get; set; } // Indicates if this rule is currently applicable

        [Display(Name = "School (Leave empty for global rule)")]
        public int? SchoolId { get; set; }

        [ForeignKey("SchoolId")]
        public virtual School? School { get; set; }

        [Display(Name = "Period (Leave empty for global rule)")]
        public int? AcademicPeriodId { get; set; }
        public virtual AcademicPeriod AcademicPeriod { get; set; }

        [Display(Name = "Attempt (Leave empty for global rule)")]
        public int? Attempt { get; set; }
    }


}
