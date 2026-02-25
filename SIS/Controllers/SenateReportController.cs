using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Accounts;
using SIS.Models.Admin;
using SIS.Models.Reports;
using SIS.Services.Progression;
using SIS.Services.Reports;
using System.Security.Claims;
using System.Text.Json;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin,Registrar,Dean,ProgramCoordinator")]
    public class SenateReportController : Controller
    {
        private readonly ISenateReportService _senateReportService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SenateReportController> _logger;
        private readonly IStudentProgressionService _studentProgressionService;

        public SenateReportController(
            ISenateReportService senateReportService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<SenateReportController> logger,
            IStudentProgressionService studentProgressionService)
        {
            _senateReportService = senateReportService;
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _studentProgressionService = studentProgressionService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Get current user for role-based filtering
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Unauthorized();
                }

                // Check user roles
                bool isAdmin = User.IsInRole("Admin");
                bool isDean = User.IsInRole("Dean");
                bool isRegistrar = User.IsInRole("Registrar");
                bool isProgramCoordinator = User.IsInRole("ProgramCoordinator");

                // Get filter options
                var filterOptions = await _senateReportService.GetFilterOptionsAsync();

                // Apply role-based filtering to schools if needed
                if (!isAdmin && !isRegistrar)
                {
                    if (isDean)
                    {
                        // Filter schools where user is Dean
                        var userSchools = await _context.Schools
                            .Where(s => s.DeanId == currentUser.Id || s.AssistantDeanId == currentUser.Id)
                            .ToListAsync();
                        filterOptions.Schools = userSchools;
                    }
                    else if (isProgramCoordinator)
                    {
                        // Filter based on programmes where user is coordinator
                        var userProgrammes = await _context.Programmes
                            .Include(p => p.Department)
                                .ThenInclude(d => d.School)
                            .Where(p => p.CoordinatorId == currentUser.Id)
                            .ToListAsync();

                        filterOptions.Schools = userProgrammes
                            .Select(p => p.Department.School)
                            .DistinctBy(s => s.Id)
                            .ToList();
                    }
                }

                // Set ViewBag data for the view
                ViewBag.IsAdmin = isAdmin;
                ViewBag.IsDean = isDean;
                ViewBag.IsRegistrar = isRegistrar;
                ViewBag.IsProgramCoordinator = isProgramCoordinator;
                ViewBag.CurrentUser = currentUser.UserName;

                return View(filterOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Senate Report index page");
                TempData["Error"] = "Unable to load the Senate Report page. Please try again.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReport([FromBody] SenateReportFilters filters)
        {
            try
            {
                if (filters == null)
                {
                    return BadRequest(new { success = false, message = "Invalid filter parameters" });
                }

                _logger.LogInformation($"Generating senate report with filters: {JsonSerializer.Serialize(filters)}");

                // Apply role-based restrictions
                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                // Generate the report
                var report = await _senateReportService.GenerateReportAsync(filters);

                // Get performance summary if programme-level report
                PerformanceSummaryDto performanceSummary = null;
                if (filters.ProgrammeId.HasValue &&
                    filters.AcademicYearId.HasValue &&
                    filters.Semester.HasValue)
                {
                    try
                    {
                        performanceSummary = await _senateReportService.GetPerformanceSummaryAsync(
                            filters.ProgrammeId.Value,
                            filters.AcademicYearId.Value,
                            filters.Semester.Value,
                            filters.YearOfStudy.Value
                        );

                        _logger.LogInformation($"Performance summary generated: {performanceSummary.TotalStudents} students");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate performance summary");
                        // Continue without performance summary
                    }
                }

                return Json(new
                {
                    success = true,
                    data = report,
                    performanceSummary = performanceSummary,
                    message = $"Report generated successfully with {report.Summaries.Count} entities"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating senate report");
                return Json(new
                {
                    success = false,
                    message = "Failed to generate report. Please try again.",
                    error = ex.Message
                });
            }
        }

        // NEW ACTION: Get Performance Summary separately (if needed)
        [HttpGet]
        public async Task<IActionResult> GetPerformanceSummary(
            int programmeId,
            int academicYearId,
            int semester)
        {
            try
            {
                if (programmeId <= 0 || academicYearId <= 0 || semester <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid parameters" });
                }

                // Check access
                if (!await CanAccessProgrammeAsync(programmeId))
                {
                    return Json(new { success = false, message = "Access denied to this programme." });
                }

                var summary = await _senateReportService.GetPerformanceSummaryAsync(
                    programmeId,
                    academicYearId,
                    semester,
                    1
                );

                return Json(new
                {
                    success = true,
                    data = summary,
                    message = "Performance summary retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance summary");
                return Json(new
                {
                    success = false,
                    message = "Failed to get performance summary.",
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DrillDown(string level, int entityId, [FromQuery] SenateReportFilters filters)
        {
            try
            {
                if (string.IsNullOrEmpty(level) || entityId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid drill-down parameters" });
                }

                // Create new filters for drill-down
                var drillFilters = CreateDrillDownFilters(filters, level, entityId);

                // Apply role-based restrictions
                var currentUser = await _userManager.GetUserAsync(User);
                drillFilters = await ApplyRoleBasedFilters(drillFilters, currentUser);

                // Generate drill-down report
                var report = await _senateReportService.GenerateReportAsync(drillFilters);

                return Json(new
                {
                    success = true,
                    data = report,
                    level = drillFilters.ReportLevel,
                    message = $"Drill-down report generated for {level}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in drill-down report generation");
                return Json(new
                {
                    success = false,
                    message = "Failed to generate drill-down report.",
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartmentsBySchool(int schoolId)
        {
            try
            {
                if (schoolId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid school ID" });
                }

                var departments = await _senateReportService.GetDepartmentsBySchoolAsync(schoolId);

                return Json(new
                {
                    success = true,
                    departments = departments.Select(d => new { d.Id, d.Name })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting departments for school {schoolId}");
                return Json(new
                {
                    success = false,
                    message = "Failed to load departments."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammesByDepartment(int departmentId)
        {
            try
            {
                if (departmentId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid department ID" });
                }

                var programmes = await _senateReportService.GetProgrammesByDepartmentAsync(departmentId);

                return Json(new
                {
                    success = true,
                    programmes = programmes.Select(p => new { p.Id, p.Name })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting programmes for department {departmentId}");
                return Json(new
                {
                    success = false,
                    message = "Failed to load programmes."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportToPdf([FromBody] SenateReportFilters filters)
        {
            try
            {
                // Apply role-based restrictions
                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                // Generate report data
                var report = await _senateReportService.GenerateReportAsync(filters);

                // For now, return JSON response - we'll implement PDF generation later
                return Json(new
                {
                    success = true,
                    message = "PDF export functionality will be implemented in the next phase",
                    data = new
                    {
                        reportLevel = report.ReportLevel,
                        totalEntities = report.Summaries.Count,
                        totalStudents = report.Totals.TotalStudents,
                        generatedAt = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PDF export");
                return Json(new
                {
                    success = false,
                    message = "Failed to generate PDF export."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportToExcel([FromBody] SenateReportFilters filters)
        {
            try
            {
                // Apply role-based restrictions
                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                // Generate report data
                var report = await _senateReportService.GenerateReportAsync(filters);

                // For now, return JSON response - we'll implement Excel generation later
                return Json(new
                {
                    success = true,
                    message = "Excel export functionality will be implemented in the next phase",
                    data = new
                    {
                        reportLevel = report.ReportLevel,
                        totalEntities = report.Summaries.Count,
                        totalStudents = report.Totals.TotalStudents,
                        generatedAt = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Excel export");
                return Json(new
                {
                    success = false,
                    message = "Failed to generate Excel export."
                });
            }
        }

        // Helper method to apply role-based filters
        private async Task<SenateReportFilters> ApplyRoleBasedFilters(SenateReportFilters filters, ApplicationUser currentUser)
        {
            bool isAdmin = User.IsInRole("Admin");
            bool isRegistrar = User.IsInRole("Registrar");
            bool isDean = User.IsInRole("Dean");
            bool isProgramCoordinator = User.IsInRole("ProgramCoordinator");

            // Admin and Registrar can see everything
            if (isAdmin || isRegistrar)
            {
                return filters;
            }

            // Apply restrictions for other roles
            if (isDean)
            {
                // Restrict to schools where user is Dean
                var userSchools = await _context.Schools
                    .Where(s => s.DeanId == currentUser.Id || s.AssistantDeanId == currentUser.Id)
                    .Select(s => s.Id)
                    .ToListAsync();

                if (userSchools.Any() && (!filters.SchoolId.HasValue || !userSchools.Contains(filters.SchoolId.Value)))
                {
                    filters.SchoolId = userSchools.First(); // Default to first school if not specified or invalid
                }
            }
            else if (isProgramCoordinator)
            {
                // Restrict to programmes where user is coordinator
                var userProgrammes = await _context.Programmes
                    .Where(p => p.CoordinatorId == currentUser.Id)
                    .ToListAsync();

                if (userProgrammes.Any())
                {
                    if (!filters.ProgrammeId.HasValue || !userProgrammes.Any(p => p.Id == filters.ProgrammeId.Value))
                    {
                        var firstProgramme = userProgrammes.First();
                        filters.ProgrammeId = firstProgramme.Id;
                        filters.DepartmentId = firstProgramme.DepartmentId;
                        filters.SchoolId = firstProgramme.Department?.SchoolId;
                    }
                }
            }

            return filters;
        }

        [HttpGet]
        public async Task<IActionResult> GetEntityStudentDetails(int entityId, string entityType, [FromQuery] SenateReportFilters filters)
        {
            try
            {
                if (entityId <= 0 || string.IsNullOrEmpty(entityType))
                {
                    return BadRequest(new { success = false, message = "Invalid parameters" });
                }

                _logger.LogInformation($"Getting student details for {entityType} {entityId}");

                // Apply role-based restrictions
                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                // Get student details
                var students = await _senateReportService.GetEntityStudentDetailsAsync(entityId, entityType, filters);

                // Get entity name
                string entityName = await GetEntityNameAsync(entityId, entityType);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        entityId = entityId,
                        entityType = entityType,
                        entityName = entityName,
                        students = students,
                        totalStudents = students.Count,
                        studentsWithResults = students.Count(s => s.HasPublishedResults),
                        studentsWithoutResults = students.Count(s => !s.HasPublishedResults),
                        averageGPA = students.Where(s => s.HasPublishedResults).Any()
                            ? students.Where(s => s.HasPublishedResults).Average(s => s.GPA)
                            : 0
                    },
                    message = $"Retrieved {students.Count} students"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting entity student details for {entityType} {entityId}");
                return Json(new
                {
                    success = false,
                    message = "Failed to load student details.",
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStudentProgressionDetail(int studentId, [FromQuery] SenateReportFilters filters)
        {
            try
            {
                if (studentId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid student ID" });
                }

                _logger.LogInformation($"Getting progression detail for student {studentId}");

                // Apply role-based restrictions
                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                // Get student progression detail
                var detail = await _senateReportService.GetStudentProgressionDetailAsync(studentId, filters);

                return Json(new
                {
                    success = true,
                    data = detail,
                    message = "Student progression detail retrieved successfully"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, $"No data found for student {studentId}");
                return Json(new
                {
                    success = false,
                    message = "No data found for this student with the selected filters."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting student progression detail for student {studentId}");
                return Json(new
                {
                    success = false,
                    message = "Failed to load student progression detail.",
                    error = ex.Message
                });
            }
        }

        // Helper method to get entity name
        private async Task<string> GetEntityNameAsync(int entityId, string entityType)
        {
            switch (entityType.ToLower())
            {
                case "school":
                    var school = await _context.Schools.FindAsync(entityId);
                    return school?.Name ?? "Unknown School";
                case "department":
                    var department = await _context.Departments.Include(d => d.School).FirstOrDefaultAsync(d => d.Id == entityId);
                    return department != null ? $"{department.Name} ({department.School?.Name})" : "Unknown Department";
                case "programme":
                    var programme = await _context.Programmes.Include(p => p.Department).FirstOrDefaultAsync(p => p.Id == entityId);
                    return programme != null ? $"{programme.Name} ({programme.Department?.Name})" : "Unknown Programme";
                default:
                    return "Unknown";
            }
        }

        // Helper method to create drill-down filters
        private SenateReportFilters CreateDrillDownFilters(SenateReportFilters baseFilters, string targetLevel, int entityId)
        {
            var drillFilters = new SenateReportFilters
            {
                AcademicYearId = baseFilters.AcademicYearId,
                ModeOfStudyId = baseFilters.ModeOfStudyId,
                YearOfStudy = baseFilters.YearOfStudy,
                Semester = baseFilters.Semester,
                Period = baseFilters.Period
            };

            switch (targetLevel.ToLower())
            {
                case "department":
                    drillFilters.ReportLevel = "Department";
                    drillFilters.SchoolId = entityId;
                    break;
                case "programme":
                    drillFilters.ReportLevel = "Programme";
                    drillFilters.DepartmentId = entityId;
                    drillFilters.SchoolId = baseFilters.SchoolId;
                    break;
                default:
                    drillFilters.ReportLevel = "School";
                    break;
            }

            return drillFilters;
        }

        // ===================================================================
        // BATCH PUBLISHING ACTIONS
        // ===================================================================

        /// <summary>
        /// Get pending result batches for a programme
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProgrammeBatches(
            int programmeId,
            int academicYearId,
            int semester)
        {
            try
            {
                _logger.LogInformation($"Getting batches for programme {programmeId}");

                // Apply role-based filtering
                var hasAccess = await CanAccessProgrammeAsync(programmeId);
                if (!hasAccess)
                {
                    //return Json(new { success = false, message = "Access denied to this programme." });
                }

                var batches = await _senateReportService.GetPendingBatchesForProgrammeAsync(
                    programmeId,
                    academicYearId,
                    semester);

                return Json(new
                {
                    success = true,
                    data = batches,
                    totalCount = batches.Count,
                    pendingCount = batches.Count(b => b.CanPublish),
                    approvedCount = batches.Count(b => b.ApprovalStatus == WorkflowStatus.Published)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting programme batches");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Publish selected batches
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PublishBatches([FromBody] PublishBatchesRequest request)
        {
            try
            {
                if (request?.BatchIds == null || !request.BatchIds.Any())
                {
                    return Json(new { success = false, message = "No batches selected." });
                }

                _logger.LogInformation($"Publishing {request.BatchIds.Count} batches");

                // Get current user ID
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated." });
                }

                // Verify user has permission to publish these batches
                var hasPermission = await CanPublishBatchesAsync(request.BatchIds);
                if (!hasPermission)
                {
                    //return Json(new { success = false, message = "Access denied to publish these batches." });
                }

                var result = await _senateReportService.PublishBatchesAsync(request.BatchIds, userId);

                return Json(new
                {
                    success = result.Success,
                    message = result.Message,
                    publishedCount = result.PublishedCount,
                    failedCount = result.FailedCount,
                    errors = result.Errors,
                    publishedBatchIds = result.PublishedBatchIds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing batches");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Publish all pending batches for a programme
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PublishAllProgrammeBatches([FromBody] PublishAllBatchesRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, message = "Invalid request." });
                }

                _logger.LogInformation($"Publishing all batches for programme {request.ProgrammeId}");

                // Get current user ID
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated." });
                }

                // Verify user has permission to access this programme
                var hasAccess = await CanAccessProgrammeAsync(request.ProgrammeId);
                if (!hasAccess)
                {
                    //return Json(new { success = false, message = "Access denied to this programme." });
                }

                var result = await _senateReportService.PublishAllProgrammeBatchesAsync(
                    request.ProgrammeId,
                    request.AcademicYearId,
                    request.Semester,
                    userId);

                return Json(new
                {
                    success = result.Success,
                    message = result.Message,
                    publishedCount = result.PublishedCount,
                    failedCount = result.FailedCount,
                    errors = result.Errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing all programme batches");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammeGradingOverview(
            int programmeId,
            int academicYearId,
            int semester,
            int yearOfStudy)
        {
            try
            {
                if (yearOfStudy == 0)
                {
                    yearOfStudy = 1;
                }
                // Validate user access
                if (!await CanAccessProgrammeAsync(programmeId))
                {
                    //return Json(new { success = false, message = "You do not have permission to view this programme" });
                }

                var overview = await _senateReportService.GetProgrammeGradingOverviewAsync(
                    programmeId,
                    academicYearId,
                    semester,
                    yearOfStudy);

                return Json(new
                {
                    success = true,
                    data = overview
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting grading overview for programme {programmeId}");
                return Json(new
                {
                    success = false,
                    message = "An error occurred while loading the grading overview"
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCourseGradingOverview(
            int rsbId,
            int academicYearId,
            int semester)
        {
            int courseId = 0;
            try
            {
                var rsb = await _context.ResultSubmissionBatches.FindAsync(rsbId);
                var course = await _context.Courses.FindAsync(rsb.CourseId);
                if (course == null)
                {
                    return Json(new { success = false, message = "Course not found" });
                }

                courseId = rsb.CourseId;

                var academicYear = await _context.AcademicYears.FindAsync(academicYearId);
                if (academicYear == null)
                {
                    return Json(new { success = false, message = "Academic year not found" });
                }

                // Get enrolled students
                var enrolledStudents = await _context.StudentExaminableCourses
                    .Where(sec =>
                        sec.CourseId == courseId &&
                        sec.AcademicYearId == academicYearId &&
                        sec.Semester == semester)
                    .Select(sec => sec.StudentId)
                    .Distinct()
                    .ToListAsync();

                if (!enrolledStudents.Any())
                {
                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            courseCode = course.CourseCode,
                            courseName = course.CourseName,
                            academicYear = academicYear.YearValue,
                            semester = semester,
                            gradeDistribution = new Dictionary<string, int>(),
                            totalPassed = 0,
                            totalFailed = 0,
                            passRate = 0
                        }
                    });
                }

                // Initialize grade distribution
                var gradeDistribution = new Dictionary<string, int>
                    {
                        { "A+", 0 }, { "A", 0 }, { "B+", 0 }, { "B", 0 },
                        { "C+", 0 }, { "C", 0 }, { "D+", 0 }, { "D", 0 },
                        { "EXP", 0 }, { "NE/INC", 0 }, { "DEF", 0 },
                        { "P", 0 }, { "F", 0 }
                    };

                int totalPassed = 0;
                int totalFailed = 0;

                var studentTmp = await _context.Students
                                                .Include(s => s.Programme)
                                                    .ThenInclude(p => p.Department)
                                                .FirstOrDefaultAsync(s => s.Id == enrolledStudents.FirstOrDefault());

                //var gradeConfigs = await _context.GradeConfigurations
                //    .Where(g => g.IsActive)
                //    .OrderByDescending(g => g.MinScore)
                //    .ToListAsync();
                // Get grade configurations
                var gradeConfigs = await _studentProgressionService.GetGradeConfigurationAsync(studentTmp.Programme.Department.SchoolId, studentTmp.AcademicYearId);

                double passMark = course.PassMark;

                // Calculate grades for each student
                foreach (var studentId in enrolledStudents)
                {
                    // Check for published batch
                    var hasPublishedBatch = await _context.ResultSubmissionBatches
                        .AnyAsync(rsb =>
                            rsb.CourseId == courseId &&
                            rsb.AcademicYearId == academicYearId &&
                            rsb.Semester == semester &&
                            rsb.ApprovalStatus == WorkflowStatus.Approved);

                    if (!hasPublishedBatch)
                    {
                        gradeDistribution["NE/INC"]++;
                        continue;
                    }

                    // Get assessment scores
                    var assessmentScores = await _context.StudentAssessmentScores
                        .Where(s =>
                            s.StudentId == studentId &&
                            s.CourseId == courseId &&
                            s.AcademicYearId == academicYearId &&
                            s.Semester == semester &&
                            s.IsActive &&
                            _context.ResultSubmissionBatches.Any(rsb =>
                                rsb.Id == s.rsbId &&
                                rsb.ApprovalStatus == WorkflowStatus.Approved))
                        .ToListAsync();

                    if (!assessmentScores.Any())
                    {
                        gradeDistribution["NE/INC"]++;
                        continue;
                    }

                    // Calculate total score
                    decimal totalScore = Math.Min(assessmentScores.Sum(s => s.Score), 100);

                    // Determine grade
                    var gradeConfig = gradeConfigs.FirstOrDefault(g => totalScore >= (decimal)g.MinScore);

                    if (gradeConfig != null)
                    {
                        string gradeLetter = gradeConfig.GradeLetter;
                        if (gradeDistribution.ContainsKey(gradeLetter))
                        {
                            gradeDistribution[gradeLetter]++;
                        }
                        else
                        {
                            gradeDistribution["F"]++;
                        }

                        if (totalScore >= (decimal)passMark)
                        {
                            totalPassed++;
                        }
                        else
                        {
                            totalFailed++;
                        }
                    }
                    else
                    {
                        gradeDistribution["F"]++;
                        totalFailed++;
                    }
                }

                int totalStudents = totalPassed + totalFailed;
                decimal passRate = totalStudents > 0 ? Math.Round((decimal)totalPassed / totalStudents * 100, 0) : 0;

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        courseCode = course.CourseCode,
                        courseName = course.CourseName,
                        academicYear = academicYear.YearValue,
                        semester = semester,
                        gradeDistribution = gradeDistribution,
                        totalPassed = totalPassed,
                        totalFailed = totalFailed,
                        passRate = passRate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting course grading overview for course {courseId}");
                return Json(new
                {
                    success = false,
                    message = "An error occurred while loading the grading overview"
                });
            }
        }

        // ===================================================================
        // HELPER METHODS FOR AUTHORIZATION
        // ===================================================================

        private async Task<bool> CanAccessProgrammeAsync(int programmeId)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Admins and Registrars have access to everything
            if (userRole == "Admin" || userRole == "Registrar")
            {
                return true;
            }

            // Deans can access programmes in their schools
            if (userRole == "Dean")
            {
                var programme = await _context.Programmes
                    .Include(p => p.Department)
                        .ThenInclude(d => d.School)
                    .FirstOrDefaultAsync(p => p.Id == programmeId);

                if (programme != null)
                {
                    var school = programme.Department?.School;
                    if (school != null &&
                        (school.DeanId == userId || school.AssistantDeanId == userId))
                    {
                        return true;
                    }
                }
            }

            // Programme Coordinators can access their own programmes
            if (userRole == "ProgramCoordinator")
            {
                var programme = await _context.Programmes
                    .FirstOrDefaultAsync(p => p.Id == programmeId && p.CoordinatorId == userId);

                return programme != null;
            }

            return false;
        }

        private async Task<bool> CanPublishBatchesAsync(List<int> batchIds)
        {
            // Get all batches
            var batches = await _context.ResultSubmissionBatches
                .Include(b => b.Course)
                    .ThenInclude(pc => pc.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                .Where(b => batchIds.Contains(b.Id))
                .ToListAsync();

            if (!batches.Any())
            {
                return false;
            }

            // Check each batch
            /*foreach (var batch in batches)
            {
                var programmeIds = batch.Course.ProgrammeCourses
                    .Select(pc => pc.ProgrammeId)
                    .Distinct()
                    .ToList();

                // Check if user can access any of these programmes
                var hasAccess = false;
                foreach (var programmeId in programmeIds)
                {
                    if (await CanAccessProgrammeAsync(programmeId))
                    {
                        hasAccess = true;
                        break;
                    }
                }

                if (!hasAccess)
                {
                    return false;
                }
            }*/

            return true;
        }

        // ===================================================================
        // REQUEST MODELS
        // ===================================================================

        public class PublishBatchesRequest
        {
            public List<int> BatchIds { get; set; }
        }

        public class PublishAllBatchesRequest
        {
            public int ProgrammeId { get; set; }
            public int AcademicYearId { get; set; }
            public int Semester { get; set; }
        }
    }
}
