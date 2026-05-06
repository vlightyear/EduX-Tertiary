using SIS.Enums;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.ViewModels
{
    public class StudentAdministrationViewModel
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string ProgrammeName { get; set; }
        public string SchoolName { get; set; }
        public string DepartmentName { get; set; }
        public string ModeOfStudyName { get; set; }
        public string ProgrammeLevelName { get; set; }
        public string AcademicYear { get; set; }
        public int CurrentYear { get; set; }
        public int? CurrentPeriodId { get; set; }
        public string? CurrentPeriodLabel { get; set; }
        public string StudentStatus { get; set; }
        public string RegistrationStatus { get; set; }
        public bool IsRegistered { get; set; }
        public decimal OutstandingFees { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public DateTime? AdmissionDate { get; set; }
        public int RegisteredCoursesCount { get; set; }
        public string UserRole { get; set; }
        public string AdminName { get; set; }
        public string PassportPhotoPath { get; set; } = string.Empty;
        public bool IsForeigner { get; set; } = false;
    }

    public class StudentAdminUpdateModel
    {
        public int StudentId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Phone]
        public string Phone { get; set; }

        [Required]
        public string NrcOrPassportNumber { get; set; }

        public DateTime DateOfBirth { get; set; }

        [Required]
        public string Gender { get; set; }

        [Required]
        public string Nationality { get; set; }

        [Required]
        public string MaritalStatus { get; set; }

        public string Religion { get; set; }

        // Address Information (Make optional)
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }

        // Next of Kin Information (Make optional)
        public string? NextOfKinName { get; set; }
        public string? NextOfKinRelationship { get; set; }
        public string? NextOfKinPhone { get; set; }
        public string? NextOfKinEmail { get; set; }
        public string? NextOfKinAddress { get; set; }

        // Photo Upload (Make optional)
        public IFormFile? PassportPhoto { get; set; }
        public bool IsForeigner { get; set; } = false;
    }

    public class ProgrammeChangeModel
    {
        public int StudentId { get; set; }

        [Required]
        public int ProgrammeId { get; set; }

        [Required]
        public int ModeOfStudyId { get; set; }

        [Required]
        public int SchoolId { get; set; }

        [Required]
        public int ProgrammeLevelId { get; set; }

        [Required]
        public int AcademicYearId { get; set; }

        [Range(1, 7)]
        public int CurrentYear { get; set; }

        [Range(1, 2)]
        public int CurrentPeriodId { get; set; }

        public string ChangeReason { get; set; }
    }

    public class RegistrationToggleModel
    {
        public int StudentId { get; set; }
        public bool EnableRegistration { get; set; }
        public string Reason { get; set; }
    }

    public class PasswordResetResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TemporaryPassword { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    //public class FilterOption
    //{
    //    public int Id { get; set; }
    //    public string Name { get; set; }
    //    public string Value { get; set; }
    //}

    public class StudentRegistrationInfo
    {
        public bool IsCurrentlyRegistered { get; set; }
        public string RegistrationStatus { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public int RegisteredCoursesCount { get; set; }
        public string AcademicYear { get; set; }
        public int CurrentYearPeriodId { get; set; }
        public decimal OutstandingFees { get; set; }
        public List<RegisteredCourseInfo> RegisteredCourses { get; set; } = new List<RegisteredCourseInfo>();
    }

    public class RegisteredCourseInfo
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsExaminable { get; set; }
        public DateTime RegistrationDate { get; set; }
    }

    public class AdminActionLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string AdminName { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }
    }
}