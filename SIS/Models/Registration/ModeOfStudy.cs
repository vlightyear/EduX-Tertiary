using SIS.Models.Applications;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Registration
{
    public class ModeOfStudy
    {
        [Key]
        public int ModeId { get; set; } // Primary Key

        [Required]
        public string ModeName { get; set; } // Full-time, part-time

        [Required]
        public string Code { get; set; } // FT, PT


        public int? ApplicationPeriodId { get; set; }
        [ForeignKey(nameof(ApplicationPeriodId))]
        public ApplicationPeriod ApplicationPeriod { get; set; }
    }
}