using SIS.Enums;
using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.StudentAccommodation
{
    public class AccommodationApplication : AuditClass
    {
        [Key]
        public int ApplicationId { get; set; }

        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }
        public int? SelectedBedId {  get; set; }

        [ForeignKey(nameof(SelectedBedId))]
        public BedSpace BedSpace { get; set; }

        public int PeriodId { get; set; }

        [ForeignKey("PeriodId")]
        public virtual AccommodationPeriod Period { get; set; }

        [Required]
        public DateTime ApplicationDate { get; set; }
        public int? NumberOfDays { get; set; }

        [Required]
        public Status Status { get; set; } 

        public string Notes { get; set; }

        // Navigation properties
        public virtual Allocation Allocation { get; set; }
    }
}
