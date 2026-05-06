using ClosedXML.Excel;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.Results;
using SIS.Models.StudentResults;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using static SIS.Controllers.StudentImportController;

namespace SIS.Services.ResultImport
{
    public class ResultImportService : IResultImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAssessmentScoreService _assessmentScoreService;
        private readonly ICourseResultCalculationService _resultCalculationService;
        public readonly IWorkflowService _workflowService;
        private readonly ILogger<ResultImportService> _logger;
        private readonly ConcurrentDictionary<string, ResultImportProgress> _progressTracker;

        // Configuration constants
        private const int MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB
        private const int MAX_ROWS = 1000;
        private const int BATCH_SIZE = 20; // Process 20 students at a time

        public ResultImportService(
            ApplicationDbContext context,
            IAssessmentScoreService assessmentScoreService,
            ICourseResultCalculationService resultCalculationService,
            ILogger<ResultImportService> logger,
            IWorkflowService workflowService    )
        {
            _context = context;
            _assessmentScoreService = assessmentScoreService;
            _resultCalculationService = resultCalculationService;
            _logger = logger;
            _progressTracker = new ConcurrentDictionary<string, ResultImportProgress>();
            _workflowService = workflowService;
        }

        #region Template Generation

        public async Task<byte[]> GenerateImportTemplateAsync(
            int courseId,
            int academicYearId,
            int period,
            bool includeExistingScores = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Generating import template for CourseId={CourseId}, AcademicYearId={AcademicYearId}, Period={Period}",
                    courseId, academicYearId, period);

                // Get course information
                var course = await _context.Courses
                    .Include(c => c.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

                if (course == null)
                    throw new ArgumentException("Course not found", nameof(courseId));

                // Get academic year
                var academicYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(ay => ay.YearId == academicYearId, cancellationToken);

                if (academicYear == null)
                    throw new ArgumentException("Academic year not found", nameof(academicYearId));

                // Get enrolled students
                var enrolledStudents = await _context.StudentCourseRegistrations
                    .Include(scr => scr.Student)
                        .ThenInclude(s => s.ModeOfStudy)
                    .Where(scr =>
                        scr.CourseId == courseId &&
                        scr.AcademicYearId == academicYearId &&
                        scr.YearPeriodId == period)
                    .OrderBy(scr => scr.Student.StudentId_Number)
                    .ToListAsync(cancellationToken);

                if (!enrolledStudents.Any())
                {
                    _logger.LogWarning(
                        "No enrolled students found for CourseId={CourseId}, AcademicYearId={AcademicYearId}, Period={Period}",
                        courseId, academicYearId, period);
                }

                // Get assessments for this course
                var assessments = course.CourseAssessments
                    .Select(ca => new SIS.Models.Results.AssessmentInfo
                    {
                        Id = ca.AssessmentId,
                        Name = ca.Assessment.Name,
                        WeightPercentage = ca.Assessment.WeightPercentage,
                        MaxScore = 100
                    })
                    .OrderBy(a => a.Name)
                    .ToList();

                if (!assessments.Any())
                    throw new InvalidOperationException("No assessments configured for this course");

                // Get existing scores if requested
                Dictionary<int, Dictionary<int, decimal>> existingScores = null;
                if (includeExistingScores)
                {
                    existingScores = await GetExistingScoresAsync(
                        courseId, academicYearId, period, cancellationToken);
                }

                // Generate Excel workbook
                using var workbook = new XLWorkbook();

                // Main data sheet
                var worksheet = workbook.Worksheets.Add("Results Import");
                await PopulateMainWorksheet(
                    worksheet, course, academicYear, period,
                    assessments, enrolledStudents, existingScores);

                // Instructions sheet
                var instructionsSheet = workbook.Worksheets.Add("Instructions");
                PopulateInstructionsSheet(instructionsSheet, course, assessments);

                // Assessment details sheet
                var assessmentSheet = workbook.Worksheets.Add("Assessment Details");
                PopulateAssessmentDetailsSheet(assessmentSheet, assessments, course.PassMark);

                // Convert to byte array
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                _logger.LogInformation(
                    "Template generated successfully with {StudentCount} students and {AssessmentCount} assessments",
                    enrolledStudents.Count, assessments.Count);

                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating import template for CourseId={CourseId}", courseId);
                throw;
            }
        }

        private async Task PopulateMainWorksheet(
            IXLWorksheet worksheet,
            Course course,
            AcademicYear academicYear,
            int semester,
            List<SIS.Models.Results.AssessmentInfo> assessments,
            List<StudentCourseRegistration> enrolledStudents,
            Dictionary<int, Dictionary<int, decimal>> existingScores)
        {
            // Title row
            worksheet.Cell(1, 1).Value = $"Result Import Template - {course.CourseCode}: {course.CourseName}";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Range(1, 1, 1, assessments.Count + 2).Merge();

            // Info row
            worksheet.Cell(2, 1).Value = $"Academic Year: {academicYear.YearValue} | Semester: {semester} | Pass Mark: {course.PassMark}%";
            worksheet.Cell(2, 1).Style.Font.Italic = true;
            worksheet.Range(2, 1, 2, assessments.Count + 2).Merge();

            // Warning row
            worksheet.Cell(3, 1).Value = "⚠ DO NOT modify Student Number or Full Name columns. Leave score cells blank if not yet graded.";
            worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.Red;
            worksheet.Cell(3, 1).Style.Font.Bold = true;
            worksheet.Range(3, 1, 3, assessments.Count + 2).Merge();

            // Header row (row 5)
            int headerRow = 5;
            int col = 1;

            // Fixed columns
            var headerStyle = worksheet.Cell(headerRow, col).Style;
            headerStyle.Font.Bold = true;
            headerStyle.Fill.BackgroundColor = XLColor.DarkBlue;
            headerStyle.Font.FontColor = XLColor.White;
            headerStyle.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(headerRow, col++).Value = "Student Number";
            worksheet.Cell(headerRow, col++).Value = "Full Name";

            // Assessment columns
            foreach (var assessment in assessments)
            {
                var cell = worksheet.Cell(headerRow, col++);
                cell.Value = assessment.ColumnHeader;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
                cell.Style.Font.FontColor = XLColor.Black;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;
            }

            // Apply header style to all header cells
            worksheet.Range(headerRow, 1, headerRow, assessments.Count + 2).Style.Font.Bold = true;
            worksheet.Range(headerRow, 1, headerRow, assessments.Count + 2).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // Data rows
            int row = headerRow + 1;
            foreach (var enrollment in enrolledStudents)
            {
                col = 1;
                var student = enrollment.Student;

                // Student Number (locked)
                var studentNumCell = worksheet.Cell(row, col++);
                studentNumCell.Value = student.StudentId_Number;
                studentNumCell.Style.Fill.BackgroundColor = XLColor.LightGray;
                studentNumCell.Style.Protection.Locked = true;

                // Full Name (locked)
                var nameCell = worksheet.Cell(row, col++);
                nameCell.Value = student.FullName;
                nameCell.Style.Fill.BackgroundColor = XLColor.LightGray;
                nameCell.Style.Protection.Locked = true;

                // Assessment score columns
                foreach (var assessment in assessments)
                {
                    var scoreCell = worksheet.Cell(row, col++);

                    // Pre-fill existing score if available
                    if (existingScores != null &&
                        existingScores.TryGetValue(student.Id, out var studentScores) &&
                        studentScores.TryGetValue(assessment.Id, out var existingScore))
                    {
                        scoreCell.Value = existingScore;
                        scoreCell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                    }

                    // Data validation: 0-100, decimals allowed
                    var validation = scoreCell.SetDataValidation();
                    validation.Decimal.Between(0, 100);
                    validation.ErrorTitle = "Invalid Score";
                    validation.ErrorMessage = "Score must be between 0 and 100";
                    validation.ShowErrorMessage = true;
                    validation.IgnoreBlanks = true;

                    // Format as number with 1 decimal place
                    scoreCell.Style.NumberFormat.Format = "0.0";
                    scoreCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    scoreCell.Style.Protection.Locked = false;
                }

                row++;
            }

            // Auto-fit columns
            worksheet.Columns(1, 2).Width = 15;
            worksheet.Columns(3, assessments.Count + 2).Width = 12;

            // Freeze panes (freeze header and first two columns)
            worksheet.SheetView.FreezeRows(headerRow);
            worksheet.SheetView.FreezeColumns(2);

            // Protect sheet (only score cells are editable)
            worksheet.Protect("ResultImport2025");
        }

        private void PopulateInstructionsSheet(
            IXLWorksheet worksheet,
            Course course,
            List<SIS.Models.Results.AssessmentInfo> assessments)
        {
            int row = 1;

            // Title
            worksheet.Cell(row, 1).Value = "INSTRUCTIONS FOR RESULT IMPORT";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 16;
            worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.DarkBlue;
            row += 2;

            // Course info
            worksheet.Cell(row++, 1).Value = $"Course: {course.CourseCode} - {course.CourseName}";
            worksheet.Cell(row++, 1).Value = $"Pass Mark: {course.PassMark}%";
            row++;

            // General instructions
            worksheet.Cell(row++, 1).Value = "GENERAL INSTRUCTIONS:";
            worksheet.Cell(row - 1, 1).Style.Font.Bold = true;
            worksheet.Cell(row - 1, 1).Style.Font.Underline = XLFontUnderlineValues.Single;

            var instructions = new[]
            {
                "1. DO NOT modify the Student Number or Full Name columns",
                "2. Enter scores only in the assessment columns (highlighted in light blue)",
                "3. Scores must be between 0 and 100",
                "4. You can use decimal values (e.g., 85.5)",
                "5. Leave cells blank if the assessment has not been graded yet",
                "6. DO NOT add or remove rows - only edit score values",
                "7. DO NOT change column headers or their order",
                "8. Save the file after entering all scores",
                "9. Upload the completed file through the import interface"
            };

            foreach (var instruction in instructions)
            {
                worksheet.Cell(row++, 1).Value = instruction;
            }
            row++;

            // Assessment breakdown
            worksheet.Cell(row++, 1).Value = "ASSESSMENT BREAKDOWN:";
            worksheet.Cell(row - 1, 1).Style.Font.Bold = true;
            worksheet.Cell(row - 1, 1).Style.Font.Underline = XLFontUnderlineValues.Single;

            foreach (var assessment in assessments)
            {
                worksheet.Cell(row++, 1).Value =
                    $"• {assessment.Name}: {assessment.WeightPercentage}% of final grade";
            }
            row++;

            // Grading calculation
            worksheet.Cell(row++, 1).Value = "GRADING CALCULATION:";
            worksheet.Cell(row - 1, 1).Style.Font.Bold = true;
            worksheet.Cell(row - 1, 1).Style.Font.Underline = XLFontUnderlineValues.Single;

            worksheet.Cell(row++, 1).Value = "Final Score = Sum of (Assessment Score × Weight ÷ 100)";
            worksheet.Cell(row++, 1).Value = $"Pass/Fail = Final Score >= {course.PassMark}%";
            row++;

            // Example
            worksheet.Cell(row++, 1).Value = "EXAMPLE CALCULATION:";
            worksheet.Cell(row - 1, 1).Style.Font.Bold = true;
            worksheet.Cell(row - 1, 1).Style.Font.Underline = XLFontUnderlineValues.Single;

            if (assessments.Count >= 2)
            {
                var ex1 = assessments[0];
                var ex2 = assessments[1];
                worksheet.Cell(row++, 1).Value =
                    $"If {ex1.Name} = 80 and {ex2.Name} = 75:";
                worksheet.Cell(row++, 1).Value =
                    $"Weighted Score = (80 × {ex1.WeightPercentage}/100) + (75 × {ex2.WeightPercentage}/100)";
            }
            row++;

            // Important notes
            worksheet.Cell(row++, 1).Value = "IMPORTANT NOTES:";
            worksheet.Cell(row - 1, 1).Style.Font.Bold = true;
            worksheet.Cell(row - 1, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
            worksheet.Cell(row - 1, 1).Style.Font.FontColor = XLColor.Red;

            var notes = new[]
            {
                "⚠ The system will validate all scores before import",
                "⚠ Invalid scores will be reported and the entire import will fail",
                "⚠ If you update existing scores, the system will create an audit trail",
                "⚠ After import, final grades will be automatically calculated",
                "⚠ You can download an error report if validation fails"
            };

            foreach (var note in notes)
            {
                var cell = worksheet.Cell(row++, 1);
                cell.Value = note;
                cell.Style.Font.FontColor = XLColor.Red;
            }

            // Auto-fit
            worksheet.Column(1).Width = 80;
            worksheet.Column(1).Style.Alignment.WrapText = true;
        }

        private void PopulateAssessmentDetailsSheet(
            IXLWorksheet worksheet,
            List<SIS.Models.Results.AssessmentInfo> assessments,
            double passmark)
        {
            // Headers
            worksheet.Cell(1, 1).Value = "Assessment ID";
            worksheet.Cell(1, 2).Value = "Assessment Name";
            worksheet.Cell(1, 3).Value = "Weight (%)";
            worksheet.Cell(1, 4).Value = "Max Score";

            // Style headers
            var headerRange = worksheet.Range(1, 1, 1, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // Data
            int row = 2;
            foreach (var assessment in assessments)
            {
                worksheet.Cell(row, 1).Value = assessment.Id;
                worksheet.Cell(row, 2).Value = assessment.Name;
                worksheet.Cell(row, 3).Value = assessment.WeightPercentage;
                worksheet.Cell(row, 4).Value = assessment.MaxScore;
                row++;
            }

            // Summary
            row++;
            worksheet.Cell(row, 2).Value = "TOTAL WEIGHT:";
            worksheet.Cell(row, 2).Style.Font.Bold = true;
            worksheet.Cell(row, 3).Value = assessments.Sum(a => a.WeightPercentage);
            worksheet.Cell(row, 3).Style.Font.Bold = true;

            row++;
            worksheet.Cell(row, 2).Value = "PASS MARK:";
            worksheet.Cell(row, 2).Style.Font.Bold = true;
            worksheet.Cell(row, 3).Value = passmark;
            worksheet.Cell(row, 3).Style.Font.Bold = true;

            // Validation check
            var totalWeight = assessments.Sum(a => a.WeightPercentage);
            if (totalWeight != 100)
            {
                row++;
                var warningCell = worksheet.Cell(row, 2);
                warningCell.Value = $"⚠ WARNING: Total weight is {totalWeight}% (should be 100%)";
                warningCell.Style.Font.FontColor = XLColor.Red;
                warningCell.Style.Font.Bold = true;
                worksheet.Range(row, 2, row, 4).Merge();
            }

            // Auto-fit
            worksheet.Columns().AdjustToContents();
        }

        private async Task<Dictionary<int, Dictionary<int, decimal>>> GetExistingScoresAsync(
            int courseId,
            int academicYearId,
            int period,
            CancellationToken cancellationToken)
        {
            var scores = await _context.StudentAssessmentScores
                .Where(s =>
                    s.CourseId == courseId &&
                    s.AcademicYearId == academicYearId &&
                    s.YearPeriodId == period &&
                    s.IsActive)
                .ToListAsync(cancellationToken);

            var result = new Dictionary<int, Dictionary<int, decimal>>();

            foreach (var score in scores)
            {
                if (!result.ContainsKey(score.StudentId))
                    result[score.StudentId] = new Dictionary<int, decimal>();

                result[score.StudentId][score.AssessmentId] = score.Score;
            }

            return result;
        }

        #endregion

        #region Preview and Validation

        public async Task<ResultImportPreviewResult> PreviewImportDataAsync(
            IFormFile file,
            int courseId,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default)
        {
            var result = new ResultImportPreviewResult
            {
                Success = false,
                CourseId = courseId,
                AcademicYearId = academicYearId,
                Semester = semester
            };

            try
            {
                _logger.LogInformation(
                    "Starting preview for CourseId={CourseId}, File={FileName}, Size={FileSize}",
                    courseId, file.FileName, file.Length);

                // Validate file
                var fileValidation = ValidateUploadedFile(file);
                if (!fileValidation.IsValid)
                {
                    result.Message = fileValidation.ErrorMessage;
                    return result;
                }

                // Validate course context
                var contextValidation = await ValidateCourseContextAsync(
                    courseId, academicYearId, semester, cancellationToken);

                if (!contextValidation.IsValid)
                {
                    result.Message = string.Join("; ", contextValidation.Errors);
                    return result;
                }

                // Get course and academic year info
                var course = await _context.Courses
                    .Include(c => c.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

                var academicYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(ay => ay.YearId == academicYearId, cancellationToken);

                result.CourseCode = course.CourseCode;
                result.CourseName = course.CourseName;
                result.AcademicYearValue = academicYear.YearValue;

                // Get assessments
                result.Assessments = course.CourseAssessments
                    .Select(ca => new SIS.Models.Results.AssessmentInfo
                    {
                        Id = ca.AssessmentId,
                        Name = ca.Assessment.Name,
                        WeightPercentage = ca.Assessment.WeightPercentage
                    })
                    .ToList();

                // Build validation context
                var validationContext = await BuildValidationContextAsync(
                    courseId, academicYearId, semester, null, cancellationToken);

                result.ExpectedStudentCount = validationContext.EnrolledStudentIds.Count;

                // Parse Excel file
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                if (worksheet == null)
                {
                    result.Message = "No worksheet found in Excel file";
                    return result;
                }

                // Parse data
                var parsedResults = ParseExcelData(worksheet, result.Assessments);
                result.TotalRows = parsedResults.Count;

                _logger.LogInformation(
                    "Parsed {RowCount} rows from Excel file", parsedResults.Count);

                // Validate each row
                foreach (var row in parsedResults)
                {
                    var validation = await ValidateResultRow(
                        row, validationContext, cancellationToken);

                    result.ValidationResults.Add(validation);

                    if (validation.IsValid)
                    {
                        result.ValidResults.Add(row);
                    }
                    else
                    {
                        result.InvalidResults.Add(row);
                    }
                }

                // Calculate summary
                result.Summary = CalculatePreviewSummary(
                    result.ValidResults, result.InvalidResults, validationContext);

                result.Success = true;
                result.Message = $"Preview completed: {result.ValidResults.Count} valid, {result.InvalidResults.Count} invalid";

                _logger.LogInformation(
                    "Preview completed: Valid={ValidCount}, Invalid={InvalidCount}",
                    result.ValidResults.Count, result.InvalidResults.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during preview for CourseId={CourseId}", courseId);
                result.Message = $"Error processing file: {ex.Message}";
                return result;
            }
        }

        /*public async Task<MultiCourseResultImportPreviewResult> PreviewMultiCourseImportDataAsync(
            IFormFile file,
            string importType,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default)
        {
            var result = new MultiCourseResultImportPreviewResult
            {
                Success = false,
                ImportType = importType,
                AcademicYearId = academicYearId,
                Semester = semester
            };

            try
            {
                _logger.LogInformation(
                    "Starting preview for ImportType={ImportType}, File={FileName}, Size={FileSize}",
                    importType, file.FileName, file.Length);

                // Validate CSV file
                var fileValidation = ValidateCsvFile(file);
                if (!fileValidation.IsValid)
                {
                    result.Message = fileValidation.ErrorMessage;
                    return result;
                }

                // Get academic year info
                var academicYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(ay => ay.YearId == academicYearId, cancellationToken);

                if (academicYear == null)
                {
                    result.Message = "Academic year not found";
                    return result;
                }

                result.AcademicYearValue = academicYear.YearValue;

                // Parse CSV file
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var parsedResults = new List<MultiCourseResultRow>();
                var courseCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var studentIdsInFile = new HashSet<string>();
                int lineNumber = 0;

                // Read and parse CSV line by line
                while (!reader.EndOfStream)
                {
                    lineNumber++;
                    var line = await reader.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var columns = line.Split(',');

                    if (columns.Length < 4)
                    {
                        _logger.LogWarning(
                            "Line {LineNumber} has insufficient columns: {Line}",
                            lineNumber, line);
                        continue;
                    }

                    try
                    {
                        // CSV Format: StudentID, CourseCode, CAMark, ExamMark
                        var resultRow = new MultiCourseResultRow
                        {
                            RowNumber = lineNumber,
                            StudentIdNumber = columns[0].Trim(),
                            CourseCode = columns[1].Trim(),
                            CAMark = ParseDecimalMark(columns[2].Trim()),
                            ExamMark = ParseDecimalMark(columns[3].Trim())
                        };

                        parsedResults.Add(resultRow);
                        courseCodesInFile.Add(resultRow.CourseCode);
                        studentIdsInFile.Add(resultRow.StudentIdNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Error parsing line {LineNumber}: {Line}",
                            lineNumber, line);
                    }
                }

                result.TotalRows = parsedResults.Count;

                _logger.LogInformation(
                    "Parsed {RowCount} rows from CSV file with {CourseCount} unique courses and {StudentCount} unique students",
                    parsedResults.Count, courseCodesInFile.Count, studentIdsInFile.Count);

                // Get all students with their programme information
                var students = await _context.Students
                    .Where(s => studentIdsInFile.Contains(s.StudentId_Number))
                    .Select(s => new
                    {
                        s.Id,
                        s.StudentId_Number,
                        s.ProgrammeId
                    })
                    .ToListAsync(cancellationToken);

                var studentLookup = students.ToDictionary(
                    s => s.StudentId_Number,
                    s => new { s.Id, s.ProgrammeId },
                    StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation("Found {StudentCount} students in database", students.Count);

                // Get all courses for the programmes of these students
                var programmeIds = students.Select(s => s.ProgrammeId).Distinct().ToList();

                var courses = await _context.Courses
                    .Include(c => c.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .Where(c => programmeIds.Contains(c.ProgrammeID))
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Loaded {CourseCount} courses from {ProgrammeCount} programmes",
                    courses.Count, programmeIds.Count);

                // Build validation contexts for each unique student-course combination
                var validationContexts = new Dictionary<string, ResultImportValidationContext>();

                foreach (var row in parsedResults)
                {
                    // Get student's programme
                    if (!studentLookup.TryGetValue(row.StudentIdNumber, out var studentInfo))
                    {
                        _logger.LogWarning(
                            "Student {StudentId} not found in database",
                            row.StudentIdNumber);
                        continue;
                    }

                    // Find course by CourseCode AND ProgrammeID
                    var course = courses.FirstOrDefault(c =>
                                        c.CourseCode.Replace(" ", "").Equals(row.CourseCode.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) &&
                                        c.ProgrammeID == studentInfo.ProgrammeId);

                    if (course == null)
                    {
                        _logger.LogWarning(
                            "Course {CourseCode} not found for student {StudentId}'s programme (ProgrammeID: {ProgrammeId})",
                            row.CourseCode, row.StudentIdNumber, studentInfo.ProgrammeId);
                        continue;
                    }

                    // Store the resolved CourseId in the row for later use
                    row.CourseId = course.Id;
                    row.StudentId = studentInfo.Id;

                    // Create unique key for student's programme + course combination
                    string contextKey = $"{studentInfo.ProgrammeId}_{course.Id}";

                    if (!validationContexts.ContainsKey(contextKey))
                    {
                        try
                        {
                            var context = await BuildValidationContextAsync(
                                course.Id, academicYearId, semester, cancellationToken);
                            validationContexts[contextKey] = context;

                            _logger.LogInformation(
                                "Built validation context for Course {CourseCode} (ID: {CourseId}) in Programme {ProgrammeId}",
                                course.CourseCode, course.Id, studentInfo.ProgrammeId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Error building validation context for Course {CourseCode} in Programme {ProgrammeId}",
                                row.CourseCode, studentInfo.ProgrammeId);
                        }
                    }
                }

                // Validate each row
                foreach (var row in parsedResults)
                {
                    ResultValidationResult validation;

                    // Check if student exists
                    if (!studentLookup.TryGetValue(row.StudentIdNumber, out var studentInfo))
                    {
                        validation = new ResultValidationResult
                        {
                            RowNumber = row.RowNumber,
                            StudentNumber = row.StudentIdNumber,
                            IsValid = false,
                            Errors = new List<string> { $"Student '{row.StudentIdNumber}' not found in database" }
                        };
                    }
                    // Check if course was resolved
                    else if (row.CourseId == 0)
                    {
                        validation = new ResultValidationResult
                        {
                            RowNumber = row.RowNumber,
                            StudentNumber = row.StudentIdNumber,
                            IsValid = false,
                            Errors = new List<string>
                    {
                        $"Course '{row.CourseCode}' not found for student's programme (ProgrammeID: {studentInfo.ProgrammeId})"
                    }
                        };
                    }
                    else
                    {
                        // Get the validation context
                        string contextKey = $"{studentInfo.ProgrammeId}_{row.CourseId}";

                        if (!validationContexts.TryGetValue(contextKey, out var context))
                        {
                            validation = new ResultValidationResult
                            {
                                RowNumber = row.RowNumber,
                                StudentNumber = row.StudentIdNumber,
                                IsValid = false,
                                Errors = new List<string> { "Validation context not available" }
                            };
                        }
                        else
                        {
                            validation = await ValidateMultiCourseResultRow(
                                row, context, cancellationToken);
                        }
                    }

                    result.ValidationResults.Add(validation);

                    if (validation.IsValid)
                    {
                        result.ValidResults.Add(row);
                    }
                    else
                    {
                        result.InvalidResults.Add(row);
                    }
                }

                // Calculate summary
                result.Summary = CalculateMultiCoursePreviewSummary(
                    result.ValidResults, result.InvalidResults, validationContexts);

                result.Success = true;
                result.Message = $"Preview completed: {result.ValidResults.Count} valid, {result.InvalidResults.Count} invalid across {courseCodesInFile.Count} unique course codes";

                _logger.LogInformation(
                    "Preview completed: Valid={ValidCount}, Invalid={InvalidCount}, Courses={CourseCount}, Programmes={ProgrammeCount}",
                    result.ValidResults.Count, result.InvalidResults.Count,
                    courseCodesInFile.Count, programmeIds.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during multi-course preview");
                result.Message = $"Error processing file: {ex.Message}";
                return result;
            }
        }
        public class MultiCourseResultRow
        {
            public int RowNumber { get; set; }
            public string StudentIdNumber { get; set; }
            public string CourseCode { get; set; }
            public decimal? CAMark { get; set; }
            public decimal? ExamMark { get; set; }
            public int StudentId { get; set; }
            public int CourseId { get; set; }
        }*/

        public async Task<MultiCourseResultImportPreviewResult> PreviewMultiCourseImportDataAsync(
            IFormFile file,
            string importType,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default)
        {
            var result = new MultiCourseResultImportPreviewResult
            {
                Success = false,
                ImportType = importType,
                AcademicYearId = academicYearId,
                Semester = semester
            };

            try
            {
                _logger.LogInformation(
                    "Starting preview for ImportType={ImportType}, File={FileName}, Size={FileSize}",
                    importType, file.FileName, file.Length);

                // Validate CSV file
                var fileValidation = ValidateCsvFile(file);
                if (!fileValidation.IsValid)
                {
                    result.Message = fileValidation.ErrorMessage;
                    return result;
                }

                // Get academic year info
                var academicYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(ay => ay.YearId == academicYearId, cancellationToken);

                if (academicYear == null)
                {
                    result.Message = "Academic year not found";
                    return result;
                }

                result.AcademicYearValue = academicYear.YearValue;

                // Parse CSV file
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var parsedResults = new List<MultiCourseResultRow>();
                var courseCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var studentIdsInFile = new HashSet<string>();
                int lineNumber = 0;

                // Read and parse CSV line by line
                while (!reader.EndOfStream)
                {
                    lineNumber++;
                    var line = await reader.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var columns = line.Split(',');

                    if (columns.Length < 4)
                    {
                        _logger.LogWarning(
                            "Line {LineNumber} has insufficient columns: {Line}",
                            lineNumber, line);
                        continue;
                    }

                    try
                    {
                        // CSV Format: StudentID, CourseCode, CAMark, ExamMark
                        var resultRow = new MultiCourseResultRow
                        {
                            RowNumber = lineNumber,
                            StudentIdNumber = columns[0].Trim(),
                            CourseCode = columns[1].Trim(),
                            CAMark = ParseDecimalMark(columns[2].Trim()),
                            ExamMark = ParseDecimalMark(columns[3].Trim())
                        };

                        parsedResults.Add(resultRow);
                        courseCodesInFile.Add(resultRow.CourseCode);
                        studentIdsInFile.Add(resultRow.StudentIdNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Error parsing line {LineNumber}: {Line}",
                            lineNumber, line);
                    }
                }

                result.TotalRows = parsedResults.Count;

                _logger.LogInformation(
                    "Parsed {RowCount} rows from CSV file with {CourseCount} unique courses and {StudentCount} unique students",
                    parsedResults.Count, courseCodesInFile.Count, studentIdsInFile.Count);

                // Get all students with their programme information AND FULL NAME
                var students = await _context.Students
                    .Include(s => s.CurrentYearPeriod)
                        .ThenInclude(cyp => cyp.AcademicPeriod)
                    .Where(s => studentIdsInFile.Contains(s.StudentId_Number))
                    .Select(s => new
                    {
                        s.Id,
                        s.StudentId_Number,
                        s.ProgrammeId,
                        s.FullName,
                        s.CurrentYearPeriodId,
                        s.StudentCurrentYear,
                        s.AcademicYearId,
                        s.CurrentYearPeriod,
                        AcademicPeriodId = s.CurrentYearPeriod.AcademicPeriod.Id
                    })
                    .ToListAsync(cancellationToken);

                var studentLookup = students.ToDictionary(
                    s => s.StudentId_Number,
                    s => new { s.Id, s.ProgrammeId, s.FullName, s.CurrentYearPeriodId, s.StudentCurrentYear, s.AcademicYearId, s.AcademicPeriodId },
                    StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation("Found {StudentCount} students in database", students.Count);

                // Get all courses for the programmes of these students
                var programmeIds = students.Select(s => s.ProgrammeId).Distinct().ToList();

                var courses = await _context.Courses
                    .Include(c => c.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .Where(c => programmeIds.Contains(c.ProgrammeID))
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Loaded {CourseCount} courses from {ProgrammeCount} programmes",
                    courses.Count, programmeIds.Count);

                // Build validation contexts for each unique student-course combination
                var validationContexts = new Dictionary<string, ResultImportValidationContext>();
                var failedContextKeys = new HashSet<string>(); // ADDED: Track failed contexts

                foreach (var row in parsedResults)
                {
                    // Get student's programme
                    if (!studentLookup.TryGetValue(row.StudentIdNumber, out var studentInfo))
                    {
                        _logger.LogWarning(
                            "Student {StudentId} not found in database",
                            row.StudentIdNumber);
                        continue;
                    }

                    // Store student's full name
                    row.StudentFullName = studentInfo.FullName;

                    // Find course by CourseCode AND ProgrammeID
                    var course = courses.FirstOrDefault(c =>
                        c.CourseCode.Replace(" ", "").Equals(row.CourseCode.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) &&
                        c.ProgrammeID == studentInfo.ProgrammeId &&
                        c.YearTaken == studentInfo.StudentCurrentYear &&
                        c.PeriodTakenId == studentInfo.AcademicPeriodId &&
                        c.IsExaminable);

                    if (course == null)
                    {
                        course = _context.Courses
                            .Where(c =>
                                c.CourseCode.Replace(" ", "")
                                    .Equals(row.CourseCode.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)
                                && c.ProgrammeID == studentInfo.ProgrammeId
                                && c.PeriodTakenId == studentInfo.AcademicPeriodId
                                && c.IsExaminable
                                && _context.StudentCourseRegistrations.Any(scr =>
                                    scr.CourseId == c.Id
                                    && scr.StudentId == studentInfo.Id
                                    && scr.AcademicYearId == studentInfo.AcademicYearId))
                            .FirstOrDefault();

                        if (course == null)
                        {
                            _logger.LogWarning(
                            "Course {CourseCode} not found for student {StudentId}'s programme (ProgrammeID: {ProgrammeId})",
                            row.CourseCode, row.StudentIdNumber, studentInfo.ProgrammeId);
                            continue;
                        }
                    }

                    // Store the resolved CourseId in the row for later use
                    row.CourseId = course.Id;
                    row.StudentId = studentInfo.Id;

                    // Create unique key for student's programme + course combination
                    string contextKey = $"{studentInfo.ProgrammeId}_{course.Id}";

                    if (!validationContexts.ContainsKey(contextKey) && !failedContextKeys.Contains(contextKey))
                    {
                        try
                        {
                            var context = await BuildValidationContextAsync(
                                course.Id, academicYearId, semester, studentInfo.ProgrammeId, cancellationToken);
                            validationContexts[contextKey] = context;


                            _logger.LogInformation(
                                "Built validation context for Course {CourseCode} (ID: {CourseId}) in Programme {ProgrammeId}",
                                course.CourseCode, course.Id, studentInfo.ProgrammeId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Error building validation context for Course {CourseCode} in Programme {ProgrammeId}",
                                row.CourseCode, studentInfo.ProgrammeId);

                            // ADDED: Track that this context failed to build
                            failedContextKeys.Add(contextKey);
                        }
                    }
                }

                // Validate each row
                foreach (var row in parsedResults)
                {
                    ResultValidationResult validation;

                    // Check if student exists
                    if (!studentLookup.TryGetValue(row.StudentIdNumber, out var studentInfo))
                    {
                        validation = new ResultValidationResult
                        {
                            RowNumber = row.RowNumber,
                            StudentNumber = row.StudentIdNumber,
                            IsValid = false,
                            Errors = new List<string> { $"Student '{row.StudentIdNumber}' not found in database" }
                        };
                    }
                    // Check if course was resolved
                    else if (row.CourseId == 0)
                    {
                        validation = new ResultValidationResult
                        {
                            RowNumber = row.RowNumber,
                            StudentNumber = row.StudentIdNumber,
                            IsValid = false,
                            Errors = new List<string>
                    {
                        $"Course '{row.CourseCode}' not found for student's programme (ProgrammeID: {studentInfo.ProgrammeId}) & student's year: {studentInfo.StudentCurrentYear} & student's period: {studentInfo.AcademicPeriodId}"
                    }
                        };
                    }
                    else
                    {
                        // Get the validation context
                        string contextKey = $"{studentInfo.ProgrammeId}_{row.CourseId}";

                        // IMPROVED: Check if context building failed
                        if (failedContextKeys.Contains(contextKey))
                        {
                            validation = new ResultValidationResult
                            {
                                RowNumber = row.RowNumber,
                                StudentNumber = row.StudentIdNumber,
                                IsValid = false,
                                Errors = new List<string> { $"Could not build validation context for course '{row.CourseCode}'. Check if student is enrolled." }
                            };
                        }
                        else if (!validationContexts.TryGetValue(contextKey, out var context))
                        {
                            validation = new ResultValidationResult
                            {
                                RowNumber = row.RowNumber,
                                StudentNumber = row.StudentIdNumber,
                                IsValid = false,
                                Errors = new List<string> { "Validation context not available" }
                            };
                        }
                        else
                        {
                            validation = await ValidateMultiCourseResultRow(
                                row, context, cancellationToken);
                        }
                    }

                    result.ValidationResults.Add(validation);

                    if (validation.IsValid)
                    {
                        result.ValidResults.Add(row);
                    }
                    else
                    {
                        result.InvalidResults.Add(row);
                    }
                }

                // Calculate summary
                result.Summary = CalculateMultiCoursePreviewSummary(
                    result.ValidResults, result.InvalidResults, validationContexts);

                result.Success = true;
                result.Message = $"Preview completed: {result.ValidResults.Count} valid, {result.InvalidResults.Count} invalid across {courseCodesInFile.Count} unique course codes";

                _logger.LogInformation(
                    "Preview completed: Valid={ValidCount}, Invalid={InvalidCount}, Courses={CourseCount}, Programmes={ProgrammeCount}",
                    result.ValidResults.Count, result.InvalidResults.Count,
                    courseCodesInFile.Count, programmeIds.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during multi-course preview");
                result.Message = $"Error processing file: {ex.Message}";
                return result;
            }
        }

        public async Task<SupDefResultImportPreviewResult> PreviewSupDefImportDataAsync(
            IFormFile file,
            string importType,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default)
        {
            var result = new SupDefResultImportPreviewResult
            {
                Success = false,
                ImportType = importType,
                AcademicYearId = academicYearId,
                Semester = semester
            };

            try
            {
                _logger.LogInformation(
                    "Starting preview for ImportType={ImportType}, File={FileName}, Size={FileSize}",
                    importType, file.FileName, file.Length);

                // Validate CSV file
                var fileValidation = ValidateCsvFile(file);
                if (!fileValidation.IsValid)
                {
                    result.Message = fileValidation.ErrorMessage;
                    return result;
                }

                // Get academic year info
                var academicYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(ay => ay.YearId == academicYearId, cancellationToken);

                if (academicYear == null)
                {
                    result.Message = "Academic year not found";
                    return result;
                }

                result.AcademicYearValue = academicYear.YearValue;

                // Parse CSV file
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var parsedResults = new List<SupDefResultRow>();
                var courseCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var studentIdsInFile = new HashSet<string>();
                int lineNumber = 0;

                // Read and parse CSV line by line
                while (!reader.EndOfStream)
                {
                    lineNumber++;
                    var line = await reader.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var columns = line.Split(',');

                    if (columns.Length < 3)
                    {
                        _logger.LogWarning(
                            "Line {LineNumber} has insufficient columns: {Line}",
                            lineNumber, line);
                        continue;
                    }

                    try
                    {
                        // CSV Format: StudentID, CourseCode, CAMark, ExamMark
                        var resultRow = new SupDefResultRow
                        {
                            RowNumber = lineNumber,
                            StudentIdNumber = columns[0].Trim(),
                            CourseCode = columns[1].Trim(),
                            ExamMark = (decimal)ParseDecimalMark(columns[2].Trim())
                        };

                        parsedResults.Add(resultRow);
                        courseCodesInFile.Add(resultRow.CourseCode);
                        studentIdsInFile.Add(resultRow.StudentIdNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Error parsing line {LineNumber}: {Line}",
                            lineNumber, line);
                    }
                }

                result.TotalRows = parsedResults.Count;

                _logger.LogInformation(
                    "Parsed {RowCount} rows from CSV file with {CourseCount} unique courses and {StudentCount} unique students",
                    parsedResults.Count, courseCodesInFile.Count, studentIdsInFile.Count);

                // Get all students with their programme information AND FULL NAME
                var students = await _context.Students
                    .Include(s => s.CurrentYearPeriod)
                        .ThenInclude(cyp => cyp.AcademicPeriod)
                    .Where(s => studentIdsInFile.Contains(s.StudentId_Number))
                    .Select(s => new
                    {
                        s.Id,
                        s.StudentId_Number,
                        s.ProgrammeId,
                        s.FullName,
                        s.CurrentYearPeriodId,
                        s.StudentCurrentYear,
                        s.AcademicYearId,
                        AcademicPeriodId = s.CurrentYearPeriod.AcademicPeriod.Id
                    })
                    .ToListAsync(cancellationToken);

                var studentLookup = students.ToDictionary(
                    s => s.StudentId_Number,
                    s => new { s.Id, s.ProgrammeId, s.FullName, s.CurrentYearPeriodId, s.StudentCurrentYear, s.AcademicYearId, s.AcademicPeriodId },
                    StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation("Found {StudentCount} students in database", students.Count);

                // Get all courses for the programmes of these students
                var programmeIds = students.Select(s => s.ProgrammeId).Distinct().ToList();

                var courses = await _context.Courses
                    .Include(c => c.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .Where(c => programmeIds.Contains(c.ProgrammeID))
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Loaded {CourseCount} courses from {ProgrammeCount} programmes",
                    courses.Count, programmeIds.Count);

                // Build validation contexts for each unique student-course combination
                var validationContexts = new Dictionary<string, ResultImportValidationContext>();
                var failedContextKeys = new HashSet<string>(); // ADDED: Track failed contexts

                foreach (var row in parsedResults)
                {
                    // Get student's programme
                    if (!studentLookup.TryGetValue(row.StudentIdNumber, out var studentInfo))
                    {
                        _logger.LogWarning(
                            "Student {StudentId} not found in database",
                            row.StudentIdNumber);
                        continue;
                    }

                    // Store student's full name
                    row.StudentFullName = studentInfo.FullName;

                    // Find course by CourseCode AND ProgrammeID
                    var course = courses.FirstOrDefault(c =>
                        c.CourseCode.Replace(" ", "").Equals(row.CourseCode.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) &&
                        c.ProgrammeID == studentInfo.ProgrammeId &&
                        c.YearTaken == studentInfo.StudentCurrentYear &&
                        c.PeriodTakenId == studentInfo.AcademicPeriodId &&
                        c.IsExaminable);

                    if (course == null)
                    {
                        course = _context.Courses
                            .Where(c =>
                                c.CourseCode.Replace(" ", "")
                                    .Equals(row.CourseCode.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)
                                && c.ProgrammeID == studentInfo.ProgrammeId
                                && c.PeriodTakenId == studentInfo.AcademicPeriodId
                                && c.IsExaminable
                                && _context.StudentCourseRegistrations.Any(scr =>
                                    scr.CourseId == c.Id
                                    && scr.StudentId == studentInfo.Id
                                    && scr.AcademicYearId == studentInfo.AcademicYearId))
                            .FirstOrDefault();

                        if (course == null)
                        {
                            _logger.LogWarning(
                            "Course {CourseCode} not found for student {StudentId}'s programme (ProgrammeID: {ProgrammeId})",
                            row.CourseCode, row.StudentIdNumber, studentInfo.ProgrammeId);
                            continue;
                        }
                    }

                    // Store the resolved CourseId in the row for later use
                    row.CourseId = course.Id;
                    row.StudentId = studentInfo.Id;

                    // Create unique key for student's programme + course combination
                    string contextKey = $"{studentInfo.ProgrammeId}_{course.Id}";

                    if (!validationContexts.ContainsKey(contextKey) && !failedContextKeys.Contains(contextKey))
                    {
                        try
                        {
                            var context = await BuildValidationContextAsync(
                                course.Id, academicYearId, semester, studentInfo.ProgrammeId, cancellationToken);
                            validationContexts[contextKey] = context;


                            _logger.LogInformation(
                                "Built validation context for Course {CourseCode} (ID: {CourseId}) in Programme {ProgrammeId}",
                                course.CourseCode, course.Id, studentInfo.ProgrammeId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Error building validation context for Course {CourseCode} in Programme {ProgrammeId}",
                                row.CourseCode, studentInfo.ProgrammeId);

                            // Track that this context failed to build
                            failedContextKeys.Add(contextKey);
                        }
                    }
                }

                // Validate each row
                foreach (var row in parsedResults)
                {
                    ResultValidationResult validation;

                    // Check if student exists
                    if (!studentLookup.TryGetValue(row.StudentIdNumber, out var studentInfo))
                    {
                        validation = new ResultValidationResult
                        {
                            RowNumber = row.RowNumber,
                            StudentNumber = row.StudentIdNumber,
                            IsValid = false,
                            Errors = new List<string> { $"Student '{row.StudentIdNumber}' not found in database" }
                        };
                    }
                    // Check if course was resolved
                    else if (row.CourseId == 0)
                    {
                        validation = new ResultValidationResult
                        {
                            RowNumber = row.RowNumber,
                            StudentNumber = row.StudentIdNumber,
                            IsValid = false,
                            Errors = new List<string>
                    {
                        $"Course '{row.CourseCode}' not found for student's programme (ProgrammeID: {studentInfo.ProgrammeId}) & student's year: {studentInfo.StudentCurrentYear} & student's period: {studentInfo.AcademicPeriodId}"
                    }
                        };
                    }
                    else
                    {
                        // Get the validation context
                        string contextKey = $"{studentInfo.ProgrammeId}_{row.CourseId}";

                        // Check if context building failed
                        if (failedContextKeys.Contains(contextKey))
                        {
                            validation = new ResultValidationResult
                            {
                                RowNumber = row.RowNumber,
                                StudentNumber = row.StudentIdNumber,
                                IsValid = false,
                                Errors = new List<string> { $"Could not build validation context for course '{row.CourseCode}'. Check if student is enrolled." }
                            };
                        }
                        else if (!validationContexts.TryGetValue(contextKey, out var context))
                        {
                            validation = new ResultValidationResult
                            {
                                RowNumber = row.RowNumber,
                                StudentNumber = row.StudentIdNumber,
                                IsValid = false,
                                Errors = new List<string> { "Validation context not available" }
                            };
                        }
                        else
                        {
                            validation = await ValidateSupDefResultRow(
                                row, context, cancellationToken);
                        }
                    }

                    result.ValidationResults.Add(validation);

                    if (validation.IsValid)
                    {
                        result.ValidResults.Add(row);
                    }
                    else
                    {
                        result.InvalidResults.Add(row);
                    }
                }

                // Calculate summary
                result.Summary = CalculateSupDefPreviewSummary(
                    result.ValidResults, result.InvalidResults, validationContexts);

                result.Success = true;
                result.Message = $"Preview completed: {result.ValidResults.Count} valid, {result.InvalidResults.Count} invalid across {courseCodesInFile.Count} unique course codes";

                _logger.LogInformation(
                    "Preview completed: Valid={ValidCount}, Invalid={InvalidCount}, Courses={CourseCount}, Programmes={ProgrammeCount}",
                    result.ValidResults.Count, result.InvalidResults.Count,
                    courseCodesInFile.Count, programmeIds.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during multi-course preview");
                result.Message = $"Error processing file: {ex.Message}";
                return result;
            }
        }

        public class MultiCourseResultRow
        {
            public int RowNumber { get; set; }
            public string StudentIdNumber { get; set; }
            public string StudentFullName { get; set; }
            public string CourseCode { get; set; }
            public decimal? CAMark { get; set; }
            public decimal? ExamMark { get; set; }
            public int StudentId { get; set; }
            public int CourseId { get; set; }
        }

        public class SupDefResultRow
        {
            public int RowNumber { get; set; }
            public string StudentIdNumber { get; set; }
            public string StudentFullName { get; set; }
            public string CourseCode { get; set; }
            public decimal ExamMark { get; set; }
            public int StudentId { get; set; }
            public int CourseId { get; set; }
        }

        private FileValidationResult ValidateCsvFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Please select a file to upload"
                };
            }

            if (file.Length > MAX_FILE_SIZE)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File size exceeds maximum limit of {MAX_FILE_SIZE / 1024 / 1024}MB"
                };
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".csv")
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Only CSV files (.csv) are supported for multi-course import"
                };
            }

            return new FileValidationResult { IsValid = true };
        }

        private decimal? ParseDecimalMark(string markString)
        {
            if (string.IsNullOrWhiteSpace(markString))
                return null;

            if (decimal.TryParse(markString.Trim(), out decimal mark))
                return mark;

            return null;
        }

        private async Task<ResultValidationResult> ValidateMultiCourseResultRow(
            MultiCourseResultRow row,
            ResultImportValidationContext context,
            CancellationToken cancellationToken)
        {
            var validation = new ResultValidationResult
            {
                RowNumber = row.RowNumber,
                StudentNumber = row.StudentIdNumber,
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            // Validate student ID
            if (string.IsNullOrWhiteSpace(row.StudentIdNumber))
            {
                validation.Errors.Add("Student ID is required");
                validation.IsValid = false;
            }
            else if (!context.StudentNumberToIdMap.ContainsKey(row.StudentIdNumber.ToLower()))
            {
                validation.Errors.Add($"Student '{row.StudentIdNumber}' is not enrolled in course '{row.CourseCode}'");
                validation.IsValid = false;
            }

            // Validate CA mark (0-40)
            if (row.CAMark.HasValue)
            {
                if (row.CAMark.Value < 0 || row.CAMark.Value > 40)
                {
                    validation.Errors.Add($"CA mark must be between 0 and 40 (got {row.CAMark.Value})");
                    validation.IsValid = false;
                }
            }

            // Validate Exam mark (0-60)
            if (row.ExamMark.HasValue)
            {
                if (row.ExamMark.Value < 0 || row.ExamMark.Value > 100)
                {
                    validation.Errors.Add($"Exam mark must be between 0 and 100 (got {row.ExamMark.Value})");
                    validation.IsValid = false;
                }
            }

            // Check if at least one mark is provided
            if (!row.CAMark.HasValue && !row.ExamMark.HasValue)
            {
                validation.Errors.Add("At least one mark (CA or Exam) must be provided");
                validation.IsValid = false;
            }

            return validation;
        }

         private ImportPreviewSummary CalculateMultiCoursePreviewSummary(
            List<MultiCourseResultRow> validResults,
            List<MultiCourseResultRow> invalidResults,
            Dictionary<string, ResultImportValidationContext> validationContexts)
        {
            var summary = new ImportPreviewSummary
            {
                TotalRows = validResults.Count + invalidResults.Count,
                ValidRows = validResults.Count,
                InvalidRows = invalidResults.Count,
                NewScores = validResults.Count,
                UpdatedScores = 0,
                MissingStudents = 0,
                UnenrolledStudents = 0
            };

            // Group by course to get statistics per course
            var courseGroups = validResults.GroupBy(r => r.CourseCode);

            foreach (var group in courseGroups)
            {
                _logger.LogInformation(
                    "Course {CourseCode}: {Count} valid results",
                    group.Key, group.Count());
            }

            return summary;
        }

        private async Task<ResultValidationResult> ValidateSupDefResultRow(
            SupDefResultRow row,
            ResultImportValidationContext context,
            CancellationToken cancellationToken)
        {
            var validation = new ResultValidationResult
            {
                RowNumber = row.RowNumber,
                StudentNumber = row.StudentIdNumber,
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            // Validate student ID
            if (string.IsNullOrWhiteSpace(row.StudentIdNumber))
            {
                validation.Errors.Add("Student ID is required");
                validation.IsValid = false;
            }
            else if (!context.StudentNumberToIdMap.ContainsKey(row.StudentIdNumber.ToLower()))
            {
                validation.Errors.Add($"Student '{row.StudentIdNumber}' is not enrolled in course '{row.CourseCode}'");
                validation.IsValid = false;
            }

            // Validate Exam mark (0-60)
            if (row.ExamMark < 0 || row.ExamMark > 100)
            {
                validation.Errors.Add($"Exam mark must be between 0 and 100 (got {row.ExamMark})");
                validation.IsValid = false;
            }

            return validation;
        }

        private ImportPreviewSummary CalculateSupDefPreviewSummary(
           List<SupDefResultRow> validResults,
           List<SupDefResultRow> invalidResults,
           Dictionary<string, ResultImportValidationContext> validationContexts)
        {
            var summary = new ImportPreviewSummary
            {
                TotalRows = validResults.Count + invalidResults.Count,
                ValidRows = validResults.Count,
                InvalidRows = invalidResults.Count,
                NewScores = validResults.Count,
                UpdatedScores = 0,
                MissingStudents = 0,
                UnenrolledStudents = 0
            };

            // Group by course to get statistics per course
            var courseGroups = validResults.GroupBy(r => r.CourseCode);

            foreach (var group in courseGroups)
            {
                _logger.LogInformation(
                    "Course {CourseCode}: {Count} valid results",
                    group.Key, group.Count());
            }

            return summary;
        }

        public class ValidationResult
        {
            public int RowNumber { get; set; }
            public string StudentId { get; set; }
            public string CourseCode { get; set; }
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        private FileValidationResult ValidateUploadedFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Please select a file to upload"
                };
            }

            if (file.Length > MAX_FILE_SIZE)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File size exceeds maximum limit of {MAX_FILE_SIZE / 1024 / 1024}MB"
                };
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx")
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Only Excel files (.xlsx) are supported"
                };
            }

            return new FileValidationResult { IsValid = true };
        }

        private async Task<ResultImportValidationContext> BuildValidationContextAsync(
             int courseId,
             int academicYearId,
             int period,
             int? programmeId,
             CancellationToken cancellationToken)
        {
            var context = new ResultImportValidationContext
            {
                CourseId = courseId,
                AcademicYearId = academicYearId,
                Semester = period
            };

            // Get enrolled students
            var enrolledStudents = await _context.StudentCourseRegistrations
                .Where(scr =>
                    scr.CourseId == courseId &&
                    scr.AcademicYearId == academicYearId &&
                    scr.YearPeriodId == period &&
                    (!programmeId.HasValue || scr.Student.ProgrammeId == programmeId))
                .Select(scr => new
                {
                    scr.Student.Id,
                    scr.Student.StudentId_Number
                })
                .ToListAsync(cancellationToken);

            context.EnrolledStudentIds = enrolledStudents.Select(s => s.Id).ToList();

            context.StudentNumberToIdMap = enrolledStudents
            .GroupBy(s => s.StudentId_Number.ToLower())
            .ToDictionary(
                g => g.Key,
                g => g.First().Id
            );


            // Get assessments through Course navigation property
            var course = await _context.Courses
                .Include(c => c.CourseAssessments)
                    .ThenInclude(ca => ca.Assessment)
                .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

            var assessments = course?.CourseAssessments?
                .Select(ca => new SIS.Models.Results.AssessmentInfo
                {
                    Id = ca.AssessmentId,
                    Name = ca.Assessment.Name,
                    WeightPercentage = ca.Assessment.WeightPercentage
                })
                .ToList() ?? new List<SIS.Models.Results.AssessmentInfo>();

            context.CourseAssessments = assessments;

            // Get existing scores
            var existingScores = await _context.StudentAssessmentScores
                .Where(s =>
                    s.CourseId == courseId &&
                    s.AcademicYearId == academicYearId &&
                    s.YearPeriodId == period &&
                    s.IsActive)
                .Select(s => new
                {
                    s.StudentId,
                    s.AssessmentId,
                    s.Score
                })
                .ToListAsync(cancellationToken);

            foreach (var score in existingScores)
            {
                if (!context.ExistingScores.ContainsKey(score.StudentId))
                    context.ExistingScores[score.StudentId] = new Dictionary<int, decimal>();

                context.ExistingScores[score.StudentId][score.AssessmentId] = score.Score;
            }

            return context;
        }

        private List<ResultImportDto> ParseExcelData(
            IXLWorksheet worksheet,
            List<SIS.Models.Results.AssessmentInfo> assessments)
        {
            var results = new List<ResultImportDto>();

            // Find header row (should be row 5 based on our template)
            int headerRow = 5;
            var range = worksheet.RangeUsed();

            if (range == null || range.RowCount() < headerRow + 1)
            {
                return results;
            }

            // Parse data rows (start from headerRow + 1)
            for (int rowNum = headerRow + 1; rowNum <= range.RowCount(); rowNum++)
            {
                var row = worksheet.Row(rowNum);

                // Skip empty rows
                var studentNumber = row.Cell(1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(studentNumber))
                    continue;

                var result = new ResultImportDto
                {
                    RowNumber = rowNum,
                    StudentNumber = studentNumber,
                    FullName = row.Cell(2).GetString().Trim(),
                    AssessmentScores = new Dictionary<int, decimal>()
                };

                // Parse assessment scores (start from column 3)
                int col = 3;
                foreach (var assessment in assessments)
                {
                    var cell = row.Cell(col);
                    var cellValue = cell.GetString().Trim();

                    // Only add score if cell is not empty
                    if (!string.IsNullOrWhiteSpace(cellValue))
                    {
                        if (decimal.TryParse(cellValue, NumberStyles.Number,
                            CultureInfo.InvariantCulture, out decimal score))
                        {
                            result.AssessmentScores[assessment.Id] = score;
                        }
                        else
                        {
                            result.ValidationErrors.Add(
                                $"Invalid score format for {assessment.Name}: '{cellValue}'");
                        }
                    }

                    col++;
                }

                results.Add(result);
            }

            return results;
        }

        private async Task<ResultValidationResult> ValidateResultRow(
            ResultImportDto row,
            ResultImportValidationContext context,
            CancellationToken cancellationToken)
        {
            var validation = new ResultValidationResult
            {
                RowNumber = row.RowNumber,
                StudentNumber = row.StudentNumber,
                IsValid = true
            };

            // Validate student number
            if (string.IsNullOrWhiteSpace(row.StudentNumber))
            {
                validation.Errors.Add("Student number is required");
                validation.IsValid = false;
            }
            else if (!context.StudentNumberToIdMap.TryGetValue(
                row.StudentNumber.ToLower(), out int studentId))
            {
                validation.Errors.Add($"Student '{row.StudentNumber}' not found or not enrolled in this course");
                validation.IsValid = false;
            }
            else
            {
                row.StudentId = studentId;
                row.IsEnrolled = true;

                // Check if student has existing scores
                if (context.ExistingScores.ContainsKey(studentId))
                {
                    row.HasExistingScores = true;
                    validation.Warnings.Add("Student has existing scores that will be updated");
                }
            }

            // Validate at least one score is provided
            if (!row.AssessmentScores.Any())
            {
                validation.Warnings.Add("No scores provided for any assessment");
            }

            // Validate each score
            foreach (var (assessmentId, score) in row.AssessmentScores)
            {
                // Check if assessment exists
                var assessment = context.CourseAssessments.FirstOrDefault(a => a.Id == assessmentId);
                if (assessment == null)
                {
                    validation.Errors.Add($"Assessment ID {assessmentId} not found for this course");
                    validation.IsValid = false;
                    continue;
                }

                // Validate score range
                if (score < 0 || score > 100)
                {
                    validation.Errors.Add($"{assessment.Name}: Score must be between 0 and 100 (got {score})");
                    validation.IsValid = false;
                }

                // Check for excessive decimal places
                if (Math.Round(score, 2) != score)
                {
                    validation.Warnings.Add($"{assessment.Name}: Score will be rounded to 2 decimal places");
                }
            }

            // Check for duplicate assessments (shouldn't happen with our template, but validate anyway)
            var duplicateAssessments = row.AssessmentScores.GroupBy(s => s.Key)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var dupId in duplicateAssessments)
            {
                var assessment = context.CourseAssessments.FirstOrDefault(a => a.Id == dupId);
                validation.Errors.Add($"Duplicate score for {assessment?.Name ?? $"Assessment {dupId}"}");
                validation.IsValid = false;
            }

            return validation;
        }

        private ImportPreviewSummary CalculatePreviewSummary(
            List<ResultImportDto> validResults,
            List<ResultImportDto> invalidResults,
            ResultImportValidationContext context)
        {
            var summary = new ImportPreviewSummary
            {
                TotalRows = validResults.Count + invalidResults.Count,
                ValidRows = validResults.Count,
                InvalidRows = invalidResults.Count,
                NewScores = validResults.Count(r => !r.HasExistingScores),
                UpdatedScores = validResults.Count(r => r.HasExistingScores),
                MissingStudents = invalidResults.Count(r => !r.IsEnrolled),
                UnenrolledStudents = invalidResults.Count(r => !r.IsEnrolled)
            };

            return summary;
        }

        #endregion

        #region Process Import

        public async Task<ResultImportProcessResult> ProcessImportAsync(
            List<ResultImportDto> validResults,
            int courseId,
            int academicYearId,
            int semester,
            string importedBy,
            string progressKey = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ResultImportProcessResult
            {
                Success = false,
                TotalProcessed = validResults.Count,
                CourseId = courseId,
                AcademicYearId = academicYearId,
                Semester = semester
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "Starting import process for {StudentCount} students in CourseId={CourseId}",
                    validResults.Count, courseId);

                // Get course info for result
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

                result.CourseCode = course?.CourseCode;
                result.CourseName = course?.CourseName;

                // Initialize progress tracking
                if (!string.IsNullOrEmpty(progressKey))
                {
                    UpdateProgress(progressKey, "Initializing import...", 0, 0, validResults.Count);
                }

                // Process in batches
                var batches = validResults
                    .Select((student, index) => new { student, index })
                    .GroupBy(x => x.index / BATCH_SIZE)
                    .Select(g => g.Select(x => x.student).ToList())
                    .ToList();

                int processedCount = 0;
                ResultSubmissionBatch rsb = await CreateAssessmentScoreBatchAsync(courseId, 1, academicYearId, semester, importedBy);

                foreach (var batch in batches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batchNumber = batches.IndexOf(batch) + 1;
                    var progressPercent = (int)((double)processedCount / validResults.Count * 100);

                    if (!string.IsNullOrEmpty(progressKey))
                    {
                        UpdateProgress(progressKey,
                            $"Processing batch {batchNumber} of {batches.Count}...",
                            progressPercent,
                            processedCount,
                            validResults.Count);
                    }
                    
                    await ProcessBatchAsync(
                        batch,
                        courseId,
                        academicYearId,
                        semester,
                        importedBy,
                        result,
                        rsb.Id,
                        cancellationToken);

                    processedCount += batch.Count;

                    // Small delay between batches
                    if (batchNumber < batches.Count)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }

                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;

                // Calculate summary
                result.ImportSummary = new ResultImportSummary
                {
                    TotalRowsInFile = validResults.Count,
                    ValidRowsProcessed = result.SuccessfulImports,
                    InvalidRowsSkipped = 0,
                    StudentsSuccessful = result.SuccessfulImports,
                    StudentsFailed = result.FailedImports,
                    TotalScoresRecorded = result.TotalScoresProcessed,
                    NewScores = result.NewScoresCreated,
                    UpdatedScores = result.ExistingScoresUpdated,
                    IntegrityIssuesFixed = result.IntegrityRestored,
                    ResultsCalculated = result.SuccessfulImports,
                    ProcessingDuration = FormatDuration(stopwatch.Elapsed)
                };

                result.Success = true;
                result.Message = $"Import completed: {result.SuccessfulImports} successful, {result.FailedImports} failed";

                _workflowService.InitiateWorkflowAsync(1, "ResultSubmissionBatch", rsb.Id, importedBy);

                if (!string.IsNullOrEmpty(progressKey))
                {
                    UpdateProgress(progressKey, "Import completed!", 100, validResults.Count, validResults.Count);
                }

                _logger.LogInformation(
                    "Import completed: Successful={SuccessCount}, Failed={FailCount}, Duration={Duration}",
                    result.SuccessfulImports, result.FailedImports, stopwatch.Elapsed);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Import process was cancelled");
                result.Message = "Import process was cancelled";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during import process");
                result.Message = $"Import failed: {ex.Message}";
                return result;
            }
        }

        public async Task<ResultImportProcessResult> ProcessMultiCourseImportAsync(
            List<MultiCourseResultRow> validResults,
            string importType,
            int academicYearId,
            int semester,
            string importedBy,
            string progressKey = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ResultImportProcessResult
            {
                Success = false,
                TotalProcessed = validResults.Count,
                AcademicYearId = academicYearId,
                Semester = semester
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var uniqueCourses = validResults.Select(r => r.CourseId).Distinct().Count();

                _logger.LogInformation(
                    "Starting multi-course import process for {RowCount} rows across {CourseCount} courses",
                    validResults.Count, uniqueCourses);

                if (!string.IsNullOrEmpty(progressKey))
                {
                    UpdateProgress(progressKey, "Initializing import...", 0, 0, validResults.Count);
                }

                // Pre-load all assessment mappings for courses
                var courseIds = validResults.Select(r => r.CourseId).Distinct().ToList();
                var assessmentMappings = await LoadAssessmentMappingsAsync(courseIds, cancellationToken);

                // Group by course
                var courseGroups = validResults.GroupBy(r => r.CourseId).ToList();

                int processedCount = 0;
                int totalRows = validResults.Count;

                foreach (var courseGroup in courseGroups)
                {
                    var courseId = courseGroup.Key;
                    var courseRows = courseGroup.ToList();

                    _logger.LogInformation(
                        "Processing course {CourseId}: {RowCount} rows",
                        courseId, courseRows.Count);

                    // Validate assessment mapping exists
                    if (!assessmentMappings.TryGetValue(courseId, out var assessmentMapping))
                    {
                        _logger.LogError(
                            "No CA/Exam assessment mapping found for CourseId={CourseId}. Skipping {Count} rows.",
                            courseId, courseRows.Count);

                        foreach (var row in courseRows)
                        {
                            result.FailedRows.Add(new FailedResultImportRow
                            {
                                RowNumber = row.RowNumber,
                                StudentNumber = row.StudentIdNumber,
                                ErrorMessage = $"Course {courseId} does not have CA or Exam assessments configured"
                            });
                            result.FailedImports++;
                        }
                        continue;
                    }

                    // Create batch for this course
                    ResultSubmissionBatch rsb = await CreateAssessmentScoreBatchAsync(
                        courseId, 1, academicYearId, semester, importedBy);

                    // Process in batches
                    var batches = courseRows
                        .Select((row, index) => new { row, index })
                        .GroupBy(x => x.index / BATCH_SIZE)
                        .Select(g => g.Select(x => x.row).ToList())
                        .ToList();

                    foreach (var batch in batches)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var progressPercent = (int)((double)processedCount / totalRows * 100);

                        if (!string.IsNullOrEmpty(progressKey))
                        {
                            UpdateProgress(progressKey,
                                $"Processing course {courseId}, batch {batches.IndexOf(batch) + 1} of {batches.Count}...",
                                progressPercent,
                                processedCount,
                                totalRows);
                        }

                        await ProcessMultiCourseBatchAsync(
                            batch,
                            courseId,
                            assessmentMapping,
                            academicYearId,
                            semester,
                            importedBy,
                            result,
                            rsb.Id,
                            cancellationToken);

                        processedCount += batch.Count;

                        if (batches.IndexOf(batch) < batches.Count - 1)
                        {
                            await Task.Delay(100, cancellationToken);
                        }
                    }

                    await _workflowService.InitiateWorkflowAsync(
                        1, "ResultSubmissionBatch", rsb.Id, importedBy);
                }

                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;

                result.ImportSummary = new ResultImportSummary
                {
                    TotalRowsInFile = validResults.Count,
                    ValidRowsProcessed = result.SuccessfulImports,
                    InvalidRowsSkipped = 0,
                    StudentsSuccessful = result.SuccessfulImports,
                    StudentsFailed = result.FailedImports,
                    TotalScoresRecorded = result.TotalScoresProcessed,
                    NewScores = result.NewScoresCreated,
                    UpdatedScores = result.ExistingScoresUpdated,
                    IntegrityIssuesFixed = result.IntegrityRestored,
                    ResultsCalculated = result.SuccessfulImports,
                    ProcessingDuration = FormatDuration(stopwatch.Elapsed)
                };

                result.Success = true;
                result.Message = $"Import completed: {result.SuccessfulImports} successful, {result.FailedImports} failed across {uniqueCourses} courses";

                if (!string.IsNullOrEmpty(progressKey))
                {
                    UpdateProgress(progressKey, "Import completed!", 100, totalRows, totalRows);
                }

                _logger.LogInformation(
                    "Multi-course import completed: Successful={SuccessCount}, Failed={FailCount}, Courses={CourseCount}, Duration={Duration}",
                    result.SuccessfulImports, result.FailedImports, uniqueCourses, stopwatch.Elapsed);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Import process was cancelled");
                result.Message = "Import process was cancelled";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during multi-course import process");
                result.Message = $"Import failed: {ex.Message}";
                return result;
            }
        }

        public async Task<ResultImportProcessResult> ProcessSupDefImportAsync(
            List<SupDefResultRow> validResults,
            string importType,
            int academicYearId,
            int semester,
            string importedBy,
            string progressKey = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ResultImportProcessResult
            {
                Success = false,
                TotalProcessed = validResults.Count,
                AcademicYearId = academicYearId,
                Semester = semester
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var uniqueCourses = validResults.Select(r => r.CourseId).Distinct().Count();

                _logger.LogInformation(
                    "Starting multi-course import process for {RowCount} rows across {CourseCount} courses",
                    validResults.Count, uniqueCourses);

                if (!string.IsNullOrEmpty(progressKey))
                {
                    UpdateProgress(progressKey, "Initializing import...", 0, 0, validResults.Count);
                }

                // Pre-load all assessment mappings for courses
                var courseIds = validResults.Select(r => r.CourseId).Distinct().ToList();
                var assessmentMappings = await LoadAssessmentMappingsAsync(courseIds, cancellationToken);

                // Group by course
                var courseGroups = validResults.GroupBy(r => r.CourseId).ToList();

                int processedCount = 0;
                int totalRows = validResults.Count;

                foreach (var courseGroup in courseGroups)
                {
                    var courseId = courseGroup.Key;
                    var courseRows = courseGroup.ToList();

                    _logger.LogInformation(
                        "Processing course {CourseId}: {RowCount} rows",
                        courseId, courseRows.Count);

                    // Validate assessment mapping exists
                    if (!assessmentMappings.TryGetValue(courseId, out var assessmentMapping))
                    {
                        _logger.LogError(
                            "No Exam assessment mapping found for CourseId={CourseId}. Skipping {Count} rows.",
                            courseId, courseRows.Count);

                        foreach (var row in courseRows)
                        {
                            result.FailedRows.Add(new FailedResultImportRow
                            {
                                RowNumber = row.RowNumber,
                                StudentNumber = row.StudentIdNumber,
                                ErrorMessage = $"Course {courseId} does not have Exam assessments configured"
                            });
                            result.FailedImports++;
                        }
                        continue;
                    }

                    // Create batch for this course
                    ResultSubmissionBatch rsb = await CreateAssessmentScoreBatchAsync(
                        courseId, 1, academicYearId, semester, importedBy);

                    // Process in batches
                    var batches = courseRows
                        .Select((row, index) => new { row, index })
                        .GroupBy(x => x.index / BATCH_SIZE)
                        .Select(g => g.Select(x => x.row).ToList())
                        .ToList();

                    foreach (var batch in batches)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var progressPercent = (int)((double)processedCount / totalRows * 100);

                        if (!string.IsNullOrEmpty(progressKey))
                        {
                            UpdateProgress(progressKey,
                                $"Processing course {courseId}, batch {batches.IndexOf(batch) + 1} of {batches.Count}...",
                                progressPercent,
                                processedCount,
                                totalRows);
                        }

                        await ProcessSupDefBatchAsync(
                            batch,
                            courseId,
                            assessmentMapping,
                            academicYearId,
                            semester,
                            importedBy,
                            result,
                            rsb.Id,
                            cancellationToken);

                        processedCount += batch.Count;

                        if (batches.IndexOf(batch) < batches.Count - 1)
                        {
                            await Task.Delay(100, cancellationToken);
                        }
                    }

                    await _workflowService.InitiateWorkflowAsync(
                        1, "ResultSubmissionBatch", rsb.Id, importedBy);
                }

                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;

                result.ImportSummary = new ResultImportSummary
                {
                    TotalRowsInFile = validResults.Count,
                    ValidRowsProcessed = result.SuccessfulImports,
                    InvalidRowsSkipped = 0,
                    StudentsSuccessful = result.SuccessfulImports,
                    StudentsFailed = result.FailedImports,
                    TotalScoresRecorded = result.TotalScoresProcessed,
                    NewScores = result.NewScoresCreated,
                    UpdatedScores = result.ExistingScoresUpdated,
                    IntegrityIssuesFixed = result.IntegrityRestored,
                    ResultsCalculated = result.SuccessfulImports,
                    ProcessingDuration = FormatDuration(stopwatch.Elapsed)
                };

                result.Success = true;
                result.Message = $"Import completed: {result.SuccessfulImports} successful, {result.FailedImports} failed across {uniqueCourses} courses";

                if (!string.IsNullOrEmpty(progressKey))
                {
                    UpdateProgress(progressKey, "Import completed!", 100, totalRows, totalRows);
                }

                _logger.LogInformation(
                    "Multi-course import completed: Successful={SuccessCount}, Failed={FailCount}, Courses={CourseCount}, Duration={Duration}",
                    result.SuccessfulImports, result.FailedImports, uniqueCourses, stopwatch.Elapsed);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Import process was cancelled");
                result.Message = "Import process was cancelled";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during multi-course import process");
                result.Message = $"Import failed: {ex.Message}";
                return result;
            }
        }

        private async Task<Dictionary<int, CourseAssessmentMapping>> LoadAssessmentMappingsAsync(
            List<int> courseIds,
            CancellationToken cancellationToken)
        {
            var mappings = new Dictionary<int, CourseAssessmentMapping>();

            var courseAssessments = await _context.CourseAssessment
                .Where(ca => courseIds.Contains(ca.CourseId))
                .Include(ca => ca.Assessment)
                .ToListAsync(cancellationToken);

            foreach (var courseId in courseIds)
            {
                var assessments = courseAssessments.Where(ca => ca.CourseId == courseId).ToList();

                var caAssessment = assessments.FirstOrDefault(a =>
                    a.Assessment.Name.Contains("CA-2025", StringComparison.OrdinalIgnoreCase));

                var examAssessment = assessments.FirstOrDefault(a =>
                    a.Assessment.Name.Contains("Exam", StringComparison.OrdinalIgnoreCase));

                if (caAssessment != null || examAssessment != null)
                {
                    mappings[courseId] = new CourseAssessmentMapping
                    {
                        CAAssessmentId = caAssessment?.AssessmentId,
                        ExamAssessmentId = examAssessment?.AssessmentId,
                        CAAssessmentName = caAssessment?.Assessment.Name,
                        ExamAssessmentName = examAssessment?.Assessment.Name
                    };

                    _logger.LogInformation(
                        "Course {CourseId}: CA={CAName}, Exam={ExamName}",
                        courseId,
                        caAssessment?.Assessment.Name ?? "None",
                        examAssessment?.Assessment.Name ?? "None");
                }
            }

            return mappings;
        }

        private async Task ProcessMultiCourseBatchAsync(
            List<MultiCourseResultRow> batch,
            int courseId,
            CourseAssessmentMapping assessmentMapping,
            int academicYearId,
            int semester,
            string importedBy,
            ResultImportProcessResult result,
            int rsbId,
            CancellationToken cancellationToken)
        {
            foreach (var row in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    int scoresRecorded = 0;
                    int newScores = 0;
                    int updatedScores = 0;
                    var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == row.StudentId);

                    // Process CA mark
                    if (row.CAMark.HasValue && assessmentMapping.CAAssessmentId.HasValue)
                    {
                        var scoreResult = await ProcessScoreAsync(
                            row.StudentId,
                            courseId,
                            assessmentMapping.CAAssessmentId.Value,
                            academicYearId,
                            semester,
                            row.CAMark.Value,
                            importedBy,
                            rsbId,
                            (int)student.StudentCurrentYear,
                            cancellationToken);

                        if (scoreResult.Success)
                        {
                            scoresRecorded++;
                            if (scoreResult.IsNew) newScores++;
                            else updatedScores++;
                        }
                    }

                    // Process Exam mark
                    if (row.ExamMark.HasValue && assessmentMapping.ExamAssessmentId.HasValue)
                    {
                        var scoreResult = await ProcessScoreAsync(
                            row.StudentId,
                            courseId,
                            assessmentMapping.ExamAssessmentId.Value,
                            academicYearId,
                            semester,
                            row.ExamMark.Value,
                            importedBy,
                            rsbId,
                            (int)student.StudentCurrentYear,
                            cancellationToken);

                        if (scoreResult.Success)
                        {
                            scoresRecorded++;
                            if (scoreResult.IsNew) newScores++;
                            else updatedScores++;
                        }
                    }

                    // Calculate course result
                    StudentCourseResult calculatedResult = null;
                    try
                    {
                        calculatedResult = await _resultCalculationService.CalculateResultAsync(
                            row.StudentId,
                            courseId,
                            academicYearId,
                            semester,
                            importedBy);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Could not calculate result for StudentId={StudentId}, CourseId={CourseId}",
                            row.StudentId, courseId);
                    }

                    result.ImportedResults.Add(new ImportedResultInfo
                    {
                        RowNumber = row.RowNumber,
                        StudentNumber = row.StudentIdNumber,
                        StudentName = "",
                        ScoresRecorded = scoresRecorded,
                        CalculatedTotal = calculatedResult?.NormalizedTotal ?? 0,
                        Grade = calculatedResult?.GradeLetter ?? "N/A",
                        IsPassed = calculatedResult?.IsPassed ?? false,
                        HadIntegrityIssue = false
                    });

                    result.SuccessfulImports++;
                    result.TotalScoresProcessed += scoresRecorded;
                    result.NewScoresCreated += newScores;
                    result.ExistingScoresUpdated += updatedScores;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing row {RowNumber}, StudentNumber={StudentNumber}, CourseCode={CourseCode}",
                        row.RowNumber, row.StudentIdNumber, row.CourseCode);

                    result.FailedRows.Add(new FailedResultImportRow
                    {
                        RowNumber = row.RowNumber,
                        StudentNumber = row.StudentIdNumber,
                        ErrorMessage = ex.Message,
                        DetailedErrors = new List<string> { ex.ToString() }
                    });

                    result.FailedImports++;
                }
            }
        }

        private async Task ProcessSupDefBatchAsync(
            List<SupDefResultRow> batch,
            int courseId,
            CourseAssessmentMapping assessmentMapping,
            int academicYearId,
            int semester,
            string importedBy,
            ResultImportProcessResult result,
            int rsbId,
            CancellationToken cancellationToken)
        {
            foreach (var row in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    int scoresRecorded = 0;
                    int newScores = 0;
                    int updatedScores = 0;
                    var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == row.StudentId);

                    // Process Exam mark
                    if (assessmentMapping.ExamAssessmentId.HasValue)
                    {
                        var scoreResult = await ProcessSupDefScoreAsync(
                            row.StudentId,
                            courseId,
                            assessmentMapping.ExamAssessmentId.Value,
                            academicYearId,
                            semester,
                            row.ExamMark,
                            importedBy,
                            rsbId,
                            (int)student.StudentCurrentYear,
                            cancellationToken);

                        if (scoreResult.Success)
                        {
                            scoresRecorded++;
                            if (scoreResult.IsNew) newScores++;
                            else updatedScores++;
                        }
                    }

                    // Calculate course result
                    StudentCourseResult calculatedResult = null;
                    try
                    {
                        calculatedResult = await _resultCalculationService.CalculateResultAsync(
                            row.StudentId,
                            courseId,
                            academicYearId,
                            semester,
                            importedBy);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Could not calculate result for StudentId={StudentId}, CourseId={CourseId}",
                            row.StudentId, courseId);
                    }

                    result.ImportedResults.Add(new ImportedResultInfo
                    {
                        RowNumber = row.RowNumber,
                        StudentNumber = row.StudentIdNumber,
                        StudentName = "",
                        ScoresRecorded = scoresRecorded,
                        CalculatedTotal = calculatedResult?.NormalizedTotal ?? 0,
                        Grade = calculatedResult?.GradeLetter ?? "N/A",
                        IsPassed = calculatedResult?.IsPassed ?? false,
                        HadIntegrityIssue = false
                    });

                    result.SuccessfulImports++;
                    result.TotalScoresProcessed += scoresRecorded;
                    result.NewScoresCreated += newScores;
                    result.ExistingScoresUpdated += updatedScores;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing row {RowNumber}, StudentNumber={StudentNumber}, CourseCode={CourseCode}",
                        row.RowNumber, row.StudentIdNumber, row.CourseCode);

                    result.FailedRows.Add(new FailedResultImportRow
                    {
                        RowNumber = row.RowNumber,
                        StudentNumber = row.StudentIdNumber,
                        ErrorMessage = ex.Message,
                        DetailedErrors = new List<string> { ex.ToString() }
                    });

                    result.FailedImports++;
                }
            }
        }

        private async Task<ScoreProcessingResult> ProcessScoreAsync(
            int studentId,
            int courseId,
            int assessmentId,
            int academicYearId,
            int period,
            decimal score,
            string importedBy,
            int rsbId,
            int yearOfStudy,
            CancellationToken cancellationToken)
        {
            bool exists = await _assessmentScoreService.ScoreExistsAsync(
                studentId, courseId, assessmentId, academicYearId, period);

            if (exists)
            {
                var existingScore = await _context.StudentAssessmentScores
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == studentId &&
                        s.CourseId == courseId &&
                        s.AssessmentId == assessmentId &&
                        s.AcademicYearId == academicYearId &&
                        s.YearPeriodId == period &&
                        s.IsActive,
                        cancellationToken);

                if (existingScore != null && existingScore.Score != score)
                {
                    await _assessmentScoreService.UpdateScoreAsync(
                        existingScore.Id,
                        score,
                        importedBy,
                        "Multi-course bulk import update",
                        rsbId);

                    return new ScoreProcessingResult { Success = true, IsNew = false };
                }

                return new ScoreProcessingResult { Success = false, IsNew = false };
            }
            else
            {
                await _assessmentScoreService.RecordScoreAsync(
                    studentId,
                    courseId,
                    academicYearId,
                    assessmentId,
                    period,
                    score,
                    importedBy,
                    rsbId,
                    yearOfStudy,
                    1,
                    "Multi-course bulk import");

                return new ScoreProcessingResult { Success = true, IsNew = true };
            }
        }

        private async Task<ScoreProcessingResult> ProcessSupDefScoreAsync(
            int studentId,
            int courseId,
            int assessmentId,
            int academicYearId,
            int period,
            decimal score,
            string importedBy,
            int rsbId,
            int yearOfStudy,
            CancellationToken cancellationToken)
        {
            bool exists = await _assessmentScoreService.ScoreExistsAsync(
                studentId, courseId, assessmentId, academicYearId, period);

            if(!exists)
            {
                await _assessmentScoreService.RecordScoreAsync(
                    studentId,
                    courseId,
                    academicYearId,
                    assessmentId,
                    period,
                    score,
                    importedBy,
                    rsbId,
                    yearOfStudy,
                    1,
                    "Def bulk import");

                return new ScoreProcessingResult { Success = true, IsNew = true };
            }

            if (exists)
            {
                var existingScore = await _context.StudentAssessmentScores
                    .Where(s =>
                        s.StudentId == studentId &&
                        s.CourseId == courseId &&
                        s.AssessmentId == assessmentId &&
                        s.AcademicYearId == academicYearId &&
                        s.YearPeriodId == period &&
                        s.IsActive)
                    .OrderByDescending(s => s.Attempt)
                    .FirstOrDefaultAsync(cancellationToken);


                if (existingScore != null && existingScore.Score != score && existingScore.Attempt == 2 && existingScore.Remarks.ToLower().Contains("sup"))
                {
                    await _assessmentScoreService.UpdateScoreAsync(
                        existingScore.Id,
                        score,
                        importedBy,
                        "Sup bulk import update",
                        rsbId);

                    return new ScoreProcessingResult { Success = true, IsNew = false };
                }
                else if (existingScore != null && existingScore.Score != score && existingScore.Attempt == 1 && existingScore.Remarks.ToLower().Contains("def"))
                {
                    await _assessmentScoreService.UpdateScoreAsync(
                        existingScore.Id,
                        score,
                        importedBy,
                        "Def bulk import update",
                        rsbId);

                    return new ScoreProcessingResult { Success = true, IsNew = false };
                }
                else
                {
                    await _assessmentScoreService.RecordScoreAsync(
                        studentId,
                        courseId,
                        academicYearId,
                        assessmentId,
                        period,
                        score,
                        importedBy,
                        rsbId,
                        yearOfStudy,
                        2,
                        "Sup bulk import");
                    return new ScoreProcessingResult { Success = true, IsNew = true };
                }
            }
            return new ScoreProcessingResult { Success = false, IsNew = false };
        }

        // Helper classes
        private class CourseAssessmentMapping
        {
            public int? CAAssessmentId { get; set; }
            public int? ExamAssessmentId { get; set; }
            public string CAAssessmentName { get; set; }
            public string ExamAssessmentName { get; set; }
        }

        private class ScoreProcessingResult
        {
            public bool Success { get; set; }
            public bool IsNew { get; set; }
        }

        private async Task ProcessBatchAsync(
            List<ResultImportDto> batch,
            int courseId,
            int academicYearId,
            int period,
            string importedBy,
            ResultImportProcessResult result,
            int rsbId,
            CancellationToken cancellationToken)
        {
            foreach (var studentResult in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    int scoresRecorded = 0;
                    int newScores = 0;
                    int updatedScores = 0;
                    int integrityRestored = 0;
                    var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentResult.StudentId);

                    // Process each assessment score
                    foreach (var (assessmentId, score) in studentResult.AssessmentScores)
                    {
                        try
                        {
                            // Check if score exists
                            bool exists = await _assessmentScoreService.ScoreExistsAsync(
                                studentResult.StudentId,
                                courseId,
                                assessmentId,
                                academicYearId,
                                period);

                            if (exists)
                            {
                                // Get existing score to check integrity
                                var existingScore = await _context.StudentAssessmentScores
                                    .FirstOrDefaultAsync(s =>
                                        s.StudentId == studentResult.StudentId &&
                                        s.CourseId == courseId &&
                                        s.AssessmentId == assessmentId &&
                                        s.AcademicYearId == academicYearId &&
                                        s.YearPeriodId == period &&
                                        s.IsActive,
                                        cancellationToken);

                                if (existingScore != null)
                                {
                                    // Check if score actually changed
                                    if (existingScore.Score != score)
                                    {
                                        // Update will restore integrity if tampered
                                        await _assessmentScoreService.UpdateScoreAsync(
                                            existingScore.Id,
                                            score,
                                            importedBy,
                                            "Bulk import update",
                                            rsbId);

                                        updatedScores++;
                                        scoresRecorded++;

                                        // Check if integrity was restored
                                        // (UpdateScoreAsync logs this, but we track it here too)
                                        _logger.LogDebug(
                                            "Updated score for StudentId={StudentId}, AssessmentId={AssessmentId}",
                                            studentResult.StudentId, assessmentId);
                                    }
                                    // else: score unchanged, skip
                                }
                            }
                            else
                            {
                                // Create new score
                                await _assessmentScoreService.RecordScoreAsync(
                                    studentResult.StudentId,
                                    courseId,
                                    academicYearId,
                                    assessmentId,
                                    period,
                                    score,
                                    importedBy,
                                    rsbId,
                                    (int)student.StudentCurrentYear,
                                    1,
                                    "Bulk import");

                                newScores++;
                                scoresRecorded++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "Error processing score for StudentId={StudentId}, AssessmentId={AssessmentId}",
                                studentResult.StudentId, assessmentId);
                            // Continue with other assessments
                        }
                    }

                    // Calculate course result using existing service
                    StudentCourseResult calculatedResult = null;
                    try
                    {
                        calculatedResult = await _resultCalculationService.CalculateResultAsync(
                            studentResult.StudentId,
                            courseId,
                            academicYearId,
                            period,
                            importedBy);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Could not calculate result for StudentId={StudentId}",
                            studentResult.StudentId);
                    }

                    // Record success
                    result.ImportedResults.Add(new ImportedResultInfo
                    {
                        RowNumber = studentResult.RowNumber,
                        StudentNumber = studentResult.StudentNumber,
                        StudentName = studentResult.FullName,
                        ScoresRecorded = scoresRecorded,
                        CalculatedTotal = calculatedResult?.NormalizedTotal ?? 0,
                        Grade = calculatedResult?.GradeLetter ?? "N/A",
                        IsPassed = calculatedResult?.IsPassed ?? false,
                        HadIntegrityIssue = integrityRestored > 0
                    });

                    result.SuccessfulImports++;
                    result.TotalScoresProcessed += scoresRecorded;
                    result.NewScoresCreated += newScores;
                    result.ExistingScoresUpdated += updatedScores;
                    result.IntegrityRestored += integrityRestored;

                    _logger.LogDebug(
                        "Successfully processed StudentId={StudentId}, Scores={ScoreCount}",
                        studentResult.StudentId, scoresRecorded);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing student row {RowNumber}, StudentNumber={StudentNumber}",
                        studentResult.RowNumber, studentResult.StudentNumber);

                    result.FailedRows.Add(new FailedResultImportRow
                    {
                        RowNumber = studentResult.RowNumber,
                        StudentNumber = studentResult.StudentNumber,
                        StudentName = studentResult.FullName,
                        OriginalData = studentResult,
                        ErrorMessage = ex.Message,
                        DetailedErrors = new List<string> { ex.ToString() }
                    });

                    result.FailedImports++;
                }
            }
        }

        // ==================== ASSESSMENT SCORE OPERATIONS ====================

        public async Task<ResultSubmissionBatch> CreateAssessmentScoreBatchAsync(
            int courseId,
            int assessmentId,
            int academicYearId,
            int period,
            string uploadedById)
        {
            var batch = new ResultSubmissionBatch
            {
                CourseId = courseId,
                AssessmentId = assessmentId,
                SubmissionType = "AssessmentScores",
                AcademicYearId = academicYearId,
                YearPeriodId = period,
                UploadedById = uploadedById,
                UploadedAt = DateTime.Now,
                ApprovalStatus = WorkflowStatus.Draft,
                BatchHash = Guid.NewGuid().ToString(), // Temporary, will be updated
                CreatedBy = uploadedById,
                CreatedAt = DateTime.Now
            };

            _context.ResultSubmissionBatches.Add(batch);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created assessment score batch {batch.Id}");

            return batch;
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes >= 1)
                return $"{duration.TotalMinutes:F1} minutes";
            else
                return $"{duration.TotalSeconds:F1} seconds";
        }

        #endregion

        #region Error Report

        public async Task<byte[]> GenerateErrorReportAsync(
            ResultImportPreviewResult previewResult,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Import Errors");

                // Title
                worksheet.Cell(1, 1).Value = "Result Import Error Report";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 14;
                worksheet.Range(1, 1, 1, 5).Merge();

                // Course info
                worksheet.Cell(2, 1).Value =
                    $"Course: {previewResult.CourseCode} - {previewResult.CourseName} | " +
                    $"Academic Year: {previewResult.AcademicYearValue} | Semester: {previewResult.Semester}";
                worksheet.Range(2, 1, 2, 5).Merge();

                // Summary
                worksheet.Cell(3, 1).Value =
                    $"Total Rows: {previewResult.TotalRows} | Valid: {previewResult.ValidResults.Count} | " +
                    $"Invalid: {previewResult.InvalidResults.Count}";
                worksheet.Range(3, 1, 3, 5).Merge();

                // Headers
                int headerRow = 5;
                worksheet.Cell(headerRow, 1).Value = "Row #";
                worksheet.Cell(headerRow, 2).Value = "Student Number";
                worksheet.Cell(headerRow, 3).Value = "Full Name";
                worksheet.Cell(headerRow, 4).Value = "Errors";
                worksheet.Cell(headerRow, 5).Value = "Warnings";

                // Style headers
                var headerRange = worksheet.Range(headerRow, 1, headerRow, 5);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.Red;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

                // Data rows
                int row = headerRow + 1;
                foreach (var invalidResult in previewResult.InvalidResults)
                {
                    var validation = previewResult.ValidationResults
                        .FirstOrDefault(v => v.RowNumber == invalidResult.RowNumber);

                    worksheet.Cell(row, 1).Value = invalidResult.RowNumber;
                    worksheet.Cell(row, 2).Value = invalidResult.StudentNumber;
                    worksheet.Cell(row, 3).Value = invalidResult.FullName;
                    worksheet.Cell(row, 4).Value = validation != null
                        ? string.Join("; ", validation.Errors)
                        : "Unknown error";
                    worksheet.Cell(row, 5).Value = validation != null && validation.Warnings.Any()
                        ? string.Join("; ", validation.Warnings)
                        : "";

                    // Highlight error row
                    worksheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightPink;

                    row++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                // Convert to byte array
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating error report");
                throw;
            }
        }

        #endregion

        #region Progress Tracking

        public async Task<ResultImportProgress> GetImportProgressAsync(
            string progressKey,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return _progressTracker.TryGetValue(progressKey, out var progress)
                ? progress
                : null;
        }

        public async Task CleanupProgressAsync(string progressKey)
        {
            try
            {
                await Task.Delay(100);
                _progressTracker.TryRemove(progressKey, out _);
                _logger.LogDebug("Cleaned up progress tracking for key: {ProgressKey}", progressKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up progress for key: {ProgressKey}", progressKey);
            }
        }

        private void UpdateProgress(
            string progressKey,
            string message,
            int percentComplete,
            int currentStudent,
            int totalStudents)
        {
            if (string.IsNullOrEmpty(progressKey))
                return;

            try
            {
                _progressTracker[progressKey] = new ResultImportProgress
                {
                    CurrentStep = message,
                    PercentComplete = percentComplete,
                    Message = message,
                    LastUpdated = DateTime.Now,
                    CurrentStudent = currentStudent,
                    TotalStudents = totalStudents,
                    IsComplete = percentComplete >= 100
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating progress for key: {ProgressKey}", progressKey);
            }
        }

        #endregion

        #region Course Validation

        public async Task<CourseImportValidationResult> ValidateCourseContextAsync(
            int courseId,
            int academicYearId,
            int period,
            CancellationToken cancellationToken = default)
        {
            var result = new CourseImportValidationResult
            {
                IsValid = true
            };

            try
            {
                // Check if course exists
                var course = await _context.Courses
                    .Include(c => c.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

                if (course == null)
                {
                    result.IsValid = false;
                    result.CourseExists = false;
                    result.Errors.Add("Course not found");
                    return result;
                }

                result.CourseExists = true;

                // Check if course has assessments
                if (!course.CourseAssessments.Any())
                {
                    result.IsValid = false;
                    result.HasAssessments = false;
                    result.Errors.Add("No assessments configured for this course");
                    return result;
                }

                result.HasAssessments = true;
                result.AssessmentCount = course.CourseAssessments.Count;

                // Validate total weight
                var totalWeight = course.CourseAssessments.Sum(ca => ca.Assessment.WeightPercentage);
                result.TotalAssessmentWeight = totalWeight;

                if (totalWeight != 100)
                {
                    result.Warnings.Add(
                        $"Total assessment weight is {totalWeight}% (expected 100%). " +
                        "Results will be normalized.");
                }

                // Check if academic year exists
                var academicYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(ay => ay.YearId == academicYearId, cancellationToken);

                if (academicYear == null)
                {
                    result.IsValid = false;
                    result.Errors.Add("Academic year not found");
                    return result;
                }

                // Check for enrolled students
                var enrolledCount = await _context.StudentCourseRegistrations
                    .CountAsync(scr =>
                        scr.CourseId == courseId &&
                        scr.AcademicYearId == academicYearId &&
                        scr.YearPeriodId == period,
                        cancellationToken);

                if (enrolledCount == 0)
                {
                    result.IsValid = false;
                    result.HasEnrolledStudents = false;
                    result.Errors.Add("No students enrolled in this course for the specified academic year and semester");
                    return result;
                }

                result.HasEnrolledStudents = true;
                result.EnrolledStudentCount = enrolledCount;

                // Check if results are already published
                var publishedCount = await _context.StudentCourseResults
                    .CountAsync(r =>
                        r.CourseId == courseId &&
                        r.AcademicYearId == academicYearId &&
                        r.Semester == period &&
                        r.Status == Enums.Status.Published,
                        cancellationToken);

                if (publishedCount > 0)
                {
                    result.ResultsAlreadyPublished = true;
                    result.Warnings.Add(
                        $"{publishedCount} result(s) already published. " +
                        "Importing will update scores but not automatically republish.");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating course context for CourseId={CourseId}", courseId);
                result.IsValid = false;
                result.Errors.Add($"Error during validation: {ex.Message}");
                return result;
            }
        }

        public async Task<CourseImportStatistics> GetImportStatisticsAsync(
      int courseId,
      int academicYearId,
      int period,
      CancellationToken cancellationToken = default)
        {
            try
            {
                var course = await _context.Courses
                    .Include(c => c.CourseAssessments)
                    .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

                var stats = new CourseImportStatistics
                {
                    CourseId = courseId,
                    CourseName = course?.CourseName ?? "Unknown",
                    AcademicYearId = academicYearId,
                    Semester = period
                };

                // Get total enrolled
                stats.TotalEnrolled = await _context.StudentCourseRegistrations
                    .CountAsync(scr =>
                        scr.CourseId == courseId &&
                        scr.AcademicYearId == academicYearId &&
                        scr.YearPeriodId == period,
                        cancellationToken);

                // Get assessment count for this course
                var assessmentCount = course?.CourseAssessments?.Count ?? 0;

                // Get students with scores
                var studentsWithScores = await _context.StudentAssessmentScores
                    .Where(s =>
                        s.CourseId == courseId &&
                        s.AcademicYearId == academicYearId &&
                        s.YearPeriodId == period &&
                        s.IsActive)
                    .GroupBy(s => s.StudentId)
                    .Select(g => new
                    {
                        StudentId = g.Key,
                        ScoreCount = g.Count()
                    })
                    .ToListAsync(cancellationToken);

                stats.StudentsWithScores = studentsWithScores.Count;
                stats.StudentsWithCompleteScores = studentsWithScores
                    .Count(s => s.ScoreCount >= assessmentCount);
                stats.StudentsWithPartialScores = studentsWithScores
                    .Count(s => s.ScoreCount < assessmentCount && s.ScoreCount > 0);
                stats.StudentsWithNoScores = stats.TotalEnrolled - stats.StudentsWithScores;

                // Get last import info (from audit logs)
                var lastImport = await _context.ResultAuditLogs
                    .Where(a =>
                        a.CourseId == courseId &&
                        a.AcademicYearId == academicYearId &&
                        a.EntityType == "AssessmentScore" &&
                        a.ActionType == "Created" &&
                        (a.Reason != null && a.Reason.Contains("Bulk import")))
                    .OrderByDescending(a => a.ChangedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (lastImport != null)
                {
                    stats.LastImportDate = lastImport.ChangedAt;
                    stats.LastImportedBy = lastImport.ChangedBy;
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting import statistics for CourseId={CourseId}", courseId);
                throw;
            }
        }

        #endregion

        #region Helper Classes

        private class FileValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }

        #endregion
    }
}