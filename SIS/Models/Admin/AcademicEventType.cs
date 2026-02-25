using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Admin
{
    public class AcademicEventType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Event Type Name")]
        public string Name { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Default Color")]
        public string DefaultColor { get; set; } = "#3788d8"; // Default blue color

        [StringLength(50)]
        [Display(Name = "Icon Name")]
        public string IconName { get; set; } = "event"; // Default Google Material icon

        // Navigation property to related events
        public virtual ICollection<AcademicCalendarEvent> Events { get; set; } = new HashSet<AcademicCalendarEvent>();
    }
}