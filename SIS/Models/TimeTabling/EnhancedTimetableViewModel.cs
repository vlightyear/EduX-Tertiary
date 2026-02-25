using SIS.Models.Admin;
using SIS.Models.Registration;

namespace SIS.Models.TimeTabling
{
    public class EnhancedTimetableViewModel
    {
        // Academic session info
        public AcademicYear AcademicYear { get; set; }
        public ModeOfStudy ModeOfStudy { get; set; }

        // Date range
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalWeeks { get; set; }

        // Configuration
        public List<WorkingDayViewModel> WorkingDays { get; set; }
        public Dictionary<int, List<TimeSlotViewModel>> TimeSlotConfigs { get; set; }
        public List<LearningRoom> Rooms { get; set; }

        // Timetable data
        public List<Timetable> TimetableEntries { get; set; }
    }

    public class TimeSlotViewModel
    {
        public int PeriodNumber { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }

        // Formatting helpers
        public string FormattedTime => $"{StartTime.ToString(@"hh\:mm")} - {EndTime.ToString(@"hh\:mm")}";
    }

    public class WorkingDayViewModel
    {
        public string DayName { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public int TimeSlotConfigId { get; set; }
    }

    // Classes for deserializing JSON data
    //public class WorkingDayData
    //{
    //    public string day { get; set; }
    //    public bool isWorkingDay { get; set; }
    //    public string timeSlotConfigId { get; set; }
    //}

    //public class PeriodData
    //{
    //    public int periodNumber { get; set; }
    //    public string startTime { get; set; }
    //    public string endTime { get; set; }
    //    public string type { get; set; }
    //    public string description { get; set; }
    //}
}
