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
    [Authorize] // Basic authorization, you may want to customize this
    public class AssessmentConfigurationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AssessmentConfigurationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/AssessmentConfiguration
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AssessmentConfiguration>>> GetAssessmentConfigurations()
        {
            return await _context.AssessmentConfigurations
                .Include(ac => ac.Assessment)
                .Include(ac => ac.Course)
                .ToListAsync();
        }

        // GET: api/AssessmentConfiguration/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AssessmentConfiguration>> GetAssessmentConfiguration(int id)
        {
            var assessmentConfiguration = await _context.AssessmentConfigurations
                .Include(ac => ac.Assessment)
                .Include(ac => ac.Course)
                .Include(ac => ac.QuestionGroups)
                    .ThenInclude(qg => qg.QuestionGroup)
                .FirstOrDefaultAsync(ac => ac.Id == id);

            if (assessmentConfiguration == null)
            {
                return NotFound();
            }

            return assessmentConfiguration;
        }

        // GET: api/AssessmentConfiguration/ByCourse/5
        [HttpGet("ByCourse/{courseId}")]
        public async Task<ActionResult<IEnumerable<AssessmentConfiguration>>> GetAssessmentConfigurationsByCourse(int courseId)
        {
            var course = await _context.Courses.FindAsync(courseId);

            if (course == null)
            {
                return NotFound("Course not found");
            }

            return await _context.AssessmentConfigurations
                .Where(ac => ac.CourseId == courseId)
                .Include(ac => ac.Assessment)
                .ToListAsync();
        }

        // GET: api/AssessmentConfiguration/Active
        [HttpGet("Active")]
        public async Task<ActionResult<IEnumerable<AssessmentConfiguration>>> GetActiveAssessmentConfigurations()
        {
            var now = DateTime.Now;
            return await _context.AssessmentConfigurations
                .Where(ac => ac.IsPublished && ac.StartDateTime <= now && ac.EndDateTime >= now)
                .Include(ac => ac.Assessment)
                .Include(ac => ac.Course)
                .ToListAsync();
        }

        // GET: api/AssessmentConfiguration/ActiveForStudent/{studentId}
        [HttpGet("ActiveForStudent/{studentId}")]
        public async Task<ActionResult<IEnumerable<AssessmentConfiguration>>> GetActiveAssessmentConfigurationsForStudent(string studentId)
        {
            // Get current date and time
            var now = DateTime.Now;

            // Get all active published assessments
            var activeConfigurations = await _context.AssessmentConfigurations
                .Where(ac => ac.IsPublished && ac.StartDateTime <= now && ac.EndDateTime >= now)
                .Include(ac => ac.Assessment)
                .Include(ac => ac.Course)
                .ToListAsync();

            // Get student course enrollments (assuming you have a StudentCourse table)
            // Modify this based on your actual enrollment structure
            var studentCourses = await _context.StudentCourses
                .Where(sc => sc.StudentId == int.Parse(studentId))
                .Select(sc => sc.CourseId)
                .ToListAsync();

            // Filter configurations to only those for courses the student is enrolled in
            var studentConfigurations = activeConfigurations
                .Where(ac => studentCourses.Contains(ac.CourseId))
                .ToList();

            return studentConfigurations;
        }

        // POST: api/AssessmentConfiguration
        [HttpPost]
        public async Task<ActionResult<AssessmentConfiguration>> CreateAssessmentConfiguration(AssessmentConfigurationCreateModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Verify assessment exists
                var assessment = await _context.Assessments.FindAsync(model.AssessmentId);
                if (assessment == null)
                {
                    return BadRequest("Invalid assessment ID");
                }

                // Verify course exists
                var course = await _context.Courses.FindAsync(model.CourseId);
                if (course == null)
                {
                    return BadRequest("Invalid course ID");
                }

                // Validate dates
                if (model.StartDateTime >= model.EndDateTime)
                {
                    return BadRequest("Start date/time must be before end date/time");
                }

                // Create the assessment configuration
                var assessmentConfiguration = new AssessmentConfiguration
                {
                    AssessmentId = model.AssessmentId,
                    CourseId = model.CourseId,
                    //AcademicYear = model.AcademicYear,
                    //ModeOfStudy = model.ModeOfStudy,
                    StartDateTime = model.StartDateTime,
                    EndDateTime = model.EndDateTime,
                    DurationMinutes = model.DurationMinutes,
                    RandomizeQuestions = model.RandomizeQuestions,
                    PreventTabSwitching = model.PreventTabSwitching,
                    ShowResults = model.ShowResults,
                    IsPublished = false, // Default to unpublished
                    CreatedBy = User.Identity.Name,
                    CreatedAt = DateTime.Now
                };

                _context.AssessmentConfigurations.Add(assessmentConfiguration);
                await _context.SaveChangesAsync();

                // Add question groups if provided
                if (model.QuestionGroups != null && model.QuestionGroups.Any())
                {
                    foreach (var groupModel in model.QuestionGroups)
                    {
                        // Verify question group exists
                        var questionGroup = await _context.QuestionGroups.FindAsync(groupModel.QuestionGroupId);
                        if (questionGroup == null)
                        {
                            // Rollback and return error
                            await transaction.RollbackAsync();
                            return BadRequest($"Invalid question group ID: {groupModel.QuestionGroupId}");
                        }

                        // Ensure the question group belongs to the same course
                        if (questionGroup.CourseId != model.CourseId)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest($"Question group {groupModel.QuestionGroupId} does not belong to the selected course");
                        }

                        var assessmentQuestionGroup = new AssessmentQuestionGroup
                        {
                            AssessmentConfigurationId = assessmentConfiguration.Id,
                            QuestionGroupId = groupModel.QuestionGroupId,
                            NumberOfQuestionsToUse = groupModel.NumberOfQuestionsToUse
                        };

                        _context.AssessmentQuestionGroups.Add(assessmentQuestionGroup);
                    }

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetAssessmentConfiguration), new { id = assessmentConfiguration.Id }, assessmentConfiguration);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/AssessmentConfiguration/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAssessmentConfiguration(int id, AssessmentConfigurationUpdateModel model)
        {
            if (id != model.Id)
            {
                return BadRequest("Mismatched ID");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Retrieve the existing assessment configuration
                var existingConfig = await _context.AssessmentConfigurations
                    .Include(ac => ac.QuestionGroups)
                    .FirstOrDefaultAsync(ac => ac.Id == id);

                if (existingConfig == null)
                {
                    return NotFound();
                }

                // Check if there are already student attempts for this assessment
                var hasAttempts = await _context.StudentAttempts
                    .AnyAsync(sa => sa.AssessmentConfigurationId == id);

                if (hasAttempts)
                {
                    // If students have already started taking the assessment,
                    // only allow updating certain properties
                    existingConfig.ShowResults = model.ShowResults;
                    existingConfig.EndDateTime = model.EndDateTime; // Allow extending end time

                    // Update audit information
                    existingConfig.UpdatedBy = User.Identity.Name;
                    existingConfig.UpdatedAt = DateTime.Now;

                    _context.Entry(existingConfig).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return NoContent();
                }

                // If no attempts yet, allow full update

                // Verify assessment exists
                var assessment = await _context.Assessments.FindAsync(model.AssessmentId);
                if (assessment == null)
                {
                    return BadRequest("Invalid assessment ID");
                }

                // Verify course exists
                var course = await _context.Courses.FindAsync(model.CourseId);
                if (course == null)
                {
                    return BadRequest("Invalid course ID");
                }

                // Validate dates
                if (model.StartDateTime >= model.EndDateTime)
                {
                    return BadRequest("Start date/time must be before end date/time");
                }

                // Update properties
                existingConfig.AssessmentId = model.AssessmentId;
                existingConfig.CourseId = model.CourseId;
                //existingConfig.AcademicYear = model.AcademicYear;
                //existingConfig.ModeOfStudy = model.ModeOfStudy;
                existingConfig.StartDateTime = model.StartDateTime;
                existingConfig.EndDateTime = model.EndDateTime;
                existingConfig.DurationMinutes = model.DurationMinutes;
                existingConfig.RandomizeQuestions = model.RandomizeQuestions;
                existingConfig.PreventTabSwitching = model.PreventTabSwitching;
                existingConfig.ShowResults = model.ShowResults;

                // Update audit information
                existingConfig.UpdatedBy = User.Identity.Name;
                existingConfig.UpdatedAt = DateTime.Now;

                _context.Entry(existingConfig).State = EntityState.Modified;

                // Handle question groups update if provided
                if (model.QuestionGroups != null)
                {
                    // Remove existing question groups
                    _context.AssessmentQuestionGroups.RemoveRange(existingConfig.QuestionGroups);
                    await _context.SaveChangesAsync();

                    // Add updated question groups
                    foreach (var groupModel in model.QuestionGroups)
                    {
                        // Verify question group exists
                        var questionGroup = await _context.QuestionGroups.FindAsync(groupModel.QuestionGroupId);
                        if (questionGroup == null)
                        {
                            // Rollback and return error
                            await transaction.RollbackAsync();
                            return BadRequest($"Invalid question group ID: {groupModel.QuestionGroupId}");
                        }

                        // Ensure the question group belongs to the same course
                        if (questionGroup.CourseId != model.CourseId)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest($"Question group {groupModel.QuestionGroupId} does not belong to the selected course");
                        }

                        var assessmentQuestionGroup = new AssessmentQuestionGroup
                        {
                            AssessmentConfigurationId = existingConfig.Id,
                            QuestionGroupId = groupModel.QuestionGroupId,
                            NumberOfQuestionsToUse = groupModel.NumberOfQuestionsToUse
                        };

                        _context.AssessmentQuestionGroups.Add(assessmentQuestionGroup);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();

                if (!AssessmentConfigurationExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/AssessmentConfiguration/5/Publish
        [HttpPut("{id}/Publish")]
        public async Task<IActionResult> PublishAssessmentConfiguration(int id)
        {
            var assessmentConfiguration = await _context.AssessmentConfigurations
                .Include(ac => ac.QuestionGroups)
                .FirstOrDefaultAsync(ac => ac.Id == id);

            if (assessmentConfiguration == null)
            {
                return NotFound();
            }

            try
            {
                // Validate that the assessment configuration has question groups
                if (!assessmentConfiguration.QuestionGroups.Any())
                {
                    return BadRequest("Cannot publish assessment without question groups");
                }

                // Set to published
                assessmentConfiguration.IsPublished = true;

                // Update audit information
                assessmentConfiguration.UpdatedBy = User.Identity.Name;
                assessmentConfiguration.UpdatedAt = DateTime.Now;

                _context.Entry(assessmentConfiguration).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/AssessmentConfiguration/5/Unpublish
        [HttpPut("{id}/Unpublish")]
        public async Task<IActionResult> UnpublishAssessmentConfiguration(int id)
        {
            var assessmentConfiguration = await _context.AssessmentConfigurations.FindAsync(id);
            if (assessmentConfiguration == null)
            {
                return NotFound();
            }

            try
            {
                // Check if there are already student attempts for this assessment
                var hasAttempts = await _context.StudentAttempts
                    .AnyAsync(sa => sa.AssessmentConfigurationId == id);

                if (hasAttempts)
                {
                    return BadRequest("Cannot unpublish assessment that already has student attempts");
                }

                // Set to unpublished
                assessmentConfiguration.IsPublished = false;

                // Update audit information
                assessmentConfiguration.UpdatedBy = User.Identity.Name;
                assessmentConfiguration.UpdatedAt = DateTime.Now;

                _context.Entry(assessmentConfiguration).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // DELETE: api/AssessmentConfiguration/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAssessmentConfiguration(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var assessmentConfiguration = await _context.AssessmentConfigurations
                    .Include(ac => ac.QuestionGroups)
                    .FirstOrDefaultAsync(ac => ac.Id == id);

                if (assessmentConfiguration == null)
                {
                    return NotFound();
                }

                // Check if there are already student attempts for this assessment
                var hasAttempts = await _context.StudentAttempts
                    .AnyAsync(sa => sa.AssessmentConfigurationId == id);

                if (hasAttempts)
                {
                    return BadRequest("Cannot delete assessment that already has student attempts");
                }

                // Remove question groups first
                _context.AssessmentQuestionGroups.RemoveRange(assessmentConfiguration.QuestionGroups);

                // Then remove the configuration
                _context.AssessmentConfigurations.Remove(assessmentConfiguration);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private bool AssessmentConfigurationExists(int id)
        {
            return _context.AssessmentConfigurations.Any(e => e.Id == id);
        }
    }

    // Models for creating and updating assessment configurations
    public class AssessmentConfigurationCreateModel
    {
        public int AssessmentId { get; set; }
        public int CourseId { get; set; }
        public string AcademicYear { get; set; }
        public string ModeOfStudy { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public int DurationMinutes { get; set; }
        public bool RandomizeQuestions { get; set; }
        public bool PreventTabSwitching { get; set; }
        public bool ShowResults { get; set; }
        public List<AssessmentQuestionGroupModel> QuestionGroups { get; set; } = new List<AssessmentQuestionGroupModel>();
    }

    public class AssessmentConfigurationUpdateModel : AssessmentConfigurationCreateModel
    {
        public int Id { get; set; }
    }

    public class AssessmentQuestionGroupModel
    {
        public int QuestionGroupId { get; set; }
        public int NumberOfQuestionsToUse { get; set; }
    }
}