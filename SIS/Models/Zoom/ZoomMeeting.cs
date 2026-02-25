using SIS.Data;
using SIS.Models.Admin;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Enums;

namespace SIS.Models.Zoom
{
    public class ZoomMeeting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Topic { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public int Duration { get; set; } // in minutes

        [Required]
        [StringLength(50)]
        public string ZoomMeetingId { get; set; }

        [Required]
        [StringLength(1000)]
        public string JoinUrl { get; set; }

        [Required]
        [StringLength(1000)]
        public string StartUrl { get; set; }

        [StringLength(500)]
        public string Password { get; set; }

        public string Agenda { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? EndedAt { get; set; }

        [Required]
        public Status Status { get; set; } = Status.Scheduled;

        // Foreign keys
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        public string CreatedById { get; set; } 

        [ForeignKey("CreatedById")]
        public virtual ApplicationUser CreatedBy { get; set; }
    }
}
