using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Accounts;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.Reports;
using SIS.Models.Results;
using SIS.Models.StudentApplication;
using SIS.Models.StudentResults;
using SIS.Services;
using SIS.Services.Progression;
using SIS.Services.Transcripts;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentScoreInfo = SIS.Models.Admin.AssessmentScoreInfo;

namespace SIS.Controllers
{
    public class StudentResultsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StudentResultsController> _logger;
        private readonly IAssessmentScoreService _assessmentScoreService;
        private readonly ICourseResultCalculationService _resultCalculationService;
        private readonly IResultAuditService _auditService;
        private readonly IResultIntegrityService _integrityService;
        private readonly ITranscriptGenerationService _transcriptService;
        private readonly IStudentProgressionService _progressionService;

        public StudentResultsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<StudentResultsController> logger,
            IAssessmentScoreService assessmentScoreService,
            ICourseResultCalculationService resultCalculationService,
            IResultAuditService auditService,
            IResultIntegrityService integrityService,
            ITranscriptGenerationService transcriptService,
            IStudentProgressionService progressionService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _assessmentScoreService = assessmentScoreService;
            _resultCalculationService = resultCalculationService;
            _auditService = auditService;
            _integrityService = integrityService;
            _transcriptService = transcriptService;
            _progressionService = progressionService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Route("StudentResults/Results/{studentId?}")]
        public async Task<IActionResult> Results(int? studentId = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                // If studentId is not provided or is 0, get from logged-in user
                int actualStudentId = studentId ?? 0;

                Student student = null;
                if(actualStudentId != 0)
                {
                    student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Id == actualStudentId);
                }
                else
                {
                    student = await _context.Students
                        .Include(s => s.Programme)
                            .ThenInclude(p => p.Department)
                        .Include(s => s.AcademicYear)
                        .FirstOrDefaultAsync(s => s.Username == user.UserName);
                    actualStudentId = student.Id;
                }

                if (student == null) return NotFound("Student record not found.");

                _logger.LogInformation($"Processing results for student: {student.Id}");

                // Check if student can view complete results (no outstanding fees)
                decimal outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
                decimal currentInvoiceBalance = StudentTools.GetCurrentInvoicesBalance(student.Id);

                outstandingBalance = outstandingBalance - currentInvoiceBalance;

                bool canViewCompleteResults = outstandingBalance <= 0;

                var viewModel = new Models.Admin.StudentResultsViewModel
                {
                    OutstandingFees = outstandingBalance,
                    OverallGPA = 0.0M,
                    AcademicYears = new List<AcademicYearResults>(),
                    Grades = new List<Models.Admin.GradeConfiguration>(),
                    CoursePassMarks = new Dictionary<int, double>(),
                    CanViewCompleteResults = canViewCompleteResults
                };

                // **Query view once for all student results - view has pre-computed grades**
                var allStudentResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw(@"
                        WITH RankedResults AS
                        (
                            SELECT *,
                                   ROW_NUMBER() OVER
                                   (
                                       PARTITION BY StudentId_Number, CourseCode
                                       ORDER BY Attempt DESC
                                   ) AS rn
                            FROM VW_StudentResults
                            WHERE StudentId_Number = {0}
                              AND ApprovalStatus = {1}
                        )
                        SELECT *
                        FROM RankedResults
                        WHERE rn = 1
                        ORDER BY AcademicYearId, Semester, CourseCode",
                        student.StudentId_Number,
                        "7")
                    .AsNoTracking()
                    .ToListAsync();


                if (!allStudentResults.Any())
                {
                    _logger.LogInformation($"No results found for student {student.Id}");
                    return View(viewModel);
                }

                // **Batch fetch academic years**
                var academicYearIds = allStudentResults.Select(r => r.AcademicYearId).Distinct().ToList();
                var academicYearsDict = await _context.AcademicYears
                    .Where(ay => academicYearIds.Contains(ay.YearId))
                    .ToDictionaryAsync(ay => ay.YearId);

                // **Batch fetch courses for credits and pass marks**
                var courseCodePairs = allStudentResults
                    .Select(r => new { r.CourseCode, r.YearOfStudy, r.Semester })
                    .Distinct()
                    .ToList();

                var allCourses = await _context.Courses
                    .Where(c => courseCodePairs.Select(p => p.CourseCode).Contains(c.CourseCode))
                    .ToListAsync();

                // Create lookup with fallback logic
                var coursesDict = new Dictionary<string, Course>();
                foreach (var pair in courseCodePairs)
                {
                    var key = $"{pair.CourseCode}_{pair.YearOfStudy}_{pair.Semester}";

                    // Try exact match first
                    var course = allCourses.FirstOrDefault(c =>
                        c.CourseCode == pair.CourseCode &&
                        c.YearTaken == pair.YearOfStudy &&
                        c.SemesterTaken == pair.Semester);

                    // Fallback to just course code
                    if (course == null)
                    {
                        course = allCourses.FirstOrDefault(c => c.CourseCode == pair.CourseCode);
                    }

                    if (course != null)
                    {
                        coursesDict[key] = course;
                    }
                }

                decimal totalGpaPoints = 0;
                int totalCreditsAttempted = 0;
                int totalCreditsEarned = 0;
                int overallFailedCourses = 0;

                // Group by academic year
                var yearGroups = allStudentResults
                    .GroupBy(r => r.AcademicYearId)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var yearGroup in yearGroups)
                {
                    var yearId = yearGroup.Key;
                    var yearData = yearGroup.ToList();

                    if (!academicYearsDict.TryGetValue(yearId, out var academicYear))
                    {
                        _logger.LogWarning($"Academic year {yearId} not found");
                        continue;
                    }

                    var yearResults = new AcademicYearResults
                    {
                        YearId = yearId,
                        YearValue = academicYear.YearValue,
                        Semesters = new List<SemesterResults>(),
                        AcademicStanding = null
                    };

                    decimal yearGpaPoints = 0;
                    int yearCreditsAttempted = 0;
                    int yearCreditsEarned = 0;
                    int yearFailedCourses = 0;

                    // Group by semester
                    var semesterGroups = yearData
                        .GroupBy(r => r.Semester)
                        .OrderBy(g => g.Key)
                        .ToList();

                    foreach (var semesterGroup in semesterGroups)
                    {
                        var semester = semesterGroup.Key;
                        var semesterData = semesterGroup.ToList();

                        var semesterCourses = new List<CourseResult>();

                        // Semester-level tracking
                        decimal semesterGpaPoints = 0;
                        int semesterCreditsAttempted = 0;
                        int semesterCreditsEarned = 0;
                        int semesterFailedCourses = 0;
                        int? highestAttemptInSemester = null;
                        bool hasDeferedExam = false;

                        foreach (var result in semesterData)
                        {
                            try
                            {
                                // Track highest attempt in this semester
                                if (highestAttemptInSemester == null || result.Attempt > highestAttemptInSemester)
                                {
                                    highestAttemptInSemester = result.Attempt;
                                }

                                // Lookup course
                                var courseKey = $"{result.CourseCode}_{result.YearOfStudy}_{semester}";

                                if (!coursesDict.TryGetValue(courseKey, out var course))
                                {
                                    _logger.LogWarning($"Course {result.CourseCode} not found");
                                    continue;
                                }

                                // Add course pass mark to dictionary
                                viewModel.CoursePassMarks[course.Id] = course.PassMark;

                                // Build assessment scores dictionary from view
                                var scores = new Dictionary<string, Models.Admin.AssessmentScoreInfo>();

                                if (result.CA.HasValue && result.CA.Value > 0)
                                {
                                    scores["CA"] = new Models.Admin.AssessmentScoreInfo
                                    {
                                        Score = result.CA.Value,
                                        WeightPercentage = 0
                                    };
                                }

                                if (result.Exam.HasValue)
                                {
                                    scores["Exam"] = new Models.Admin.AssessmentScoreInfo
                                    {
                                        Score = result.Exam.Value,
                                        WeightPercentage = 0
                                    };
                                }

                                // **Read pre-computed values directly from view**
                                decimal? totalScore = result.TotalScore;
                                string grade = result.GradeLetter;
                                bool isPassed = result.IsPassingGrade == 1;
                                decimal gradePoints = result.GPAValue.Value;

                                // Check for deferred exams
                                if (grade == "NE")
                                {
                                    hasDeferedExam = true;
                                }

                                // Only include in GPA calculation if student can view complete results and not NE
                                if (canViewCompleteResults && grade != "NE")
                                {
                                    // Semester-level accumulation
                                    semesterCreditsAttempted += course.Credits;

                                    if (!isPassed)
                                    {
                                        semesterFailedCourses++;
                                    }
                                    else
                                    {
                                        semesterCreditsEarned += course.Credits;
                                    }

                                    // Add to semester GPA calculation
                                    semesterGpaPoints += gradePoints * course.Credits;

                                    // Year-level accumulation
                                    yearCreditsAttempted += course.Credits;
                                    totalCreditsAttempted += course.Credits;

                                    if (!isPassed)
                                    {
                                        yearFailedCourses++;
                                        overallFailedCourses++;
                                    }
                                    else
                                    {
                                        yearCreditsEarned += course.Credits;
                                        totalCreditsEarned += course.Credits;
                                    }

                                    // Add to year/overall GPA calculation
                                    yearGpaPoints += gradePoints * course.Credits;
                                    totalGpaPoints += gradePoints * course.Credits;
                                }

                                // View already filtered for approved/published results
                                bool isPublished = true;
                                bool courseCanViewCompleteResults = canViewCompleteResults;

                                semesterCourses.Add(new CourseResult
                                {
                                    CourseId = course.Id,
                                    CourseCode = result.CourseCode,
                                    CourseName = result.CourseName,
                                    Credits = course.Credits,
                                    Scores = scores,
                                    TotalScore = totalScore,
                                    Grade = grade,
                                    Remark = result.Description,
                                    IsPassed = isPassed,
                                    IsPublished = isPublished,
                                    CanViewComplete = courseCanViewCompleteResults
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error processing course result for CourseCode={result.CourseCode}");
                            }
                        }

                        if (semesterCourses.Any())
                        {
                            var semesterResults = new SemesterResults
                            {
                                SemesterId = semester,
                                Courses = semesterCourses,
                                GPA = semesterCreditsAttempted > 0 ? semesterGpaPoints / semesterCreditsAttempted : 0,
                                CreditsAttempted = semesterCreditsAttempted,
                                CreditsEarned = semesterCreditsEarned,
                                FailedCourses = semesterFailedCourses
                            };

                            // **Calculate semester academic standing/comment**
                            int totalCoursesInSemester = semesterCourses.Count;
                            if (totalCoursesInSemester > 0)
                            {
                                // Check for deferred status first
                                if (hasDeferedExam)
                                {
                                    semesterResults.AcademicStanding = "DEF";
                                }
                                else
                                {
                                    int failedPercentage = (int)Math.Floor(((double)semesterFailedCourses / totalCoursesInSemester) * 100);

                                    // Get progression rule for this semester
                                    var progressionRule = await _progressionService.GetApplicableProgressionRuleAsync(
                                        student,
                                        failedPercentage,
                                        semester,  // Use current semester being processed
                                        highestAttemptInSemester  // Pass highest attempt in semester
                                    );

                                    if (progressionRule != null)
                                    {
                                        semesterResults.AcademicStanding = progressionRule.Action;
                                    }
                                }
                            }

                            yearResults.Semesters.Add(semesterResults);
                        }
                    }

                    // Calculate year GPA
                    yearResults.GPA = yearCreditsAttempted > 0 ? yearGpaPoints / yearCreditsAttempted : 0;
                    yearResults.CreditsAttempted = yearCreditsAttempted;
                    yearResults.CreditsEarned = yearCreditsEarned;
                    yearResults.FailedCourses = yearFailedCourses;

                    // Year-level academic standing (optional - could be derived from semesters or left null)
                    // Uncomment if you want year-level standing as well
                    /*
                    int totalCoursesInYear = yearResults.GetTotalCourses();
                    if (totalCoursesInYear > 0)
                    {
                        int yearFailedPercentage = (int)Math.Floor(((double)yearFailedCourses / totalCoursesInYear) * 100);
                        var yearProgressionRule = await _progressionService.GetApplicableProgressionRuleAsync(
                            student,
                            yearFailedPercentage,
                            student.CurrentSemester,
                            yearResults.Semesters.Max(s => s.Courses.Max(c => c.Attempt))
                        );

                        if (yearProgressionRule != null)
                        {
                            yearResults.AcademicStanding = yearProgressionRule.Action;
                        }
                    }
                    */

                    viewModel.AcademicYears.Add(yearResults);
                    ViewBag.StudentId = actualStudentId;
                }

                // Calculate overall GPA
                viewModel.OverallGPA = totalCreditsAttempted > 0 ? totalGpaPoints / totalCreditsAttempted : 0;
                viewModel.TotalCreditsAttempted = totalCreditsAttempted;
                viewModel.TotalCreditsEarned = totalCreditsEarned;

                _logger.LogInformation($"Results loaded for student {student.Id}: {viewModel.AcademicYears.Count} years, Overall GPA: {viewModel.OverallGPA}");

                if(studentId != null)
                {
                    return PartialView("~/Views/StudentResults/Results.cshtml", viewModel);
                }
                else
                {
                    return View(viewModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student results");
                TempData["Error"] = "An error occurred while loading your results. Please try again.";
                return RedirectToAction("Student_Dashboard", "Home");
            }
        }

        /*public async Task<IActionResult> Results()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                var student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student == null) return NotFound("Student record not found.");

                _logger.LogInformation($"Processing results for student: {student.Id}");

                // Check if student can view complete results (no outstanding fees)
                decimal outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
                bool canViewCompleteResults = outstandingBalance <= 0;

                var viewModel = new StudentResultsViewModel
                {
                    OutstandingFees = outstandingBalance,
                    OverallGPA = 0.0M,
                    AcademicYears = new List<AcademicYearResults>(),
                    Grades = new List<GradeConfiguration>(), // Empty since view provides grades
                    CoursePassMarks = new Dictionary<int, double>(),
                    CanViewCompleteResults = canViewCompleteResults
                };

                // **Query view once for all student results - view has pre-computed grades**
                var allStudentResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw(@"
                SELECT * FROM VW_StudentResults 
                WHERE StudentId_Number = {0} AND ApprovalStatus = {1}
                ORDER BY AcademicYearId, Semester, CourseCode",
                        student.StudentId_Number, "7")
                    .ToListAsync();

                if (!allStudentResults.Any())
                {
                    _logger.LogInformation($"No results found for student {student.Id}");
                    return View(viewModel);
                }

                // **Batch fetch academic years**
                var academicYearIds = allStudentResults.Select(r => r.AcademicYearId).Distinct().ToList();
                var academicYearsDict = await _context.AcademicYears
                    .Where(ay => academicYearIds.Contains(ay.YearId))
                    .ToDictionaryAsync(ay => ay.YearId);

                // **Batch fetch courses for credits and pass marks**
                var courseCodePairs = allStudentResults
                    .Select(r => new { r.CourseCode, r.YearOfStudy, r.Semester })
                    .Distinct()
                    .ToList();

                var allCourses = await _context.Courses
                    .Where(c => courseCodePairs.Select(p => p.CourseCode).Contains(c.CourseCode))
                    .ToListAsync();

                // Create lookup with fallback logic
                var coursesDict = new Dictionary<string, Course>();
                foreach (var pair in courseCodePairs)
                {
                    var key = $"{pair.CourseCode}_{pair.YearOfStudy}_{pair.Semester}";

                    // Try exact match first
                    var course = allCourses.FirstOrDefault(c =>
                        c.CourseCode == pair.CourseCode &&
                        c.YearTaken == pair.YearOfStudy &&
                        c.SemesterTaken == pair.Semester);

                    // Fallback to just course code
                    if (course == null)
                    {
                        course = allCourses.FirstOrDefault(c => c.CourseCode == pair.CourseCode);
                    }

                    if (course != null)
                    {
                        coursesDict[key] = course;
                    }
                }

                decimal totalGpaPoints = 0;
                int totalCreditsAttempted = 0;
                int totalCreditsEarned = 0;
                int overallFailedCourses = 0;

                // Group by academic year
                var yearGroups = allStudentResults
                    .GroupBy(r => r.AcademicYearId)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var yearGroup in yearGroups)
                {
                    var yearId = yearGroup.Key;
                    var yearData = yearGroup.ToList();

                    if (!academicYearsDict.TryGetValue(yearId, out var academicYear))
                    {
                        _logger.LogWarning($"Academic year {yearId} not found");
                        continue;
                    }

                    var yearResults = new AcademicYearResults
                    {
                        YearId = yearId,
                        YearValue = academicYear.YearValue,
                        Semesters = new List<SemesterResults>(),
                        AcademicStanding = null
                    };

                    decimal yearGpaPoints = 0;
                    int yearCreditsAttempted = 0;
                    int yearCreditsEarned = 0;
                    int yearFailedCourses = 0;
                    int? highestAttemptAcrossYear = null; // Track highest attempt for the year

                    // Group by semester
                    var semesterGroups = yearData
                        .GroupBy(r => r.Semester)
                        .OrderBy(g => g.Key)
                        .ToList();

                    foreach (var semesterGroup in semesterGroups)
                    {
                        var semester = semesterGroup.Key;
                        var semesterData = semesterGroup.ToList();

                        var semesterCourses = new List<CourseResult>();

                        foreach (var result in semesterData)
                        {
                            try
                            {
                                // Track highest attempt across all courses in the year
                                if (highestAttemptAcrossYear == null || result.Attempt > highestAttemptAcrossYear)
                                {
                                    highestAttemptAcrossYear = result.Attempt;
                                }

                                // Lookup course
                                var courseKey = $"{result.CourseCode}_{result.YearOfStudy}_{semester}";

                                if (!coursesDict.TryGetValue(courseKey, out var course))
                                {
                                    _logger.LogWarning($"Course {result.CourseCode} not found");
                                    continue;
                                }

                                // Add course pass mark to dictionary
                                viewModel.CoursePassMarks[course.Id] = course.PassMark;

                                // Build assessment scores dictionary from view
                                var scores = new Dictionary<string, AssessmentScoreInfo>();

                                if (result.CA.HasValue && result.CA.Value > 0)
                                {
                                    scores["CA"] = new AssessmentScoreInfo
                                    {
                                        Score = result.CA.Value,
                                        WeightPercentage = 0
                                    };
                                }

                                if (result.Exam.HasValue)
                                {
                                    scores["Exam"] = new AssessmentScoreInfo
                                    {
                                        Score = result.Exam.Value,
                                        WeightPercentage = 0
                                    };
                                }

                                // **Read pre-computed values directly from view**
                                decimal? totalScore = result.TotalScore;
                                string grade = result.GradeLetter;
                                bool isPassed = result.IsPassingGrade == 1;
                                decimal gradePoints = result.GPAValue.Value;

                                // Check for deferred exams
                                if (grade == "NE")
                                {
                                    yearResults.AcademicStanding = "DEF";
                                }

                                // Only include in GPA calculation if student can view complete results and not NE
                                if (canViewCompleteResults && grade != "NE")
                                {
                                    yearCreditsAttempted += course.Credits;
                                    totalCreditsAttempted += course.Credits;

                                    if (!isPassed)
                                    {
                                        yearFailedCourses++;
                                        overallFailedCourses++;
                                    }
                                    else
                                    {
                                        yearCreditsEarned += course.Credits;
                                        totalCreditsEarned += course.Credits;
                                    }

                                    // Add to GPA calculation
                                    yearGpaPoints += gradePoints * course.Credits;
                                    totalGpaPoints += gradePoints * course.Credits;
                                }

                                // View already filtered for approved/published results
                                bool isPublished = true;
                                bool courseCanViewCompleteResults = canViewCompleteResults;

                                semesterCourses.Add(new CourseResult
                                {
                                    CourseId = course.Id,
                                    CourseCode = result.CourseCode,
                                    CourseName = result.CourseName,
                                    Credits = course.Credits,
                                    Scores = scores,
                                    TotalScore = totalScore,
                                    Grade = grade,
                                    Remark = result.Description,
                                    IsPassed = isPassed,
                                    IsPublished = isPublished,
                                    CanViewComplete = courseCanViewCompleteResults
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error processing course result for CourseCode={result.CourseCode}");
                            }
                        }

                        if (semesterCourses.Any())
                        {
                            yearResults.Semesters.Add(new SemesterResults
                            {
                                SemesterId = semester,
                                Courses = semesterCourses
                            });
                        }
                    }

                    // Calculate year GPA
                    yearResults.GPA = yearCreditsAttempted > 0 ? yearGpaPoints / yearCreditsAttempted : 0;
                    yearResults.CreditsAttempted = yearCreditsAttempted;
                    yearResults.CreditsEarned = yearCreditsEarned;
                    yearResults.FailedCourses = yearFailedCourses;

                    // Determine academic standing for this year
                    int totalCoursesInYear = yearResults.GetTotalCourses();
                    if (totalCoursesInYear > 0 && string.IsNullOrEmpty(yearResults.AcademicStanding))
                    {
                        int failedPercentage = (int)Math.Floor(((double)yearFailedCourses / totalCoursesInYear) * 100);

                        // Pass the highest attempt to the progression rule method
                        var progressionRule = await _progressionService.GetApplicableProgressionRuleAsync(
                            student,
                            failedPercentage,
                            student.CurrentSemester,
                            highestAttemptAcrossYear  // Pass highest attempt
                        );

                        if (progressionRule != null)
                        {
                            yearResults.AcademicStanding = progressionRule.Action;
                        }
                    }

                    viewModel.AcademicYears.Add(yearResults);
                }

                // Calculate overall GPA
                viewModel.OverallGPA = totalCreditsAttempted > 0 ? totalGpaPoints / totalCreditsAttempted : 0;
                viewModel.TotalCreditsAttempted = totalCreditsAttempted;
                viewModel.TotalCreditsEarned = totalCreditsEarned;

                _logger.LogInformation($"Results loaded for student {student.Id}: {viewModel.AcademicYears.Count} years, Overall GPA: {viewModel.OverallGPA}");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student results");
                TempData["Error"] = "An error occurred while loading your results. Please try again.";
                return RedirectToAction("Student_Dashboard", "Home");
            }
        }*/

        private async Task<ProgressionRule?> GetApplicableProgressionRule(
            Student student,
            int totalFailedCourses,
            int? semester = null,
            int? attempt = null)
        {
            var studentSchoolId = await _context.Students
                .Where(s => s.Id == student.Id)
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Select(s => s.Programme.Department.School.Id)
                .FirstOrDefaultAsync();

            IQueryable<ProgressionRule> baseQuery = _context.ProgressionRules
                .Where(r => r.IsActive &&
                            r.PercentFailedOfCourseLoad >= totalFailedCourses);

            // Apply optional filters
            if (semester.HasValue)
                baseQuery = baseQuery.Where(r => r.Semester == semester.Value);

            if (attempt.HasValue)
                baseQuery = baseQuery.Where(r => r.Attempt == attempt.Value);

            ProgressionRule? progressionRule = null;

            // Try school-specific rule
            if (studentSchoolId > 0)
            {
                progressionRule = await baseQuery
                    .Where(r => r.SchoolId == studentSchoolId)
                    .OrderBy(r => r.PercentFailedOfCourseLoad)
                    .FirstOrDefaultAsync();
            }

            // Fall back to global rule
            if (progressionRule == null)
            {
                progressionRule = await baseQuery
                    .Where(r => r.SchoolId == null)
                    .OrderBy(r => r.PercentFailedOfCourseLoad)
                    .FirstOrDefaultAsync();
            }

            return progressionRule;
        }

        // Helper method to calculate weighted and normalized total
        private decimal CalculateWeightedNormalizedTotal(Dictionary<string, AssessmentScoreInfo> scores)
        {
            if (!scores.Any()) return 0;

            decimal weightedTotal = 0;
            decimal totalWeight = 0;

            foreach (var score in scores)
            {
                weightedTotal += (score.Value.Score * score.Value.WeightPercentage / 100);
                totalWeight += score.Value.WeightPercentage;
            }

            // Normalize to 100 if total weight is not 100
            decimal normalizedTotal = totalWeight > 0 ? (weightedTotal / totalWeight) * 100 : 0;

            // Cap at 100
            return Math.Min(normalizedTotal, 100);
        }

        /*private async Task<string> DetermineAcademicStanding(Student student, int failedCourses, decimal gpa)
        {
            var progressionRule = await GetApplicableProgressionRule(student, failedCourses);

            if (progressionRule == null)
            {
                // If no rule found, find the most severe rule (likely exclusion)
                progressionRule = await _context.ProgressionRules
                    .Where(r => r.IsActive && r.SchoolId == null) // Only check global rules for fallback
                    .OrderByDescending(r => r.PercentFailedOfCourseLoad)
                    .FirstOrDefaultAsync();
            }

            return progressionRule?.Action ?? "Academic Exclusion";
        }*/

        public async Task<IActionResult> AssessmentManagement()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                bool isHOD = await _userManager.IsInRoleAsync(user, "HOD");
                bool isLecturer = await _userManager.IsInRoleAsync(user, "Lecturer");
                bool isDean = await _userManager.IsInRoleAsync(user, "Dean");

                if (!isHOD && !isLecturer && !isDean)
                {
                    return RedirectToAction("AccessDenied", "Account");
                }

                var courseIds = new List<int>();

                if (isDean)
                {
                    var school = await _context.Schools
                        .Include(s => s.Departments)
                            .ThenInclude(d => d.Programmes)
                        .FirstOrDefaultAsync(s => s.DeanId == user.Id);

                    if (school != null)
                    {
                        var programmeIds = school.Departments
                            .SelectMany(d => d.Programmes)
                            .Select(p => p.Id)
                            .ToList();

                        courseIds = await _context.Courses
                            .Where(c => programmeIds.Contains(c.ProgrammeID))
                            .Select(c => c.Id)
                            .ToListAsync();
                    }
                }
                else if (isHOD)
                {
                    var department = await _context.Departments
                        .Include(d => d.Programmes)
                        .FirstOrDefaultAsync(d => d.HODId == user.Id);

                    if (department != null)
                    {
                        var programmeIds = department.Programmes.Select(p => p.Id).ToList();

                        courseIds = await _context.Courses
                            .Where(c => programmeIds.Contains(c.ProgrammeID))
                            .Select(c => c.Id)
                            .ToListAsync();
                    }
                }
                else // Lecturer
                {
                    courseIds = await _context.Courses
                        .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == user.Id) ||
                                   c.InstructorId == user.Id)
                        .Select(c => c.Id)
                        .ToListAsync();
                }

                // Get ALL active academic years
                var academicYears = await _context.AcademicYears
                    .Where(ay => ay.IsActive)
                    .OrderByDescending(ay => ay.YearValue)
                    .ToListAsync();

                // Get student course registrations for the courses this user manages
                var registrations = await _context.StudentCourseRegistrations
                    .Include(scr => scr.Course)
                        .ThenInclude(c => c.Programme)
                    .Include(scr => scr.Course.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .Include(scr => scr.Student)
                    .Include(scr => scr.AcademicYear)
                    .Where(scr => courseIds.Contains(scr.CourseId))
                    .ToListAsync();

                // Get existing StudentCourseResults for tracking assessment progress
                var courseResults = await _context.StudentCourseResults
                    .Where(r => courseIds.Contains(r.CourseId))
                    .ToListAsync();

                // Manually load assessment scores for each result
                foreach (var result in courseResults)
                {
                    result.AssessmentScores = await _context.StudentAssessmentScores
                        .Where(s =>
                            s.StudentId == result.StudentId &&
                            s.CourseId == result.CourseId &&
                            s.AcademicYearId == result.AcademicYearId &&
                            s.Semester == result.Semester &&
                            s.IsActive)
                        .ToListAsync();
                }

                var viewModel = new CourseAssessmentViewModel
                {
                    IsHOD = isHOD || isDean,
                    AcademicYears = new List<AcademicYearCourses>()
                };

                foreach (var academicYear in academicYears)
                {
                    // Get registrations for this specific academic year
                    var yearRegistrations = registrations
                        .Where(r => r.AcademicYearId == academicYear.YearId)
                        .ToList();

                    // Group by course and semester to create course details
                    var yearCourses = yearRegistrations
                        .GroupBy(r => new
                        {
                            r.CourseId,
                            r.Course.CourseCode,
                            r.Course.CourseName,
                            ProgrammeName = r.Course.Programme?.Name ?? "N/A",
                            r.Semester
                        })
                        .Select(cg =>
                        {
                            var courseRegistrations = cg.ToList();
                            var course = cg.First().Course;
                            var totalAssessments = course.CourseAssessments?.Count ?? 0;
                            var totalStudents = courseRegistrations.Count;
                            var totalRequired = totalStudents * totalAssessments;

                            // Check for existing results/scores for this course and academic year
                            var existingResults = courseResults
                                .Where(r => r.CourseId == cg.Key.CourseId &&
                                           r.AcademicYearId == academicYear.YearId &&
                                           r.Semester == cg.Key.Semester)
                                .ToList();

                            var totalAssessed = 0;
                            var isPublished = false;

                            if (existingResults.Any())
                            {
                                // Count actual assessment scores that have been entered
                                totalAssessed = existingResults
                                    .SelectMany(r => r.AssessmentScores ?? new List<StudentAssessmentScore>())
                                    .Count(s => s.IsActive && s.Score > 0);

                                // Check if all results are published
                                isPublished = existingResults.Any() &&
                                             existingResults.All(r => r.Status == Status.Published);
                            }

                            var pendingAssessments = totalRequired - totalAssessed;
                            var progressPercentage = totalRequired > 0
                                ? Math.Round(((double)totalAssessed / totalRequired) * 100, 1)
                                : 0;

                            var assessmentSummary = $"{totalAssessed}/{totalRequired} completed";

                            string assessmentStatus;
                            if (isPublished)
                                assessmentStatus = "Published";
                            else if (pendingAssessments == 0 && totalRequired > 0)
                                assessmentStatus = "All Graded";
                            else if (totalAssessed == 0)
                                assessmentStatus = "Not Started";
                            else
                                assessmentStatus = "In Progress";

                            return new CourseDetails
                            {
                                CourseId = cg.Key.CourseId,
                                CourseCode = cg.Key.CourseCode,
                                CourseName = cg.Key.CourseName,
                                ProgrammeName = cg.Key.ProgrammeName,
                                Semester = cg.Key.Semester,
                                EnrolledStudentsCount = totalStudents,
                                AssessmentsString = assessmentSummary,
                                PendingAssessments = pendingAssessments,
                                GradedPercentage = progressPercentage,
                                AssessmentStatus = assessmentStatus
                            };
                        })
                        .OrderBy(c => c.Semester)
                        .ThenBy(c => c.CourseCode)
                        .ToList();

                    // Always add the academic year, even if no registrations exist
                    viewModel.AcademicYears.Add(new AcademicYearCourses
                    {
                        YearId = academicYear.YearId,
                        YearValue = academicYear.YearValue,
                        Courses = yearCourses,
                        IsPublished = yearCourses.Any() && yearCourses.All(c => c.AssessmentStatus == "Published")
                    });

                    _logger.LogInformation($"Academic Year {academicYear.YearValue}: Found {yearCourses.Count} courses with registrations");
                }

                if (viewModel.AcademicYears.Any())
                {
                    viewModel.SelectedYearId = viewModel.AcademicYears.First().YearId;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AssessmentManagement: {Message}", ex.Message);
                return RedirectToAction("Error", "Home");
            }
        }

        private string DetermineAssessmentStatus(int totalRequired, int pendingAssessments, bool isPublished)
        {
            if (isPublished)
                return "Published";
            if (pendingAssessments == 0)
                return "All Graded";
            if (pendingAssessments == totalRequired)
                return "Not Started";
            return "In Progress";
        }

        private double CalculateGradedPercentage(List<dynamic> courseStudents)
        {
            if (!courseStudents.Any()) return 0;

            var gradedCount = courseStudents.Count(c =>
                !string.IsNullOrEmpty(c.AssessmentScores) &&
                !c.AssessmentScores.Contains("\"score\":\"-\""));

            return (gradedCount * 100.0) / courseStudents.Count;
        }


        [HttpGet]
        public async Task<IActionResult> CourseAssessments(int id) // id is the CourseId
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var course = await _context.Courses
                    .Include(c => c.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (course == null)
                {
                    return NotFound("Course not found.");
                }

                // Get active grade configurations
                var grades = await _context.GradeConfigurations
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.MinScore)
                    .ToListAsync();

                // Get students registered for this course
                // FIX: Use separate Include chains and AsSplitQuery to avoid EF navigation property loading issues
                var enrolledStudentRegistrations = await _context.StudentCourseRegistrations
                    .Include(scr => scr.AcademicYear)                    // Load Registration's AcademicYear
                    .Include(scr => scr.Student)                         // Load Student
                    .Include(scr => scr.Student.ModeOfStudy)            // Load Student's ModeOfStudy
                    .Include(scr => scr.Student.AcademicYear)           // Load Student's AcademicYear
                    .AsSplitQuery()                                      // Use split queries for better reliability
                    .Where(scr => scr.CourseId == id)
                    .ToListAsync();

                _logger.LogInformation($"Loaded {enrolledStudentRegistrations.Count} registrations for CourseId {id}");

                // Defensive: Log and filter any records where navigation properties failed to load
                var invalidRecords = enrolledStudentRegistrations
                    .Where(e => e.AcademicYear == null || e.Student == null ||
                                e.Student.ModeOfStudy == null || e.Student.AcademicYear == null)
                    .ToList();

                if (invalidRecords.Any())
                {
                    _logger.LogError(
                        $"CourseId {id}: Failed to load navigation properties for {invalidRecords.Count} registrations. " +
                        $"IDs: {string.Join(", ", invalidRecords.Select(r => r.Id))}");
                }

                // Group students by academic year, semester, and mode of study
                // Only process records where all navigation properties loaded successfully
                var groupedStudents = enrolledStudentRegistrations
                    .Where(e => e.AcademicYear != null &&
                                e.Student != null &&
                                e.Student.ModeOfStudy != null &&
                                e.Student.AcademicYear != null)
                    .GroupBy(e => new
                    {
                        AcademicYearId = e.AcademicYearId,
                        Semester = e.Semester,
                        AcademicYear = e.AcademicYear.YearValue,
                        ModeOfStudyId = e.Student.ModeOfStudyId,
                        ModeOfStudy = e.Student.ModeOfStudy.ModeName
                    })
                    .OrderBy(g => g.Key.AcademicYear)
                    .ThenBy(g => g.Key.Semester)
                    .ThenBy(g => g.Key.ModeOfStudy)
                    .ToList();

                _logger.LogInformation($"Created {groupedStudents.Count} groups for CourseId {id}");

                var viewModel = new CourseAssessmentDetailsViewModel
                {
                    CourseId = course.Id,
                    CourseCode = course.CourseCode,
                    CourseName = course.CourseName,
                    AssessmentGroups = new List<AssessmentGroup>(),
                    Assessments = course.CourseAssessments
                        .Select(ca => new SIS.Models.Admin.AssessmentInfo
                        {
                            Id = ca.AssessmentId,
                            Name = ca.Assessment.Name,
                            WeightPercentage = ca.Assessment.WeightPercentage
                        })
                        .ToList(),
                    Grades = grades
                };

                // Process each group
                foreach (var group in groupedStudents)
                {
                    var studentAssessments = new List<StudentAssessment>();
                    foreach (var registrationEntry in group)
                    {
                        var student = registrationEntry.Student;
                        try
                        {
                            var scores = new Dictionary<int, SIS.Models.Admin.AssessmentScore>();

                            // Get assessment scores from the new table using the service
                            var assessmentScores = await _assessmentScoreService.GetScoresForStudentAsync(
                                student.Id,
                                course.Id,
                                registrationEntry.AcademicYearId);

                            // Check integrity for each score
                            foreach (var scoreRecord in assessmentScores)
                            {
                                bool isValid = _integrityService.VerifyScoreHash(scoreRecord);

                                if (!isValid)
                                {
                                    _logger.LogWarning(
                                        "TAMPER DETECTED: StudentId={StudentId}, CourseId={CourseId}, AssessmentId={AssessmentId}, ScoreId={ScoreId}",
                                        student.Id, course.Id, scoreRecord.AssessmentId, scoreRecord.Id);
                                }
                            }

                            // Populate scores dictionary for each assessment defined for the course
                            foreach (var ca in course.CourseAssessments)
                            {
                                var existingScore = assessmentScores.FirstOrDefault(s =>
                                    s.AssessmentId == ca.AssessmentId);

                                // Check if this specific score is tampered
                                bool isTampered = false;
                                string originalScoreInfo = null;

                                if (existingScore != null)
                                {
                                    isTampered = !_integrityService.VerifyScoreHash(existingScore);

                                    if (isTampered)
                                    {
                                        // Try to calculate what the original score might have been
                                        originalScoreInfo = $"Score: {existingScore.Score}, Weight: {existingScore.WeightPercentage}%, " +
                                            $"Recorded: {existingScore.RecordedAt:yyyy-MM-dd HH:mm}, " +
                                            $"By: {existingScore.RecordedBy}";
                                    }
                                }

                                scores.Add(ca.AssessmentId, new SIS.Models.Admin.AssessmentScore
                                {
                                    AssessmentName = ca.Assessment.Name,
                                    Score = existingScore?.Score ?? 0,
                                    WeightPercentage = ca.Assessment.WeightPercentage,
                                    IsTampered = isTampered,
                                    TamperDetails = originalScoreInfo,
                                    ScoreId = existingScore?.Id
                                });
                            }

                            studentAssessments.Add(new StudentAssessment
                            {
                                StudentId = student.Id,
                                StudentName = student.FullName,
                                StudentNumber = student.StudentId_Number,
                                Scores = scores
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Error loading assessment scores for student {student.Id} in course {course.Id}");
                            continue;
                        }
                    }

                    viewModel.AssessmentGroups.Add(new AssessmentGroup
                    {
                        GroupId = $"{group.Key.AcademicYearId}-{group.Key.ModeOfStudyId}",
                        AcademicYearId = group.Key.AcademicYearId,
                        Semester = group.Key.Semester,
                        AcademicYear = group.Key.AcademicYear,
                        ModeOfStudy = group.Key.ModeOfStudy,
                        StudentAssessments = studentAssessments
                    });
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading course assessments for CourseId {id}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner Exception:");
                }
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAssessmentScores([FromBody] List<StudentScoreUpdateModel> students)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                foreach (var student in students)
                {
                    try
                    {
                        // Use academic year and semester from the registration context passed from frontend
                        int academicYearId = student.AcademicYearId;
                        int semester = student.Semester;

                        // Validate student exists (basic check)
                        var studentExists = await _context.Students.AsNoTracking()
                            .FirstOrDefaultAsync(s => s.Id == student.StudentId);

                        if (studentExists == null)
                        {
                            errors.Add($"Student {student.StudentId} not found");
                            errorCount++;
                            continue;
                        }

                        foreach (var scoreData in student.AssessmentScores)
                        {
                            try
                            {
                                // Check if score exists
                                bool exists = await _assessmentScoreService.ScoreExistsAsync(
                                    student.StudentId,
                                    student.CourseId,
                                    scoreData.Key,
                                    academicYearId,
                                    semester);

                                if (exists)
                                {
                                    // Update existing score
                                    var existingScore = await _context.StudentAssessmentScores
                                        .FirstOrDefaultAsync(s =>
                                            s.StudentId == student.StudentId &&
                                            s.CourseId == student.CourseId &&
                                            s.AssessmentId == scoreData.Key &&
                                            s.AcademicYearId == academicYearId &&
                                            s.Semester == semester &&
                                            s.IsActive);

                                    if (existingScore != null && existingScore.Score != scoreData.Value.Score)
                                    {
                                        await _assessmentScoreService.UpdateScoreAsync(
                                            existingScore.Id,
                                            scoreData.Value.Score,
                                            user.Id,
                                            "Score updated via web interface",
                                            existingScore.rsbId.Value);
                                    }
                                }
                                else
                                {
                                    // Record new score
                                    await _assessmentScoreService.RecordScoreAsync(
                                        student.StudentId,
                                        student.CourseId,
                                        academicYearId,
                                        scoreData.Key,
                                        semester,
                                        scoreData.Value.Score,
                                        user.Id,
                                        0,
                                        (int)studentExists.StudentCurrentYear,
                                        1,
                                        "Score recorded via web interface");
                                }

                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error processing assessment {scoreData.Key} for student {student.StudentId}: {ex.Message}");

                                // ⭐ MODIFIED: More helpful error message
                                string errorMessage = ex.Message;
                                if (ex is InvalidOperationException && ex.Message.Contains("integrity"))
                                {
                                    errorMessage = "Score integrity issue detected - please verify and re-enter";
                                }

                                errors.Add($"Assessment {scoreData.Key}: {errorMessage}");
                                errorCount++;
                            }
                        }

                        // Calculate/Recalculate course result
                        try
                        {
                            await _resultCalculationService.CalculateResultAsync(
                                student.StudentId,
                                student.CourseId,
                                academicYearId,
                                semester,
                                user.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Could not calculate result for student {student.StudentId}: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error updating scores for student {student.StudentId}: {ex.Message}");
                        errors.Add($"Student {student.StudentId}: {ex.Message}");
                        errorCount++;
                    }
                }

                if (errorCount == 0)
                {
                    return Json(new { success = true, message = $"All scores updated successfully ({successCount} scores)" });
                }
                else if (successCount > 0)
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Partially successful: {successCount} scores updated, {errorCount} errors",
                        errors = errors
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = "Failed to update scores",
                        errors = errors
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating assessment scores: {ex.Message}");
                return Json(new { success = false, message = "Error updating scores: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PublishResults(int yearId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                bool isHOD = await _userManager.IsInRoleAsync(user, "HOD");
                bool isDean = await _userManager.IsInRoleAsync(user, "Dean");

                if (!isHOD && !isDean)
                {
                    return Json(new { success = false, message = "Unauthorized access" });
                }

                // Check if academic year exists
                var academicYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(ay => ay.YearId == yearId);
                if (academicYear == null)
                {
                    return Json(new { success = false, message = "Academic year not found" });
                }

                List<int> courseIds = new List<int>();

                if (isDean)
                {
                    var school = await _context.Schools
                        .Include(s => s.Departments)
                            .ThenInclude(d => d.Programmes)
                        .FirstOrDefaultAsync(s => s.DeanId == user.Id);

                    if (school == null)
                    {
                        return Json(new { success = false, message = "School not found or you are not assigned as Dean" });
                    }

                    var programmeIds = school.Departments
                        .SelectMany(d => d.Programmes)
                        .Select(p => p.Id)
                        .ToList();

                    courseIds = await _context.Courses
                        .Where(c => programmeIds.Contains(c.ProgrammeID))
                        .Select(c => c.Id)
                        .ToListAsync();
                }
                else if (isHOD)
                {
                    var department = await _context.Departments
                        .Include(d => d.Programmes)
                        .FirstOrDefaultAsync(d => d.HODId == user.Id);

                    if (department == null)
                    {
                        return Json(new { success = false, message = "Department not found or you are not assigned as HOD" });
                    }

                    var programmeIds = department.Programmes.Select(p => p.Id).ToList();

                    courseIds = await _context.Courses
                        .Where(c => programmeIds.Contains(c.ProgrammeID))
                        .Select(c => c.Id)
                        .ToListAsync();
                }

                if (!courseIds.Any())
                {
                    return Json(new { success = false, message = "No courses found under your jurisdiction" });
                }

                // Publish results using the new service
                int publishedCount = 0;

                foreach (var courseId in courseIds)
                {
                    // Publish for both semesters
                    for (int semester = 1; semester <= 2; semester++)
                    {
                        try
                        {
                            bool published = await _resultCalculationService.PublishResultsForCourseAsync(
                                courseId,
                                yearId,
                                semester,
                                user.Id);

                            if (published)
                            {
                                var count = await _context.StudentCourseResults
                                    .CountAsync(r =>
                                        r.CourseId == courseId &&
                                        r.AcademicYearId == yearId &&
                                        r.Semester == semester &&
                                        r.Status == Status.Published);

                                publishedCount += count;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Could not publish results for CourseId {courseId}, Semester {semester}: {ex.Message}");
                        }
                    }
                }

                string roleDescription = isDean ? "Dean" : "HOD";
                _logger.LogInformation($"{roleDescription} {user.UserName} published {publishedCount} results for academic year {academicYear.YearValue}");

                TempData["Success"] = $"Results published successfully! {publishedCount} results updated.";
                return Json(new { success = true, message = $"Successfully published {publishedCount} results" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error publishing results: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while publishing results" });
            }
        }


        [HttpPost]
        public async Task<IActionResult> ConfirmProgression(int academicYearId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                var student = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student == null) return NotFound();

                // Call the progression service
                var result = await _progressionService.ExecuteProgressionAsync(
                    student.Id,
                    academicYearId,
                    user.Id);

                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    _logger.LogInformation(
                        "Student {StudentNumber} progressed with action {Action}",
                        student.StudentId_Number, result.Action);
                    return RedirectToAction("Student_Dashboard", "Home");
                }
                else
                {
                    var errorMessage = result.Errors.Any()
                        ? string.Join(", ", result.Errors)
                        : "An error occurred while processing progression";
                    TempData["Error"] = errorMessage;
                    return RedirectToAction("Results");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing progression");
                TempData["Error"] = "An error occurred while processing progression";
                return RedirectToAction("Results");
            }
        }

        private void UpdateStudentProgression(Student student, AcademicYear nextAcademicYear,
            string progressionAction, bool isOnProbation)
        {
            if (student.Programme.IsSemesterBased)
            {
                if (student.CurrentSemester == 1)
                {
                    student.CurrentSemester = 2;
                }
                else
                {
                    student.StudentCurrentYear += 1;
                    student.CurrentSemester = 1;
                }
            }
            else
            {
                student.StudentCurrentYear += 1;
                student.CurrentSemester = 1;
            }

            student.AcademicYearId = nextAcademicYear.YearId;
            student.RegistrationStatus = isOnProbation ? Status.AcademicProbation : Status.Unregistered;
            student.IsRegistered = false;
            student.RegistrationDate = null;
        }

        private string GetProgressionMessage(string progressionAction, bool isSemesterBased) => progressionAction switch
        {
            "Proceed" => isSemesterBased ? "Successfully progressed to next semester/year" : "Successfully progressed to next academic year",
            "ProceedWithRepeat" => isSemesterBased ? "Progressed to next semester/year with units to repeat" : "Progressed to next year with units to repeat",
            "ProceedOnProbation" => isSemesterBased ? "Progressed to next semester/year on academic probation" : "Progressed to next year on academic probation",
            "RepeatYear" => "Set to repeat current academic year",
            "RepeatSemester" => "Set to repeat current semester",
            "Exclude" => "Academic exclusion processed - please contact academic office",
            "Withdraw" => "Withdrawal processed - please contact academic office",
            _ => "Progression status updated"
        };

        private string GetProgressionRemarks(string progressionAction, int failedCourses,
            bool isSemesterBased, int? currentSemester) => progressionAction switch
            {
                "Proceed" => isSemesterBased ?
            $"Clear pass - proceed to {(currentSemester == 1 ? "semester 2" : "next academic year")}" :
            "Clear pass - proceed to next academic year",
                "ProceedWithRepeat" => isSemesterBased ?
            $"Proceed to {(currentSemester == 1 ? "semester 2" : "next academic year")} with {failedCourses} failed units to repeat" :
            $"Proceed to next year with {failedCourses} failed units to repeat",
                "ProceedOnProbation" => isSemesterBased ?
            $"Proceed to {(currentSemester == 1 ? "semester 2" : "next academic year")} on academic probation - must improve performance" :
            "Proceed to next year on academic probation - must improve performance",
                "RepeatYear" => "Must repeat current academic year",
                "RepeatSemester" => "Must repeat current semester",
                "Exclude" => "Academic exclusion due to poor performance",
                "Withdraw" => "Withdrawal recommended based on academic performance",
                _ => "Contact academic advisor for guidance"
            };

        private string CalculateOverallGrade(decimal gpa)
        {
            if (gpa >= 4.0m) return "A";
            if (gpa >= 3.5m) return "B+";
            if (gpa >= 3.0m) return "B";
            if (gpa >= 2.5m) return "C+";
            if (gpa >= 2.0m) return "C";
            if (gpa >= 1.5m) return "D+";
            if (gpa >= 1.0m) return "D";
            return "F";
        }

        private async Task CreateCarryoverCoursesFromResults(Student student, List<int> failedCourseIds,
            string progressionAction, string userId)
        {
            var actionsWithCarryover = new[] { "ProceedWithRepeat", "RepeatYear", "RepeatSemester", "ProceedOnProbation" };

            if (!actionsWithCarryover.Contains(progressionAction))
            {
                return;
            }

            var carryoverCourses = new List<StudentCarryoverCourse>();

            // Get the failed course results to extract semester information
            var failedResults = await _context.StudentCourseResults
                .Where(r => r.StudentId == student.Id &&
                           failedCourseIds.Contains(r.CourseId) &&
                           r.AcademicYearId == student.AcademicYearId)
                .ToListAsync();

            foreach (var failedResult in failedResults)
            {
                var existingCarryover = await _context.StudentCarryoverCourses
                    .FirstOrDefaultAsync(scc => scc.StudentId == student.Id &&
                                               scc.CourseId == failedResult.CourseId &&
                                               scc.IsActive);

                if (existingCarryover == null)
                {
                    var carryover = new StudentCarryoverCourse
                    {
                        StudentId = student.Id,
                        CourseId = failedResult.CourseId,
                        OriginalAcademicYearId = failedResult.AcademicYearId,
                        OriginalSemester = failedResult.Semester,
                        Reason = "Failed",
                        IsActive = true,
                        CarryoverDate = DateTime.Now,
                        Notes = $"Failed during {progressionAction} - carried over from academic year {student.AcademicYear?.YearValue}",
                        CreatedAt = DateTime.Now,
                        CreatedBy = userId
                    };

                    carryoverCourses.Add(carryover);
                }
            }

            if (carryoverCourses.Any())
            {
                _context.StudentCarryoverCourses.AddRange(carryoverCourses);
                _logger.LogInformation($"Created {carryoverCourses.Count} carryover courses for student {student.StudentId_Number}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTamperDetails(int scoreId)
        {
            try
            {
                var score = await _assessmentScoreService.GetScoreByIdAsync(scoreId);

                if (score == null)
                {
                    return Json(new { success = false, message = "Score not found" });
                }

                // Verify tampering
                bool isTampered = !_integrityService.VerifyScoreHash(score);

                if (!isTampered)
                {
                    return Json(new { success = false, message = "No tampering detected" });
                }

                // Build detailed information
                var details = $@"
            <div class='space-y-2 text-sm'>
                <div><strong>Current Score:</strong> {score.Score:F2}</div>
                <div><strong>Weight:</strong> {score.WeightPercentage:F1}%</div>
                <div><strong>Max Score:</strong> {score.MaxScore}</div>
                <div><strong>Recorded By:</strong> {score.RecordedBy}</div>
                <div><strong>Recorded At:</strong> {score.RecordedAt:yyyy-MM-dd HH:mm:ss} UTC</div>
                {(score.ModifiedAt.HasValue ? $"<div class='text-orange-600'><strong>Last Modified:</strong> {score.ModifiedAt:yyyy-MM-dd HH:mm:ss} UTC</div>" : "")}
                {(!string.IsNullOrEmpty(score.ModifiedBy) ? $"<div class='text-orange-600'><strong>Modified By:</strong> {score.ModifiedBy}</div>" : "")}
                <div class='mt-2 pt-2 border-t'>
                    <strong>Current Hash:</strong><br/>
                    <code class='text-xs break-all'>{score.ScoreHash}</code>
                </div>
                <div class='text-red-600 font-medium mt-2'>
                    ⚠️ Hash verification failed - data has been altered
                </div>
            </div>
        ";

                return Json(new { success = true, details = details });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tamper details for ScoreId={ScoreId}", scoreId);
                return Json(new { success = false, message = "Error retrieving details" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadSemesterTranscript(int studentId, int academicYearId, int semester)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                var student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null) return NotFound("Student record not found.");

                // Check outstanding fees
                decimal outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
                if (outstandingBalance > 0)
                {
                    return BadRequest("Please clear your outstanding fees to download transcripts.");
                }

                // Get academic year
                var academicYear = await _context.AcademicYears.FindAsync(academicYearId);
                if (academicYear == null) return NotFound("Academic year not found.");

                // Get semester results
                var semesterResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw(@"
                        WITH RankedResults AS
                        (
                            SELECT *,
                                   ROW_NUMBER() OVER
                                   (
                                       PARTITION BY StudentId_Number, CourseCode
                                       ORDER BY Attempt DESC
                                   ) AS rn
                            FROM VW_StudentResults
                            WHERE StudentId_Number = {0}
                              AND AcademicYearId = {1}
                              AND Semester = {2}
                              AND ApprovalStatus = {3}
                        )
                        SELECT *
                        FROM RankedResults
                        WHERE rn = 1
                        ORDER BY CourseCode",
                        student.StudentId_Number,
                        academicYearId,
                        semester,
                        "7")
                    .AsNoTracking()
                    .ToListAsync();

                if (!semesterResults.Any())
                {
                    return NotFound("No results found for this semester.");
                }

                // Generate PDF
                var pdfBytes = await GenerateSemesterTranscriptPDF(student, academicYear, semester, semesterResults);

                return File(pdfBytes, "application/pdf", $"Semester_{semester}_Transcript_{academicYear.YearValue}_{student.StudentId_Number}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating semester transcript for student {StudentId}", studentId);
                return StatusCode(500, "An error occurred while generating the transcript.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadYearTranscript(int studentId, int academicYearId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                var student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null) return NotFound("Student record not found.");

                // Check outstanding fees
                decimal outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
                if (outstandingBalance > 0)
                {
                    return BadRequest("Please clear your outstanding fees to download transcripts.");
                }

                // Get academic year
                var academicYear = await _context.AcademicYears.FindAsync(academicYearId);
                if (academicYear == null) return NotFound("Academic year not found.");

                // Get year results
                var yearResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw(@"
                        WITH RankedResults AS
                        (
                            SELECT *,
                                   ROW_NUMBER() OVER
                                   (
                                       PARTITION BY StudentId_Number, CourseCode
                                       ORDER BY Attempt DESC
                                   ) AS rn
                            FROM VW_StudentResults
                            WHERE StudentId_Number = {0}
                              AND AcademicYearId = {1}
                              AND ApprovalStatus = {2}
                        )
                        SELECT *
                        FROM RankedResults
                        WHERE rn = 1
                        ORDER BY Semester, CourseCode",
                        student.StudentId_Number,
                        academicYearId,
                        "7")
                    .AsNoTracking()
                    .ToListAsync();

                if (!yearResults.Any())
                {
                    return NotFound("No results found for this academic year.");
                }

                // Generate PDF
                var pdfBytes = GenerateYearTranscriptPDF(student, academicYear, yearResults);

                return File(pdfBytes, "application/pdf", $"Year_Transcript_{academicYear.YearValue}_{student.StudentId_Number}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating year transcript for student {StudentId}", studentId);
                return StatusCode(500, "An error occurred while generating the transcript.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFullTranscript(int studentId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                var student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null) return NotFound("Student record not found.");

                // Check outstanding fees
                decimal outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
                if (outstandingBalance > 0)
                {
                    return BadRequest("Please clear your outstanding fees to download transcripts.");
                }

                // Get all results
                var allResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw(@"
                        WITH RankedResults AS
                        (
                            SELECT *,
                                   ROW_NUMBER() OVER
                                   (
                                       PARTITION BY StudentId_Number, CourseCode
                                       ORDER BY Attempt DESC
                                   ) AS rn
                            FROM VW_StudentResults
                            WHERE StudentId_Number = {0}
                              AND ApprovalStatus = {1}
                        )
                        SELECT *
                        FROM RankedResults
                        WHERE rn = 1
                        ORDER BY AcademicYearId, Semester, CourseCode",
                        student.StudentId_Number,
                        "7")
                    .AsNoTracking()
                    .ToListAsync();

                if (!allResults.Any())
                {
                    return NotFound("No results found.");
                }

                // Generate PDF
                var pdfBytes = GenerateFullTranscriptPDF(student, allResults);

                return File(pdfBytes, "application/pdf", $"Full_Transcript_{student.StudentId_Number}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating full transcript for student {StudentId}", studentId);
                return StatusCode(500, "An error occurred while generating the transcript.");
            }
        }

        // PDF Generation Helper Methods
        private async Task<byte[]> GenerateSemesterTranscriptPDF(Student student, AcademicYear academicYear, int semester, List<StudentResultView> results)
        {
            using (var memoryStream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);

                document.Open();

                // Add header
                AddTranscriptHeader(document, "SEMESTER TRANSCRIPT");
                AddStudentInfo(document, student);
                AddSemesterInfo(document, academicYear, semester);

                // Calculate semester stats
                var courses = results.GroupBy(r => r.CourseCode).Select(g => g.First()).ToList();
                decimal semesterGPA = CalculateSemesterGPA(courses);
                int creditsAttempted = courses.Sum(c => GetCourseCredits(c.CourseCode));
                int creditsEarned = courses.Where(c => c.IsPassingGrade == 1).Sum(c => GetCourseCredits(c.CourseCode));
                int failedCourses = courses.Count(c => c.IsPassingGrade == 0);

                // Add results table
                AddResultsTable(document, courses, "Semester Results");

                // Add semester summary
                AddSemesterSummary(document, semesterGPA, creditsAttempted, creditsEarned, failedCourses);

                // Add semester comment
                var semesterComment = await DetermineSemesterComment(student, semester, failedCourses, courses.Count, courses.Max(c => c.Attempt));
                AddComment(document, semesterComment);

                document.Close();
                return memoryStream.ToArray();
            }
        }

        private byte[] GenerateYearTranscriptPDF(Student student, AcademicYear academicYear, List<StudentResultView> results)
        {
            using (var memoryStream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);

                document.Open();

                // Add header
                AddTranscriptHeader(document, "ACADEMIC YEAR TRANSCRIPT");
                AddStudentInfo(document, student);
                AddYearInfo(document, academicYear);

                // Group by semester
                var semesterGroups = results.GroupBy(r => r.Semester).OrderBy(g => g.Key);

                decimal yearGPA = 0;
                int totalCreditsAttempted = 0;
                int totalCreditsEarned = 0;
                int totalFailed = 0;

                foreach (var semesterGroup in semesterGroups)
                {
                    var semesterCourses = semesterGroup.GroupBy(r => r.CourseCode).Select(g => g.First()).ToList();

                    // Add semester heading
                    Paragraph semesterTitle = new Paragraph($"Semester {semesterGroup.Key}", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12));
                    semesterTitle.SpacingBefore = 10;
                    document.Add(semesterTitle);

                    // Add results table
                    AddResultsTable(document, semesterCourses, $"Semester {semesterGroup.Key} Results");

                    // Calculate semester stats
                    decimal semesterGPA = CalculateSemesterGPA(semesterCourses);
                    int creditsAttempted = semesterCourses.Sum(c => GetCourseCredits(c.CourseCode));
                    int creditsEarned = semesterCourses.Where(c => c.IsPassingGrade == 1).Sum(c => GetCourseCredits(c.CourseCode));
                    int failedCourses = semesterCourses.Count(c => c.IsPassingGrade == 0);

                    // Add semester summary
                    AddSemesterSummary(document, semesterGPA, creditsAttempted, creditsEarned, failedCourses);

                    // Accumulate year totals
                    totalCreditsAttempted += creditsAttempted;
                    totalCreditsEarned += creditsEarned;
                    totalFailed += failedCourses;
                }

                // Calculate year GPA
                yearGPA = totalCreditsAttempted > 0 ? CalculateYearGPA(results) : 0;

                // Add year summary
                AddYearSummary(document, yearGPA, totalCreditsAttempted, totalCreditsEarned, totalFailed);

                document.Close();
                return memoryStream.ToArray();
            }
        }

        private byte[] GenerateFullTranscriptPDF(Student student, List<StudentResultView> results)
        {
            using (var memoryStream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);

                document.Open();

                // Add header
                AddTranscriptHeader(document, "OFFICIAL ACADEMIC TRANSCRIPT");
                AddStudentInfo(document, student);

                // Group by academic year
                var yearGroups = results.GroupBy(r => r.AcademicYearId).OrderBy(g => g.Key);

                decimal overallGPA = 0;
                int totalCreditsAttempted = 0;
                int totalCreditsEarned = 0;

                foreach (var yearGroup in yearGroups)
                {
                    var academicYear = _context.AcademicYears.Find(yearGroup.Key);

                    // Add year heading
                    Paragraph yearTitle = new Paragraph($"Academic Year: {academicYear?.YearValue}", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14));
                    yearTitle.SpacingBefore = 15;
                    yearTitle.SpacingAfter = 5;
                    document.Add(yearTitle);

                    // Group by semester within year
                    var semesterGroups = yearGroup.GroupBy(r => r.Semester).OrderBy(g => g.Key);

                    foreach (var semesterGroup in semesterGroups)
                    {
                        var semesterCourses = semesterGroup.GroupBy(r => r.CourseCode).Select(g => g.First()).ToList();

                        // Add semester heading
                        Paragraph semesterTitle = new Paragraph($"Semester {semesterGroup.Key}", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12));
                        semesterTitle.SpacingBefore = 10;
                        document.Add(semesterTitle);

                        // Add results table
                        AddResultsTable(document, semesterCourses, null);

                        // Calculate semester stats
                        int creditsAttempted = semesterCourses.Sum(c => GetCourseCredits(c.CourseCode));
                        int creditsEarned = semesterCourses.Where(c => c.IsPassingGrade == 1).Sum(c => GetCourseCredits(c.CourseCode));

                        totalCreditsAttempted += creditsAttempted;
                        totalCreditsEarned += creditsEarned;
                    }
                }

                // Calculate overall GPA
                overallGPA = totalCreditsAttempted > 0 ? CalculateOverallGPA(results) : 0;

                // Add overall summary
                AddOverallSummary(document, overallGPA, totalCreditsAttempted, totalCreditsEarned);

                document.Close();
                return memoryStream.ToArray();
            }
        }

        // Helper methods for PDF generation
        private void AddTranscriptHeader(Document document, string title)
        {
            iTextSharp.text.Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
            Paragraph header = new Paragraph("EDEN UNIVERSITY", titleFont);
            header.Alignment = Element.ALIGN_CENTER;
            document.Add(header);

            Paragraph subtitle = new Paragraph(title, FontFactory.GetFont(FontFactory.HELVETICA, 12));
            subtitle.Alignment = Element.ALIGN_CENTER;
            subtitle.SpacingAfter = 20;
            document.Add(subtitle);
        }

        private void AddStudentInfo(Document document, Student student)
        {
            PdfPTable infoTable = new PdfPTable(2);
            infoTable.WidthPercentage = 100;
            infoTable.SpacingAfter = 15;

            AddInfoRow(infoTable, "Student ID:", student.StudentId_Number);
            AddInfoRow(infoTable, "Name:", student.FullName);
            AddInfoRow(infoTable, "Programme:", student.Programme.Name);
            AddInfoRow(infoTable, "Department:", student.Programme.Department.Name);

            document.Add(infoTable);
        }

        private void AddSemesterInfo(Document document, AcademicYear year, int semester)
        {
            Paragraph info = new Paragraph($"Academic Year: {year.YearValue} | Semester: {semester}", FontFactory.GetFont(FontFactory.HELVETICA, 10));
            info.SpacingAfter = 10;
            document.Add(info);
        }

        private void AddYearInfo(Document document, AcademicYear year)
        {
            Paragraph info = new Paragraph($"Academic Year: {year.YearValue}", FontFactory.GetFont(FontFactory.HELVETICA, 10));
            info.SpacingAfter = 10;
            document.Add(info);
        }

        private void AddResultsTable(Document document, List<StudentResultView> courses, string tableTitle)
        {
            if (!string.IsNullOrEmpty(tableTitle))
            {
                Paragraph title = new Paragraph(tableTitle, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11));
                title.SpacingBefore = 10;
                title.SpacingAfter = 5;
                document.Add(title);
            }

            PdfPTable table = new PdfPTable(5);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 2f, 4f, 1.5f, 1.5f, 2f });
            table.SpacingAfter = 10;

            // Header
            iTextSharp.text.Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            AddTableHeader(table, "Code", headerFont);
            AddTableHeader(table, "Course Name", headerFont);
            AddTableHeader(table, "Credits", headerFont);
            AddTableHeader(table, "Grade", headerFont);
            AddTableHeader(table, "Comment", headerFont);

            // Rows
            iTextSharp.text.Font cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            foreach (var course in courses)
            {
                AddTableCell(table, course.CourseCode, cellFont);
                AddTableCell(table, course.CourseName, cellFont);
                AddTableCell(table, GetCourseCredits(course.CourseCode).ToString(), cellFont);
                AddTableCell(table, course.GradeLetter ?? "-", cellFont);
                AddTableCell(table, course.Description ?? "-", cellFont);
            }

            document.Add(table);
        }

        private void AddSemesterSummary(Document document, decimal gpa, int attempted, int earned, int failed)
        {
            PdfPTable summaryTable = new PdfPTable(4);
            summaryTable.WidthPercentage = 100;
            summaryTable.SpacingAfter = 10;

            iTextSharp.text.Font summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            AddInfoRow(summaryTable, "Semester GPA:", gpa.ToString("F2"));
            AddInfoRow(summaryTable, "Credits Attempted:", attempted.ToString());
            AddInfoRow(summaryTable, "Credits Earned:", earned.ToString());
            AddInfoRow(summaryTable, "Failed Courses:", failed.ToString());

            document.Add(summaryTable);
        }

        private void AddYearSummary(Document document, decimal gpa, int attempted, int earned, int failed)
        {
            Paragraph summaryTitle = new Paragraph("Year Summary", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11));
            summaryTitle.SpacingBefore = 15;
            summaryTitle.SpacingAfter = 5;
            document.Add(summaryTitle);

            PdfPTable summaryTable = new PdfPTable(2);
            summaryTable.WidthPercentage = 100;
            summaryTable.SpacingAfter = 10;

            AddInfoRow(summaryTable, "Year GPA:", gpa.ToString("F2"));
            AddInfoRow(summaryTable, "Total Credits Attempted:", attempted.ToString());
            AddInfoRow(summaryTable, "Total Credits Earned:", earned.ToString());
            AddInfoRow(summaryTable, "Total Failed Courses:", failed.ToString());

            document.Add(summaryTable);
        }

        private void AddOverallSummary(Document document, decimal gpa, int attempted, int earned)
        {
            Paragraph summaryTitle = new Paragraph("Overall Summary", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12));
            summaryTitle.SpacingBefore = 20;
            summaryTitle.SpacingAfter = 10;
            document.Add(summaryTitle);

            PdfPTable summaryTable = new PdfPTable(2);
            summaryTable.WidthPercentage = 60;
            summaryTable.HorizontalAlignment = Element.ALIGN_CENTER;

            iTextSharp.text.Font boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            AddInfoRow(summaryTable, "Overall GPA:", gpa.ToString("F2"));
            AddInfoRow(summaryTable, "Total Credits Attempted:", attempted.ToString());
            AddInfoRow(summaryTable, "Total Credits Earned:", earned.ToString());

            document.Add(summaryTable);
        }

        private void AddComment(Document document, string comment)
        {
            Paragraph commentPara = new Paragraph($"Comment: {comment}", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10));
            commentPara.SpacingBefore = 10;
            document.Add(commentPara);
        }

        private void AddInfoRow(PdfPTable table, string label, string value)
        {
            iTextSharp.text.Font labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9);
            iTextSharp.text.Font valueFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            PdfPCell labelCell = new PdfPCell(new Phrase(label, labelFont));
            labelCell.Border = Rectangle.NO_BORDER;
            labelCell.PaddingBottom = 5;
            table.AddCell(labelCell);

            PdfPCell valueCell = new PdfPCell(new Phrase(value, valueFont));
            valueCell.Border = Rectangle.NO_BORDER;
            valueCell.PaddingBottom = 5;
            table.AddCell(valueCell);
        }

        private void AddTableHeader(PdfPTable table, string text, iTextSharp.text.Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = BaseColor.LIGHT_GRAY;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private void AddTableCell(PdfPTable table, string text, iTextSharp.text.Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private decimal CalculateSemesterGPA(List<StudentResultView> courses)
        {
            decimal totalPoints = 0;
            int totalCredits = 0;

            foreach (var course in courses.Where(c => c.GradeLetter != "NE"))
            {
                int credits = GetCourseCredits(course.CourseCode);
                totalPoints += (course.GPAValue ?? 0) * credits;
                totalCredits += credits;
            }

            return totalCredits > 0 ? totalPoints / totalCredits : 0;
        }

        private decimal CalculateYearGPA(List<StudentResultView> results)
        {
            var courses = results.GroupBy(r => r.CourseCode).Select(g => g.First()).ToList();
            return CalculateSemesterGPA(courses);
        }

        private decimal CalculateOverallGPA(List<StudentResultView> results)
        {
            var courses = results.GroupBy(r => r.CourseCode).Select(g => g.First()).ToList();
            return CalculateSemesterGPA(courses);
        }

        private int GetCourseCredits(string courseCode)
        {
            var course = _context.Courses.FirstOrDefault(c => c.CourseCode == courseCode);
            return course?.Credits ?? 0;
        }

        private async Task<string> DetermineSemesterComment(Student student, int semester, int failed, int total, int? attempt)
        {
            if (total == 0) return "No courses";

            int failedPercentage = (int)Math.Floor(((double)failed / total) * 100);

            var progressionRule = await _progressionService.GetApplicableProgressionRuleAsync(
                                        student,
                                        failedPercentage,
                                        semester,  // Use current semester being processed
                                        attempt  // Pass highest attempt in semester
                                    );

            // Add your progression logic here based on failed percentage and attempt
            if (failedPercentage == 0) return "Proceed";
            if (failedPercentage <= 30) return "ProceedWithRepeat";
            if (failedPercentage <= 50) return attempt >= 2 ? "RepeatSemester" : "ProceedWithRepeat";
            return progressionRule.Action;
        }

}
}