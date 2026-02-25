namespace SIS.Models.TimeTabling
{
    public class TimetableGenerationResult
    {

        public List<Timetable> TimetableEntries { get; set; } = new List<Timetable>();
        public List<ScheduleTracking> ScheduleTrackings { get; set; } = new List<ScheduleTracking>();
    }
}
