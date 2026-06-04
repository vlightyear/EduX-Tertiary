using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Results;
using SIS.Models.StudentResults;
using System.Text.Json;

namespace SIS.Services
{
    public interface ICourseResultCalculationService
    {
        Task<StudentCourseResult> CalculateResultAsync(int studentId, int courseId, int academicYearId,
            int semester, string userId);
        Task<List<StudentCourseResult>> CalculateResultsForCourseAsync(int courseId, int academicYearId,
            int semester, string userId);
        Task<StudentCourseResult> RecalculateResultAsync(int resultId, string userId, string reason);
        Task<bool> PublishResultAsync(int resultId, string userId);
        Task<bool> PublishResultsForCourseAsync(int courseId, int academicYearId, int semester, string userId);
        Task<StudentCourseResult?> GetResultAsync(int studentId, int courseId, int academicYearId, int semester);
        Task<List<StudentCourseResult>> GetResultsForStudentAsync(int studentId, int academicYearId);
        Task<decimal> CalculateGPAAsync(int studentId, int academicYearId);
    }

    public class CourseResultCalculationService : ICourseResultCalculationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IResultIntegrityService _integrityService;
        private readonly IResultAuditService _auditService;
        private readonly ILogger<CourseResultCalculationService> _logger;

        public CourseResultCalculationService(
            ApplicationDbContext context,
            IResultIntegrityService integrityService,
            IResultAuditService auditService,
            ILogger<CourseResultCalculationService> logger)
        {
            _context = context;
            _integrityService = integrityService;
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>
        /// Calculate result for a single student in a specific course
        /// </summary>
        public async Task<StudentCourseResult> CalculateResultAsync(
            int studentId,
            int courseId,
            int academicYearId,
            int semester,
            string userId)
        {
            try
            {
                // Get all assessment scores for this student/course
                var assessmentScores = await _context.StudentAssessmentScores
                    .Include(s => s.Assessment)
                    .Where(s =>
                        s.StudentId == studentId &&
                        s.CourseId == courseId &&
                        s.AcademicYearId == academicYearId &&
                        s.YearPeriodId == semester &&
                        s.IsActive)
                    .ToListAsync();

                if (!assessmentScores.Any())
                {
                    throw new InvalidOperationException(
                        "No assessment scores found for this student and course.");
                }

                // Verify integrity of all scores
                // ⭐ MODIFIED: Check integrity but allow calculation to proceed with warning
                bool allScoresValid = true;
                var tamperedAssessments = new List<int>();

                foreach (var score in assessmentScores)
                {
                    if (!_integrityService.VerifyScoreHash(score))
                    {
                        allScoresValid = false;
                        tamperedAssessments.Add(score.AssessmentId);

                        _logger.LogWarning(
                            "Score integrity check failed for assessment {AssessmentId}. StudentId={StudentId}, CourseId={CourseId}. Proceeding with calculation using current values.",
                            score.AssessmentId, studentId, courseId);
                    }
                }

                // If any scores were tampered, log it in the audit
                if (!allScoresValid)
                {
                    await _auditService.LogActionAsync(
                        entityType: "CourseResult",
                        entityId: 0, // Will be updated later
                        studentId: studentId,
                        courseId: courseId,
                        academicYearId: academicYearId,
                        actionType: "CalculatedWithTamperedScores",
                        oldValue: null,
                        newValue: JsonSerializer.Serialize(new
                        {
                            tamperedAssessments = tamperedAssessments,
                            message = "Result calculated despite tampered scores"
                        }),
                        userId: userId,
                        reason: $"Calculated with {tamperedAssessments.Count} tampered assessment(s): {string.Join(", ", tamperedAssessments)}"
                    );
                }

                // Get course information
                var course = await _context.Courses.FindAsync(courseId);
                if (course == null)
                {
                    throw new ArgumentException("Course not found", nameof(courseId));
                }

                // Calculate weighted total
                decimal weightedTotal = 0;
                decimal totalWeight = 0;

                foreach (var score in assessmentScores)
                {
                    weightedTotal += (score.Score /** score.WeightPercentage / 100*/);
                    totalWeight += score.WeightPercentage;
                }

                // Normalize to 100 if total weight is not 100
                decimal normalizedTotal = totalWeight > 0 ? (weightedTotal / totalWeight) * 100 : 0;
                normalizedTotal = Math.Min(normalizedTotal, 100); // Cap at 100

                // Get grade configurations
                var grades = await _context.GradeConfigurations
                    .Where(g => g.IsActive)
                    .OrderByDescending(g => g.MinScore)
                    .ToListAsync();

                // Determine grade
                var gradeConfig = grades.FirstOrDefault(g =>
                    normalizedTotal >= g.MinScore && normalizedTotal <= g.MaxScore);

                if (gradeConfig == null)
                {
                    throw new InvalidOperationException(
                        $"No grade configuration found for score {normalizedTotal}");
                }

                // Determine if passed
                bool isPassed = normalizedTotal >= (decimal)course.PassMark;

                // Calculate credits
                int credits = 3; // Default, could be fetched from course if variable
                int creditsEarned = isPassed ? credits : 0;

                // Check if result already exists
                var existingResult = await _context.StudentCourseResults
                    .FirstOrDefaultAsync(r =>
                        r.StudentId == studentId &&
                        r.CourseId == courseId &&
                        r.AcademicYearId == academicYearId &&
                        r.YearPeriodId == semester);

                StudentCourseResult result;

                if (existingResult != null)
                {
                    // Update existing result
                    var oldValue = JsonSerializer.Serialize(new
                    {
                        normalizedTotal = existingResult.NormalizedTotal,
                        gradeLetter = existingResult.GradeLetter,
                        gradePoints = existingResult.GradePoints,
                        isPassed = existingResult.IsPassed
                    });

                    existingResult.WeightedTotal = weightedTotal;
                    existingResult.NormalizedTotal = normalizedTotal;
                    existingResult.GradeLetter = gradeConfig.GradeLetter;
                    existingResult.GradePoints = gradeConfig.GPAValue;
                    existingResult.IsPassed = isPassed;
                    existingResult.Credits = credits;
                    existingResult.CreditsEarned = creditsEarned;
                    existingResult.PassMark = (decimal)course.PassMark;
                    existingResult.TotalWeightPercentage = totalWeight;
                    existingResult.AssessmentCount = assessmentScores.Count;
                    existingResult.CalculatedAt = DateTime.Now;
                    existingResult.CalculatedBy = userId;

                    // Regenerate hash
                    existingResult.ResultHash = _integrityService.GenerateResultHash(existingResult);

                    result = existingResult;

                    // Audit log
                    await _auditService.LogActionAsync(
                        entityType: "CourseResult",
                        entityId: result.Id,
                        studentId: studentId,
                        courseId: courseId,
                        academicYearId: academicYearId,
                        actionType: "Recalculated",
                        oldValue: oldValue,
                        newValue: JsonSerializer.Serialize(new
                        {
                            normalizedTotal,
                            gradeLetter = gradeConfig.GradeLetter,
                            gradePoints = gradeConfig.GPAValue,
                            isPassed
                        }),
                        userId: userId,
                        reason: "Result recalculated"
                    );
                }
                else
                {
                    // Create new result
                    result = new StudentCourseResult
                    {
                        StudentId = studentId,
                        CourseId = courseId,
                        AcademicYearId = academicYearId,
                        YearPeriodId = semester,
                        WeightedTotal = weightedTotal,
                        NormalizedTotal = normalizedTotal,
                        GradeLetter = gradeConfig.GradeLetter,
                        GradePoints = gradeConfig.GPAValue,
                        IsPassed = isPassed,
                        Credits = credits,
                        CreditsEarned = creditsEarned,
                        Status = Status.Draft,
                        PassMark = (decimal)course.PassMark,
                        TotalWeightPercentage = totalWeight,
                        AssessmentCount = assessmentScores.Count,
                        CalculatedAt = DateTime.Now,
                        CalculatedBy = userId,
                        IsCarryover = false,
                        AttemptNumber = 1,
                        ResultHash = string.Empty
                    };

                    // Generate hash
                    result.ResultHash = _integrityService.GenerateResultHash(result);

                    _context.StudentCourseResults.Add(result);

                    // Audit log
                    await _auditService.LogActionAsync(
                        entityType: "CourseResult",
                        entityId: result.Id,
                        studentId: studentId,
                        courseId: courseId,
                        academicYearId: academicYearId,
                        actionType: "Created",
                        oldValue: null,
                        newValue: JsonSerializer.Serialize(new
                        {
                            normalizedTotal,
                            gradeLetter = gradeConfig.GradeLetter,
                            gradePoints = gradeConfig.GPAValue,
                            isPassed
                        }),
                        userId: userId,
                        reason: "Initial calculation"
                    );
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Result calculated: StudentId={StudentId}, CourseId={CourseId}, Total={Total}, Grade={Grade}",
                    studentId, courseId, normalizedTotal, gradeConfig.GradeLetter);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error calculating result for StudentId={StudentId}, CourseId={CourseId}",
                    studentId, courseId);
                throw;
            }
        }

        /// <summary>
        /// Calculate results for all students in a course
        /// </summary>
        public async Task<List<StudentCourseResult>> CalculateResultsForCourseAsync(
            int courseId,
            int academicYearId,
            int semester,
            string userId)
        {
            try
            {
                // Get all students enrolled in this course
                var enrolledStudents = await _context.StudentAssessmentScores
                    .Where(s =>
                        s.CourseId == courseId &&
                        s.AcademicYearId == academicYearId &&
                        s.YearPeriodId == semester &&
                        s.IsActive)
                    .Select(s => s.StudentId)
                    .Distinct()
                    .ToListAsync();

                var results = new List<StudentCourseResult>();

                foreach (var studentId in enrolledStudents)
                {
                    try
                    {
                        var result = await CalculateResultAsync(
                            studentId, courseId, academicYearId, semester, userId);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to calculate result for StudentId={StudentId} in CourseId={CourseId}",
                            studentId, courseId);
                    }
                }

                _logger.LogInformation(
                    "Batch calculation completed: CourseId={CourseId}, {SuccessCount}/{TotalCount} successful",
                    courseId, results.Count, enrolledStudents.Count);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error calculating results for CourseId={CourseId}",
                    courseId);
                throw;
            }
        }

        /// <summary>
        /// Recalculate an existing result
        /// </summary>
        public async Task<StudentCourseResult> RecalculateResultAsync(
            int resultId,
            string userId,
            string reason)
        {
            try
            {
                var existingResult = await _context.StudentCourseResults
                    .FirstOrDefaultAsync(r => r.Id == resultId);

                if (existingResult == null)
                {
                    throw new ArgumentException("Result not found", nameof(resultId));
                }

                // Verify integrity before recalculation
                if (!_integrityService.VerifyResultHash(existingResult))
                {
                    _logger.LogWarning(
                        "Result integrity check failed before recalculation. ResultId={ResultId}",
                        resultId);
                    throw new InvalidOperationException(
                        "Result data integrity check failed. Possible tampering detected.");
                }

                return await CalculateResultAsync(
                    existingResult.StudentId,
                    existingResult.CourseId,
                    existingResult.AcademicYearId,
                    existingResult.YearPeriodId ?? 0,
                    userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error recalculating result. ResultId={ResultId}",
                    resultId);
                throw;
            }
        }

        /// <summary>
        /// Publish a single result (make it visible to students)
        /// </summary>
        public async Task<bool> PublishResultAsync(int resultId, string userId)
        {
            try
            {
                var result = await _context.StudentCourseResults
                    .FirstOrDefaultAsync(r => r.Id == resultId);

                if (result == null)
                {
                    return false;
                }

                if (result.Status == Status.Published)
                {
                    _logger.LogWarning("Result already published. ResultId={ResultId}", resultId);
                    return true;
                }

                // Verify integrity before publishing
                if (!_integrityService.VerifyResultHash(result))
                {
                    throw new InvalidOperationException(
                        "Result integrity check failed. Cannot publish potentially tampered data.");
                }

                var oldStatus = result.Status;

                result.Status = Status.Published;
                result.PublishedBy = userId;
                result.PublishedAt = DateTime.Now;

                // Regenerate hash with new status
                result.ResultHash = _integrityService.GenerateResultHash(result);

                await _context.SaveChangesAsync();

                // Audit log
                await _auditService.LogActionAsync(
                    entityType: "CourseResult",
                    entityId: resultId,
                    studentId: result.StudentId,
                    courseId: result.CourseId,
                    academicYearId: result.AcademicYearId,
                    actionType: "Published",
                    oldValue: JsonSerializer.Serialize(new { status = oldStatus }),
                    newValue: JsonSerializer.Serialize(new { status = Status.Published }),
                    userId: userId,
                    reason: "Result published to student"
                );

                _logger.LogInformation(
                    "Result published: ResultId={ResultId}, StudentId={StudentId}, CourseId={CourseId}",
                    resultId, result.StudentId, result.CourseId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing result. ResultId={ResultId}", resultId);
                throw;
            }
        }

        /// <summary>
        /// Publish all results for a course
        /// </summary>
        public async Task<bool> PublishResultsForCourseAsync(
            int courseId,
            int academicYearId,
            int semester,
            string userId)
        {
            var batchId = Guid.NewGuid().ToString();

            try
            {
                var results = await _context.StudentCourseResults
                    .Where(r =>
                        r.CourseId == courseId &&
                        r.AcademicYearId == academicYearId &&
                        r.YearPeriodId == semester &&
                        r.Status != Status.Published)
                    .ToListAsync();

                if (!results.Any())
                {
                    _logger.LogWarning(
                        "No unpublished results found for CourseId={CourseId}, Semester={Semester}",
                        courseId, semester);
                    return false;
                }

                foreach (var result in results)
                {
                    result.Status = Status.Published;
                    result.PublishedBy = userId;
                    result.PublishedAt = DateTime.Now;
                    result.ResultHash = _integrityService.GenerateResultHash(result);

                    // Audit log
                    await _auditService.LogActionAsync(
                        entityType: "CourseResult",
                        entityId: result.Id,
                        studentId: result.StudentId,
                        courseId: courseId,
                        academicYearId: academicYearId,
                        actionType: "Published",
                        oldValue: JsonSerializer.Serialize(new { status = Status.Draft }),
                        newValue: JsonSerializer.Serialize(new { status = Status.Published }),
                        userId: userId,
                        reason: "Batch publication",
                        isBatch: true,
                        batchId: batchId
                    );
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Batch publication completed: CourseId={CourseId}, {Count} results published, BatchId={BatchId}",
                    courseId, results.Count, batchId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error publishing results for CourseId={CourseId}, BatchId={BatchId}",
                    courseId, batchId);
                throw;
            }
        }

        /// <summary>
        /// Get a specific result
        /// </summary>
        public async Task<StudentCourseResult?> GetResultAsync(
            int studentId,
            int courseId,
            int academicYearId,
            int semester)
        {
            var result = await _context.StudentCourseResults
                .Include(r => r.Student)
                .Include(r => r.Course)
                .Include(r => r.AcademicYear)
                .FirstOrDefaultAsync(r =>
                    r.StudentId == studentId &&
                    r.CourseId == courseId &&
                    r.AcademicYearId == academicYearId &&
                    r.YearPeriodId == semester);

            // Manually load assessment scores if result exists
            if (result != null)
            {
                result.AssessmentScores = await _context.StudentAssessmentScores
                    .Include(s => s.Assessment)
                    .Where(s =>
                        s.StudentId == result.StudentId &&
                        s.CourseId == result.CourseId &&
                        s.AcademicYearId == result.AcademicYearId &&
                        s.YearPeriodId == result.YearPeriodId &&
                        s.IsActive)
                    .ToListAsync();

                // Verify integrity
                if (!_integrityService.VerifyResultHash(result))
                {
                    _logger.LogWarning(
                        "Result integrity check failed. ResultId={ResultId}",
                        result.Id);
                }
            }

            return result;
        }

        /// <summary>
        /// Get all results for a student in an academic year
        /// </summary>
        public async Task<List<StudentCourseResult>> GetResultsForStudentAsync(
            int studentId,
            int academicYearId)
        {
            return await _context.StudentCourseResults
                .Include(r => r.Course)
                .Include(r => r.AcademicYear)
                .Include(r => r.AssessmentScores)
                    .ThenInclude(s => s.Assessment)
                .Where(r =>
                    r.StudentId == studentId &&
                    r.AcademicYearId == academicYearId)
                .OrderBy(r => r.YearPeriodId)
                .ThenBy(r => r.Course.CourseCode)
                .ToListAsync();
        }

        /// <summary>
        /// Calculate GPA for a student in an academic year
        /// </summary>
        public async Task<decimal> CalculateGPAAsync(int studentId, int academicYearId)
        {
            try
            {
                var results = await _context.StudentCourseResults
                    .Where(r =>
                        r.StudentId == studentId &&
                        r.AcademicYearId == academicYearId &&
                        r.Status == Status.Published)
                    .ToListAsync();

                if (!results.Any())
                {
                    return 0;
                }

                decimal totalGradePoints = 0;
                int totalCredits = 0;

                foreach (var result in results)
                {
                    totalGradePoints += result.GradePoints * result.Credits;
                    totalCredits += result.Credits;
                }

                return totalCredits > 0 ? totalGradePoints / totalCredits : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error calculating GPA for StudentId={StudentId}, AcademicYearId={AcademicYearId}",
                    studentId, academicYearId);
                throw;
            }
        }
    }
}