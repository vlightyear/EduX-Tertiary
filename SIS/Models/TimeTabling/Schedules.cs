using SIS.Models.Admin;

namespace SIS.Models.TimeTabling
{
    public class AssignedSession
    {
        public CourseSession Session { get; set; }
        public int AssignedRoomId { get; set; }
        public string AssignedDay { get; set; }
        public int AssignedPeriod { get; set; }    // 0 = first period in that day
    }

    public class CourseSession
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string LecturerId { get; set; }
        public string SessionType { get; set; } // e.g. "Lecture", "Tutorial", "Lab", "Exam"
        public int DurationPeriods { get; set; } // e.g., spans 1 or 2 periods
        public int MeetingFrequencyPerWeek { get; set; } = 1;
        public List<int> PossibleRoomIds { get; set; } = new();
    }

    public class Period
    {
        public int PeriodNumber { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }
}
