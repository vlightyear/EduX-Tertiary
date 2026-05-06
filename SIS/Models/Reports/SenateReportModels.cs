using Microsoft.EntityFrameworkCore;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Reports
{
    public class SenateReportFilters
    {
        public int? AcademicYearId { get; set; }
        public int? SchoolId { get; set; }
        public int? DepartmentId { get; set; }
        public int? ProgrammeId { get; set; }
        public int? ModeOfStudyId { get; set; }
        public int? YearOfStudy { get; set; }
        public int? AcademicPeriod { get; set; }
        public string ReportLevel { get; set; } = "School";
        public string Period { get; set; } // "Current", "Previous", etc.
    }

    public class SenateReportViewModel
    {
        public SenateReportFilters AppliedFilters { get; set; } = new SenateReportFilters();
        public string ReportLevel { get; set; } = "School";
        public List<EntityProgressionSummary> Summaries { get; set; } = new List<EntityProgressionSummary>();
        public ReportTotals Totals { get; set; } = new ReportTotals();
        public List<BreadcrumbItem> Breadcrumbs { get; set; } = new List<BreadcrumbItem>();
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        // Filter Options for Dropdowns
        public List<AcademicYear> AcademicYears { get; set; } = new List<AcademicYear>();
        public List<School> Schools { get; set; } = new List<School>();
        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Programme> Programmes { get; set; } = new List<Programme>();
        public List<ModeOfStudy> ModesOfStudy { get; set; } = new List<ModeOfStudy>();
    }

    public class EntityProgressionSummary
    {
        public int EntityId { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty; // School, Department, Programme
        public Dictionary<string, int> ProgressionRuleCounts { get; set; } = new Dictionary<string, int>();
        public int TotalStudents { get; set; }
        public int StudentsWithResults { get; set; }
        public int StudentsWithoutResults { get; set; }
        public decimal AverageGPA { get; set; }
        public decimal PassRate { get; set; } // Percentage of students who can proceed
        public bool CanDrillDown { get; set; }
        public string DrillDownLevel { get; set; } = string.Empty;
    }

    public class ReportTotals
    {
        public int TotalStudents { get; set; }
        public int TotalWithResults { get; set; }
        public int TotalWithoutResults { get; set; }
        public Dictionary<string, int> OverallProgressionCounts { get; set; } = new Dictionary<string, int>();
        public decimal OverallAverageGPA { get; set; }
        public decimal OverallPassRate { get; set; }
    }

    public class BreadcrumbItem
    {
        public string Text { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class StudentProgressionData
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public decimal GPA { get; set; }
        public int FailedCourses { get; set; }
        public int TotalCourses { get; set; }
        public string ProgressionRule { get; set; } = string.Empty;
        public string Programme { get; set; }
        public int YearOfStudy { get; set; }
        public string ProgressionStatus { get; set; }
        public string AcademicStanding { get; set; }
        public bool HasPublishedResults { get; set; }
        public StudentProgressionDetailViewModel? StudentProgression { get; set; }
        public List<FailedCourseInfo> FailedCourseDetails { get; set; }
    }

    public class FailedCourseInfo
    {
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string? GradeLetter { get; set; }
        public decimal? Marks { get; set; }
        public int? Attempt { get; set; }
        public int Credits { get; set; }
    }

    // Helper class for assessment score calculations
    public class AssessmentScoreInfo
    {
        public decimal Score { get; set; }
        public decimal WeightPercentage { get; set; }
    }

    // Export-related models for future enhancement
    public class SenateReportExportOptions
    {
        public string Title { get; set; } = "Senate Academic Progress Report";
        public string GeneratedBy { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public string ReportNumber { get; set; } = string.Empty;
        public Dictionary<string, string> FilterSummary { get; set; } = new Dictionary<string, string>();
        public string AcademicSession { get; set; } = string.Empty;
        public bool RequiresSignature { get; set; } = true;
    }

    public class StudentProgressionDetailViewModel
    {
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Programme { get; set; } = string.Empty;
        public string School { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int YearOfStudy { get; set; }
        public int Semester { get; set; }
        public decimal GPA { get; set; }
        public int FailedCourses { get; set; }
        public int TotalCourses { get; set; }
        public string ProgressionRule { get; set; } = string.Empty;
        public string AcademicStanding { get; set; } = string.Empty;
        public List<CourseProgressionDetail> Courses { get; set; } = new List<CourseProgressionDetail>();
    }

    // Cache model for performance
    public class SenateReportCache
    {
        public int Id { get; set; }
        public string ReportKey { get; set; } = string.Empty;
        public string ReportData { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class CourseProgressionDetail
    {
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public int Credits { get; set; }
        public int Semester { get; set; }
        public decimal TotalScore { get; set; }
        public string CaScore { get; set; }
        public string ExamScore { get; set; }
        public string Grade { get; set; } = string.Empty;
        public decimal GradePoints { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsPassed { get; set; }
    }

    public class ResultSubmissionBatchSummary
    {
        public int Id { get; set; }
        public string CourseName { get; set; }
        public string CourseCode { get; set; }
        public string AssessmentName { get; set; }
        public string SubmissionType { get; set; }
        public WorkflowStatus ApprovalStatus { get; set; }
        public string ApprovalStatusText { get; set; }
        public int TotalRecords { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedByName { get; set; }
        public DateTime? SubmittedForApprovalAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovedByName { get; set; }
        public bool CanPublish { get; set; }
    }

    public class PublishBatchesResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int PublishedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<int> PublishedBatchIds { get; set; } = new List<int>();
    }

    [Keyless]
    [Table("vw_StudentResults")]
    public class StudentResultView
    {
        public string StudentId_Number { get; set; } = default!;

        public string FullName { get; set; } = default!;

        public string Programme { get; set; } = default!;

        public string YearSemester { get; set; } = default!;

        public string CourseCode { get; set; } = default!;

        public string CourseName { get; set; } = default!;

        public decimal? CA { get; set; }

        public decimal? Exam { get; set; }

        public int Attempt { get; set; }

        public int AcademicYearId { get; set; }

        public int YearOfStudy { get; set; }

        public int Semester { get; set; }

        public int DepartmentId { get; set; }

        public int SchoolId { get; set; }

        public int ProgrammeId { get; set; }

        public int ModeOfStudyId { get; set; }

        public string GradeLetter { get; set; } = default!;

        public decimal? GPAValue { get; set; }

        public string Description { get; set; } = default!;

        public int IsPassingGrade { get; set; }

        public decimal? TotalScore { get; set; }
    }
}