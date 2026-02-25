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
    public class QuestionGroupController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QuestionGroupController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/QuestionGroup
        [HttpGet]
        public async Task<ActionResult<IEnumerable<QuestionGroup>>> GetQuestionGroups()
        {
            return await _context.QuestionGroups
                .Include(qg => qg.Course)
                .ToListAsync();
        }

        // GET: api/QuestionGroup/5
        [HttpGet("{id}")]
        public async Task<ActionResult<QuestionGroup>> GetQuestionGroup(int id)
        {
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Course)
                .Include(qg => qg.Questions)
                .FirstOrDefaultAsync(qg => qg.Id == id);

            if (questionGroup == null)
            {
                return NotFound();
            }

            return questionGroup;
        }

        // GET: api/QuestionGroup/ByCourse/5
        [HttpGet("ByCourse/{courseId}")]
        public async Task<ActionResult<IEnumerable<QuestionGroup>>> GetQuestionGroupsByCourse(int courseId)
        {
            var course = await _context.Courses.FindAsync(courseId);

            if (course == null)
            {
                return NotFound("Course not found");
            }

            return await _context.QuestionGroups
                .Where(qg => qg.CourseId == courseId)
                .Include(qg => qg.Questions)
                .ToListAsync();
        }

        // POST: api/QuestionGroup
        [HttpPost]
        public async Task<ActionResult<QuestionGroup>> CreateQuestionGroup(QuestionGroup questionGroup)
        {
            try
            {
                // Verify course exists
                var course = await _context.Courses.FindAsync(questionGroup.CourseId);
                if (course == null)
                {
                    return BadRequest("Invalid course ID");
                }

                // Set audit information
                questionGroup.CreatedBy = User.Identity.Name;
                questionGroup.CreatedAt = DateTime.Now;

                _context.QuestionGroups.Add(questionGroup);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetQuestionGroup), new { id = questionGroup.Id }, questionGroup);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/QuestionGroup/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuestionGroup(int id, QuestionGroup questionGroup)
        {
            if (id != questionGroup.Id)
            {
                return BadRequest("Mismatched ID");
            }

            // Retrieve the existing question group
            var existingQuestionGroup = await _context.QuestionGroups.FindAsync(id);
            if (existingQuestionGroup == null)
            {
                return NotFound();
            }

            try
            {
                // Verify course exists
                var course = await _context.Courses.FindAsync(questionGroup.CourseId);
                if (course == null)
                {
                    return BadRequest("Invalid course ID");
                }

                // Update properties
                existingQuestionGroup.Name = questionGroup.Name;
                existingQuestionGroup.CourseId = questionGroup.CourseId;
                existingQuestionGroup.Topics = questionGroup.Topics;
                existingQuestionGroup.Description = questionGroup.Description;

                // Set audit information
                existingQuestionGroup.UpdatedBy = User.Identity.Name;
                existingQuestionGroup.UpdatedAt = DateTime.Now;

                _context.Entry(existingQuestionGroup).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!QuestionGroupExists(id))
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
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // DELETE: api/QuestionGroup/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestionGroup(int id)
        {
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Questions)
                .FirstOrDefaultAsync(qg => qg.Id == id);

            if (questionGroup == null)
            {
                return NotFound();
            }

            // Check if this question group is being used in any assessment
            var isUsedInAssessment = await _context.AssessmentQuestionGroups
                .AnyAsync(aqg => aqg.QuestionGroupId == id);

            if (isUsedInAssessment)
            {
                return BadRequest("Cannot delete question group as it is being used in one or more assessments");
            }

            try
            {
                _context.QuestionGroups.Remove(questionGroup);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private bool QuestionGroupExists(int id)
        {
            return _context.QuestionGroups.Any(e => e.Id == id);
        }
    }
}