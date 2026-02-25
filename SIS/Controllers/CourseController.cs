using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Registration;
using SIS.Models.Courses;
using SIS.Models.Lecturer;
using SIS.Enums;
using Microsoft.AspNetCore.Authorization;

namespace SIS.Controllers
{
    [Authorize]
    public class CourseController : Controller
    {

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public CourseController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> MyCourses()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Get student information based on username
                var student = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student == null)
                {
                    return NotFound("Student record not found.");
                }

                // Add academic year information to ViewBag
                ViewBag.AcademicYear = student.AcademicYear?.YearValue;
                ViewBag.Semester = student.CurrentSemester;
                ViewBag.RegistrationStatus = student.RegistrationStatus;

                // Check if registration status is Pending
                if (student.RegistrationStatus == Status.Pending)
                {
                    // Return an empty list - the view will display a message based on ViewBag.RegistrationStatus
                    return View(new List<StudentCoursesViewModel>());
                }

                // Get registered courses for the student only if registration is completed
                var courses = await _context.StudentCourseRegistrations
                    .Where(r => r.StudentId == student.Id &&
                               r.AcademicYearId == student.AcademicYearId)
                    .Join(_context.Courses,
                        scr => scr.CourseId,
                        c => c.Id,
                        (scr, c) => new StudentCoursesViewModel
                        {
                            Id = c.Id,
                            CourseCode = c.CourseCode,
                            CourseName = c.CourseName,
                            CourseDescription = c.CourseDescription,
                            IsMandatory = c.IsMandatory,
                            Semester = scr.Semester
                        })
                    .ToListAsync();

                return View(courses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {DateTime.Now} - Error retrieving courses for user: {User?.Identity?.Name}");
                Console.WriteLine($"[ERROR] {DateTime.Now} - Exception: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: Course/ViewCourseContent/{id}
        public async Task<IActionResult> ViewCourseContent(int id, int? chapterId = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Get student information
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);
                if (student == null)
                {
                    return NotFound("Student record not found.");
                }

                // Check if the student is registered for this course
                var isRegistered = await _context.StudentCourseRegistrations
                    .AnyAsync(r => r.StudentId == student.Id && r.CourseId == id);
                if (!isRegistered)
                {
                    TempData["Error"] = "You are not registered for this course.";
                    return RedirectToAction("MyCourses", "Course");
                }

                // Get course details
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.Id == id);
                if (course == null)
                {
                    return NotFound("Course not found.");
                }

                // Get course chapters
                var chapters = await _context.Chapters
                    .Where(ch => ch.CourseId == id && ch.IsActive)
                    .OrderBy(ch => ch.OrderIndex)
                    .ToListAsync();

                // Get student's progress for all chapters in this course
                var chapterProgress = await _context.ChapterProgress
                    .Where(cp => cp.StudentId == student.Id && cp.CourseId == id)
                    .ToDictionaryAsync(cp => cp.ChapterId, cp => cp.IsCompleted);

                // NEW: Get student's ratings for all chapters in this course
                var chapterRatings = await _context.ChapterRatings
                    .Where(cr => cr.StudentId == student.Id && cr.CourseId == id)
                    .ToDictionaryAsync(cr => cr.ChapterId, cr => new ChapterRatingViewModel
                    {
                        ChapterId = cr.ChapterId,
                        CourseId = cr.CourseId,
                        Rating = cr.Rating,
                        ReviewText = cr.ReviewText,
                        HasRated = true,
                        RatedAt = cr.CreatedAt
                    });

                // Determine which chapters are accessible (updated logic with rating requirement)
                var accessibleChapters = new HashSet<int>();
                bool canAccessNextChapter = true; // First chapter is always accessible

                foreach (var chapter in chapters)
                {
                    if (canAccessNextChapter)
                    {
                        accessibleChapters.Add(chapter.Id);
                        // A chapter is completed AND rated before next chapter can be accessed
                        bool isCompleted = chapterProgress.ContainsKey(chapter.Id) && chapterProgress[chapter.Id];
                        bool isRated = chapterRatings.ContainsKey(chapter.Id);
                        canAccessNextChapter = isCompleted && isRated;
                    }
                }

                // If specific chapter is selected, use it. Otherwise, default to first chapter or -1 for uncategorized
                int selectedChapterId = chapterId ?? (chapters.Any() ? chapters.First().Id : -1);

                // Check if the selected chapter is accessible
                if (selectedChapterId != -1 && !accessibleChapters.Contains(selectedChapterId))
                {
                    // If trying to access a locked chapter, redirect to the last accessible chapter
                    var lastAccessible = accessibleChapters.Any() ? accessibleChapters.Max() : -1;
                    TempData["Warning"] = "You must complete and rate previous chapters before accessing this content.";
                    return RedirectToAction("ViewCourseContent", new { id = id, chapterId = lastAccessible });
                }

                // Determine if we're viewing uncategorized content
                bool viewingUncategorized = selectedChapterId == -1;

                // Get course content
                var courseContents = await _context.CourseContents
                    .Where(cc => cc.CourseId == id && cc.IsActive)
                    .OrderByDescending(cc => cc.CreatedAt)
                    .ToListAsync();

                // Group content by chapters
                var uncategorizedContent = courseContents
                    .Where(cc => cc.ChapterId == null)
                    .ToList();

                var contentByChapter = courseContents
                    .Where(cc => cc.ChapterId != null)
                    .GroupBy(cc => cc.ChapterId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Get available assessments for this student
                var currentDateTime = DateTime.Now;
                var assessments = await _context.AssessmentConfigurations
                    .Include(ac => ac.Assessment)
                    .Include(ac => ac.AcademicYear)
                    .Include(ac => ac.ModeOfStudy)
                    .Where(ac => ac.CourseId == id && ac.IsPublished)
                    .OrderBy(ac => ac.EndDateTime)
                    .ToListAsync();

                // Group assessments by chapters
                var uncategorizedAssessments = assessments
                    .Where(a => a.ChapterId == null)
                    .ToList();

                var assessmentsByChapter = assessments
                    .Where(a => a.ChapterId != null)
                    .GroupBy(a => a.ChapterId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Get current assessments (within time window)
                var availableAssessments = assessments
                    .Where(ac =>
                        ac.StartDateTime <= currentDateTime &&
                        ac.EndDateTime >= currentDateTime)
                    .ToList();

                // Get student's previous attempts
                var studentIdString = student.Id.ToString();
                var studentAttempts = await _context.StudentAttempts
                    .Where(sa => sa.StudentId == studentIdString)
                    .ToListAsync();

                // Process assessment data to include attempt information
                var assessmentData = availableAssessments.Select(assessment => new
                {
                    Assessment = assessment,
                    StudentAttempt = studentAttempts
                        .FirstOrDefault(sa => sa.AssessmentConfigurationId == assessment.Id),
                    TimeRemaining = assessment.EndDateTime - currentDateTime
                }).ToList();

                // Setup ViewBag data
                ViewBag.Course = course;
                ViewBag.Chapters = chapters;
                ViewBag.UncategorizedContent = uncategorizedContent;
                ViewBag.ContentByChapter = contentByChapter;
                ViewBag.UncategorizedAssessments = uncategorizedAssessments;
                ViewBag.AssessmentsByChapter = assessmentsByChapter;
                ViewBag.AssessmentData = assessmentData;
                ViewBag.SelectedChapterId = selectedChapterId;
                ViewBag.ViewingUncategorized = viewingUncategorized;
                ViewBag.StudentId = student.Id.ToString();

                // Add the chapter progress and accessibility information to ViewBag
                ViewBag.ChapterProgress = chapterProgress;
                ViewBag.AccessibleChapters = accessibleChapters;

                // NEW: Add chapter ratings to ViewBag
                ViewBag.ChapterRatings = chapterRatings;

                // Calculate overall course progress
                int totalChapters = chapters.Count;
                int completedChapters = chapterProgress.Count(cp => cp.Value == true);
                int progressPercentage = totalChapters > 0 ? (completedChapters * 100) / totalChapters : 0;

                ViewBag.CompletedChapters = completedChapters;
                ViewBag.TotalChapters = totalChapters;
                ViewBag.ProgressPercentage = progressPercentage;

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {DateTime.Now} - Error retrieving course content for course ID: {id}");
                Console.WriteLine($"[ERROR] {DateTime.Now} - Exception: {ex.Message}");
                TempData["Error"] = "An error occurred while retrieving course content.";
                return RedirectToAction("MyCourses", "Course");
            }
        }

        // POST: api/ChapterProgress/MarkComplete
        [HttpPost]
        public async Task<IActionResult> MarkChapterComplete([FromBody] ChapterProgressRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Get current user
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized("User not authenticated.");
                }

                // Get student information
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);
                if (student == null)
                {
                    return NotFound("Student record not found.");
                }

                // Verify the student is registered for the course
                var isRegistered = await _context.StudentCourseRegistrations
                    .AnyAsync(r => r.StudentId == student.Id && r.CourseId == request.CourseId);
                if (!isRegistered)
                {
                    return BadRequest("You are not registered for this course.");
                }

                // Validate the chapter exists and belongs to the course
                var chapter = await _context.Chapters
                    .FirstOrDefaultAsync(c => c.Id == request.ChapterId && c.CourseId == request.CourseId);
                if (chapter == null)
                {
                    return NotFound("Chapter not found or does not belong to this course.");
                }

                // Check if there are any required assessments for this chapter
                var requiredAssessments = await _context.AssessmentConfigurations
                    .Where(a => a.ChapterId == request.ChapterId && a.IsPublished)
                    .ToListAsync();

                // If there are assessments, check if the student has attempted any
                bool assessmentRequirementMet = true;
                if (requiredAssessments.Any())
                {
                    var studentIdString = student.Id.ToString();
                    var attemptsMade = await _context.StudentAttempts
                        .AnyAsync(sa =>
                            sa.StudentId == studentIdString &&
                            requiredAssessments.Select(ra => ra.Id).Contains(sa.AssessmentConfigurationId));

                    assessmentRequirementMet = attemptsMade;

                    if (!assessmentRequirementMet)
                    {
                        return BadRequest("You must attempt at least one assessment in this chapter before marking it as complete.");
                    }
                }

                // NEW: Check if the student has rated this chapter
                var existingRating = await _context.ChapterRatings
                    .FirstOrDefaultAsync(cr =>
                        cr.StudentId == student.Id &&
                        cr.ChapterId == request.ChapterId &&
                        cr.CourseId == request.CourseId);

                if (existingRating == null)
                {
                    return BadRequest("You must rate this chapter before marking it as complete.");
                }

                // Check if a progress record already exists
                var existingProgress = await _context.ChapterProgress
                    .FirstOrDefaultAsync(cp =>
                        cp.StudentId == student.Id &&
                        cp.ChapterId == request.ChapterId &&
                        cp.CourseId == request.CourseId);

                if (existingProgress != null)
                {
                    // Update existing record
                    existingProgress.IsCompleted = true;
                    existingProgress.CompletedDate = DateTime.Now;
                    existingProgress.AssessmentAttempted = assessmentRequirementMet;
                    existingProgress.UpdatedAt = DateTime.Now;

                    _context.ChapterProgress.Update(existingProgress);
                }
                else
                {
                    // Create new progress record
                    var newProgress = new ChapterProgress
                    {
                        StudentId = student.Id,
                        ChapterId = request.ChapterId,
                        CourseId = request.CourseId,
                        IsCompleted = true,
                        CompletedDate = DateTime.Now,
                        AssessmentAttempted = assessmentRequirementMet,
                        CreatedAt = DateTime.Now
                    };

                    await _context.ChapterProgress.AddAsync(newProgress);
                }

                await _context.SaveChangesAsync();

                // Get next chapter (if any)
                var nextChapter = await _context.Chapters
                    .Where(c => c.CourseId == request.CourseId && c.OrderIndex > chapter.OrderIndex)
                    .OrderBy(c => c.OrderIndex)
                    .FirstOrDefaultAsync();

                return Ok(new
                {
                    success = true,
                    message = "Chapter marked as complete successfully.",
                    nextChapterId = nextChapter?.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // NEW: GET Chapter Rating
        [HttpGet]
        public async Task<IActionResult> GetChapterRating(int chapterId, int courseId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized("User not authenticated.");
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);
                if (student == null)
                {
                    return NotFound("Student record not found.");
                }

                var rating = await _context.ChapterRatings
                    .FirstOrDefaultAsync(cr =>
                        cr.StudentId == student.Id &&
                        cr.ChapterId == chapterId &&
                        cr.CourseId == courseId);

                var ratingViewModel = new ChapterRatingViewModel
                {
                    ChapterId = chapterId,
                    CourseId = courseId,
                    HasRated = rating != null
                };

                if (rating != null)
                {
                    ratingViewModel.Rating = rating.Rating;
                    ratingViewModel.ReviewText = rating.ReviewText;
                    ratingViewModel.RatedAt = rating.CreatedAt;
                }

                return Ok(ratingViewModel);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // NEW: Submit Chapter Rating
        [HttpPost]
        public async Task<IActionResult> SubmitChapterRating([FromBody] ChapterRatingRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized("User not authenticated.");
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);
                if (student == null)
                {
                    return NotFound("Student record not found.");
                }

                // Verify the student is registered for the course
                var isRegistered = await _context.StudentCourseRegistrations
                    .AnyAsync(r => r.StudentId == student.Id && r.CourseId == request.CourseId);
                if (!isRegistered)
                {
                    return BadRequest("You are not registered for this course.");
                }

                // Validate the chapter exists and belongs to the course
                var chapter = await _context.Chapters
                    .FirstOrDefaultAsync(c => c.Id == request.ChapterId && c.CourseId == request.CourseId);
                if (chapter == null)
                {
                    return NotFound("Chapter not found or does not belong to this course.");
                }

                // Check if a rating already exists
                var existingRating = await _context.ChapterRatings
                    .FirstOrDefaultAsync(cr =>
                        cr.StudentId == student.Id &&
                        cr.ChapterId == request.ChapterId &&
                        cr.CourseId == request.CourseId);

                if (existingRating != null)
                {
                    // Update existing rating
                    existingRating.Rating = request.Rating;
                    existingRating.ReviewText = request.ReviewText?.Trim();
                    existingRating.UpdatedAt = DateTime.Now;

                    _context.ChapterRatings.Update(existingRating);
                }
                else
                {
                    // Create new rating
                    var newRating = new ChapterRating
                    {
                        StudentId = student.Id,
                        ChapterId = request.ChapterId,
                        CourseId = request.CourseId,
                        Rating = request.Rating,
                        ReviewText = request.ReviewText?.Trim(),
                        CreatedAt = DateTime.Now
                    };

                    await _context.ChapterRatings.AddAsync(newRating);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = existingRating != null ? "Chapter rating updated successfully." : "Chapter rating submitted successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // GET: api/ChapterProgress/CanAccessChapter/5
        [HttpGet]
        public async Task<IActionResult> CanAccessChapter(int chapterId)
        {
            try
            {
                // Get current user
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized("User not authenticated.");
                }

                // Get student information
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);
                if (student == null)
                {
                    return NotFound("Student record not found.");
                }

                // Get the chapter info
                var chapter = await _context.Chapters
                    .FirstOrDefaultAsync(c => c.Id == chapterId);
                if (chapter == null)
                {
                    return NotFound("Chapter not found.");
                }

                // Get the course's chapters in order
                var courseChapters = await _context.Chapters
                    .Where(c => c.CourseId == chapter.CourseId)
                    .OrderBy(c => c.OrderIndex)
                    .ToListAsync();

                // Check if this is the first chapter (always accessible)
                if (courseChapters.First().Id == chapterId)
                {
                    return Ok(new { canAccess = true, reason = "First chapter is always accessible." });
                }

                // Find previous chapter
                int currentIndex = -1;
                for (int i = 0; i < courseChapters.Count; i++)
                {
                    if (courseChapters[i].Id == chapterId)
                    {
                        currentIndex = i;
                        break;
                    }
                }

                if (currentIndex > 0)
                {
                    var previousChapter = courseChapters[currentIndex - 1];

                    // Check if previous chapter was completed
                    var previousChapterProgress = await _context.ChapterProgress
                        .FirstOrDefaultAsync(cp =>
                            cp.StudentId == student.Id &&
                            cp.ChapterId == previousChapter.Id &&
                            cp.IsCompleted);

                    if (previousChapterProgress == null)
                    {
                        return Ok(new
                        {
                            canAccess = false,
                            reason = $"You must complete the previous chapter '{previousChapter.Title}' first."
                        });
                    }

                    // NEW: Check if previous chapter was rated
                    var previousChapterRating = await _context.ChapterRatings
                        .FirstOrDefaultAsync(cr =>
                            cr.StudentId == student.Id &&
                            cr.ChapterId == previousChapter.Id);

                    if (previousChapterRating == null)
                    {
                        return Ok(new
                        {
                            canAccess = false,
                            reason = $"You must rate the previous chapter '{previousChapter.Title}' before proceeding."
                        });
                    }
                }

                return Ok(new { canAccess = true, reason = "Chapter is accessible." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }

    // Request model for marking a chapter complete
    public class ChapterProgressRequest
    {
        public int ChapterId { get; set; }
        public int CourseId { get; set; }
    }
}