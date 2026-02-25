namespace SIS.Models.StudentApplication
{
    public class ProgrammeDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DurationYears { get; set; }
        public int MinimumPointsTop5Subjects { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string ProgrammeLevelName { get; set; } = string.Empty;
        public string ModeOfStudyName { get; set; } = string.Empty;
        public string CoordinatorName { get; set; } = string.Empty;
        public int EnrollmentCount { get; set; }
        public string YearlyRequirements { get; set; } = string.Empty;

        // Courses organized by year
        public List<CoursesGroupedByYearViewModel> CoursesByYear { get; set; } = new();

        // Fee information
        public List<ProgrammeFeeDetailViewModel> FeeBreakdown { get; set; } = new();
        public decimal TotalFeesPerYear { get; set; }

        // Navigation
        public int SchoolId { get; set; }
        public int DepartmentId { get; set; }
    }
}
