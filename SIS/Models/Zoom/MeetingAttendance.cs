using SIS.Data;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Zoom
{
    public class MeetingAttendance
    {
        [Key]
        public int Id { get; set; }

        public int ZoomMeetingId { get; set; }

        [ForeignKey("ZoomMeetingId")]
        public virtual ZoomMeeting Meeting { get; set; }

        public string StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual ApplicationUser Student { get; set; }

        public DateTime JoinedAt { get; set; }

        public DateTime? LeftAt { get; set; }

        public int DurationMinutes { get; set; }
    }
}
