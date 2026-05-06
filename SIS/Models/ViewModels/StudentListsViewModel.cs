using SIS.Enums;
using SIS.Models.StudyPermits;

namespace SIS.Models.ViewModels
{
    public class StudentListsViewModel
    {
        public string UserRole { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string JurisdictionInfo { get; set; } = string.Empty;

        // Filter Options
        public List<FilterOption> Schools { get; set; } = new List<FilterOption>();
        public List<FilterOption> Programmes { get; set; } = new List<FilterOption>();
        public List<FilterOption> ModesOfStudy { get; set; } = new List<FilterOption>();
        public List<FilterOption> ProgrammeLevels { get; set; } = new List<FilterOption>();
        public List<FilterOption> AcademicYears { get; set; } = new List<FilterOption>();
        public List<FilterOption> RegistrationStatuses { get; set; } = new List<FilterOption>();

        // Year and Semester options
        public List<int> YearOptions { get; set; } = new List<int> { 1, 2, 3, 4, 5 };
        public List<int> SemesterOptions { get; set; } = new List<int> { 1, 2 };
    }

    public class StudentListFiltersViewModel
    {
        public int? SchoolId { get; set; }
        public int? ProgrammeId { get; set; }
        public int? ModeOfStudyId { get; set; }
        public int? ProgrammeLevelId { get; set; }
        public int? AcademicYearId { get; set; }
        public int? CurrentYear { get; set; }
        public int? CurrentPeriod { get; set; }
        public Status? RegistrationStatus { get; set; }
        public bool? IsRegistered { get; set; }
        public bool? HasOutstandingFees { get; set; }

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string SortBy { get; set; } = "FullName";
        public string SortDirection { get; set; } = "asc";
    }

    public class FilteredStudentViewModel
    {
        public int Id { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ProgrammeName { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string ModeOfStudyName { get; set; } = string.Empty;
        public string ProgrammeLevelName { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public int CurrentYear { get; set; }
        public int CurrentPeriodId { get; set; }
        public string CurrentPeriodLabel { get; set; } = string.Empty;
        public string StudentStatus { get; set; } = string.Empty;
        public string RegistrationStatus { get; set; } = string.Empty;
        public bool IsRegistered { get; set; }
        public decimal OutstandingFees { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public DateTime? AdmissionDate { get; set; }

        public string NrcOrPassportNumber { get; set; }
        public string Gender { get; set; }
        public string Nationality { get; set; }
        public bool IsForeigner { get; set; }
        public StudyPermit? StudyPermit { get; set; }
    }

    public class StudentListResultsViewModel
    {
        public List<FilteredStudentViewModel> Students { get; set; } = new List<FilteredStudentViewModel>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPrevious { get; set; }
        public bool HasNext { get; set; }
        public StudentListFiltersViewModel AppliedFilters { get; set; } = new StudentListFiltersViewModel();
    }

    public class FilterOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}
