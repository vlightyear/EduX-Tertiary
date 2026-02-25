using SIS.Models.Admin;
using SIS.Models.Registration;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.TimeTabling
{
    public class Timetable : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }

        [Required]
        public int LearningRoomId { get; set; }

        [ForeignKey("LearningRoomId")]
        public LearningRoom? LearningRoom { get; set; }

        [Required]
        [Display(Name = "Time Slot Configuration")]
        public int TimeSlotConfigId { get; set; }

        [ForeignKey("TimeSlotConfigId")]
        public TimeSlotConfiguration? TimeSlotConfig { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Display(Name = "Academic Year")]
        public int AcademicYearId { get; set; }

        [ForeignKey("AcademicYearId")]
        public AcademicYear? AcademicYear { get; set; }

        [Required]
        [Display(Name = "Mode of Study")]
        public int ModeOfStudyId { get; set; }

        [ForeignKey("ModeOfStudyId")]
        public ModeOfStudy? ModeOfStudy { get; set; }

        [Required]
        public int PeriodNumber { get; set; }

        [Display(Name = "Special Instructions")]
        public string? SpecialInstructions { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Draft"; // Draft or Published

        [Display(Name = "Recurring Schedule")]
        public bool IsRecurring { get; set; }

        [Display(Name = "Recurrence End Date")]
        public DateTime? RecurrenceEndDate { get; set; }
    }
}
