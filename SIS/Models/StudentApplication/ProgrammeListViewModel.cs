namespace SIS.Models.StudentApplication
{
    public class ProgrammeListViewModel
    {
        public List<SchoolWithProgrammesViewModel> Schools { get; set; } = new();
        public int TotalProgrammes { get; set; }
        public int TotalSchools { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
        public int? SelectedSchoolId { get; set; }
        public int? SelectedProgrammeLevelId { get; set; }
    }

    public class SchoolWithProgrammesViewModel
    {
        public int SchoolId { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public string SchoolDescription { get; set; } = string.Empty;
        public List<DepartmentWithProgrammesViewModel> Departments { get; set; } = new();
        public int TotalProgrammes { get; set; }
    }

    public class DepartmentWithProgrammesViewModel
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string DepartmentDescription { get; set; } = string.Empty;
        public List<ProgrammeSummaryViewModel> Programmes { get; set; } = new();
    }

    public class ProgrammeSummaryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DurationYears { get; set; }
        public string ProgrammeLevelName { get; set; } = string.Empty;
        public string ModeOfStudyName { get; set; } = string.Empty;
        public int MinimumPointsTop5Subjects { get; set; }
        public int EnrollmentCount { get; set; }
    }
}
