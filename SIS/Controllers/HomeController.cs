
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Results;
using System.Diagnostics;
using System.Text.Json;
using SIS.Services.Emails;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using SIS.Services.PDF;
using SIS.Services; // Add this for IInstitutionConfigService

namespace SIS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IPdfInvoiceService _pdfService;
        private readonly IHtmlPdfService _htmlPdfService;
        private readonly IInstitutionConfigService _institutionConfig;
        private readonly IWebHostEnvironment _webHostEnvironment;

        [ActivatorUtilitiesConstructor]
        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IPdfInvoiceService pdfService,
            IHtmlPdfService htmlPdfService,
            IInstitutionConfigService institutionConfig,
            IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _pdfService = pdfService;
            _htmlPdfService = htmlPdfService;
            _institutionConfig = institutionConfig;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {

            // Stats for the top cards
            var userCount = await _context.Users.CountAsync();
            var schoolCount = await _context.Schools.CountAsync();
            var programmeCount = await _context.Programmes.CountAsync();
            var departmentCount = await _context.Departments.CountAsync();

            // Fetch student statistics
            var totalStudents = await _context.Students.CountAsync();
            var newAdmissions = await _context.Students
                .Where(s => s.AdmissionDate != null && s.AdmissionDate.Value.AddMonths(3) > DateTime.Now)
                .CountAsync();
            var graduatedStudents = await _context.Students
                .Where(s => s.StudentStatus == Status.Completed)
                .CountAsync();
            var activeStudents = await _context.Students
                .Where(s => s.IsRegistered)
                .CountAsync();

            // Calculate course completion percentage - this is an example
            var courseCompletionPercentage = 85; // You may need to calculate this based on your business logic

            // Get top 5 schools by enrollment count
            var topSchools = await _context.Schools
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    EnrolledCount = _context.Students.Count(st => st.SchoolId == s.Id),
                    ApplicationsCount = s.Applicants.Count,
                    AdmittedCount = _context.Students.Count(st => st.SchoolId == s.Id && st.IsAdmitted)
                })
                .OrderByDescending(s => s.EnrolledCount)
                //.Take(6)
                .ToListAsync();

            var schoolNames = topSchools.Select(s => s.Name).ToList();
            var enrolledStudents = topSchools.Select(s => s.EnrolledCount).ToList();
            var applicationsReceived = topSchools.Select(s => s.ApplicationsCount).ToList();
            var admittedStudents = topSchools.Select(s => s.AdmittedCount).ToList();

            // For line chart - grade performance trends (sample months)
            var months = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul" };

            // This is just an example - you would need to implement actual grade analytics
            // based on your CourseGrades model
            var highGrades = new List<int> { 0, 0, 0, 0, 0, 0 };
            var lowGrades = new List<int> { 0, 0, 0, 0, 0, 0 };

            // Set ViewBag data for the charts and stats cards
            ViewBag.UserCount = userCount;
            ViewBag.SchoolCount = schoolCount;
            ViewBag.ProgrammeCount = programmeCount;
            ViewBag.DepartmentCount = departmentCount;

            ViewBag.TotalStudents = totalStudents;
            ViewBag.NewAdmissions = newAdmissions;
            ViewBag.GraduatedStudents = graduatedStudents;
            ViewBag.ActiveStudents = activeStudents;
            ViewBag.CourseCompletionPercentage = courseCompletionPercentage;

            // Bar chart data
            ViewBag.Faculties = Newtonsoft.Json.JsonConvert.SerializeObject(schoolNames);
            ViewBag.EnrolledStudents = Newtonsoft.Json.JsonConvert.SerializeObject(enrolledStudents);
            ViewBag.ApplicationsReceived = Newtonsoft.Json.JsonConvert.SerializeObject(applicationsReceived);
            ViewBag.AdmittedStudents = Newtonsoft.Json.JsonConvert.SerializeObject(admittedStudents);

            // Line chart data
            ViewBag.Months = Newtonsoft.Json.JsonConvert.SerializeObject(months);
            ViewBag.HighGrades = Newtonsoft.Json.JsonConvert.SerializeObject(highGrades);
            ViewBag.LowGrades = Newtonsoft.Json.JsonConvert.SerializeObject(lowGrades);

            // Donut chart data
            var donutChartData = new int[] { totalStudents, newAdmissions, graduatedStudents, activeStudents };
            ViewBag.DonutChartData = Newtonsoft.Json.JsonConvert.SerializeObject(donutChartData);

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Authorize]
        public async Task<IActionResult> Student_Dashboard()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found in Student_Dashboard");
                    return RedirectToAction("Login", "Account");
                }

                var student = await _context.Students
                    .Include(s => s.School)
                    .Include(s => s.Programme)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                decimal outstandindFees = StudentTools.GetStudentOutstandingBalance(student.Id);

                if (student.OutstandingFees != outstandindFees)
                {
                    student.OutstandingFees = outstandindFees;
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }

                if (student == null)
                {
                    _logger.LogWarning($"Student not found for user: {user.UserName}");
                    return NotFound("Student record not found");
                }

                var performanceViewModel = new StudentPerformanceViewModel
                {
                    Courses = new List<CoursePerformanceViewModel>(),
                    YearGPA = 0,
                    OverallGPA = 0,
                    TotalFailedCourses = 0
                };

                // Only proceed with performance data if student is registered
                if (student.IsRegistered) 
                {
                    try
                    {
                        decimal outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
                        // Check if student can view complete results (no outstanding fees)
                        bool canViewCompleteResults = outstandingBalance <= 0;

                        // ? UPDATED: Get current courses from StudentCourseResults table
                        var currentCourseResults = await _context.StudentCourseResults
                            .Include(r => r.Course)
                                .ThenInclude(c => c.CourseAssessments)
                                    .ThenInclude(ca => ca.Assessment)
                            .Where(r =>
                                r.StudentId == student.Id &&
                                r.AcademicYearId == student.AcademicYearId)
                            .ToListAsync();

                        // ? UPDATED: Manually load assessment scores for each result
                        foreach (var result in currentCourseResults)
                        {
                            result.AssessmentScores = await _context.StudentAssessmentScores
                                .Include(s => s.Assessment)
                                .Where(s =>
                                    s.StudentId == result.StudentId &&
                                    s.CourseId == result.CourseId &&
                                    s.AcademicYearId == result.AcademicYearId &&
                                    s.Semester == result.Semester &&
                                    s.IsActive)
                                .ToListAsync();
                        }

                        // ? UPDATED: Process each course result
                        foreach (var result in currentCourseResults)
                        {
                            try
                            {
                                var coursePerformance = new CoursePerformanceViewModel
                                {
                                    CourseId = result.CourseId,
                                    CourseCode = result.Course?.CourseCode ?? "N/A",
                                    CourseName = result.Course?.CourseName ?? "N/A",
                                    Status = result.Status.ToString(),
                                    Scores = new Dictionary<string, Models.Results.AssessmentScore>(),
                                    TotalScore = 0
                                };

                                // ? UPDATED: Check if this course can show complete results
                                bool courseCanViewCompleteResults = (result.Status == Status.Published) && canViewCompleteResults;

                                // ? UPDATED: Process assessment scores from navigation property
                                if (result.AssessmentScores != null && result.AssessmentScores.Any())
                                {
                                    decimal totalWeightedScore = 0;
                                    decimal totalWeight = 0;

                                    foreach (var assessmentScore in result.AssessmentScores.Where(s => s.IsActive))
                                    {
                                        try
                                        {
                                            var assessmentName = assessmentScore.Assessment?.Name ?? "Unknown";
                                            var score = assessmentScore.Score;
                                            var weight = assessmentScore.WeightPercentage;

                                            // ? UPDATED: Apply restrictions - only include exam scores if results are published AND student can view complete results
                                            bool isExam = IsExamAssessment(assessmentName);
                                            bool includeScore = true;

                                            if (isExam)
                                            {
                                                // Only include exam score if results are published AND student has no outstanding fees
                                                includeScore = courseCanViewCompleteResults;
                                            }
                                            else
                                            {
                                                // Continuous assessments visible if published (regardless of fees)
                                                includeScore = result.Status == Status.Published;
                                            }

                                            if (includeScore)
                                            {
                                                coursePerformance.Scores[assessmentName] = new Models.Results.AssessmentScore
                                                {
                                                    AssessmentName = assessmentName,
                                                    Score = score
                                                };

                                                // Calculate weighted score for total
                                                totalWeightedScore += (score * weight / 100);
                                                totalWeight += weight;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError($"Error processing assessment score for course {result.Course?.CourseCode}: {ex.Message}");
                                            continue;
                                        }
                                    }

                                    // ? UPDATED: Use calculated total from StudentCourseResults if available and accessible
                                    if (courseCanViewCompleteResults && result.NormalizedTotal > 0)
                                    {
                                        // Use the pre-calculated normalized total from the database
                                        coursePerformance.TotalScore = result.NormalizedTotal;
                                    }
                                    else if (totalWeight > 0)
                                    {
                                        // Calculate partial total (continuous assessments only) with normalization
                                        coursePerformance.TotalScore = Math.Min((totalWeightedScore / totalWeight) * 100, 100);
                                    }
                                }
                                else if (courseCanViewCompleteResults && result.NormalizedTotal > 0)
                                {
                                    // ? UPDATED: Even if no assessment scores loaded, use the result's total if accessible
                                    coursePerformance.TotalScore = result.NormalizedTotal;
                                }

                                performanceViewModel.Courses.Add(coursePerformance);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error processing course performance for {result.Course?.CourseCode}: {ex.Message}");

                                // Add course with empty scores if there's an error
                                performanceViewModel.Courses.Add(new CoursePerformanceViewModel
                                {
                                    CourseId = result.CourseId,
                                    CourseCode = result.Course?.CourseCode ?? "N/A",
                                    CourseName = result.Course?.CourseName ?? "N/A",
                                    Status = result.Status.ToString(),
                                    Scores = new Dictionary<string, Models.Results.AssessmentScore>(),
                                    TotalScore = 0
                                });
                            }
                        }

                        // ? UPDATED: Calculate GPA from StudentCourseResults
                        if (currentCourseResults.Any(r => r.Status == Status.Published && canViewCompleteResults))
                        {
                            var publishedResults = currentCourseResults
                                .Where(r => r.Status == Status.Published)
                                .ToList();

                            if (publishedResults.Any())
                            {
                                decimal totalGradePoints = 0;
                                int totalCredits = 0;

                                foreach (var result in publishedResults)
                                {
                                    totalGradePoints += result.GradePoints * result.Credits;
                                    totalCredits += result.Credits;
                                }

                                if (totalCredits > 0)
                                {
                                    performanceViewModel.YearGPA = Math.Round(totalGradePoints / totalCredits, 2);
                                }

                                // Count failed courses
                                performanceViewModel.TotalFailedCourses = publishedResults.Count(r => !r.IsPassed);
                            }
                        }

                        // Get historical GPA data from academic performance archives (if available)
                        try
                        {
                            var academicPerformanceRecords = await _context.StudentAcademicPerformanceArchives
                                .Where(a => a.StudentNumber == student.StudentId_Number)
                                .OrderByDescending(a => a.CreatedAt)
                                .ToListAsync();

                            if (academicPerformanceRecords.Any())
                            {
                                // Use the most recent overall GPA from archives
                                var latestPerformance = academicPerformanceRecords.First();
                                performanceViewModel.OverallGPA = latestPerformance.GPA;

                                // If current year GPA is 0, use archived GPA
                                if (performanceViewModel.YearGPA == 0 && academicPerformanceRecords.Any())
                                {
                                    performanceViewModel.YearGPA = latestPerformance.GPA;
                                }
                            }
                            else
                            {
                                // If no historical data, use current year GPA as overall GPA
                                performanceViewModel.OverallGPA = performanceViewModel.YearGPA;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error retrieving academic performance records for student {student.StudentId_Number}: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing performance data for student {student.StudentId_Number}: {ex.Message}");
                    }
                }

                ViewData["PerformanceData"] = performanceViewModel;
                ViewData["CanViewCompleteResults"] = student.OutstandingFees <= 0;

                return View(student);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error in Student_Dashboard: {ex.Message}\nStackTrace: {ex.StackTrace}");

                // Return a user-friendly error page or redirect
                TempData["ErrorMessage"] = "An error occurred while loading your dashboard. Please try again later.";
                return RedirectToAction("Error", "Home");
            }
        }

        // ? NEW: Helper method to identify exam assessments (add this to your controller)
        private bool IsExamAssessment(string assessmentName)
        {
            if (string.IsNullOrEmpty(assessmentName)) return false;

            var examNames = new[] { "Exam", "EXAM", "Final Exam", "Final", "Main Exam", "End of Semester Exam" };
            return examNames.Contains(assessmentName, StringComparer.OrdinalIgnoreCase);
        }



        [HttpGet]
        [Authorize]
        public IActionResult PreviewStudentPhoto(string photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                return NotFound("Photo path is required");
            }

            try
            {
                // Get current user to ensure they can only access their own photo
                var currentUser = _userManager.GetUserAsync(User).Result;
                if (currentUser == null)
                {
                    return Unauthorized();
                }

                // Get the student record to verify ownership
                var student = _context.Students
                    .FirstOrDefault(s => s.Username == currentUser.UserName);

                if (student == null || student.PassportPhotoPath != photoPath)
                {
                    return Unauthorized("Access denied");
                }

                // Convert relative path to absolute if needed
                if (!Path.IsPathRooted(photoPath))
                {
                    photoPath = Path.Combine(_webHostEnvironment.ContentRootPath, photoPath);
                }

                // Security check: Validate and sanitize the file path
                var fullPath = Path.GetFullPath(photoPath);

                // Ensure the file exists
                if (!System.IO.File.Exists(fullPath))
                {
                    return NotFound($"Photo file not found");
                }

                // Determine content type based on file extension
                string contentType;
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        contentType = "image/jpeg";
                        break;
                    case ".png":
                        contentType = "image/png";
                        break;
                    case ".gif":
                        contentType = "image/gif";
                        break;
                    case ".webp":
                        contentType = "image/webp";
                        break;
                    default:
                        return BadRequest("Unsupported image format");
                }

                // Return file with content type and caching headers
                Response.Headers.Add("Cache-Control", "public, max-age=3600"); // Cache for 1 hour
                return PhysicalFile(fullPath, contentType);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error accessing student photo: {ex.Message}");
                return StatusCode(500, "Error loading photo");
            }
        }


        [Authorize(Roles = "VC,DVC,Registrar,Admin")]
        public async Task<IActionResult> VCDashboard()
        {
            try
            {
                // Stats Cards Data - Fixed Active Students Logic
                var totalStudents = await _context.Students
                    .Where(s => s.IsRegistered == true && s.RegistrationStatus == Status.Registered)
                    .CountAsync();

                var totalRevenue = await _context.FinancialStatements
                    .SumAsync(fs => fs.AmountPaid);

                var outstandingFees = await _context.Students
                    .SumAsync(s => s.OutstandingFees);

                // New Admissions - Students admitted within last 7 days
                var date = new DateTime(2026, 01, 01);
                var newAdmissions = await _context.Students
                    .Where(s => s.IsAdmitted == true &&
                               s.AdmissionDate.HasValue &&
                               s.AdmissionDate.Value >= date) //DateTime.Now.AddDays(-30))
                    .CountAsync();

                // Revenue Trends (Last 12 months)
                var revenueData = await _context.FinancialStatements
                    .Where(fs => fs.PaymentDate >= DateTime.Now.AddMonths(-12))
                    .GroupBy(fs => new { fs.PaymentDate.Year, fs.PaymentDate.Month })
                    .Select(g => new {
                        Month = g.Key.Month,
                        Year = g.Key.Year,
                        Total = g.Sum(fs => fs.AmountPaid)
                    })
                    .OrderBy(x => x.Year).ThenBy(x => x.Month)
                    .ToListAsync();

                // Create month labels and revenue amounts
                var monthLabels = new List<string>();
                var revenueAmounts = new List<decimal>();

                for (int i = 11; i >= 0; i--)
                {
                    var targetDate = DateTime.Now.AddMonths(-i);
                    var monthData = revenueData.FirstOrDefault(r => r.Year == targetDate.Year && r.Month == targetDate.Month);

                    monthLabels.Add(targetDate.ToString("MMM yyyy"));
                    revenueAmounts.Add(monthData?.Total ?? 0);
                }

                // School Performance (Top 5 by revenue) - Fixed to use registered students
                var schoolData = await _context.Students
                    .Include(s => s.School)
                    .Include(s => s.FinancialStatements)
                    .Where(s => s.School != null && s.FinancialStatements.Any() &&
                               s.IsRegistered == true)
                    .GroupBy(s => s.School.Name)
                    .Select(g => new {
                        SchoolName = g.Key,
                        Revenue = g.SelectMany(s => s.FinancialStatements).Sum(fs => fs.AmountPaid),
                        StudentCount = g.Count()
                    })
                    .OrderByDescending(x => x.Revenue)
                    //.Take(5)
                    .ToListAsync();

                var schoolNames = schoolData.Select(s => s.SchoolName).ToList();
                var schoolRevenues = schoolData.Select(s => s.Revenue).ToList();
                var schoolStudentCounts = schoolData.Select(s => s.StudentCount).ToList();

                // Payment Methods Distribution (Last 6 months)
                var paymentMethodsData = await _context.FinancialStatements
                    .Where(fs => fs.PaymentDate >= DateTime.Now.AddMonths(-6))
                    .GroupBy(fs => fs.PaymentMethod ?? "Unknown")
                    .Select(g => new {
                        Method = g.Key,
                        Count = g.Count(),
                        Total = g.Sum(fs => fs.AmountPaid)
                    })
                    .OrderByDescending(x => x.Total)
                    .ToListAsync();

                var paymentMethods = paymentMethodsData.Select(p => p.Method).ToList();
                var paymentAmounts = paymentMethodsData.Select(p => p.Total).ToList();

                // Collection Rate - Fixed calculation based on actual student balances
                // Total amount that should be collected = Total Revenue + Outstanding Fees
                var totalAmountDue = totalRevenue + outstandingFees;
                var collectionRate = totalAmountDue > 0 ? Math.Round((totalRevenue / totalAmountDue) * 100, 1) : 100;

                // Registration Compliance - Fixed to use all students vs registered
                var allStudents = await _context.Students.CountAsync(); // Total students in system
                var registeredStudents = await _context.Students
                    .Where(s => s.IsRegistered == true && s.RegistrationStatus == Status.Registered)
                    .CountAsync();
                var registrationRate = allStudents > 0 ? Math.Round((registeredStudents / (decimal)allStudents) * 100, 1) : 0;

                // Recent High-Value Transactions (Last 10)
                var recentTransactions = await _context.FinancialStatements
                    .Include(fs => fs.Student)
                    .Where(fs => fs.AmountPaid > 1000) // High-value transactions
                    .OrderByDescending(fs => fs.PaymentDate)
                    .Take(10)
                    .Select(fs => new {
                        StudentName = fs.Student.FullName ?? "Unknown",
                        StudentId = fs.Student.StudentId_Number,
                        Amount = fs.AmountPaid,
                        PaymentDate = fs.PaymentDate,
                        PaymentMethod = fs.PaymentMethod ?? "Unknown",
                        TransactionRef = fs.TransactionReference
                    })
                    .ToListAsync();

                // Students with Overdue Payments (> 30 days) - Updated query
                var overdueStudents = await _context.Students
                    .Include(s => s.Programme)
                    .Where(s => s.OutstandingFees > 0 &&
                               (s.IsRegistered == false || s.RegistrationStatus != Status.Registered))
                    .OrderByDescending(s => s.OutstandingFees)
                    .Take(10)
                    .Select(s => new {
                        StudentName = s.FullName ?? "Unknown",
                        StudentId = s.StudentId_Number,
                        Programme = s.Programme.Name ?? "Unknown",
                        OutstandingAmount = s.OutstandingFees,
                        LastPaymentDate = s.FinancialStatements
                            .OrderByDescending(fs => fs.PaymentDate)
                            .Select(fs => fs.PaymentDate)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                // Top Performing Programmes by Revenue - Updated for registered students
                var programmePerformance = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.FinancialStatements)
                    .Where(s => s.Programme != null && s.FinancialStatements.Any() &&
                               s.IsRegistered == true)
                    .GroupBy(s => s.Programme.Name)
                    .Select(g => new {
                        ProgrammeName = g.Key,
                        TotalRevenue = g.SelectMany(s => s.FinancialStatements).Sum(fs => fs.AmountPaid),
                        StudentCount = g.Count(),
                        AvgRevenuePerStudent = g.SelectMany(s => s.FinancialStatements).Sum(fs => fs.AmountPaid) / g.Count()
                    })
                    .OrderByDescending(x => x.TotalRevenue)
                    .Take(5)
                    .ToListAsync();

                // Set ViewBag data - Updated variable names
                ViewBag.ActiveStudents = totalStudents; // This is now registered students
                ViewBag.AllStudents = allStudents; // Total students in system
                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.OutstandingFees = outstandingFees;
                ViewBag.NewAdmissions = newAdmissions;
                ViewBag.CollectionRate = collectionRate;
                ViewBag.RegistrationRate = registrationRate;

                // Chart data
                ViewBag.MonthLabels = JsonConvert.SerializeObject(monthLabels);
                ViewBag.RevenueAmounts = JsonConvert.SerializeObject(revenueAmounts);
                ViewBag.SchoolNames = JsonConvert.SerializeObject(schoolNames);
                ViewBag.SchoolRevenues = JsonConvert.SerializeObject(schoolRevenues);
                ViewBag.SchoolStudentCounts = JsonConvert.SerializeObject(schoolStudentCounts);
                ViewBag.PaymentMethods = JsonConvert.SerializeObject(paymentMethods);
                ViewBag.PaymentAmounts = JsonConvert.SerializeObject(paymentAmounts);

                // Table data
                ViewBag.RecentTransactions = JsonConvert.SerializeObject(recentTransactions);
                ViewBag.OverdueStudents = JsonConvert.SerializeObject(overdueStudents);
                ViewBag.ProgrammePerformance = JsonConvert.SerializeObject(programmePerformance);

                return View("VCDashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading VC Dashboard");
                TempData["Error"] = "An error occurred while loading the dashboard.";
                return RedirectToAction("Index");
            }
        }




























































        // Test method to send an email
        public async Task<IActionResult> SendTestEmail()
        {
            string recipientEmail = "chishaonesimus@gmail.com"; // Replace with actual email
            var institution = _institutionConfig.GetCurrentInstitution();
            string subject = $"Test Email from {institution.Name} eCampus";
            string body = $@"
                <h2>Hello from {institution.Name}!</h2>
                <p>This is a test email from our eCampus system.</p>
                <p>If you receive this, our email configuration is working correctly.</p>
                <br>
                <p>Best regards,<br>{institution.Name} Administration</p>
            ";

            bool emailSent = await _emailService.SendEmailAsync(recipientEmail, subject, body);

            if (emailSent)
            {
                TempData["Info"] = "Email sent successfully!";
            }
            else
            {
                TempData["Info"] = "Failed to send email. Check configuration.";
            }

            return RedirectToAction("Index");
        }



        // Add this method to test the email
        public async Task<IActionResult> TestApplicationEmail()
        {
            try
            {
                bool emailSent = await _emailService.SendApplicationSubmissionEmailAsync(
                    applicantName: "Test Applicant",
                    applicantEmail: "chishaonesimus@gmail.com", // Replace with your actual email
                    programmeName: "Bachelor of Computer Science",
                    schoolName: "School of Computing",
                    referenceNumber: "TEST123456",
                    paymentAmount: 250.00m,
                    transactionReference: "TEST_TXN_12345"
                );

                if (emailSent)
                {
                    TempData["Info"] = "Test application email sent successfully!";
                }
                else
                {
                    TempData["Info"] = "Failed to send test email. Check logs.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Message"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }


        // Add these test methods to your AdmissionProcessController
        // Updated test method using the new HTML PDF service

        [HttpGet]
        public async Task<IActionResult> TestAdmissionEmail()
        {
            try
            {
                // Get existing student from database
                var testStudent = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.School)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync();

                if (testStudent == null)
                {
                    TempData["Message"] = "No students found in database for testing.";
                    return RedirectToAction("PendingAdmission");
                }

                // Override email for testing (don't save to DB)
                var originalEmail = testStudent.Email;
                testStudent.Email = "chishaonesimus@gmail.com"; // Replace with your actual email

                // Generate beautiful HTML-based admission letter PDF
                byte[] admissionLetterPdf = await _htmlPdfService.GenerateAdmissionLetterAsync(testStudent);

                // Send email with generated PDF
                bool emailSent = await _emailService.SendAdmissionEmailAsync(testStudent, admissionLetterPdf);

                // Restore original email
                testStudent.Email = originalEmail;

                if (emailSent)
                {
                    TempData["Info"] = $"Beautiful HTML-based admission email sent successfully to {testStudent.FullName}!";
                }
                else
                {
                    TempData["Info"] = "Failed to send test admission email. Check logs.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Info"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // TEST METHOD 2: Test Rejection Email
        [HttpGet]
        public async Task<IActionResult> TestRejectionEmail()
        {
            try
            {
                // Get existing applicant data from database
                var testApplicant = await _context.Applicants
                    .Include(a => a.Programme)
                    .Include(a => a.School)
                    .FirstOrDefaultAsync();

                if (testApplicant == null)
                {
                    TempData["Info"] = "No applicants found in database for testing.";
                    return RedirectToAction("PendingAdmission");
                }

                bool emailSent = await _emailService.SendRejectionEmailAsync(
                    applicantName: testApplicant.FullName,
                    applicantEmail: "chishaonesimus@gmail.com", // Replace with your actual email
                    programmeName: testApplicant.Programme?.Name ?? "Unknown Programme",
                    schoolName: testApplicant.School?.Name ?? "Unknown School",
                    reason: "Your academic qualifications do not meet the minimum requirements for this programme. The programme requires a minimum of 5 credits in Mathematics and English at Grade 12 level."
                );

                if (emailSent)
                {
                    TempData["Info"] = $"Test rejection email sent successfully for {testApplicant.FullName}!";
                }
                else
                {
                    TempData["Info"] = "Failed to send test rejection email. Check logs.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Info"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // TEST METHOD 3: Test Waitlist Email
        [HttpGet]
        public async Task<IActionResult> TestWaitlistEmail()
        {
            try
            {
                // Get existing applicant data from database
                var testApplicant = await _context.Applicants
                    .Include(a => a.Programme)
                    .Include(a => a.School)
                    .Include(a => a.AcademicYear)
                    .FirstOrDefaultAsync();

                if (testApplicant == null)
                {
                    TempData["Info"] = "No applicants found in database for testing.";
                    return RedirectToAction("PendingAdmission");
                }

                // Use the applicant's academic year start date, or default to March 2025
                DateTime academicYearStart = testApplicant.AcademicYear?.StartDate ?? new DateTime(2025, 3, 15);

                bool emailSent = await _emailService.SendWaitlistEmailAsync(
                    applicantName: testApplicant.FullName,
                    applicantEmail: "chishaonesimus@gmail.com", // Replace with your actual email
                    programmeName: testApplicant.Programme?.Name ?? "Unknown Programme",
                    schoolName: testApplicant.School?.Name ?? "Unknown School",
                    academicYearStart: academicYearStart
                );

                if (emailSent)
                {
                    TempData["Info"] = $"Test waitlist email sent successfully for {testApplicant.FullName}!";
                }
                else
                {
                    TempData["Info"] = "Failed to send test waitlist email. Check logs.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Info"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // TEST METHOD 4: Test All Email Types
        [HttpGet]
        public async Task<IActionResult> TestAllAdmissionEmails()
        {
            try
            {
                var results = new List<string>();

                // Test 1: Admission Email
                var testStudent = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.School)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync();

                if (testStudent != null)
                {
                    var originalEmail = testStudent.Email;
                    testStudent.Email = "chishaonesimus@gmail.com"; // Replace with your actual email

                    byte[] dummyPdf = System.Text.Encoding.UTF8.GetBytes("Test PDF content");
                    bool admissionSent = await _emailService.SendAdmissionEmailAsync(testStudent, dummyPdf);

                    testStudent.Email = originalEmail; // Restore original
                    results.Add($"Admission Email: {(admissionSent ? "SENT" : "FAILED")}");

                    // Small delay between emails
                    await Task.Delay(1000);
                }
                else
                {
                    results.Add("Admission Email: NO STUDENT DATA");
                }

                // Test 2: Rejection Email
                var testApplicant = await _context.Applicants
                    .Include(a => a.Programme)
                    .Include(a => a.School)
                    .FirstOrDefaultAsync();

                if (testApplicant != null)
                {
                    bool rejectionSent = await _emailService.SendRejectionEmailAsync(
                        testApplicant.FullName,
                        "chishaonesimus@gmail.com", // Replace with your actual email
                        testApplicant.Programme?.Name ?? "Test Programme",
                        testApplicant.School?.Name ?? "Test School",
                        "This is a test rejection reason."
                    );
                    results.Add($"Rejection Email: {(rejectionSent ? "SENT" : "FAILED")}");
                    await Task.Delay(1000);
                }
                else
                {
                    results.Add("Rejection Email: NO APPLICANT DATA");
                }

                // Test 3: Waitlist Email
                if (testApplicant != null)
                {
                    bool waitlistSent = await _emailService.SendWaitlistEmailAsync(
                        testApplicant.FullName,
                        "chishaonesimus@gmail.com", // Replace with your actual email
                        testApplicant.Programme?.Name ?? "Test Programme",
                        testApplicant.School?.Name ?? "Test School",
                        new DateTime(2025, 3, 15)
                    );
                    results.Add($"Waitlist Email: {(waitlistSent ? "SENT" : "FAILED")}");
                }
                else
                {
                    results.Add("Waitlist Email: NO APPLICANT DATA");
                }

                TempData["Info"] = "Test Results: " + string.Join(" | ", results);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Info"] = $"Error testing emails: {ex.Message}";
                return RedirectToAction("Index");
            }
        }


        // TEST METHOD: Test User Creation Email
        [HttpGet]
        public async Task<IActionResult> TestUserCreationEmail()
        {
            try
            {
                // Test data - you can modify these values as needed
                string testFullName = "John Test User";
                string testEmail = "chishaonesimus@gmail.com"; // Replace with your actual email
                string testRole = "Student"; // or "Admin", "Staff", etc.
                string testPassword = "TempPass123!"; // Test password
                string testLoginUrl = "https://ecampus.edenuniversity.edu.zm/Account/Login"; // Update to your actual login URL

                bool emailSent = await _emailService.SendUserCreationEmailAsync(
                    fullName: testFullName,
                    email: testEmail,
                    role: testRole,
                    password: testPassword,
                    loginUrl: testLoginUrl
                );

                if (emailSent)
                {
                    TempData["Info"] = $"Test user creation email sent successfully to {testEmail}!";
                }
                else
                {
                    TempData["Info"] = "Failed to send test user creation email. Check logs for details.";
                }

                return RedirectToAction("Index"); // or wherever you want to redirect
            }
            catch (Exception ex)
            {
                TempData["Info"] = $"Error testing user creation email: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ALTERNATIVE: Test with actual user data from database
        [HttpGet]
        public async Task<IActionResult> TestUserCreationEmailWithRealData()
        {
            try
            {
                // Get an existing user from database for testing
                var testUser = await _userManager.Users.FirstOrDefaultAsync();

                if (testUser == null)
                {
                    TempData["Info"] = "No users found in database for testing.";
                    return RedirectToAction("Index");
                }

                // Get the user's role
                var userRoles = await _userManager.GetRolesAsync(testUser);
                string userRole = userRoles.FirstOrDefault() ?? "User";

                // Test password (this would normally be the generated password)
                string testPassword = "TestPassword123!";

                bool emailSent = await _emailService.SendUserCreationEmailAsync(
                    fullName: testUser.FullName,
                    email: "chishaonesimus@gmail.com", // Replace with your actual test email
                    role: userRole,
                    password: testPassword
                );

                if (emailSent)
                {
                    TempData["Info"] = $"Test user creation email sent successfully for {testUser.FullName}!";
                }
                else
                {
                    TempData["Info"] = "Failed to send test user creation email. Check logs for details.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Info"] = $"Error testing user creation email with real data: {ex.Message}";
                return RedirectToAction("Index");
            }
        }




    }
}