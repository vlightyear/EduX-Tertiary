using SIS.Models.Admin;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.TimeTabling
{
    public class ScheduleTracking : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Entity ID")]
        public string EntityId { get; set; }  // Can be either LearningRoomId or InstructorId

        [Required]
        [StringLength(20)]
        [Display(Name = "Entity Type")]
        public string EntityType { get; set; }  // "LearningRoom" or "Instructor"

        [Required]
        [Display(Name = "Time Slot Configuration")]
        public int TimeSlotConfigId { get; set; }

        [ForeignKey("TimeSlotConfigId")]
        public TimeSlotConfiguration? TimeSlotConfig { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Display(Name = "Is Occupied")]
        public bool IsOccupied { get; set; }

        [Display(Name = "Occupied By Course")]
        public int? OccupiedByCourseId { get; set; }

        [ForeignKey("OccupiedByCourseId")]
        public Course? OccupiedByCourse { get; set; }

        [Required]
        public int PeriodNumber { get; set; }
    }
}
