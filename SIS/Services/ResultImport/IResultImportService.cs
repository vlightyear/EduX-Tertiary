using Microsoft.AspNetCore.Http;
using SIS.Models.Results;
using static SIS.Services.ResultImport.ResultImportService;

namespace SIS.Services.ResultImport
{
    /// <summary>
    /// Service for handling bulk result imports from Excel files
    /// Integrates with existing AssessmentScoreService and ResultCalculationService
    /// </summary>
    public interface IResultImportService
    {
        /// <summary>
        /// Generate an Excel template for result import
        /// Template includes enrolled students pre-filled with their details
        /// and dynamic columns for each assessment configured for the course
        /// </summary>
        /// <param name="courseId">Course ID</param>
        /// <param name="academicYearId">Academic Year ID</param>
        /// <param name="semester">Semester (1 or 2)</param>
        /// <param name="includeExistingScores">Whether to pre-fill existing scores</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel file as byte array</returns>
        Task<byte[]> GenerateImportTemplateAsync(
            int courseId,
            int academicYearId,
            int semester,
            bool includeExistingScores = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Preview and validate uploaded result file
        /// Performs all validation checks without saving to database
        /// </summary>
        /// <param name="file">Uploaded Excel file</param>
        /// <param name="courseId">Course ID</param>
        /// <param name="academicYearId">Academic Year ID</param>
        /// <param name="semester">Semester (1 or 2)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Preview result with validation details</returns>
        Task<ResultImportPreviewResult> PreviewImportDataAsync(
            IFormFile file,
            int courseId,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default);

        Task<MultiCourseResultImportPreviewResult> PreviewMultiCourseImportDataAsync(
            IFormFile file,
            string importType,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default);

        Task<SupDefResultImportPreviewResult> PreviewSupDefImportDataAsync(
            IFormFile file,
            string importType,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Process validated result imports
        /// Uses existing AssessmentScoreService and ResultCalculationService
        /// to ensure consistency with manual entry
        /// </summary>
        /// <param name="validResults">List of validated result DTOs</param>
        /// <param name="courseId">Course ID</param>
        /// <param name="academicYearId">Academic Year ID</param>
        /// <param name="semester">Semester (1 or 2)</param>
        /// <param name="importedBy">User ID of person performing import</param>
        /// <param name="progressKey">Key for tracking import progress</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Import process result with success/failure details</returns>
        Task<ResultImportProcessResult> ProcessImportAsync(
            List<ResultImportDto> validResults,
            int courseId,
            int academicYearId,
            int semester,
            string importedBy,
            string progressKey = null,
            CancellationToken cancellationToken = default);

        Task<ResultImportProcessResult> ProcessMultiCourseImportAsync(
            List<MultiCourseResultRow> validResults,
            string importType,
            int academicYearId,
            int semester,
            string importedBy,
            string progressKey = null,
            CancellationToken cancellationToken = default);

        Task<ResultImportProcessResult> ProcessSupDefImportAsync(
            List<SupDefResultRow> validResults,
            string importType,
            int academicYearId,
            int semester,
            string importedBy,
            string progressKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate an error report for validation failures
        /// Creates an Excel file with detailed error information
        /// </summary>
        /// <param name="previewResult">Preview result containing validation errors</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Excel error report as byte array</returns>
        Task<byte[]> GenerateErrorReportAsync(
            ResultImportPreviewResult previewResult,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get import progress for real-time updates
        /// Used for AJAX polling during long-running imports
        /// </summary>
        /// <param name="progressKey">Progress tracking key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current import progress</returns>
        Task<ResultImportProgress> GetImportProgressAsync(
            string progressKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleanup progress tracking data
        /// Called after import completion or cancellation
        /// </summary>
        /// <param name="progressKey">Progress tracking key to cleanup</param>
        Task CleanupProgressAsync(string progressKey);

        /// <summary>
        /// Validate course context for import
        /// Ensures course exists, has assessments configured, and has enrolled students
        /// </summary>
        /// <param name="courseId">Course ID</param>
        /// <param name="academicYearId">Academic Year ID</param>
        /// <param name="semester">Semester (1 or 2)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result with any issues found</returns>
        Task<CourseImportValidationResult> ValidateCourseContextAsync(
            int courseId,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get import statistics for a course
        /// Shows how many results have been imported, updated, etc.
        /// </summary>
        /// <param name="courseId">Course ID</param>
        /// <param name="academicYearId">Academic Year ID</param>
        /// <param name="semester">Semester (1 or 2)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Import statistics</returns>
        Task<CourseImportStatistics> GetImportStatisticsAsync(
            int courseId,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Progress tracking for result imports
    /// </summary>
    public class ResultImportProgress
    {
        public string CurrentStep { get; set; }
        public int PercentComplete { get; set; }
        public string Message { get; set; }
        public DateTime LastUpdated { get; set; }
        public int CurrentStudent { get; set; }
        public int TotalStudents { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public bool IsComplete { get; set; }
        public bool HasErrors { get; set; }
    }

    /// <summary>
    /// Validation result for course context
    /// </summary>
    public class CourseImportValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        // Course information
        public bool CourseExists { get; set; }
        public bool HasAssessments { get; set; }
        public bool HasEnrolledStudents { get; set; }
        public bool ResultsAlreadyPublished { get; set; }

        // Statistics
        public int AssessmentCount { get; set; }
        public int EnrolledStudentCount { get; set; }
        public decimal TotalAssessmentWeight { get; set; }

        public string ValidationSummary
        {
            get
            {
                if (IsValid && !Warnings.Any())
                    return "✓ Course ready for import";
                if (IsValid && Warnings.Any())
                    return $"⚠ Ready with {Warnings.Count} warning(s)";
                return $"✗ Cannot import - {Errors.Count} error(s)";
            }
        }
    }

    /// <summary>
    /// Import statistics for a course
    /// </summary>
    public class CourseImportStatistics
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public int AcademicYearId { get; set; }
        public int Semester { get; set; }

        public int TotalEnrolled { get; set; }
        public int StudentsWithScores { get; set; }
        public int StudentsWithCompleteScores { get; set; }
        public int StudentsWithPartialScores { get; set; }
        public int StudentsWithNoScores { get; set; }

        public decimal CompletionPercentage => TotalEnrolled > 0
            ? Math.Round((decimal)StudentsWithCompleteScores / TotalEnrolled * 100, 1)
            : 0;

        public bool ReadyForImport => TotalEnrolled > 0;
        public bool HasAnyScores => StudentsWithScores > 0;

        public DateTime? LastImportDate { get; set; }
        public string LastImportedBy { get; set; }
    }
}