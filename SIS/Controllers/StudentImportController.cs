using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models;
using SIS.Models.Registration;
using SIS.Models.ViewModels;
using SIS.Services.Emails;
using SIS.Services.StudentImport;
using System.Text.Json;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin,Registrar")]
    public class StudentImportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStudentImportService _studentImportService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly EmailService _emailService;
        private readonly ILogger<StudentImportController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public StudentImportController(
            ApplicationDbContext context,
            IStudentImportService studentImportService,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            EmailService emailService,
            ILogger<StudentImportController> logger,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _studentImportService = studentImportService;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: StudentImport
        public async Task<IActionResult> Index()
        {
            try
            {
                // Clear any existing session data to prevent issues
                ClearSessionData();

                // Get summary statistics for the view
                var totalStudents = await _context.Students.CountAsync();
                var totalAdmittedStudents = await _context.Students
                    .Where(s => s.StudentStatus == Status.Admitted)
                    .CountAsync();
                var totalRegisteredStudents = await _context.Students
                    .Where(s => s.IsRegistered)
                    .CountAsync();

                var viewModel = new StudentImportIndexViewModel
                {
                    TotalStudents = totalStudents,
                    TotalAdmittedStudents = totalAdmittedStudents,
                    TotalRegisteredStudents = totalRegisteredStudents,
                    MaxFileSize = 10 * 1024 * 1024, // 10MB
                    MaxRowsPerImport = 1000,
                    SupportedFormats = new[] { ".xlsx" },
                    RequiredColumns = new[]
                    {
                        "FullName", "Email", "StudentId_Number", "NrcOrPassportNumber",
                        "DateOfBirth (YYYY-MM-DD)", "Gender", "Phone", "Nationality",
                        "SchoolId", "ProgrammeId", "ProgrammeLevelId", "ModeOfStudyId",
                        "YearPeriodId", "StudentCurrentYear",
                        "AddressLine1", "City", "State", "Country",
                        "NextOfKinName", "NextOfKinRelation", "NextOfKinPhone",
                        "DepartmentId (Optional - for hierarchy validation)"
                    }
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student import index page");
                TempData["Error"] = "An error occurred while loading the import page.";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: StudentImport/UploadAndPreview
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestTimeout(300000)] // Reduced timeout to 5 minutes
        public async Task<IActionResult> UploadAndPreview(IFormFile importFile, CancellationToken cancellationToken = default)
        {
            try
            {
                // Clear any existing session data first
                ClearSessionData();

                // Validate file upload
                var fileValidation = ValidateUploadedFile(importFile);
                if (!fileValidation.IsValid)
                {
                    TempData["Error"] = fileValidation.ErrorMessage;
                    return RedirectToAction("Index");
                }

                // Process the file and get preview results with cancellation support
                var previewResult = await _studentImportService.PreviewImportDataAsync(importFile, cancellationToken);

                if (!previewResult.Success)
                {
                    TempData["Error"] = previewResult.Message;
                    return RedirectToAction("Index");
                }

                // Generate unique key for this preview session
                var previewKey = $"Preview_{Guid.NewGuid():N}_{DateTime.Now.Ticks}";

                // Store preview data with expiration time
                var sessionData = new
                {
                    PreviewResult = previewResult,
                    ExpiresAt = DateTime.Now.AddMinutes(30), // 30 minute expiration
                    CreatedAt = DateTime.Now
                };

                try
                {
                    var serializedData = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false // Reduce size
                    });

                    HttpContext.Session.SetString($"PreviewData_{previewKey}", serializedData);
                    TempData["PreviewKey"] = previewKey;

                    _logger.LogInformation($"Preview data stored with key: {previewKey}, Valid: {previewResult.ValidStudents.Count}, Invalid: {previewResult.InvalidStudents.Count}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store preview data in session");
                    TempData["Error"] = "Failed to store preview data. Please try with a smaller file.";
                    return RedirectToAction("Index");
                }

                // Pass statistics via ViewBag for the layout
                ViewBag.TotalStudents = await _context.Students.CountAsync(cancellationToken);
                ViewBag.TotalAdmittedStudents = await _context.Students
                    .Where(s => s.StudentStatus == Status.Admitted)
                    .CountAsync(cancellationToken);
                ViewBag.TotalRegisteredStudents = await _context.Students
                    .Where(s => s.IsRegistered)
                    .CountAsync(cancellationToken);
                ViewBag.MaxFileSize = 10 * 1024 * 1024; // 10MB
                ViewBag.MaxRowsPerImport = 1000;
                ViewBag.RequiredColumns = new[]
                {
                    "FullName", "Email", "StudentId_Number", "NrcOrPassportNumber",
                    "DateOfBirth (YYYY-MM-DD)", "Gender", "Phone", "Nationality",
                    "SchoolId", "ProgrammeId", "ProgrammeLevelId", "ModeOfStudyId",
                    "YearPeriodId", "StudentCurrentYear",
                    "AddressLine1", "City", "State", "Country",
                    "NextOfKinName", "NextOfKinRelation", "NextOfKinPhone",
                    "DepartmentId (Optional - for hierarchy validation)"
                };

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
                TempData["Error"] = "An error occurred while processing the uploaded file.";
                return RedirectToAction("Index");
            }
        }

        // POST: StudentImport/ProcessImport
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestTimeout(1800000)] // 30 minutes for processing
        public async Task<IActionResult> ProcessImport(CancellationToken cancellationToken = default)
        {
            try
            {
                // Retrieve and validate preview data
                var previewKey = TempData["PreviewKey"] as string;
                if (string.IsNullOrEmpty(previewKey))
                {
                    _logger.LogWarning("Preview key not found in TempData");
                    TempData["Error"] = "Preview data not found. Please upload the file again.";
                    return RedirectToAction("Index");
                }

                var sessionDataJson = HttpContext.Session.GetString($"PreviewData_{previewKey}");
                if (string.IsNullOrEmpty(sessionDataJson))
                {
                    _logger.LogWarning($"Preview data not found in session for key: {previewKey}");
                    TempData["Error"] = "Preview data expired. Please upload the file again.";
                    return RedirectToAction("Index");
                }

                // Deserialize and validate session data
                dynamic sessionData;
                ImportPreviewResult previewResult;

                try
                {
                    sessionData = JsonSerializer.Deserialize<dynamic>(sessionDataJson, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    // Check expiration
                    var expiresAtJson = ((JsonElement)sessionData).GetProperty("expiresAt").GetString();
                    if (DateTime.TryParse(expiresAtJson, out var expiresAt) && DateTime.Now > expiresAt)
                    {
                        _logger.LogWarning($"Preview data expired for key: {previewKey}");
                        HttpContext.Session.Remove($"PreviewData_{previewKey}");
                        TempData["Error"] = "Preview data has expired. Please upload the file again.";
                        return RedirectToAction("Index");
                    }

                    // Extract preview result
                    var previewResultJson = ((JsonElement)sessionData).GetProperty("previewResult").GetRawText();
                    previewResult = JsonSerializer.Deserialize<ImportPreviewResult>(previewResultJson, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to deserialize preview data for key: {previewKey}");
                    HttpContext.Session.Remove($"PreviewData_{previewKey}");
                    TempData["Error"] = "Invalid preview data. Please upload the file again.";
                    return RedirectToAction("Index");
                }

                if (previewResult == null || !previewResult.Success || !previewResult.ValidStudents.Any())
                {
                    _logger.LogWarning($"Invalid or empty preview result for key: {previewKey}");
                    HttpContext.Session.Remove($"PreviewData_{previewKey}");
                    TempData["Error"] = "No valid students found for import. Please check your data and try again.";
                    return RedirectToAction("Index");
                }

                // Clean up session data immediately after successful retrieval
                HttpContext.Session.Remove($"PreviewData_{previewKey}");

                // Create a progress key for tracking
                var progressKey = $"Progress_{Guid.NewGuid():N}_{DateTime.Now.Ticks}";
                TempData["ProgressKey"] = progressKey;

                _logger.LogInformation($"Starting import process for {previewResult.ValidStudents.Count} students with progress key: {progressKey}");

                // Process the import with cancellation support
                var importResult = await _studentImportService.ProcessImportAsync(
                    previewResult.ValidStudents,
                    User.Identity.Name,
                    progressKey,
                    cancellationToken);

                // Generate detailed results
                var detailedResults = new ImportProcessResult
                {
                    Success = importResult.Success,
                    Message = importResult.Message,
                    TotalProcessed = importResult.TotalProcessed,
                    SuccessfulImports = importResult.SuccessfulImports,
                    FailedImports = importResult.FailedImports,
                    ImportedStudents = importResult.ImportedStudents,
                    FailedRows = importResult.FailedRows,
                    ProcessingTime = importResult.ProcessingTime,
                    EmailsSent = importResult.EmailsSent,
                    ImportSummary = new ImportSummary
                    {
                        TotalRowsInFile = previewResult.TotalRows,
                        ValidRowsForImport = previewResult.ValidStudents?.Count ?? 0,
                        InvalidRowsSkipped = previewResult.InvalidStudents?.Count ?? 0,
                        UsersCreated = importResult.SuccessfulImports,
                        EmailsSent = importResult.EmailsSent,
                        ErrorsEncountered = importResult.FailedImports
                    }
                };

                // Store results for potential download/review
                try
                {
                    var resultsJson = JsonSerializer.Serialize(detailedResults, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });
                    TempData["ImportResults"] = resultsJson;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to serialize import results for storage");
                    // Continue without storing results
                }

                _logger.LogInformation($"Import process completed. Success: {importResult.Success}, Imported: {importResult.SuccessfulImports}, Failed: {importResult.FailedImports}");

                // Force cleanup after import
                ForceCleanupAfterImport();

                return View("Results", detailedResults);
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
                TempData["Error"] = "An error occurred while processing the import.";
                return RedirectToAction("Index");
            }
        }

        // GET: StudentImport/DownloadTemplate
        [RequestTimeout(300000)] // 5 minutes
        public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken = default)
        {
            try
            {
                var templateBytes = await _studentImportService.GenerateImportTemplateAsync(cancellationToken);
                var fileName = $"Student_Import_Template_{DateTime.Now:yyyyMMdd}.xlsx";

                return File(templateBytes,
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
                TempData["Error"] = "An error occurred while generating the template.";
                return RedirectToAction("Index");
            }
        }

        // GET: StudentImport/DownloadErrorReport
        [RequestTimeout(300000)] // 5 minutes
        public async Task<IActionResult> DownloadErrorReport(CancellationToken cancellationToken = default)
        {
            try
            {
                // Retrieve preview data from TempData
                var previewKey = TempData["PreviewKey"] as string;
                if (string.IsNullOrEmpty(previewKey))
                {
                    TempData["Error"] = "No error data available for download.";
                    return RedirectToAction("Index");
                }

                var previewDataJson = HttpContext.Session.GetString($"PreviewData_{previewKey}");
                if (string.IsNullOrEmpty(previewDataJson))
                {
                    TempData["Error"] = "Preview data expired. Please upload the file again.";
                    return RedirectToAction("Index");
                }

                var sessionData = JsonSerializer.Deserialize<dynamic>(previewDataJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var previewResultJson = ((JsonElement)sessionData).GetProperty("previewResult").GetRawText();
                var previewResult = JsonSerializer.Deserialize<ImportPreviewResult>(previewResultJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (previewResult?.InvalidStudents == null || !previewResult.InvalidStudents.Any())
                {
                    TempData["Info"] = "No errors found to download.";
                    return RedirectToAction("Index");
                }

                var errorReportBytes = await _studentImportService.GenerateErrorReportAsync(previewResult, cancellationToken);
                var fileName = $"Student_Import_Errors_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                return File(errorReportBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Error report generation was cancelled");
                TempData["Error"] = "Error report generation was cancelled due to timeout.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating error report");
                TempData["Error"] = "An error occurred while generating the error report.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelImport()
        {
            try
            {
                // Clean up all session data
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

        // GET: StudentImport/GetImportProgress (for AJAX polling)
        [HttpGet]
        public async Task<IActionResult> GetImportProgress(string progressKey, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(progressKey))
                {
                    return Json(new { success = false, message = "Progress key is required" });
                }

                var progress = await _studentImportService.GetImportProgressAsync(progressKey, cancellationToken);

                return Json(new
                {
                    success = true,
                    progress = progress ?? new ImportProgress
                    {
                        CurrentStep = "Completed",
                        PercentComplete = 100,
                        Message = "Import completed",
                        CurrentBatch = 0,
                        TotalBatches = 0
                    }
                });
            }
            catch (OperationCanceledException)
            {
                return Json(new { success = false, message = "Operation cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting import progress");
                return Json(new { success = false, message = "Error retrieving progress" });
            }
        }

        // Additional helper methods remain the same...
        // [Rest of the existing helper methods]

        #region Private Helper Methods

        /// <summary>
        /// Clear all session data related to import operations
        /// </summary>
        private void ClearSessionData()
        {
            try
            {
                // Get all session keys that start with our prefixes
                var keysToRemove = new List<string>();

                // Note: HttpContext.Session doesn't provide a way to enumerate keys directly
                // So we'll use TempData to track and clean up

                var previewKey = TempData["PreviewKey"] as string;
                var progressKey = TempData["ProgressKey"] as string;

                if (!string.IsNullOrEmpty(previewKey))
                {
                    HttpContext.Session.Remove($"PreviewData_{previewKey}");
                }

                if (!string.IsNullOrEmpty(progressKey))
                {
                    // Clean up progress tracking
                    _ = Task.Run(() => _studentImportService.CleanupProgressAsync(progressKey));
                }

                // Clear TempData
                TempData.Remove("PreviewKey");
                TempData.Remove("ImportResults");
                TempData.Remove("ProgressKey");

                _logger.LogDebug("Session data cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error clearing session data");
            }
        }














        /// <summary>
        /// Force cleanup of memory and session data after import
        /// </summary>
        private void ForceCleanupAfterImport()
        {
            try
            {
                // Clear all session data
                ClearSessionData();

                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                _logger.LogDebug("Forced cleanup completed after import");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during forced cleanup");
            }
        }







        private FileValidationResult ValidateUploadedFile(IFormFile file)
        {
            // Check if file was uploaded
            if (file == null || file.Length == 0)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Please select a file to upload."
                };
            }

            // Check file size (10MB limit)
            const long maxFileSize = 10 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File size exceeds the maximum limit of 10MB."
                };
            }

            // Check file extension
            var allowedExtensions = new[] { ".xlsx" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Only Excel files (.xlsx) are supported."
                };
            }

            // Check content type
            var allowedContentTypes = new[] {
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "application/vnd.ms-excel"
            };
            if (!allowedContentTypes.Contains(file.ContentType))
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid file type. Please upload a valid Excel file."
                };
            }

            return new FileValidationResult { IsValid = true };
        }

        #endregion

        #region Helper Classes

        public class FileValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }

        #endregion
    }

    // ViewModels remain the same...
    #region ViewModels and DTOs

    public class StudentImportIndexViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalAdmittedStudents { get; set; }
        public int TotalRegisteredStudents { get; set; }
        public long MaxFileSize { get; set; }
        public int MaxRowsPerImport { get; set; }
        public string[] SupportedFormats { get; set; }
        public string[] RequiredColumns { get; set; }
    }

    #endregion
}