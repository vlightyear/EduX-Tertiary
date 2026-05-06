using SIS.Models.StudentApplication;

namespace SIS.Models.TimeTabling
{
    // ViewModels/StudentTimetableViewModel.cs
    public class StudentTimetableViewModel
    {
        public Student Student { get; set; }
        public List<string> WorkingDays { get; set; }
        public List<PeriodViewModel> Periods { get; set; }
        public Dictionary<string, Dictionary<int, List<StudentTimetableSessionViewModel>>> WeeklyTimetable { get; set; }
        public string AcademicYearValue { get; set; }
        public string ModeOfStudyName { get; set; }
        public string Period { get; set; }
    }

    public class StudentTimetableSessionViewModel
    {
        public int TimetableId { get; set; }
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string InstructorName { get; set; }
        public string RoomName { get; set; }
        public int PeriodNumber { get; set; }
        public DateTime Date { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public string Status { get; set; }
    }
}
