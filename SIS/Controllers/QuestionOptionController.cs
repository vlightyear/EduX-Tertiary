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
    public class QuestionOptionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QuestionOptionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/QuestionOption
        [HttpGet]
        public async Task<ActionResult<IEnumerable<QuestionOption>>> GetQuestionOptions()
        {
            return await _context.QuestionOptions
                .Include(qo => qo.Question)
                .ToListAsync();
        }

        // GET: api/QuestionOption/5
        [HttpGet("{id}")]
        public async Task<ActionResult<QuestionOption>> GetQuestionOption(int id)
        {
            var questionOption = await _context.QuestionOptions
                .Include(qo => qo.Question)
                .FirstOrDefaultAsync(qo => qo.Id == id);

            if (questionOption == null)
            {
                return NotFound();
            }

            return questionOption;
        }

        // GET: api/QuestionOption/ByQuestion/5
        [HttpGet("ByQuestion/{questionId}")]
        public async Task<ActionResult<IEnumerable<QuestionOption>>> GetQuestionOptionsByQuestion(int questionId)
        {
            var question = await _context.Questions.FindAsync(questionId);

            if (question == null)
            {
                return NotFound("Question not found");
            }

            return await _context.QuestionOptions
                .Where(qo => qo.QuestionId == questionId)
                .ToListAsync();
        }

        // POST: api/QuestionOption
        [HttpPost]
        public async Task<ActionResult<QuestionOption>> CreateQuestionOption(QuestionOption questionOption)
        {
            try
            {
                // Verify question exists
                var question = await _context.Questions.FindAsync(questionOption.QuestionId);
                if (question == null)
                {
                    return BadRequest("Invalid question ID");
                }

                // Verify that this is a multiple choice question
                if (question.QuestionType != "MultipleChoice" && question.QuestionType != "TrueFalse")
                {
                    return BadRequest("Options can only be added to Multiple Choice or True/False questions");
                }

                // Set audit information
                questionOption.CreatedBy = User.Identity.Name;
                questionOption.CreatedAt = DateTime.Now;

                _context.QuestionOptions.Add(questionOption);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetQuestionOption), new { id = questionOption.Id }, questionOption);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // POST: api/QuestionOption/Batch
        [HttpPost("Batch")]
        public async Task<ActionResult<IEnumerable<QuestionOption>>> CreateQuestionOptionsBatch(BatchOptionCreateModel model)
        {
            try
            {
                // Verify question exists
                var question = await _context.Questions.FindAsync(model.QuestionId);
                if (question == null)
                {
                    return BadRequest("Invalid question ID");
                }

                // Verify that this is a multiple choice question
                if (question.QuestionType != "MultipleChoice" && question.QuestionType != "TrueFalse")
                {
                    return BadRequest("Options can only be added to Multiple Choice or True/False questions");
                }

                // Ensure there's at least one correct answer
                if (!model.Options.Any(o => o.IsCorrect))
                {
                    return BadRequest("At least one option must be marked as correct");
                }

                // For True/False questions, there should be exactly 2 options
                if (question.QuestionType == "TrueFalse" && model.Options.Count != 2)
                {
                    return BadRequest("True/False questions must have exactly 2 options");
                }

                // Create the options
                var options = new List<QuestionOption>();
                foreach (var option in model.Options)
                {
                    var newOption = new QuestionOption
                    {
                        QuestionId = model.QuestionId,
                        OptionText = option.OptionText,
                        IsCorrect = option.IsCorrect,
                        CreatedBy = User.Identity.Name,
                        CreatedAt = DateTime.Now
                    };

                    options.Add(newOption);
                    _context.QuestionOptions.Add(newOption);
                }

                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetQuestionOptionsByQuestion), new { questionId = model.QuestionId }, options);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // PUT: api/QuestionOption/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuestionOption(int id, QuestionOption questionOption)
        {
            if (id != questionOption.Id)
            {
                return BadRequest("Mismatched ID");
            }

            // Retrieve the existing question option
            var existingQuestionOption = await _context.QuestionOptions.FindAsync(id);
            if (existingQuestionOption == null)
            {
                return NotFound();
            }

            try
            {
                // Verify question exists
                var question = await _context.Questions.FindAsync(questionOption.QuestionId);
                if (question == null)
                {
                    return BadRequest("Invalid question ID");
                }

                // Update properties
                existingQuestionOption.QuestionId = questionOption.QuestionId;
                existingQuestionOption.OptionText = questionOption.OptionText;
                existingQuestionOption.IsCorrect = questionOption.IsCorrect;

                // Set audit information
                existingQuestionOption.UpdatedBy = User.Identity.Name;
                existingQuestionOption.UpdatedAt = DateTime.Now;

                _context.Entry(existingQuestionOption).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!QuestionOptionExists(id))
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

        // DELETE: api/QuestionOption/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestionOption(int id)
        {
            var questionOption = await _context.QuestionOptions.FindAsync(id);
            if (questionOption == null)
            {
                return NotFound();
            }

            try
            {
                // Get the question for this option
                var question = await _context.Questions.FindAsync(questionOption.QuestionId);

                // Get count of options for this question
                var optionCount = await _context.QuestionOptions
                    .Where(qo => qo.QuestionId == questionOption.QuestionId)
                    .CountAsync();

                // For TrueFalse questions, we must maintain exactly 2 options
                if (question.QuestionType == "TrueFalse" && optionCount <= 2)
                {
                    return BadRequest("Cannot delete option from True/False question. There must be exactly 2 options.");
                }

                // For MultipleChoice, we should have at least 2 options
                if (question.QuestionType == "MultipleChoice" && optionCount <= 2)
                {
                    return BadRequest("Multiple choice questions must have at least 2 options");
                }

                // Check if this is the only correct option
                var isOnlyCorrectOption = questionOption.IsCorrect &&
                    await _context.QuestionOptions
                        .Where(qo => qo.QuestionId == questionOption.QuestionId && qo.IsCorrect && qo.Id != id)
                        .CountAsync() == 0;

                if (isOnlyCorrectOption)
                {
                    return BadRequest("Cannot delete the only correct option for this question");
                }

                _context.QuestionOptions.Remove(questionOption);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private bool QuestionOptionExists(int id)
        {
            return _context.QuestionOptions.Any(e => e.Id == id);
        }
    }

    // Class used for batch creation of options
    public class BatchOptionCreateModel
    {
        public int QuestionId { get; set; }
        public List<OptionModel> Options { get; set; } = new List<OptionModel>();
    }

    public class OptionModel
    {
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
    }
}