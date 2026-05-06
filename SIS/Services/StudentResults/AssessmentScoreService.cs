using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.Results;
using SIS.Models.StudentApplication;
using SIS.Models.StudentResults;
using System.Text.Json;

namespace SIS.Services
{
    public interface IAssessmentScoreService
    {
        Task<StudentAssessmentScore> RecordScoreAsync(int studentId, int courseId, int academicYearId,
            int assessmentId, int semester, decimal score, string userId, int rsbId, int yearOfStudy, int attempt = 1, string? remarks = null);
        Task<StudentAssessmentScore> UpdateScoreAsync(int scoreId, decimal newScore, string userId, string reason, int rsbId);
        Task<List<StudentAssessmentScore>> GetScoresForStudentAsync(int studentId, int courseId, int academicYearId);
        Task<List<StudentAssessmentScore>> GetScoresForCourseAsync(int courseId, int academicYearId, int semester);
        Task<bool> ValidateScoreIntegrityAsync(int scoreId);
        Task<bool> DeleteScoreAsync(int scoreId, string userId, string reason);
        Task<int> BulkRecordScoresAsync(List<BulkScoreInput> scores, string userId, int rsbId);
        Task<StudentAssessmentScore?> GetScoreByIdAsync(int scoreId);
        Task<bool> ScoreExistsAsync(int studentId, int courseId, int assessmentId, int academicYearId, int semester);
    }

    public class AssessmentScoreService : IAssessmentScoreService
    {
        private readonly ApplicationDbContext _context;
        private readonly IResultIntegrityService _integrityService;
        private readonly IResultAuditService _auditService;
        private readonly ILogger<AssessmentScoreService> _logger;

        public AssessmentScoreService(
            ApplicationDbContext context,
            IResultIntegrityService integrityService,
            IResultAuditService auditService,
            ILogger<AssessmentScoreService> logger)
        {
            _context = context;
            _integrityService = integrityService;
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>
        /// Record a new assessment score for a student
        /// </summary>
        public async Task<StudentAssessmentScore> RecordScoreAsync(
            int studentId,
            int courseId,
            int academicYearId,
            int assessmentId,
            int semester,
            decimal score,
            string userId,
            int rsbId,
            int yearOfStudy,
            int attempt = 1,
            string? remarks = null)
        {
            try
            {
                // Validate score range
                if (score < 0 || score > 100)
                {
                    throw new ArgumentException("Score must be between 0 and 100", nameof(score));
                }

                // Check if score already exists
                var existingScore = await _context.StudentAssessmentScores
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == studentId &&
                        s.CourseId == courseId &&
                        s.AssessmentId == assessmentId &&
                        s.AcademicYearId == academicYearId &&
                        s.YearPeriodId == semester &&
                        s.Attempt == attempt &&
                        s.IsActive);

                if (existingScore != null)
                {
                    existingScore.StudentId = studentId;
                    existingScore.CourseId = courseId;
                    existingScore.AcademicYearId = academicYearId;
                    existingScore.AssessmentId = assessmentId;
                    existingScore.YearPeriodId = semester;
                    existingScore.Score = score;
                    existingScore.MaxScore = 100;
                    //existingScore.WeightPercentage = assessment.WeightPercentage;
                    existingScore.RecordedBy = userId;
                    existingScore.RecordedAt = DateTime.Now;
                    existingScore.IsActive = true;
                    existingScore.rsbId = rsbId;
                    existingScore.YearOfStudy = yearOfStudy;
                    existingScore.Attempt = attempt;
                    existingScore.Remarks = remarks;
                    existingScore.ScoreHash = _integrityService.GenerateScoreHash(existingScore);
                    _context.Update(existingScore);
                    await _context.SaveChangesAsync();
                    return existingScore;
                    /*throw new InvalidOperationException(
                        $"Score already exists for this student and assessment. Use UpdateScoreAsync instead.");*/
                }

                // Get assessment weight percentage
                var assessment = await _context.Assessments.FindAsync(assessmentId);
                if (assessment == null)
                {
                    throw new ArgumentException("Assessment not found", nameof(assessmentId));
                }

                // Create new score record
                var assessmentScore = new StudentAssessmentScore
                {
                    StudentId = studentId,
                    CourseId = courseId,
                    AcademicYearId = academicYearId,
                    AssessmentId = assessmentId,
                    YearPeriodId = semester,
                    Score = score,
                    MaxScore = 100,
                    WeightPercentage = assessment.WeightPercentage,
                    RecordedBy = userId,
                    RecordedAt = DateTime.Now,
                    IsActive = true,
                    rsbId = rsbId,
                    YearOfStudy = yearOfStudy,
                    Attempt = attempt,
                    Remarks = remarks,
                    ScoreHash = string.Empty // Will be set after generation
                };

                // Generate hash
                assessmentScore.ScoreHash = _integrityService.GenerateScoreHash(assessmentScore);

                // Save to database
                _context.StudentAssessmentScores.Add(assessmentScore);
                await _context.SaveChangesAsync();

                // Create audit log
                await _auditService.LogActionAsync(
                    entityType: "AssessmentScore",
                    entityId: assessmentScore.Id,
                    studentId: studentId,
                    courseId: courseId,
                    academicYearId: academicYearId,
                    actionType: "Created",
                    oldValue: null,
                    newValue: JsonSerializer.Serialize(new
                    {
                        score,
                        assessmentId,
                        weight = assessment.WeightPercentage
                    }),
                    userId: userId,
                    reason: remarks
                );

                _logger.LogInformation(
                    "Assessment score recorded: StudentId={StudentId}, CourseId={CourseId}, AssessmentId={AssessmentId}, Score={Score}",
                    studentId, courseId, assessmentId, score);

                return assessmentScore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording assessment score for StudentId={StudentId}, CourseId={CourseId}",
                    studentId, courseId);
                throw;
            }
        }

        /// <summary>
        /// Update an existing assessment score
        /// </summary>
        public async Task<StudentAssessmentScore> UpdateScoreAsync(int scoreId, decimal newScore, string userId, string reason, int rsbId)
        {
            try
            {
                // Validate score range
                if (newScore < 0 || newScore > 100)
                {
                    throw new ArgumentException("Score must be between 0 and 100", nameof(newScore));
                }

                // Get existing score
                var existingScore = await _context.StudentAssessmentScores
                    .Include(s => s.Assessment)
                    .FirstOrDefaultAsync(s => s.Id == scoreId && s.IsActive);

                if (existingScore == null)
                {
                    throw new ArgumentException("Score not found or has been deleted", nameof(scoreId));
                }

                // ⭐ MODIFIED: Check integrity but allow update to restore it
                bool wasIntegrityValid = _integrityService.VerifyScoreHash(existingScore);

                if (!wasIntegrityValid)
                {
                    _logger.LogWarning(
                        "Score integrity check failed before update. ScoreId={ScoreId}. Allowing update to restore integrity.",
                        scoreId);

                    // Add note to remarks about integrity restoration
                    reason = string.IsNullOrEmpty(reason)
                        ? "Integrity restoration - score re-entered after tampering detection"
                        : $"{reason} (Integrity restoration)";
                }

                // Store old values for audit
                var oldValue = JsonSerializer.Serialize(new
                {
                    score = existingScore.Score,
                    recordedBy = existingScore.RecordedBy,
                    recordedAt = existingScore.RecordedAt,
                    integrityValid = wasIntegrityValid // ⭐ NEW: Track integrity status
                });

                // Update score
                existingScore.Score = newScore;
                existingScore.ModifiedBy = userId;
                existingScore.ModifiedAt = DateTime.Now;
                existingScore.rsbId = rsbId;
                existingScore.Remarks = string.IsNullOrEmpty(existingScore.Remarks)
                    ? reason
                    : $"{existingScore.Remarks}; {reason}";

                // Regenerate hash - this will restore integrity
                existingScore.ScoreHash = _integrityService.GenerateScoreHash(existingScore);

                await _context.SaveChangesAsync();

                // Create audit log
                await _auditService.LogActionAsync(
                    entityType: "AssessmentScore",
                    entityId: scoreId,
                    studentId: existingScore.StudentId,
                    courseId: existingScore.CourseId,
                    academicYearId: existingScore.AcademicYearId,
                    actionType: wasIntegrityValid ? "Updated" : "IntegrityRestored", // ⭐ NEW
                    oldValue: oldValue,
                    newValue: JsonSerializer.Serialize(new
                    {
                        score = newScore,
                        integrityRestored = !wasIntegrityValid // ⭐ NEW
                    }),
                    userId: userId,
                    reason: reason
                );

                _logger.LogInformation(
                    "Assessment score updated: ScoreId={ScoreId}, OldScore={OldScore}, NewScore={NewScore}, IntegrityRestored={IntegrityRestored}",
                    scoreId, existingScore.Score, newScore, !wasIntegrityValid);

                return existingScore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating assessment score. ScoreId={ScoreId}", scoreId);
                throw;
            }
        }

        /// <summary>
        /// Get all assessment scores for a student in a specific course
        /// </summary>
        public async Task<List<StudentAssessmentScore>> GetScoresForStudentAsync(
            int studentId,
            int courseId,
            int academicYearId)
        {
            try
            {
                var scores = await _context.StudentAssessmentScores
                    .Include(s => s.Assessment)
                    .Include(s => s.Course)
                    .Where(s =>
                        s.StudentId == studentId &&
                        s.CourseId == courseId &&
                        s.AcademicYearId == academicYearId &&
                        s.IsActive)
                    .OrderBy(s => s.YearPeriodId)
                    .ThenBy(s => s.Assessment.Name)
                    .ToListAsync();

                // Verify integrity of all scores
                foreach (var score in scores)
                {
                    if (!_integrityService.VerifyScoreHash(score))
                    {
                        _logger.LogWarning(
                            "Score integrity check failed. ScoreId={ScoreId}, StudentId={StudentId}",
                            score.Id, studentId);
                    }
                }

                return scores;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving scores for StudentId={StudentId}, CourseId={CourseId}",
                    studentId, courseId);
                throw;
            }
        }

        /// <summary>
        /// Get all assessment scores for a course (all students)
        /// </summary>
        public async Task<List<StudentAssessmentScore>> GetScoresForCourseAsync(
            int courseId,
            int academicYearId,
            int semester)
        {
            try
            {
                return await _context.StudentAssessmentScores
                    .Include(s => s.Student)
                    .Include(s => s.Assessment)
                    .Where(s =>
                        s.CourseId == courseId &&
                        s.AcademicYearId == academicYearId &&
                        s.YearPeriodId == semester &&
                        s.IsActive)
                    .OrderBy(s => s.Student.StudentId_Number)
                    .ThenBy(s => s.Assessment.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving scores for CourseId={CourseId}, Semester={Semester}",
                    courseId, semester);
                throw;
            }
        }

        /// <summary>
        /// Validate the integrity of a specific score
        /// </summary>
        public async Task<bool> ValidateScoreIntegrityAsync(int scoreId)
        {
            try
            {
                var score = await _context.StudentAssessmentScores
                    .FirstOrDefaultAsync(s => s.Id == scoreId);

                if (score == null)
                {
                    return false;
                }

                return _integrityService.VerifyScoreHash(score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating score integrity. ScoreId={ScoreId}", scoreId);
                return false;
            }
        }

        /// <summary>
        /// Soft delete a score (sets IsActive to false)
        /// </summary>
        public async Task<bool> DeleteScoreAsync(int scoreId, string userId, string reason)
        {
            try
            {
                var score = await _context.StudentAssessmentScores
                    .FirstOrDefaultAsync(s => s.Id == scoreId && s.IsActive);

                if (score == null)
                {
                    return false;
                }

                // Store old value for audit
                var oldValue = JsonSerializer.Serialize(new
                {
                    score = score.Score,
                    isActive = score.IsActive
                });

                // Soft delete
                score.IsActive = false;
                score.ModifiedBy = userId;
                score.ModifiedAt = DateTime.Now;
                score.Remarks = string.IsNullOrEmpty(score.Remarks)
                    ? $"Deleted: {reason}"
                    : $"{score.Remarks}; Deleted: {reason}";

                await _context.SaveChangesAsync();

                // Create audit log
                await _auditService.LogActionAsync(
                    entityType: "AssessmentScore",
                    entityId: scoreId,
                    studentId: score.StudentId,
                    courseId: score.CourseId,
                    academicYearId: score.AcademicYearId,
                    actionType: "Deleted",
                    oldValue: oldValue,
                    newValue: JsonSerializer.Serialize(new { isActive = false }),
                    userId: userId,
                    reason: reason
                );

                _logger.LogInformation("Assessment score deleted: ScoreId={ScoreId}, Reason={Reason}",
                    scoreId, reason);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting score. ScoreId={ScoreId}", scoreId);
                throw;
            }
        }

        /// <summary>
        /// Bulk record assessment scores
        /// </summary>
        public async Task<int> BulkRecordScoresAsync(List<BulkScoreInput> scores, string userId, int rsbId)
        {
            var successCount = 0;
            var batchId = Guid.NewGuid().ToString();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var input in scores)
                {
                    var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == input.StudentId);
                    try
                    {
                        await RecordScoreAsync(
                            input.StudentId,
                            input.CourseId,
                            input.AcademicYearId,
                            input.AssessmentId,
                            input.Semester,
                            input.Score,
                            userId,
                            rsbId,
                            (int)student.StudentCurrentYear,
                            1,
                            $"Bulk import - Batch: {batchId}"
                        );
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to record score in bulk operation for StudentId={StudentId}",
                            input.StudentId);
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Bulk score import completed: {SuccessCount}/{TotalCount} successful, BatchId={BatchId}",
                    successCount, scores.Count, batchId);

                return successCount;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in bulk score recording. BatchId={BatchId}", batchId);
                throw;
            }
        }

        /// <summary>
        /// Get a single score by ID
        /// </summary>
        public async Task<StudentAssessmentScore?> GetScoreByIdAsync(int scoreId)
        {
            return await _context.StudentAssessmentScores
                .Include(s => s.Assessment)
                .Include(s => s.Student)
                .Include(s => s.Course)
                .FirstOrDefaultAsync(s => s.Id == scoreId);
        }

        /// <summary>
        /// Check if a score already exists
        /// </summary>
        public async Task<bool> ScoreExistsAsync(
            int studentId,
            int courseId,
            int assessmentId,
            int academicYearId,
            int semester)
        {
            return await _context.StudentAssessmentScores
                .AnyAsync(s =>
                    s.StudentId == studentId &&
                    s.CourseId == courseId &&
                    s.AssessmentId == assessmentId &&
                    s.AcademicYearId == academicYearId &&
                    s.YearPeriodId == semester &&
                    s.IsActive);
        }
    }

    /// <summary>
    /// Model for bulk score input
    /// </summary>
    public class BulkScoreInput
    {
        public int StudentId { get; set; }
        public int CourseId { get; set; }
        public int AcademicYearId { get; set; }
        public int AssessmentId { get; set; }
        public int Semester { get; set; }
        public decimal Score { get; set; }
    }
}