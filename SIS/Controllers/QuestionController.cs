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
    public class QuestionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QuestionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Question
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Question>>> GetQuestions()
        {
            return await _context.Questions
                .Include(q => q.QuestionGroup)
                .Include(q => q.Options)
                .ToListAsync();
        }

        // GET: api/Question/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Question>> GetQuestion(int id)
        {
            var question = await _context.Questions
                .Include(q => q.QuestionGroup)
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            return question;
        }

        // GET: api/Question/ByGroup/5
        [HttpGet("ByGroup/{groupId}")]
        public async Task<ActionResult<IEnumerable<Question>>> GetQuestionsByGroup(int groupId)
        {
            var questionGroup = await _context.QuestionGroups.FindAsync(groupId);

            if (questionGroup == null)
            {
                return NotFound("Question group not found");
            }

            return await _context.Questions
                .Where(q => q.QuestionGroupId == groupId)
                .Include(q => q.Options)
                .ToListAsync();
        }

        // POST: api/Question
        [HttpPost]
        public async Task<ActionResult<Question>> CreateQuestion(Question question)
        {
            try
            {
                // Verify question group exists
                var questionGroup = await _context.QuestionGroups.FindAsync(question.QuestionGroupId);
                if (questionGroup == null)
                {
                    return BadRequest("Invalid question group ID");
                }

                // Validate question type
                if (!IsValidQuestionType(question.QuestionType))
                {
                    return BadRequest("Invalid question type. Allowed types are: MultipleChoice, ShortAnswer, LongText, TrueFalse");
                }

                // Set audit information
                question.CreatedBy = User.Identity.Name;
                question.CreatedAt = DateTime.Now;
                question.IsActive = true;

                _context.Questions.Add(question);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetQuestion), new { id = question.Id }, question);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/Question/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuestion(int id, Question question)
        {
            if (id != question.Id)
            {
                return BadRequest("Mismatched ID");
            }

            // Retrieve the existing question
            var existingQuestion = await _context.Questions.FindAsync(id);
            if (existingQuestion == null)
            {
                return NotFound();
            }

            try
            {
                // Verify question group exists
                var questionGroup = await _context.QuestionGroups.FindAsync(question.QuestionGroupId);
                if (questionGroup == null)
                {
                    return BadRequest("Invalid question group ID");
                }

                // Validate question type
                if (!IsValidQuestionType(question.QuestionType))
                {
                    return BadRequest("Invalid question type. Allowed types are: MultipleChoice, ShortAnswer, LongText, TrueFalse");
                }

                // Update properties
                existingQuestion.QuestionGroupId = question.QuestionGroupId;
                existingQuestion.QuestionText = question.QuestionText;
                existingQuestion.QuestionType = question.QuestionType;
                existingQuestion.Points = question.Points;
                existingQuestion.AdditionalInfo = question.AdditionalInfo;
                existingQuestion.IsActive = question.IsActive;

                // Set audit information
                existingQuestion.UpdatedBy = User.Identity.Name;
                existingQuestion.UpdatedAt = DateTime.Now;

                _context.Entry(existingQuestion).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!QuestionExists(id))
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

        // DELETE: api/Question/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            // Check if this question is being used in any student responses
            var isUsedInStudentResponse = await _context.StudentResponses
                .AnyAsync(sr => sr.QuestionId == id);

            if (isUsedInStudentResponse)
            {
                return BadRequest("Cannot delete question as it has been used in student responses");
            }

            try
            {
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private bool QuestionExists(int id)
        {
            return _context.Questions.Any(e => e.Id == id);
        }

        private bool IsValidQuestionType(string questionType)
        {
            string[] validTypes = { "MultipleChoice", "ShortAnswer", "LongText", "TrueFalse" };
            return validTypes.Contains(questionType);
        }
    }
}