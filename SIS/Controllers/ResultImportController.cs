using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models;
using SIS.Models.Registration;
using SIS.Models.Results;
using SIS.Services.ResultImport;
using System.Text.Json;

namespace SIS.Controllers
{
    [Authorize(Roles = "Lecturer,HOD,Dean")]
    public class ResultImportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IResultImportService _resultImportService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ResultImportController> _logger;

        public ResultImportController(
            ApplicationDbContext context,
            IResultImportService resultImportService,
            UserManager<ApplicationUser> userManager,
            ILogger<ResultImportController> logger)
        {
            _context = context;
            _resultImportService = resultImportService;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: ResultImport
        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Clear any existing session data
                ClearSessionData();

                // Get user's courses
                var courseIds = await GetUserCourseIdsAsync(user);

                if (!courseIds.Any())
                {
                    TempData["Info"] = "No courses assigned to you. Please contact the administrator.";
                    return View(new ResultImportIndexViewModel());
                }

                // Get active academic years
                var academicYears = await _context.AcademicYears
                    .Where(ay => ay.IsActive)
                    .OrderByDescending(ay => ay.YearValue)
                    .ToListAsync();

                // Get courses with basic info
                var courses = await _context.Courses
                    .Include(c => c.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Where(c => courseIds.Contains(c.Id))
                    .OrderBy(c => c.CourseCode)
                    .Select(c => new CourseImportInfo
                    {
                        CourseId = c.Id,
                        CourseCode = c.CourseCode,
                        CourseName = c.CourseName,
                        ProgrammeName = c.Programme.Name,
                        SchoolName = c.Programme.Department.School.Name,
                        Credits = 3 // Default, adjust if you have this in Course model
                    })
                    .ToListAsync();

                var viewModel = new ResultImportIndexViewModel
                {
                    Courses = courses,
                    AcademicYears = academicYears,
                    MaxFileSize = 10 * 1024 * 1024, // 10MB
                    SupportedFormats = new[] { ".xlsx" }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading result import index page");
                TempData["Error"] = "An error occurred while loading the page.";
                return RedirectToAction("Index", "Home");
            }
        }

        public async Task<IActionResult> MultiCourseImport()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Clear any existing session data
                ClearSessionData();

                // Get user's courses
                /*var courseIds = await GetUserCourseIdsAsync(user);

                if (!courseIds.Any())
                {
                    TempData["Info"] = "No courses assigned to you. Please contact the administrator.";
                    return View(new ResultImportIndexViewModel());
                }*/

                // Get active academic years
                var academicYears = await _context.AcademicYears
                    .Where(ay => ay.IsActive)
                    .OrderByDescending(ay => ay.YearValue)
                    .ToListAsync();

                // Get courses with basic info
                /*var courses = await _context.Courses
                    .Include(c => c.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Where(c => courseIds.Contains(c.Id))
                    .OrderBy(c => c.CourseCode)
                    .Select(c => new CourseImportInfo
                    {
                        CourseId = c.Id,
                        CourseCode = c.CourseCode,
                        CourseName = c.CourseName,
                        ProgrammeName = c.Programme.Name,
                        SchoolName = c.Programme.Department.School.Name,
                        Credits = 3 // Default, adjust if you have this in Course model
                    })
                    .ToListAsync();*/

                var viewModel = new ResultImportIndexViewModel
                {
                    Courses = [],
                    AcademicYears = academicYears,
                    MaxFileSize = 10 * 1024 * 1024, // 10MB
                    SupportedFormats = new[] { ".xlsx" }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading result import index page");
                TempData["Error"] = "An error occurred while loading the page.";
                return RedirectToAction("Index", "Home");
            }
        }

        public async Task<IActionResult> SupDefImport()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Clear any existing session data
                ClearSessionData();

                // Get active academic years
                var academicYears = await _context.AcademicYears
                    .Where(ay => ay.IsActive)
                    .OrderByDescending(ay => ay.YearValue)
                    .ToListAsync();

                var viewModel = new ResultImportIndexViewModel
                {
                    Courses = [],
                    AcademicYears = academicYears,
                    MaxFileSize = 10 * 1024 * 1024, // 10MB
                    SupportedFormats = new[] { ".xlsx" }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading result import index page");
                TempData["Error"] = "An error occurred while loading the page.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: ResultImport/ValidateCourse
        [HttpGet]
        public async Task<IActionResult> ValidateCourse(
            int courseId,
            int academicYearId,
            int semester)
        {
            try
            {
                var validation = await _resultImportService.ValidateCourseContextAsync(
                    courseId, academicYearId, semester);

                // Get statistics
                var statistics = await _resultImportService.GetImportStatisticsAsync(
                    courseId, academicYearId, semester);

                return Json(new
                {
                    success = validation.IsValid,
                    validation = new
                    {
                        isValid = validation.IsValid,
                        errors = validation.Errors,
                        warnings = validation.Warnings,
                        summary = validation.ValidationSummary,
                        courseExists = validation.CourseExists,
                        hasAssessments = validation.HasAssessments,
                        hasEnrolledStudents = validation.HasEnrolledStudents,
                        assessmentCount = validation.AssessmentCount,
                        enrolledStudentCount = validation.EnrolledStudentCount,
                        totalWeight = validation.TotalAssessmentWeight,
                        resultsPublished = validation.ResultsAlreadyPublished
                    },
                    statistics = new
                    {
                        totalEnrolled = statistics.TotalEnrolled,
                        studentsWithScores = statistics.StudentsWithScores,
                        studentsWithCompleteScores = statistics.StudentsWithCompleteScores,
                        studentsWithPartialScores = statistics.StudentsWithPartialScores,
                        studentsWithNoScores = statistics.StudentsWithNoScores,
                        completionPercentage = statistics.CompletionPercentage,
                        lastImportDate = statistics.LastImportDate,
                        lastImportedBy = statistics.LastImportedBy
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating course context");
                return Json(new
                {
                    success = false,
                    message = "Error validating course: " + ex.Message
                });
            }
        }

        // GET: ResultImport/DownloadTemplate
        [HttpGet]
        [RequestTimeout(300000)] // 5 minutes
        public async Task<IActionResult> DownloadTemplate(
            int courseId,
            int academicYearId,
            int semester,
            bool includeExisting = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Generating template for CourseId={CourseId}, AcademicYearId={AcademicYearId}, Semester={Semester}",
                    courseId, academicYearId, semester);

                // Verify user has access to this course
                var user = await _userManager.GetUserAsync(User);
                var courseIds = await GetUserCourseIdsAsync(user);

                if (!courseIds.Contains(courseId))
                {
                    TempData["Error"] = "You do not have access to this course.";
                    return RedirectToAction("Index");
                }

                // Get course info for filename
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

                if (course == null)
                {
                    TempData["Error"] = "Course not found.";
                    return RedirectToAction("Index");
                }

                // Generate template
                var templateBytes = await _resultImportService.GenerateImportTemplateAsync(
                    courseId, academicYearId, semester, includeExisting, cancellationToken);

                var fileName = $"Results_Import_{course.CourseCode}_Sem{semester}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                _logger.LogInformation(
                    "Template generated successfully: {FileName}", fileName);

                return File(
                    templateBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Template generation was cancelled");
                TempData["Error"] = "Template generation was cancelled due to timeout.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating import template");
                TempData["Error"] = "An error occurred while generating the template: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: ResultImport/UploadAndPreview
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestTimeout(300000)] // 5 minutes
        public async Task<IActionResult> UploadAndPreview(
            IFormFile importFile,
            int courseId,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Starting upload and preview for CourseId={CourseId}, File={FileName}",
                    courseId, importFile?.FileName);

                // Clear any existing session data
                ClearSessionData();

                // Validate file upload
                if (importFile == null || importFile.Length == 0)
                {
                    TempData["Error"] = "Please select a file to upload.";
                    return RedirectToAction("Index");
                }

                // Verify user has access to this course
                var user = await _userManager.GetUserAsync(User);
                var courseIds = await GetUserCourseIdsAsync(user);

                if (!courseIds.Contains(courseId))
                {
                    TempData["Error"] = "You do not have access to this course.";
                    return RedirectToAction("Index");
                }

                // Preview the import
                var previewResult = await _resultImportService.PreviewImportDataAsync(
                    importFile, courseId, academicYearId, semester, cancellationToken);

                if (!previewResult.Success)
                {
                    TempData["Error"] = previewResult.Message;
                    return RedirectToAction("Index");
                }

                // Generate unique key for this preview session
                var previewKey = $"ResultPreview_{Guid.NewGuid():N}_{DateTime.Now.Ticks}";

                // Store preview data with expiration time
                var sessionData = new
                {
                    PreviewResult = previewResult,
                    CourseId = courseId,
                    AcademicYearId = academicYearId,
                    Semester = semester,
                    PreviewKey = previewKey, // ← ADD THIS
                    ExpiresAt = DateTime.Now.AddMinutes(30),
                    CreatedAt = DateTime.Now
                };

                try
                {
                    var serializedData = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });

                    HttpContext.Session.SetString($"ResultPreviewData_{previewKey}", serializedData);

                    // Store the preview key in session as well (backup)
                    HttpContext.Session.SetString("CurrentPreviewKey", previewKey);

                    _logger.LogInformation(
                        "Preview data stored with key: {PreviewKey}, Valid: {ValidCount}, Invalid: {InvalidCount}",
                        previewKey, previewResult.ValidResults.Count, previewResult.InvalidResults.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store preview data in session");
                    TempData["Error"] = "Failed to store preview data. Please try with a smaller file.";
                    return RedirectToAction("Index");
                }
                // Pass the preview key to the view via ViewBag
                ViewBag.PreviewKey = previewKey;
                return View("Preview", previewResult);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("File upload and preview was cancelled");
                TempData["Error"] = "The operation was cancelled due to timeout.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file upload and preview");
                TempData["Error"] = "An error occurred while processing the file: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestTimeout(300000)] // 5 minutes
        public async Task<IActionResult> MultiUploadAndPreview(
            IFormFile importFile,
            string importType,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Starting upload and preview for ImportType={ImportType}, File={FileName}",
                    importType, importFile?.FileName);

                // Clear any existing session data
                ClearSessionData();

                // Validate file upload
                if (importFile == null || importFile.Length == 0)
                {
                    TempData["Error"] = "Please select a file to upload.";
                    return RedirectToAction("MultiCourseImport");
                }

                // Verify user has access to this course
                var user = await _userManager.GetUserAsync(User);
                var courseIds = await GetUserCourseIdsAsync(user);

                // Preview the import
                var previewResult = await _resultImportService.PreviewMultiCourseImportDataAsync(
                    importFile, importType, academicYearId, semester, cancellationToken);

                if (!previewResult.Success)
                {
                    TempData["Error"] = previewResult.Message;
                    return RedirectToAction("MultiCourseImport");
                }

                // Generate unique key for this preview session
                var previewKey = $"ResultPreview_{Guid.NewGuid():N}_{DateTime.Now.Ticks}";

                // Store preview data with expiration time
                var sessionData = new
                {
                    PreviewResult = previewResult,
                    ImportType = importType,
                    AcademicYearId = academicYearId,
                    Semester = semester,
                    PreviewKey = previewKey,
                    ExpiresAt = DateTime.Now.AddMinutes(30),
                    CreatedAt = DateTime.Now
                };

                try
                {
                    var serializedData = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });

                    HttpContext.Session.SetString($"ResultPreviewData_{previewKey}", serializedData);

                    // Store the preview key in session as well (backup)
                    HttpContext.Session.SetString("CurrentPreviewKey", previewKey);

                    _logger.LogInformation(
                        "Preview data stored with key: {PreviewKey}, Valid: {ValidCount}, Invalid: {InvalidCount}",
                        previewKey, previewResult.ValidResults.Count, previewResult.InvalidResults.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store preview data in session");
                    TempData["Error"] = "Failed to store preview data. Please try with a smaller file.";
                    return RedirectToAction("MultiCourseImport");
                }
                // Pass the preview key to the view via ViewBag
                ViewBag.PreviewKey = previewKey;
                return View("MultiCourseImportPreview", previewResult);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("File upload and preview was cancelled");
                TempData["Error"] = "The operation was cancelled due to timeout.";
                return RedirectToAction("MultiCourseImport");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file upload and preview");
                TempData["Error"] = "An error occurred while processing the file: " + ex.Message;
                return RedirectToAction("MultiCourseImport");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestTimeout(300000)] // 5 minutes
        public async Task<IActionResult> SupDefUploadAndPreview(
            IFormFile importFile,
            string importType,
            int academicYearId,
            int semester,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Starting upload and preview for ImportType={ImportType}, File={FileName}",
                    importType, importFile?.FileName);

                // Clear any existing session data
                ClearSessionData();

                // Validate file upload
                if (importFile == null || importFile.Length == 0)
                {
                    TempData["Error"] = "Please select a file to upload.";
                    return RedirectToAction("SupDefImport");
                }

                // Verify user has access to this course
                var user = await _userManager.GetUserAsync(User);
                var courseIds = await GetUserCourseIdsAsync(user);

                // Preview the import
                var previewResult = await _resultImportService.PreviewSupDefImportDataAsync(
                    importFile, importType, academicYearId, semester, cancellationToken);

                if (!previewResult.Success)
                {
                    TempData["Error"] = previewResult.Message;
                    return RedirectToAction("SupDefImport");
                }

                // Generate unique key for this preview session
                var previewKey = $"ResultPreview_{Guid.NewGuid():N}_{DateTime.Now.Ticks}";

                // Store preview data with expiration time
                var sessionData = new
                {
                    PreviewResult = previewResult,
                    ImportType = importType,
                    AcademicYearId = academicYearId,
                    Semester = semester,
                    PreviewKey = previewKey,
                    ExpiresAt = DateTime.Now.AddMinutes(30),
                    CreatedAt = DateTime.Now
                };

                try
                {
                    var serializedData = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });

                    HttpContext.Session.SetString($"ResultPreviewData_{previewKey}", serializedData);

                    // Store the preview key in session as well (backup)
                    HttpContext.Session.SetString("CurrentPreviewKey", previewKey);

                    _logger.LogInformation(
                        "Preview data stored with key: {PreviewKey}, Valid: {ValidCount}, Invalid: {InvalidCount}",
                        previewKey, previewResult.ValidResults.Count, previewResult.InvalidResults.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store preview data in session");
                    TempData["Error"] = "Failed to store preview data. Please try with a smaller file.";
                    return RedirectToAction("SupDefImport");
                }
                // Pass the preview key to the view via ViewBag
                ViewBag.PreviewKey = previewKey;
                return View("SupDefImportPreview", previewResult);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("File upload and preview was cancelled");
                TempData["Error"] = "The operation was cancelled due to timeout.";
                return RedirectToAction("SupDefImport");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file upload and preview");
                TempData["Error"] = "An error occurred while processing the file: " + ex.Message;
                return RedirectToAction("SupDefImport");
            }
        }

        // POST: ResultImport/ProcessImport
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestTimeout(1800000)] // 30 minutes
        public async Task<IActionResult> ProcessImport(string previewKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["Error"] = "User session expired. Please login again.";
                    return RedirectToAction("Login", "Account");
                }

                // Try to get previewKey from multiple sources for reliability
                if (string.IsNullOrEmpty(previewKey))
                {
                    // Try from form
                    previewKey = Request.Form["previewKey"].ToString();
                    _logger.LogInformation("Preview key from form: {PreviewKey}", previewKey);
                }

                if (string.IsNullOrEmpty(previewKey))
                {
                    // Try from session backup
                    previewKey = HttpContext.Session.GetString("CurrentPreviewKey");
                    _logger.LogInformation("Preview key from session backup: {PreviewKey}", previewKey);
                }

                if (string.IsNullOrEmpty(previewKey))
                {
                    _logger.LogWarning("Preview key not found in any source");
                    TempData["Error"] = "Preview data not found. Please upload the file again.";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation("Processing import with preview key: {PreviewKey}", previewKey);

                // Retrieve preview data from session
                var sessionDataJson = HttpContext.Session.GetString($"ResultPreviewData_{previewKey}");
                if (string.IsNullOrEmpty(sessionDataJson))
                {
                    _logger.LogWarning("Preview data not found in session for key: {PreviewKey}", previewKey);
                    TempData["Error"] = "Preview data expired. Please upload the file again.";
                    return RedirectToAction("Index");
                }

                // Deserialize and validate session data
                dynamic sessionData;
                ResultImportPreviewResult previewResult;
                int courseId, academicYearId, semester;

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    sessionData = JsonSerializer.Deserialize<dynamic>(sessionDataJson, options);

                    // Check expiration
                    var expiresAtJson = ((JsonElement)sessionData).GetProperty("expiresAt").GetString();
                    if (DateTime.TryParse(expiresAtJson, out var expiresAt) && DateTime.Now > expiresAt)
                    {
                        _logger.LogWarning("Preview data expired for key: {PreviewKey}", previewKey);
                        HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                        HttpContext.Session.Remove("CurrentPreviewKey");
                        TempData["Error"] = "Preview data has expired. Please upload the file again.";
                        return RedirectToAction("Index");
                    }

                    // Extract data
                    var previewResultJson = ((JsonElement)sessionData).GetProperty("previewResult").GetRawText();
                    previewResult = JsonSerializer.Deserialize<ResultImportPreviewResult>(previewResultJson, options);

                    courseId = ((JsonElement)sessionData).GetProperty("courseId").GetInt32();
                    academicYearId = ((JsonElement)sessionData).GetProperty("academicYearId").GetInt32();
                    semester = ((JsonElement)sessionData).GetProperty("semester").GetInt32();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize preview data for key: {PreviewKey}", previewKey);
                    HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                    HttpContext.Session.Remove("CurrentPreviewKey");
                    TempData["Error"] = "Invalid preview data. Please upload the file again.";
                    return RedirectToAction("Index");
                }

                // Validate preview result
                if (previewResult == null || !previewResult.Success || !previewResult.ValidResults.Any())
                {
                    _logger.LogWarning("Invalid or empty preview result for key: {PreviewKey}", previewKey);
                    HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                    HttpContext.Session.Remove("CurrentPreviewKey");
                    TempData["Error"] = "No valid results found for import. Please check your data.";
                    return RedirectToAction("Index");
                }

                // Verify user still has access
                var courseIds = await GetUserCourseIdsAsync(user);
                if (!courseIds.Contains(courseId))
                {
                    TempData["Error"] = "You do not have access to this course.";
                    //return RedirectToAction("Index");
                }

                // Clean up session data immediately after successful retrieval
                HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                HttpContext.Session.Remove("CurrentPreviewKey");

                // Create progress tracking key
                var progressKey = $"ResultProgress_{Guid.NewGuid():N}_{DateTime.Now.Ticks}";

                _logger.LogInformation(
                    "Starting import process for {StudentCount} students with progress key: {ProgressKey}",
                    previewResult.ValidResults.Count, progressKey);

                // Process the import
                var importResult = await _resultImportService.ProcessImportAsync(
                    previewResult.ValidResults,
                    courseId,
                    academicYearId,
                    semester,
                    user.Id,
                    progressKey,
                    cancellationToken);

                _logger.LogInformation(
                    "Import completed: Success={Success}, Imported={ImportCount}, Failed={FailCount}",
                    importResult.Success, importResult.SuccessfulImports, importResult.FailedImports);

                return View("Results", importResult);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Import processing was cancelled");
                TempData["Error"] = "The import operation was cancelled due to timeout.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during import processing");
                TempData["Error"] = "An error occurred while processing the import: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestTimeout(1800000)] // 30 minutes
        public async Task<IActionResult> ProcessMultiCourseImport(string previewKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["Error"] = "User session expired. Please login again.";
                    return RedirectToAction("Login", "Account");
                }

                // Try to get previewKey from multiple sources for reliability
                if (string.IsNullOrEmpty(previewKey))
                {
                    // Try from form
                    previewKey = Request.Form["previewKey"].ToString();
                    _logger.LogInformation("Preview key from form: {PreviewKey}", previewKey);
                }

                if (string.IsNullOrEmpty(previewKey))
                {
                    // Try from session backup
                    previewKey = HttpContext.Session.GetString("CurrentPreviewKey");
                    _logger.LogInformation("Preview key from session backup: {PreviewKey}", previewKey);
                }

                if (string.IsNullOrEmpty(previewKey))
                {
                    _logger.LogWarning("Preview key not found in any source");
                    TempData["Error"] = "Preview data not found. Please upload the file again.";
                    return RedirectToAction("MultiCourseImport");
                }

                _logger.LogInformation("Processing import with preview key: {PreviewKey}", previewKey);

                // Retrieve preview data from session
                var sessionDataJson = HttpContext.Session.GetString($"ResultPreviewData_{previewKey}");
                if (string.IsNullOrEmpty(sessionDataJson))
                {
                    _logger.LogWarning("Preview data not found in session for key: {PreviewKey}", previewKey);
                    TempData["Error"] = "Preview data expired. Please upload the file again.";
                    return RedirectToAction("MultiCourseImport");
                }

                // Deserialize and validate session data
                dynamic sessionData;
                MultiCourseResultImportPreviewResult previewResult;
                string importType;
                int academicYearId, semester;

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    sessionData = JsonSerializer.Deserialize<dynamic>(sessionDataJson, options);

                    // Check expiration
                    var expiresAtJson = ((JsonElement)sessionData).GetProperty("expiresAt").GetString();
                    if (DateTime.TryParse(expiresAtJson, out var expiresAt) && DateTime.Now > expiresAt)
                    {
                        _logger.LogWarning("Preview data expired for key: {PreviewKey}", previewKey);
                        HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                        HttpContext.Session.Remove("CurrentPreviewKey");
                        TempData["Error"] = "Preview data has expired. Please upload the file again.";
                        return RedirectToAction("MultiCourseImport");
                    }

                    // Extract data
                    var previewResultJson = ((JsonElement)sessionData).GetProperty("previewResult").GetRawText();
                    previewResult = JsonSerializer.Deserialize<MultiCourseResultImportPreviewResult>(previewResultJson, options);

                    importType = ((JsonElement)sessionData).GetProperty("importType").GetString();
                    academicYearId = ((JsonElement)sessionData).GetProperty("academicYearId").GetInt32();
                    semester = ((JsonElement)sessionData).GetProperty("semester").GetInt32();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize preview data for key: {PreviewKey}", previewKey);
                    HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                    HttpContext.Session.Remove("CurrentPreviewKey");
                    TempData["Error"] = "Invalid preview data. Please upload the file again.";
                    return RedirectToAction("MultiCourseImport");
                }

                // Validate preview result
                if (previewResult == null || !previewResult.Success || !previewResult.ValidResults.Any())
                {
                    _logger.LogWarning("Invalid or empty preview result for key: {PreviewKey}", previewKey);
                    HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                    HttpContext.Session.Remove("CurrentPreviewKey");
                    TempData["Error"] = "No valid results found for import. Please check your data.";
                    return RedirectToAction("MultiCourseImport");
                }

                // Verify user still has access
                var courseIds = await GetUserCourseIdsAsync(user);
                /*if (!courseIds.Contains(courseId))
                {
                    TempData["Error"] = "You do not have access to this course.";
                    return RedirectToAction("Index");
                }*/

                // Clean up session data immediately after successful retrieval
                HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                HttpContext.Session.Remove("CurrentPreviewKey");

                // Create progress tracking key
                var progressKey = $"ResultProgress_{Guid.NewGuid():N}_{DateTime.Now.Ticks}";

                _logger.LogInformation(
                    "Starting import process for {StudentCount} students with progress key: {ProgressKey}",
                    previewResult.ValidResults.Count, progressKey);

                // Process the import
                var importResult = await _resultImportService.ProcessMultiCourseImportAsync(
                    previewResult.ValidResults,
                    importType,
                    academicYearId,
                    semester,
                    user.Id,
                    progressKey,
                    cancellationToken);

                _logger.LogInformation(
                    "Import completed: Success={Success}, Imported={ImportCount}, Failed={FailCount}",
                    importResult.Success, importResult.SuccessfulImports, importResult.FailedImports);

                return View("Results", importResult);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Import processing was cancelled");
                TempData["Error"] = "The import operation was cancelled due to timeout.";
                return RedirectToAction("MultiCourseImport");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during import processing");
                TempData["Error"] = "An error occurred while processing the import: " + ex.Message;
                return RedirectToAction("MultiCourseImport");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestTimeout(1800000)] // 30 minutes
        public async Task<IActionResult> ProcessSupDefImport(string previewKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["Error"] = "User session expired. Please login again.";
                    return RedirectToAction("Login", "Account");
                }

                // Try to get previewKey from multiple sources for reliability
                if (string.IsNullOrEmpty(previewKey))
                {
                    // Try from form
                    previewKey = Request.Form["previewKey"].ToString();
                    _logger.LogInformation("Preview key from form: {PreviewKey}", previewKey);
                }

                if (string.IsNullOrEmpty(previewKey))
                {
                    // Try from session backup
                    previewKey = HttpContext.Session.GetString("CurrentPreviewKey");
                    _logger.LogInformation("Preview key from session backup: {PreviewKey}", previewKey);
                }

                if (string.IsNullOrEmpty(previewKey))
                {
                    _logger.LogWarning("Preview key not found in any source");
                    TempData["Error"] = "Preview data not found. Please upload the file again.";
                    return RedirectToAction("MultiCourseImport");
                }

                _logger.LogInformation("Processing import with preview key: {PreviewKey}", previewKey);

                // Retrieve preview data from session
                var sessionDataJson = HttpContext.Session.GetString($"ResultPreviewData_{previewKey}");
                if (string.IsNullOrEmpty(sessionDataJson))
                {
                    _logger.LogWarning("Preview data not found in session for key: {PreviewKey}", previewKey);
                    TempData["Error"] = "Preview data expired. Please upload the file again.";
                    return RedirectToAction("MultiCourseImport");
                }

                // Deserialize and validate session data
                dynamic sessionData;
                SupDefResultImportPreviewResult previewResult;
                string importType;
                int academicYearId, semester;

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    sessionData = JsonSerializer.Deserialize<dynamic>(sessionDataJson, options);

                    // Check expiration
                    var expiresAtJson = ((JsonElement)sessionData).GetProperty("expiresAt").GetString();
                    if (DateTime.TryParse(expiresAtJson, out var expiresAt) && DateTime.Now > expiresAt)
                    {
                        _logger.LogWarning("Preview data expired for key: {PreviewKey}", previewKey);
                        HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                        HttpContext.Session.Remove("CurrentPreviewKey");
                        TempData["Error"] = "Preview data has expired. Please upload the file again.";
                        return RedirectToAction("MultiCourseImport");
                    }

                    // Extract data
                    var previewResultJson = ((JsonElement)sessionData).GetProperty("previewResult").GetRawText();
                    previewResult = JsonSerializer.Deserialize<SupDefResultImportPreviewResult>(previewResultJson, options);

                    importType = ((JsonElement)sessionData).GetProperty("importType").GetString();
                    academicYearId = ((JsonElement)sessionData).GetProperty("academicYearId").GetInt32();
                    semester = ((JsonElement)sessionData).GetProperty("semester").GetInt32();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize preview data for key: {PreviewKey}", previewKey);
                    HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                    HttpContext.Session.Remove("CurrentPreviewKey");
                    TempData["Error"] = "Invalid preview data. Please upload the file again.";
                    return RedirectToAction("MultiCourseImport");
                }

                // Validate preview result
                if (previewResult == null || !previewResult.Success || !previewResult.ValidResults.Any())
                {
                    _logger.LogWarning("Invalid or empty preview result for key: {PreviewKey}", previewKey);
                    HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                    HttpContext.Session.Remove("CurrentPreviewKey");
                    TempData["Error"] = "No valid results found for import. Please check your data.";
                    return RedirectToAction("MultiCourseImport");
                }

                // Verify user still has access
                var courseIds = await GetUserCourseIdsAsync(user);
                /*if (!courseIds.Contains(courseId))
                {
                    TempData["Error"] = "You do not have access to this course.";
                    return RedirectToAction("Index");
                }*/

                // Clean up session data immediately after successful retrieval
                HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                HttpContext.Session.Remove("CurrentPreviewKey");

                // Create progress tracking key
                var progressKey = $"ResultProgress_{Guid.NewGuid():N}_{DateTime.Now.Ticks}";

                _logger.LogInformation(
                    "Starting import process for {StudentCount} students with progress key: {ProgressKey}",
                    previewResult.ValidResults.Count, progressKey);

                // Process the import
                var importResult = await _resultImportService.ProcessSupDefImportAsync(
                    previewResult.ValidResults,
                    importType,
                    academicYearId,
                    semester,
                    user.Id,
                    progressKey,
                    cancellationToken);

                _logger.LogInformation(
                    "Import completed: Success={Success}, Imported={ImportCount}, Failed={FailCount}",
                    importResult.Success, importResult.SuccessfulImports, importResult.FailedImports);

                return View("Results", importResult);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Import processing was cancelled");
                TempData["Error"] = "The import operation was cancelled due to timeout.";
                return RedirectToAction("MultiCourseImport");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during import processing");
                TempData["Error"] = "An error occurred while processing the import: " + ex.Message;
                return RedirectToAction("MultiCourseImport");
            }
        }

        // GET: ResultImport/DownloadErrorReport
        [HttpGet]
        [RequestTimeout(300000)] // 5 minutes
        public async Task<IActionResult> DownloadErrorReport(
            string previewKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(previewKey))
                {
                    // Try to get from session backup
                    previewKey = HttpContext.Session.GetString("CurrentPreviewKey");
                }

                if (string.IsNullOrEmpty(previewKey))
                {
                    TempData["Error"] = "No preview data available for error report.";
                    return RedirectToAction("Index");
                }

                var sessionDataJson = HttpContext.Session.GetString($"ResultPreviewData_{previewKey}");
                if (string.IsNullOrEmpty(sessionDataJson))
                {
                    TempData["Error"] = "Preview data expired. Please upload the file again.";
                    return RedirectToAction("Index");
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var sessionData = JsonSerializer.Deserialize<dynamic>(sessionDataJson, options);
                var previewResultJson = ((JsonElement)sessionData).GetProperty("previewResult").GetRawText();
                var previewResult = JsonSerializer.Deserialize<ResultImportPreviewResult>(previewResultJson, options);

                if (previewResult?.InvalidResults == null || !previewResult.InvalidResults.Any())
                {
                    TempData["Info"] = "No errors found to download.";
                    return RedirectToAction("Index");
                }

                var errorReportBytes = await _resultImportService.GenerateErrorReportAsync(
                    previewResult, cancellationToken);

                var fileName = $"Result_Import_Errors_{previewResult.CourseCode}_Sem{previewResult.Semester}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                return File(
                    errorReportBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Error report generation was cancelled");
                TempData["Error"] = "Error report generation was cancelled.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating error report");
                TempData["Error"] = "An error occurred while generating the error report.";
                return RedirectToAction("Index");
            }
        }

        // POST: ResultImport/CancelImport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelImport()
        {
            try
            {
                ClearSessionData();
                TempData["Info"] = "Import operation cancelled.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling import");
                TempData["Error"] = "An error occurred while cancelling the import.";
                return RedirectToAction("Index");
            }
        }

        // GET: ResultImport/GetImportProgress (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetImportProgress(
            string progressKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(progressKey))
                {
                    return Json(new { success = false, message = "Progress key is required" });
                }

                var progress = await _resultImportService.GetImportProgressAsync(
                    progressKey, cancellationToken);

                if (progress == null)
                {
                    return Json(new
                    {
                        success = true,
                        progress = new
                        {
                            currentStep = "Completed",
                            percentComplete = 100,
                            message = "Import completed",
                            isComplete = true
                        }
                    });
                }

                return Json(new
                {
                    success = true,
                    progress = new
                    {
                        currentStep = progress.CurrentStep,
                        percentComplete = progress.PercentComplete,
                        message = progress.Message,
                        currentStudent = progress.CurrentStudent,
                        totalStudents = progress.TotalStudents,
                        successCount = progress.SuccessCount,
                        failureCount = progress.FailureCount,
                        isComplete = progress.IsComplete,
                        hasErrors = progress.HasErrors
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting import progress");
                return Json(new { success = false, message = "Error retrieving progress" });
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Get course IDs that the user has access to
        /// </summary>
        private async Task<List<int>> GetUserCourseIdsAsync(ApplicationUser user)
        {
            var courseIds = new List<int>();

            bool isDean = await _userManager.IsInRoleAsync(user, "Dean");
            bool isHOD = await _userManager.IsInRoleAsync(user, "HOD");
            bool isLecturer = await _userManager.IsInRoleAsync(user, "Lecturer");

            if (isDean)
            {
                // Dean has access to all courses in their school
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
                // HOD has access to all courses in their department
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
            else if (isLecturer)
            {
                // Lecturer has access to courses they teach
                courseIds = await _context.Courses
                    .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == user.Id) ||
                               c.InstructorId == user.Id)
                    .Select(c => c.Id)
                    .ToListAsync();
            }

            return courseIds;
        }

        /// <summary>
        /// Clear all session data related to result import
        /// </summary>
        private void ClearSessionData()
        {
            try
            {
                // Get the current preview key
                var previewKey = HttpContext.Session.GetString("CurrentPreviewKey");

                if (!string.IsNullOrEmpty(previewKey))
                {
                    HttpContext.Session.Remove($"ResultPreviewData_{previewKey}");
                    _logger.LogDebug("Removed preview data for key: {PreviewKey}", previewKey);
                }

                // Clear the backup key
                HttpContext.Session.Remove("CurrentPreviewKey");

                // Clean up any orphaned preview data (optional - for maintenance)
                // Note: This is a simple approach. In production, consider a background job
                var sessionKeys = HttpContext.Session.Keys.Where(k => k.StartsWith("ResultPreviewData_")).ToList();
                foreach (var key in sessionKeys)
                {
                    try
                    {
                        var dataJson = HttpContext.Session.GetString(key);
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            var data = JsonSerializer.Deserialize<dynamic>(dataJson);
                            var expiresAtJson = ((JsonElement)data).GetProperty("expiresAt").GetString();
                            if (DateTime.TryParse(expiresAtJson, out var expiresAt) && DateTime.Now > expiresAt)
                            {
                                HttpContext.Session.Remove(key);
                                _logger.LogDebug("Cleaned up expired session data: {Key}", key);
                            }
                        }
                    }
                    catch
                    {
                        // If we can't parse it, remove it
                        HttpContext.Session.Remove(key);
                    }
                }

                _logger.LogDebug("Session data cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error clearing session data");
            }
        }

        #endregion
    }

    #region View Models

    /// <summary>
    /// View model for the index page
    /// </summary>
    public class ResultImportIndexViewModel
    {
        public List<CourseImportInfo> Courses { get; set; } = new List<CourseImportInfo>();
        public List<AcademicYear> AcademicYears { get; set; } = new List<AcademicYear>();
        public long MaxFileSize { get; set; }
        public string[] SupportedFormats { get; set; }
    }

    /// <summary>
    /// Course information for import selection
    /// </summary>
    public class CourseImportInfo
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string ProgrammeName { get; set; }
        public string SchoolName { get; set; }
        public int Credits { get; set; }
    }

    #endregion
}