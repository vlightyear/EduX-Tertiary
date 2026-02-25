namespace SIS.Models.ProgramCoordinator
{
    // 1. Main view model for lecturer management page
    public class LecturerManagementViewModel
    {
        public List<LecturerViewModel> Lecturers { get; set; } = new List<LecturerViewModel>();
        public LecturerFilterOptions FilterOptions { get; set; } = new LecturerFilterOptions();
        public LecturerFilterModel AppliedFilters { get; set; } = new LecturerFilterModel();
        public LecturerStatistics Statistics { get; set; } = new LecturerStatistics();
    }

    // Individual lecturer summary for list view
    public class LecturerViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Department { get; set; }
        public int CourseCount { get; set; }
        public string Status { get; set; }
        public string ProfileImageUrl { get; set; }
    }

    // Filter options available in UI
    public class LecturerFilterOptions
    {
        public List<string> Departments { get; set; } = new List<string>();
        public List<object> Courses { get; set; } = new List<object>(); // Id and Name
        public List<string> Statuses { get; set; } = new List<string>();
    }

    // Statistics shown on dashboard
    public class LecturerStatistics
    {
        public int TotalLecturers { get; set; }
        public double AverageCoursesPerLecturer { get; set; }
        public Dictionary<string, int> LecturersByDepartment { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> LecturersByStatus { get; set; } = new Dictionary<string, int>();
    }

    // 2. Detailed lecturer profile view model
    public class LecturerDetailsViewModel
    {
        // Basic Info
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string ProfileImageUrl { get; set; }

        // Academic Info
        public string Qualifications { get; set; }
        public string Specialization { get; set; }
        public string Biography { get; set; }

        // Contact & Location
        public string Office { get; set; }
        public string OfficeHours { get; set; }
        public DateTime? JoinDate { get; set; }

        // Course Assignments
        public List<CourseAssignmentViewModel> CourseAssignments { get; set; } = new List<CourseAssignmentViewModel>();

        // Teaching Load Summary
        public TeachingLoadViewModel TeachingLoad { get; set; } = new TeachingLoadViewModel();
    }

    // 3. Course assignment view model
    public class CourseAssignmentViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public int YearTaken { get; set; }
        public int SemesterTaken { get; set; }
        public int ProgramId { get; set; }
        public string ProgramName { get; set; }
    }

    // Teaching load summary
    public class TeachingLoadViewModel
    {
        public int TotalCourses { get; set; }
        public Dictionary<int, int> CoursesByYear { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> CoursesBySemester { get; set; } = new Dictionary<int, int>();
        public Dictionary<string, int> CoursesByProgram { get; set; } = new Dictionary<string, int>();
    }

    // Extended course assignment view model with more details
    public class DetailedCourseAssignmentViewModel : CourseAssignmentViewModel
    {
        public string DepartmentName { get; set; }
        public int EnrolledStudents { get; set; }
        public int ScheduledHours { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    // Extended teaching load view model with more metrics
    public class DetailedTeachingLoadViewModel : TeachingLoadViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalScheduledHours { get; set; }
        public Dictionary<string, int> CoursesByDepartment { get; set; } = new Dictionary<string, int>();
        public double AverageStudentsPerCourse { get; set; }
    }

    // View model for lecturer course assignments page
    public class LecturerCourseAssignmentsViewModel
    {
        public string LecturerId { get; set; }
        public string LecturerName { get; set; }
        public List<DetailedCourseAssignmentViewModel> CourseAssignments { get; set; } = new List<DetailedCourseAssignmentViewModel>();
        public DetailedTeachingLoadViewModel TeachingLoad { get; set; } = new DetailedTeachingLoadViewModel();
        public List<string> AcademicYears { get; set; } = new List<string>();
    }

    // 4. Filter model for lecturer search and filtering
    public class LecturerFilterModel
    {
        public string Department { get; set; }
        public int? CourseId { get; set; }
        public string Status { get; set; }
        public string SearchTerm { get; set; }
        public string SortBy { get; set; }
        public bool SortDescending { get; set; }
    }

    // 5. View models for different report types
    public class LecturerReportViewModel
    {
        public string ReportType { get; set; }
        public DateTime ReportDate { get; set; }
        public int TotalLecturers { get; set; }

        // For summary reports
        public List<LecturerSummary> LecturersSummary { get; set; } = new List<LecturerSummary>();

        // For program-based reports
        public List<ProgramLecturerSummary> ProgramSummaries { get; set; } = new List<ProgramLecturerSummary>();

        // For department-based reports
        public List<DepartmentLecturerSummary> DepartmentSummaries { get; set; } = new List<DepartmentLecturerSummary>();

        // For teaching load reports
        public List<TeachingLoadSummary> TeachingLoadSummaries { get; set; } = new List<TeachingLoadSummary>();
        public double AverageCoursesPerLecturer { get; set; }
        public int MaxCoursesPerLecturer { get; set; }
    }

    // Basic lecturer summary for reports
    public class LecturerSummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int CourseCount { get; set; }
    }

    // Program-specific lecturer summary
    public class ProgramLecturerSummary
    {
        public string ProgramName { get; set; }
        public int LecturerCount { get; set; }
        public List<string> Lecturers { get; set; } = new List<string>();
    }

    // Department-specific lecturer summary
    public class DepartmentLecturerSummary
    {
        public string DepartmentName { get; set; }
        public int LecturerCount { get; set; }
        public List<string> Lecturers { get; set; } = new List<string>();
    }

    // Teaching load summary for reports
    public class TeachingLoadSummary
    {
        public string LecturerId { get; set; }
        public string LecturerName { get; set; }
        public int CourseCount { get; set; }
        public int TotalScheduledHours { get; set; }
        public Dictionary<string, int> CoursesPerSemester { get; set; } = new Dictionary<string, int>();
    }
}