using SIS.Controllers;
using SIS.Data;
using SIS.Models.StudyPermits;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.ViewModels
{
    // Main Index ViewModel
    public class StudentLookupIndexViewModel
    {
        public string UserRole { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string JurisdictionInfo { get; set; } = string.Empty;
        public List<string> SearchTypes { get; set; } = new();
        public ApplicationUser User { get; set; }
    }

    // Search Results ViewModel
    public class StudentSearchResultViewModel
    {
        public int Id { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ProgrammeName { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public int CurrentYear { get; set; }
        public string StudentStatus { get; set; } = string.Empty;
        public string RegistrationStatus { get; set; } = string.Empty;
        public decimal OutstandingFees { get; set; }
        public bool IsRegistered { get; set; }
        public string PassportPhotoPath { get; set; } = string.Empty;
    }

    // Student Profile ViewModel
    public class StudentProfileViewModel
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string MaritalStatus { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public string Religion { get; set; } = string.Empty;
        public string NrcOrPassportNumber { get; set; } = string.Empty;
        public bool IsForeigner { get; set; }

        // Academic Information
        public string ProgrammeName { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string ProgrammeLevelName { get; set; } = string.Empty;
        public string ModeOfStudyName { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public int CurrentYear { get; set; }
        public int CurrentSemester { get; set; }

        // Status Information
        public string StudentStatus { get; set; } = string.Empty;
        public string RegistrationStatus { get; set; } = string.Empty;
        public bool IsRegistered { get; set; }
        public bool IsAdmitted { get; set; }
        public DateTime? AdmissionDate { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string PassportPhotoPath { get; set; } = string.Empty;

        // Related Information
        public AddressViewModel? Address { get; set; }
        public NextOfKinViewModel? NextOfKin { get; set; }
        public List<StudyPermit> StudyPermits { get; set; }
    }

    // Address ViewModel
    public class AddressViewModel
    {
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
    }

    // Next of Kin ViewModel
    public class NextOfKinViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    // Student Financial ViewModel
    public class StudentFinancialViewModel
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public decimal TotalFees { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal OutstandingBalance { get; set; }
        public string InvoiceReference { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public List<UnifiedTransactionDto> FinancialStatement { get; set; }

        // Payment Requirements
        public decimal MinRegistrationPayment { get; set; }
        public decimal MinExamPayment { get; set; }
        public bool CanRegister { get; set; }
        public bool CanTakeExams { get; set; }

        // Breakdown and History
        public List<FeeBreakdownViewModel> FeeBreakdown { get; set; } = new();
        public List<PaymentHistoryViewModel> PaymentHistory { get; set; } = new();
    }

    // Fee Breakdown ViewModel
    public class FeeBreakdownViewModel
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public decimal PercentagePaid => Amount > 0 ? Math.Round((Paid / Amount) * 100, 1) : 0;
    }

    // Payment History ViewModel
    public class PaymentHistoryViewModel
    {
        public string TransactionReference { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal OutstandingAmount { get; set; }
        public int AcademicYearId { get; set; }
    }

    // Student Results ViewModel
    public class StudentResultsViewModel
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string ProgrammeName { get; set; } = string.Empty;
        public int CurrentYear { get; set; }
        public int CurrentSemester { get; set; }
        public decimal OutstandingFees { get; set; }
        public bool CanViewCompleteResults { get; set; }
        public List<AcademicYearResultsViewModel> AcademicYearResults { get; set; } = new();
        public List<PerformanceArchiveViewModel> PerformanceArchives { get; set; } = new();
    }

    // Academic Year Results ViewModel
    public class AcademicYearResultsViewModel
    {
        public int AcademicYearId { get; set; }
        public string AcademicYear { get; set; } = string.Empty;
        public List<SemesterResultsViewModel> Semesters { get; set; } = new();
        public decimal YearGPA { get; set; }
        public int CreditsAttempted { get; set; }
        public int CreditsEarned { get; set; }
        public string AcademicStanding { get; set; } = string.Empty;
    }

    // Semester Results ViewModel
    public class SemesterResultsViewModel
    {
        public int Semester { get; set; }
        public List<CourseResultViewModel> Courses { get; set; } = new();
    }

    // Course Result ViewModel
    public class CourseResultViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public int Credits { get; set; }
        public decimal TotalScore { get; set; }
        public string Grade { get; set; } = string.Empty;
        public bool IsPassed { get; set; }
        public bool IsPublished { get; set; }
        public Dictionary<string, decimal> AssessmentScores { get; set; } = new();
    }

    // Performance Archive ViewModel
    public class PerformanceArchiveViewModel
    {
        public int AcademicYearId { get; set; }
        public int YearOfStudy { get; set; }
        public int TotalCoursesTaken { get; set; }
        public int CoursesPassed { get; set; }
        public int CoursesFailed { get; set; }
        public decimal GPA { get; set; }
        public string OverallGrade { get; set; } = string.Empty;
        public string ProgressionStatus { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // Grade Configuration ViewModel (for display purposes)
    public class GradeConfiguration
    {
        public string GradeLetter { get; set; } = string.Empty;
        public decimal MinScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal GPAValue { get; set; }
        public bool IsActive { get; set; }
    }
}