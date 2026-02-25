using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Assessments;
using Ganss.Xss;
using System.Security.Claims;
using SIS.Extensions;
using SIS.Models.Import;
using SIS.Services.QuestionImport;

namespace SIS.Controllers
{
    
    public class AssessmentsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AssessmentsController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }
        // GET: Assessments/QuestionGroups
        public async Task<IActionResult> QuestionGroups()
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get current user's courses (where they are assigned as lecturer or instructor)
            var lecturerCourseIds = await _context.Courses
                .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                            c.InstructorId == currentUserId)
                .Select(c => c.Id)
                .ToListAsync();

            var questionGroups = await _context.QuestionGroups
                .Include(qg => qg.Course)
                .Include(qg => qg.Questions)
                .Where(qg => lecturerCourseIds.Contains(qg.CourseId))
                .ToListAsync();

            // Get question type distribution for lecturer's courses only
            var questionTypes = await _context.Questions
                .Where(q => lecturerCourseIds.Contains(q.QuestionGroup.CourseId))
                .GroupBy(q => q.QuestionType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.QuestionTypeDistribution = questionTypes;

            return View(questionGroups);
        }

        // GET: Assessments/QuestionGroupsByCourse/5
        public async Task<IActionResult> QuestionGroupsByCourse(int courseId)
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the course belongs to the current lecturer
            var course = await _context.Courses
                .Where(c => c.Id == courseId &&
                           (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                            c.InstructorId == currentUserId))
                .FirstOrDefaultAsync();

            if (course == null)
            {
                TempData["Error"] = "Course not found or you don't have access to this course";
                return RedirectToAction(nameof(QuestionGroups));
            }

            ViewBag.Course = course;

            var questionGroups = await _context.QuestionGroups
                .Where(qg => qg.CourseId == courseId)
                .Include(qg => qg.Questions)
                .ToListAsync();

            // Get question type distribution for this specific course
            var questionTypes = await _context.Questions
                .Where(q => q.QuestionGroup.CourseId == courseId)
                .GroupBy(q => q.QuestionType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.QuestionTypeDistribution = questionTypes;

            return View(questionGroups);
        }


        // GET: Assessments/QuestionGroupDetails/5
        public async Task<IActionResult> QuestionGroupDetails(int id)
        {
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Course)
                .Include(qg => qg.Questions)
                .FirstOrDefaultAsync(qg => qg.Id == id);

            if (questionGroup == null)
            {
                TempData["Error"] = "Question group not found";
                return RedirectToAction(nameof(QuestionGroups));
            }

            return View(questionGroup);
        }

        // GET: Assessments/CreateQuestionGroup
        public async Task<IActionResult> CreateQuestionGroup(int? courseId = null)
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get only courses where the current user is assigned as lecturer or instructor
            var lecturerCourses = await _context.Courses
                .Include(c => c.Programme)
                .Include(c => c.CourseLecturers)
                .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                            c.InstructorId == currentUserId)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            if (!lecturerCourses.Any())
            {
                TempData["Error"] = "You are not assigned to any courses";
                return RedirectToAction(nameof(QuestionGroups));
            }

            // If courseId is provided and is not 0, verify it belongs to the lecturer
            if (courseId.HasValue && courseId.Value > 0)
            {
                var course = lecturerCourses.FirstOrDefault(c => c.Id == courseId.Value);
                if (course != null)
                {
                    ViewBag.CourseId = courseId.Value;
                    ViewBag.CourseName = course.CourseName;
                }
                else
                {
                    TempData["Error"] = "You don't have access to the specified course";
                    return RedirectToAction(nameof(QuestionGroups));
                }
            }
            // If courseId is 0 or null, don't set ViewBag.CourseId

            ViewBag.Courses = lecturerCourses;
            return View();
        }


        // POST: Assessments/CreateQuestionGroup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestionGroup(QuestionGroup questionGroup)
        {
            try
            {
                // Get the current logged-in lecturer's ID
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized();
                }

                // Verify course exists and belongs to the current lecturer
                var course = await _context.Courses
                    .Where(c => c.Id == questionGroup.CourseId &&
                               (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                c.InstructorId == currentUserId))
                    .FirstOrDefaultAsync();

                if (course == null)
                {
                    ModelState.AddModelError("CourseId", "Invalid course selected or you don't have access to this course");
                    ViewBag.Courses = await _context.Courses
                        .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                    c.InstructorId == currentUserId)
                        .OrderBy(c => c.CourseName)
                        .ToListAsync();
                    return View(questionGroup);
                }

                // Set audit information
                questionGroup.CreatedBy = User.Identity.Name ?? "System";
                questionGroup.CreatedAt = DateTime.Now;

                _context.QuestionGroups.Add(questionGroup);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Question group created successfully";
                return RedirectToAction(nameof(QuestionGroups));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating question group: {ex.Message}");

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                ViewBag.Courses = await _context.Courses
                    .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                c.InstructorId == currentUserId)
                    .OrderBy(c => c.CourseName)
                    .ToListAsync();
                return View(questionGroup);
            }
        }

        // GET: Assessments/EditQuestionGroup/5
        public async Task<IActionResult> EditQuestionGroup(int id)
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Course)
                    .ThenInclude(c => c.CourseLecturers)
                .Where(qg => qg.Id == id &&
                            (qg.Course.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                             qg.Course.InstructorId == currentUserId))
                .FirstOrDefaultAsync();

            if (questionGroup == null)
            {
                TempData["Error"] = "Question group not found or you don't have access to it";
                return RedirectToAction(nameof(QuestionGroups));
            }

            // Get list of courses for the dropdown (only lecturer's courses)
            ViewBag.Courses = await _context.Courses
                .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                            c.InstructorId == currentUserId)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            return View(questionGroup);
        }

        // POST: Assessments/EditQuestionGroup/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestionGroup(int id, QuestionGroup questionGroup)
        {
            if (id != questionGroup.Id)
            {
                TempData["Error"] = "Invalid request";
                return RedirectToAction(nameof(QuestionGroups));
            }

            try
            {
                // Get the current logged-in lecturer's ID
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized();
                }

                // Retrieve the existing question group and verify access
                var existingQuestionGroup = await _context.QuestionGroups
                    .Include(qg => qg.Course)
                        .ThenInclude(c => c.CourseLecturers)
                    .Where(qg => qg.Id == id &&
                                (qg.Course.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                 qg.Course.InstructorId == currentUserId))
                    .FirstOrDefaultAsync();

                if (existingQuestionGroup == null)
                {
                    TempData["Error"] = "Question group not found or you don't have access to it";
                    return RedirectToAction(nameof(QuestionGroups));
                }

                // Verify the new course belongs to the lecturer
                var course = await _context.Courses
                    .Where(c => c.Id == questionGroup.CourseId &&
                               (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                c.InstructorId == currentUserId))
                    .FirstOrDefaultAsync();

                if (course == null)
                {
                    ModelState.AddModelError("CourseId", "Invalid course selected or you don't have access to this course");
                    ViewBag.Courses = await _context.Courses
                        .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                    c.InstructorId == currentUserId)
                        .OrderBy(c => c.CourseName)
                        .ToListAsync();
                    return View(questionGroup);
                }

                // Update properties
                existingQuestionGroup.Name = questionGroup.Name;
                existingQuestionGroup.CourseId = questionGroup.CourseId;
                existingQuestionGroup.Topics = questionGroup.Topics;
                existingQuestionGroup.Description = questionGroup.Description;

                // Set update information
                existingQuestionGroup.UpdatedBy = User.Identity.Name ?? "System";
                existingQuestionGroup.UpdatedAt = DateTime.Now;

                _context.Update(existingQuestionGroup);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Question group updated successfully";
                return RedirectToAction(nameof(QuestionGroups));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!QuestionGroupExists(id))
                {
                    TempData["Error"] = "Question group not found";
                    return RedirectToAction(nameof(QuestionGroups));
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating question group: {ex.Message}");

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                ViewBag.Courses = await _context.Courses
                    .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                c.InstructorId == currentUserId)
                    .OrderBy(c => c.CourseName)
                    .ToListAsync();
                return View(questionGroup);
            }
        }

        // GET: Assessments/DeleteQuestionGroup/5
        public async Task<IActionResult> DeleteQuestionGroup(int id)
        {
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Course)
                .FirstOrDefaultAsync(qg => qg.Id == id);

            if (questionGroup == null)
            {
                TempData["Error"] = "Question group not found";
                return RedirectToAction(nameof(QuestionGroups));
            }

            return View(questionGroup);
        }

        // POST: Assessments/DeleteQuestionGroupConfirmed/5
        [HttpPost, ActionName("DeleteQuestionGroupConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestionGroupConfirmed(int id)
        {
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Questions)
                .FirstOrDefaultAsync(qg => qg.Id == id);

            if (questionGroup == null)
            {
                TempData["Error"] = "Question group not found";
                return RedirectToAction(nameof(QuestionGroups));
            }

            // Check if this question group is being used in any assessment
            var isUsedInAssessment = await _context.AssessmentQuestionGroups
                .AnyAsync(aqg => aqg.QuestionGroupId == id);

            if (isUsedInAssessment)
            {
                TempData["Error"] = "Cannot delete question group as it is being used in one or more assessments";
                return RedirectToAction(nameof(QuestionGroupDetails), new { id });
            }

            try
            {
                _context.QuestionGroups.Remove(questionGroup);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Question group deleted successfully";
                return RedirectToAction(nameof(QuestionGroups));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting question group: {ex.Message}";
                return RedirectToAction(nameof(QuestionGroupDetails), new { id });
            }
        }

        private bool QuestionGroupExists(int id)
        {
            return _context.QuestionGroups.Any(e => e.Id == id);
        }


        // Region Question Management

        // GET: Assessments/Questions
        public async Task<IActionResult> Questions()
        {
            var questions = await _context.Questions
                .Include(q => q.QuestionGroup)
                .Include(q => q.Options)
                .ToListAsync();

            return View(questions);
        }

        // GET: Assessments/QuestionDetails/5
        public async Task<IActionResult> QuestionDetails(int id)
        {
            var question = await _context.Questions
                .Include(q => q.QuestionGroup)
                    .ThenInclude(qg => qg.Course)
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                TempData["Error"] = "Question not found";
                return RedirectToAction(nameof(Questions));
            }

            return View(question);
        }

        // GET: Assessments/QuestionsByGroup/5
        public async Task<IActionResult> QuestionsByGroup(int groupId)
        {
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Course)
                .Include(qg => qg.Questions)
                .FirstOrDefaultAsync(qg => qg.Id == groupId);

            if (questionGroup == null)
            {
                TempData["Error"] = "Question group not found";
                return RedirectToAction(nameof(QuestionGroups));
            }

            ViewBag.QuestionGroup = questionGroup;

            var questions = await _context.Questions
                .Where(q => q.QuestionGroupId == groupId)
                .Include(q => q.Options)
                .ToListAsync();

            return View(questions);
        }

        // GET: Assessments/CreateQuestion
        [HttpGet]
        public async Task<IActionResult> CreateQuestion(int? questionGroupId = null)
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            if (questionGroupId.HasValue)
            {
                var questionGroup = await _context.QuestionGroups
                    .Include(qg => qg.Course)
                        .ThenInclude(c => c.CourseLecturers)
                    .Where(qg => qg.Id == questionGroupId.Value &&
                                (qg.Course.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                 qg.Course.InstructorId == currentUserId))
                    .FirstOrDefaultAsync();

                if (questionGroup != null)
                {
                    ViewBag.QuestionGroup = questionGroup;
                }
                else
                {
                    TempData["Error"] = "Question group not found or you don't have access to it";
                    return RedirectToAction(nameof(QuestionGroups));
                }
            }

            // Get all question groups for lecturer's courses only
            ViewBag.QuestionGroups = await _context.QuestionGroups
                .Include(qg => qg.Course)
                    .ThenInclude(c => c.CourseLecturers)
                .Where(qg => qg.Course.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                             qg.Course.InstructorId == currentUserId)
                .OrderBy(qg => qg.Course.CourseName)
                .ThenBy(qg => qg.Name)
                .ToListAsync();

            var question = new Question
            {
                QuestionGroupId = questionGroupId ?? 0,
                IsActive = true,
                Points = 1,
                QuestionType = "MultipleChoice",
                CreatedBy = User.Identity.Name ?? "System",
                CreatedAt = DateTime.Now
            };

            return View(question);
        }

        // POST: Assessments/CreateQuestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestion(Question question, IFormFile questionImage,
            List<string> optionTexts = null, List<bool> optionCorrect = null, bool? trueFalseIsTrue = null)
        {
            // Remove validation errors for fields we'll set programmatically
            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("ImagePath");
            ModelState.Remove("ImageDescription");
            ModelState.Remove("ImageDisplayPosition");
            ModelState.Remove("QuestionGroup");
            ModelState.Remove("Options");

            try
            {
                // Verify question group exists
                var questionGroup = await _context.QuestionGroups.FindAsync(question.QuestionGroupId);
                if (questionGroup == null)
                {
                    ModelState.AddModelError("QuestionGroupId", "Invalid question group");
                    ViewBag.QuestionGroups = await _context.QuestionGroups
                        .Include(qg => qg.Course)
                        .OrderBy(qg => qg.Course.CourseName)
                        .ThenBy(qg => qg.Name)
                        .ToListAsync();
                    return View(question);
                }

                // Set audit information
                question.CreatedBy = User.Identity.Name ?? "System";
                question.CreatedAt = DateTime.Now;

                // Handle image upload if provided
                if (questionImage != null && questionImage.Length > 0)
                {
                    // Validate file type
                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                    if (!allowedTypes.Contains(questionImage.ContentType.ToLower()))
                    {
                        ModelState.AddModelError("questionImage", "Only JPG, PNG and GIF image types are allowed");

                        ViewBag.QuestionGroups = await _context.QuestionGroups
                            .Include(qg => qg.Course)
                            .OrderBy(qg => qg.Course.CourseName)
                            .ThenBy(qg => qg.Name)
                            .ToListAsync();
                        ViewBag.QuestionGroup = questionGroup;
                        return View(question);
                    }

                    // Validate file size (max 5MB)
                    if (questionImage.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("questionImage", "Image size cannot exceed 5MB");

                        ViewBag.QuestionGroups = await _context.QuestionGroups
                            .Include(qg => qg.Course)
                            .OrderBy(qg => qg.Course.CourseName)
                            .ThenBy(qg => qg.Name)
                            .ToListAsync();
                        ViewBag.QuestionGroup = questionGroup;
                        return View(question);
                    }

                    // Create unique filename
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" +
                        Path.GetFileName(questionImage.FileName);

                    // Define upload directory path
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                        "uploads", "question-images");

                    // Create directory if it doesn't exist
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Set the file path
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Save the file
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await questionImage.CopyToAsync(fileStream);
                    }

                    // Set the image path in the question model
                    question.ImagePath = "/uploads/question-images/" + uniqueFileName;

                    // If no image description provided but image exists, set a default one
                    if (string.IsNullOrWhiteSpace(question.ImageDescription))
                    {
                        question.ImageDescription = "Image for question";
                    }

                    // Set default position if not provided
                    if (string.IsNullOrWhiteSpace(question.ImageDisplayPosition))
                    {
                        question.ImageDisplayPosition = "Above";
                    }
                }

                // Handle different question types
                switch (question.QuestionType)
                {
                    case "MultipleChoice":
                        if (optionTexts != null && optionTexts.Count > 0)
                        {
                            question.Options = new List<QuestionOption>();

                            // Process options
                            for (int i = 0; i < optionTexts.Count; i++)
                            {
                                if (!string.IsNullOrWhiteSpace(optionTexts[i]))
                                {
                                    bool isCorrect = optionCorrect != null && i < optionCorrect.Count && optionCorrect[i];

                                    var option = new QuestionOption
                                    {
                                        OptionText = optionTexts[i],
                                        IsCorrect = isCorrect,
                                        CreatedBy = User.Identity.Name ?? "System",
                                        CreatedAt = DateTime.Now
                                    };

                                    question.Options.Add(option);
                                }
                            }

                            // Validate at least one correct answer
                            if (!question.Options.Any(o => o.IsCorrect))
                            {
                                ModelState.AddModelError("", "At least one option must be marked as correct");
                                ViewBag.QuestionGroup = questionGroup;
                                ViewBag.QuestionGroups = await _context.QuestionGroups
                                    .Include(qg => qg.Course)
                                    .OrderBy(qg => qg.Course.CourseName)
                                    .ThenBy(qg => qg.Name)
                                    .ToListAsync();
                                return RedirectToAction(nameof(CreateQuestion));
                            }
                        }
                        break;

                    case "TrueFalse":
                        // Create standard True/False options
                        question.Options = new List<QuestionOption>
                {
                    new QuestionOption
                    {
                        OptionText = "True",
                        IsCorrect = trueFalseIsTrue.HasValue && trueFalseIsTrue.Value,
                        CreatedBy = User.Identity.Name ?? "System",
                        CreatedAt = DateTime.Now
                    },
                    new QuestionOption
                    {
                        OptionText = "False",
                        IsCorrect = trueFalseIsTrue.HasValue && !trueFalseIsTrue.Value,
                        CreatedBy = User.Identity.Name ?? "System",
                        CreatedAt = DateTime.Now
                    }
                };
                        break;

                    case "ShortAnswer":
                    case "LongText":
                        // These don't need options
                        question.Options = new List<QuestionOption>();
                        break;
                }

                // Sanitize HTML content
                question.QuestionText = SanitizeHtml(question.QuestionText);
                if (!string.IsNullOrEmpty(question.AdditionalInfo))
                {
                    question.AdditionalInfo = SanitizeHtml(question.AdditionalInfo);
                }

                _context.Questions.Add(question);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Question created successfully";
                return RedirectToAction(nameof(QuestionGroupDetails), new { id = question.QuestionGroupId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating question: {ex.Message}");
                // Log the exception for debugging
                Console.WriteLine($"Exception: {ex}");
                Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                // If we got this far, something failed, redisplay form
                var group = await _context.QuestionGroups
                    .Include(qg => qg.Course)
                    .FirstOrDefaultAsync(qg => qg.Id == question.QuestionGroupId);

                if (group != null)
                {
                    ViewBag.QuestionGroup = group;
                }

                ViewBag.QuestionGroups = await _context.QuestionGroups
                    .Include(qg => qg.Course)
                    .OrderBy(qg => qg.Course.CourseName)
                    .ThenBy(qg => qg.Name)
                    .ToListAsync();
                return View(question);
            }
        }

        // GET: Assessments/EditQuestion/5
        public async Task<IActionResult> EditQuestion(int id)
        {
            var question = await _context.Questions
                .Include(q => q.QuestionGroup)
                    .ThenInclude(qg => qg.Course)
                .Include(q => q.Options) // Make sure this is here
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                TempData["Error"] = "Question not found";
                return RedirectToAction(nameof(Questions));
            }

            // Explicitly load options if they weren't included
            if (question.Options == null || !question.Options.Any())
            {
                var options = await _context.QuestionOptions
                    .Where(o => o.QuestionId == id)
                    .ToListAsync();

                question.Options = options;
            }

            ViewBag.QuestionGroups = await _context.QuestionGroups
                .Include(qg => qg.Course)
                .OrderBy(qg => qg.Course.CourseName)
                .ThenBy(qg => qg.Name)
                .ToListAsync();

            ViewBag.QuestionGroup = question.QuestionGroup;

            return View(question); 
        }


        // POST: Assessments/EditQuestion/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestionSubmit(int id, Question question, IFormFile questionImage,
            List<string> optionTexts = null, List<bool> optionCorrect = null, bool? trueFalseIsTrue = null,
            bool removeExistingImage = false)
        {
            if (id != question.Id)
            {
                TempData["Error"] = "Invalid request";
                return RedirectToAction(nameof(Questions));
            }

            // Remove validation errors for fields we'll set programmatically
            ModelState.Remove("UpdatedBy");
            ModelState.Remove("UpdatedAt");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("QuestionGroup");
            ModelState.Remove("ImagePath");
            ModelState.Remove("ImageDescription");
            ModelState.Remove("ImageDisplayPosition");

            try
            {
                // Retrieve the existing question
                var existingQuestion = await _context.Questions
                    .Include(q => q.Options)
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (existingQuestion == null)
                {
                    TempData["Error"] = "Question not found";
                    return RedirectToAction(nameof(Questions));
                }

                // Verify question group exists
                var questionGroup = await _context.QuestionGroups.FindAsync(question.QuestionGroupId);
                if (questionGroup == null)
                {
                    ModelState.AddModelError("QuestionGroupId", "Invalid question group");
                    ViewBag.QuestionGroups = await _context.QuestionGroups
                        .Include(qg => qg.Course)
                        .OrderBy(qg => qg.Course.CourseName)
                        .ThenBy(qg => qg.Name)
                        .ToListAsync();
                    return View(question);
                }

                // Validate question type
                if (!IsValidQuestionType(question.QuestionType))
                {
                    ModelState.AddModelError("QuestionType", "Invalid question type. Allowed types are: MultipleChoice, ShortAnswer, LongText, TrueFalse");
                    ViewBag.QuestionGroups = await _context.QuestionGroups
                        .Include(qg => qg.Course)
                        .OrderBy(qg => qg.Course.CourseName)
                        .ThenBy(qg => qg.Name)
                        .ToListAsync();
                    return View(question);
                }

                // Handle image related actions
                if (removeExistingImage && !string.IsNullOrEmpty(existingQuestion.ImagePath))
                {
                    string oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                        existingQuestion.ImagePath.TrimStart('/'));

                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }

                    existingQuestion.ImagePath = null;
                    existingQuestion.ImageDescription = null;
                }

                // Handle new image upload if provided
                if (questionImage != null && questionImage.Length > 0)
                {
                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                    if (!allowedTypes.Contains(questionImage.ContentType.ToLower()))
                    {
                        ModelState.AddModelError("questionImage", "Only JPG, PNG and GIF image types are allowed");
                        ViewBag.QuestionGroups = await _context.QuestionGroups
                            .Include(qg => qg.Course)
                            .OrderBy(qg => qg.Course.CourseName)
                            .ThenBy(qg => qg.Name)
                            .ToListAsync();
                        ViewBag.QuestionGroup = questionGroup;
                        return View(question);
                    }

                    if (questionImage.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("questionImage", "Image size cannot exceed 5MB");
                        ViewBag.QuestionGroups = await _context.QuestionGroups
                            .Include(qg => qg.Course)
                            .OrderBy(qg => qg.Course.CourseName)
                            .ThenBy(qg => qg.Name)
                            .ToListAsync();
                        ViewBag.QuestionGroup = questionGroup;
                        return View(question);
                    }

                    if (!string.IsNullOrEmpty(existingQuestion.ImagePath))
                    {
                        string oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                            existingQuestion.ImagePath.TrimStart('/'));

                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(questionImage.FileName);
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "question-images");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await questionImage.CopyToAsync(fileStream);
                    }

                    existingQuestion.ImagePath = "/uploads/question-images/" + uniqueFileName;
                    existingQuestion.ImageDescription = question.ImageDescription;
                    if (string.IsNullOrWhiteSpace(existingQuestion.ImageDescription))
                    {
                        existingQuestion.ImageDescription = "Image for question";
                    }

                    existingQuestion.ImageDisplayPosition = question.ImageDisplayPosition;
                    if (string.IsNullOrWhiteSpace(existingQuestion.ImageDisplayPosition))
                    {
                        existingQuestion.ImageDisplayPosition = "Above";
                    }
                }
                else
                {
                    if (!removeExistingImage && !string.IsNullOrEmpty(existingQuestion.ImagePath))
                    {
                        existingQuestion.ImageDescription = question.ImageDescription;
                        existingQuestion.ImageDisplayPosition = question.ImageDisplayPosition;
                    }
                }

                // Update properties - INCLUDING THE RICH TEXT CONTENT
                existingQuestion.QuestionGroupId = question.QuestionGroupId;
                existingQuestion.QuestionText = question.QuestionText; // This now contains the Quill HTML content
                existingQuestion.QuestionType = question.QuestionType;
                existingQuestion.Points = question.Points;
                existingQuestion.AdditionalInfo = question.AdditionalInfo; // This now contains the Quill HTML content
                existingQuestion.IsActive = question.IsActive;

                // Set audit information
                existingQuestion.UpdatedBy = User.Identity.Name ?? "System";
                existingQuestion.UpdatedAt = DateTime.Now;

                // Sanitize HTML content
                existingQuestion.QuestionText = SanitizeHtml(existingQuestion.QuestionText);
                if (!string.IsNullOrEmpty(existingQuestion.AdditionalInfo))
                {
                    existingQuestion.AdditionalInfo = SanitizeHtml(existingQuestion.AdditionalInfo);
                }

                // Remove existing options
                _context.QuestionOptions.RemoveRange(existingQuestion.Options);
                await _context.SaveChangesAsync();
                existingQuestion.Options.Clear();

                // Handle different question types
                switch (question.QuestionType)
                {
                    case "MultipleChoice":
                        if (optionTexts != null && optionTexts.Count > 0)
                        {
                            for (int i = 0; i < optionTexts.Count; i++)
                            {
                                if (!string.IsNullOrWhiteSpace(optionTexts[i]))
                                {
                                    bool isCorrect = optionCorrect != null && i < optionCorrect.Count && optionCorrect[i];

                                    var option = new QuestionOption
                                    {
                                        QuestionId = existingQuestion.Id,
                                        OptionText = optionTexts[i],
                                        IsCorrect = isCorrect,
                                        CreatedBy = User.Identity.Name ?? "System",
                                        CreatedAt = DateTime.Now
                                    };

                                    existingQuestion.Options.Add(option);
                                }
                            }

                            if (!existingQuestion.Options.Any(o => o.IsCorrect))
                            {
                                ModelState.AddModelError("", "At least one option must be marked as correct");
                                ViewBag.QuestionGroups = await _context.QuestionGroups
                                    .Include(qg => qg.Course)
                                    .OrderBy(qg => qg.Course.CourseName)
                                    .ThenBy(qg => qg.Name)
                                    .ToListAsync();
                                ViewBag.QuestionGroup = questionGroup;
                                return View(question);
                            }
                        }
                        break;

                    case "TrueFalse":
                        var trueOption = new QuestionOption
                        {
                            QuestionId = existingQuestion.Id,
                            OptionText = "True",
                            IsCorrect = trueFalseIsTrue.HasValue && trueFalseIsTrue.Value,
                            CreatedBy = User.Identity.Name ?? "System",
                            CreatedAt = DateTime.Now
                        };

                        var falseOption = new QuestionOption
                        {
                            QuestionId = existingQuestion.Id,
                            OptionText = "False",
                            IsCorrect = trueFalseIsTrue.HasValue && !trueFalseIsTrue.Value,
                            CreatedBy = User.Identity.Name ?? "System",
                            CreatedAt = DateTime.Now
                        };

                        existingQuestion.Options.Add(trueOption);
                        existingQuestion.Options.Add(falseOption);
                        break;

                    case "ShortAnswer":
                    case "LongText":
                        // These don't need options
                        break;
                }

                // Final save with updated options
                await _context.SaveChangesAsync();

                TempData["Success"] = "Question updated successfully";
                return RedirectToAction(nameof(QuestionGroupDetails), new { id = existingQuestion.QuestionGroupId });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!QuestionExists(id))
                {
                    TempData["Error"] = "Question not found";
                    return RedirectToAction(nameof(Questions));
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating question: {ex.Message}");
                Console.WriteLine($"Exception: {ex}");
                Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
            }

            // If we got this far, something failed, redisplay form
            ViewBag.QuestionGroups = await _context.QuestionGroups
                .Include(qg => qg.Course)
                .OrderBy(qg => qg.Course.CourseName)
                .ThenBy(qg => qg.Name)
                .ToListAsync();

            var group = await _context.QuestionGroups
                .Include(qg => qg.Course)
                .FirstOrDefaultAsync(qg => qg.Id == question.QuestionGroupId);

            if (group != null)
            {
                ViewBag.QuestionGroup = group;
            }

            return View(question);
        }
        // GET: Assessments/DeleteQuestion/5
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var question = await _context.Questions
                .Include(q => q.QuestionGroup)
                    .ThenInclude(qg => qg.Course)
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                TempData["Error"] = "Question not found";
                return RedirectToAction(nameof(Questions));
            }

            return View(question);
        }

        // POST: Assessments/DeleteQuestionConfirmed
        [HttpPost, ActionName("DeleteQuestionConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestionConfirmed(int id)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return Json(new { success = false, message = "Question not found" });
            }

            // Check if this question is being used in any student responses
            var isUsedInStudentResponse = await _context.StudentResponses
                .AnyAsync(sr => sr.QuestionId == id);

            if (isUsedInStudentResponse)
            {
                return Json(new { success = false, message = "Cannot delete question as it has been used in student responses" });
            }

            try
            {
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting question: {ex.Message}" });
            }
        }

        // GET: Assessments/PreviewQuestion/5
        public async Task<IActionResult> PreviewQuestion(int id)
        {
            var question = await _context.Questions
                .Include(q => q.QuestionGroup)
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                TempData["Error"] = "Question not found";
                return RedirectToAction(nameof(Questions));
            }

            return View(question);
        }

        // GET: Assessments/GetQuestionOptions/5
        [HttpGet]
        public async Task<IActionResult> GetQuestionOptions(int id)
        {
            var options = await _context.QuestionOptions
                .Where(o => o.QuestionId == id)
                .Select(o => new { o.Id, o.OptionText, o.IsCorrect })
                .ToListAsync();

            return Json(options);
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



        private string SanitizeHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var sanitizer = new HtmlSanitizer();

            // Configure allowed tags and attributes
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedTags.Add("p");
            sanitizer.AllowedTags.Add("br");
            sanitizer.AllowedTags.Add("strong");
            sanitizer.AllowedTags.Add("b");
            sanitizer.AllowedTags.Add("em");
            sanitizer.AllowedTags.Add("i");
            sanitizer.AllowedTags.Add("u");
            sanitizer.AllowedTags.Add("s");
            sanitizer.AllowedTags.Add("ul");
            sanitizer.AllowedTags.Add("ol");
            sanitizer.AllowedTags.Add("li");
            sanitizer.AllowedTags.Add("table");
            sanitizer.AllowedTags.Add("tr");
            sanitizer.AllowedTags.Add("td");
            sanitizer.AllowedTags.Add("th");
            sanitizer.AllowedTags.Add("thead");
            sanitizer.AllowedTags.Add("tbody");
            sanitizer.AllowedTags.Add("a");
            sanitizer.AllowedTags.Add("img");
            sanitizer.AllowedTags.Add("sub");
            sanitizer.AllowedTags.Add("sup");
            sanitizer.AllowedTags.Add("span");
            sanitizer.AllowedTags.Add("div");

            // Allow MathJax classes and attributes
            sanitizer.AllowedAttributes.Add("class");
            sanitizer.AllowedAttributes.Add("style");
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("src");
            sanitizer.AllowedAttributes.Add("alt");

            return sanitizer.Sanitize(input);
        }




        #region Question Import (Bulk Upload)

        // GET: Assessments/ImportQuestions/5
        public async Task<IActionResult> ImportQuestions(int questionGroupId)
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the question group exists and belongs to the current lecturer
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Course)
                    .ThenInclude(c => c.CourseLecturers)
                .Where(qg => qg.Id == questionGroupId &&
                            (qg.Course.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                             qg.Course.InstructorId == currentUserId))
                .FirstOrDefaultAsync();

            if (questionGroup == null)
            {
                TempData["Error"] = "Question group not found or you don't have access to it";
                return RedirectToAction(nameof(QuestionGroups));
            }

            ViewBag.QuestionGroup = questionGroup;
            return View();
        }

        // POST: Assessments/UploadImportFile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImportFile(int questionGroupId, IFormFile importFile,
            [FromServices] QuestionImportService importService)
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the question group exists and belongs to the current lecturer
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Course)
                    .ThenInclude(c => c.CourseLecturers)
                .Where(qg => qg.Id == questionGroupId &&
                            (qg.Course.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                             qg.Course.InstructorId == currentUserId))
                .FirstOrDefaultAsync();

            if (questionGroup == null)
            {
                TempData["Error"] = "Question group not found or you don't have access to it";
                return RedirectToAction(nameof(QuestionGroups));
            }

            // Validate file
            var (isValid, errorMessage) = importService.ValidateFile(importFile);
            if (!isValid)
            {
                TempData["Error"] = errorMessage;
                ViewBag.QuestionGroup = questionGroup;
                return View("ImportQuestions");
            }

            try
            {
                // Parse the file
                using (var stream = importFile.OpenReadStream())
                {
                    var importedQuestions = await importService.ParseTextFileAsync(stream);

                    if (importedQuestions == null || !importedQuestions.Any())
                    {
                        TempData["Error"] = "No valid questions found in the file. Please check the format.";
                        ViewBag.QuestionGroup = questionGroup;
                        return View("ImportQuestions");
                    }

                    // Store in session for preview
                    HttpContext.Session.SetObject("ImportedQuestions", importedQuestions);
                    HttpContext.Session.SetInt32("ImportQuestionGroupId", questionGroupId);

                    TempData["Success"] = $"File parsed successfully. Found {importedQuestions.Count} question(s).";
                    return RedirectToAction(nameof(PreviewImportedQuestions), new { questionGroupId });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error parsing file: {ex.Message}";
                ViewBag.QuestionGroup = questionGroup;
                return View("ImportQuestions");
            }
        }

        // GET: Assessments/PreviewImportedQuestions/5
        public async Task<IActionResult> PreviewImportedQuestions(int questionGroupId)
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the question group exists and belongs to the current lecturer
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Course)
                    .ThenInclude(c => c.CourseLecturers)
                .Where(qg => qg.Id == questionGroupId &&
                            (qg.Course.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                             qg.Course.InstructorId == currentUserId))
                .FirstOrDefaultAsync();

            if (questionGroup == null)
            {
                TempData["Error"] = "Question group not found or you don't have access to it";
                return RedirectToAction(nameof(QuestionGroups));
            }

            // Check if we have session data
            var sessionQuestionGroupId = HttpContext.Session.GetInt32("ImportQuestionGroupId");
            if (!sessionQuestionGroupId.HasValue || sessionQuestionGroupId.Value != questionGroupId)
            {
                TempData["Error"] = "No import data found. Please upload a file first.";
                return RedirectToAction(nameof(ImportQuestions), new { questionGroupId });
            }

            var importedQuestions = HttpContext.Session.GetObject<List<QuestionImportModel>>("ImportedQuestions");
            if (importedQuestions == null || !importedQuestions.Any())
            {
                TempData["Error"] = "No import data found. Please upload a file first.";
                return RedirectToAction(nameof(ImportQuestions), new { questionGroupId });
            }

            var viewModel = new BulkImportPreviewViewModel
            {
                QuestionGroupId = questionGroupId,
                QuestionGroup = questionGroup,
                ImportedQuestions = importedQuestions
            };

            viewModel.CalculateStatistics();

            return View(viewModel);
        }

        // POST: Assessments/UpdateImportedQuestion
        [HttpPost]
        public IActionResult UpdateImportedQuestion([FromBody] QuestionImportModel updatedQuestion)
        {
            try
            {
                var importedQuestions = HttpContext.Session.GetObject<List<QuestionImportModel>>("ImportedQuestions");

                if (importedQuestions == null || !importedQuestions.Any())
                {
                    return Json(new { success = false, message = "No import data found in session" });
                }

                var question = importedQuestions.FirstOrDefault(q => q.TemporaryId == updatedQuestion.TemporaryId);
                if (question == null)
                {
                    return Json(new { success = false, message = "Question not found" });
                }

                // Update common properties
                question.QuestionText = updatedQuestion.QuestionText;
                question.Points = updatedQuestion.Points;
                question.AdditionalInfo = updatedQuestion.AdditionalInfo;

                // Update type-specific properties
                switch (question.QuestionType)
                {
                    case "MultipleChoice":
                        if (updatedQuestion.Options != null)
                        {
                            question.Options = updatedQuestion.Options;
                        }
                        break;

                    case "TrueFalse":
                        question.TrueFalseAnswer = updatedQuestion.TrueFalseAnswer;
                        // Clear options for TrueFalse (they shouldn't have any)
                        question.Options = new List<ImportOptionModel>();
                        break;

                    case "ShortAnswer":
                        question.ExpectedAnswer = updatedQuestion.ExpectedAnswer ?? string.Empty;
                        question.MaxLength = updatedQuestion.MaxLength;
                        // Clear options for ShortAnswer
                        question.Options = new List<ImportOptionModel>();
                        break;

                    case "LongText":
                        question.ExpectedAnswer = updatedQuestion.ExpectedAnswer ?? string.Empty;
                        question.MinLength = updatedQuestion.MinLength;
                        question.MaxLength = updatedQuestion.MaxLength;
                        question.ExpectedKeywords = updatedQuestion.ExpectedKeywords ?? string.Empty;
                        // Clear options for LongText
                        question.Options = new List<ImportOptionModel>();
                        break;
                }

                // Re-validate the question using the service
                var importService = new QuestionImportService();
                importService.ValidateQuestion(question);

                // Save back to session
                HttpContext.Session.SetObject("ImportedQuestions", importedQuestions);

                return Json(new
                {
                    success = true,
                    isValid = question.IsValid,
                    validationErrors = question.ValidationErrors,
                    correctAnswersCount = question.GetCorrectAnswersCount()
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Assessments/DeleteImportedQuestion
        [HttpPost]
        public IActionResult DeleteImportedQuestion(int temporaryId)
        {
            try
            {
                var importedQuestions = HttpContext.Session.GetObject<List<QuestionImportModel>>("ImportedQuestions");

                if (importedQuestions == null || !importedQuestions.Any())
                {
                    return Json(new { success = false, message = "No import data found in session" });
                }

                var question = importedQuestions.FirstOrDefault(q => q.TemporaryId == temporaryId);
                if (question == null)
                {
                    return Json(new { success = false, message = "Question not found" });
                }

                importedQuestions.Remove(question);

                // Save back to session
                HttpContext.Session.SetObject("ImportedQuestions", importedQuestions);

                return Json(new
                {
                    success = true,
                    remainingCount = importedQuestions.Count,
                    validCount = importedQuestions.Count(q => q.IsValid),
                    invalidCount = importedQuestions.Count(q => !q.IsValid)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Assessments/ConfirmImport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmImport(int questionGroupId,
            [FromServices] QuestionImportService importService)
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the question group exists and belongs to the current lecturer
            var questionGroup = await _context.QuestionGroups
                .Include(qg => qg.Course)
                    .ThenInclude(c => c.CourseLecturers)
                .Where(qg => qg.Id == questionGroupId &&
                            (qg.Course.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                             qg.Course.InstructorId == currentUserId))
                .FirstOrDefaultAsync();

            if (questionGroup == null)
            {
                TempData["Error"] = "Question group not found or you don't have access to it";
                return RedirectToAction(nameof(QuestionGroups));
            }

            // Get questions from session
            var importedQuestions = HttpContext.Session.GetObject<List<QuestionImportModel>>("ImportedQuestions");
            if (importedQuestions == null || !importedQuestions.Any())
            {
                TempData["Error"] = "No import data found. Please upload a file first.";
                return RedirectToAction(nameof(ImportQuestions), new { questionGroupId });
            }

            // Filter only valid questions
            var validQuestions = importedQuestions.Where(q => q.IsValid).ToList();

            if (!validQuestions.Any())
            {
                TempData["Error"] = "No valid questions to import. Please fix validation errors or upload a different file.";
                return RedirectToAction(nameof(PreviewImportedQuestions), new { questionGroupId });
            }

            try
            {
                var savedCount = 0;
                var createdBy = User.Identity.Name ?? "System";

                // Use execution strategy to handle transactions properly
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            foreach (var importQuestion in validQuestions)
                            {
                                var question = importService.ConvertToQuestion(importQuestion, questionGroupId, createdBy);

                                // Sanitize HTML content
                                question.QuestionText = SanitizeHtml(question.QuestionText);
                                if (!string.IsNullOrEmpty(question.AdditionalInfo))
                                {
                                    question.AdditionalInfo = SanitizeHtml(question.AdditionalInfo);
                                }

                                _context.Questions.Add(question);
                                savedCount++;
                            }

                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                        }
                        catch (Exception)
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                });

                // Clear session data
                HttpContext.Session.Remove("ImportedQuestions");
                HttpContext.Session.Remove("ImportQuestionGroupId");

                TempData["Success"] = $"Successfully imported {savedCount} question(s) to the question group.";
                return RedirectToAction(nameof(QuestionGroupDetails), new { id = questionGroupId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error importing questions: {ex.Message}";
                return RedirectToAction(nameof(PreviewImportedQuestions), new { questionGroupId });
            }
        }

        // GET: Assessments/DownloadTemplate
        public IActionResult DownloadTemplate([FromServices] QuestionImportService importService)
        {
            var templateContent = importService.GenerateTemplateContent();
            var bytes = System.Text.Encoding.UTF8.GetBytes(templateContent);

            return File(bytes, "text/plain", "question-import-template.txt");
        }

        // POST: Assessments/CancelImport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelImport(int questionGroupId)
        {
            // Clear session data
            HttpContext.Session.Remove("ImportedQuestions");
            HttpContext.Session.Remove("ImportQuestionGroupId");

            TempData["Info"] = "Import cancelled";
            return RedirectToAction(nameof(QuestionGroupDetails), new { id = questionGroupId });
        }

        #endregion






        #region Question Options Management

        // GET: Assessments/QuestionOptions
        public async Task<IActionResult> QuestionOptions()
        {
            var questionOptions = await _context.QuestionOptions
                .Include(qo => qo.Question)
                .ToListAsync();

            return View(questionOptions);
        }

        // GET: Assessments/QuestionOptionsByQuestion/5
        public async Task<IActionResult> QuestionOptionsByQuestion(int questionId)
        {
            var question = await _context.Questions
                .Include(q => q.QuestionGroup)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
            {
                TempData["Error"] = "Question not found";
                return RedirectToAction(nameof(Questions));
            }

            ViewBag.Question = question;

            var options = await _context.QuestionOptions
                .Where(qo => qo.QuestionId == questionId)
                .ToListAsync();

            return View(options);
        }

        // GET: Assessments/CreateQuestionOption
        public async Task<IActionResult> CreateQuestionOption(int questionId)
        {
            var question = await _context.Questions
                .Include(q => q.QuestionGroup)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
            {
                TempData["Error"] = "Question not found";
                return RedirectToAction(nameof(Questions));
            }

            // Verify that this is a multiple choice question
            if (question.QuestionType != "MultipleChoice" && question.QuestionType != "TrueFalse")
            {
                TempData["Error"] = "Options can only be added to Multiple Choice or True/False questions";
                return RedirectToAction(nameof(QuestionDetails), new { id = questionId });
            }

            ViewBag.Question = question;

            var questionOption = new QuestionOption
            {
                QuestionId = questionId,
                IsCorrect = false,
                
                // Set audit information
                CreatedBy = User.Identity.Name ?? "System",
                CreatedAt = DateTime.Now
            };

            return View(questionOption);
        }

        // POST: Assessments/CreateQuestionOption
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestionOption(QuestionOption questionOption)
        {
            try
            {
                // Verify question exists
                var question = await _context.Questions.FindAsync(questionOption.QuestionId);
                if (question == null)
                {
                    TempData["Error"] = "Invalid question ID";
                    return RedirectToAction(nameof(Questions));
                }

                // Verify that this is a multiple choice question
                if (question.QuestionType != "MultipleChoice" && question.QuestionType != "TrueFalse")
                {
                    TempData["Error"] = "Options can only be added to Multiple Choice or True/False questions";
                    return RedirectToAction(nameof(QuestionDetails), new { id = questionOption.QuestionId });
                }

                // Set audit information
                questionOption.CreatedBy = User.Identity.Name ?? "System";
                questionOption.CreatedAt = DateTime.Now;

                _context.QuestionOptions.Add(questionOption);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Question option created successfully";
                return RedirectToAction(nameof(QuestionDetails), new { id = questionOption.QuestionId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating question option: {ex.Message}";

                var question = await _context.Questions
                    .Include(q => q.QuestionGroup)
                    .FirstOrDefaultAsync(q => q.Id == questionOption.QuestionId);

                if (question != null)
                {
                    ViewBag.Question = question;
                }

                return View(questionOption);
            }
        }

        // GET: Assessments/EditQuestionOption/5
        public async Task<IActionResult> EditQuestionOption(int id)
        {
            var questionOption = await _context.QuestionOptions
                .Include(qo => qo.Question)
                .FirstOrDefaultAsync(qo => qo.Id == id);

            if (questionOption == null)
            {
                TempData["Error"] = "Question option not found";
                return RedirectToAction(nameof(Questions));
            }

            ViewBag.Question = await _context.Questions
                .Include(q => q.QuestionGroup)
                .FirstOrDefaultAsync(q => q.Id == questionOption.QuestionId);

            return View(questionOption);
        }

        // POST: Assessments/EditQuestionOption/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestionOption(int id, QuestionOption questionOption)
        {
            if (id != questionOption.Id)
            {
                TempData["Error"] = "Invalid request";
                return RedirectToAction(nameof(Questions));
            }

            try
            {
                // Retrieve the existing question option
                var existingQuestionOption = await _context.QuestionOptions.FindAsync(id);
                if (existingQuestionOption == null)
                {
                    TempData["Error"] = "Question option not found";
                    return RedirectToAction(nameof(Questions));
                }

                // Verify question exists
                var question = await _context.Questions.FindAsync(questionOption.QuestionId);
                if (question == null)
                {
                    TempData["Error"] = "Invalid question ID";
                    return RedirectToAction(nameof(Questions));
                }

                // Update properties
                existingQuestionOption.OptionText = questionOption.OptionText;
                existingQuestionOption.IsCorrect = questionOption.IsCorrect;

                // Set audit information
                existingQuestionOption.UpdatedBy = User.Identity.Name ?? "System";
                existingQuestionOption.UpdatedAt = DateTime.Now;

                _context.Update(existingQuestionOption);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Question option updated successfully";
                return RedirectToAction(nameof(QuestionDetails), new { id = existingQuestionOption.QuestionId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating question option: {ex.Message}";

                ViewBag.Question = await _context.Questions
                    .Include(q => q.QuestionGroup)
                    .FirstOrDefaultAsync(q => q.Id == questionOption.QuestionId);

                return View(questionOption);
            }
        }

        // GET: Assessments/DeleteQuestionOption/5
        public async Task<IActionResult> DeleteQuestionOption(int id)
        {
            var questionOption = await _context.QuestionOptions
                .Include(qo => qo.Question)
                .ThenInclude(q => q.QuestionGroup)
                .FirstOrDefaultAsync(qo => qo.Id == id);

            if (questionOption == null)
            {
                TempData["Error"] = "Question option not found";
                return RedirectToAction(nameof(Questions));
            }

            return View(questionOption);
        }

        // POST: Assessments/DeleteQuestionOptionConfirmed/5
        [HttpPost, ActionName("DeleteQuestionOptionConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestionOptionConfirmed(int id)
        {
            var questionOption = await _context.QuestionOptions.FindAsync(id);
            if (questionOption == null)
            {
                TempData["Error"] = "Question option not found";
                return RedirectToAction(nameof(Questions));
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
                    TempData["Error"] = "Cannot delete option from True/False question. There must be exactly 2 options.";
                    return RedirectToAction(nameof(QuestionDetails), new { id = questionOption.QuestionId });
                }

                // For MultipleChoice, we should have at least 2 options
                if (question.QuestionType == "MultipleChoice" && optionCount <= 2)
                {
                    TempData["Error"] = "Multiple choice questions must have at least 2 options";
                    return RedirectToAction(nameof(QuestionDetails), new { id = questionOption.QuestionId });
                }

                // Check if this is the only correct option
                var isOnlyCorrectOption = questionOption.IsCorrect &&
                    await _context.QuestionOptions
                        .Where(qo => qo.QuestionId == questionOption.QuestionId && qo.IsCorrect && qo.Id != id)
                        .CountAsync() == 0;

                if (isOnlyCorrectOption)
                {
                    TempData["Error"] = "Cannot delete the only correct option for this question";
                    return RedirectToAction(nameof(QuestionDetails), new { id = questionOption.QuestionId });
                }

                var questionId = questionOption.QuestionId;
                _context.QuestionOptions.Remove(questionOption);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Question option deleted successfully";
                return RedirectToAction(nameof(QuestionDetails), new { id = questionId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting question option: {ex.Message}";
                return RedirectToAction(nameof(QuestionDetails), new { id = questionOption.QuestionId });
            }
        }

        // BATCH CREATION METHOD
        // GET: Assessments/CreateQuestionOptionsBatch/5
        public async Task<IActionResult> CreateQuestionOptionsBatch(int questionId)
        {
            var question = await _context.Questions
                .Include(q => q.QuestionGroup)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
            {
                TempData["Error"] = "Question not found";
                return RedirectToAction(nameof(Questions));
            }

            // Verify that this is a multiple choice question
            if (question.QuestionType != "MultipleChoice" && question.QuestionType != "TrueFalse")
            {
                TempData["Error"] = "Options can only be added to Multiple Choice or True/False questions";
                return RedirectToAction(nameof(QuestionDetails), new { id = questionId });
            }

            ViewBag.Question = question;

            var model = new BatchOptionCreateModel
            {
                QuestionId = questionId,
                Options = new List<OptionModel>()
            };

            // Add empty options to start with
            for (int i = 0; i < 4; i++)
            {
                model.Options.Add(new OptionModel());
            }

            return View(model);
        }

        // POST: Assessments/CreateQuestionOptionsBatch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestionOptionsBatch(BatchOptionCreateModel model)
        {
            try
            {
                // Verify question exists
                var question = await _context.Questions.FindAsync(model.QuestionId);
                if (question == null)
                {
                    TempData["Error"] = "Invalid question ID";
                    return RedirectToAction(nameof(Questions));
                }

                // Verify that this is a multiple choice question
                if (question.QuestionType != "MultipleChoice" && question.QuestionType != "TrueFalse")
                {
                    TempData["Error"] = "Options can only be added to Multiple Choice or True/False questions";
                    return RedirectToAction(nameof(QuestionDetails), new { id = model.QuestionId });
                }

                // Filter out empty options
                model.Options = model.Options
                    .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
                    .ToList();

                // Ensure there's at least one correct answer
                if (!model.Options.Any(o => o.IsCorrect))
                {
                    TempData["Error"] = "At least one option must be marked as correct";
                    ViewBag.Question = question;
                    return View(model);
                }

                // For True/False questions, there should be exactly 2 options
                if (question.QuestionType == "TrueFalse" && model.Options.Count != 2)
                {
                    TempData["Error"] = "True/False questions must have exactly 2 options";
                    ViewBag.Question = question;
                    return View(model);
                }

                // Create the options
                foreach (var option in model.Options)
                {
                    var newOption = new QuestionOption
                    {
                        QuestionId = model.QuestionId,
                        OptionText = option.OptionText,
                        IsCorrect = option.IsCorrect,
                        CreatedBy = User.Identity.Name ?? "System",
                        CreatedAt = DateTime.Now
                    };

                    _context.QuestionOptions.Add(newOption);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Question options created successfully";
                return RedirectToAction(nameof(QuestionDetails), new { id = model.QuestionId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating question options: {ex.Message}";

                var question = await _context.Questions
                    .Include(q => q.QuestionGroup)
                    .FirstOrDefaultAsync(q => q.Id == model.QuestionId);

                if (question != null)
                {
                    ViewBag.Question = question;
                }

                return View(model);
            }
        }

        #endregion

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
}
