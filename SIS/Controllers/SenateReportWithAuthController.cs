using ClosedXML.Excel;
using iText.Html2pdf;
using iText.Kernel.Pdf;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Data;
using SIS.Models.Accounts;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.Reports;
using SIS.Models.StudentApplication;
using SIS.Services.Reports;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin,Registrar,Dean,HOD,VC,DVC")]
    public class SenateReportWithAuthController : Controller
    {
        private readonly ISenateReportService _senateReportService;
        private readonly ApplicationDbContext _context;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SenateReportWithAuthController> _logger;
        private readonly IMemoryCache _cache;

        // Cache keys
        private const string GRADE_CONFIGS_CACHE_KEY = "GradeConfigurations";
        private const string FILTER_OPTIONS_CACHE_KEY = "FilterOptions_{0}"; // {0} = userId
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public SenateReportWithAuthController(
            ISenateReportService senateReportService,
            ApplicationDbContext context,
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            ILogger<SenateReportWithAuthController> logger,
            IMemoryCache cache)
        {
            _senateReportService = senateReportService;
            _context = context;
            _contextFactory = contextFactory;
            _userManager = userManager;
            _logger = logger;
            _cache = cache;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Unauthorized();
                }

                // Get role flags efficiently
                var (isAdmin, isDean, isHOD, isVC, isDVC, isRegistrar) = GetUserRoles();

                var filterOptions = await _senateReportService.GetFilterOptionsAsync();

                // Initialize filtered data containers
                List<School> filteredSchools;
                List<Department> filteredDepartments;
                List<Programme> filteredProgrammes;

                // Apply role-based filtering with optimized queries
                if (!isAdmin && !isRegistrar && !isVC && !isDVC)
                {
                    if (isDean)
                    {
                        // Dean: Single optimized query with projection
                        var userSchoolIds = await _context.Schools
                            .AsNoTracking()
                            .Where(s => s.DeanId == currentUser.Id || s.AssistantDeanId == currentUser.Id)
                            .Select(s => s.Id)
                            .ToListAsync();

                        filteredSchools = filterOptions.Schools
                            .Where(s => userSchoolIds.Contains(s.Id))
                            .ToList();

                        // Batch load departments and programmes
                        var deptAndProgrammes = await _context.Departments
                            .AsNoTracking()
                            .Where(d => userSchoolIds.Contains(d.SchoolId))
                            .Select(d => new
                            {
                                Department = d,
                                Programmes = _context.Programmes
                                    .Where(p => p.DepartmentId == d.Id)
                                    .ToList()
                            })
                            .ToListAsync();

                        filteredDepartments = deptAndProgrammes.Select(x => x.Department).ToList();
                        filteredProgrammes = deptAndProgrammes.SelectMany(x => x.Programmes).ToList();
                    }
                    else if (isHOD)
                    {
                        // HOD: Optimized single query with includes
                        var userDepartments = await _context.Departments
                            .AsNoTracking()
                            .Where(d => d.HODId == currentUser.Id)
                            .Include(d => d.School)
                            .ToListAsync();

                        var departmentIds = userDepartments.Select(d => d.Id).ToList();

                        filteredSchools = userDepartments
                            .Select(d => d.School)
                            .Where(s => s != null)
                            .DistinctBy(s => s.Id)
                            .ToList();

                        filteredDepartments = userDepartments;

                        // Single query for programmes
                        filteredProgrammes = await _context.Programmes
                            .AsNoTracking()
                            .Where(p => departmentIds.Contains(p.DepartmentId))
                            .ToListAsync();
                    }
                    else
                    {
                        filteredSchools = new List<School>();
                        filteredDepartments = new List<Department>();
                        filteredProgrammes = new List<Programme>();
                    }
                }
                else
                {
                    // Full access roles - use DbContextFactory for parallel operations
                    filteredSchools = filterOptions.Schools;

                    // Use separate contexts for parallel operations to avoid "second operation started" error
                    await using var context1 = await _contextFactory.CreateDbContextAsync();
                    await using var context2 = await _contextFactory.CreateDbContextAsync();

                    var departmentsTask = context1.Departments.AsNoTracking().ToListAsync();
                    var programmesTask = context2.Programmes.AsNoTracking().ToListAsync();

                    await Task.WhenAll(departmentsTask, programmesTask);

                    filteredDepartments = departmentsTask.Result;
                    filteredProgrammes = programmesTask.Result;
                }

                // Update filter options
                filterOptions.Schools = filteredSchools;

                // Set ViewBag data
                ViewBag.FilteredDepartments = filteredDepartments;
                ViewBag.FilteredProgrammes = filteredProgrammes;

                if (isHOD && filteredSchools.Any())
                {
                    ViewBag.DefaultSchoolId = filteredSchools.First().Id;
                    ViewBag.DefaultDepartmentId = filteredDepartments.FirstOrDefault()?.Id;
                    ViewBag.DefaultProgrammeId = filteredProgrammes.FirstOrDefault()?.Id;
                }
                else if (isDean && filteredSchools.Any())
                {
                    ViewBag.DefaultSchoolId = filteredSchools.First().Id;
                }

                ViewBag.IsAdmin = isAdmin;
                ViewBag.IsDean = isDean;
                ViewBag.IsHOD = isHOD;
                ViewBag.IsVC = isVC;
                ViewBag.IsDVC = isDVC;
                ViewBag.IsRegistrar = isRegistrar;
                ViewBag.CurrentUser = currentUser.UserName;
                ViewBag.UserId = currentUser.Id;

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
                int programmeId = filters.ProgrammeId ?? 0;
                int academicYearId = filters.AcademicYearId ?? 0;
                int semester = filters.AcademicPeriod ?? 0;
                int yearOfStudy = filters.YearOfStudy ?? 0;

                var students = await _senateReportService.GetEntityStudentDetailsAsync(
                    programmeId,
                    filters.ReportLevel,
                    filters
                );

                var summary = await _senateReportService.GetPerformanceSummaryAsync(
                        programmeId,
                        academicYearId,
                        semester,
                        yearOfStudy
                    );

                var gradingOverview = await _senateReportService.GetProgrammeGradingOverviewAsync(
                        programmeId,
                        academicYearId,
                        semester,
                        yearOfStudy);

                return Json(new
                {
                    success = true,
                    data = students,
                    performanceSummary = summary,
                    gradingOverview = gradingOverview,
                    message = $"Report generated successfully with {students.Count} entities"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                return Json(new
                {
                    success = false,
                    message = "Failed to generate report.",
                    error = ex.Message
                });
            }
        }

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

                if (!await CanAccessProgrammeAsync(programmeId))
                {
                    return Json(new { success = false, message = "Access denied to this programme." });
                }

                var summary = await _senateReportService.GetPerformanceSummaryAsync(
                    programmeId, academicYearId, semester, 1);

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

                var drillFilters = CreateDrillDownFilters(filters, level, entityId);

                var currentUser = await _userManager.GetUserAsync(User);
                drillFilters = await ApplyRoleBasedFilters(drillFilters, currentUser);

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

                if (!await CanAccessSchoolAsync(schoolId))
                {
                    return Json(new { success = false, message = "Access denied to this school." });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                bool isHOD = User.IsInRole("HOD");

                // Single optimized query with conditional HOD filtering
                var departmentsQuery = _context.Departments
                    .AsNoTracking()
                    .Where(d => d.SchoolId == schoolId);

                if (isHOD)
                {
                    departmentsQuery = departmentsQuery.Where(d => d.HODId == userId);
                }

                var departments = await departmentsQuery
                    .Select(d => new { d.Id, d.Name })
                    .ToListAsync();

                return Json(new { success = true, departments });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting departments for school {schoolId}");
                return Json(new { success = false, message = "Failed to load departments." });
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

                if (!await CanAccessDepartmentAsync(departmentId))
                {
                    return Json(new { success = false, message = "Access denied to this department." });
                }

                // Optimized single query with projection
                var programmes = await _context.Programmes
                    .AsNoTracking()
                    .Where(p => p.DepartmentId == departmentId)
                    .Select(p => new { p.Id, p.Name })
                    .ToListAsync();

                return Json(new { success = true, programmes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting programmes for department {departmentId}");
                return Json(new { success = false, message = "Failed to load programmes." });
            }
        }

        #region CORRECTED Export Methods - Using Existing GenerateReport Logic

        [HttpPost]
        public async Task<IActionResult> ExportToPdf([FromBody] SenateReportFilters filters)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                // Use the SAME logic as GenerateReport method
                int programmeId = filters.ProgrammeId ?? 0;
                int academicYearId = filters.AcademicYearId ?? 0;
                int semester = filters.AcademicPeriod ?? 0;
                int yearOfStudy = filters.YearOfStudy ?? 0;

                // Get data using your existing service calls
                var students = await _senateReportService.GetEntityStudentDetailsAsync(
                    programmeId,
                    filters.ReportLevel,
                    filters
                );

                var summary = await _senateReportService.GetPerformanceSummaryAsync(
                    programmeId,
                    academicYearId,
                    semester,
                    yearOfStudy
                );

                var gradingOverview = await _senateReportService.GetProgrammeGradingOverviewAsync(
                    programmeId,
                    academicYearId,
                    semester,
                    yearOfStudy
                );

                if (students == null || !students.Any())
                {
                    return Json(new { success = false, message = "No data available to export. Please generate a report first." });
                }

                // Generate HTML content for PDF
                var htmlContent = await GeneratePdfHtmlAsync(students, summary, gradingOverview, filters);

                // Convert HTML to PDF
                var pdfBytes = ConvertHtmlToPdf(htmlContent);

                // Return file for download
                var fileName = $"Senate_Report_{filters.ReportLevel}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF export");
                return Json(new { success = false, message = $"Error generating PDF: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportToExcel([FromBody] SenateReportFilters filters)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                // Use the SAME logic as GenerateReport method
                int programmeId = filters.ProgrammeId ?? 0;
                int academicYearId = filters.AcademicYearId ?? 0;
                int semester = filters.AcademicPeriod ?? 0;
                int yearOfStudy = filters.YearOfStudy ?? 0;

                // Get data using your existing service calls
                var students = await _senateReportService.GetEntityStudentDetailsAsync(
                    programmeId,
                    filters.ReportLevel,
                    filters
                );

                var summary = await _senateReportService.GetPerformanceSummaryAsync(
                    programmeId,
                    academicYearId,
                    semester,
                    yearOfStudy
                );

                var gradingOverview = await _senateReportService.GetProgrammeGradingOverviewAsync(
                    programmeId,
                    academicYearId,
                    semester,
                    yearOfStudy
                );

                if (students == null || !students.Any())
                {
                    return Json(new { success = false, message = "No data available to export. Please generate a report first." });
                }

                // Create Excel workbook
                using var workbook = new XLWorkbook();

                // Add sheets
                CreateSummarySheet(workbook, students, summary, filters);
                CreateStudentPerformanceSheet(workbook, students, filters);

                if (gradingOverview != null && gradingOverview.Courses?.Any() == true)
                {
                    CreateGradingOverviewSheet(workbook, gradingOverview);
                }

                if (summary != null && summary.TotalStudents > 0)
                {
                    CreatePerformanceSummarySheet(workbook, summary);
                }

                // Save to memory stream
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                var fileName = $"Senate_Report_{filters.ReportLevel}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Excel export");
                return Json(new { success = false, message = $"Error generating Excel: {ex.Message}" });
            }
        }

        #endregion

        #region PDF Generation Helper Methods

        private async Task<string> GeneratePdfHtmlAsync(
            List<StudentProgressionData> students,
            PerformanceSummaryDto summary,
            ProgrammeGradingOverview gradingOverview,
            SenateReportFilters filters)
        {
            var sb = new StringBuilder();

            // Get filter display values
            var academicYearName = "N/A";
            var programmeName = "N/A";
            var schoolName = "N/A";
            var departmentName = "N/A";

            if (filters.AcademicYearId.HasValue)
            {
                var year = await _context.AcademicYears.FindAsync(filters.AcademicYearId.Value);
                academicYearName = year?.YearValue ?? "N/A";
            }

            if (filters.ProgrammeId.HasValue)
            {
                var programme = await _context.Programmes
                    .Include(p => p.Department)
                    .ThenInclude(d => d.School)
                    .FirstOrDefaultAsync(p => p.Id == filters.ProgrammeId.Value);

                if (programme != null)
                {
                    programmeName = programme.Name;
                    departmentName = programme.Department?.Name ?? "N/A";
                    schoolName = programme.Department?.School?.Name ?? "N/A";
                }
            }
            else if (filters.DepartmentId.HasValue)
            {
                var department = await _context.Departments
                    .Include(d => d.School)
                    .FirstOrDefaultAsync(d => d.Id == filters.DepartmentId.Value);

                if (department != null)
                {
                    departmentName = department.Name;
                    schoolName = department.School?.Name ?? "N/A";
                }
            }
            else if (filters.SchoolId.HasValue)
            {
                var school = await _context.Schools.FindAsync(filters.SchoolId.Value);
                schoolName = school?.Name ?? "N/A";
            }

            // HTML Head with comprehensive styles
            sb.Append(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; font-size: 11px; line-height: 1.4; }
        .header { text-align: center; margin-bottom: 25px; border-bottom: 3px solid #1e40af; padding-bottom: 15px; }
        .header h1 { font-size: 22px; margin: 8px 0; color: #1e40af; letter-spacing: 1px; }
        .header h2 { font-size: 16px; margin: 5px 0; color: #374151; }
        .header p { font-size: 12px; color: #6b7280; margin: 3px 0; }
        
        .meta-info { background: linear-gradient(to right, #f3f4f6, #e5e7eb); padding: 12px; margin-bottom: 20px; border-radius: 5px; border-left: 4px solid #1e40af; }
        .meta-info table { width: 100%; border-collapse: collapse; }
        .meta-info td { padding: 4px 8px; vertical-align: top; }
        .meta-info td:nth-child(odd) { font-weight: 600; color: #374151; width: 25%; }
        .meta-info td:nth-child(even) { color: #1f2937; }
        
        .summary-stats { display: table; width: 100%; margin-bottom: 25px; table-layout: fixed; }
        .stat-box { display: table-cell; padding: 12px; text-align: center; border: 2px solid #e5e7eb; background: linear-gradient(to bottom, #ffffff, #f9fafb); }
        .stat-box:not(:last-child) { border-right: none; }
        .stat-box .label { font-size: 9px; color: #6b7280; text-transform: uppercase; margin-bottom: 6px; font-weight: 600; letter-spacing: 0.5px; }
        .stat-box .value { font-size: 20px; font-weight: bold; color: #1f2937; }
        .stat-box:nth-child(1) .value { color: #1e40af; }
        .stat-box:nth-child(2) .value { color: #059669; }
        .stat-box:nth-child(3) .value { color: #dc2626; }
        .stat-box:nth-child(4) .value { color: #7c3aed; }
        
        h3.section-title { 
            color: #1e40af; 
            border-bottom: 2px solid #1e40af; 
            padding-bottom: 8px; 
            margin: 25px 0 12px 0; 
            font-size: 14px;
            display: flex;
            align-items: center;
        }
        
        table.data-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 10px; }
        table.data-table th { 
            background: linear-gradient(to bottom, #1e40af, #1e3a8a); 
            color: white; 
            padding: 8px 6px; 
            text-align: left; 
            font-size: 10px; 
            border: 1px solid #1e3a8a; 
            font-weight: 600;
        }
        table.data-table td { padding: 6px; border: 1px solid #e5e7eb; }
        table.data-table tr:nth-child(even) { background-color: #f9fafb; }
        table.data-table tr:hover { background-color: #f3f4f6; }
        
        .gpa-excellent { color: #059669; font-weight: 600; }
        .gpa-good { color: #2563eb; font-weight: 600; }
        .gpa-fair { color: #d97706; font-weight: 600; }
        .gpa-poor { color: #dc2626; font-weight: 600; }
        
        .failed-highlight { color: #dc2626; font-weight: bold; background-color: #fee2e2; }
        
        .footer { 
            margin-top: 40px; 
            padding-top: 15px; 
            border-top: 2px solid #e5e7eb; 
            font-size: 9px; 
            color: #6b7280; 
        }
        
        .signature-section { margin-top: 35px; display: table; width: 100%; }
        .signature-block { display: table-cell; width: 33%; text-align: center; padding: 15px; }
        .signature-line { 
            border-top: 2px solid #000; 
            margin-top: 45px; 
            padding-top: 8px; 
            font-size: 10px;
            font-weight: 600;
        }
        .signature-title { font-size: 9px; color: #6b7280; margin-top: 3px; }
        
        .page-break { page-break-after: always; }
        .no-break { page-break-inside: avoid; }
        
        .badge { 
            display: inline-block; 
            padding: 3px 8px; 
            border-radius: 12px; 
            font-size: 9px; 
            font-weight: 600;
        }
        .badge-proceed { background-color: #d1fae5; color: #065f46; }
        .badge-repeat { background-color: #fef3c7; color: #92400e; }
        .badge-probation { background-color: #fed7aa; color: #9a3412; }
        .badge-exclude { background-color: #fecaca; color: #991b1b; }
        .badge-pending { background-color: #e5e7eb; color: #374151; }
        
        @media print { 
            body { margin: 0; } 
            .page-break { page-break-after: always; }
            .no-break { page-break-inside: avoid; }
        }
    </style>
</head>
<body>");

            // Header
            sb.Append(@"
    <div class='header'>
        <h1>EDEN UNIVERSITY</h1>
        <h2>SENATE ACADEMIC PROGRESS REPORT</h2>
        <p>Office of the Registrar - Academic Affairs Division</p>
    </div>");

            // Meta Information
            sb.Append("<div class='meta-info'><table>");
            sb.Append("<tr>");
            sb.Append($"<td>Report Level:</td><td><strong>{filters.ReportLevel}</strong></td>");
            sb.Append($"<td>Academic Year:</td><td><strong>{academicYearName}</strong></td>");
            sb.Append("</tr><tr>");

            if (filters.ReportLevel == "School" || filters.ReportLevel == "Department" || filters.ReportLevel == "Programme")
            {
                sb.Append($"<td>School:</td><td>{schoolName}</td>");
            }
            else
            {
                sb.Append("<td colspan='2'></td>");
            }

            sb.Append($"<td>Academic Period:</td><td><strong>{(filters.AcademicPeriod.HasValue ? $"Period {filters.AcademicPeriod}" : "All Academic Periods")}</strong></td>");
            sb.Append("</tr><tr>");

            if (filters.ReportLevel == "Department" || filters.ReportLevel == "Programme")
            {
                sb.Append($"<td>Department:</td><td>{departmentName}</td>");
            }
            else
            {
                sb.Append("<td colspan='2'></td>");
            }

            sb.Append($"<td>Year of Study:</td><td>{(filters.YearOfStudy.HasValue ? $"Year {filters.YearOfStudy}" : "All Years")}</td>");
            sb.Append("</tr><tr>");

            if (filters.ReportLevel == "Programme")
            {
                sb.Append($"<td>Programme:</td><td>{programmeName}</td>");
            }
            else
            {
                sb.Append("<td colspan='2'></td>");
            }

            sb.Append($"<td>Generated:</td><td><strong>{DateTime.Now:dddd, MMMM dd, yyyy 'at' HH:mm}</strong></td>");
            sb.Append("</tr>");
            sb.Append("</table></div>");

            // Summary Statistics
            var totalStudents = students.Count;
            var withResults = students.Count(s => s.HasPublishedResults);
            var withoutResults = totalStudents - withResults;
            var avgGPA = students.Where(s => s.HasPublishedResults).Any()
                ? students.Where(s => s.HasPublishedResults).Average(s => s.GPA)
                : 0;

            sb.Append($@"
    <div class='summary-stats'>
        <div class='stat-box'>
            <div class='label'>Total Students</div>
            <div class='value'>{totalStudents}</div>
        </div>
        <div class='stat-box'>
            <div class='label'>With Results</div>
            <div class='value'>{withResults}</div>
        </div>
        <div class='stat-box'>
            <div class='label'>Pending Results</div>
            <div class='value'>{withoutResults}</div>
        </div>
        <div class='stat-box'>
            <div class='label'>Average GPA</div>
            <div class='value'>{avgGPA:F2}</div>
        </div>
    </div>");

            // Performance Summary Table (if available)
            if (summary != null && summary.TotalStudents > 0)
            {
                sb.Append(@"
    <div class='no-break'>
        <h3 class='section-title'>📊 Performance Summary</h3>
        <table class='data-table'>
            <thead>
                <tr>
                    <th style='width: 40px; text-align: center;'>No.</th>
                    <th>Description</th>
                    <th style='width: 120px; text-align: right;'>Count</th>
                    <th style='width: 80px; text-align: right;'>Percentage</th>
                </tr>
            </thead>
            <tbody>");

                var perfRows = new[]
                {
                    new { No = 1, Desc = "Students with Clear Pass", Value = summary.StudentsWithClearPass },
                    new { No = 2, Desc = "Students with Supplementary Exams", Value = summary.StudentsWithSupplementaryExams },
                    new { No = 3, Desc = "Students with Proceed with Repeats", Value = summary.StudentsWithProceedWithRepeats },
                    new { No = 4, Desc = "Students with Repeat Semester", Value = summary.StudentsWithRepeatSemester },
                    new { No = 5, Desc = "Students with Deferred Exams", Value = summary.StudentsWithDeferredExams },
                    new { No = 6, Desc = "Students Excluded", Value = summary.StudentsExcluded },
                    new { No = 7, Desc = "Students Disqualified", Value = summary.StudentsDisqualified }
                };

                foreach (var row in perfRows)
                {
                    var percentage = summary.TotalStudents > 0 ? (decimal)row.Value / summary.TotalStudents * 100 : 0;
                    sb.Append($@"
                <tr>
                    <td style='text-align: center;'>{row.No}</td>
                    <td>{row.Desc}</td>
                    <td style='text-align: right;'><strong>{row.Value}</strong></td>
                    <td style='text-align: right;'>{percentage:F1}%</td>
                </tr>");
                }

                sb.Append($@"
                <tr style='background-color: #e5e7eb; font-weight: bold;'>
                    <td style='text-align: center;'>—</td>
                    <td>TOTAL STUDENTS</td>
                    <td style='text-align: right;'>{summary.TotalStudents}</td>
                    <td style='text-align: right;'>100%</td>
                </tr>");

                sb.Append("</tbody></table></div>");
            }

            // Student Performance Table
            sb.Append(@"
    <h3 class='section-title'>👨‍🎓 Student Performance Details</h3>
    <table class='data-table'>
        <thead>
            <tr>
                <th style='width: 35px;'>#</th>
                <th style='width: 110px;'>Student Number</th>
                <th>Student Name</th>
                <th style='width: 70px; text-align: center;'>Total<br>Courses</th>
                <th style='width: 70px; text-align: center;'>Failed<br>Courses</th>
                <th style='width: 60px; text-align: center;'>GPA</th>
                <th style='width: 130px;'>Progression Status</th>
            </tr>
        </thead>
        <tbody>");

            int studentIndex = 1;
            foreach (var student in students)
            {
                var gpaClass = student.GPA >= (decimal)3.5 ? "gpa-excellent" :
                               student.GPA >= (decimal)3.0 ? "gpa-good" :
                               student.GPA >= (decimal)2.0 ? "gpa-fair" : "gpa-poor";

                var failedClass = student.FailedCourses > 0 ? "failed-highlight" : "";

                var progressionBadge = GetProgressionBadge(student.ProgressionRule);

                sb.Append($@"
                <tr>
                    <td style='text-align: center;'>{studentIndex++}</td>
                    <td>{student.StudentNumber}</td>
                    <td>{student.StudentName}</td>
                    <td style='text-align: center;'>{student.TotalCourses}</td>
                    <td style='text-align: center;' class='{failedClass}'>{student.FailedCourses}</td>
                    <td style='text-align: center;' class='{gpaClass}'>{(student.HasPublishedResults ? student.GPA.ToString("F2") : "—")}</td>
                    <td>{progressionBadge}</td>
                </tr>");
            }

            sb.Append("</tbody></table>");

            // Grading Overview (if available)
            if (gradingOverview != null && gradingOverview.Courses?.Any() == true)
            {
                sb.Append(@"
    <div class='page-break'></div>
    <h3 class='section-title'>📈 Grading Overview</h3>
    <p style='margin-bottom: 12px; color: #374151; font-size: 11px;'><strong>" + gradingOverview.ProgrammeName + "</strong> — " +
                    gradingOverview.AcademicYear + " — Semester " + gradingOverview.Semester + @"</p>
    <table class='data-table'>
        <thead>
            <tr>
                <th style='width: 80px;'>Course No</th>
                <th>Course Name</th>
                <th style='width: 40px; text-align: center; background-color: #7c3aed;'>A+</th>
                <th style='width: 40px; text-align: center; background-color: #7c3aed;'>A</th>
                <th style='width: 40px; text-align: center; background-color: #2563eb;'>B+</th>
                <th style='width: 40px; text-align: center; background-color: #2563eb;'>B</th>
                <th style='width: 40px; text-align: center; background-color: #059669;'>C+</th>
                <th style='width: 40px; text-align: center; background-color: #059669;'>C</th>
                <th style='width: 40px; text-align: center; background-color: #d97706;'>D+</th>
                <th style='width: 40px; text-align: center; background-color: #d97706;'>D</th>
                <th style='width: 40px; text-align: center; background-color: #dc2626;'>F</th>
                <th style='width: 70px; text-align: center;'>Pass<br>Rate</th>
            </tr>
        </thead>
        <tbody>");

                foreach (var course in gradingOverview.Courses)
                {
                    var grades = course.GradeDistribution;
                    var passRateColor = course.PassRate >= 80 ? "color: #059669;" :
                                       course.PassRate >= 60 ? "color: #2563eb;" :
                                       course.PassRate >= 40 ? "color: #d97706;" : "color: #dc2626;";

                    sb.Append($@"
                <tr>
                    <td><strong>{course.CourseNo}</strong></td>
                    <td style='font-size: 9px;'>{course.CourseName}</td>
                    <td style='text-align: center; background-color: #f3e8ff;'>{grades.GetValueOrDefault("A+", 0)}</td>
                    <td style='text-align: center; background-color: #f3e8ff;'>{grades.GetValueOrDefault("A", 0)}</td>
                    <td style='text-align: center; background-color: #dbeafe;'>{grades.GetValueOrDefault("B+", 0)}</td>
                    <td style='text-align: center; background-color: #dbeafe;'>{grades.GetValueOrDefault("B", 0)}</td>
                    <td style='text-align: center; background-color: #d1fae5;'>{grades.GetValueOrDefault("C+", 0)}</td>
                    <td style='text-align: center; background-color: #d1fae5;'>{grades.GetValueOrDefault("C", 0)}</td>
                    <td style='text-align: center; background-color: #fed7aa;'>{grades.GetValueOrDefault("D+", 0)}</td>
                    <td style='text-align: center; background-color: #fed7aa;'>{grades.GetValueOrDefault("D", 0)}</td>
                    <td style='text-align: center; background-color: #fecaca;'>{grades.GetValueOrDefault("F", 0)}</td>
                    <td style='text-align: center; font-weight: bold; {passRateColor}'>{course.PassRate:F1}%</td>
                </tr>");
                }

                sb.Append("</tbody></table>");
            }

            // Signature Section
            sb.Append(@"
    <div class='signature-section'>
        <div class='signature-block'>
            <div class='signature-line'>
                Prepared By
                <div class='signature-title'>Academic Registrar</div>
            </div>
        </div>
        <div class='signature-block'>
            <div class='signature-line'>
                Reviewed By
                <div class='signature-title'>Dean of School</div>
            </div>
        </div>
        <div class='signature-block'>
            <div class='signature-line'>
                Approved By
                <div class='signature-title'>Vice Chancellor</div>
            </div>
        </div>
    </div>");

            // Footer
            sb.Append($@"
    <div class='footer'>
        <table style='width: 100%;'>
            <tr>
                <td style='width: 70%;'>
                    <strong style='color: #1f2937;'>Eden University — Senate Academic Progress Report</strong><br>
                    Generated: {DateTime.Now:dddd, MMMM dd, yyyy 'at' HH:mm:ss}<br>
                    Report ID: SEN-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}
                </td>
                <td style='text-align: right; vertical-align: top;'>
                    <div style='background-color: #fee2e2; color: #991b1b; padding: 4px 8px; border-radius: 3px; font-weight: 600; font-size: 8px;'>
                        CONFIDENTIAL
                    </div>
                </td>
            </tr>
        </table>
        <p style='margin-top: 8px; font-size: 8px; color: #9ca3af;'>
            <em>This is a computer-generated document. This report contains sensitive academic information and should be handled in accordance with university privacy policies.</em>
        </p>
    </div>");

            sb.Append("</body></html>");

            return sb.ToString();
        }

        private string GetProgressionBadge(string progressionRule)
        {
            if (string.IsNullOrEmpty(progressionRule))
                return "<span class='badge badge-pending'>Results Pending</span>";

            return progressionRule switch
            {
                "Proceed" => "<span class='badge badge-proceed'>Proceed</span>",
                "ProceedWithRepeat" => "<span class='badge badge-repeat'>Proceed + Repeat</span>",
                "ProceedOnProbation" => "<span class='badge badge-probation'>Probation</span>",
                "RepeatYear" => "<span class='badge badge-exclude'>Repeat Year</span>",
                "RepeatSemester" => "<span class='badge badge-exclude'>Repeat Semester</span>",
                "Exclude" => "<span class='badge badge-exclude'>Academic Exclusion</span>",
                _ => $"<span class='badge badge-pending'>{progressionRule}</span>"
            };
        }

        private byte[] ConvertHtmlToPdf(string html)
        {
            using var memoryStream = new MemoryStream();
            HtmlConverter.ConvertToPdf(html, memoryStream);
            return memoryStream.ToArray();
        }

        #endregion

        #region Excel Generation Helper Methods

        private void CreateSummarySheet(
            XLWorkbook workbook,
            List<StudentProgressionData> students,
            PerformanceSummaryDto summary,
            SenateReportFilters filters)
        {
            var ws = workbook.Worksheets.Add("Summary");

            // Title section
            ws.Cell(1, 1).Value = "EDEN UNIVERSITY";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 18;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(1, 1, 1, 6).Merge();

            ws.Cell(2, 1).Value = "SENATE ACADEMIC PROGRESS REPORT";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 14;
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(2, 1, 2, 6).Merge();

            // Meta Information
            int row = 4;
            ws.Cell(row, 1).Value = "Report Level:";
            ws.Cell(row, 2).Value = filters.ReportLevel;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = "Academic Year:";
            ws.Cell(row, 5).Value = filters.AcademicYearId?.ToString() ?? "All Years";
            ws.Cell(row, 4).Style.Font.Bold = true;

            row++;
            ws.Cell(row, 1).Value = "Academic Period:";
            ws.Cell(row, 2).Value = filters.AcademicPeriod.HasValue ? $"Period {filters.AcademicPeriod}" : "All Academic Periods";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = "Year of Study:";
            ws.Cell(row, 5).Value = filters.YearOfStudy.HasValue ? $"Year {filters.YearOfStudy}" : "All Years";
            ws.Cell(row, 4).Style.Font.Bold = true;

            row++;
            ws.Cell(row, 1).Value = "Generated:";
            ws.Cell(row, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 1).Style.Font.Bold = true;

            // Summary Statistics
            row += 2;
            ws.Cell(row, 1).Value = "SUMMARY STATISTICS";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            ws.Range(row, 1, row, 6).Merge();

            row++;
            var totalStudents = students.Count;
            var withResults = students.Count(s => s.HasPublishedResults);
            var avgGPA = students.Where(s => s.HasPublishedResults).Any()
                ? students.Where(s => s.HasPublishedResults).Average(s => s.GPA)
                : 0;

            ws.Cell(row, 1).Value = "Metric";
            ws.Cell(row, 2).Value = "Value";
            ws.Cell(row, 3).Value = "Metric";
            ws.Cell(row, 4).Value = "Value";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightGray;

            row++;
            ws.Cell(row, 1).Value = "Total Students";
            ws.Cell(row, 2).Value = totalStudents;
            ws.Cell(row, 3).Value = "With Results";
            ws.Cell(row, 4).Value = withResults;

            row++;
            ws.Cell(row, 1).Value = "Without Results";
            ws.Cell(row, 2).Value = totalStudents - withResults;
            ws.Cell(row, 3).Value = "Average GPA";
            ws.Cell(row, 4).Value = avgGPA;
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";

            // Auto-fit columns
            ws.Columns().AdjustToContents();
        }

        private void CreateStudentPerformanceSheet(
            XLWorkbook workbook,
            List<StudentProgressionData> students,
            SenateReportFilters filters)
        {
            var ws = workbook.Worksheets.Add("Student Performance");

            // Title
            ws.Cell(1, 1).Value = "STUDENT PERFORMANCE DETAILS";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 7).Merge();

            // Headers
            int row = 3;
            var headers = new[] { "#", "Student Number", "Student Name", "Total Courses", "Failed Courses", "GPA", "Progression Rule" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
            }

            // Style header
            var headerRange = ws.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.DarkOrange;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Data
            int studentNumber = 1;
            foreach (var student in students)
            {
                row++;
                ws.Cell(row, 1).Value = studentNumber++;
                ws.Cell(row, 2).Value = student.StudentNumber;
                ws.Cell(row, 3).Value = student.StudentName;
                ws.Cell(row, 4).Value = student.TotalCourses;
                ws.Cell(row, 5).Value = student.FailedCourses;
                ws.Cell(row, 6).Value = student.HasPublishedResults ? student.GPA : 0;
                ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 7).Value = student.ProgressionRule ?? "Results Pending";

                // Highlight failed courses
                if (student.FailedCourses > 0)
                {
                    ws.Cell(row, 5).Style.Font.FontColor = XLColor.Red;
                    ws.Cell(row, 5).Style.Font.Bold = true;
                    ws.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.LightPink;
                }

                // Color-code GPA
                if (student.HasPublishedResults)
                {
                    if (student.GPA >= (decimal)3.5)
                        ws.Cell(row, 6).Style.Font.FontColor = XLColor.DarkGreen;
                    else if (student.GPA >= (decimal)3.0)
                        ws.Cell(row, 6).Style.Font.FontColor = XLColor.Blue;
                    else if (student.GPA >= (decimal)2.0)
                        ws.Cell(row, 6).Style.Font.FontColor = XLColor.Orange;
                    else
                        ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;
                }
            }

            // Auto-fit
            ws.Columns().AdjustToContents();

            // Borders
            var dataRange = ws.Range(3, 1, row, headers.Length);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private void CreateGradingOverviewSheet(XLWorkbook workbook, ProgrammeGradingOverview gradingOverview)
        {
            var ws = workbook.Worksheets.Add("Grading Overview");

            // Title
            ws.Cell(1, 1).Value = "GRADING OVERVIEW";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 13).Merge();

            ws.Cell(2, 1).Value = $"{gradingOverview.ProgrammeName} - {gradingOverview.AcademicYear} - Semester {gradingOverview.Semester}";
            ws.Cell(2, 1).Style.Font.FontSize = 11;
            ws.Range(2, 1, 2, 13).Merge();

            // Headers
            int row = 4;
            var headers = new[] { "Course No", "Course Name", "A+", "A", "B+", "B", "C+", "C", "D+", "D", "F", "Total Failed", "Pass Rate" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
            }

            // Style header
            var headerRange = ws.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.DarkGreen;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Data
            foreach (var course in gradingOverview.Courses)
            {
                row++;
                var grades = course.GradeDistribution;

                ws.Cell(row, 1).Value = course.CourseNo;
                ws.Cell(row, 2).Value = course.CourseName;
                ws.Cell(row, 3).Value = grades.GetValueOrDefault("A+", 0);
                ws.Cell(row, 4).Value = grades.GetValueOrDefault("A", 0);
                ws.Cell(row, 5).Value = grades.GetValueOrDefault("B+", 0);
                ws.Cell(row, 6).Value = grades.GetValueOrDefault("B", 0);
                ws.Cell(row, 7).Value = grades.GetValueOrDefault("C+", 0);
                ws.Cell(row, 8).Value = grades.GetValueOrDefault("C", 0);
                ws.Cell(row, 9).Value = grades.GetValueOrDefault("D+", 0);
                ws.Cell(row, 10).Value = grades.GetValueOrDefault("D", 0);
                ws.Cell(row, 11).Value = grades.GetValueOrDefault("F", 0);
                ws.Cell(row, 12).Value = course.TotalFailed;
                ws.Cell(row, 13).Value = course.PassRate / 100;
                ws.Cell(row, 13).Style.NumberFormat.Format = "0.0%";

                // Color-code pass rate
                if (course.PassRate >= 80)
                    ws.Cell(row, 13).Style.Font.FontColor = XLColor.DarkGreen;
                else if (course.PassRate >= 60)
                    ws.Cell(row, 13).Style.Font.FontColor = XLColor.Blue;
                else if (course.PassRate >= 40)
                    ws.Cell(row, 13).Style.Font.FontColor = XLColor.Orange;
                else
                    ws.Cell(row, 13).Style.Font.FontColor = XLColor.Red;
            }

            // Auto-fit
            ws.Columns().AdjustToContents();

            // Borders
            var dataRange = ws.Range(4, 1, row, headers.Length);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private void CreatePerformanceSummarySheet(XLWorkbook workbook, PerformanceSummaryDto summary)
        {
            var ws = workbook.Worksheets.Add("Performance Summary");

            // Title
            ws.Cell(1, 1).Value = "PERFORMANCE SUMMARY";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 4).Merge();

            // Headers
            int row = 3;
            ws.Cell(row, 1).Value = "No.";
            ws.Cell(row, 2).Value = "Description";
            ws.Cell(row, 3).Value = "Count";
            ws.Cell(row, 4).Value = "Percentage";

            // Style header
            var headerRange = ws.Range(row, 1, row, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            headerRange.Style.Font.FontColor = XLColor.White;

            // Data
            var perfData = new[]
            {
                new { No = 1, Desc = "Students with Clear Pass", Value = summary.StudentsWithClearPass },
                new { No = 2, Desc = "Students with Supplementary Exams", Value = summary.StudentsWithSupplementaryExams },
                new { No = 3, Desc = "Students with Proceed with Repeats", Value = summary.StudentsWithProceedWithRepeats },
                new { No = 4, Desc = "Students with Repeat Semester", Value = summary.StudentsWithRepeatSemester },
                new { No = 5, Desc = "Students with Deferred Exams", Value = summary.StudentsWithDeferredExams },
                new { No = 6, Desc = "Students Excluded", Value = summary.StudentsExcluded },
                new { No = 7, Desc = "Students Disqualified", Value = summary.StudentsDisqualified }
            };

            foreach (var item in perfData)
            {
                row++;
                var percentage = summary.TotalStudents > 0 ? (decimal)item.Value / summary.TotalStudents * 100 : 0;

                ws.Cell(row, 1).Value = item.No;
                ws.Cell(row, 2).Value = item.Desc;
                ws.Cell(row, 3).Value = item.Value;
                ws.Cell(row, 4).Value = percentage / 100;
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
            }

            // Total row
            row++;
            ws.Cell(row, 1).Value = "—";
            ws.Cell(row, 2).Value = "TOTAL STUDENTS";
            ws.Cell(row, 3).Value = summary.TotalStudents;
            ws.Cell(row, 4).Value = 1.0;
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";

            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightGray;

            // Auto-fit
            ws.Columns().AdjustToContents();

            // Borders
            var dataRange = ws.Range(3, 1, row, 4);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        #endregion

        /*[HttpPost]
        public async Task<IActionResult> ExportToPdf([FromBody] SenateReportFilters filters)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                var report = await _senateReportService.GenerateReportAsync(filters);

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
                return Json(new { success = false, message = "Failed to generate PDF export." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportToExcel([FromBody] SenateReportFilters filters)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                var report = await _senateReportService.GenerateReportAsync(filters);

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
                return Json(new { success = false, message = "Failed to generate Excel export." });
            }
        }*/

        private async Task<SenateReportFilters> ApplyRoleBasedFilters(SenateReportFilters filters, ApplicationUser currentUser)
        {
            var (isAdmin, isDean, isHOD, isVC, isDVC, isRegistrar) = GetUserRoles();

            // VC, DVC, Admin, Registrar: Full access - no filtering
            if (isAdmin || isRegistrar || isVC || isDVC)
            {
                return filters;
            }

            // Dean: School-level access
            if (isDean)
            {
                var userSchoolId = await _context.Schools
                    .AsNoTracking()
                    .Where(s => s.DeanId == currentUser.Id || s.AssistantDeanId == currentUser.Id)
                    .Select(s => s.Id)
                    .FirstOrDefaultAsync();

                if (userSchoolId > 0)
                {
                    if (!filters.SchoolId.HasValue || filters.SchoolId.Value != userSchoolId)
                    {
                        filters.SchoolId = userSchoolId;
                    }
                }
                else
                {
                    filters.SchoolId = -1;
                }
            }
            // HOD: Programme-level access
            else if (isHOD)
            {
                var hodDepartment = await _context.Departments
                    .AsNoTracking()
                    .Where(d => d.HODId == currentUser.Id)
                    .Select(d => new { d.Id, d.SchoolId })
                    .FirstOrDefaultAsync();

                if (hodDepartment != null)
                {
                    filters.DepartmentId = hodDepartment.Id;
                    filters.SchoolId = hodDepartment.SchoolId;

                    if (filters.ProgrammeId.HasValue)
                    {
                        var programmeExists = await _context.Programmes
                            .AsNoTracking()
                            .AnyAsync(p => p.Id == filters.ProgrammeId.Value &&
                                          p.DepartmentId == hodDepartment.Id);

                        if (!programmeExists)
                        {
                            filters.ProgrammeId = await _context.Programmes
                                .AsNoTracking()
                                .Where(p => p.DepartmentId == hodDepartment.Id)
                                .Select(p => (int?)p.Id)
                                .FirstOrDefaultAsync();
                        }
                    }
                }
                else
                {
                    filters.DepartmentId = -1;
                }
            }

            return filters;
        }

        [HttpGet]
        public async Task<IActionResult> GetEntityStudentDetails(
            int entityId,
            string entityType,
            [FromQuery] SenateReportFilters filters)
        {
            try
            {
                if (entityId <= 0 || string.IsNullOrEmpty(entityType))
                {
                    return BadRequest(new { success = false, message = "Invalid parameters" });
                }

                _logger.LogInformation($"Getting student details for {entityType} {entityId}");

                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

                // Use separate context for GetEntityNameAsync to allow parallel execution
                await using var separateContext = await _contextFactory.CreateDbContextAsync();

                var studentsTask = _senateReportService.GetEntityStudentDetailsAsync(entityId, entityType, filters);
                var entityNameTask = GetEntityNameAsync(entityId, entityType, separateContext);

                await Task.WhenAll(studentsTask, entityNameTask);

                var students = studentsTask.Result;
                var entityName = entityNameTask.Result;

                // Calculate statistics in memory (already loaded data)
                var studentsWithResults = students.Where(s => s.HasPublishedResults).ToList();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        entityId,
                        entityType,
                        entityName,
                        students,
                        totalStudents = students.Count,
                        studentsWithResults = studentsWithResults.Count,
                        studentsWithoutResults = students.Count - studentsWithResults.Count,
                        averageGPA = studentsWithResults.Any()
                            ? studentsWithResults.Average(s => s.GPA)
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
        public async Task<IActionResult> GetStudentProgressionDetail(
            int studentId,
            [FromQuery] SenateReportFilters filters)
        {
            try
            {
                if (studentId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid student ID" });
                }

                _logger.LogInformation($"Getting progression detail for student {studentId}");

                var currentUser = await _userManager.GetUserAsync(User);
                filters = await ApplyRoleBasedFilters(filters, currentUser);

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

        // Overload that uses the injected context (for non-parallel calls)
        private async Task<string> GetEntityNameAsync(int entityId, string entityType)
        {
            return await GetEntityNameAsync(entityId, entityType, _context);
        }

        // New overload that accepts a context parameter (for parallel calls)
        private async Task<string> GetEntityNameAsync(int entityId, string entityType, ApplicationDbContext context)
        {
            return entityType.ToLower() switch
            {
                "school" => await context.Schools
                    .AsNoTracking()
                    .Where(s => s.Id == entityId)
                    .Select(s => s.Name ?? "Unknown School")
                    .FirstOrDefaultAsync() ?? "Unknown School",

                "department" => await context.Departments
                    .AsNoTracking()
                    .Where(d => d.Id == entityId)
                    .Select(d => d.Name + " (" + (d.School.Name ?? "") + ")")
                    .FirstOrDefaultAsync() ?? "Unknown Department",

                "programme" => await context.Programmes
                    .AsNoTracking()
                    .Where(p => p.Id == entityId)
                    .Select(p => p.Name + " (" + (p.Department.Name ?? "") + ")")
                    .FirstOrDefaultAsync() ?? "Unknown Programme",

                _ => "Unknown"
            };
        }

        private SenateReportFilters CreateDrillDownFilters(
            SenateReportFilters baseFilters,
            string targetLevel,
            int entityId)
        {
            var drillFilters = new SenateReportFilters
            {
                AcademicYearId = baseFilters.AcademicYearId,
                ModeOfStudyId = baseFilters.ModeOfStudyId,
                YearOfStudy = baseFilters.YearOfStudy,
                AcademicPeriod = baseFilters.AcademicPeriod,
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

        [HttpGet]
        public async Task<IActionResult> GetProgrammeBatches(
            int programmeId,
            int academicYearId,
            int semester)
        {
            try
            {
                _logger.LogInformation($"Getting batches for programme {programmeId}");

                if (!await CanAccessProgrammeAsync(programmeId))
                {
                    return Json(new { success = false, message = "Access denied to this programme." });
                }

                var batches = await _senateReportService.GetPendingBatchesForProgrammeAsync(
                    programmeId, academicYearId, semester);

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

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated." });
                }

                if (!await CanPublishBatchesAsync(request.BatchIds))
                {
                    return Json(new { success = false, message = "Access denied to publish these batches." });
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

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated." });
                }

                if (!await CanAccessProgrammeAsync(request.ProgrammeId))
                {
                    return Json(new { success = false, message = "Access denied to this programme." });
                }

                var result = await _senateReportService.PublishAllProgrammeBatchesAsync(
                    request.ProgrammeId, request.AcademicYearId, request.Semester, userId);

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
                if (yearOfStudy == 0) yearOfStudy = 1;

                if (!await CanAccessProgrammeAsync(programmeId))
                {
                    return Json(new { success = false, message = "You do not have permission to view this programme" });
                }

                var overview = await _senateReportService.GetProgrammeGradingOverviewAsync(
                    programmeId, academicYearId, semester, yearOfStudy);

                return Json(new { success = true, data = overview });
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
            try
            {
                // OPTIMIZED: Single query to get RSB with Course info
                var rsbWithCourse = await _context.ResultSubmissionBatches
                    .AsNoTracking()
                    .Where(r => r.Id == rsbId)
                    .Select(r => new
                    {
                        r.CourseId,
                        CourseCode = r.Course.CourseCode,
                        CourseName = r.Course.CourseName,
                        PassMark = r.Course.PassMark
                    })
                    .FirstOrDefaultAsync();

                if (rsbWithCourse == null)
                {
                    return Json(new { success = false, message = "Result submission batch not found" });
                }

                var academicYear = await _context.AcademicYears
                    .AsNoTracking()
                    .Where(a => a.YearId == academicYearId)
                    .Select(a => a.YearValue)
                    .FirstOrDefaultAsync();

                if (academicYear == null)
                {
                    return Json(new { success = false, message = "Academic year not found" });
                }

                int courseId = rsbWithCourse.CourseId;
                double passMark = rsbWithCourse.PassMark;

                // Use DbContextFactory for parallel operations to avoid "second operation started" error
                await using var context1 = await _contextFactory.CreateDbContextAsync();
                await using var context2 = await _contextFactory.CreateDbContextAsync();
                await using var context3 = await _contextFactory.CreateDbContextAsync();

                var gradeConfigsTask = GetCachedGradeConfigurationsAsync();

                // Get enrolled students with their scores in a single optimized query
                var studentScoresTask = context1.StudentExaminableCourses
                    .AsNoTracking()
                    .Where(sec =>
                        sec.CourseId == courseId &&
                        sec.AcademicYearId == academicYearId &&
                        sec.YearPeriodId == semester)
                    .Select(sec => sec.StudentId)
                    .Distinct()
                    .GroupJoin(
                        context1.StudentAssessmentScores
                            .AsNoTracking()
                            .Where(s =>
                                s.CourseId == courseId &&
                                s.AcademicYearId == academicYearId &&
                                s.YearPeriodId == semester &&
                                s.IsActive &&
                                context1.ResultSubmissionBatches
                                    .Any(rsb => rsb.Id == s.rsbId &&
                                               rsb.ApprovalStatus == WorkflowStatus.Approved)),
                        studentId => studentId,
                        score => score.StudentId,
                        (studentId, scores) => new
                        {
                            StudentId = studentId,
                            TotalScore = scores.Sum(s => s.Score),
                            HasScores = scores.Any()
                        })
                    .ToListAsync();

                // Check if any published batch exists
                var hasPublishedBatchTask = context2.ResultSubmissionBatches
                    .AsNoTracking()
                    .AnyAsync(rsb =>
                        rsb.CourseId == courseId &&
                        rsb.AcademicYearId == academicYearId &&
                        rsb.YearPeriodId == semester &&
                        rsb.ApprovalStatus == WorkflowStatus.Approved);

                await Task.WhenAll(gradeConfigsTask, studentScoresTask, hasPublishedBatchTask);

                var gradeConfigs = gradeConfigsTask.Result;
                var studentScores = studentScoresTask.Result;
                var hasPublishedBatch = hasPublishedBatchTask.Result;

                if (!studentScores.Any())
                {
                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            courseCode = rsbWithCourse.CourseCode,
                            courseName = rsbWithCourse.CourseName,
                            academicYear,
                            semester,
                            gradeDistribution = CreateEmptyGradeDistribution(),
                            totalPassed = 0,
                            totalFailed = 0,
                            passRate = 0
                        }
                    });
                }

                // Calculate grade distribution in memory
                var gradeDistribution = CreateEmptyGradeDistribution();
                int totalPassed = 0;
                int totalFailed = 0;

                foreach (var student in studentScores)
                {
                    if (!hasPublishedBatch || !student.HasScores)
                    {
                        gradeDistribution["NE/INC"]++;
                        continue;
                    }

                    decimal totalScore = Math.Min(student.TotalScore, 100);
                    var gradeConfig = gradeConfigs.FirstOrDefault(g => totalScore >= (decimal)g.MinScore);

                    if (gradeConfig != null)
                    {
                        string gradeLetter = gradeConfig.GradeLetter;
                        if (gradeDistribution.ContainsKey(gradeLetter))
                            gradeDistribution[gradeLetter]++;
                        else
                            gradeDistribution["F"]++;

                        if (totalScore >= (decimal)passMark)
                            totalPassed++;
                        else
                            totalFailed++;
                    }
                    else
                    {
                        gradeDistribution["F"]++;
                        totalFailed++;
                    }
                }

                int totalStudents = totalPassed + totalFailed;
                decimal passRate = totalStudents > 0
                    ? Math.Round((decimal)totalPassed / totalStudents * 100, 0)
                    : 0;

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        courseCode = rsbWithCourse.CourseCode,
                        courseName = rsbWithCourse.CourseName,
                        academicYear,
                        semester,
                        gradeDistribution,
                        totalPassed,
                        totalFailed,
                        passRate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting course grading overview for RSB {rsbId}");
                return Json(new
                {
                    success = false,
                    message = "An error occurred while loading the grading overview"
                });
            }
        }

        // ===================================================================
        // HELPER METHODS
        // ===================================================================

        private (bool isAdmin, bool isDean, bool isHOD, bool isVC, bool isDVC, bool isRegistrar) GetUserRoles()
        {
            return (
                User.IsInRole("Admin"),
                User.IsInRole("Dean"),
                User.IsInRole("HOD"),
                User.IsInRole("VC"),
                User.IsInRole("DVC"),
                User.IsInRole("Registrar")
            );
        }

        private async Task<List<GradeConfiguration>> GetCachedGradeConfigurationsAsync()
        {
            return await _cache.GetOrCreateAsync(GRADE_CONFIGS_CACHE_KEY, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                // Use a separate context for cache population to avoid conflicts
                await using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GradeConfigurations
                    .AsNoTracking()
                    .Where(g => g.IsActive)
                    .OrderByDescending(g => g.MinScore)
                    .ToListAsync();
            });
        }

        private static Dictionary<string, int> CreateEmptyGradeDistribution()
        {
            return new Dictionary<string, int>
            {
                { "A+", 0 }, { "A", 0 }, { "B+", 0 }, { "B", 0 },
                { "C+", 0 }, { "C", 0 }, { "D+", 0 }, { "D", 0 },
                { "EXP", 0 }, { "NE/INC", 0 }, { "DEF", 0 },
                { "P", 0 }, { "F", 0 }
            };
        }

        // ===================================================================
        // ROLE-BASED ACCESS CONTROL METHODS (OPTIMIZED)
        // ===================================================================

        private async Task<bool> CanAccessSchoolAsync(int schoolId)
        {
            var (isAdmin, isDean, isHOD, isVC, isDVC, isRegistrar) = GetUserRoles();

            if (isAdmin || isRegistrar || isVC || isDVC)
                return true;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (isDean)
            {
                return await _context.Schools
                    .AsNoTracking()
                    .AnyAsync(s => s.Id == schoolId &&
                                  (s.DeanId == userId || s.AssistantDeanId == userId));
            }

            if (isHOD)
            {
                return await _context.Departments
                    .AsNoTracking()
                    .AnyAsync(d => d.SchoolId == schoolId && d.HODId == userId);
            }

            return false;
        }

        private async Task<bool> CanAccessDepartmentAsync(int departmentId)
        {
            var (isAdmin, isDean, isHOD, isVC, isDVC, isRegistrar) = GetUserRoles();

            if (isAdmin || isRegistrar || isVC || isDVC)
                return true;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (isDean)
            {
                return await _context.Departments
                    .AsNoTracking()
                    .AnyAsync(d => d.Id == departmentId &&
                                  (d.School.DeanId == userId || d.School.AssistantDeanId == userId));
            }

            if (isHOD)
            {
                return await _context.Departments
                    .AsNoTracking()
                    .AnyAsync(d => d.Id == departmentId && d.HODId == userId);
            }

            return false;
        }

        private async Task<bool> CanAccessProgrammeAsync(int programmeId)
        {
            var (isAdmin, isDean, isHOD, isVC, isDVC, isRegistrar) = GetUserRoles();

            if (isAdmin || isRegistrar || isVC || isDVC)
                return true;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Single optimized query to check access
            var programmeAccess = await _context.Programmes
                .AsNoTracking()
                .Where(p => p.Id == programmeId)
                .Select(p => new
                {
                    DepartmentHODId = p.Department.HODId,
                    SchoolDeanId = p.Department.School.DeanId,
                    SchoolAssistantDeanId = p.Department.School.AssistantDeanId
                })
                .FirstOrDefaultAsync();

            if (programmeAccess == null)
                return false;

            if (isDean)
            {
                return programmeAccess.SchoolDeanId == userId ||
                       programmeAccess.SchoolAssistantDeanId == userId;
            }

            if (isHOD)
            {
                return programmeAccess.DepartmentHODId == userId;
            }

            return false;
        }

        private async Task<bool> CanPublishBatchesAsync(List<int> batchIds)
        {
            var (isAdmin, isDean, isHOD, isVC, isDVC, isRegistrar) = GetUserRoles();

            if (isAdmin || isRegistrar || isVC || isDVC)
                return true;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Single optimized query to check all batches
            var batchAccess = await _context.ResultSubmissionBatches
                .AsNoTracking()
                .Where(b => batchIds.Contains(b.Id))
                .Select(b => new
                {
                    b.Id,
                    DepartmentHODId = b.Course.Programme.Department.HODId,
                    SchoolDeanId = b.Course.Programme.Department.School.DeanId,
                    SchoolAssistantDeanId = b.Course.Programme.Department.School.AssistantDeanId
                })
                .ToListAsync();

            if (!batchAccess.Any() || batchAccess.Count != batchIds.Count)
                return false;

            if (isDean)
            {
                return batchAccess.All(b =>
                    b.SchoolDeanId == userId || b.SchoolAssistantDeanId == userId);
            }

            if (isHOD)
            {
                return batchAccess.All(b => b.DepartmentHODId == userId);
            }

            return false;
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
