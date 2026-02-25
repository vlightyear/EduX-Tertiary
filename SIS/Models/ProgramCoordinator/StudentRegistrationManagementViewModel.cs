using SIS.Enums;

namespace SIS.Models.ProgramCoordinator
{
    // Main management view model
    public class StudentRegistrationManagementViewModel
    {
        public List<StudentSummaryViewModel> Students { get; set; } = new();
        public StudentRegistrationFilterOptions FilterOptions { get; set; } = new();
        public StudentRegistrationFilterModel AppliedFilters { get; set; } = new();
        public StudentRegistrationStatistics Statistics { get; set; } = new();
    }

    // Student summary for the main list
    public class StudentSummaryViewModel
    {
        public string StudentId { get; set; }
        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public string ProgramName { get; set; }
        public int CurrentYear { get; set; }
        public int CurrentSemester { get; set; }
        public int RegisteredCoursesCount { get; set; }
        public Status RegistrationStatus { get; set; }
        public string PaymentStatus { get; set; } // "Paid", "Partial", "Unpaid"
        public decimal OutstandingFees { get; set; }
    }

    // Filter options for dropdowns
    public class StudentRegistrationFilterOptions
    {
        public List<ProgramViewModel> Programs { get; set; } = new();
        public List<int> Years { get; set; } = new();
        public List<int> Semesters { get; set; } = new();
        public List<string> RegistrationStatuses { get; set; } = new();
    }

    // Applied filter model
    public class StudentRegistrationFilterModel
    {
        public string SearchTerm { get; set; }
        public string Program { get; set; }
        public int? Year { get; set; }
        public int? Semester { get; set; }
        public string RegistrationStatus { get; set; }
    }

    // Statistics for dashboard
    public class StudentRegistrationStatistics
    {
        public int TotalStudents { get; set; }
        public int RegisteredStudents { get; set; }
        public int UnregisteredStudents { get; set; }
        public int FullyPaidStudents { get; set; }
        public Dictionary<string, int> StudentsByProgram { get; set; } = new();
        public Dictionary<int, int> StudentsByYear { get; set; } = new();
    }

    // Program view model for dropdowns
    public class CoordinatorProgramViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    // Student registration details view model
    public class StudentRegistrationDetailsViewModel
    {
        public string StudentId { get; set; }
        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public string ProgramName { get; set; }
        public int CurrentYear { get; set; }
        public int CurrentSemester { get; set; }
        public Status RegistrationStatus { get; set; }
        public List<CourseViewModel> RegisteredCourses { get; set; } = new();
        public PaymentSummaryViewModel PaymentInfo { get; set; } = new();
        public bool CanModifyRegistration { get; set; }
    }

    // Course view model for displaying courses
    public class CoordinatorCourseViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string CourseDescription { get; set; }
        public bool IsMandatory { get; set; }
        public int Credits { get; set; }
        public int Year { get; set; }
        public int Semester { get; set; }
        public DateTime? RegistrationDate { get; set; }
    }

    // Payment summary view model
    public class PaymentSummaryViewModel
    {
        public decimal TotalPaid { get; set; }
        public decimal OutstandingBalance { get; set; }
        public string PaymentStatus { get; set; } // "Fully Paid", "Partially Paid", "Not Paid"
    }

    // Modify registration view model
    public class ModifyRegistrationViewModel
    {
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string ProgramName { get; set; }
        public int CurrentYear { get; set; }
        public int CurrentSemester { get; set; }
        public List<CourseSelectionViewModel> AvailableCourses { get; set; } = new();
        public string ModificationReason { get; set; }
    }

    // Course selection view model for modification
    public class CourseSelectionViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string CourseDescription { get; set; }
        public bool IsMandatory { get; set; }
        public int Credits { get; set; }
        public int Year { get; set; }
        public int Semester { get; set; }
        public bool IsSelected { get; set; }
    }
}