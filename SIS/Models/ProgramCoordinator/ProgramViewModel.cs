using SIS.Models.Admin;

namespace SIS.Models.ProgramCoordinator
{
    // Add these to your project, typically in a ViewModels folder

    public class ProgramViewModel
    {
        public Programme Programme { get; set; }
        public int ActiveStudentCount { get; set; }
        public int PendingApplicationsCount { get; set; }
        public int EnrollmentCount { get; set; }
    }

    public class ProgramDetailsViewModel
    {
        public Programme Programme { get; set; }
        public ProgramMetricsViewModel Metrics { get; set; }
        public Dictionary<int, List<CourseViewModel>> CourseStructure { get; set; }
    }

    public class ProgramMetricsViewModel
    {
        public Dictionary<int, int> EnrollmentByYear { get; set; }
        public Dictionary<string, int> GenderDistribution { get; set; }
        public IEnumerable<object> EnrollmentTrends { get; set; }
        public IEnumerable<object> CompletionRates { get; set; }
    }

    public class CourseViewModel
    {
        public Course Course { get; set; }
        public bool IsMandatory { get; set; }
        public int Semester { get; set; }
    }

    public class ManageProgramCoursesViewModel
    {
        public Programme Program { get; set; }
        public List<ProgrammeCourse> ProgramCourses { get; set; }
        public List<Course> AvailableCourses { get; set; }
        public Dictionary<int, List<CourseViewModel>> CourseStructure { get; set; }
    }

    
}
