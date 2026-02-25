using System.ComponentModel.DataAnnotations;
using static SIS.Services.ResultImport.ResultImportService;

namespace SIS.Models.Results
{
    /// <summary>
    /// Represents a single row of result data from Excel import
    /// </summary>
    public class ResultImportDto
    {
        public int RowNumber { get; set; }

        [Required]
        public string StudentNumber { get; set; }

        /// <summary>
        /// Student full name for verification purposes only
        /// Not used for matching - StudentNumber is the key
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// Internal Student ID (resolved during validation)
        /// </summary>
        public int StudentId { get; set; }

        /// <summary>
        /// Dictionary of Assessment ID to Score
        /// Key: AssessmentId, Value: Score (0-100)
        /// </summary>
        public Dictionary<int, decimal> AssessmentScores { get; set; } = new Dictionary<int, decimal>();

        /// <summary>
        /// Validation errors for this row
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new List<string>();

        /// <summary>
        /// Warnings (non-blocking issues)
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Whether this student is enrolled in the course
        /// </summary>
        public bool IsEnrolled { get; set; }

        /// <summary>
        /// Whether scores for this student already exist
        /// </summary>
        public bool HasExistingScores { get; set; }
    }

    /// <summary>
    /// Result of the preview/validation phase
    /// </summary>
    public class ResultImportPreviewResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// Total rows in the uploaded file (excluding header)
        /// </summary>
        public int TotalRows { get; set; }

        /// <summary>
        /// Course information
        /// </summary>
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }

        /// <summary>
        /// Academic context
        /// </summary>
        public int AcademicYearId { get; set; }
        public string AcademicYearValue { get; set; }
        public int Semester { get; set; }

        /// <summary>
        /// Valid results ready for import
        /// </summary>
        public List<ResultImportDto> ValidResults { get; set; } = new List<ResultImportDto>();

        /// <summary>
        /// Invalid results with validation errors
        /// </summary>
        public List<ResultImportDto> InvalidResults { get; set; } = new List<ResultImportDto>();

        /// <summary>
        /// Detailed validation results for each row
        /// </summary>
        public List<ResultValidationResult> ValidationResults { get; set; } = new List<ResultValidationResult>();

        /// <summary>
        /// Assessment configuration for this course
        /// </summary>
        public List<AssessmentInfo> Assessments { get; set; } = new List<AssessmentInfo>();

        /// <summary>
        /// Total number of enrolled students expected
        /// </summary>
        public int ExpectedStudentCount { get; set; }

        /// <summary>
        /// Summary statistics
        /// </summary>
        public ImportPreviewSummary Summary { get; set; } = new ImportPreviewSummary();
    }

    public class MultiCourseResultImportPreviewResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// Total rows in the uploaded file (excluding header)
        /// </summary>
        public int TotalRows { get; set; }

        /// <summary>
        /// Course information
        /// </summary>
        public string ImportType { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }

        /// <summary>
        /// Academic context
        /// </summary>
        public int AcademicYearId { get; set; }
        public string AcademicYearValue { get; set; }
        public int Semester { get; set; }

        /// <summary>
        /// Valid results ready for import
        /// </summary>
        public List<MultiCourseResultRow> ValidResults { get; set; } = new List<MultiCourseResultRow>();

        /// <summary>
        /// Invalid results with validation errors
        /// </summary>
        public List<MultiCourseResultRow> InvalidResults { get; set; } = new List<MultiCourseResultRow>();

        /// <summary>
        /// Detailed validation results for each row
        /// </summary>
        public List<ResultValidationResult> ValidationResults { get; set; } = new List<ResultValidationResult>();

        /// <summary>
        /// Assessment configuration for this course
        /// </summary>
        public List<AssessmentInfo> Assessments { get; set; } = new List<AssessmentInfo>();

        /// <summary>
        /// Total number of enrolled students expected
        /// </summary>
        public int ExpectedStudentCount { get; set; }

        /// <summary>
        /// Summary statistics
        /// </summary>
        public ImportPreviewSummary Summary { get; set; } = new ImportPreviewSummary();
    }

    public class SupDefResultImportPreviewResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// Total rows in the uploaded file (excluding header)
        /// </summary>
        public int TotalRows { get; set; }

        /// <summary>
        /// Course information
        /// </summary>
        public string ImportType { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }

        /// <summary>
        /// Academic context
        /// </summary>
        public int AcademicYearId { get; set; }
        public string AcademicYearValue { get; set; }
        public int Semester { get; set; }

        /// <summary>
        /// Valid results ready for import
        /// </summary>
        public List<SupDefResultRow> ValidResults { get; set; } = new List<SupDefResultRow>();

        /// <summary>
        /// Invalid results with validation errors
        /// </summary>
        public List<SupDefResultRow> InvalidResults { get; set; } = new List<SupDefResultRow>();

        /// <summary>
        /// Detailed validation results for each row
        /// </summary>
        public List<ResultValidationResult> ValidationResults { get; set; } = new List<ResultValidationResult>();

        /// <summary>
        /// Assessment configuration for this course
        /// </summary>
        public List<AssessmentInfo> Assessments { get; set; } = new List<AssessmentInfo>();

        /// <summary>
        /// Total number of enrolled students expected
        /// </summary>
        public int ExpectedStudentCount { get; set; }

        /// <summary>
        /// Summary statistics
        /// </summary>
        public ImportPreviewSummary Summary { get; set; } = new ImportPreviewSummary();
    }

    /// <summary>
    /// Validation result for a single row
    /// </summary>
    public class ResultValidationResult
    {
        public int RowNumber { get; set; }
        public string StudentNumber { get; set; }
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Summary of validation issues
        /// </summary>
        public string ValidationSummary
        {
            get
            {
                if (IsValid && !Warnings.Any())
                    return "✓ Valid";
                if (IsValid && Warnings.Any())
                    return $"⚠ Valid with {Warnings.Count} warning(s)";
                return $"✗ Invalid - {Errors.Count} error(s)";
            }
        }
    }

    /// <summary>
    /// Assessment information for display
    /// </summary>
    public class AssessmentInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal WeightPercentage { get; set; }
        public decimal MaxScore { get; set; } = 100;

        /// <summary>
        /// Column header for Excel template
        /// </summary>
        public string ColumnHeader => $"{Name} ({WeightPercentage}%)";
    }

    /// <summary>
    /// Summary statistics for preview
    /// </summary>
    public class ImportPreviewSummary
    {
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int InvalidRows { get; set; }
        public int NewScores { get; set; }
        public int UpdatedScores { get; set; }
        public int MissingStudents { get; set; }
        public int UnenrolledStudents { get; set; }

        public decimal SuccessRate => TotalRows > 0
            ? Math.Round((decimal)ValidRows / TotalRows * 100, 1)
            : 0;
    }

    /// <summary>
    /// Result of the import processing phase
    /// </summary>
    public class ResultImportProcessResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// Total number of students processed
        /// </summary>
        public int TotalProcessed { get; set; }

        /// <summary>
        /// Number of successfully imported students
        /// </summary>
        public int SuccessfulImports { get; set; }

        /// <summary>
        /// Number of failed imports
        /// </summary>
        public int FailedImports { get; set; }

        /// <summary>
        /// Number of scores that had integrity issues and were restored
        /// </summary>
        public int IntegrityRestored { get; set; }

        /// <summary>
        /// Total assessment scores recorded/updated
        /// </summary>
        public int TotalScoresProcessed { get; set; }

        /// <summary>
        /// Number of new scores created
        /// </summary>
        public int NewScoresCreated { get; set; }

        /// <summary>
        /// Number of existing scores updated
        /// </summary>
        public int ExistingScoresUpdated { get; set; }

        /// <summary>
        /// Time taken to process the import
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }

        /// <summary>
        /// Details of successfully imported results
        /// </summary>
        public List<ImportedResultInfo> ImportedResults { get; set; } = new List<ImportedResultInfo>();

        /// <summary>
        /// Details of failed imports
        /// </summary>
        public List<FailedResultImportRow> FailedRows { get; set; } = new List<FailedResultImportRow>();

        /// <summary>
        /// Import summary for display
        /// </summary>
        public ResultImportSummary ImportSummary { get; set; } = new ResultImportSummary();

        /// <summary>
        /// Course context
        /// </summary>
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public int AcademicYearId { get; set; }
        public int Semester { get; set; }
    }

    /// <summary>
    /// Information about a successfully imported result
    /// </summary>
    public class ImportedResultInfo
    {
        public int RowNumber { get; set; }
        public string StudentNumber { get; set; }
        public string StudentName { get; set; }
        public int ScoresRecorded { get; set; }
        public decimal CalculatedTotal { get; set; }
        public string Grade { get; set; }
        public bool IsPassed { get; set; }
        public bool HadIntegrityIssue { get; set; }
        public string Programme { get; set; }
        public string Course { get; set; }
        public string Year { get; set; }
        public string StudentStudyPeriod { get; set; }
        public int Semester { get; set; }
    }

    /// <summary>
    /// Information about a failed import row
    /// </summary>
    public class FailedResultImportRow
    {
        public int RowNumber { get; set; }
        public string StudentNumber { get; set; }
        public string StudentName { get; set; }
        public ResultImportDto OriginalData { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> DetailedErrors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Summary of the import operation
    /// </summary>
    public class ResultImportSummary
    {
        public int TotalRowsInFile { get; set; }
        public int ValidRowsProcessed { get; set; }
        public int InvalidRowsSkipped { get; set; }
        public int StudentsSuccessful { get; set; }
        public int StudentsFailed { get; set; }
        public int TotalScoresRecorded { get; set; }
        public int NewScores { get; set; }
        public int UpdatedScores { get; set; }
        public int IntegrityIssuesFixed { get; set; }
        public int ResultsCalculated { get; set; }
        public string ProcessingDuration { get; set; }

        public decimal SuccessRate => TotalRowsInFile > 0
            ? Math.Round((decimal)StudentsSuccessful / TotalRowsInFile * 100, 1)
            : 0;
    }

    /// <summary>
    /// Template configuration for generating Excel templates
    /// </summary>
    public class ResultTemplateConfiguration
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public int AcademicYearId { get; set; }
        public string AcademicYearValue { get; set; }
        public int Semester { get; set; }
        public List<AssessmentInfo> Assessments { get; set; } = new List<AssessmentInfo>();
        public List<EnrolledStudentInfo> EnrolledStudents { get; set; } = new List<EnrolledStudentInfo>();
        public decimal CoursePassMark { get; set; }
    }

    /// <summary>
    /// Enrolled student information for template pre-fill
    /// </summary>
    public class EnrolledStudentInfo
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string ModeOfStudy { get; set; }

        /// <summary>
        /// Existing scores (if any) for pre-filling
        /// </summary>
        public Dictionary<int, decimal>? ExistingScores { get; set; }
    }

    /// <summary>
    /// Validation context for result imports
    /// </summary>
    public class ResultImportValidationContext
    {
        public int CourseId { get; set; }
        public int AcademicYearId { get; set; }
        public int Semester { get; set; }
        public List<int> EnrolledStudentIds { get; set; } = new List<int>();
        public Dictionary<string, int> StudentNumberToIdMap { get; set; } = new Dictionary<string, int>();
        public List<AssessmentInfo> CourseAssessments { get; set; } = new List<AssessmentInfo>();
        public Dictionary<int, Dictionary<int, decimal>> ExistingScores { get; set; } = new Dictionary<int, Dictionary<int, decimal>>();
        public bool AllowOverwrite { get; set; } = true;
        public bool RequireAllAssessments { get; set; } = false;

        public static implicit operator ValidationContext(ResultImportValidationContext v)
        {
            throw new NotImplementedException();
        }
    }
}