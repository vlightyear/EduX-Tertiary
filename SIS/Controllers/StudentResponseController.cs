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
    public class StudentResponseController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StudentResponseController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/StudentResponse
        [HttpGet]
        [Authorize(Roles = "Admin,Instructor")] // Restrict to admin and instructors
        public async Task<ActionResult<IEnumerable<StudentResponse>>> GetStudentResponses()
        {
            return await _context.StudentResponses
                .Include(sr => sr.Question)
                .Include(sr => sr.StudentAttempt)
                .ToListAsync();
        }

        // GET: api/StudentResponse/5
        [HttpGet("{id}")]
        public async Task<ActionResult<StudentResponse>> GetStudentResponse(int id)
        {
            var studentResponse = await _context.StudentResponses
                .Include(sr => sr.Question)
                    .ThenInclude(q => q.Options)
                .Include(sr => sr.StudentAttempt)
                .FirstOrDefaultAsync(sr => sr.Id == id);

            if (studentResponse == null)
            {
                return NotFound();
            }

            // Security check - only allow access to instructors, admins, or the student who owns the response
            string currentUserId = User.Identity.Name; // Assuming Identity.Name contains user ID
            bool isAdmin = User.IsInRole("Admin");
            bool isInstructor = User.IsInRole("Instructor");

            if (!isAdmin && !isInstructor && studentResponse.StudentAttempt.StudentId != currentUserId)
            {
                return Forbid();
            }

            return studentResponse;
        }

        // GET: api/StudentResponse/ByAttempt/5
        [HttpGet("ByAttempt/{attemptId}")]
        public async Task<ActionResult<IEnumerable<StudentResponse>>> GetStudentResponsesByAttempt(int attemptId)
        {
            // First check if the attempt exists
            var attempt = await _context.StudentAttempts.FindAsync(attemptId);
            if (attempt == null)
            {
                return NotFound("Attempt not found");
            }

            // Security check - only allow access to instructors, admins, or the student who owns the attempt
            string currentUserId = User.Identity.Name; // Assuming Identity.Name contains user ID
            bool isAdmin = User.IsInRole("Admin");
            bool isInstructor = User.IsInRole("Instructor");

            if (!isAdmin && !isInstructor && attempt.StudentId != currentUserId)
            {
                return Forbid();
            }

            var responses = await _context.StudentResponses
                .Where(sr => sr.StudentAttemptId == attemptId)
                .Include(sr => sr.Question)
                    .ThenInclude(q => q.Options)
                .OrderBy(sr => sr.Id) // Order by ID for consistent response order
                .ToListAsync();

            return responses;
        }

        // POST: api/StudentResponse
        [HttpPost]
        public async Task<ActionResult<StudentResponse>> CreateStudentResponse(StudentResponse studentResponse)
        {
            // Verify the attempt exists
            var attempt = await _context.StudentAttempts
                .Include(sa => sa.AssessmentConfiguration)
                .FirstOrDefaultAsync(sa => sa.Id == studentResponse.StudentAttemptId);

            if (attempt == null)
            {
                return BadRequest("Invalid attempt ID");
            }

            // Security check - only the student who owns the attempt can create responses
            string currentUserId = User.Identity.Name; // Assuming Identity.Name contains user ID
            if (attempt.StudentId != currentUserId)
            {
                return Forbid();
            }

            // Check if the attempt is still in progress
            if (attempt.Status != "InProgress")
            {
                return BadRequest("Cannot save response for a completed attempt");
            }

            // Verify the question exists and belongs to the assessment
            var question = await _context.Questions.FindAsync(studentResponse.QuestionId);
            if (question == null)
            {
                return BadRequest("Invalid question ID");
            }

            // Check if a response already exists for this question in this attempt
            var existingResponse = await _context.StudentResponses
                .FirstOrDefaultAsync(sr =>
                    sr.StudentAttemptId == studentResponse.StudentAttemptId &&
                    sr.QuestionId == studentResponse.QuestionId);

            if (existingResponse != null)
            {
                return BadRequest("A response for this question already exists. Use PUT to update it.");
            }

            try
            {
                // Set audit information
                studentResponse.CreatedBy = currentUserId;
                studentResponse.CreatedAt = DateTime.Now;
                studentResponse.IsGraded = false;

                _context.StudentResponses.Add(studentResponse);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetStudentResponse), new { id = studentResponse.Id }, studentResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/StudentResponse/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStudentResponse(int id, StudentResponseUpdateModel model)
        {
            var studentResponse = await _context.StudentResponses
                .Include(sr => sr.StudentAttempt)
                .FirstOrDefaultAsync(sr => sr.Id == id);

            if (studentResponse == null)
            {
                return NotFound();
            }

            // Security check - only the student who owns the response can update it
            string currentUserId = User.Identity.Name; // Assuming Identity.Name contains user ID
            if (studentResponse.StudentAttempt.StudentId != currentUserId)
            {
                return Forbid();
            }

            // Check if the attempt is still in progress
            if (studentResponse.StudentAttempt.Status != "InProgress")
            {
                return BadRequest("Cannot update response for a completed attempt");
            }

            try
            {
                // Update properties
                studentResponse.ResponseText = model.ResponseText;

                // Reset grading if response is changed
                if (studentResponse.IsGraded)
                {
                    studentResponse.IsGraded = false;
                    studentResponse.IsCorrect = null;
                    studentResponse.Score = null;
                }

                // Set audit information
                studentResponse.UpdatedBy = currentUserId;
                studentResponse.UpdatedAt = DateTime.Now;

                _context.Entry(studentResponse).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/StudentResponse/5/Grade
        [HttpPut("{id}/Grade")]
        [Authorize(Roles = "Admin,Instructor")] // Restrict to admin and instructors
        public async Task<IActionResult> GradeStudentResponse(int id, GradeResponseModel model)
        {
            var studentResponse = await _context.StudentResponses
                .Include(sr => sr.StudentAttempt)
                .Include(sr => sr.Question)
                .FirstOrDefaultAsync(sr => sr.Id == id);

            if (studentResponse == null)
            {
                return NotFound();
            }

            // Check if the attempt has been submitted
            if (studentResponse.StudentAttempt.Status == "InProgress")
            {
                return BadRequest("Cannot grade a response for an in-progress attempt");
            }

            try
            {
                // Validate the score is within range
                if (model.Score < 0 || model.Score > studentResponse.Question.Points)
                {
                    return BadRequest($"Score must be between 0 and {studentResponse.Question.Points}");
                }

                // Update grading information
                studentResponse.Score = model.Score;
                studentResponse.IsCorrect = model.Score >= studentResponse.Question.Points;
                studentResponse.FeedbackFromInstructor = model.Feedback;
                studentResponse.IsGraded = true;

                // Set audit information
                studentResponse.UpdatedBy = User.Identity.Name;
                studentResponse.UpdatedAt = DateTime.Now;

                _context.Entry(studentResponse).State = EntityState.Modified;

                // Update attempt total score and percentage
                await UpdateAttemptScores(studentResponse.StudentAttemptId);

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/StudentResponse/BatchGrade
        [HttpPut("BatchGrade")]
        [Authorize(Roles = "Admin,Instructor")] // Restrict to admin and instructors
        public async Task<IActionResult> BatchGradeResponses(BatchGradeModel model)
        {
            try
            {
                // Verify all responses exist and belong to completed attempts
                foreach (var gradeItem in model.Responses)
                {
                    var response = await _context.StudentResponses
                        .Include(sr => sr.StudentAttempt)
                        .Include(sr => sr.Question)
                        .FirstOrDefaultAsync(sr => sr.Id == gradeItem.ResponseId);

                    if (response == null)
                    {
                        return BadRequest($"Response with ID {gradeItem.ResponseId} not found");
                    }

                    if (response.StudentAttempt.Status == "InProgress")
                    {
                        return BadRequest($"Cannot grade response {gradeItem.ResponseId} for an in-progress attempt");
                    }

                    // Validate the score is within range
                    if (gradeItem.Score < 0 || gradeItem.Score > response.Question.Points)
                    {
                        return BadRequest($"Score for response {gradeItem.ResponseId} must be between 0 and {response.Question.Points}");
                    }
                }

                // Process all grades
                var affectedAttemptIds = new HashSet<int>();

                foreach (var gradeItem in model.Responses)
                {
                    var response = await _context.StudentResponses
                        .Include(sr => sr.Question)
                        .FirstOrDefaultAsync(sr => sr.Id == gradeItem.ResponseId);

                    // Update grading information
                    response.Score = gradeItem.Score;
                    response.IsCorrect = gradeItem.Score >= response.Question.Points;
                    response.FeedbackFromInstructor = gradeItem.Feedback;
                    response.IsGraded = true;

                    // Set audit information
                    response.UpdatedBy = User.Identity.Name;
                    response.UpdatedAt = DateTime.Now;

                    _context.Entry(response).State = EntityState.Modified;

                    // Track which attempts need score updates
                    affectedAttemptIds.Add(response.StudentAttemptId);
                }

                // Update totals for all affected attempts
                foreach (var attemptId in affectedAttemptIds)
                {
                    await UpdateAttemptScores(attemptId);
                }

                await _context.SaveChangesAsync();

                return Ok($"Successfully graded {model.Responses.Count} responses");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Helper method to update attempt scores
        private async Task UpdateAttemptScores(int attemptId)
        {
            var attempt = await _context.StudentAttempts
                .Include(sa => sa.Responses)
                    .ThenInclude(r => r.Question)
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Assessment)
                .FirstOrDefaultAsync(sa => sa.Id == attemptId);

            if (attempt == null)
            {
                return;
            }

            // Calculate total score and points
            decimal totalScore = 0;
            decimal totalPoints = 0;

            foreach (var response in attempt.Responses)
            {
                if (response.IsGraded && response.Score.HasValue)
                {
                    totalScore += response.Score.Value;
                }

                totalPoints += response.Question.Points;
            }

            // Update attempt scores
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

            _context.Entry(attempt).State = EntityState.Modified;
        }
    }

    // Models for updating and grading responses
    public class StudentResponseUpdateModel
    {
        public string ResponseText { get; set; }
    }

    public class GradeResponseModel
    {
        public decimal Score { get; set; }
        public string Feedback { get; set; }
    }

    public class BatchGradeModel
    {
        public List<BatchGradeItem> Responses { get; set; } = new List<BatchGradeItem>();
    }

    public class BatchGradeItem
    {
        public int ResponseId { get; set; }
        public decimal Score { get; set; }
        public string Feedback { get; set; }
    }
}