using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Data;
using SIS.Models;
using SIS.Models.Assessments;
using SIS.Models.StudentApplication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentAssessmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StudentAssessmentController> _logger;

        public StudentAssessmentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<StudentAssessmentController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: StudentAssessment/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                _logger.LogInformation($"Details action called for assessment id: {id}");

                // Get the assessment configuration with related data
                var assessmentConfig = await _context.AssessmentConfigurations
                    .Include(a => a.Assessment)
                    .Include(a => a.Course)
                    .Include(a => a.AcademicYear)
                    .Include(a => a.ModeOfStudy)
                    .FirstOrDefaultAsync(a => a.Id == id && a.IsPublished);

                if (assessmentConfig == null)
                {
                    _logger.LogWarning($"Assessment configuration not found or not published. Id: {id}");
                    TempData["Error"] = "Assessment not found or not available.";
                    return RedirectToAction("MyCourses", "Course");
                }

                // Check if assessment is within the available time window
                var currentTime = DateTime.Now;
                _logger.LogInformation($"Current time: {currentTime}, Assessment start: {assessmentConfig.StartDateTime}, Assessment end: {assessmentConfig.EndDateTime}");

                if (currentTime < assessmentConfig.StartDateTime || currentTime > assessmentConfig.EndDateTime)
                {
                    _logger.LogWarning($"Assessment {id} not available at current time {currentTime}");
                    TempData["Error"] = "This assessment is not currently available.";
                    return RedirectToAction("ViewCourseContent", "Course", new { id = assessmentConfig.CourseId });
                }

                // Get the current student
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found in Details action");
                    return RedirectToAction("Login", "Account");
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);
                if (student == null)
                {
                    _logger.LogWarning($"Student record not found for user {user.UserName}");
                    return NotFound("Student record not found.");
                }

                // Check if student is enrolled in the course
                var isEnrolled = await _context.StudentCourseRegistrations
                    .AnyAsync(r => r.StudentId == student.Id && r.CourseId == assessmentConfig.CourseId);
                if (!isEnrolled)
                {
                    _logger.LogWarning($"Student {student.Id} not enrolled in course {assessmentConfig.CourseId}");
                    TempData["Error"] = "You are not enrolled in this course.";
                    return RedirectToAction("MyCourses", "Course");
                }

                // Check for existing attempts
                var studentIdString = student.Id.ToString();
                var existingAttempt = await _context.StudentAttempts
                    .FirstOrDefaultAsync(a => a.StudentId == studentIdString && a.AssessmentConfigurationId == id);

                // Create view model
                var viewModel = new AssessmentDetailsViewModel
                {
                    Assessment = assessmentConfig,
                    StudentAttempt = existingAttempt,
                    TimeRemaining = assessmentConfig.EndDateTime - currentTime,
                    HasExistingAttempt = existingAttempt != null,
                    CanStart = true // Default to true, we'll set to false based on conditions
                };

                // Determine if student can start/continue the assessment
                if (existingAttempt != null)
                {
                    _logger.LogInformation($"Existing attempt found for student {student.Id}, assessment {id}, status: {existingAttempt.Status}");

                    if (existingAttempt.Status == "Submitted" || existingAttempt.Status == "TimedOut")
                    {
                        viewModel.CanStart = false;
                        viewModel.ActionButtonText = "View Results";
                        viewModel.ActionButtonUrl = Url.Action("Results", "StudentAssessment", new { id = existingAttempt.Id });
                    }
                    else if (existingAttempt.Status == "InProgress")
                    {
                        viewModel.CanStart = true;
                        viewModel.ActionButtonText = "Continue Assessment";
                        viewModel.ActionButtonUrl = Url.Action("Take", "StudentAssessment", new { id = existingAttempt.Id });

                        // Calculate remaining time for this attempt
                        if (existingAttempt.StartTime != default(DateTime)) // Check if it has a meaningful value
                        {
                            TimeSpan attemptTimeLimit = TimeSpan.FromMinutes(assessmentConfig.DurationMinutes);
                            TimeSpan timeElapsed = currentTime - existingAttempt.StartTime;
                            TimeSpan remainingTime = attemptTimeLimit - timeElapsed;

                            _logger.LogInformation($"Attempt time calculation: limit={attemptTimeLimit}, elapsed={timeElapsed}, remaining={remainingTime}");

                            if (remainingTime <= TimeSpan.Zero)
                            {
                                // Time has expired, will be handled in the Take action
                                viewModel.TimeExpiryWarning = "Your allotted time for this assessment has expired. The assessment will be automatically submitted when you continue.";
                            }
                            else
                            {
                                viewModel.AttemptTimeRemaining = remainingTime;
                            }
                        }
                    }
                }
                else
                {
                    viewModel.ActionButtonText = "Start Assessment";
                    viewModel.ActionButtonUrl = Url.Action("Start", "StudentAssessment", new { id = assessmentConfig.Id });
                }

                _logger.LogInformation($"Details action completed successfully for assessment {id}");
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Details action for assessment id: {id}");
                TempData["Error"] = "An error occurred while loading the assessment. Please try again later.";
                return RedirectToAction("MyCourses", "Course");
            }
        }

        // GET: StudentAssessment/Start/{id}
        public async Task<IActionResult> Start(int id)
        {
            try
            {
                _logger.LogInformation($"Start action called for assessment id: {id}");

                // Get the assessment configuration
                var assessmentConfig = await _context.AssessmentConfigurations
                    .Include(a => a.Assessment)
                    .Include(a => a.Course)
                    .FirstOrDefaultAsync(a => a.Id == id && a.IsPublished);

                if (assessmentConfig == null)
                {
                    _logger.LogWarning($"Assessment configuration not found or not published. Id: {id}");
                    TempData["Error"] = "Assessment not found or not available.";
                    return RedirectToAction("MyCourses", "Course");
                }

                // Verify assessment is within the time window
                var currentTime = DateTime.Now;
                _logger.LogInformation($"Current time: {currentTime}, Assessment start: {assessmentConfig.StartDateTime}, Assessment end: {assessmentConfig.EndDateTime}");

                if (currentTime < assessmentConfig.StartDateTime || currentTime > assessmentConfig.EndDateTime)
                {
                    _logger.LogWarning($"Assessment {id} not available at current time {currentTime}");
                    TempData["Error"] = "This assessment is not currently available.";
                    return RedirectToAction("ViewCourseContent", "Course", new { id = assessmentConfig.CourseId });
                }

                // Get the current student
                var student = await GetCurrentStudentAsync();
                if (student == null)
                {
                    _logger.LogWarning("Student record not found in Start action");
                    return NotFound("Student record not found.");
                }

                // Check if student is enrolled in the course
                var isEnrolled = await _context.StudentCourseRegistrations
                    .AnyAsync(r => r.StudentId == student.Id && r.CourseId == assessmentConfig.CourseId);

                if (!isEnrolled)
                {
                    _logger.LogWarning($"Student {student.Id} not enrolled in course {assessmentConfig.CourseId}");
                    TempData["Error"] = "You are not enrolled in this course.";
                    return RedirectToAction("MyCourses", "Course");
                }

                // Check for existing attempts
                var studentIdString = student.Id.ToString();
                var existingAttempt = await _context.StudentAttempts
                    .FirstOrDefaultAsync(a => a.StudentId == studentIdString && a.AssessmentConfigurationId == id);

                if (existingAttempt != null)
                {
                    _logger.LogInformation($"Existing attempt found for student {student.Id}, assessment {id}, status: {existingAttempt.Status}");

                    if (existingAttempt.Status == "Submitted" || existingAttempt.Status == "TimedOut")
                    {
                        _logger.LogWarning($"Attempt {existingAttempt.Id} already submitted");
                        TempData["Error"] = "You have already submitted this assessment.";
                        return RedirectToAction("Details", new { id });
                    }

                    if (existingAttempt.Status == "InProgress")
                    {
                        // Calculate if the attempt has timed out
                        var timeLimit = TimeSpan.FromMinutes(assessmentConfig.DurationMinutes);
                        var elapsed = currentTime - existingAttempt.StartTime;

                        _logger.LogInformation($"Checking time for attempt {existingAttempt.Id}: limit={timeLimit}, elapsed={elapsed}");

                        if (elapsed > timeLimit)
                        {
                            // Auto-submit the attempt as it has timed out
                            existingAttempt.Status = "TimedOut";
                            existingAttempt.EndTime = existingAttempt.StartTime.Add(timeLimit);
                            existingAttempt.UpdatedBy = User.Identity.Name;
                            existingAttempt.UpdatedAt = currentTime;

                            await _context.SaveChangesAsync();

                            _logger.LogWarning($"Attempt {existingAttempt.Id} has timed out and auto-submitted");
                            TempData["Error"] = "Your assessment time has expired and has been automatically submitted.";
                            return RedirectToAction("Details", new { id });
                        }

                        // Continue with existing attempt
                        return RedirectToAction("Take", new { id = existingAttempt.Id });
                    }
                }

                // Create new attempt
                var studentAttempt = new StudentAttempt
                {
                    StudentId = studentIdString,
                    AssessmentConfigurationId = id,
                    StartTime = currentTime,
                    Status = "InProgress",
                    CreatedBy = User.Identity.Name,
                    CreatedAt = currentTime
                };

                _context.StudentAttempts.Add(studentAttempt);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"New attempt created: {studentAttempt.Id} for student {student.Id}, assessment {id}");

                // Load questions for this attempt
                await LoadQuestionsForAttemptAsync(studentAttempt, assessmentConfig);

                return RedirectToAction("Take", new { id = studentAttempt.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Start action for assessment id: {id}");
                TempData["Error"] = "An error occurred while starting the assessment. Please try again later.";
                return RedirectToAction("MyCourses", "Course");
            }
        }

        // GET: StudentAssessment/Take/{id}
        public async Task<IActionResult> Take(int id, int questionIndex = 0)
        {
            try
            {
                _logger.LogInformation($"Take action called for attempt id: {id}, question index: {questionIndex}");

                // Get student attempt with responses
                var studentAttempt = await _context.StudentAttempts
                    .Include(a => a.Responses)
                        .ThenInclude(r => r.Question)
                            .ThenInclude(q => q.Options)
                    .Include(a => a.AssessmentConfiguration)
                        .ThenInclude(ac => ac.Assessment)
                    .Include(a => a.AssessmentConfiguration)
                        .ThenInclude(ac => ac.Course)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (studentAttempt == null)
                {
                    _logger.LogWarning($"Attempt {id} not found");
                    TempData["Error"] = "Assessment attempt not found.";
                    return RedirectToAction("MyCourses", "Course");
                }

                // Check if this attempt belongs to the current student
                var student = await GetCurrentStudentAsync();
                if (student == null || student.Id.ToString() != studentAttempt.StudentId)
                {
                    _logger.LogWarning($"Unauthorized access attempt - student ID mismatch for attempt {id}");
                    return Forbid();
                }

                // Check status
                if (studentAttempt.Status == "Submitted" || studentAttempt.Status == "TimedOut")
                {
                    _logger.LogWarning($"Attempt {id} already submitted, redirecting to results");
                    TempData["Info"] = "This assessment has already been submitted.";
                    return RedirectToAction("Results", new { id });
                }

                // Check if the attempt has timed out
                var currentTime = DateTime.Now;
                var timeLimit = TimeSpan.FromMinutes(studentAttempt.AssessmentConfiguration.DurationMinutes);
                var elapsed = currentTime - studentAttempt.StartTime;

                _logger.LogInformation($"Time check for attempt {id}: limit={timeLimit}, elapsed={elapsed}");

                if (elapsed > timeLimit)
                {
                    // Auto-submit the attempt as it has timed out
                    studentAttempt.Status = "TimedOut";
                    studentAttempt.EndTime = studentAttempt.StartTime.Add(timeLimit);
                    studentAttempt.UpdatedBy = User.Identity.Name;
                    studentAttempt.UpdatedAt = currentTime;

                    await _context.SaveChangesAsync();

                    _logger.LogWarning($"Attempt {id} has timed out during Take action, auto-submitted");
                    TempData["Error"] = "Your assessment time has expired and has been automatically submitted.";
                    return RedirectToAction("Results", new { id });
                }

                // Get all responses ordered by ID (to maintain consistent order)
                var responses = studentAttempt.Responses.OrderBy(r => r.Id).ToList();

                // Validate question index
                if (questionIndex < 0 || questionIndex >= responses.Count)
                {
                    _logger.LogWarning($"Invalid question index {questionIndex} for attempt {id}, defaulting to 0");
                    questionIndex = 0;
                }

                // Get current question and response
                var currentResponse = responses[questionIndex];
                var currentQuestion = currentResponse.Question;

                _logger.LogInformation($"Showing question {currentQuestion.Id} for attempt {id}");

                // Create view model
                var viewModel = new AssessmentTakeViewModel
                {
                    StudentAttempt = studentAttempt,
                    CurrentResponse = currentResponse,
                    CurrentQuestion = currentQuestion,
                    CurrentIndex = questionIndex,
                    TotalQuestions = responses.Count,
                    TimeRemaining = timeLimit - elapsed,
                    ResponsesNavigation = responses.Select((r, index) => new ResponseNavigationItem
                    {
                        Index = index,
                        QuestionId = r.QuestionId,
                        HasResponse = !string.IsNullOrEmpty(r.ResponseText),
                        IsCurrentQuestion = index == questionIndex
                    }).ToList(),
                    // Add flag to indicate if manual grading is required for this assessment
                    RequiresManualGrading = responses.Any(r =>
                        r.Question.QuestionType == "ShortAnswer" ||
                        r.Question.QuestionType == "LongText")
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Take action for attempt id: {id}, question index: {questionIndex}");
                TempData["Error"] = "An error occurred while loading the assessment. Please try again later.";
                return RedirectToAction("MyCourses", "Course");
            }
        }

        // POST: StudentAssessment/SubmitAnswer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAnswer(int responseId, string responseText, int questionIndex, int attemptId, bool navigateToNext = false)
        {
            try
            {
                _logger.LogInformation($"SubmitAnswer called for response {responseId}, attempt {attemptId}");

                // Get the response to update
                var response = await _context.StudentResponses
                    .Include(r => r.StudentAttempt)
                    .FirstOrDefaultAsync(r => r.Id == responseId);

                if (response == null)
                {
                    _logger.LogWarning($"Response {responseId} not found");
                    return Json(new { success = false, message = "Response not found" });
                }

                // Check if this response belongs to the current student
                var student = await GetCurrentStudentAsync();
                if (student == null || student.Id.ToString() != response.StudentAttempt.StudentId)
                {
                    _logger.LogWarning($"Unauthorized access attempt for response {responseId}");
                    return Json(new { success = false, message = "Unauthorized" });
                }

                // Check if attempt is still in progress
                if (response.StudentAttempt.Status != "InProgress")
                {
                    _logger.LogWarning($"Attempt {response.StudentAttemptId} not in progress");
                    return Json(new { success = false, message = "Assessment has already been submitted" });
                }

                // Update the response
                response.ResponseText = responseText;
                response.UpdatedBy = User.Identity.Name;
                response.UpdatedAt = DateTime.Now;

                // For multiple choice, we can do auto-grading here
                var question = await _context.Questions
                    .Include(q => q.Options)
                    .FirstOrDefaultAsync(q => q.Id == response.QuestionId);

                if (question.QuestionType == "MultipleChoice" || question.QuestionType == "TrueFalse")
                {
                    // Parse the responseText as selected option ID(s)
                    if (int.TryParse(responseText, out int selectedOptionId))
                    {
                        var selectedOption = question.Options.FirstOrDefault(o => o.Id == selectedOptionId);
                        if (selectedOption != null)
                        {
                            response.IsCorrect = selectedOption.IsCorrect;
                            response.Score = selectedOption.IsCorrect ? question.Points : 0;
                            response.IsGraded = true;

                            _logger.LogInformation($"Auto-graded response {responseId}: correct={response.IsCorrect}, score={response.Score}");
                        }
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Response {responseId} saved successfully");

                // Determine where to navigate next
                int nextIndex = questionIndex;
                if (navigateToNext)
                {
                    var totalQuestions = await _context.StudentResponses
                        .CountAsync(r => r.StudentAttemptId == attemptId);

                    nextIndex = (questionIndex + 1) % totalQuestions;
                    _logger.LogInformation($"Navigating to next question, index: {nextIndex}");
                }

                return Json(new
                {
                    success = true,
                    nextUrl = Url.Action("Take", new { id = attemptId, questionIndex = nextIndex })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SubmitAnswer for response {responseId}, attempt {attemptId}");
                return Json(new { success = false, message = "An error occurred while saving your answer." });
            }
        }

        // GET: StudentAssessment/Navigate
        public async Task<IActionResult> Navigate(int attemptId, int questionIndex)
        {
            try
            {
                _logger.LogInformation($"Navigate called for attempt {attemptId}, question index: {questionIndex}");

                // Verify the attempt belongs to the current student
                var studentAttempt = await _context.StudentAttempts
                    .FirstOrDefaultAsync(a => a.Id == attemptId);

                if (studentAttempt == null)
                {
                    _logger.LogWarning($"Attempt {attemptId} not found");
                    TempData["Error"] = "Assessment attempt not found.";
                    return RedirectToAction("MyCourses", "Course");
                }

                var student = await GetCurrentStudentAsync();
                if (student == null || student.Id.ToString() != studentAttempt.StudentId)
                {
                    _logger.LogWarning($"Unauthorized access attempt for navigation to attempt {attemptId}");
                    return Forbid();
                }

                return RedirectToAction("Take", new { id = attemptId, questionIndex });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Navigate for attempt {attemptId}, question index: {questionIndex}");
                TempData["Error"] = "An error occurred while navigating to the question. Please try again.";
                return RedirectToAction("MyCourses", "Course");
            }
        }

        // POST: StudentAssessment/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int attemptId)
        {
            try
            {
                _logger.LogInformation($"Submit called for attempt {attemptId}");

                // Get the attempt
                var studentAttempt = await _context.StudentAttempts
                    .Include(a => a.Responses)
                    .Include(a => a.AssessmentConfiguration)
                        .ThenInclude(ac => ac.Assessment)
                    .FirstOrDefaultAsync(a => a.Id == attemptId);

                if (studentAttempt == null)
                {
                    _logger.LogWarning($"Attempt {attemptId} not found during submission");
                    TempData["Error"] = "Assessment attempt not found.";
                    return RedirectToAction("MyCourses", "Course");
                }

                // Check if this attempt belongs to the current student
                var student = await GetCurrentStudentAsync();
                if (student == null || student.Id.ToString() != studentAttempt.StudentId)
                {
                    _logger.LogWarning($"Unauthorized submission attempt for {attemptId}");
                    return Forbid();
                }

                // Check if already submitted
                if (studentAttempt.Status == "Submitted" || studentAttempt.Status == "TimedOut")
                {
                    _logger.LogWarning($"Attempt {attemptId} already submitted");
                    TempData["Info"] = "This assessment has already been submitted.";
                    return RedirectToAction("Results", new { id = attemptId });
                }

                // Submit the attempt
                studentAttempt.Status = "Submitted";
                studentAttempt.EndTime = DateTime.Now;
                studentAttempt.UpdatedBy = User.Identity.Name;
                studentAttempt.UpdatedAt = DateTime.Now;

                _logger.LogInformation($"Marking attempt {attemptId} as submitted");

                // Calculate score for auto-graded questions
                if (studentAttempt.Responses.Any(r => r.IsGraded))
                {
                    var totalPossiblePoints = await _context.Questions
                        .Where(q => studentAttempt.Responses.Select(r => r.QuestionId).Contains(q.Id))
                        .SumAsync(q => q.Points);

                    var earnedPoints = studentAttempt.Responses
                        .Where(r => r.IsGraded)
                        .Sum(r => r.Score ?? 0);

                    studentAttempt.TotalScore = earnedPoints;

                    if (totalPossiblePoints > 0)
                    {
                        studentAttempt.Percentage = (decimal)(earnedPoints / totalPossiblePoints * 100);

                        // Use the assessment's pass mark instead of a fixed value
                        decimal passMark = studentAttempt.AssessmentConfiguration.Assessment.PassMark;
                        studentAttempt.Passed = studentAttempt.Percentage >= passMark;

                        _logger.LogInformation($"Score calculated for attempt {attemptId}: {earnedPoints}/{totalPossiblePoints} = {studentAttempt.Percentage}%, Pass Mark: {passMark}");
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Attempt {attemptId} successfully submitted");

                TempData["Success"] = "Your assessment has been submitted successfully.";
                return RedirectToAction("Results", new { id = attemptId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Submit for attempt {attemptId}");
                TempData["Error"] = "An error occurred while submitting your assessment. Please try again.";
                return RedirectToAction("Take", new { id = attemptId });
            }
        }

        // POST: StudentAssessment/AutoSave
        [HttpPost]
        public async Task<IActionResult> AutoSave(int responseId, string responseText)
        {
            try
            {
                // This is a simplified version for auto-saving without navigation
                var response = await _context.StudentResponses
                    .Include(r => r.StudentAttempt)
                    .FirstOrDefaultAsync(r => r.Id == responseId);

                if (response == null)
                {
                    _logger.LogWarning($"Response {responseId} not found during autosave");
                    return Json(new { success = false });
                }

                // Check if this response belongs to the current student
                var student = await GetCurrentStudentAsync();
                if (student == null || student.Id.ToString() != response.StudentAttempt.StudentId)
                {
                    _logger.LogWarning($"Unauthorized autosave attempt for response {responseId}");
                    return Json(new { success = false });
                }

                // Check if attempt is still in progress
                if (response.StudentAttempt.Status != "InProgress")
                {
                    _logger.LogWarning($"Attempt {response.StudentAttemptId} not in progress during autosave");
                    return Json(new { success = false });
                }

                // Update the response
                response.ResponseText = responseText;
                response.UpdatedBy = User.Identity.Name;
                response.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                _logger.LogDebug($"Autosave successful for response {responseId}");

                return Json(new { success = true, lastSaved = DateTime.Now.ToString("HH:mm:ss") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in AutoSave for response {responseId}");
                return Json(new { success = false });
            }
        }

        // GET: StudentAssessment/Results/{id}
        public async Task<IActionResult> Results(int id)
        {
            try
            {
                _logger.LogInformation($"Results called for attempt {id}");

                // Get the attempt with all related data
                var studentAttempt = await _context.StudentAttempts
                    .Include(a => a.Responses)
                        .ThenInclude(r => r.Question)
                            .ThenInclude(q => q.Options)
                    .Include(a => a.AssessmentConfiguration)
                        .ThenInclude(ac => ac.Assessment)
                    .Include(a => a.AssessmentConfiguration)
                        .ThenInclude(ac => ac.Course)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (studentAttempt == null)
                {
                    _logger.LogWarning($"Attempt {id} not found during results view");
                    TempData["Error"] = "Assessment attempt not found.";
                    return RedirectToAction("MyCourses", "Course");
                }

                // Check if this attempt belongs to the current student
                var student = await GetCurrentStudentAsync();
                if (student == null || student.Id.ToString() != studentAttempt.StudentId)
                {
                    _logger.LogWarning($"Unauthorized access to results for attempt {id}");
                    return Forbid();
                }

                // If the assessment is not set to show results and it's not fully graded, redirect
                if (!studentAttempt.AssessmentConfiguration.ShowResults &&
                    studentAttempt.Responses.Any(r => !r.IsGraded))
                {
                    _logger.LogInformation($"Results for attempt {id} not viewable yet - waiting for grading");
                    TempData["Info"] = "Results will be available after grading is complete.";
                    return RedirectToAction("ViewCourseContent", "Course",
                        new { id = studentAttempt.AssessmentConfiguration.CourseId });
                }

                // Check if there are questions requiring manual grading
                bool requiresManualGrading = studentAttempt.Responses.Any(r =>
                    r.Question.QuestionType == "ShortAnswer" ||
                    r.Question.QuestionType == "LongText");

                // Check if any responses are not yet graded
                bool hasUnGradedResponses = studentAttempt.Responses.Any(r => !r.IsGraded);

                // Build the view model
                var viewModel = new AssessmentResultsViewModel
                {
                    StudentAttempt = studentAttempt,
                    Responses = studentAttempt.Responses.OrderBy(r => r.Id).ToList(),
                    TotalQuestions = studentAttempt.Responses.Count,
                    TotalAnswered = studentAttempt.Responses.Count(r => !string.IsNullOrEmpty(r.ResponseText)),
                    TotalCorrect = studentAttempt.Responses.Count(r => r.IsCorrect == true),
                    TotalScore = studentAttempt.TotalScore ?? 0,
                    Percentage = studentAttempt.Percentage ?? 0,
                    TimeTaken = studentAttempt.EndTime.HasValue
                        ? studentAttempt.EndTime.Value - studentAttempt.StartTime
                        : TimeSpan.Zero,
                    RequiresManualGrading = requiresManualGrading,
                    HasUnGradedResponses = hasUnGradedResponses,
                    // Get the configured pass mark instead of assuming 50%
                    PassMark = studentAttempt.AssessmentConfiguration.Assessment.PassMark
                };

                _logger.LogInformation($"Results view model created for attempt {id}: score={viewModel.TotalScore}, percentage={viewModel.Percentage}%");
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in Results action for attempt id: {id}");
                TempData["Error"] = "An error occurred while loading the assessment results. Please try again later.";
                return RedirectToAction("MyCourses", "Course");
            }
        }

        // Helper methods
        private async Task<Student> GetCurrentStudentAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found in GetCurrentStudentAsync");
                    return null;
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student == null)
                {
                    _logger.LogWarning($"Student record not found for user {user.UserName}");
                }

                return student;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCurrentStudentAsync helper method");
                return null;
            }
        }

        private async Task LoadQuestionsForAttemptAsync(StudentAttempt attempt, AssessmentConfiguration config)
        {
            try
            {
                _logger.LogInformation($"LoadQuestionsForAttemptAsync called for attempt {attempt.Id}");

                // Load all question groups for this assessment
                var assessmentQuestionGroups = await _context.AssessmentQuestionGroups
                    .Where(aqg => aqg.AssessmentConfigurationId == config.Id)
                    .Include(aqg => aqg.QuestionGroup)
                        .ThenInclude(qg => qg.Questions)
                    .ToListAsync();

                _logger.LogInformation($"Found {assessmentQuestionGroups.Count} question groups for config {config.Id}");

                // Create list to track all selected questions
                var selectedQuestions = new List<Question>();

                // For each question group, select the appropriate number of questions
                foreach (var assessmentQuestionGroup in assessmentQuestionGroups)
                {
                    var questions = assessmentQuestionGroup.QuestionGroup.Questions
                        .Where(q => q.IsActive)
                        .ToList();

                    // Determine how many questions to use from this group
                    int questionsToUse = Math.Min(
                        assessmentQuestionGroup.NumberOfQuestionsToUse,
                        questions.Count);

                    _logger.LogInformation($"For group {assessmentQuestionGroup.QuestionGroupId}: Using {questionsToUse} of {questions.Count} available questions");

                    // If randomization is enabled, select random questions
                    if (config.RandomizeQuestions)
                    {
                        // Shuffle the questions
                        var random = new Random();
                        questions = questions.OrderBy(q => random.Next()).ToList();
                        _logger.LogInformation("Questions randomized");
                    }

                    // Take the required number of questions
                    selectedQuestions.AddRange(questions.Take(questionsToUse));
                }

                _logger.LogInformation($"Total of {selectedQuestions.Count} questions selected for attempt {attempt.Id}");

                // Create responses for each selected question
                foreach (var question in selectedQuestions)
                {
                    var response = new StudentResponse
                    {
                        StudentAttemptId = attempt.Id,
                        QuestionId = question.Id,
                        ResponseText = "",
                        CreatedBy = User.Identity.Name,
                        CreatedAt = DateTime.Now
                    };

                    _context.StudentResponses.Add(response);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Student responses created for attempt {attempt.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in LoadQuestionsForAttemptAsync for attempt {attempt.Id}");
                throw; // Re-throw to be handled by calling method
            }
        }
    }
}