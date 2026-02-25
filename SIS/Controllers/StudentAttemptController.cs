using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Assessments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIS.Web.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Basic authorization, customize as needed
    public class StudentAttemptController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StudentAttemptController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/StudentAttempt
        [HttpGet]
        [Authorize(Roles = "Admin,Instructor")] // Restrict to admin and instructors
        public async Task<ActionResult<IEnumerable<StudentAttempt>>> GetStudentAttempts()
        {
            return await _context.StudentAttempts
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Assessment)
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Course)
                .ToListAsync();
        }

        // GET: api/StudentAttempt/5
        [HttpGet("{id}")]
        public async Task<ActionResult<StudentAttempt>> GetStudentAttempt(int id)
        {
            var studentAttempt = await _context.StudentAttempts
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Assessment)
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Course)
                .Include(sa => sa.Responses)
                .FirstOrDefaultAsync(sa => sa.Id == id);

            if (studentAttempt == null)
            {
                return NotFound();
            }

            // Security check - only allow instructors, admins, or the student who owns the attempt
            string currentUserId = User.Identity.Name; // Assuming Identity.Name contains user ID
            bool isAdmin = User.IsInRole("Admin");
            bool isInstructor = User.IsInRole("Instructor");

            if (!isAdmin && !isInstructor && studentAttempt.StudentId != currentUserId)
            {
                return Forbid();
            }

            return studentAttempt;
        }

        // GET: api/StudentAttempt/ByStudent/studentId
        [HttpGet("ByStudent/{studentId}")]
        public async Task<ActionResult<IEnumerable<StudentAttempt>>> GetStudentAttemptsByStudent(string studentId)
        {
            // Security check - only allow instructors, admins, or the student themself
            string currentUserId = User.Identity.Name; // Assuming Identity.Name contains user ID
            bool isAdmin = User.IsInRole("Admin");
            bool isInstructor = User.IsInRole("Instructor");

            if (!isAdmin && !isInstructor && studentId != currentUserId)
            {
                return Forbid();
            }

            return await _context.StudentAttempts
                .Where(sa => sa.StudentId == studentId)
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Assessment)
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Course)
                .OrderByDescending(sa => sa.StartTime)
                .ToListAsync();
        }

        // GET: api/StudentAttempt/ByAssessment/5
        [HttpGet("ByAssessment/{assessmentConfigId}")]
        [Authorize(Roles = "Admin,Instructor")] // Restrict to admin and instructors
        public async Task<ActionResult<IEnumerable<StudentAttempt>>> GetStudentAttemptsByAssessment(int assessmentConfigId)
        {
            var assessmentConfig = await _context.AssessmentConfigurations.FindAsync(assessmentConfigId);
            if (assessmentConfig == null)
            {
                return NotFound("Assessment configuration not found");
            }

            return await _context.StudentAttempts
                .Where(sa => sa.AssessmentConfigurationId == assessmentConfigId)
                .Include(sa => sa.Responses)
                .OrderByDescending(sa => sa.StartTime)
                .ToListAsync();
        }

        // POST: api/StudentAttempt
        [HttpPost]
        public async Task<ActionResult<StudentAttempt>> StartAttempt(StartAttemptModel model)
        {
            var studentId = User.Identity.Name; // Assuming Identity.Name contains user ID

            // Validate assessment configuration exists
            var assessmentConfig = await _context.AssessmentConfigurations
                .Include(ac => ac.Assessment)
                .Include(ac => ac.QuestionGroups)
                    .ThenInclude(qg => qg.QuestionGroup)
                        .ThenInclude(g => g.Questions)
                .FirstOrDefaultAsync(ac => ac.Id == model.AssessmentConfigurationId);

            if (assessmentConfig == null)
            {
                return BadRequest("Assessment configuration not found");
            }

            // Check if the assessment is published and available
            var now = DateTime.Now;
            if (!assessmentConfig.IsPublished || now < assessmentConfig.StartDateTime || now > assessmentConfig.EndDateTime)
            {
                return BadRequest("Assessment is not available at this time");
            }

            // Check if student already has an in-progress attempt
            var existingAttempt = await _context.StudentAttempts
                .FirstOrDefaultAsync(sa =>
                    sa.StudentId == studentId &&
                    sa.AssessmentConfigurationId == model.AssessmentConfigurationId &&
                    sa.Status == "InProgress");

            if (existingAttempt != null)
            {
                return BadRequest("You already have an attempt in progress for this assessment");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Create the attempt
                var attempt = new StudentAttempt
                {
                    StudentId = studentId,
                    AssessmentConfigurationId = model.AssessmentConfigurationId,
                    StartTime = now,
                    Status = "InProgress",
                    CreatedBy = studentId,
                    CreatedAt = now
                };

                _context.StudentAttempts.Add(attempt);
                await _context.SaveChangesAsync();

                // Select questions for the attempt
                var questionIds = new List<int>();

                foreach (var assessmentQuestionGroup in assessmentConfig.QuestionGroups)
                {
                    var questions = assessmentQuestionGroup.QuestionGroup.Questions.Where(q => q.IsActive).ToList();

                    // Determine how many questions to select
                    int numToSelect = Math.Min(assessmentQuestionGroup.NumberOfQuestionsToUse, questions.Count);

                    // Select random questions if randomization is enabled
                    if (assessmentConfig.RandomizeQuestions && numToSelect < questions.Count)
                    {
                        var random = new Random();
                        var selectedQuestions = questions
                            .OrderBy(q => random.Next())
                            .Take(numToSelect)
                            .ToList();

                        questionIds.AddRange(selectedQuestions.Select(q => q.Id));
                    }
                    else
                    {
                        // Otherwise take the first N questions
                        questionIds.AddRange(questions.Take(numToSelect).Select(q => q.Id));
                    }
                }

                // Create empty responses for each question
                foreach (var questionId in questionIds)
                {
                    var response = new StudentResponse
                    {
                        StudentAttemptId = attempt.Id,
                        QuestionId = questionId,
                        ResponseText = "",
                        IsGraded = false,
                        CreatedBy = studentId,
                        CreatedAt = now
                    };

                    _context.StudentResponses.Add(response);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Return the created attempt with responses
                var attemptWithResponses = await _context.StudentAttempts
                    .Include(sa => sa.Responses)
                        .ThenInclude(r => r.Question)
                    .FirstOrDefaultAsync(sa => sa.Id == attempt.Id);

                return CreatedAtAction(nameof(GetStudentAttempt), new { id = attempt.Id }, attemptWithResponses);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/StudentAttempt/5/Submit
        [HttpPut("{id}/Submit")]
        public async Task<IActionResult> SubmitAttempt(int id)
        {
            var studentId = User.Identity.Name; // Assuming Identity.Name contains user ID

            var attempt = await _context.StudentAttempts
                .Include(sa => sa.Responses)
                    .ThenInclude(r => r.Question)
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Assessment)
                .FirstOrDefaultAsync(sa => sa.Id == id);

            if (attempt == null)
            {
                return NotFound();
            }

            // Security check - only the student who owns the attempt can submit it
            if (attempt.StudentId != studentId)
            {
                return Forbid();
            }

            // Check if attempt is still in progress
            if (attempt.Status != "InProgress")
            {
                return BadRequest("This attempt has already been submitted");
            }

            try
            {
                // Mark the attempt as submitted
                attempt.EndTime = DateTime.Now;
                attempt.Status = "Submitted";
                attempt.UpdatedBy = studentId;
                attempt.UpdatedAt = DateTime.Now;

                // Auto-grade multiple choice questions
                decimal totalScore = 0;
                decimal totalPoints = 0;

                foreach (var response in attempt.Responses)
                {
                    // Only auto-grade multiple choice and true/false questions
                    if (response.Question.QuestionType == "MultipleChoice" ||
                        response.Question.QuestionType == "TrueFalse")
                    {
                        // Skip if response is empty
                        if (string.IsNullOrEmpty(response.ResponseText))
                        {
                            response.IsCorrect = false;
                            response.Score = 0;
                            response.IsGraded = true;
                        }
                        else
                        {
                            // Check if selected option(s) match the correct option(s)
                            var selectedOptions = response.ResponseText.Split(',').Select(int.Parse).ToList();

                            // Get correct options for this question
                            var correctOptions = await _context.QuestionOptions
                                .Where(qo => qo.QuestionId == response.QuestionId && qo.IsCorrect)
                                .Select(qo => qo.Id)
                                .ToListAsync();

                            // For single-select questions (most common case)
                            if (selectedOptions.Count == 1 && correctOptions.Count == 1)
                            {
                                response.IsCorrect = selectedOptions[0] == correctOptions[0];
                                response.Score = (response.IsCorrect ?? false) ? response.Question.Points : 0;

                            }
                            // For multi-select questions, require exact match
                            else
                            {
                                var isFullyCorrect = selectedOptions.Count == correctOptions.Count &&
                                                  !selectedOptions.Except(correctOptions).Any();

                                response.IsCorrect = isFullyCorrect;
                                response.Score = isFullyCorrect ? response.Question.Points : 0;
                            }

                            response.IsGraded = true;
                        }
                    }

                    // Add to totals if graded
                    if (response.IsGraded && response.Score.HasValue)
                    {
                        totalScore += response.Score.Value;
                    }

                    totalPoints += response.Question.Points;
                }

                // Calculate final score and pass/fail status if all questions are graded
                bool allGraded = attempt.Responses.All(r => r.IsGraded);

                if (allGraded)
                {
                    attempt.TotalScore = totalScore;

                    // Calculate percentage
                    if (totalPoints > 0)
                    {
                        attempt.Percentage = (totalScore / totalPoints) * 100;

                        // Set pass/fail status
                        attempt.Passed = attempt.Percentage >= attempt.AssessmentConfiguration.Assessment.PassMark;
                    }
                    else
                    {
                        attempt.Percentage = 0;
                        attempt.Passed = false;
                    }
                }

                _context.Entry(attempt).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/StudentAttempt/5/AutoSubmit
        [HttpPut("{id}/AutoSubmit")]
        public async Task<IActionResult> AutoSubmitAttempt(int id)
        {
            var attempt = await _context.StudentAttempts
                .Include(sa => sa.Responses)
                    .ThenInclude(r => r.Question)
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Assessment)
                .FirstOrDefaultAsync(sa => sa.Id == id);

            if (attempt == null)
            {
                return NotFound();
            }

            // Check if attempt is still in progress
            if (attempt.Status != "InProgress")
            {
                return BadRequest("This attempt has already been submitted");
            }

            // Validate that the auto-submit is legitimate
            var now = DateTime.Now;
            var expectedEndTime = attempt.StartTime.AddMinutes(attempt.AssessmentConfiguration.DurationMinutes);
            var assessmentEndTime = attempt.AssessmentConfiguration.EndDateTime;

            bool timeExpired = now >= expectedEndTime || now >= assessmentEndTime;

            // Only allow auto-submit if time has expired or from an administrator
            bool isAdmin = User.IsInRole("Admin");
            if (!timeExpired && !isAdmin)
            {
                return BadRequest("Cannot auto-submit before time expires");
            }

            try
            {
                // Mark the attempt as timed out
                attempt.EndTime = now;
                attempt.Status = "TimedOut";
                attempt.UpdatedBy = "System"; // Or use admin ID if it's an admin action
                attempt.UpdatedAt = now;

                // Auto-grade multiple choice questions (same logic as SubmitAttempt)
                decimal totalScore = 0;
                decimal totalPoints = 0;

                foreach (var response in attempt.Responses)
                {
                    // Only auto-grade multiple choice and true/false questions
                    if (response.Question.QuestionType == "MultipleChoice" ||
                        response.Question.QuestionType == "TrueFalse")
                    {
                        // Skip if response is empty
                        if (string.IsNullOrEmpty(response.ResponseText))
                        {
                            response.IsCorrect = false;
                            response.Score = 0;
                            response.IsGraded = true;
                        }
                        else
                        {
                            // Check if selected option(s) match the correct option(s)
                            var selectedOptions = response.ResponseText.Split(',').Select(int.Parse).ToList();

                            // Get correct options for this question
                            var correctOptions = await _context.QuestionOptions
                                .Where(qo => qo.QuestionId == response.QuestionId && qo.IsCorrect)
                                .Select(qo => qo.Id)
                                .ToListAsync();

                            // For single-select questions
                            if (selectedOptions.Count == 1 && correctOptions.Count == 1)
                            {
                                response.IsCorrect = selectedOptions[0] == correctOptions[0];
                                response.Score = (response.IsCorrect ?? false) ? response.Question.Points : 0;

                            }
                            // For multi-select questions, require exact match
                            else
                            {
                                var isFullyCorrect = selectedOptions.Count == correctOptions.Count &&
                                                  !selectedOptions.Except(correctOptions).Any();

                                response.IsCorrect = isFullyCorrect;
                                response.Score = isFullyCorrect ? response.Question.Points : 0;
                            }

                            response.IsGraded = true;
                        }
                    }

                    // Add to totals if graded
                    if (response.IsGraded && response.Score.HasValue)
                    {
                        totalScore += response.Score.Value;
                    }

                    totalPoints += response.Question.Points;
                }

                // Calculate final score and pass/fail status if all questions are graded
                bool allGraded = attempt.Responses.All(r => r.IsGraded);

                if (allGraded)
                {
                    attempt.TotalScore = totalScore;

                    // Calculate percentage
                    if (totalPoints > 0)
                    {
                        attempt.Percentage = (totalScore / totalPoints) * 100;

                        // Set pass/fail status
                        attempt.Passed = attempt.Percentage >= attempt.AssessmentConfiguration.Assessment.PassMark;
                    }
                    else
                    {
                        attempt.Percentage = 0;
                        attempt.Passed = false;
                    }
                }

                _context.Entry(attempt).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/StudentAttempt/AutoSubmitExpired
        [HttpPut("AutoSubmitExpired")]
        [Authorize(Roles = "Admin,System")] // Restrict to admin or system account
        public async Task<IActionResult> AutoSubmitExpiredAttempts()
        {
            var now = DateTime.Now;

            // Find all in-progress attempts that have exceeded their time limit
            var expiredAttempts = await _context.StudentAttempts
                .Include(sa => sa.AssessmentConfiguration)
                .Include(sa => sa.Responses)
                    .ThenInclude(r => r.Question)
                .Where(sa =>
                    sa.Status == "InProgress" &&
                    (
                        sa.StartTime.AddMinutes(sa.AssessmentConfiguration.DurationMinutes) <= now ||
                        sa.AssessmentConfiguration.EndDateTime <= now
                    )
                )
                .ToListAsync();

            if (!expiredAttempts.Any())
            {
                return Ok("No expired attempts found");
            }

            try
            {
                foreach (var attempt in expiredAttempts)
                {
                    // Mark the attempt as timed out
                    attempt.EndTime = now;
                    attempt.Status = "TimedOut";
                    attempt.UpdatedBy = "System";
                    attempt.UpdatedAt = now;

                    // Auto-grade multiple choice questions
                    decimal totalScore = 0;
                    decimal totalPoints = 0;
                    bool allResponsesToBeGraded = true;

                    foreach (var response in attempt.Responses)
                    {
                        // Only auto-grade multiple choice and true/false questions
                        if (response.Question.QuestionType == "MultipleChoice" ||
                            response.Question.QuestionType == "TrueFalse")
                        {
                            // Skip if response is empty
                            if (string.IsNullOrEmpty(response.ResponseText))
                            {
                                response.IsCorrect = false;
                                response.Score = 0;
                                response.IsGraded = true;
                            }
                            else
                            {
                                try
                                {
                                    // Check if selected option(s) match the correct option(s)
                                    var selectedOptions = response.ResponseText.Split(',').Select(int.Parse).ToList();

                                    // Get correct options for this question
                                    var correctOptions = await _context.QuestionOptions
                                        .Where(qo => qo.QuestionId == response.QuestionId && qo.IsCorrect)
                                        .Select(qo => qo.Id)
                                        .ToListAsync();

                                    // For single-select questions
                                    if (selectedOptions.Count == 1 && correctOptions.Count == 1)
                                    {
                                        response.IsCorrect = selectedOptions[0] == correctOptions[0];
                                        response.Score = (response.IsCorrect ?? false) ? response.Question.Points : 0;

                                    }
                                    // For multi-select questions, require exact match
                                    else
                                    {
                                        var isFullyCorrect = selectedOptions.Count == correctOptions.Count &&
                                                          !selectedOptions.Except(correctOptions).Any();

                                        response.IsCorrect = isFullyCorrect;
                                        response.Score = isFullyCorrect ? response.Question.Points : 0;
                                    }

                                    response.IsGraded = true;
                                }
                                catch
                                {
                                    // Handle any parsing errors
                                    response.IsCorrect = false;
                                    response.Score = 0;
                                    response.IsGraded = true;
                                }
                            }
                        }
                        else
                        {
                            // Non-auto-gradable questions need manual grading
                            allResponsesToBeGraded = false;
                        }

                        // Add to totals if graded
                        if (response.IsGraded && response.Score.HasValue)
                        {
                            totalScore += response.Score.Value;
                        }

                        totalPoints += response.Question.Points;
                    }

                    // Calculate final score and pass/fail status if all questions are graded
                    if (allResponsesToBeGraded)
                    {
                        attempt.TotalScore = totalScore;

                        // Calculate percentage
                        if (totalPoints > 0)
                        {
                            attempt.Percentage = (totalScore / totalPoints) * 100;

                            // Set pass/fail status based on assessment's pass mark
                            var assessment = await _context.Assessments
                                .FirstOrDefaultAsync(a => a.Id == attempt.AssessmentConfiguration.AssessmentId);

                            if (assessment != null)
                            {
                                attempt.Passed = attempt.Percentage >= assessment.PassMark;
                            }
                        }
                        else
                        {
                            attempt.Percentage = 0;
                            attempt.Passed = false;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Ok($"Auto-submitted {expiredAttempts.Count} expired attempts");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

    // Model for starting a new attempt
    public class StartAttemptModel
    {
        public int AssessmentConfigurationId { get; set; }
    }
}