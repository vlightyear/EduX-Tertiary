namespace SIS.Models.TimeTabling
{
    public class TimetableSessionViewModel
    {
        public int TimetableId { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public string CourseCode { get; set; }
        public string InstructorName { get; set; }
        public string ProgrammeName { get; set; }
        public int PeriodNumber { get; set; }
        public string DepartmentName { get; set; }
        public DateTime Date { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public string Status { get; set; }
        public bool IsRecurring { get; set; }
        public DateTime? RecurrenceEndDate { get; set; }
    }


    public class LecturerTimetableViewModel
    {
        public string AcademicYearValue { get; set; }
        public string ModeOfStudyName { get; set; }
        public List<string> WorkingDays { get; set; } = new List<string>();
        public List<PeriodViewModel> Periods { get; set; } = new List<PeriodViewModel>();
        public Dictionary<string, Dictionary<int, List<TimetableSessionViewModel>>> WeeklyTimetable { get; set; } = new Dictionary<string, Dictionary<int, List<TimetableSessionViewModel>>>();
        public List<Timetable> TimetableEntries { get; set; } = new List<Timetable>();
    }

    // Using the existing TimetableSessionViewModel from your code
    // You mentioned this already exists in your project

    public class PeriodViewModel
    {
        public int PeriodNumber { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Type { get; set; }
    }
}
