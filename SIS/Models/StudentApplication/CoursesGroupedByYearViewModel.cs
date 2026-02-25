namespace SIS.Models.StudentApplication
{
    public class CoursesGroupedByYearViewModel
    {
        public int YearOfStudy { get; set; }
        public string YearDisplayName { get; set; } = string.Empty; // e.g., "First Year", "Second Year"
        public List<CourseSemesterViewModel> Semesters { get; set; } = new();
        public int TotalCourses { get; set; }
        public int MandatoryCourses { get; set; }
        public int ElectiveCourses { get; set; }
    }

    public class CourseSemesterViewModel
    {
        public int SemesterNumber { get; set; }
        public string SemesterDisplayName { get; set; } = string.Empty; // e.g., "Semester 1", "Semester 2"
        public List<CourseDetailViewModel> Courses { get; set; } = new();
    }

    public class CourseDetailViewModel
    {
        public int Id { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string CourseDescription { get; set; } = string.Empty;
        public string CourseType { get; set; } = string.Empty;
        public int YearTaken { get; set; }
        public int SemesterTaken { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsExaminable { get; set; }
        public int PassMark { get; set; }
        public string InstructorName { get; set; } = string.Empty;
        public int MeetingFrequencyPerWeek { get; set; }
        public List<string> PrerequisiteCourses { get; set; } = new();
        public string CapacityRequired { get; set; } = string.Empty;
    }
}
