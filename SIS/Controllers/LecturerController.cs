using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIS.Data;
using SIS.DTOs;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Assessments;
using SIS.Models.Lecturer;
using System.Linq;
using System.Security.Claims;

namespace SIS.Controllers
{
    public class LecturerController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public LecturerController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }
        // Updated Index method for dynamic lecturer dashboard
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Index()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get lecturer's courses
            var lecturerCourses = await _context.Courses
                .Include(c => c.Programme)
                    .ThenInclude(p => p.Department)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                            c.InstructorId == currentUserId)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            var courseIds = lecturerCourses.Select(c => c.Id).ToList();

            // Get total students across all lecturer's courses (from current registrations)
            var totalStudents = await _context.StudentCourseRegistrations
                .Where(scr => courseIds.Contains(scr.CourseId))
                .Select(scr => scr.StudentId)
                .Distinct()
                .CountAsync();

            // ⭐ UPDATED: Get the most recent active academic year and semester
            var mostRecentAcademicYear = await _context.AcademicYears
                .Where(ay => ay.IsActive)
                .OrderByDescending(ay => ay.YearValue)
                .ThenByDescending(ay => ay.SemesterId)
                .FirstOrDefaultAsync();

            int? currentAcademicYearId = mostRecentAcademicYear?.YearId;
            int? currentSemester = mostRecentAcademicYear?.SemesterId;

            // ⭐ UPDATED: Count pending assessments from StudentCourseResults (unpublished results)
            var pendingAssessments = 0;
            if (currentAcademicYearId.HasValue && currentSemester.HasValue)
            {
                pendingAssessments = await _context.StudentCourseResults
                    .Where(r => courseIds.Contains(r.CourseId) &&
                               r.AcademicYearId == currentAcademicYearId.Value &&
                               r.Semester == currentSemester.Value &&
                               r.Status != Status.Published)
                    .CountAsync();
            }

            // ⭐ UPDATED: Calculate grading progress from StudentCourseResults and StudentAssessmentScores
            decimal gradingProgress = 0;
            if (currentAcademicYearId.HasValue && currentSemester.HasValue)
            {
                // Get all course results for lecturer's courses in current semester
                var courseResults = await _context.StudentCourseResults
                    .Include(r => r.Course)
                        .ThenInclude(c => c.CourseAssessments)
                    .Where(r => courseIds.Contains(r.CourseId) &&
                               r.AcademicYearId == currentAcademicYearId.Value &&
                               r.Semester == currentSemester.Value)
                    .ToListAsync();

                if (courseResults.Any())
                {
                    // Calculate total required assessments
                    int totalRequiredScores = 0;
                    int totalRecordedScores = 0;

                    foreach (var result in courseResults)
                    {
                        // Get expected number of assessments for this course
                        var expectedAssessments = result.Course?.CourseAssessments?.Count ?? 0;
                        totalRequiredScores += expectedAssessments;

                        // Count actual recorded scores for this student-course combination
                        var recordedScores = await _context.StudentAssessmentScores
                            .CountAsync(s => s.StudentId == result.StudentId &&
                                            s.CourseId == result.CourseId &&
                                            s.AcademicYearId == result.AcademicYearId &&
                                            s.Semester == result.Semester &&
                                            s.IsActive &&
                                            s.Score > 0);

                        totalRecordedScores += recordedScores;
                    }

                    if (totalRequiredScores > 0)
                    {
                        gradingProgress = Math.Round((decimal)totalRecordedScores / totalRequiredScores * 100, 1);
                    }
                }
            }

            // ⭐ UPDATED: Get assessment trends from StudentCourseResults (most recent semester)
            var gradeTrends = new Dictionary<string, object>();

            if (currentAcademicYearId.HasValue && currentSemester.HasValue)
            {
                foreach (var course in lecturerCourses.Take(5)) // Limit to 5 courses for performance
                {
                    // Get published results for this course in the current semester
                    var publishedResults = await _context.StudentCourseResults
                        .Include(r => r.Course)
                        .Where(r => r.CourseId == course.Id &&
                                   r.AcademicYearId == currentAcademicYearId.Value &&
                                   r.Semester == currentSemester.Value &&
                                   r.Status == Status.Published &&
                                   r.NormalizedTotal > 0)
                        .ToListAsync();

                    if (publishedResults.Any())
                    {
                        // Get all unique assessments for this course from the Course navigation property
                        var courseWithAssessments = await _context.Courses
                            .Include(c => c.CourseAssessments)
                                .ThenInclude(ca => ca.Assessment)
                            .FirstOrDefaultAsync(c => c.Id == course.Id);

                        var assessmentNames = new List<string>();
                        var averageScores = new List<decimal>();
                        var highestScores = new List<decimal>();
                        var lowestScores = new List<decimal>();

                        if (courseWithAssessments?.CourseAssessments != null)
                        {
                            var orderedAssessments = courseWithAssessments.CourseAssessments
                                .OrderBy(ca => ca.Assessment.Name)
                                .ToList();

                            foreach (var courseAssessment in orderedAssessments)
                            {
                                // Get all scores for this assessment across all students
                                var assessmentScores = await _context.StudentAssessmentScores
                                    .Where(s => s.CourseId == course.Id &&
                                               s.AssessmentId == courseAssessment.AssessmentId &&
                                               s.AcademicYearId == currentAcademicYearId.Value &&
                                               s.Semester == currentSemester.Value &&
                                               s.IsActive &&
                                               s.Score > 0)
                                    .Select(s => s.Score)
                                    .ToListAsync();

                                if (assessmentScores.Any())
                                {
                                    assessmentNames.Add(courseAssessment.Assessment.Name);
                                    averageScores.Add(Math.Round(assessmentScores.Average(), 1));
                                    highestScores.Add(assessmentScores.Max());
                                    lowestScores.Add(assessmentScores.Min());
                                }
                            }
                        }

                        if (assessmentNames.Any())
                        {
                            gradeTrends[course.CourseCode] = new
                            {
                                assessments = assessmentNames,
                                averageScores = averageScores,
                                highestScores = highestScores,
                                lowestScores = lowestScores,
                                courseName = course.CourseName,
                                semester = $"Semester {currentSemester}",
                                academicYear = mostRecentAcademicYear?.YearValue ?? "N/A"
                            };
                        }
                    }
                }
            }

            // ⭐ UPDATED: Today's schedule - leave empty/message (feature not ready)
            var todaySchedule = new List<object>();

            // ⭐ UPDATED: Get course overview data from StudentCourseResults
            var courseOverview = new List<object>();

            if (currentAcademicYearId.HasValue && currentSemester.HasValue)
            {
                foreach (var course in lecturerCourses.Take(10))
                {
                    // Get enrolled students count
                    var enrolledCount = await _context.StudentCourseRegistrations
                        .CountAsync(scr => scr.CourseId == course.Id &&
                                          scr.AcademicYearId == currentAcademicYearId.Value &&
                                          scr.Semester == currentSemester.Value);

                    // Get average performance from published results
                    var publishedResults = await _context.StudentCourseResults
                        .Where(r => r.CourseId == course.Id &&
                                   r.AcademicYearId == currentAcademicYearId.Value &&
                                   r.Semester == currentSemester.Value &&
                                   r.Status == Status.Published &&
                                   r.NormalizedTotal > 0)
                        .ToListAsync();

                    decimal avgPerformance = 0;
                    if (publishedResults.Any())
                    {
                        avgPerformance = Math.Round(publishedResults.Average(r => r.NormalizedTotal), 1);
                    }

                    // Count graded vs total students
                    var totalStudentsInCourse = await _context.StudentCourseResults
                        .CountAsync(r => r.CourseId == course.Id &&
                                        r.AcademicYearId == currentAcademicYearId.Value &&
                                        r.Semester == currentSemester.Value);

                    var gradedStudents = publishedResults.Count;

                    courseOverview.Add(new
                    {
                        courseId = course.Id,
                        courseName = course.CourseName,
                        courseCode = course.CourseCode,
                        totalStudents = enrolledCount,
                        averagePerformance = avgPerformance > 0 ? $"{avgPerformance}%" : "N/A",
                        gradingStatus = totalStudentsInCourse > 0
                            ? $"{gradedStudents}/{totalStudentsInCourse} graded"
                            : "No students",
                        isFullyGraded = totalStudentsInCourse > 0 && gradedStudents == totalStudentsInCourse
                    });
                }
            }

            // ⭐ UPDATED: Get upcoming deadlines from StudentCourseResults (unpublished results needing attention)
            var upcomingDeadlines = new List<object>();

            if (currentAcademicYearId.HasValue && currentSemester.HasValue)
            {
                // Find courses with incomplete grading
                var coursesNeedingAttention = await _context.StudentCourseResults
                    .Include(r => r.Course)
                    .Where(r => courseIds.Contains(r.CourseId) &&
                               r.AcademicYearId == currentAcademicYearId.Value &&
                               r.Semester == currentSemester.Value &&
                               r.Status != Status.Published)
                    .GroupBy(r => new { r.CourseId, r.Course.CourseCode, r.Course.CourseName })
                    .Select(g => new
                    {
                        CourseId = g.Key.CourseId,
                        CourseCode = g.Key.CourseCode,
                        CourseName = g.Key.CourseName,
                        UnpublishedCount = g.Count()
                    })
                    .OrderByDescending(x => x.UnpublishedCount)
                    .Take(10)
                    .ToListAsync();

                foreach (var course in coursesNeedingAttention)
                {
                    upcomingDeadlines.Add(new
                    {
                        title = $"Complete grading for {course.CourseCode}",
                        description = $"{course.UnpublishedCount} student(s) awaiting grade publication",
                        daysRemaining = 0, // Immediate attention
                        priority = course.UnpublishedCount > 10 ? "high" :
                                  course.UnpublishedCount > 5 ? "medium" : "low",
                        courseId = course.CourseId
                    });
                }
            }

            // Pass data to view
            ViewBag.TotalCourses = lecturerCourses.Count;
            ViewBag.TotalStudents = totalStudents;
            ViewBag.PendingAssessments = pendingAssessments;
            ViewBag.GradingProgress = gradingProgress;
            ViewBag.CurrentSemester = currentSemester;
            ViewBag.CurrentAcademicYear = mostRecentAcademicYear?.YearValue;

            ViewBag.GradeTrends = JsonConvert.SerializeObject(gradeTrends);
            ViewBag.TodaySchedule = JsonConvert.SerializeObject(todaySchedule);
            ViewBag.CourseOverview = JsonConvert.SerializeObject(courseOverview);
            ViewBag.UpcomingDeadlines = JsonConvert.SerializeObject(upcomingDeadlines);
            ViewBag.LecturerCourses = lecturerCourses;

            return View("LecturerDashboard");
        }

        // ⭐ NEW: Helper method to identify exam assessments
        private bool IsExamAssessment(string assessmentName)
        {
            if (string.IsNullOrEmpty(assessmentName)) return false;

            var examNames = new[] { "Exam", "EXAM", "Final Exam", "Final", "Main Exam", "End of Semester Exam" };
            return examNames.Contains(assessmentName, StringComparer.OrdinalIgnoreCase);
        }

        // Action method for Courses page
        public IActionResult Courses()
        {
            // This will be implemented later
            return View();
        }

        // Action method for Students page
        public IActionResult Students()
        {
            // This will be implemented later
            return View();
        }

        // Action method for Assignments page
        public IActionResult Assignments()
        {
            // This will be implemented later
            return View();
        }

        // Action method for Messages page
        public IActionResult Messages()
        {
            // This will be implemented later
            return View();
        }


        // Add this method to your CourseController class
        // POST: Course/DeleteContent
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> DeleteContent(int id)
        {
            try
            {
                var content = await _context.CourseContents.FindAsync(id);

                if (content == null)
                {
                    return NotFound();
                }

                // Verify the lecturer has access to this course
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized();
                }

                // Check if this lecturer has rights to the course
                var course = await _context.Courses
                    .Include(c => c.CourseLecturers)
                    .FirstOrDefaultAsync(c => c.Id == content.CourseId &&
                                         (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                          c.InstructorId == currentUserId));

                if (course == null)
                {
                    return Unauthorized();
                }

                // Store CourseId before removing content for redirection
                int courseId = content.CourseId;

                // Check if it's a file (not a URL) and delete the file from storage
                if (!string.IsNullOrEmpty(content.FilePath) &&
                    !content.FilePath.StartsWith("http://") &&
                    !content.FilePath.StartsWith("https://"))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), content.FilePath);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // Remove from database
                _context.CourseContents.Remove(content);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Course content deleted successfully.";
                return RedirectToAction("CourseContent", new { id = courseId });
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error in DeleteContent: {ex.Message}");
                TempData["Error"] = "An error occurred while deleting the content.";
                return RedirectToAction("LecturerCourses");
            }
        }


        // Lecturer Methods
        // GET: Course/LecturerCourses
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> LecturerCourses()
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Fetch all courses where this lecturer is assigned
            var lecturerCourses = await _context.Courses
                .Include(c => c.Programme)
                    .ThenInclude(p => p.Department)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                            c.InstructorId == currentUserId)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            return View(lecturerCourses);
        }

        // Updated CourseContent method
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> CourseContent(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var course = await _context.Courses
                .Include(c => c.Programme)
                    .ThenInclude(p => p.Department)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .FirstOrDefaultAsync(c => c.Id == id &&
                                         (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                          c.InstructorId == currentUserId));

            if (course == null)
            {
                return NotFound();
            }

            // Fetch course chapters
            var courseChapters = await _context.Chapters
                .Where(ch => ch.CourseId == id && ch.IsActive)
                .OrderBy(ch => ch.OrderIndex)
                .ToListAsync();

            // Fetch course content
            var courseContents = await _context.CourseContents
                .Where(cc => cc.CourseId == id && cc.IsActive)
                .OrderByDescending(cc => cc.CreatedAt)
                .ToListAsync();

            // Get uncategorized content (no chapter)
            var uncategorizedContent = courseContents
                .Where(cc => cc.ChapterId == null)
                .ToList();

            // Group content by chapters
            var contentByChapter = courseContents
                .Where(cc => cc.ChapterId != null)
                .GroupBy(cc => cc.ChapterId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Fetch course assessments with proper includes
            var courseAssessments = await _context.AssessmentConfigurations
                .Include(ac => ac.Assessment)
                .Include(ac => ac.AcademicYear)
                .Include(ac => ac.ModeOfStudy)
                .Where(ac => ac.CourseId == id)
                .OrderByDescending(ac => ac.CreatedAt)
                .ToListAsync();

            var assessmentsByChapter = courseAssessments
                .Where(ac => ac.ChapterId != null)
                .GroupBy(ac => ac.ChapterId)
                .ToDictionary(g => g.Key, g => g.ToList());

            ViewBag.AssessmentsByChapter = assessmentsByChapter;

            // Get course statistics - Use StudentCourseRegistrations instead of StudentCourses
            ViewBag.TotalStudents = await _context.StudentCourseRegistrations.CountAsync(scr => scr.CourseId == id);
            ViewBag.ContentItems = courseContents.Count;

            // Get chapter completion statistics
            var totalChapters = courseChapters.Count;
            var chapterProgressData = await _context.ChapterProgress
                .Where(cp => cp.CourseId == id)
                .ToListAsync();

            // Calculate total completed chapters across all students
            var totalCompletedChapters = chapterProgressData.Count(cp => cp.IsCompleted);

            // Count unique students who have any chapter progress
            var studentsWithProgress = chapterProgressData
                .Select(cp => cp.StudentId)
                .Distinct()
                .Count();

            // Calculate overall completion percentage (if there are students and chapters)
            int completionPercentage = 0;
            if (studentsWithProgress > 0 && totalChapters > 0)
            {
                var possibleCompletions = studentsWithProgress * totalChapters;
                completionPercentage = possibleCompletions > 0
                    ? (totalCompletedChapters * 100) / possibleCompletions
                    : 0;
            }

            // NEW: Calculate progress metrics for improved statistics display

            // Calculate how many students have completed each chapter percentage
            var chapterCompletionRates = new Dictionary<int, double>();
            foreach (var chapter in courseChapters)
            {
                var studentsCompletedChapter = chapterProgressData
                    .Count(cp => cp.ChapterId == chapter.Id && cp.IsCompleted);

                var completionRate = ViewBag.TotalStudents > 0
                    ? (double)studentsCompletedChapter / ViewBag.TotalStudents * 100
                    : 0;

                chapterCompletionRates.Add(chapter.Id, Math.Round((double)completionRate, 1));
            }

            // Get the most and least completed chapters
            int? mostCompletedChapterId = null;
            int? leastCompletedChapterId = null;
            double highestCompletionRate = 0;
            double lowestCompletionRate = 100;

            if (chapterCompletionRates.Any())
            {
                mostCompletedChapterId = chapterCompletionRates
                    .OrderByDescending(c => c.Value)
                    .First().Key;

                leastCompletedChapterId = chapterCompletionRates
                    .OrderBy(c => c.Value)
                    .First().Key;

                highestCompletionRate = chapterCompletionRates[mostCompletedChapterId.Value];
                lowestCompletionRate = chapterCompletionRates[leastCompletedChapterId.Value];
            }

            // Calculate average time to complete chapters (if data is available)
            TimeSpan? averageCompletionTime = null;
            var chaptersWithCompletionTime = chapterProgressData
                .Where(cp => cp.IsCompleted && cp.CompletedDate.HasValue)
                .ToList();

            if (chaptersWithCompletionTime.Any())
            {
                double totalHours = chaptersWithCompletionTime
                    .Sum(cp => (cp.CompletedDate.Value - cp.CreatedAt).TotalHours);

                averageCompletionTime = TimeSpan.FromHours(
                    totalHours / chaptersWithCompletionTime.Count);
            }


            //Calculate course rating statistics
            var courseRatings = await _context.ChapterRatings
                .Where(cr => cr.CourseId == id)
                .ToListAsync();

            // Calculate average rating for this course
            if (courseRatings.Any())
            {
                ViewBag.AverageRating = Math.Round(courseRatings.Average(cr => cr.Rating), 1);
                ViewBag.TotalRatings = courseRatings.Count;
                ViewBag.TotalStudentsRated = courseRatings.Select(cr => cr.StudentId).Distinct().Count();

                // Rating distribution for potential future use
                var ratingDistribution = courseRatings
                    .GroupBy(cr => cr.Rating)
                    .ToDictionary(g => g.Key, g => g.Count());
                ViewBag.RatingDistribution = ratingDistribution;
            }
            else
            {
                ViewBag.AverageRating = 0.0;
                ViewBag.TotalRatings = 0;
                ViewBag.TotalStudentsRated = 0;
                ViewBag.RatingDistribution = new Dictionary<int, int>();
            }

            // Pass all the calculated progress statistics to the view
            ViewBag.StudentsWithProgress = studentsWithProgress;
            ViewBag.CompletedChapters = totalCompletedChapters;
            ViewBag.CompletionPercentage = completionPercentage;
            ViewBag.AssessmentCount = courseAssessments.Count;
            ViewBag.ChapterCompletionRates = chapterCompletionRates;
            ViewBag.MostCompletedChapterId = mostCompletedChapterId;
            ViewBag.LeastCompletedChapterId = leastCompletedChapterId;
            ViewBag.HighestCompletionRate = highestCompletionRate;
            ViewBag.LowestCompletionRate = lowestCompletionRate;
            ViewBag.AverageCompletionTime = averageCompletionTime;

            // Pass the course content and assessments to the view
            ViewBag.CourseChapters = courseChapters;
            ViewBag.UncategorizedContent = uncategorizedContent;
            ViewBag.ContentByChapter = contentByChapter;
            ViewBag.CourseContent = courseContents; // Keep for backward compatibility
            ViewBag.CourseAssessments = courseAssessments;

            return View(course);
        }

        // GET: Course/UploadContent/{id}
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> UploadContent(int? id, int? chapterId = null)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the lecturer has access to this course
            var course = await _context.Courses
                .Include(c => c.CourseLecturers)
                .FirstOrDefaultAsync(c => c.Id == id &&
                                     (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                      c.InstructorId == currentUserId));

            if (course == null)
            {
                return NotFound();
            }

            // Create a new CourseContent model with the CourseId prefilled
            var model = new CourseContent
            {
                CourseId = course.Id,
                ChapterId = chapterId,
                CreatedBy = User.Identity.Name,
                CreatedAt = DateTime.Now,
            };

            // Get available chapters for this course
            var chapters = await _context.Chapters
                .Where(ch => ch.CourseId == id && ch.IsActive)
                .OrderBy(ch => ch.OrderIndex)
                .ToListAsync();

            ViewBag.Course = course;
            ViewBag.Chapters = chapters;
            ViewBag.ContentCategories = new List<string>
            {
                "General",
                "Lecture Notes",
                "Assignment",
                "Reading Material",
                "Tutorial",
                "Video Lecture",
                "Reference Material",
                "Past Paper"
            };

            return View(model);
        }

        // POST: Course/UploadContent
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> UploadContent(CourseContent model)
        {
            model.Id = 0;

            try
            {
                // Check if it's a file upload or a link
                if (model.UploadFile != null)
                {
                    // Handle file upload
                    string uniqueFileName = await ProcessFileUpload(model);

                    // Update model with file information
                    model.FileName = model.UploadFile.FileName;
                    model.FilePath = uniqueFileName;
                    model.FileType = Path.GetExtension(model.FileName).TrimStart('.');
                    model.FileSize = model.UploadFile.Length;
                    model.FileSizeFormatted = FormatFileSize(model.FileSize);
                }
                else if (!string.IsNullOrEmpty(model.FilePath) &&
                         (model.FilePath.StartsWith("http://") || model.FilePath.StartsWith("https://")))
                {
                    // Handle external link
                    model.FileType = "URL";
                    model.FileSize = 0;
                    model.FileSizeFormatted = "0 KB";
                    model.FileName = model.Title;
                }
                else
                {
                    ModelState.AddModelError("", "Please upload a file or provide a valid URL");

                    var course = await _context.Courses.FindAsync(model.CourseId);
                    ViewBag.Course = course;

                    // Get available chapters for this course
                    var chapters = await _context.Chapters
                        .Where(ch => ch.CourseId == model.CourseId && ch.IsActive)
                        .OrderBy(ch => ch.OrderIndex)
                        .ToListAsync();

                    ViewBag.Chapters = chapters;
                    ViewBag.ContentCategories = new List<string>
                    {
                        "General",
                        "Lecture Notes",
                        "Assignment",
                        "Reading Material",
                        "Tutorial",
                        "Video Lecture",
                        "Reference Material",
                        "Past Paper"
                    };

                    return View(model);
                }

                // Set audit fields
                model.CreatedBy = User.Identity.Name;
                model.CreatedAt = DateTime.Now;

                // Save to database
                _context.CourseContents.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Course content uploaded successfully.";
                return RedirectToAction("CourseContent", new { id = model.CourseId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while uploading content: " + ex.Message);
                Console.WriteLine("Error: " + ex.Message);

                var course = await _context.Courses.FindAsync(model.CourseId);
                ViewBag.Course = course;

                // Get available chapters for this course
                var chapters = await _context.Chapters
                    .Where(ch => ch.CourseId == model.CourseId && ch.IsActive)
                    .OrderBy(ch => ch.OrderIndex)
                    .ToListAsync();

                ViewBag.Chapters = chapters;
                ViewBag.ContentCategories = new List<string>
                {
                    "General",
                    "Lecture Notes",
                    "Assignment",
                    "Reading Material",
                    "Tutorial",
                    "Video Lecture",
                    "Reference Material",
                    "Past Paper"
                };

                return View(model);
            }
        }

        // Helper methods
        private async Task<string> ProcessFileUpload(CourseContent model)
        {
            // Create directory if it doesn't exist
            var courseDirPath = Path.Combine("CourseContentUploads", model.CourseId.ToString());
            var fullDirPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", courseDirPath);

            if (!Directory.Exists(fullDirPath))
            {
                Directory.CreateDirectory(fullDirPath);
            }

            // Generate unique file name
            var fileGuid = Guid.NewGuid().ToString();
            var fileName = fileGuid + "_" + model.UploadFile.FileName;
            var filePath = Path.Combine(courseDirPath, fileName);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath);

            // Save file
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await model.UploadFile.CopyToAsync(stream);
            }

            return filePath;
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }


        // GET: Lecturer/CreateAssessmentConfig/{id}
        [HttpGet]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> CreateAssessmentConfig(int? id, int? assessmentConfigId = null)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the lecturer has access to this course
            var course = await _context.Courses
                .Include(c => c.CourseLecturers)
                .Include(c => c.CourseAssessments)
                    .ThenInclude(ca => ca.Assessment)
                .FirstOrDefaultAsync(c => c.Id == id &&
                                   (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                    c.InstructorId == currentUserId));

            if (course == null)
            {
                return NotFound();
            }

            // Get active academic years
            var activeAcademicYears = await _context.AcademicYears
                .Where(ay => ay.IsActive)
                .OrderByDescending(ay => ay.StartDate)
                .ToListAsync();

            // Get modes of study
            var modesOfStudy = await _context.ModesOfStudy
                .OrderBy(m => m.ModeName)
                .ToListAsync();

            AssessmentConfiguration model;

            // Check if we're editing an existing configuration
            if (assessmentConfigId.HasValue)
            {
                model = await _context.AssessmentConfigurations
                    .Include(ac => ac.QuestionGroups)
                        .ThenInclude(qg => qg.QuestionGroup)
                    .Include(ac => ac.AcademicYear)
                    .Include(ac => ac.ModeOfStudy)
                    .FirstOrDefaultAsync(ac => ac.Id == assessmentConfigId.Value && ac.CourseId == id);

                if (model == null)
                {
                    return NotFound();
                }
            }
            else
            {
                // Create a new configuration model with default values
                var currentAcademicYear = activeAcademicYears.FirstOrDefault();
                var defaultModeOfStudy = modesOfStudy.FirstOrDefault(m => m.Code == "FT"); // Assuming "FT" is the code for Full-time

                model = new AssessmentConfiguration
                {
                    CourseId = course.Id,
                    StartDateTime = DateTime.Now.AddDays(1).Date.AddHours(9), // Default to 9 AM tomorrow
                    EndDateTime = DateTime.Now.AddDays(1).Date.AddHours(11),  // Default to 11 AM tomorrow
                    DurationMinutes = 120,  // Default 2 hours
                    AcademicYearId = currentAcademicYear?.YearId ?? 0,
                    ModeOfStudyId = defaultModeOfStudy?.ModeId ?? 0,
                    CreatedBy = User.Identity.Name,
                    CreatedAt = DateTime.Now
                };
            }

            ViewBag.Course = course;

            // Get course assessments
            ViewBag.CourseAssessments = course.CourseAssessments
                .OrderBy(ca => ca.Assessment.Name)
                .ToList();

            // Get all academic years for dropdown
            ViewBag.AcademicYears = activeAcademicYears;

            // Get all modes of study for dropdown
            ViewBag.ModesOfStudy = modesOfStudy;

            // Get question groups for this course
            ViewBag.QuestionGroups = await _context.QuestionGroups
                .Include(qg => qg.Questions)
                .Where(qg => qg.CourseId == id)
                .OrderBy(qg => qg.Name)
                .ToListAsync();


            var chapters = await _context.Chapters
                .Where(ch => ch.CourseId == id && ch.IsActive)
                .OrderBy(ch => ch.OrderIndex)
                .ToListAsync();

            ViewBag.Chapters = chapters;

            return View(model);
        }


        // POST: Lecturer/SaveAssessmentConfig
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> SaveAssessmentConfig(AssessmentConfiguration model, string save, IFormCollection form)
        {
            try
            {
                // Set publishing status based on submit button
                model.IsPublished = (save == "publish");

                // Set audit fields for new record
                model.CreatedBy = User.Identity.Name;
                model.CreatedAt = DateTime.Now;

                // Make sure ID is 0 to let the database auto-generate it
                model.Id = 0;

                // Add configuration to context
                _context.AssessmentConfigurations.Add(model);


                // Save the question groups
                await _context.SaveChangesAsync();

                TempData["Success"] = model.IsPublished
                    ? "Assessment has been configured and published successfully."
                    : "Assessment has been saved as a draft.";

                return RedirectToAction("CourseContent", new { id = model.CourseId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
                Console.WriteLine($"Error saving assessment configuration: {ex.InnerException}");
            }

            // If we got this far, something failed, redisplay form
            var course = await _context.Courses
                .Include(c => c.CourseLecturers)
                .Include(c => c.CourseAssessments)
                    .ThenInclude(ca => ca.Assessment)
                .FirstOrDefaultAsync(c => c.Id == model.CourseId);

            ViewBag.Course = course;

            // Get course assessments
            ViewBag.CourseAssessments = course.CourseAssessments
                .OrderBy(ca => ca.Assessment.Name)
                .ToList();

            // Get active academic years
            var activeAcademicYears = await _context.AcademicYears
                .Where(ay => ay.IsActive)
                .OrderByDescending(ay => ay.StartDate)
                .ToListAsync();
            ViewBag.AcademicYears = activeAcademicYears;

            // Get modes of study
            var modesOfStudy = await _context.ModesOfStudy
                .OrderBy(m => m.ModeName)
                .ToListAsync();
            ViewBag.ModesOfStudy = modesOfStudy;

            // Get question groups for this course
            ViewBag.QuestionGroups = await _context.QuestionGroups
                .Include(qg => qg.Questions)
                .Where(qg => qg.CourseId == model.CourseId)
                .OrderBy(qg => qg.Name)
                .ToListAsync();

            // Get chapters for this course
            ViewBag.Chapters = await _context.Chapters
                .Where(ch => ch.CourseId == model.CourseId && ch.IsActive)
                .OrderBy(ch => ch.OrderIndex)
                .ToListAsync();

            return View("CreateAssessmentConfig", model);
        }

        // GET: Lecturer/EditAssessmentConfig/{id}/{assessmentConfigId}
        [HttpGet]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> EditAssessmentConfig(int? id, int? assessmentConfigId)
        {
            if (id == null || assessmentConfigId == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the lecturer has access to this course
            var course = await _context.Courses
                .Include(c => c.CourseLecturers)
                .Include(c => c.CourseAssessments)
                    .ThenInclude(ca => ca.Assessment)
                .FirstOrDefaultAsync(c => c.Id == id &&
                                   (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                    c.InstructorId == currentUserId));

            if (course == null)
            {
                return NotFound();
            }

            // Get the assessment configuration with its question groups
            var assessmentConfig = await _context.AssessmentConfigurations
                .Include(ac => ac.QuestionGroups)
                .FirstOrDefaultAsync(ac => ac.Id == assessmentConfigId && ac.CourseId == id);

            if (assessmentConfig == null)
            {
                return NotFound();
            }

            // Explicitly load question groups if needed
            if (assessmentConfig.QuestionGroups == null || !assessmentConfig.QuestionGroups.Any())
            {
                var questionGroups = await _context.AssessmentQuestionGroups
                    .Where(qg => qg.AssessmentConfigurationId == assessmentConfigId)
                    .ToListAsync();

                assessmentConfig.QuestionGroups = questionGroups;
            }

            // Load view data
            ViewBag.Course = course;
            ViewBag.CourseAssessments = course.CourseAssessments
                .OrderBy(ca => ca.Assessment.Name)
                .ToList();

            ViewBag.AcademicYears = await _context.AcademicYears
                .OrderByDescending(ay => ay.StartDate)
                .ToListAsync();

            ViewBag.ModesOfStudy = await _context.ModesOfStudy
                .OrderBy(m => m.ModeName)
                .ToListAsync();

            ViewBag.QuestionGroups = await _context.QuestionGroups
                .Include(qg => qg.Questions)
                .Where(qg => qg.CourseId == id)
                .OrderBy(qg => qg.Name)
                .ToListAsync();

            // Add chapters for this course
            ViewBag.Chapters = await _context.Chapters
                .Where(ch => ch.CourseId == id && ch.IsActive)
                .OrderBy(ch => ch.OrderIndex)
                .ToListAsync();

            ViewBag.AssessmentConfigId = assessmentConfigId;

            return View(assessmentConfig);
        }

        // POST: Lecturer/EditAssessmentConfig
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> EditAssessmentConfig(AssessmentConfiguration model, string save)
        {
            try
            {
                // Fetch the existing configuration
                var existingConfig = await _context.AssessmentConfigurations
                    .Include(ac => ac.QuestionGroups)
                    .FirstOrDefaultAsync(ac => ac.Id == model.Id && ac.CourseId == model.CourseId);

                if (existingConfig == null)
                {
                    return NotFound();
                }

                // Update the properties
                existingConfig.AssessmentId = model.AssessmentId;
                existingConfig.AcademicYearId = model.AcademicYearId;
                existingConfig.ModeOfStudyId = model.ModeOfStudyId;
                existingConfig.ChapterId = model.ChapterId; // Added ChapterId update
                existingConfig.StartDateTime = model.StartDateTime;
                existingConfig.EndDateTime = model.EndDateTime;
                existingConfig.DurationMinutes = model.DurationMinutes;
                existingConfig.RandomizeQuestions = model.RandomizeQuestions;
                existingConfig.PreventTabSwitching = model.PreventTabSwitching;
                existingConfig.ShowResults = model.ShowResults;
                existingConfig.IsPublished = (save == "publish");

                // Update audit fields
                existingConfig.UpdatedBy = User.Identity.Name;
                existingConfig.UpdatedAt = DateTime.Now;

                // Handle the question groups - Remove all existing
                if (existingConfig.QuestionGroups.Any())
                {
                    _context.AssessmentQuestionGroups.RemoveRange(existingConfig.QuestionGroups);
                    await _context.SaveChangesAsync();
                }

                // Add the new question groups from the model
                if (model.QuestionGroups != null && model.QuestionGroups.Any())
                {
                    // Create new groups with properly set parent config ID
                    var newGroups = model.QuestionGroups
                        .Where(qg => qg.QuestionGroupId > 0) // Ensure valid group ID
                        .Select(qg => new AssessmentQuestionGroup
                        {
                            AssessmentConfigurationId = existingConfig.Id,
                            QuestionGroupId = qg.QuestionGroupId,
                            NumberOfQuestionsToUse = qg.NumberOfQuestionsToUse > 0 ? qg.NumberOfQuestionsToUse : 1
                        })
                        .ToList();

                    if (newGroups.Any())
                    {
                        await _context.AssessmentQuestionGroups.AddRangeAsync(newGroups);
                    }
                }

                // Save all changes
                await _context.SaveChangesAsync();

                TempData["Success"] = existingConfig.IsPublished
                    ? "Assessment has been updated and published successfully."
                    : "Assessment has been updated and saved as a draft.";

                return RedirectToAction("CourseContent", new { id = model.CourseId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
                TempData["Error"] = "An error occurred while saving: " + ex.Message;

                // When there's an error, we need to reload all the ViewBag data before returning
                // Get course data for ViewBag
                var course = await _context.Courses
                    .Include(c => c.CourseLecturers)
                    .Include(c => c.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .FirstOrDefaultAsync(c => c.Id == model.CourseId);

                ViewBag.Course = course;
                ViewBag.CourseAssessments = course.CourseAssessments
                    .OrderBy(ca => ca.Assessment.Name)
                    .ToList();

                ViewBag.AcademicYears = await _context.AcademicYears
                    .OrderByDescending(ay => ay.StartDate)
                    .ToListAsync();

                ViewBag.ModesOfStudy = await _context.ModesOfStudy
                    .OrderBy(m => m.ModeName)
                    .ToListAsync();

                ViewBag.QuestionGroups = await _context.QuestionGroups
                    .Include(qg => qg.Questions)
                    .Where(qg => qg.CourseId == model.CourseId)
                    .OrderBy(qg => qg.Name)
                    .ToListAsync();

                // Add chapters to ViewBag
                ViewBag.Chapters = await _context.Chapters
                    .Where(ch => ch.CourseId == model.CourseId && ch.IsActive)
                    .OrderBy(ch => ch.OrderIndex)
                    .ToListAsync();

                // Set the assessment config ID
                ViewBag.AssessmentConfigId = model.Id;

                return View(model);
            }
        }

        // Helper method to load view data for edit
        private async Task LoadViewDataForEdit(AssessmentConfiguration model)
        {
            var course = await _context.Courses
                .Include(c => c.CourseLecturers)
                .Include(c => c.CourseAssessments)
                    .ThenInclude(ca => ca.Assessment)
                .FirstOrDefaultAsync(c => c.Id == model.CourseId);

            if (course == null)
            {
                // Fallback for error cases
                course = await _context.Courses.FindAsync(model.CourseId);
            }

            ViewBag.Course = course;

            ViewBag.CourseAssessments = course?.CourseAssessments
                .OrderBy(ca => ca.Assessment.Name)
                .ToList() ?? new List<CourseAssessment>();

            ViewBag.AcademicYears = await _context.AcademicYears
                .OrderByDescending(ay => ay.StartDate)
                .ToListAsync();

            ViewBag.ModesOfStudy = await _context.ModesOfStudy
                .OrderBy(m => m.ModeName)
                .ToListAsync();

            ViewBag.QuestionGroups = await _context.QuestionGroups
                .Include(qg => qg.Questions)
                .Where(qg => qg.CourseId == model.CourseId)
                .OrderBy(qg => qg.Name)
                .ToListAsync();

            // Ensure question groups are loaded for the model
            if (model.QuestionGroups == null || !model.QuestionGroups.Any())
            {
                model.QuestionGroups = await _context.AssessmentQuestionGroups
                    .Where(qg => qg.AssessmentConfigurationId == model.Id)
                    .ToListAsync();
            }
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> PublishAssessment(int id)
        {
            var assessmentConfig = await _context.AssessmentConfigurations.FindAsync(id);

            if (assessmentConfig == null)
            {
                return NotFound();
            }

            // Verify lecturer has access to this course
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var hasCourseAccess = await _context.Courses
                .AnyAsync(c => c.Id == assessmentConfig.CourseId &&
                              (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                               c.InstructorId == currentUserId));

            if (!hasCourseAccess)
            {
                return Unauthorized();
            }

            // Update published status
            assessmentConfig.IsPublished = true;
            assessmentConfig.UpdatedBy = User.Identity.Name;
            assessmentConfig.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Assessment has been published successfully.";
            return RedirectToAction("CourseContent", new { id = assessmentConfig.CourseId });
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> UnpublishAssessment(int id)
        {
            var assessmentConfig = await _context.AssessmentConfigurations.FindAsync(id);

            if (assessmentConfig == null)
            {
                return NotFound();
            }

            // Verify lecturer has access to this course
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var hasCourseAccess = await _context.Courses
                .AnyAsync(c => c.Id == assessmentConfig.CourseId &&
                              (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                               c.InstructorId == currentUserId));

            if (!hasCourseAccess)
            {
                return Unauthorized();
            }

            // Update published status
            assessmentConfig.IsPublished = false;
            assessmentConfig.UpdatedBy = User.Identity.Name;
            assessmentConfig.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Assessment has been unpublished and is now in draft mode.";
            return RedirectToAction("CourseContent", new { id = assessmentConfig.CourseId });
        }

        // GET: Lecturer/PreviewAssessment/{id}
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> PreviewAssessment(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get the assessment configuration with all related data
            var assessmentConfig = await _context.AssessmentConfigurations
                .Include(ac => ac.Assessment)
                .Include(ac => ac.Course)
                .Include(ac => ac.AcademicYear)
                .Include(ac => ac.ModeOfStudy)
                .Include(ac => ac.QuestionGroups)
                    .ThenInclude(qg => qg.QuestionGroup)
                        .ThenInclude(qg => qg.Questions)
                            .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(ac => ac.Id == id);

            if (assessmentConfig == null)
            {
                return NotFound();
            }

            // Check if the lecturer has access to this course
            bool hasAccess = await _context.Courses
                .AnyAsync(c => c.Id == assessmentConfig.CourseId &&
                         (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                          c.InstructorId == currentUserId));

            if (!hasAccess)
            {
                return Unauthorized();
            }

            // Prepare the questions for display
            var previewQuestions = new List<AssessmentQuestionPreviewViewModel>();

            // For each question group
            foreach (var questionGroup in assessmentConfig.QuestionGroups)
            {
                var group = questionGroup.QuestionGroup;
                var questions = group.Questions.ToList();

                // If randomization is enabled, we'll show the number of questions that would be selected
                int questionCount = assessmentConfig.RandomizeQuestions ?
                    Math.Min(questionGroup.NumberOfQuestionsToUse, questions.Count) :
                    questions.Count;

                // Add each question from this group
                for (int i = 0; i < questionCount; i++)
                {
                    var question = questions[i];

                    var questionViewModel = new AssessmentQuestionPreviewViewModel
                    {
                        QuestionId = question.Id,
                        QuestionText = question.QuestionText,
                        QuestionType = question.QuestionType,
                        QuestionGroupName = group.Name,
                        Instructions = question.AdditionalInfo,
                        // Add the image properties
                        ImagePath = question.ImagePath,
                        ImageDescription = question.ImageDescription,
                        ImageDisplayPosition = question.ImageDisplayPosition,
                        Options = question.Options?.Select(o => new QuestionOptionViewModel
                        {
                            OptionId = o.Id,
                            OptionText = o.OptionText,
                            IsCorrect = o.IsCorrect
                        }).ToList()
                    };

                    previewQuestions.Add(questionViewModel);
                }
            }

            // Create the view model
            var viewModel = new AssessmentPreviewViewModel
            {
                AssessmentConfig = assessmentConfig,
                Questions = previewQuestions,
                IsRandomized = assessmentConfig.RandomizeQuestions,
                PreventTabSwitching = assessmentConfig.PreventTabSwitching,
                ShowResults = assessmentConfig.ShowResults
            };

            return View(viewModel);
        }


        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> AssessmentDetails(int id)
        {
            // Get the assessment configuration with related data
            var assessmentConfig = await _context.AssessmentConfigurations
                .Include(ac => ac.Assessment)
                .Include(ac => ac.Course)
                .Include(ac => ac.AcademicYear)
                .Include(ac => ac.ModeOfStudy)
                .FirstOrDefaultAsync(ac => ac.Id == id);

            if (assessmentConfig == null)
            {
                return NotFound();
            }

            // Verify lecturer has access to this course
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var hasCourseAccess = await _context.Courses
                .Where(c => c.Id == assessmentConfig.CourseId)
                .AnyAsync(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                              c.InstructorId == currentUserId);

            if (!hasCourseAccess)
            {
                return Unauthorized();
            }

            // Get all students enrolled in this course
            var enrolledStudents = await _context.StudentCourseRegistrations
                .Where(scr => scr.CourseId == assessmentConfig.CourseId)
                .Include(scr => scr.Student)
                .Include(scr => scr.Student.ModeOfStudy) // Include the student's mode of study
                .Include(scr => scr.AcademicYear)
                .ToListAsync();

            // Get all student attempts for this assessment
            var studentAttempts = await _context.StudentAttempts
                .Where(sa => sa.AssessmentConfigurationId == id)
                .ToListAsync();

            // Calculate statistics - fix the totalStudents count
            var totalStudents = enrolledStudents.Count;
            var totalAttempts = studentAttempts.Count;
            var completedAttempts = studentAttempts.Count(a => a.Status == "Submitted" || a.Status == "TimedOut");

            decimal averageScore = 0;
            var attemptsWithScores = studentAttempts.Where(a => a.Percentage.HasValue).ToList();
            if (attemptsWithScores.Any())
            {
                averageScore = (decimal)attemptsWithScores.Average(a => a.Percentage!.Value);
            }

            var passRate = totalAttempts > 0
                ? (decimal)studentAttempts.Count(a => a.Passed == true) / totalAttempts * 100
                : 0;

            // Create student attempt status list with academic year and mode of study info
            var studentAttemptStatusList = new List<StudentAssessmentStatusViewModel>();
            foreach (var es in enrolledStudents)
            {
                var attempt = studentAttempts.FirstOrDefault(sa => sa.StudentId == es.StudentId.ToString());
                studentAttemptStatusList.Add(new StudentAssessmentStatusViewModel
                {
                    StudentId = es.StudentId,
                    StudentName = es.Student.FullName,
                    StudentNumber = es.Student.StudentId_Number,
                    HasAttempted = attempt != null,
                    Status = attempt?.Status,
                    Score = attempt?.Percentage,
                    StartTime = attempt?.StartTime,
                    EndTime = attempt?.EndTime,
                    AttemptId = attempt?.Id,
                    AcademicYearId = es.AcademicYearId,
                    AcademicYearName = es.AcademicYear.YearValue,
                    ModeOfStudyId = es.Student.ModeOfStudyId,  // Changed to use Student's mode of study
                    ModeOfStudyName = es.Student.ModeOfStudy.ModeName  // Changed to use Student's mode of study
                });
            }

            // Sort the list
            studentAttemptStatusList = studentAttemptStatusList.OrderBy(s => s.AcademicYearName)
                                                            .ThenBy(s => s.ModeOfStudyName)
                                                            .ThenBy(s => s.StudentName)
                                                            .ToList();

            // Group students by academic year and mode of study
            var groupedStudents = studentAttemptStatusList.GroupBy(s => new { s.AcademicYearId, s.AcademicYearName, s.ModeOfStudyId, s.ModeOfStudyName })
                                                        .Select(g => new StudentGroupViewModel
                                                        {
                                                            AcademicYearId = g.Key.AcademicYearId,
                                                            AcademicYearName = g.Key.AcademicYearName,
                                                            ModeOfStudyId = g.Key.ModeOfStudyId,
                                                            ModeOfStudyName = g.Key.ModeOfStudyName,
                                                            Students = g.ToList(),
                                                            GroupLabel = $"{g.Key.AcademicYearName} - {g.Key.ModeOfStudyName}"
                                                        })
                                                        .OrderBy(g => g.AcademicYearName)
                                                        .ThenBy(g => g.ModeOfStudyName)
                                                        .ToList();

            // Prepare view model
            var viewModel = new LecturerAssessmentDetailsViewModel
            {
                AssessmentConfig = assessmentConfig,
                TotalStudents = totalStudents,
                TotalAttempts = totalAttempts,
                CompletedAttempts = completedAttempts,
                AverageScore = averageScore,
                PassRate = passRate,
                StudentAttempts = studentAttemptStatusList,
                GroupedStudents = groupedStudents
            };

            return View(viewModel);
        }

        // Additional method to allow marking/grading a specific student attempt
        // GET: Lecturer/GradeAttempt/{id}
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> GradeAttempt(int id)
        {
            var studentAttempt = await _context.StudentAttempts
                .Include(sa => sa.Responses)
                    .ThenInclude(r => r.Question)
                        .ThenInclude(q => q.Options)
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Assessment)
                .Include(sa => sa.AssessmentConfiguration)
                    .ThenInclude(ac => ac.Course)
                .FirstOrDefaultAsync(sa => sa.Id == id);

            if (studentAttempt == null)
            {
                return NotFound();
            }

            // Verify lecturer has access to this course
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var hasCourseAccess = await _context.Courses
                .AnyAsync(c => c.Id == studentAttempt.AssessmentConfiguration.CourseId &&
                              (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                               c.InstructorId == currentUserId));

            if (!hasCourseAccess)
            {
                return Unauthorized();
            }

            // Get student information
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id.ToString() == studentAttempt.StudentId);

            if (student == null)
            {
                return NotFound("Student not found");
            }

            // Calculate pass status based on percentage (if available)
            if (studentAttempt.Percentage.HasValue)
            {
                studentAttempt.Passed = studentAttempt.Percentage.Value >= 50; // Assuming 50% passing grade
                await _context.SaveChangesAsync();
            }

            // Order responses for consistent display
            var orderedResponses = studentAttempt.Responses.OrderBy(r => r.Id).ToList();

            // Check if any responses need grading
            bool needsGrading = orderedResponses.Any(r => !r.IsGraded);

            // Ensure each response has properly initialized properties
            foreach (var response in orderedResponses)
            {
                // Ensure the question is loaded with its options
                if (response.Question != null && (response.Question.Options == null || !response.Question.Options.Any()))
                {
                    response.Question.Options = await _context.QuestionOptions
                        .Where(qo => qo.QuestionId == response.QuestionId)
                        .ToListAsync();
                }

                // If multiple choice or true/false and not graded yet, we can auto-grade
                if (!response.IsGraded &&
                    (response.Question.QuestionType == "MultipleChoice" || response.Question.QuestionType == "TrueFalse") &&
                    !string.IsNullOrEmpty(response.ResponseText))
                {
                    if (int.TryParse(response.ResponseText, out int selectedOptionId))
                    {
                        var selectedOption = response.Question.Options.FirstOrDefault(o => o.Id == selectedOptionId);
                        if (selectedOption != null)
                        {
                            response.IsCorrect = selectedOption.IsCorrect;
                            response.Score = selectedOption.IsCorrect ? response.Question.Points : 0;
                            response.IsGraded = true;
                            response.UpdatedBy = User.Identity.Name;
                            response.UpdatedAt = DateTime.Now;
                        }
                    }
                }
            }

            // Check if we just auto-graded anything
            if (orderedResponses.Any(r => r.UpdatedAt == DateTime.Now))
            {
                await _context.SaveChangesAsync();
            }

            // Create view model
            var viewModel = new GradeAttemptViewModel
            {
                StudentAttempt = studentAttempt,
                StudentName = student.FullName,
                StudentNumber = student.StudentId_Number,
                Responses = orderedResponses,
                NeedsGrading = needsGrading
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> SaveGrades(int attemptId, Dictionary<int, GradeResponse> responses)
        {
            // Get the student attempt
            var studentAttempt = await _context.StudentAttempts
                .Include(sa => sa.Responses)
                .Include(sa => sa.AssessmentConfiguration)
                .FirstOrDefaultAsync(sa => sa.Id == attemptId);

            if (studentAttempt == null)
            {
                return NotFound();
            }

            // Verify lecturer has access to this course
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var hasCourseAccess = await _context.Courses
                .AnyAsync(c => c.Id == studentAttempt.AssessmentConfiguration.CourseId &&
                              (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                               c.InstructorId == currentUserId));

            if (!hasCourseAccess)
            {
                return Unauthorized();
            }

            // Update each response with grading info
            if (responses != null)
            {
                decimal totalPoints = 0;

                foreach (var response in studentAttempt.Responses)
                {
                    if (responses.TryGetValue(response.Id, out GradeResponse gradeInfo))
                    {
                        // Update grading info
                        response.IsCorrect = gradeInfo.IsCorrect;
                        response.Score = gradeInfo.Score;

                        // Add feedback using the existing FeedbackFromInstructor field
                        response.FeedbackFromInstructor = gradeInfo.Feedback;

                        // Ensure original ResponseText is preserved and not null
                        if (string.IsNullOrEmpty(response.ResponseText))
                        {
                            response.ResponseText = ""; // Set empty string instead of null
                        }

                        response.IsGraded = true;
                        response.UpdatedBy = User.Identity.Name;
                        response.UpdatedAt = DateTime.Now;

                        if (response.Score.HasValue)
                        {
                            totalPoints += response.Score.Value;
                        }
                    }
                }

                // Update the attempt total score and percentage
                var totalPossiblePoints = await _context.Questions
                    .Where(q => studentAttempt.Responses.Select(r => r.QuestionId).Contains(q.Id))
                    .SumAsync(q => q.Points);

                studentAttempt.TotalScore = totalPoints;

                if (totalPossiblePoints > 0)
                {
                    studentAttempt.Percentage = (decimal)(totalPoints / totalPossiblePoints * 100);
                    // Assuming 50% is passing
                    studentAttempt.Passed = studentAttempt.Percentage >= 50;
                }
                else
                {
                    // Handle edge case where there are no possible points
                    studentAttempt.Percentage = 0;
                    studentAttempt.Passed = false;
                }

                studentAttempt.UpdatedBy = User.Identity.Name;
                studentAttempt.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Grades saved successfully.";
            }

            return RedirectToAction("AssessmentDetails", new { id = studentAttempt.AssessmentConfigurationId });
        }

        // Class to hold grade data from form submission
        //public class GradeResponse
        //{
        //    public bool? IsCorrect { get; set; }
        //    public decimal? Score { get; set; }
        //    public string Feedback { get; set; }
        //}

        [HttpGet]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> PublishAssessmentResults(int id)
        {
            // Get the assessment configuration with needed data
            var assessmentConfig = await _context.AssessmentConfigurations
                .Include(ac => ac.Assessment)
                .Include(ac => ac.Course)
                .FirstOrDefaultAsync(ac => ac.Id == id);

            if (assessmentConfig == null)
            {
                return NotFound();
            }

            // Verify lecturer has access to this course
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var hasCourseAccess = await _context.Courses
                .AnyAsync(c => c.Id == assessmentConfig.CourseId &&
                              (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                               c.InstructorId == currentUserId));

            if (!hasCourseAccess)
            {
                return Unauthorized();
            }

            // Get all student attempts for this assessment
            var studentAttempts = await _context.StudentAttempts
                .Where(sa => sa.AssessmentConfigurationId == id &&
                       (sa.Status == "Submitted" || sa.Status == "TimedOut"))
                .ToListAsync();

            if (!studentAttempts.Any())
            {
                TempData["Warning"] = "No completed student attempts found for this assessment.";
                return RedirectToAction("AssessmentDetails", new { id });
            }

            // Get the assessment details
            var assessment = await _context.Assessments.FindAsync(assessmentConfig.AssessmentId);
            if (assessment == null)
            {
                TempData["Error"] = "Could not find the assessment details.";
                return RedirectToAction("AssessmentDetails", new { id });
            }

            int updatedCount = 0;
            int makeupCount = 0;
            var errors = new List<string>();

            // Group attempts by student to get the highest score for each student
            var studentHighestScores = studentAttempts
                .Where(sa => sa.Percentage.HasValue)
                .GroupBy(sa => sa.StudentId)
                .Select(g => new
                {
                    StudentId = g.Key,
                    HighestPercentage = g.Max(sa => sa.Percentage.Value),
                    BestAttempt = g.OrderByDescending(sa => sa.Percentage.Value).First()
                })
                .ToList();

            // Process each student's highest score
            foreach (var studentScore in studentHighestScores)
            {
                try
                {
                    // Find the student's examinable course record
                    var examinableCourse = await _context.StudentExaminableCourses
                        .FirstOrDefaultAsync(sec =>
                            sec.StudentId.ToString() == studentScore.StudentId &&
                            sec.CourseId == assessmentConfig.CourseId);

                    if (examinableCourse == null)
                    {
                        errors.Add($"Examinable course record not found for student ID {studentScore.StudentId}");
                        continue;
                    }

                    // Get the assessment ID
                    string assessmentId = assessmentConfig.AssessmentId.ToString();

                    // Use the percentage score (not weighted)
                    decimal percentageScore = Math.Round(studentScore.HighestPercentage, 1);
                    string scoreValue = percentageScore.ToString();

                    // Parse existing JSON or create new structure
                    JObject assessmentScores;

                    if (string.IsNullOrEmpty(examinableCourse.AssessmentScores))
                    {
                        assessmentScores = new JObject();
                    }
                    else
                    {
                        assessmentScores = JObject.Parse(examinableCourse.AssessmentScores);
                    }

                    bool isUpdate = false;
                    bool isMakeup = false;

                    // Create or update assessment entry
                    JObject assessmentEntry;

                    if (assessmentScores[assessmentId] != null)
                    {
                        // Assessment entry exists - check if new score is higher
                        assessmentEntry = (JObject)assessmentScores[assessmentId];

                        // Get existing score
                        string existingScoreStr = assessmentEntry["score"]?.ToString();

                        if (!string.IsNullOrEmpty(existingScoreStr) && existingScoreStr != "-")
                        {
                            if (decimal.TryParse(existingScoreStr, out decimal existingScore))
                            {
                                // Only update if new score is higher
                                if (percentageScore > existingScore)
                                {
                                    assessmentEntry["score"] = scoreValue;
                                    isUpdate = true;
                                    isMakeup = true;
                                }
                                else
                                {
                                    // Skip update - existing score is higher or equal
                                    continue;
                                }
                            }
                            else
                            {
                                // Existing score is invalid, update with new score
                                assessmentEntry["score"] = scoreValue;
                                isUpdate = true;
                            }
                        }
                        else
                        {
                            // Existing score is empty or "-", update with new score
                            assessmentEntry["score"] = scoreValue;
                            isUpdate = true;
                        }
                    }
                    else
                    {
                        // Create new assessment entry
                        assessmentEntry = new JObject
                        {
                            ["assessment_name"] = assessment.Name,
                            ["score"] = scoreValue
                        };
                        assessmentScores[assessmentId] = assessmentEntry;
                        isUpdate = true;
                    }

                    if (isUpdate)
                    {
                        // Save updated JSON back to the record
                        examinableCourse.AssessmentScores = assessmentScores.ToString(Formatting.None);

                        // Update the record
                        _context.Update(examinableCourse);
                        updatedCount++;

                        if (isMakeup)
                        {
                            makeupCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error updating student ID {studentScore.StudentId}: {ex.Message}");
                }
            }

            // Save all changes
            await _context.SaveChangesAsync();

            // Set appropriate message
            if (updatedCount > 0)
            {
                string message = $"Successfully published results for {updatedCount} students";
                if (makeupCount > 0)
                {
                    message += $" ({makeupCount} makeup scores updated with higher marks)";
                }
                message += ".";
                TempData["Success"] = message;
            }
            else
            {
                TempData["Info"] = "No new or higher scores found to update.";
            }

            if (errors.Any())
            {
                TempData["Error"] = $"Encountered {errors.Count} errors while publishing results.";
                // Log the errors for admin review
                foreach (var error in errors)
                {
                    Console.WriteLine(error);
                }
            }

            return RedirectToAction("AssessmentDetails", new { id });
        }




        // Chapter methods

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public IActionResult CreateChapter([FromBody] ChapterCreateRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, message = "No data provided." });
                }

                // Map from request to Chapter entity
                var chapter = new Chapter
                {
                    CourseId = request.CourseId,
                    Title = request.Title,
                    Description = request.Description,
                    OrderIndex = request.OrderIndex,
                    CreatedBy = User?.Identity?.Name ?? "System",
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                // Add to database
                _context.Chapters.Add(chapter);
                _context.SaveChanges();

                TempData["Success"] = "Chapter created successfully.";
                return Json(new { success = true, message = "Chapter created successfully" });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                Console.WriteLine($"Error: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }


        [HttpGet]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> GetChapter(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Json(new { success = false, message = "Unauthorized access." });
            }

            var chapter = await _context.Chapters.FindAsync(id);
            if (chapter == null)
            {
                return NotFound(new { success = false, message = "Chapter not found." });
            }

            // Verify lecturer has access to the course
            var hasCourseAccess = await _context.Courses
                .AnyAsync(c => c.Id == chapter.CourseId &&
                              (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                               c.InstructorId == currentUserId));

            if (!hasCourseAccess)
            {
                return Json(new { success = false, message = "You don't have access to this chapter." });
            }

            return Json(new
            {
                id = chapter.Id,
                courseId = chapter.CourseId,
                title = chapter.Title,
                description = chapter.Description,
                orderIndex = chapter.OrderIndex,
                isActive = chapter.IsActive,
                createdAt = chapter.CreatedAt,
                createdBy = chapter.CreatedBy
            });
        }

        // POST: Lecturer/UpdateChapter
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> UpdateChapter([FromBody] ChapterUpdateRequest request)
        {
            try
            {
                if (request == null || request.Id <= 0)
                {
                    return Json(new { success = false, message = "Invalid data provided." });
                }

                var currentUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Json(new { success = false, message = "Unauthorized access." });
                }

                var existingChapter = await _context.Chapters.FindAsync(request.Id);
                if (existingChapter == null)
                {
                    return NotFound(new { success = false, message = "Chapter not found." });
                }

                // Verify lecturer has access to the course
                var hasCourseAccess = await _context.Courses
                    .AnyAsync(c => c.Id == existingChapter.CourseId &&
                                  (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                   c.InstructorId == currentUserId));

                if (!hasCourseAccess)
                {
                    return Json(new { success = false, message = "You don't have access to this chapter." });
                }

                // Update properties from the request
                existingChapter.Title = request.Title;
                existingChapter.Description = request.Description;
                existingChapter.OrderIndex = request.OrderIndex;
                existingChapter.IsActive = request.IsActive;

                // Set audit fields
                existingChapter.UpdatedBy = User?.Identity?.Name ?? "System";
                existingChapter.UpdatedAt = DateTime.Now;

                _context.Update(existingChapter);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Chapter updated successfully.";
                return Json(new { success = true, message = "Chapter updated successfully." });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating chapter: {ex.Message}";
                Console.WriteLine($"Error updating chapter: {ex.Message}");
                return Json(new { success = false, message = $"Error updating chapter: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> DeleteChapter(int id)
        {
            try
            {
                var currentUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Json(new { success = false, message = "Unauthorized access." });
                }

                var chapter = await _context.Chapters.FindAsync(id);
                if (chapter == null)
                {
                    return NotFound(new { success = false, message = "Chapter not found." });
                }

                // Verify lecturer has access to the course
                var hasCourseAccess = await _context.Courses
                    .AnyAsync(c => c.Id == chapter.CourseId &&
                                  (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                   c.InstructorId == currentUserId));

                if (!hasCourseAccess)
                {
                    return Json(new { success = false, message = "You don't have access to this chapter." });
                }

                // First, update any content to remove chapter association
                var chapterContents = await _context.CourseContents
                    .Where(cc => cc.ChapterId == id)
                    .ToListAsync();

                foreach (var content in chapterContents)
                {
                    content.ChapterId = null;
                    content.UpdatedBy = User?.Identity?.Name ?? "System";
                    content.UpdatedAt = DateTime.Now;
                }

                // Now remove the chapter
                _context.Chapters.Remove(chapter);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Chapter deleted successfully.";
                return Json(new { success = true, message = "Chapter deleted successfully." });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting chapter: {ex.Message}";
                Console.WriteLine($"Error deleting chapter: {ex.Message}");
                return Json(new { success = false, message = $"Error deleting chapter: {ex.Message}" });
            }
        }






        // Updated CourseStudents method with fixed type comparisons
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> CourseStudents(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the lecturer has access to this course
            var course = await _context.Courses
                .Include(c => c.Programme)
                .FirstOrDefaultAsync(c => c.Id == id &&
                                       (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                        c.InstructorId == currentUserId));

            if (course == null)
            {
                return NotFound();
            }

            // Get all course chapters for progress calculation
            var courseChapters = await _context.Chapters
                .Where(ch => ch.CourseId == id && ch.IsActive)
                .OrderBy(ch => ch.OrderIndex)
                .ToListAsync();

            // Get all students enrolled in this course with their details
            var enrolledStudents = await _context.StudentCourseRegistrations
                .Where(scr => scr.CourseId == id)
                .Include(scr => scr.Student)
                    .ThenInclude(s => s.ModeOfStudy)
                .Include(scr => scr.AcademicYear)
                .ToListAsync();

            // Get all chapter progress data for these students
            // Get all chapter progress data for these students
            var studentIds = enrolledStudents.Select(s => s.StudentId).ToList(); // Keep as integers
            var chapterProgressData = await _context.ChapterProgress
                .Where(cp => cp.CourseId == id && studentIds.Contains(cp.StudentId))
                .ToListAsync();

            // Organize students by academic year and semester for tabbed display
            var studentsByYearAndSemester = enrolledStudents
                .GroupBy(s => new { YearId = s.AcademicYearId, Semester = s.Semester })
                .Select(g => new CourseStudentGroupModel
                {
                    AcademicYearId = g.Key.YearId,
                    SemesterId = g.Key.Semester,
                    AcademicYear = g.First().AcademicYear.YearValue,
                    Semester = $"Semester {g.Key.Semester}",
                    Students = g.Select(s => new CourseStudentProgressModel
                    {
                        StudentId = s.StudentId.ToString(),
                        StudentNumber = s.Student.StudentId_Number,
                        FirstName = s.Student.FullName, // Assuming FullName contains first name + last name
                        LastName = "", // Since Student model uses FullName instead of FirstName/LastName
                        Email = s.Student.Email,
                        EnrollmentDate = s.RegistrationDate,
                        ModeOfStudy = s.Student.ModeOfStudy.ModeName, // Add mode of study
                                                                      // Calculate progress for each chapter
                                                                      // Calculate progress for each chapter
                        ChapterProgress = courseChapters.Select(ch =>
                        {
                            return new CourseChapterProgressModel
                            {
                                ChapterId = ch.Id,
                                ChapterTitle = ch.Title,
                                IsCompleted = chapterProgressData
                                    .Any(cp => cp.ChapterId == ch.Id &&
                                              cp.StudentId == s.StudentId && // Keep as integers
                                              cp.IsCompleted),
                                ProgressPercentage = CalculateChapterProgress(
                                    chapterProgressData.FirstOrDefault(cp =>
                                        cp.ChapterId == ch.Id && cp.StudentId == s.StudentId)), // Keep as integers
                                CreatedAt = chapterProgressData
                                    .FirstOrDefault(cp => cp.ChapterId == ch.Id && cp.StudentId == s.StudentId)?.CreatedAt ?? DateTime.MinValue,
                                CompletedDate = chapterProgressData
                                    .FirstOrDefault(cp => cp.ChapterId == ch.Id && cp.StudentId == s.StudentId)?.CompletedDate
                            };
                        }).ToList(),
                        // Calculate overall progress
                        OverallProgress = CalculateOverallProgress(s.StudentId, // Pass as integer
                            courseChapters.Select(c => c.Id).ToList(),
                            chapterProgressData)
                    }).ToList()
                }).ToList();

            // Prepare view model with all necessary data
            var viewModel = new CourseStudentsViewModel
            {
                CourseId = course.Id,
                CourseCode = course.CourseCode,
                CourseName = course.CourseName,
                TotalStudents = enrolledStudents.Count,
                TotalChapters = courseChapters.Count,
                StudentGroups = studentsByYearAndSemester,
                Chapters = courseChapters.Select(c => new CourseChapterModel
                {
                    Id = c.Id,
                    Title = c.Title,
                    OrderIndex = c.OrderIndex
                }).ToList()
            };

            return View(viewModel);
        }

        // Helper methods for the CourseStudents action
        private int CalculateChapterProgress(ChapterProgress progress)
        {
            if (progress == null)
                return 0;

            if (progress.IsCompleted)
                return 100;

            // We'll use a simple record exists/doesn't exist logic
            return progress.CreatedAt > DateTime.MinValue ? 50 : 0;
        }

        private int CalculateOverallProgress(int studentId, List<int> chapterIds, List<ChapterProgress> progressData)
        {
            if (!chapterIds.Any())
                return 0;

            var studentProgress = progressData
                .Where(p => p.StudentId == studentId && chapterIds.Contains(p.ChapterId))
                .ToList();

            if (!studentProgress.Any())
                return 0;

            var completedChapters = studentProgress.Count(p => p.IsCompleted);
            return (completedChapters * 100) / chapterIds.Count;
        }





        // NEW METHOD: Course Rating Analytics Main Page
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> CourseRatingAnalytics(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify lecturer access to course
            var course = await _context.Courses
                .Include(c => c.CourseLecturers)
                .Include(c => c.Programme)
                    .ThenInclude(p => p.Department)
                .FirstOrDefaultAsync(c => c.Id == id &&
                                    (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                     c.InstructorId == currentUserId));

            if (course == null)
            {
                return NotFound();
            }

            // Build the rating analytics view model
            var viewModel = await BuildCourseRatingAnalytics(id, course);

            return View(viewModel);
        }

        // Helper method to build rating analytics data
        private async Task<CourseRatingAnalyticsViewModel> BuildCourseRatingAnalytics(int courseId, dynamic course)
        {
            // Get all chapters for this course
            var chapters = await _context.Chapters
                .Where(ch => ch.CourseId == courseId && ch.IsActive)
                .OrderBy(ch => ch.OrderIndex)
                .ToListAsync();

            // Get all ratings for this course
            var courseRatings = await _context.ChapterRatings
                .Include(cr => cr.Chapter)
                .Where(cr => cr.CourseId == courseId)
                .ToListAsync();

            // CORRECTED: Get all students registered for this course with their details from Student table
            var studentsData = await _context.StudentCourseRegistrations
                .Include(scr => scr.Student)
                    .ThenInclude(s => s.ModeOfStudy) // Include ModeOfStudy from Student
                .Include(scr => scr.AcademicYear)
                .Where(scr => scr.CourseId == courseId)
                .Select(scr => new
                {
                    StudentId = scr.StudentId,
                    StudentNumber = scr.Student.StudentId_Number, // Use StudentId_Number from your Student model
                    AcademicYearId = scr.AcademicYearId,
                    AcademicYear = scr.AcademicYear.YearValue,
                    SemesterId = scr.Semester, // This is directly from StudentCourseRegistration
                    Semester = scr.Semester.ToString(),
                    ModeOfStudy = scr.Student.ModeOfStudy.ModeName, // Get from Student's ModeOfStudy
                    EnrollmentDate = scr.RegistrationDate // Use RegistrationDate from StudentCourseRegistration
                })
                .ToListAsync();

            // Calculate overall statistics
            var totalRatings = courseRatings.Count;
            var averageRating = totalRatings > 0 ? Math.Round(courseRatings.Average(cr => cr.Rating), 1) : 0;
            var totalStudentsRated = courseRatings.Select(cr => cr.StudentId).Distinct().Count();

            // Calculate rating distribution
            var ratingDistribution = courseRatings
                .GroupBy(cr => cr.Rating)
                .ToDictionary(g => g.Key, g => g.Count());

            // Ensure all rating levels (1-5) are represented
            for (int i = 1; i <= 5; i++)
            {
                if (!ratingDistribution.ContainsKey(i))
                    ratingDistribution[i] = 0;
            }

            // Build chapter statistics
            var chapterStats = new List<ChapterRatingStats>();
            foreach (var chapter in chapters)
            {
                var chapterRatings = courseRatings.Where(cr => cr.ChapterId == chapter.Id).ToList();
                var chapterAverage = chapterRatings.Any() ? Math.Round(chapterRatings.Average(cr => cr.Rating), 1) : 0;

                var chapterRatingDistribution = chapterRatings
                    .GroupBy(cr => cr.Rating)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Get anonymous reviews for this chapter
                var anonymousReviews = chapterRatings
                    .Where(cr => !string.IsNullOrEmpty(cr.ReviewText))
                    .OrderByDescending(cr => cr.CreatedAt)
                    .Take(10)
                    .Select(cr => new AnonymousReview
                    {
                        ReviewText = cr.ReviewText,
                        Rating = cr.Rating,
                        ReviewDate = cr.CreatedAt,
                        AnonymousIdentifier = GenerateAnonymousIdentifier(cr.StudentId, courseId)
                    })
                    .ToList();

                var completionRate = studentsData.Count > 0
                    ? Math.Round((double)chapterRatings.Count / studentsData.Count * 100, 1)
                    : 0;

                chapterStats.Add(new ChapterRatingStats
                {
                    ChapterId = chapter.Id,
                    ChapterTitle = chapter.Title,
                    OrderIndex = chapter.OrderIndex,
                    AverageRating = chapterAverage,
                    TotalRatings = chapterRatings.Count,
                    RatingDistribution = chapterRatingDistribution,
                    RecentReviews = anonymousReviews,
                    CompletionRate = completionRate
                });
            }

            // CORRECTED: Group students by academic year and mode of study (from Student table)
            var studentGroups = new List<StudentRatingGroup>();
            var groupedStudents = studentsData
                .GroupBy(s => new { s.AcademicYearId, s.AcademicYear, s.SemesterId, s.Semester, s.ModeOfStudy })
                .OrderByDescending(g => g.Key.AcademicYear)
                .ThenBy(g => g.Key.ModeOfStudy);

            foreach (var group in groupedStudents)
            {
                var maskedStudents = new List<MaskedStudentRating>();

                foreach (var student in group.OrderBy(s => s.StudentNumber))
                {
                    var studentRatings = courseRatings.Where(cr => cr.StudentId == student.StudentId).ToList();
                    var studentAverage = studentRatings.Any() ? Math.Round(studentRatings.Average(cr => cr.Rating), 1) : 0;
                    var chaptersRated = studentRatings.Count;
                    var ratingCompletionPercentage = chapters.Count > 0
                        ? Math.Round((decimal)chaptersRated / chapters.Count * 100, 1)
                        : 0;

                    var chapterRatingDetails = new List<ChapterRatingDetail>();
                    foreach (var chapter in chapters)
                    {
                        var rating = studentRatings.FirstOrDefault(sr => sr.ChapterId == chapter.Id);
                        chapterRatingDetails.Add(new ChapterRatingDetail
                        {
                            ChapterId = chapter.Id,
                            ChapterTitle = chapter.Title,
                            OrderIndex = chapter.OrderIndex,
                            Rating = rating?.Rating,
                            ReviewText = rating?.ReviewText,
                            RatedDate = rating?.CreatedAt,
                            HasRating = rating != null
                        });
                    }

                    maskedStudents.Add(new MaskedStudentRating
                    {
                        MaskedStudentNumber = MaskStudentNumber(student.StudentNumber),
                        StudentHashId = GenerateStudentHashId(student.StudentId),
                        EnrollmentDate = student.EnrollmentDate,
                        AverageRating = studentAverage,
                        ChaptersRated = chaptersRated,
                        TotalChapters = chapters.Count,
                        RatingCompletionPercentage = ratingCompletionPercentage,
                        ChapterRatings = chapterRatingDetails.OrderBy(crd => crd.OrderIndex).ToList(),
                        LastRatingDate = studentRatings.Any() ? studentRatings.Max(sr => sr.CreatedAt) : null,
                        ModeOfStudy = student.ModeOfStudy
                    });
                }

                studentGroups.Add(new StudentRatingGroup
                {
                    AcademicYearId = group.Key.AcademicYearId,
                    AcademicYear = group.Key.AcademicYear,
                    SemesterId = group.Key.SemesterId,
                    Semester = group.Key.Semester,
                    ModeOfStudy = group.Key.ModeOfStudy,
                    Students = maskedStudents
                });
            }

            return new CourseRatingAnalyticsViewModel
            {
                CourseId = courseId,
                CourseCode = course.CourseCode,
                CourseName = course.CourseName,
                TotalStudents = studentsData.Count,
                OverallAverageRating = averageRating,
                TotalRatings = totalRatings,
                TotalStudentsRated = totalStudentsRated,
                StudentGroups = studentGroups,
                ChapterStats = chapterStats.OrderBy(cs => cs.OrderIndex).ToList(),
                RatingDistribution = ratingDistribution,
                Chapters = chapters.Select(c => new SIS.Models.Lecturer.Chapter
                {
                    Id = c.Id,
                    Title = c.Title,
                    OrderIndex = c.OrderIndex,
                    CourseId = c.CourseId,
                    IsActive = c.IsActive,
                    Description = c.Description ?? "",
                    CreatedAt = c.CreatedAt,
                    CreatedBy = c.CreatedBy ?? "System",
                }).ToList()
            };
        }

        // Helper method to mask student numbers
        private string MaskStudentNumber(string studentNumber)
        {
            if (string.IsNullOrEmpty(studentNumber))
                return "STUDENTXXXXXX";

            if (studentNumber.Length <= 6)
                return "XXXXXX";

            // Keep first part, mask last 6 digits
            var prefix = studentNumber.Substring(0, studentNumber.Length - 6);
            return $"{prefix}XXXXXX";
        }

        // Helper method to generate anonymous identifier for reviews
        private string GenerateAnonymousIdentifier(int studentId, int courseId)
        {
            var hash = $"{studentId}-{courseId}".GetHashCode();
            var index = Math.Abs(hash) % 26; // 0-25 for A-Z
            return $"Anonymous Student {(char)('A' + index)}";
        }

        // Helper method to generate consistent hash ID for students
        private string GenerateStudentHashId(int studentId)
        {
            return $"student-{Math.Abs(studentId.GetHashCode())}";
        }


        // ========================================================================
        // ASSESSMENT MANAGEMENT FOR COURSES
        // ========================================================================

        /// <summary>
        /// GET: /Lecturer/ManageCourseAssessments/{id}
        /// Shows all assessments linked to a course and allows adding/removing
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> ManageCourseAssessments(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Verify the lecturer has access to this course
            var course = await _context.Courses
                .Include(c => c.Programme)
                    .ThenInclude(p => p.Department)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .Include(c => c.CourseAssessments)
                    .ThenInclude(ca => ca.Assessment)
                .FirstOrDefaultAsync(c => c.Id == id &&
                                        (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                         c.InstructorId == currentUserId));

            if (course == null)
            {
                return NotFound();
            }

            // Get all active assessments
            var allAssessments = await _context.Assessments
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .ToListAsync();

            // Get assessments already linked to this course
            var linkedAssessmentIds = course.CourseAssessments.Select(ca => ca.AssessmentId).ToList();

            // Get assessments NOT linked to this course
            var availableAssessments = allAssessments
                .Where(a => !linkedAssessmentIds.Contains(a.Id))
                .ToList();

            // Calculate total weight
            var totalWeight = course.CourseAssessments.Sum(ca => ca.Assessment.WeightPercentage);

            // Build view model
            var viewModel = new ManageCourseAssessmentsViewModel
            {
                CourseId = course.Id,
                CourseCode = course.CourseCode,
                CourseName = course.CourseName,
                ProgrammeName = course.Programme.Name,
                DepartmentName = course.Programme.Department.Name,
                CurrentAssessments = course.CourseAssessments
                    .Select(ca => ca.Assessment)
                    .OrderBy(a => a.Name)
                    .ToList(),
                AvailableAssessments = availableAssessments,
                TotalWeightPercentage = totalWeight,
                WeightStatus = GetWeightStatus(totalWeight)
            };

            return View(viewModel);
        }

        /// <summary>
        /// POST: /Lecturer/AddAssessmentToCourse
        /// Adds an existing assessment to a course
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> AddAssessmentToCourse([FromBody] AssessmentLinkRequest request)
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Json(new { success = false, message = "Unauthorized access" });
                }

                // Verify lecturer has access to this course
                var hasAccess = await _context.Courses
                    .AnyAsync(c => c.Id == request.CourseId &&
                                  (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                   c.InstructorId == currentUserId));

                if (!hasAccess)
                {
                    return Json(new { success = false, message = "You don't have access to this course" });
                }

                // Check if assessment is already linked
                var exists = await _context.CourseAssessment
                    .AnyAsync(ca => ca.CourseId == request.CourseId &&
                                   ca.AssessmentId == request.AssessmentId);

                if (exists)
                {
                    return Json(new { success = false, message = "This assessment is already linked to the course" });
                }

                // Get the assessment to return its details
                var assessment = await _context.Assessments.FindAsync(request.AssessmentId);
                if (assessment == null)
                {
                    return Json(new { success = false, message = "Assessment not found" });
                }

                // Create the link
                var courseAssessment = new CourseAssessment
                {
                    CourseId = request.CourseId,
                    AssessmentId = request.AssessmentId
                };

                _context.CourseAssessment.Add(courseAssessment);
                await _context.SaveChangesAsync();

                // Calculate new total weight
                var totalWeight = await _context.CourseAssessment
                    .Where(ca => ca.CourseId == request.CourseId)
                    .Include(ca => ca.Assessment)
                    .SumAsync(ca => ca.Assessment.WeightPercentage);

                return Json(new
                {
                    success = true,
                    message = "Assessment added successfully",
                    assessment = new
                    {
                        id = assessment.Id,
                        name = assessment.Name,
                        type = assessment.Type,
                        weightPercentage = assessment.WeightPercentage,
                        passMark = assessment.PassMark
                    },
                    totalWeight = totalWeight,
                    weightStatus = GetWeightStatus(totalWeight)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error adding assessment: {ex.Message}" });
            }
        }

        /// <summary>
        /// POST: /Lecturer/RemoveAssessmentFromCourse
        /// Removes an assessment link from a course
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> RemoveAssessmentFromCourse([FromBody] AssessmentLinkRequest request)
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Json(new { success = false, message = "Unauthorized access" });
                }

                // Verify lecturer has access to this course
                var hasAccess = await _context.Courses
                    .AnyAsync(c => c.Id == request.CourseId &&
                                  (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                   c.InstructorId == currentUserId));

                if (!hasAccess)
                {
                    return Json(new { success = false, message = "You don't have access to this course" });
                }

                // Find the link
                var courseAssessment = await _context.CourseAssessment
                    .FirstOrDefaultAsync(ca => ca.CourseId == request.CourseId &&
                                              ca.AssessmentId == request.AssessmentId);

                if (courseAssessment == null)
                {
                    return Json(new { success = false, message = "Assessment link not found" });
                }

                // Remove the link
                _context.CourseAssessment.Remove(courseAssessment);
                await _context.SaveChangesAsync();

                // Calculate new total weight
                var totalWeight = await _context.CourseAssessment
                    .Where(ca => ca.CourseId == request.CourseId)
                    .Include(ca => ca.Assessment)
                    .SumAsync(ca => ca.Assessment.WeightPercentage);

                return Json(new
                {
                    success = true,
                    message = "Assessment removed successfully",
                    totalWeight = totalWeight,
                    weightStatus = GetWeightStatus(totalWeight)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error removing assessment: {ex.Message}" });
            }
        }

        /// <summary>
        /// POST: /Lecturer/CreateAndLinkAssessment
        /// Creates a new assessment and immediately links it to the course
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> CreateAndLinkAssessment([FromBody] CreateAndLinkAssessmentRequest request)
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Json(new { success = false, message = "Unauthorized access" });
                }

                // Verify lecturer has access to this course
                var hasAccess = await _context.Courses
                    .AnyAsync(c => c.Id == request.CourseId &&
                                  (c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                                   c.InstructorId == currentUserId));

                if (!hasAccess)
                {
                    return Json(new { success = false, message = "You don't have access to this course" });
                }

                // Create the assessment
                var assessment = new Assessment
                {
                    Name = request.Name,
                    Type = request.Type,
                    WeightPercentage = request.WeightPercentage,
                    PassMark = request.PassMark ?? 50,
                    Description = request.Description,
                    IsActive = true,
                    RequiresSubmission = request.RequiresSubmission,
                    AllowResit = request.AllowResit,
                    MaximumResitMark = request.MaximumResitMark,
                    CreatedBy = User.Identity.Name,
                    CreatedAt = DateTime.Now
                };

                _context.Assessments.Add(assessment);
                await _context.SaveChangesAsync();

                // Link to course
                var courseAssessment = new CourseAssessment
                {
                    CourseId = request.CourseId,
                    AssessmentId = assessment.Id
                };

                _context.CourseAssessment.Add(courseAssessment);
                await _context.SaveChangesAsync();

                // Calculate new total weight
                var totalWeight = await _context.CourseAssessment
                    .Where(ca => ca.CourseId == request.CourseId)
                    .Include(ca => ca.Assessment)
                    .SumAsync(ca => ca.Assessment.WeightPercentage);

                return Json(new
                {
                    success = true,
                    message = "Assessment created and linked successfully",
                    assessment = new
                    {
                        id = assessment.Id,
                        name = assessment.Name,
                        type = assessment.Type,
                        weightPercentage = assessment.WeightPercentage,
                        passMark = assessment.PassMark
                    },
                    totalWeight = totalWeight,
                    weightStatus = GetWeightStatus(totalWeight)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error creating assessment: {ex.Message}" });
            }
        }

        /// <summary>
        /// Helper method to determine weight status
        /// </summary>
        private string GetWeightStatus(decimal totalWeight)
        {
            if (totalWeight == 100)
                return "perfect";
            else if (totalWeight < 100)
                return "under";
            else
                return "over";
        }





    }

    // ========================================================================
    // REQUEST MODELS FOR AJAX CALLS
    // ========================================================================

    public class AssessmentLinkRequest
    {
        public int CourseId { get; set; }
        public int AssessmentId { get; set; }
    }

    public class CreateAndLinkAssessmentRequest
    {
        public int CourseId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int WeightPercentage { get; set; }  // Changed from decimal to int
        public int? PassMark { get; set; }  // Changed from decimal? to int?
        public string Description { get; set; }
        public bool RequiresSubmission { get; set; }
        public bool AllowResit { get; set; }
        public int? MaximumResitMark { get; set; }  // Changed from decimal? to int?
    }
}
