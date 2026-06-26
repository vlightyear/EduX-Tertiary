using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Payments;
using SIS.Models.Reports;
using SIS.Models.StudentApplication;
using SIS.Models.StudyPermits;
using SIS.Models.ViewModels;
using SIS.Services;
using SIS.Services.PDF;
using SIS.Services.Progression;
using SIS.Services.StudentApplication;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ZXing;
using ZXing.Common;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SIS.Controllers
{
    [Authorize(Roles = "VC,DVC,Dean,HOD,ProgrammeCoordinator,Lecturer, Registrar, Admin")]
    public class StudentLookupController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StudentLookupController> _logger;
        private readonly IPdfInvoiceService _pdfService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IStudentProgressionService _progressionService;
        private readonly IApplicantService _applicantService;
        private readonly IInstitutionConfigService _institutionConfig;

        public StudentLookupController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<StudentLookupController> logger,
            IPdfInvoiceService pdfService,
            IWebHostEnvironment webHostEnvironment,
            IStudentProgressionService progressionService,
            IApplicantService applicantService,
            IInstitutionConfigService institutionConfig)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _pdfService = pdfService;
            _webHostEnvironment = webHostEnvironment;
            _progressionService = progressionService;
            _applicantService = applicantService;
            _institutionConfig = institutionConfig;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Get user's jurisdiction info for display
                var jurisdictionInfo = await GetUserJurisdictionInfo(user, primaryRole);

                var viewModel = new StudentLookupIndexViewModel
                {
                    UserRole = primaryRole,
                    UserName = user.FullName,
                    JurisdictionInfo = jurisdictionInfo,
                    SearchTypes = new List<string> { "StudentNumber", "Name", "NrcPassport", "Email" },
                    User = user
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student lookup dashboard");
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SearchStudents(string searchTerm, string searchType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return Json(new { success = false, message = "Search term is required" });
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Get students based on user's jurisdiction
                var studentsQuery = await BuildStudentsQuery(user, primaryRole);

                // Apply search filter
                studentsQuery = ApplySearchFilter(studentsQuery, searchTerm, searchType);

                var students = await studentsQuery
                    .Select(s => new StudentSearchResultViewModel
                    {
                        Id = s.Id,
                        StudentNumber = s.StudentId_Number,
                        FullName = s.FullName,
                        Email = s.Email,
                        Phone = s.Phone,
                        ProgrammeName = s.Programme.Name,
                        SchoolName = s.School.Name,
                        AcademicYear = s.AcademicYear.YearValue,
                        CurrentYear = s.StudentCurrentYear ?? 0,
                        StudentStatus = s.StudentStatus.ToString(),
                        RegistrationStatus = s.RegistrationStatus.ToString(),
                        OutstandingFees = StudentTools.GetStudentOutstandingBalance(s.Id),
                        IsRegistered = s.IsRegistered,
                        PassportPhotoPath = s.PassportPhotoPath
                    })
                    .Take(50) // Limit results for performance
                    .ToListAsync();

                return Json(new { success = true, students = students });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching students with term: {SearchTerm}", searchTerm);
                return Json(new { success = false, message = "An error occurred while searching students" });
            }
        }

        [HttpPost("/StudyPermit/Save")]
        public async Task<IActionResult> Save([FromForm] StudyPermit model, IFormFile? PermitDocumentPath)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            if (PermitDocumentPath != null)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(PermitDocumentPath.FileName);
                var filePath = Path.Combine("wwwroot/uploads/study-permits", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PermitDocumentPath.CopyToAsync(stream);
                }

                model.PermitDocumentPath = "/uploads/study-permits/" + fileName;
            }

            if (model.Id == 0)
            {
                model.IsActive = true;
                model.Status = PermitStatus.Valid;
                model.CreatedBy = user.Id;
                model.CreatedAt = DateTime.Now;
                _context.StudyPermits.Add(model);
            }
            else
            {
                model.UpdatedBy = user.Id;
                model.UpdatedAt = DateTime.Now;
                _context.StudyPermits.Update(model);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("StudentLookup/GetStudentProfile/{studentId:int}")]
        public async Task<IActionResult> GetStudentProfile(int studentId)
        {
            try
            {
                _logger.LogInformation("Received studentId: {StudentId}", studentId);
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Verify user can access this student
                if (!await CanAccessStudent(user, studentId))
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Include(s => s.ProgrammeLevel)
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.AcademicYear)
                    .Include(s => s.StudentAddress)
                    .Include(s => s.NextOfKin)
                    .Include(s => s.StudyPermits)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                var profileViewModel = new StudentProfileViewModel
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentId_Number,
                    FullName = student.FullName,
                    Email = student.Email,
                    Phone = student.Phone,
                    DateOfBirth = student.DateOfBirth,
                    Gender = student.Gender,
                    MaritalStatus = student.MaritalStatus,
                    Nationality = student.Nationality,
                    Religion = student.Religion,
                    NrcOrPassportNumber = student.NrcOrPassportNumber,
                    IsForeigner = student.IsForeigner,
                    ProgrammeName = student.Programme?.Name ?? "N/A",
                    SchoolName = student.Programme?.Department?.School?.Name ?? "N/A",
                    DepartmentName = student.Programme?.Department?.Name ?? "N/A",
                    ProgrammeLevelName = student.ProgrammeLevel?.Name ?? "N/A",
                    ModeOfStudyName = student.ModeOfStudy?.ModeName ?? "N/A",
                    AcademicYear = student.AcademicYear?.YearValue ?? "N/A",
                    CurrentYear = student.StudentCurrentYear ?? 0,
                    CurrentPeriodId = student.CurrentYearPeriodId ?? 0,
                    CurrentPeriodLabel = student.CurrentYearPeriod?.FullLabel ?? "N/A",
                    StudentStatus = student.StudentStatus.ToString(),
                    RegistrationStatus = student.RegistrationStatus.ToString(),
                    IsRegistered = student.IsRegistered,
                    IsAdmitted = student.IsAdmitted,
                    AdmissionDate = student.AdmissionDate,
                    RegistrationDate = student.RegistrationDate,
                    PassportPhotoPath = student.PassportPhotoPath,
                    // Address Information
                    Address = student.StudentAddress != null ? new AddressViewModel
                    {
                        AddressLine1 = student.StudentAddress.AddressLine1,
                        AddressLine2 = student.StudentAddress.AddressLine2,
                        City = student.StudentAddress.City,
                        State = student.StudentAddress.State,
                        Country = student.StudentAddress.Country,
                        PostalCode = student.StudentAddress.PostalCode
                    } : null,
                    // Next of Kin Information
                    NextOfKin = student.NextOfKin != null ? new NextOfKinViewModel
                    {
                        Name = student.NextOfKin.Name,
                        Relationship = student.NextOfKin.Relationship,
                        Phone = student.NextOfKin.PhoneNumber,
                        Email = student.NextOfKin.Email,
                        Address = student.NextOfKin.Address
                    } : null,
                    StudyPermits = student.StudyPermits != null ? student.StudyPermits : []
                };

                return PartialView("_StudentProfilePartial", profileViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student profile for ID: {StudentId}", studentId);
                return Json(new { success = false, message = "Error loading student profile" });
            }
        }

        [HttpGet("StudentLookup/GetStudentFinancials/{studId:int?}")]
        public async Task<IActionResult> GetStudentFinancials(int? studId)
        {
            int studentId = 0;
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                if(studId == null)
                {
                    var stud = await _context.Students.Where(s => s.Email == user.Email).FirstOrDefaultAsync();

                    if(stud != null)
                    {
                        studentId = stud.Id;
                    }
                }
                else
                {
                    studentId = studId.Value;
                }

                // Verify user can access this student
                if (!await CanAccessStudent(user, studentId))
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .Include(s => s.Programme)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                // Get current invoice
                var currentInvoice = await _context.StudentInvoices
                    .Include(si => si.InvoiceItems)
                    .FirstOrDefaultAsync(si => si.StudentId == studentId &&
                                             si.AcademicYearId == student.AcademicYearId &&
                                             (student.Programme.IsSemesterBased == false || si.YearPeriodId == student.CurrentYearPeriodId));

                // Get total paid for current invoices
                var totalFees = StudentTools.GetStudentTotalFees(studentId);

                // Calculate financial summary
                var totalPaid = StudentTools.GetStudentTotalPaid(studentId);
                //var totalFees = currentInvoice?.TotalAmount ?? 0;
                var outstandingBalance = totalFees - totalPaid; //student.OutstandingFees;

                //Correct outstanding balance
                try
                {
                    if (student.OutstandingFees != outstandingBalance)
                    {
                        student.OutstandingFees = outstandingBalance;
                        _context.Update(student);
                        await _context.SaveChangesAsync();
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Error updating outstanding balance for student ID: {StudentId}", studentId);
                }
                    //End correct outstanding

                    //Get student statement
                    var paymentsQuery = _context.OnlinePayments.Where(op => op.StudentId == student.Id && op.Status == "Paid")
                        .Select(p => new UnifiedTransactionDto
                        {
                            Id = p.Id,
                            StudentId = p.StudentId,
                            Amount = p.Amount,
                            //Status = p.Status,
                            Credit = true,
                            Reference = p.ReferenceNumber,
                            AccountingSystemPostStatus = p.AccountingSystemPostStatus,
                            CreatedAt = p.TransactionDate ?? p.CreatedAt
                        });

                var invoicesQuery = _context.StudentInvoices.Where(si => si.StudentId == student.Id && si.DeletedAt == null)
                    .Select(i => new UnifiedTransactionDto
                    {
                        Id = i.Id,
                        StudentId = i.StudentId,
                        Amount = i.TotalAmount,
                        //Status = i.Status,
                        Credit = false,
                        Reference = i.InvoiceReference,
                        AccountingSystemPostStatus = i.AccountingSystemPostStatus,
                        CreatedAt = i.CreatedDate
                    });

                var unified = paymentsQuery
                    .Union(invoicesQuery)
                    .OrderBy(x => x.CreatedAt)
                    .ToList();

                // Get fee breakdown
                var feeBreakdown = new List<FeeBreakdownViewModel>();
                if (currentInvoice != null)
                {
                    var remainingPaid = totalPaid;
                    foreach (var item in currentInvoice.InvoiceItems.OrderBy(i => i.Amount))
                    {
                        var paidForThisFee = Math.Min(remainingPaid, item.Amount);
                        remainingPaid -= paidForThisFee;

                        feeBreakdown.Add(new FeeBreakdownViewModel
                        {
                            Description = item.FeeTypeName,
                            Amount = item.Amount,
                            Paid = paidForThisFee,
                            Balance = item.Amount - paidForThisFee
                        });
                    }
                }

                var financialViewModel = new StudentFinancialViewModel
                {
                    StudentId = studentId,
                    StudentNumber = student.StudentId_Number,
                    StudentName = student.FullName,
                    AcademicYear = student.AcademicYear?.YearValue ?? "N/A",
                    TotalFees = totalFees,
                    AmountPaid = totalPaid,
                    OutstandingBalance = outstandingBalance,
                    FeeBreakdown = feeBreakdown,
                    PaymentHistory = [],
                    InvoiceReference = currentInvoice?.InvoiceReference ?? "N/A",
                    InvoiceDate = currentInvoice?.CreatedDate ?? DateTime.MinValue,
                    FinancialStatement = unified,
                    // Registration requirements
                    MinRegistrationPayment = totalFees > 0 ? (totalFees * (student.AcademicYear?.MinRegistrationPaymentPercentage ?? 0)) / 100 : 0,
                    MinExamPayment = totalFees > 0 ? (totalFees * (student.AcademicYear?.MinExamPaymentPercentage ?? 0)) / 100 : 0,
                    CanRegister = totalPaid >= ((totalFees * (student.AcademicYear?.MinRegistrationPaymentPercentage ?? 0)) / 100),
                    CanTakeExams = totalPaid >= ((totalFees * (student.AcademicYear?.MinExamPaymentPercentage ?? 0)) / 100)
                };

                return PartialView("_StudentFinancialsPartial", financialViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student financials for ID: {StudentId}", studentId);
                return Json(new { success = false, message = "Error loading student financials" });
            }
        }

        [HttpGet("StudentLookup/GetStudentResults/{studentId:int}")]
        /*public async Task<IActionResult> GetStudentResults(int studentId)
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

                _logger.LogInformation($"Processing results for student: {student.Id}");

                // Check if student can view complete results (no outstanding fees)
                decimal outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
                bool canViewCompleteResults = outstandingBalance <= 0;

                var viewModel = new Models.Admin.StudentResultsViewModel
                {
                    OutstandingFees = outstandingBalance,
                    OverallGPA = 0.0M,
                    AcademicYears = new List<AcademicYearResults>(),
                    Grades = new List<Models.Admin.GradeConfiguration>(), // Empty since view provides grades
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
                    ViewBag.StudentId = studentId;
                    return PartialView("~/Views/StudentResults/Results.cshtml", viewModel);
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

                return PartialView("~/Views/StudentResults/Results.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student results");
                TempData["Error"] = "An error occurred while loading your results. Please try again.";
                return RedirectToAction("Student_Dashboard", "Home");
            }
        }*/

        public async Task<IActionResult> GetStudentResults(int studentId)
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

                _logger.LogInformation($"Processing results for student: {student.Id}");

                // Check if student can view complete results (no outstanding fees)
                decimal outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
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
                        c.PeriodTakenId == pair.Semester);

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
                    ViewBag.StudentId = studentId;
                }

                // Calculate overall GPA
                viewModel.OverallGPA = totalCreditsAttempted > 0 ? totalGpaPoints / totalCreditsAttempted : 0;
                viewModel.TotalCreditsAttempted = totalCreditsAttempted;
                viewModel.TotalCreditsEarned = totalCreditsEarned;

                _logger.LogInformation($"Results loaded for student {student.Id}: {viewModel.AcademicYears.Count} years, Overall GPA: {viewModel.OverallGPA}");

                return PartialView("~/Views/StudentResults/Results.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student results");
                TempData["Error"] = "An error occurred while loading your results. Please try again.";
                return RedirectToAction("Student_Dashboard", "Home");
            }
        }

        #region Helper Methods

        private async Task<string> GetUserJurisdictionInfo(ApplicationUser user, string role)
        {
            // All roles now have university-wide access
            return "All Students (University-wide access)";
        }

        private async Task<string> GetDeanJurisdiction(string userId)
        {
            var school = await _context.Schools
                .FirstOrDefaultAsync(s => s.DeanId == userId);
            return school != null ? $"School: {school.Name}" : "No school assigned";
        }

        private async Task<string> GetHODJurisdiction(string userId)
        {
            var department = await _context.Departments
                .Include(d => d.School)
                .FirstOrDefaultAsync(d => d.HODId == userId);
            return department != null ? $"Department: {department.Name} ({department.School?.Name})" : "No department assigned";
        }

        private async Task<string> GetProgrammeCoordinatorJurisdiction(string userId)
        {
            var programme = await _context.Programmes
                .Include(p => p.Department)
                    .ThenInclude(d => d.School)
                .FirstOrDefaultAsync(p => p.CoordinatorId == userId);
            return programme != null ? $"Programme: {programme.Name} ({programme.Department?.School?.Name})" : "No programme assigned";
        }

        private async Task<string> GetLecturerJurisdiction(string userId)
        {
            var courseCount = await _context.Courses
                .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == userId) || c.InstructorId == userId)
                .CountAsync();
            return $"Students in {courseCount} assigned courses";
        }

        private async Task<IQueryable<Student>> BuildStudentsQuery(ApplicationUser user, string role)
        {
            var query = _context.Students
                .AsNoTracking()
                .Include(s => s.StudyPermits
                    .OrderByDescending(sp => sp.ExpiryDate)
                    .Take(1))
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Include(s => s.AcademicYear)
                .Include(s => s.CurrentYearPeriod)
                    .ThenInclude(yp => yp.AcademicYear)
                .Include(s => s.CurrentYearPeriod)
                    .ThenInclude(yp => yp.AcademicPeriod)
                .Include(s => s.School)
                .AsQueryable();

            return query;
        }

        private async Task<IQueryable<Student>> FilterByDeanJurisdiction(IQueryable<Student> query, string userId)
        {
            var school = await _context.Schools
                .FirstOrDefaultAsync(s => s.DeanId == userId);

            return school != null ? query.Where(s => s.SchoolId == school.Id) : query.Where(s => false);
        }

        private async Task<IQueryable<Student>> FilterByHODJurisdiction(IQueryable<Student> query, string userId)
        {
            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.HODId == userId);

            if (department == null) return query.Where(s => false);

            var programmeIds = await _context.Programmes
                .Where(p => p.DepartmentId == department.Id)
                .Select(p => p.Id)
                .ToListAsync();

            return query.Where(s => programmeIds.Contains(s.ProgrammeId));
        }

        private async Task<IQueryable<Student>> FilterByProgrammeCoordinatorJurisdiction(IQueryable<Student> query, string userId)
        {
            var programme = await _context.Programmes
                .FirstOrDefaultAsync(p => p.CoordinatorId == userId);

            return programme != null ? query.Where(s => s.ProgrammeId == programme.Id) : query.Where(s => false);
        }

        private async Task<IQueryable<Student>> FilterByLecturerJurisdiction(IQueryable<Student> query, string userId)
        {
            // Get courses taught by this lecturer
            var courseIds = await _context.Courses
                .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == userId) || c.InstructorId == userId)
                .Select(c => c.Id)
                .ToListAsync();

            // Get students enrolled in these courses
            var studentIds = await _context.StudentExaminableCourses
                .Where(sec => courseIds.Contains(sec.CourseId))
                .Select(sec => sec.StudentId)
                .Distinct()
                .ToListAsync();

            return query.Where(s => studentIds.Contains(s.Id));
        }

        private static IQueryable<Student> ApplySearchFilter(IQueryable<Student> query, string searchTerm, string searchType)
        {
            var term = searchTerm.Trim().ToLower();

            return searchType?.ToLower() switch
            {
                "studentnumber" => query.Where(s => s.StudentId_Number.ToLower().Contains(term)),
                "name" => query.Where(s => s.FullName.ToLower().Contains(term)),
                "nrcpassport" => query.Where(s => s.NrcOrPassportNumber.ToLower().Contains(term)),
                "email" => query.Where(s => s.Email.ToLower().Contains(term)),
                _ => query.Where(s => s.StudentId_Number.ToLower().Contains(term) ||
                                    s.FullName.ToLower().Contains(term) ||
                                    s.NrcOrPassportNumber.ToLower().Contains(term) ||
                                    s.Email.ToLower().Contains(term))
            };
        }

        private async Task<bool> CanAccessStudent(ApplicationUser user, int studentId)
        {
            return true;
        }

        private CourseResultViewModel ProcessCourseResult(StudentExaminableCourse course, List<SIS.Models.ViewModels.GradeConfiguration> grades)
        {
            var result = new CourseResultViewModel
            {
                CourseId = course.CourseId,
                CourseCode = course.Course?.CourseCode ?? "N/A",
                CourseName = course.Course?.CourseName ?? "N/A",
                Credits = 3, // Default credits
                IsPublished = course.Status == Status.Published,
                AssessmentScores = new Dictionary<string, decimal>()
            };

            if (!string.IsNullOrEmpty(course.AssessmentScores))
            {
                try
                {
                    var assessmentData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(course.AssessmentScores);

                    decimal totalScore = 0;
                    decimal totalWeight = 0;

                    foreach (var assessment in assessmentData)
                    {
                        if (assessment.Value.TryGetProperty("assessment_name", out var nameElement) &&
                            assessment.Value.TryGetProperty("score", out var scoreElement))
                        {
                            var assessmentName = nameElement.GetString();
                            var score = scoreElement.GetDecimal();

                            result.AssessmentScores[assessmentName] = score;

                            // Find weight for this assessment
                            var courseAssessment = course.Course?.CourseAssessments
                                ?.FirstOrDefault(ca => ca.Assessment.Name == assessmentName);

                            var weight = courseAssessment?.Assessment.WeightPercentage ?? 0;
                            totalScore += (score * weight / 100);
                            totalWeight += weight;
                        }
                    }

                    // Normalize total score
                    result.TotalScore = totalWeight > 0 ? (totalScore / totalWeight) * 100 : 0;
                    result.Grade = DetermineGrade(result.TotalScore, grades);
                    result.IsPassed = result.TotalScore >= (decimal)(course.Course?.PassMark ?? 50);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing assessment scores for course {CourseId}", course.CourseId);
                }
            }

            return result;
        }

        private static string DetermineGrade(decimal totalScore, List<SIS.Models.ViewModels.GradeConfiguration> grades)
        {
            var grade = grades.FirstOrDefault(g => totalScore >= g.MinScore && totalScore <= g.MaxScore);
            return grade?.GradeLetter ?? "F";
        }

        #endregion



        [HttpGet]
        public async Task<IActionResult> StudentLists()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Get user's jurisdiction info for display
                var jurisdictionInfo = await GetUserJurisdictionInfo(user, primaryRole);

                // Get filter options based on user's jurisdiction
                var filterOptions = await GetUserFilterOptions(user, primaryRole);

                var viewModel = new StudentListsViewModel
                {
                    UserRole = primaryRole,
                    UserName = user.FullName,
                    JurisdictionInfo = jurisdictionInfo,
                    Schools = filterOptions.Schools,
                    Programmes = filterOptions.Programmes,
                    ModesOfStudy = filterOptions.ModesOfStudy,
                    ProgrammeLevels = filterOptions.ProgrammeLevels,
                    AcademicYears = filterOptions.AcademicYears,
                    PeriodOptions = filterOptions.PeriodOptions,
                    RegistrationStatuses = GetRegistrationStatusOptions()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student lists page");
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetFilteredStudents([FromBody] StudentListFiltersViewModel filters)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Build filtered query
                var studentsQuery = await BuildFilteredStudentsQuery(user, primaryRole, filters);

                // Get total count for pagination
                var totalCount = await studentsQuery.CountAsync();

                // Apply sorting
                studentsQuery = ApplySorting(studentsQuery, filters.SortBy, filters.SortDirection);

                // Apply pagination
                var students = await studentsQuery
                    .Skip((filters.Page - 1) * filters.PageSize)
                    .Take(filters.PageSize)
                    .Select(s => new FilteredStudentViewModel
                    {
                        Id = s.Id,
                        StudentNumber = s.StudentId_Number,
                        FullName = s.FullName,
                        Email = s.Email,
                        Phone = s.Phone,
                        ProgrammeName = s.Programme.Name,
                        SchoolName = s.School.Name,
                        DepartmentName = s.Programme.Department.Name,
                        ModeOfStudyName = s.ModeOfStudy.ModeName,
                        ProgrammeLevelName = s.ProgrammeLevel.Name,
                        AcademicYear = s.AcademicYear.YearValue,
                        CurrentYear = s.StudentCurrentYear ?? 0,
                        CurrentPeriodId = s.CurrentYearPeriodId ?? 0,
                        CurrentPeriodLabel = s.CurrentYearPeriod != null
                            ? s.CurrentYearPeriod.AcademicYear.YearValue + " - " + s.CurrentYearPeriod.AcademicPeriod.PeriodName
                            : "N/A",
                        StudentStatus = s.StudentStatus.ToString(),
                        RegistrationStatus = s.RegistrationStatus.ToString(),
                        IsRegistered = s.IsRegistered,
                        OutstandingFees = s.OutstandingFees,
                        RegistrationDate = s.RegistrationDate,
                        AdmissionDate = s.AdmissionDate,
                        Nationality = s.Nationality,
                        IsForeigner = s.IsForeigner,
                        StudyPermit = s.StudyPermits.FirstOrDefault()
                    })
                    .ToListAsync();

                var totalPages = (int)Math.Ceiling((double)totalCount / filters.PageSize);

                var result = new StudentListResultsViewModel
                {
                    Students = students,
                    TotalCount = totalCount,
                    Page = filters.Page,
                    PageSize = filters.PageSize,
                    TotalPages = totalPages,
                    HasPrevious = filters.Page > 1,
                    HasNext = filters.Page < totalPages,
                    AppliedFilters = filters
                };

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered students");
                return Json(new { success = false, message = "An error occurred while filtering students" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCascadingFilterOptions(int? schoolId, int? programmeId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                var result = new
                {
                    programmes = schoolId.HasValue ? await GetProgrammesBySchool(schoolId.Value) : new List<FilterOption>(),
                    modesOfStudy = programmeId.HasValue ? await GetModesByProgramme(programmeId.Value) : new List<FilterOption>()
                };

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cascading filter options");
                return Json(new { success = false, message = "Error loading filter options" });
            }
        }

        #region Helper Methods for Student Lists

        private async Task<StudentListsViewModel> GetUserFilterOptions(ApplicationUser user, string role)
        {
            var filterOptions = new StudentListsViewModel();

            // Give all roles access to all filter options
            var schoolsQuery = _context.Schools.AsQueryable();
            var programmesQuery = _context.Programmes.Include(p => p.Department).AsQueryable();

            // Load all filter options without jurisdiction filtering
            filterOptions.Schools = await schoolsQuery
                .Select(s => new FilterOption { Id = s.Id, Name = s.Name, Value = s.Id.ToString() })
                .ToListAsync();

            filterOptions.Programmes = await programmesQuery
                .Select(p => new FilterOption { Id = p.Id, Name = p.Name, Value = p.Id.ToString() })
                .ToListAsync();

            filterOptions.ModesOfStudy = await _context.ModesOfStudy
                .Select(m => new FilterOption { Id = m.ModeId, Name = m.ModeName, Value = m.ModeId.ToString() })
                .ToListAsync();

            filterOptions.ProgrammeLevels = await _context.ProgramLevels
                .Where(pl => pl.IsActive)
                .Select(pl => new FilterOption { Id = pl.Id, Name = pl.Name, Value = pl.Id.ToString() })
                .ToListAsync();

            filterOptions.AcademicYears = await _context.AcademicYears
                .OrderByDescending(ay => ay.YearValue)
                .Select(ay => new FilterOption { Id = ay.YearId, Name = ay.YearValue, Value = ay.YearId.ToString() })
                .ToListAsync();

            filterOptions.PeriodOptions = await _context.AcademicPeriods
                .Where(ap => ap.IsActive)
                .OrderBy(ap => ap.AcademicType)
                .ThenBy(ap => ap.PeriodNumber)
                .Select(ap => new FilterOption { Id = ap.Id, Name = ap.PeriodName, Value = ap.Id.ToString() })
                .ToListAsync();

            return filterOptions;
        }

        private async Task<IQueryable<Student>> BuildFilteredStudentsQuery(ApplicationUser user, string role, StudentListFiltersViewModel filters)
        {
            // Start with base jurisdiction query
            var query = await BuildStudentsQuery(user, role);

            // Apply additional filters
            if (filters.SchoolId.HasValue)
            {
                query = query.Where(s => s.SchoolId == filters.SchoolId.Value);
            }

            if (filters.ProgrammeId.HasValue)
            {
                query = query.Where(s => s.ProgrammeId == filters.ProgrammeId.Value);
            }

            if (filters.ModeOfStudyId.HasValue)
            {
                query = query.Where(s => s.ModeOfStudyId == filters.ModeOfStudyId.Value);
            }

            if (filters.ProgrammeLevelId.HasValue)
            {
                query = query.Where(s => s.ProgrammeLevelId == filters.ProgrammeLevelId.Value);
            }

            if (filters.AcademicYearId.HasValue)
            {
                query = query.Where(s => s.AcademicYearId == filters.AcademicYearId.Value);
            }

            if (filters.CurrentYear.HasValue)
            {
                query = query.Where(s => s.StudentCurrentYear == filters.CurrentYear.Value);
            }

            if (filters.CurrentPeriod.HasValue)
            {
                query = query.Where(s => s.CurrentYearPeriod != null &&
                                         s.CurrentYearPeriod.AcademicPeriodId == filters.CurrentPeriod.Value);
            }

            if (filters.RegistrationStatus.HasValue)
            {
                query = query.Where(s => s.RegistrationStatus == filters.RegistrationStatus.Value);
            }

            if (filters.IsRegistered.HasValue)
            {
                query = query.Where(s => s.IsRegistered == filters.IsRegistered.Value);
            }

            if (filters.HasOutstandingFees.HasValue)
            {
                if (filters.HasOutstandingFees.Value)
                {
                    query = query.Where(s => s.OutstandingFees > 0);
                }
                else
                {
                    query = query.Where(s => s.OutstandingFees <= 0);
                }
            }

            return query;
        }

        private static IQueryable<Student> ApplySorting(IQueryable<Student> query, string sortBy, string sortDirection)
        {
            var isDescending = sortDirection?.ToLower() == "desc";

            return sortBy?.ToLower() switch
            {
                "studentnumber" => isDescending ? query.OrderByDescending(s => s.StudentId_Number) : query.OrderBy(s => s.StudentId_Number),
                "fullname" => isDescending ? query.OrderByDescending(s => s.FullName) : query.OrderBy(s => s.FullName),
                "email" => isDescending ? query.OrderByDescending(s => s.Email) : query.OrderBy(s => s.Email),
                "programme" => isDescending ? query.OrderByDescending(s => s.Programme.Name) : query.OrderBy(s => s.Programme.Name),
                "school" => isDescending ? query.OrderByDescending(s => s.School.Name) : query.OrderBy(s => s.School.Name),
                "registrationstatus" => isDescending ? query.OrderByDescending(s => s.RegistrationStatus) : query.OrderBy(s => s.RegistrationStatus),
                "outstandingfees" => isDescending ? query.OrderByDescending(s => s.OutstandingFees) : query.OrderBy(s => s.OutstandingFees),
                "registrationdate" => isDescending ? query.OrderByDescending(s => s.RegistrationDate) : query.OrderBy(s => s.RegistrationDate),
                _ => isDescending ? query.OrderByDescending(s => s.FullName) : query.OrderBy(s => s.FullName)
            };
        }

        private async Task<List<FilterOption>> GetProgrammesBySchool(int schoolId)
        {
            return await _context.Programmes
                .Include(p => p.Department)
                .Where(p => p.Department.SchoolId == schoolId)
                .Select(p => new FilterOption { Id = p.Id, Name = p.Name, Value = p.Id.ToString() })
                .ToListAsync();
        }

        private async Task<List<FilterOption>> GetModesByProgramme(int programmeId)
        {
            var programme = await _context.Programmes.Include(p => p.ModeOfStudy)
                .FirstOrDefaultAsync(p => p.Id == programmeId);

            if (programme?.ModeOfStudy != null)
            {
                return new List<FilterOption>
        {
            new FilterOption { Id = programme.ModeOfStudy.ModeId, Name = programme.ModeOfStudy.ModeName, Value = programme.ModeOfStudy.ModeId.ToString() }
        };
            }

            return new List<FilterOption>();
        }

        private static List<FilterOption> GetRegistrationStatusOptions()
        {
            return Enum.GetValues<Status>()
                .Select(status => new FilterOption
                {
                    Id = (int)status,
                    Name = status.ToString(),
                    Value = status.ToString()
                })
                .ToList();
        }

        #endregion




        [HttpGet]
        public IActionResult GetExportColumnOptions()
        {
            try
            {
                var columnOptions = new List<ExportColumnOption>
        {
            new ExportColumnOption { Key = "StudentNumber", DisplayName = "Student Number", IsSelected = true },
            new ExportColumnOption { Key = "FullName", DisplayName = "Full Name", IsSelected = true },
            new ExportColumnOption { Key = "Email", DisplayName = "Email Address", IsSelected = true },
            new ExportColumnOption { Key = "Phone", DisplayName = "Phone Number", IsSelected = false },
            new ExportColumnOption { Key = "ProgrammeName", DisplayName = "Programme", IsSelected = true },
            new ExportColumnOption { Key = "SchoolName", DisplayName = "School", IsSelected = true },
            new ExportColumnOption { Key = "DepartmentName", DisplayName = "Department", IsSelected = false },
            new ExportColumnOption { Key = "ModeOfStudyName", DisplayName = "Mode of Study", IsSelected = false },
            new ExportColumnOption { Key = "ProgrammeLevelName", DisplayName = "Programme Level", IsSelected = false },
            new ExportColumnOption { Key = "AcademicYear", DisplayName = "Academic Year", IsSelected = true },
            new ExportColumnOption { Key = "CurrentYear", DisplayName = "Year of Study", IsSelected = true },
            new ExportColumnOption { Key = "CurrentSemester", DisplayName = "Academic Period", IsSelected = false },
            new ExportColumnOption { Key = "RegistrationStatus", DisplayName = "Registration Status", IsSelected = true },
            new ExportColumnOption { Key = "OutstandingFees", DisplayName = "Outstanding Fees", IsSelected = true },
            new ExportColumnOption { Key = "RegistrationDate", DisplayName = "Registration Date", IsSelected = false },
            new ExportColumnOption { Key = "NrcOrPassportNumber", DisplayName = "NRC/Passport Number", IsSelected = false },
            new ExportColumnOption { Key = "Gender", DisplayName = "Gender", IsSelected = false },
            new ExportColumnOption { Key = "Nationality", DisplayName = "Nationality", IsSelected = false },
        };

                return Json(new { success = true, columns = columnOptions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting export column options");
                return Json(new { success = false, message = "Error loading column options" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportStudentListToPdf([FromBody] StudentListExportRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Validate selected columns
                if (!request.SelectedColumns?.Any() == true)
                {
                    return Json(new { success = false, message = "Please select at least one column to export" });
                }

                // Get all filtered students (remove pagination for export)
                var exportFilters = request.Filters;
                exportFilters.Page = 1;
                exportFilters.PageSize = 5000; // Large number to get all results

                var studentsQuery = await BuildFilteredStudentsQuery(user, primaryRole, exportFilters);

                var students = await studentsQuery
                    .Select(s => new FilteredStudentViewModel
                    {
                        Id = s.Id,
                        StudentNumber = s.StudentId_Number,
                        FullName = s.FullName,
                        Email = s.Email,
                        Phone = s.Phone,
                        ProgrammeName = s.Programme.Name,
                        SchoolName = s.School.Name,
                        DepartmentName = s.Programme.Department.Name,
                        ModeOfStudyName = s.ModeOfStudy.ModeName,
                        ProgrammeLevelName = s.ProgrammeLevel.Name,
                        AcademicYear = s.AcademicYear.YearValue,
                        CurrentYear = s.StudentCurrentYear ?? 0,
                        CurrentPeriodId = s.CurrentYearPeriodId ?? 0,
                        CurrentPeriodLabel = s.CurrentYearPeriod != null
                            ? s.CurrentYearPeriod.AcademicYear.YearValue + " - " + s.CurrentYearPeriod.AcademicPeriod.PeriodName
                            : "N/A",
                        StudentStatus = s.StudentStatus.ToString(),
                        RegistrationStatus = s.RegistrationStatus.ToString(),
                        IsRegistered = s.IsRegistered,
                        OutstandingFees = s.OutstandingFees,
                        RegistrationDate = s.RegistrationDate,
                        AdmissionDate = s.AdmissionDate,
                        NrcOrPassportNumber = s.NrcOrPassportNumber,
                        Gender = s.Gender,
                        Nationality = s.Nationality,
                    })
                    .ToListAsync();

                if (!students.Any())
                {
                    return Json(new { success = false, message = "No students found with the current filters" });
                }

                // Prepare export options
                var exportOptions = new StudentListExportOptions
                {
                    Title = !string.IsNullOrEmpty(request.Title) ? request.Title : "Student List Report",
                    SelectedColumns = request.SelectedColumns,
                    GeneratedBy = user.FullName,
                    GeneratedDate = DateTime.Now,
                    TotalRecords = students.Count,
                    FilterSummary = BuildFilterSummary(request.Filters)
                };

                // Generate PDF
                var pdfBytes = await _pdfService.GenerateStudentListPdfAsync(students, exportOptions);

                // Generate filename
                var fileName = $"StudentList_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting student list to PDF");
                return Json(new { success = false, message = "An error occurred while generating the PDF export" });
            }
        }

        private Dictionary<string, string> BuildFilterSummary(StudentListFiltersViewModel filters)
        {
            var summary = new Dictionary<string, string>();

            try
            {
                if (filters.SchoolId.HasValue)
                {
                    var school = _context.Schools.Find(filters.SchoolId.Value);
                    if (school != null) summary["School"] = school.Name;
                }

                if (filters.ProgrammeId.HasValue)
                {
                    var programme = _context.Programmes.Find(filters.ProgrammeId.Value);
                    if (programme != null) summary["Programme"] = programme.Name;
                }

                if (filters.ModeOfStudyId.HasValue)
                {
                    var mode = _context.ModesOfStudy.Find(filters.ModeOfStudyId.Value);
                    if (mode != null) summary["Mode of Study"] = mode.ModeName;
                }

                if (filters.ProgrammeLevelId.HasValue)
                {
                    var level = _context.ProgramLevels.Find(filters.ProgrammeLevelId.Value);
                    if (level != null) summary["Programme Level"] = level.Name;
                }

                if (filters.AcademicYearId.HasValue)
                {
                    var year = _context.AcademicYears.Find(filters.AcademicYearId.Value);
                    if (year != null) summary["Academic Year"] = year.YearValue;
                }

                if (filters.CurrentYear.HasValue)
                    summary["Year of Study"] = $"Year {filters.CurrentYear.Value}";

                if (filters.CurrentPeriod.HasValue)
                {
                    var period = _context.AcademicPeriods.Find(filters.CurrentPeriod.Value);
                    if (period != null) summary["Academic Period"] = period.PeriodName;
                }

                if (filters.RegistrationStatus.HasValue)
                    summary["Registration Status"] = filters.RegistrationStatus.Value.ToString();

                if (filters.IsRegistered.HasValue)
                    summary["Registration"] = filters.IsRegistered.Value ? "Registered Only" : "Unregistered Only";

                if (filters.HasOutstandingFees.HasValue)
                    summary["Fee Status"] = filters.HasOutstandingFees.Value ? "Outstanding Fees" : "Fees Cleared";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error building filter summary");
            }

            return summary;
        }




        [HttpPost]
        public async Task<IActionResult> ExportStudentListToExcel([FromBody] StudentListExportRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Validate selected columns
                if (!request.SelectedColumns?.Any() == true)
                {
                    return Json(new { success = false, message = "Please select at least one column to export" });
                }

                // Get all filtered students (remove pagination for export)
                var exportFilters = request.Filters;
                exportFilters.Page = 1;
                exportFilters.PageSize = 5000; // Large number to get all results

                var studentsQuery = await BuildFilteredStudentsQuery(user, primaryRole, exportFilters);

                var students = await studentsQuery
                    .Select(s => new FilteredStudentViewModel
                    {
                        Id = s.Id,
                        StudentNumber = s.StudentId_Number,
                        FullName = s.FullName,
                        Email = s.Email,
                        Phone = s.Phone,
                        ProgrammeName = s.Programme.Name,
                        SchoolName = s.School.Name,
                        DepartmentName = s.Programme.Department.Name,
                        ModeOfStudyName = s.ModeOfStudy.ModeName,
                        ProgrammeLevelName = s.ProgrammeLevel.Name,
                        AcademicYear = s.AcademicYear.YearValue,
                        CurrentYear = s.StudentCurrentYear ?? 0,
                        CurrentPeriodId = s.CurrentYearPeriodId ?? 0,
                        CurrentPeriodLabel = s.CurrentYearPeriod != null
                            ? s.CurrentYearPeriod.AcademicYear.YearValue + " - " + s.CurrentYearPeriod.AcademicPeriod.PeriodName
                            : "N/A",
                        StudentStatus = s.StudentStatus.ToString(),
                        RegistrationStatus = s.RegistrationStatus.ToString(),
                        IsRegistered = s.IsRegistered,
                        OutstandingFees = s.OutstandingFees,
                        RegistrationDate = s.RegistrationDate,
                        AdmissionDate = s.AdmissionDate,
                        NrcOrPassportNumber = s.NrcOrPassportNumber,
                        Gender = s.Gender,
                        Nationality = s.Nationality,
                    })
                    .ToListAsync();

                if (!students.Any())
                {
                    return Json(new { success = false, message = "No students found with the current filters" });
                }

                // Prepare export options
                var exportOptions = new StudentListExportOptions
                {
                    Title = !string.IsNullOrEmpty(request.Title) ? request.Title : "Student List Report",
                    SelectedColumns = request.SelectedColumns,
                    GeneratedBy = user.FullName,
                    GeneratedDate = DateTime.Now,
                    TotalRecords = students.Count,
                    FilterSummary = BuildFilterSummary(request.Filters)
                };

                // Generate Excel
                var excelBytes = await _pdfService.GenerateStudentListExcelAsync(students, exportOptions);

                // Generate filename
                var fileName = $"StudentList_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting student list to Excel");
                return Json(new { success = false, message = "An error occurred while generating the Excel export" });
            }
        }

        [HttpGet]
        [Route("StudentLookup/PreviewFile")]
        public IActionResult PreviewFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return BadRequest("File path is required");
            }

            try
            {
                string fullPath;

                // Handle both old and new photo storage paths
                if (Path.IsPathRooted(filePath))
                {
                    // Absolute path (from application process or updated admin process)
                    fullPath = filePath;
                }
                else
                {
                    // Relative path (from old admin storage)
                    fullPath = Path.Combine(_webHostEnvironment.WebRootPath, filePath.TrimStart('/'));

                    // If not found in wwwroot, try the application uploads directory
                    if (!System.IO.File.Exists(fullPath))
                    {
                        fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath.TrimStart('/'));
                    }
                }

                // Security check: Validate and sanitize the file path
                var resolvedPath = Path.GetFullPath(fullPath);

                // Log the path for debugging
                _logger.LogInformation($"Attempting to access student photo at: {resolvedPath}");

                // Ensure the file exists
                if (!System.IO.File.Exists(resolvedPath))
                {
                    _logger.LogWarning($"Student photo not found at: {resolvedPath}");
                    return NotFound($"File not found");
                }

                // Determine content type based on file extension
                string contentType;
                var extension = Path.GetExtension(resolvedPath).ToLower();

                switch (extension)
                {
                    case ".pdf":
                        contentType = "application/pdf";
                        break;
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
                        contentType = "application/octet-stream";
                        break;
                }

                // Return file with content type
                return PhysicalFile(resolvedPath, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error accessing student photo file: {filePath}");
                return StatusCode(500, $"Error accessing file");
            }
        }


        // Add these new methods to your StudentLookupController class

        [HttpGet("StudentLookup/GetStudentPhoto/{studentId:int}")]
        public async Task<IActionResult> GetStudentPhoto(int studentId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Verify user can access this student
                if (!await CanAccessStudent(user, studentId))
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                // Check if photo exists
                var photoPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "student-photos", $"{student.StudentId_Number}.png");
                var hasExistingPhoto = System.IO.File.Exists(photoPath);

                var photoViewModel = new StudentPhotoViewModel
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentId_Number,
                    StudentName = student.FullName,
                    HasExistingPhoto = hasExistingPhoto
                };

                return PartialView("_StudentPhoto", photoViewModel);  // ✅ CORRECT NAME
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student photo tab for ID: {StudentId}", studentId);
                return StatusCode(500, "<div class='p-4 text-red-600'>Error loading photo tab</div>");
            }
        }

        [HttpPost("StudentLookup/ReprintIdCard/{studentId:int}")]
        public async Task<IActionResult> ReprintIdCard(int studentId)
        {
            try
            {
                return Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error loading student photo tab for ID: {StudentId}", studentId);
                return StatusCode(500, "<div class='p-4 text-red-600'>Error reprinting ID Card</div>");
            }
        }

        private IdCardDisplaySettings GetIdCardDisplaySettings()
        {
            var institution = _institutionConfig.GetCurrentInstitution();
            var idCard = institution.IdCard;

            var institutionName = !string.IsNullOrWhiteSpace(idCard?.InstitutionName)
                ? idCard.InstitutionName
                : institution.Name;

            var fallbackContactDetails = string.Join("\n", new[]
            {
                institution.ContactInfo?.Phone,
                institution.EmailSettings?.SenderEmail
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            var contactDetails = !string.IsNullOrWhiteSpace(idCard?.ContactDetails)
                ? idCard.ContactDetails
                : fallbackContactDetails;

            return new IdCardDisplaySettings
            {
                InstitutionName = institutionName ?? "Institution",
                InstitutionNameHtml = WebUtility.HtmlEncode(institutionName ?? "Institution"),
                ContactDetails = contactDetails ?? string.Empty,
                ContactDetailsHtml = ToHtmlLines(contactDetails),
                PrimaryColor = NormalizeHexColor(idCard?.PrimaryColor, institution.BrandColors?.Primary ?? "#1991cf"),
                HeaderTextColor = NormalizeHexColor(idCard?.HeaderTextColor, "#ffffff"),
                LogoPath = WebUtility.HtmlEncode(institution.LogoPath ?? "/images/institution-logo.png")
            };
        }

        private static string ToHtmlLines(string value)
        {
            return string.Join("<br />", (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => WebUtility.HtmlEncode(line.Trim())));
        }

        private static string NormalizeHexColor(string value, string fallback)
        {
            return Regex.IsMatch(value ?? string.Empty, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
                ? value
                : fallback;
        }

        [HttpGet("StudentLookup/GetStudentIDCard/{studentId:int}")]
        public async Task<IActionResult> GetStudentIDCard(int studentId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Verify user can access this student
                if (!await CanAccessStudent(user, studentId))
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var student = await _context.Students.Include(s => s.Programme)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                // Check if photo exists
                var photoPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "student-photos", $"{student.StudentId_Number}.png");
                var hasExistingPhoto = System.IO.File.Exists(photoPath);

                var photoViewModel = new StudentPhotoViewModel
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentId_Number,
                    StudentName = student.FullName,
                    HasExistingPhoto = hasExistingPhoto,
                    IdCardPrintedDate = student.IdCardPrintedDate
                };

                string[] nameParts = student.FullName.Trim().Split(" ");
                string firstName = nameParts[0];
                string lastName = nameParts.Last();

                DateTime futureDate = DateTime.Today.AddYears(4);
                string date = futureDate.ToString("dd-MMM-yyyy");

                // Get middle initial if it exists
                string middleInitial = "";

                if (nameParts.Length > 2)
                {
                    var middle = nameParts[1].Trim();
                    if (!string.IsNullOrEmpty(middle))
                    {
                        middleInitial = middle[0].ToString().ToUpper() + ".";
                    }
                }

                var idCardSettings = GetIdCardDisplaySettings();

                string htmlTemplate = @"
                                        <!DOCTYPE html>
                                        <html lang=""en"">
                                        <head>
                                            <meta charset=""UTF-8"">
                                            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                                            <title>ID Card Preview</title>
                                            <style>
                                                body {
                                                    font-family: Arial, sans-serif;
                                                    background-color: #f5f5f5;
                                                    padding: 20px;
                                                    margin: 0;
                                                }
                                                .preview-container {
                                                    display: flex;
                                                    flex-direction: column;
                                                    align-items: center;
                                                    gap: 20px;
                                                }
                                                .card {
                                                    box-shadow: 0 4px 8px rgba(0,0,0,0.1);
                                                    background-color: white;
                                                    margin-bottom: 20px;
                                                }

                                                @media print {
                                                    body {
                                                        background: white !important;
                                                        padding: 0 !important;
                                                        margin: 0 !important;
                                                        width: 100% !important;
                                                    }
                                                    .preview-container {
                                                        gap: 0 !important;
                                                        padding: 0 !important;
                                                        margin: 0 !important;
                                                        align-items: flex-start !important;
                                                    }
                                                    .card {
                                                        box-shadow: none !important;
                                                        margin: 0 !important;
                                                        padding: 0 !important;
                                                        page-break-inside: avoid;
                                                    }

                                                    .card[style*=""padding: 15px""] {
                                                        padding: 0 !important;
                                                        height: auto !important;
                                                    }
                                                    .card[style*=""padding: 20px""] {
                                                        padding: 0 !important;
                                                    }
                                                    
                                                    .university {
                                                        background-color: {PrimaryColor} !important;
                                                        color: {HeaderTextColor} !important;
                                                        -webkit-print-color-adjust: exact !important;
                                                        print-color-adjust: exact !important;
                                                    }
                                                    
                                                    * {
                                                        -webkit-print-color-adjust: exact !important;
                                                        print-color-adjust: exact !important;
                                                    }
                                                }

                                                @page {
                                                    margin: 0 !important;
                                                    padding: 0 !important;
                                                    size: auto;
                                                }
                                            </style>
                                        </head>
                                        <body>
                                            <div class=""preview-container"">
                                                <div class=""card"" style=""
                                                    padding: 15px;
                                                    height: 250px;
                                                    break-inside: avoid;
                                                    text-align: center;
                                                    width: 330pt;
                                                "">
                                                    <div class=""university"" style=""
                                                        font-size: 19pt;
                                                        margin-right: 37px;
                                                        margin-bottom: 5px;
                                                        padding: 2pt;
                                                        text-align: center;
                                                        font-weight: bold;
                                                        color: {HeaderTextColor};
                                                        font-family: arial;
                                                        background-color: {PrimaryColor} !important;
                                                    "">
                                                        {CardType}
                                                    </div>
                                                    <div class=""subtitle"" style=""text-align: center; margin-right: 27px"">
                                                        <span style=""
                                                            font-size: 14pt;
                                                            font-weight: bold;
                                                            margin-right: 37px;
                                                            color: #000;
                                                            text-align: center;
                                                            font-family: arial;
                                                            padding: 2pt;
                                                            padding-bottom: 0px;
                                                        "">
                                                            {InstitutionName}
                                                        </span>
                                                    </div>

                                                    <div style=""
                                                        width: 140px;
                                                        height: 167px;
                                                        text-align: center;
                                                        overflow: hidden;
                                                        float: left;
                                                        margin-top: 0px;
                                                    "">
                                                        {ProfileImage}
                                                    </div>
                                                    <div style=""width: 200pt; float: left; padding-left: 20pt"">
                                                        <div style=""
                                                            width: 100px;
                                                            float: left;
                                                            text-align: center;
                                                            font-family: arial;
                                                            padding-top: 15px;
                                                        "">
                                                            <img width=""92"" src=""{LogoPath}"" />
                                                        </div>

                                                        <div class=""name"" style=""
                                                            padding-top: {NamePadding};
                                                            padding-left: 10px;
                                                            width: 150px;
                                                            float: left;
                                                            padding-bottom: 10pt;
                                                            font-weight: bold;
                                                            font-size: 11pt;
                                                            text-align: left;
                                                            font-family: arial;
                                                            text-align: left;
                                                            color: #000;
                                                        "">
                                                            {FirstName} {MiddleInitial}<br />
                                                            <span style=""font-size: 11pt; color: #000; font-weight: bold"">
                                                                {Surname}
                                                            </span>
                                                        </div>
                                                        <div style=""padding-top: 10px; clear: both"">
                                                            <div class=""id"" style=""
                                                                font-family: arial;
                                                                color: #000;
                                                                float: left;
                                                                font-size: 13pt;
                                                                width: 50pt;
                                                                text-align: left;
                                                            "">
                                                                ID:
                                                            </div>
                                                            <div class=""studentid"" style=""
                                                                width: 140px;
                                                                font-size: 13pt;
                                                                color: #000;
                                                                font-family: arial;
                                                                font-weight: bold;
                                                                float: left;
                                                                text-align: left;
                                                            "">
                                                                {StudentId}
                                                            </div>
                                                            <br />
                                                            <div class=""date"" style=""
                                                                font-family: arial;
                                                                color: #000;
                                                                float: left;
                                                                font-size: 13pt;
                                                                width: 50pt;
                                                                text-align: left;
                                                            "">
                                                                EXP:
                                                            </div>
                                                            <div class=""studentid"" style=""
                                                                font-size: 13pt;
                                                                color: #000;
                                                                font-family: arial;
                                                                font-weight: bold;
                                                                float: left;
                                                                width: 140px;
                                                                text-align: left;
                                                            "">
                                                                {ExpiryDate}
                                                            </div>
                                                            <div class=""date"" style=""
                                                                font-family: arial;
                                                                color: #000;
                                                                float: left;
                                                                font-size: 13pt;
                                                                width: 50pt;
                                                                text-align: left;
                                                            "">
                                                                NRC:
                                                            </div>
                                                            <div class=""studentid"" style=""
                                                                font-size: 13pt;
                                                                color: #000;
                                                                font-family: arial;
                                                                font-weight: bold;
                                                                width: 140px;
                                                                float: left;
                                                                text-align: left;
                                                            "">
                                                                {NrcNumber}
                                                            </div>
                                                            <div class=""date"" style=""
                                                                width: 400px;
                                                                margin-left: -170px;
                                                                font-family: arial;
                                                                color: #000;
                                                                float: left;
                                                                font-size: 13pt;
                                                                text-align: center;
                                                                border-top: 2px solid #000;
                                                                margin-top: 4px;
                                                                padding-top: 2px;
                                                                font-weight: bold;
                                                            "">
                                                                {Program}
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                <div class=""card"" style=""
                                                    page-break-before: always;
                                                    color: #000;
                                                    text-align: center;
                                                    width: 350pt;
                                                    padding: 20px;
                                                "">
                                                    <br /><br />
                                                    <div style=""
                                                        font-size: 15pt;
                                                        font-family: arial;
                                                        font-weight: bold;
                                                        color: #000;
                                                        padding-bottom: 0px;
                                                    "">
                                                        THIS CARD IS PROPERTY OF<br />
                                                        {InstitutionName}<br />IF FOUND PLEASE CONTACT:<br />{ContactDetails}
                                                    </div>
                                                    <br />
                                                    <img src=""/StudentLookup/Barcode/{BarcodeId}"" />
                                                    <!--<br />
                                                    <div style=""font-size: 15pt"">{BarcodeText}</div>-->
                                                </div>
                                            </div>
                                        </body>
                                        </html>";

                string filledHtml = htmlTemplate
                                                .Replace("{CardType}", "STUDENT ID")
                                                .Replace("{InstitutionName}", idCardSettings.InstitutionNameHtml.ToUpper())
                                                .Replace("{ContactDetails}", idCardSettings.ContactDetailsHtml)
                                                .Replace("{PrimaryColor}", idCardSettings.PrimaryColor)
                                                .Replace("{HeaderTextColor}", idCardSettings.HeaderTextColor)
                                                .Replace("{LogoPath}", idCardSettings.LogoPath)
                                                .Replace("{ProfileImage}", $"<div style='background-color:#ffffff; height:157px; display:flex; align-items:center; justify-content:center;'><img src='/uploads/student-photos/{student.StudentId_Number}.png' style='max-width:100%; max-height:100%; object-fit:contain;' /></div>")
                                                .Replace("{NamePadding}", "10px")
                                                .Replace("{FirstName}", WebUtility.HtmlEncode(firstName.ToUpper()))
                                                .Replace("{MiddleInitial}", WebUtility.HtmlEncode(middleInitial.ToUpper()))
                                                .Replace("{Surname}", WebUtility.HtmlEncode(lastName.ToUpper()))
                                                .Replace("{SurnameFontSize}", "18pt")
                                                .Replace("{StudentId}", WebUtility.HtmlEncode(student.StudentId_Number.ToString()))
                                                .Replace("{ExpiryDate}", date)
                                                .Replace("{NrcNumber}", WebUtility.HtmlEncode(student.NrcOrPassportNumber))
                                                .Replace("{Program}", WebUtility.HtmlEncode(student.Programme.Name))
                                                .Replace("{BarcodeId}", WebUtility.HtmlEncode(student.StudentId_Number.ToString()))
                                                .Replace("{BarcodeText}", WebUtility.HtmlEncode(student.StudentId_Number.ToString()));

                ViewBag.filledHtml = filledHtml;
                ViewBag.IdCardInstitutionName = idCardSettings.InstitutionName;
                ViewBag.IdCardContactDetails = idCardSettings.ContactDetails;
                ViewBag.IdCardPrimaryColor = idCardSettings.PrimaryColor;
                ViewBag.IdCardHeaderTextColor = idCardSettings.HeaderTextColor;

                return PartialView("_StudentIDCard", photoViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student photo tab for ID: {StudentId}", studentId);
                return StatusCode(500, "<div class='p-4 text-red-600'>Error loading ID Card</div>");
            }
        }

        [HttpGet("StudentLookup/Cards")]
        public async Task<IActionResult> Cards()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Get user's jurisdiction info for display
                var jurisdictionInfo = await GetUserJurisdictionInfo(user, primaryRole);

                var viewModel = new DocketLookupIndexViewModel
                {
                    UserRole = primaryRole,
                    UserName = user.FullName,
                    JurisdictionInfo = jurisdictionInfo,
                    SearchTypes = new List<string> { "StudentNumber", "Name", "NrcPassport", "Email" }
                };

                ViewData["programmes"] = await _context.Programmes.OrderBy(p => p.Name).ToListAsync();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student lookup dashboard");
                return RedirectToAction("Error", "Home");
            }
        }

        /*[HttpPost("StudentLookup/CardsLookup")]
        public async Task<IActionResult> CardsLookup(string semester, string year, string programme)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Get user's jurisdiction info for display
                var jurisdictionInfo = await GetUserJurisdictionInfo(user, primaryRole);

                var students = await _context.Students.Include(s => s.Programme)
                                                        .Where(s => s.CurrentSemester == Int32.Parse(semester) && s.StudentCurrentYear == Int32.Parse(year) 
                                                        && s.ProgrammeId == Int32.Parse(programme))
                                                        .ToListAsync();

                string htmlTemplate = @"
                                        <!DOCTYPE html>
                                        <html lang=""en"">
                                        <head>
                                            <meta charset=""UTF-8"">
                                            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                                            <title>ID Card Preview</title>
                                            <style>
                                                body {
                                                    font-family: Arial, sans-serif;
                                                    background-color: #f5f5f5;
                                                    padding: 20px;
                                                    margin: 0;
                                                }
                                                .preview-container {
                                                    display: flex;
                                                    flex-direction: column;
                                                    align-items: center;
                                                    gap: 20px;
                                                }
                                                .card {
                                                    box-shadow: 0 4px 8px rgba(0,0,0,0.1);
                                                    background-color: white;
                                                    margin-bottom: 20px;
                                                }

                                                @media print {
                                                    body {
                                                        background: white !important;
                                                        padding: 0 !important;
                                                        margin: 0 !important;
                                                        width: 100% !important;
                                                    }
                                                    .preview-container {
                                                        gap: 0 !important;
                                                        padding: 0 !important;
                                                        margin: 0 !important;
                                                        align-items: flex-start !important;
                                                    }
                                                    .card {
                                                        box-shadow: none !important;
                                                        margin: 0 !important;
                                                        padding: 0 !important;
                                                        page-break-inside: avoid;
                                                    }

                                                    .card[style*=""padding: 15px""] {
                                                        padding: 0 !important;
                                                        height: auto !important;
                                                    }
                                                    .card[style*=""padding: 20px""] {
                                                        padding: 0 !important;
                                                    }
                                                    
                                                    .university {
                                                        background-color: #1991cf !important;
                                                        color: #fff !important;
                                                        -webkit-print-color-adjust: exact !important;
                                                        print-color-adjust: exact !important;
                                                    }
                                                    
                                                    * {
                                                        -webkit-print-color-adjust: exact !important;
                                                        print-color-adjust: exact !important;
                                                    }
                                                }

                                                @page {
                                                    margin: 0 !important;
                                                    padding: 0 !important;
                                                    size: auto;
                                                }
                                            </style>
                                        </head>
                                        <body>
                                            <div class=""preview-container"">
                                                <div class=""card"" style=""
                                                    padding: 15px;
                                                    height: 250px;
                                                    break-inside: avoid;
                                                    text-align: center;
                                                    width: 330pt;
                                                "">
                                                    <div class=""university"" style=""
                                                        font-size: 19pt;
                                                        margin-right: 37px;
                                                        margin-bottom: 5px;
                                                        padding: 2pt;
                                                        text-align: center;
                                                        font-weight: bold;
                                                        color: #fff;
                                                        font-family: arial;
                                                        background-color: #1991cf !important;
                                                    "">
                                                        {CardType}
                                                    </div>
                                                    <div class=""subtitle"" style=""text-align: center; margin-right: 27px"">
                                                        <span style=""
                                                            font-size: 14pt;
                                                            font-weight: bold;
                                                            margin-right: 37px;
                                                            color: #000;
                                                            text-align: center;
                                                            font-family: arial;
                                                            padding: 2pt;
                                                            padding-bottom: 0px;
                                                        "">
                                                            EDEN UNIVERSITY
                                                        </span>
                                                    </div>

                                                    <div style=""
                                                        width: 140px;
                                                        height: 167px;
                                                        text-align: center;
                                                        overflow: hidden;
                                                        float: left;
                                                        margin-top: 0px;
                                                    "">
                                                        {ProfileImage}
                                                    </div>
                                                    <div style=""width: 200pt; float: left; padding-left: 20pt"">
                                                        <div style=""
                                                            width: 100px;
                                                            float: left;
                                                            text-align: center;
                                                            font-family: arial;
                                                            padding-top: 15px;
                                                        "">
                                                            <img width=""92"" src=""/images/institution-logo.png"" />
                                                        </div>

                                                        <div class=""name"" style=""
                                                            padding-top: {NamePadding};
                                                            padding-left: 10px;
                                                            width: 150px;
                                                            float: left;
                                                            padding-bottom: 10pt;
                                                            font-weight: bold;
                                                            font-size: 15pt;
                                                            text-align: left;
                                                            font-family: arial;
                                                            text-align: left;
                                                            color: #000;
                                                        "">
                                                            {FirstName} {MiddleInitial}<br />
                                                            <span style=""font-size: {SurnameFontSize}; color: #000; font-weight: bold"">
                                                                {Surname}
                                                            </span>
                                                        </div>
                                                        <div style=""padding-top: 10px; clear: both"">
                                                            <div class=""id"" style=""
                                                                font-family: arial;
                                                                color: #000;
                                                                float: left;
                                                                font-size: 13pt;
                                                                width: 50pt;
                                                                text-align: left;
                                                            "">
                                                                ID:
                                                            </div>
                                                            <div class=""studentid"" style=""
                                                                width: 140px;
                                                                font-size: 13pt;
                                                                color: #000;
                                                                font-family: arial;
                                                                font-weight: bold;
                                                                float: left;
                                                                text-align: left;
                                                            "">
                                                                {StudentId}
                                                            </div>
                                                            <br />
                                                            <div class=""date"" style=""
                                                                font-family: arial;
                                                                color: #000;
                                                                float: left;
                                                                font-size: 13pt;
                                                                width: 50pt;
                                                                text-align: left;
                                                            "">
                                                                EXP:
                                                            </div>
                                                            <div class=""studentid"" style=""
                                                                font-size: 13pt;
                                                                color: #000;
                                                                font-family: arial;
                                                                font-weight: bold;
                                                                float: left;
                                                                width: 140px;
                                                                text-align: left;
                                                            "">
                                                                {ExpiryDate}
                                                            </div>
                                                            <div class=""date"" style=""
                                                                font-family: arial;
                                                                color: #000;
                                                                float: left;
                                                                font-size: 13pt;
                                                                width: 50pt;
                                                                text-align: left;
                                                            "">
                                                                NRC:
                                                            </div>
                                                            <div class=""studentid"" style=""
                                                                font-size: 13pt;
                                                                color: #000;
                                                                font-family: arial;
                                                                font-weight: bold;
                                                                width: 140px;
                                                                float: left;
                                                                text-align: left;
                                                            "">
                                                                {NrcNumber}
                                                            </div>
                                                            <div class=""date"" style=""
                                                                width: 400px;
                                                                margin-left: -170px;
                                                                font-family: arial;
                                                                color: #000;
                                                                float: left;
                                                                font-size: 13pt;
                                                                text-align: center;
                                                                border-top: 2px solid #000;
                                                                margin-top: 4px;
                                                                padding-top: 2px;
                                                                font-weight: bold;
                                                            "">
                                                                {Program}
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                <div class=""card"" style=""
                                                    page-break-before: always;
                                                    color: #000;
                                                    text-align: center;
                                                    width: 350pt;
                                                    padding: 20px;
                                                "">
                                                    <br /><br />
                                                    <div style=""
                                                        font-size: 15pt;
                                                        font-family: arial;
                                                        font-weight: bold;
                                                        color: #000;
                                                        padding-bottom: 0px;
                                                    "">
                                                        THIS CARD IS PROPERTY OF<br />
                                                        EDEN UNIVERSITY <br />IF FOUND PLEASE CONTACT: <br />+260-211843535 or
                                                        registrar@edenuniversity.education
                                                    </div>
                                                    <br />
                                                    <img src=""/StudentLookup/Barcode/{BarcodeId}"" />
                                                    <!--<br />
                                                    <div style=""font-size: 15pt"">{BarcodeText}</div>-->
                                                </div>
                                            </div>
                                        </body>
                                        </html>";
                string filledHtml = string.Empty;
                foreach(var student in students)
                {
                    string[] nameParts = student.FullName.Split(' ');
                    string firstName = nameParts[0];
                    string lastName = nameParts.Last();

                    DateTime futureDate = DateTime.Today.AddYears(4);
                    string date = futureDate.ToString("dd-MMM-yyyy");

                    // Get middle initial if it exists
                    string middleInitial = nameParts.Length > 2 ? nameParts[1][0].ToString() + "." : "";

                    string filledHtmlTmp = htmlTemplate
                                                .Replace("{CardType}", "STUDENT ID")
                                                .Replace("{ProfileImage}", $"<div style='background-color:#ffffff; height:157px; display:flex; align-items:center; justify-content:center;'><img src='/uploads/student-photos/{student.StudentId_Number}.png' style='max-width:100%; max-height:100%; object-fit:contain;' /></div>")
                                                .Replace("{NamePadding}", "10px")
                                                .Replace("{FirstName}", firstName.ToUpper())
                                                .Replace("{MiddleInitial}", middleInitial.ToUpper())
                                                .Replace("{Surname}", lastName.ToUpper())
                                                .Replace("{SurnameFontSize}", "18pt")
                                                .Replace("{StudentId}", student.StudentId_Number.ToString())
                                                .Replace("{ExpiryDate}", date)
                                                .Replace("{NrcNumber}", student.NrcOrPassportNumber)
                                                .Replace("{Program}", student.Programme.Name)
                                                .Replace("{BarcodeId}", student.StudentId_Number.ToString())
                                                .Replace("{BarcodeText}", student.StudentId_Number.ToString());

                    filledHtml += filledHtmlTmp;
                }

                return Json(new { success = true, students = students, filledHtml = filledHtml });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student lookup dashboard");
                return RedirectToAction("Error", "Home");
            }
        }*/

        [HttpPost("StudentLookup/CardsLookup")]
        public async Task<IActionResult> CardsLookup(string period, string year, string programme)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Get user's jurisdiction info for display
                var jurisdictionInfo = await GetUserJurisdictionInfo(user, primaryRole);

                var students = await _context.Students.Include(s => s.Programme)
                                                        .Include(s => s.School)
                                                        .Include(s => s.AcademicYear)
                                                        .Include(s => s.CurrentYearPeriod)
                                                        .ThenInclude(p => p.AcademicPeriod)
                                                        .Where(s => s.CurrentYearPeriod.AcademicPeriod.Id == Int32.Parse(period) && s.StudentCurrentYear == Int32.Parse(year)
                                                        && s.ProgrammeId == Int32.Parse(programme) && s.IdCardPrintedDate == null)
                                                        .ToListAsync();

                var idCardSettings = GetIdCardDisplaySettings();

                string cardTemplate = @"
                                        <div class=""preview-container"">
                                            <!-- FRONT OF CARD -->
                                            <div class=""card"" style=""
                                                padding: 15px;
                                                height: 250px;
                                                break-inside: avoid;
                                                text-align: center;
                                                width: 330pt;
                                            "">
                                                <div class=""university"" style=""
                                                    font-size: 19pt;
                                                    margin-right: 37px;
                                                    margin-bottom: 5px;
                                                    padding: 2pt;
                                                    text-align: center;
                                                    font-weight: bold;
                                                    color: {HeaderTextColor};
                                                    font-family: arial;
                                                    background-color: {PrimaryColor} !important;
                                                "">
                                                    STUDENT ID
                                                </div>
                                                <div class=""subtitle"" style=""text-align: center; margin-right: 27px"">
                                                    <span style=""
                                                        font-size: 14pt;
                                                        font-weight: bold;
                                                        margin-right: 37px;
                                                        color: #000;
                                                        text-align: center;
                                                        font-family: arial;
                                                        padding: 2pt;
                                                        padding-bottom: 0px;
                                                    "">
                                                        {InstitutionName}
                                                    </span>
                                                </div>

                                                <div style=""
                                                    width: 140px;
                                                    height: 167px;
                                                    text-align: center;
                                                    overflow: hidden;
                                                    float: left;
                                                    margin-top: 0px;
                                                "">
                                                    <div style='background-color:#ffffff; height:157px; display:flex; align-items:center; justify-content:center;'>
                                                        <img src='/uploads/student-photos/{StudentId}.png' style='max-width:100%; max-height:100%; object-fit:contain;' />
                                                    </div>
                                                </div>
                                                <div style=""width: 200pt; float: left; padding-left: 20pt"">
                                                    <div style=""
                                                        width: 100px;
                                                        float: left;
                                                        text-align: center;
                                                        font-family: arial;
                                                        padding-top: 15px;
                                                    "">
                                                        <img width=""92"" src=""{LogoPath}"" />
                                                    </div>

                                                    <div class=""name"" style=""
                                                        padding-top: 10px;
                                                        padding-left: 10px;
                                                        width: 150px;
                                                        float: left;
                                                        padding-bottom: 10pt;
                                                        font-weight: bold;
                                                        font-size: 11pt;
                                                        text-align: left;
                                                        font-family: arial;
                                                        text-align: left;
                                                        color: #000;
                                                    "">
                                                        {FirstName} {MiddleInitial}<br />
                                                        <span style=""font-size: 11pt; color: #000; font-weight: bold"">
                                                            {Surname}
                                                        </span>
                                                    </div>
                                                    <div style=""padding-top: 10px; clear: both"">
                                                        <div class=""id"" style=""
                                                            font-family: arial;
                                                            color: #000;
                                                            float: left;
                                                            font-size: 13pt;
                                                            width: 50pt;
                                                            text-align: left;
                                                        "">
                                                            ID:
                                                        </div>
                                                        <div class=""studentid"" style=""
                                                            width: 140px;
                                                            font-size: 13pt;
                                                            color: #000;
                                                            font-family: arial;
                                                            font-weight: bold;
                                                            float: left;
                                                            text-align: left;
                                                        "">
                                                            {StudentId}
                                                        </div>
                                                        <br />
                                                        <div class=""date"" style=""
                                                            font-family: arial;
                                                            color: #000;
                                                            float: left;
                                                            font-size: 13pt;
                                                            width: 50pt;
                                                            text-align: left;
                                                        "">
                                                            EXP:
                                                        </div>
                                                        <div class=""studentid"" style=""
                                                            font-size: 13pt;
                                                            color: #000;
                                                            font-family: arial;
                                                            font-weight: bold;
                                                            float: left;
                                                            width: 140px;
                                                            text-align: left;
                                                        "">
                                                            {ExpiryDate}
                                                        </div>
                                                        <div class=""date"" style=""
                                                            font-family: arial;
                                                            color: #000;
                                                            float: left;
                                                            font-size: 13pt;
                                                            width: 50pt;
                                                            text-align: left;
                                                        "">
                                                            NRC:
                                                        </div>
                                                        <div class=""studentid"" style=""
                                                            font-size: 13pt;
                                                            color: #000;
                                                            font-family: arial;
                                                            font-weight: bold;
                                                            width: 140px;
                                                            float: left;
                                                            text-align: left;
                                                        "">
                                                            {NrcNumber}
                                                        </div>
                                                        <div class=""date"" style=""
                                                            width: 400px;
                                                            margin-left: -170px;
                                                            font-family: arial;
                                                            color: #000;
                                                            float: left;
                                                            font-size: 11pt;
                                                            text-align: center;
                                                            border-top: 2px solid #000;
                                                            margin-top: 4px;
                                                            padding-top: 2px;
                                                            font-weight: bold;
                                                        "">
                                                            {Program}
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- BACK OF CARD -->
                                            <div class=""card"" style=""
                                                page-break-before: always;
                                                color: #000;
                                                text-align: center;
                                                width: 350pt;
                                                padding: 20px;
                                            "">
                                                <br /><br />
                                                <div style=""
                                                    font-size: 15pt;
                                                    font-family: arial;
                                                    font-weight: bold;
                                                    color: #000;
                                                    padding-bottom: 0px;
                                                "">
                                                    THIS CARD IS PROPERTY OF<br />
                                                    {InstitutionName}<br />IF FOUND PLEASE CONTACT:<br />{ContactDetails}
                                                </div>
                                                <br />
                                                <img src=""/StudentLookup/Barcode/{StudentId}"" />
                                                <!--<br />
                                                <div style=""font-size: 15pt"">{StudentId}</div>-->
                                            </div>
                                        </div>";

                StringBuilder cardsHtml = new StringBuilder();

                foreach (var student in students)
                {
                    string[] nameParts = student.FullName.Trim().Split(" ");
                    string firstName = nameParts[0];
                    string lastName = nameParts.Last();

                    DateTime futureDate = DateTime.Today.AddYears(4);
                    string date = futureDate.ToString("dd-MMM-yyyy");

                    // Get middle initial if it exists
                    string middleInitial = "";

                    if (nameParts.Length > 2)
                    {
                        var middle = nameParts[1].Trim();
                        if (!string.IsNullOrEmpty(middle))
                        {
                            middleInitial = middle[0].ToString().ToUpper() + ".";
                        }
                    }


                    string filledCard = cardTemplate
                                                .Replace("{InstitutionName}", idCardSettings.InstitutionNameHtml.ToUpper())
                                                .Replace("{ContactDetails}", idCardSettings.ContactDetailsHtml)
                                                .Replace("{PrimaryColor}", idCardSettings.PrimaryColor)
                                                .Replace("{HeaderTextColor}", idCardSettings.HeaderTextColor)
                                                .Replace("{LogoPath}", idCardSettings.LogoPath)
                                                .Replace("{FirstName}", WebUtility.HtmlEncode(firstName.ToUpper()))
                                                .Replace("{MiddleInitial}", WebUtility.HtmlEncode(middleInitial.ToUpper()))
                                                .Replace("{Surname}", WebUtility.HtmlEncode(lastName.ToUpper()))
                                                .Replace("{StudentId}", WebUtility.HtmlEncode(student.StudentId_Number.ToString()))
                                                .Replace("{ExpiryDate}", date)
                                                .Replace("{NrcNumber}", WebUtility.HtmlEncode(student.NrcOrPassportNumber))
                                                .Replace("{Program}", WebUtility.HtmlEncode(student.Programme.Name));

                    cardsHtml.Append(filledCard);
                }

                string filledHtml = $@"
                                <!DOCTYPE html>
                                <html lang=""en"">
                                <head>
                                    <meta charset=""UTF-8"">
                                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                                    <title>ID Card Preview</title>
                                    <style>
                                        body {{
                                            font-family: Arial, sans-serif;
                                            background-color: #f5f5f5;
                                            padding: 20px;
                                            margin: 0;
                                        }}
                                        .preview-container {{
                                            display: flex;
                                            flex-direction: column;
                                            align-items: center;
                                            gap: 20px;
                                        }}
                                        .card {{
                                            box-shadow: 0 4px 8px rgba(0,0,0,0.1);
                                            background-color: white;
                                            margin-bottom: 20px;
                                        }}

                                        @media print {{
                                            body {{
                                                background: white !important;
                                                padding: 0 !important;
                                                margin: 0 !important;
                                                width: 100% !important;
                                            }}
                                            .preview-container {{
                                                gap: 0 !important;
                                                padding: 0 !important;
                                                margin: 0 !important;
                                                align-items: flex-start !important;
                                            }}
                                            .card {{
                                                box-shadow: none !important;
                                                margin: 0 !important;
                                                padding: 0 !important;
                                                page-break-inside: avoid;
                                            }}

                                            .card[style*=""padding: 15px""] {{
                                                padding: 0 !important;
                                                height: auto !important;
                                            }}
                                            .card[style*=""padding: 20px""] {{
                                                padding: 0 !important;
                                            }}
                                            
                                            .university {{
                                                background-color: {idCardSettings.PrimaryColor} !important;
                                                color: {idCardSettings.HeaderTextColor} !important;
                                                -webkit-print-color-adjust: exact !important;
                                                print-color-adjust: exact !important;
                                            }}
                                            
                                            * {{
                                                -webkit-print-color-adjust: exact !important;
                                                print-color-adjust: exact !important;
                                            }}
                                        }}

                                        @page {{
                                            margin: 0 !important;
                                            padding: 0 !important;
                                            size: auto;
                                        }}
                                    </style>
                                </head>
                                <body>
                                    {cardsHtml}
                                </body>
                                </html>";

                return Json(new { success = true, students = students, filledHtml = filledHtml });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student lookup dashboard");
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost("StudentLookup/RecordPrintedCard")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPrintedCard([FromBody] IdCardRecordDto model)
        {
            try
            {
                // Validate the model
                if (!ModelState.IsValid)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please fill in all required fields"
                    });
                }

                // Check if student exists
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.StudentId_Number == model.StudentNumber);

                if (student == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Student not found"
                    });
                }

                student.IdCardPrintedDate = DateTime.Now;
                _context.Update(student);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"ID card recorded successfully for student {model.StudentNumber}"
                });
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(ex, "Error recording ID card");

                return Json(new
                {
                    success = false,
                    message = "An error occurred while recording the ID card. Please try again."
                });
            }
        }


        [HttpGet("/StudentLookup/Barcode/{studentNumber}")]
        public IActionResult StudentBarcode(string studentNumber, int width = 180, int height = 30, bool includeText = true)
        {
            if (string.IsNullOrWhiteSpace(studentNumber))
                return BadRequest("studentNumber is required.");

            try
            {
                using (var bitmap = GenerateCode128BarcodeBitmap(studentNumber, width, height, includeText))
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    return File(ms.ToArray(), "image/png");
                }
            }
            catch (Exception ex)
            {
                // log exception as appropriate
                return StatusCode(500, "Error generating barcode: " + ex.Message);
            }
        }

        private Bitmap GenerateCode128BarcodeBitmap(string content, int width, int height, bool includeText)
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = height,
                    Width = width,
                    Margin = 10,
                    PureBarcode = true
                }
            };

            var pixelData = writer.Write(content);

            // create bitmap from the raw pixel data (RGBA -> BGR32/24)
            var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb);
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                                            ImageLockMode.WriteOnly,
                                            bitmap.PixelFormat);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            if (includeText)
            {
                // draw the student number text beneath the barcode
                int padding = 8;
                var font = new System.Drawing.Font("Arial", 12, FontStyle.Regular, GraphicsUnit.Pixel);
                var textHeight = (int)Math.Ceiling(font.GetHeight()) + padding;

                // create a larger image to include the text area
                var output = new Bitmap(bitmap.Width, bitmap.Height + textHeight);
                using (var g = Graphics.FromImage(output))
                {
                    g.Clear(System.Drawing.Color.White);
                    g.DrawImage(bitmap, 0, 0);
                    // draw centered text
                    var textRect = new Rectangle(0, bitmap.Height, output.Width, textHeight);
                    var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(content, font, Brushes.Black, textRect, format);
                }
                bitmap.Dispose();
                return output;
            }

            return bitmap;
        }

        [HttpPost]
        public async Task<IActionResult> SaveStudentPhoto([FromBody] SaveStudentPhotoRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Verify user can access this student
                if (!await CanAccessStudent(user, request.StudentId))
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                // Validate base64 image data
                if (string.IsNullOrEmpty(request.ImageData))
                {
                    return Json(new { success = false, message = "No image data provided" });
                }

                // Remove the data URL prefix if present
                var base64Data = request.ImageData;
                if (base64Data.Contains(","))
                {
                    base64Data = base64Data.Split(',')[1];
                }

                // Convert base64 to byte array
                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(base64Data);
                }
                catch (Exception)
                {
                    return Json(new { success = false, message = "Invalid image data format" });
                }

                // Define the upload directory
                var uploadDirectory = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "student-photos");

                // Create directory if it doesn't exist
                if (!Directory.Exists(uploadDirectory))
                {
                    Directory.CreateDirectory(uploadDirectory);
                }

                // Delete existing photo if it exists
                if (!string.IsNullOrEmpty(student.PassportPhotoPath))
                {
                    var oldPhotoPath = Path.Combine(_webHostEnvironment.WebRootPath, student.PassportPhotoPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPhotoPath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldPhotoPath);
                            _logger.LogInformation($"Deleted old photo: {oldPhotoPath}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Could not delete old photo: {oldPhotoPath}");
                        }
                    }
                }

                // Save new photo with student number as filename
                var fileName = $"{student.StudentId_Number}.png";
                var filePath = Path.Combine(uploadDirectory, fileName);

                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                // Update student record with new photo path
                student.PassportPhotoPath = $"/uploads/student-photos/{fileName}";
                student.UpdatedBy = user.Id;
                student.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully saved photo for student {student.StudentId_Number}");

                return Json(new
                {
                    success = true,
                    message = "Photo saved successfully",
                    photoPath = student.PassportPhotoPath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving student photo for ID: {StudentId}", request.StudentId);
                return Json(new { success = false, message = "An error occurred while saving the photo" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStudentPhoto(int studentId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Verify user can access this student
                if (!await CanAccessStudent(user, studentId))
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                if (string.IsNullOrEmpty(student.PassportPhotoPath))
                {
                    return Json(new { success = false, message = "No photo to delete" });
                }

                // Delete the photo file
                var photoPath = Path.Combine(_webHostEnvironment.WebRootPath, student.PassportPhotoPath.TrimStart('/'));
                if (System.IO.File.Exists(photoPath))
                {
                    System.IO.File.Delete(photoPath);
                }

                // Update student record
                student.PassportPhotoPath = null;
                student.UpdatedBy = user.Id;
                student.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Photo deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting student photo for ID: {StudentId}", studentId);
                return Json(new { success = false, message = "An error occurred while deleting the photo" });
            }
        }

        // Add Student Modal - Get dropdown data endpoints
        [HttpGet]
        public async Task<IActionResult> GetSchools()
        {
            try
            {
                var schools = await _context.Schools
                    .Select(s => new { s.Id, s.Name })
                    .OrderBy(s => s.Name)
                    .ToListAsync();
                return Json(schools);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading schools");
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammeLevels()
        {
            try
            {
                var levels = await _context.ProgramLevels
                    .OrderBy(l => l.Rank)
                    .Select(l => new { l.Id, l.Name })
                    .ToListAsync();
                return Json(levels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading programme levels");
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetModesOfStudy()
        {
            try
            {
                var modes = await _context.ModesOfStudy
                    .Select(m => new { m.ModeId, m.ModeName })
                    .OrderBy(m => m.ModeName)
                    .ToListAsync();
                return Json(modes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading modes of study");
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAcademicYears()
        {
            try
            {
                var years = await _context.AcademicYears
                    .Select(y => new { y.YearId, y.YearValue, YearName = y.YearValue })
                    .OrderByDescending(y => y.YearValue)
                    .ToListAsync();
                return Json(years);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading academic years");
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartmentsBySchool(int schoolId)
        {
            try
            {
                var departments = await _context.Departments
                    .Where(d => d.SchoolId == schoolId && d.IsActive)
                    .Select(d => new { d.Id, d.Name })
                    .OrderBy(d => d.Name)
                    .ToListAsync();
                return Json(departments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading departments for school {SchoolId}", schoolId);
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammesForModal(int schoolId)
        {
            try
            {
                var programmes = await _context.Programmes
                    .Where(p => p.Department != null && p.Department.SchoolId == schoolId)
                    .Select(p => new { p.Id, p.Name })
                    .OrderBy(p => p.Name)
                    .ToListAsync();
                return Json(programmes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading programmes for school {SchoolId}", schoolId);
                return Json(new List<object>());
            }
        }

        // Add Student Main Action
        [HttpPost]
        [Authorize(Roles = "Admin,Registrar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudent()
        {
            try
            {
                var form = Request.Form;

                bool TryReadRequiredInt(string fieldName, string displayName, out int value)
                {
                    var rawValue = form[fieldName].ToString();
                    if (int.TryParse(rawValue, out value))
                    {
                        return true;
                    }

                    return false;
                }

                if (!TryReadRequiredInt("AcademicYearId", "Academic Year", out var academicYearId))
                {
                    return Json(new { success = false, message = "Please select an Academic Year." });
                }

                if (!TryReadRequiredInt("SchoolId", "School", out var schoolId))
                {
                    return Json(new { success = false, message = "Please select a School." });
                }

                if (!TryReadRequiredInt("ProgrammeId", "Programme", out var programmeId))
                {
                    return Json(new { success = false, message = "Please select a Programme." });
                }

                if (!TryReadRequiredInt("ProgrammeLevelId", "Programme Level", out var programmeLevelId))
                {
                    return Json(new { success = false, message = "Please select a Programme Level." });
                }

                if (!TryReadRequiredInt("ModeOfStudyId", "Mode of Study", out var modeOfStudyId))
                {
                    return Json(new { success = false, message = "Please select a Mode of Study." });
                }

                if (!TryReadRequiredInt("StudentCurrentYear", "Current Year", out var studentCurrentYear))
                {
                    return Json(new { success = false, message = "Please select the student's Current Year." });
                }

                int? currentYearPeriodId = null;
                if (TryReadRequiredInt("CurrentYearPeriodId", "Current Period", out var postedYearPeriodId))
                {
                    currentYearPeriodId = postedYearPeriodId;
                }
                else if (TryReadRequiredInt("CurrentSemester", "Current Semester", out var currentSemester))
                {
                    currentYearPeriodId = await _context.AcademicYearPeriods
                        .Include(yp => yp.AcademicPeriod)
                        .Where(yp => yp.AcademicYearId == academicYearId && yp.AcademicPeriod.PeriodNumber == currentSemester)
                        .Select(yp => (int?)yp.Id)
                        .FirstOrDefaultAsync();

                    if (!currentYearPeriodId.HasValue)
                    {
                        return Json(new { success = false, message = "No academic period is configured for the selected Academic Year and Semester." });
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Please select a Current Semester." });
                }

                var studentNumber = form["StudentId_Number"].ToString().Trim();
                var email = form["Email"].ToString();
                var nrcOrPassport = form["NrcOrPassportNumber"].ToString();

                if (string.IsNullOrWhiteSpace(studentNumber))
                {
                    studentNumber = await _applicantService.GenerateStudentIdAsync(academicYearId);
                }

                // Check if student number already exists
                var existingStudentNumber = await _context.Students
                    .AnyAsync(s => s.StudentId_Number == studentNumber);
                if (existingStudentNumber)
                {
                    return Json(new { success = false, message = $"Student number '{studentNumber}' already exists in the system" });
                }

                // Check if email already exists
                var existingEmail = await _context.Students
                    .AnyAsync(s => s.Email == email);
                if (existingEmail)
                {
                    return Json(new { success = false, message = $"Email '{email}' is already registered to another student" });
                }

                // Check if NRC/Passport already exists
                var existingNrc = await _context.Students
                    .AnyAsync(s => s.NrcOrPassportNumber == nrcOrPassport);
                if (existingNrc)
                {
                    return Json(new { success = false, message = $"NRC/Passport number '{nrcOrPassport}' is already registered to another student" });
                }

                // Create Student entity
                var student = new Student
                {
                    // Personal Information
                    FullName = form["FullName"].ToString(),
                    Email = form["Email"].ToString(),
                    StudentId_Number = studentNumber,
                    NrcOrPassportNumber = form["NrcOrPassportNumber"].ToString(),
                    DateOfBirth = DateTime.Parse(form["DateOfBirth"].ToString()),
                    Gender = form["Gender"].ToString(),
                    Phone = form["Phone"].ToString(),
                    MaritalStatus = form["MaritalStatus"].ToString(),
                    Nationality = form["Nationality"].ToString(),
                    Religion = string.IsNullOrEmpty(form["Religion"].ToString()) ? "Not Specified" : form["Religion"].ToString(),
                    // Handle IsForeigner - determine from Nationality if not provided
                    IsForeigner = !string.IsNullOrEmpty(form["IsForeigner"].ToString())
                        ? int.Parse(form["IsForeigner"].ToString()) == 1
                        : form["Nationality"].ToString() != "Zambian",

                    // Academic Information
                    SchoolId = schoolId,
                    ProgrammeId = programmeId,
                    ProgrammeLevelId = programmeLevelId,
                    ModeOfStudyId = modeOfStudyId,
                    AcademicYearId = academicYearId,
                    StudentCurrentYear = studentCurrentYear,
                    CurrentYearPeriodId = currentYearPeriodId,

                    // Status Fields
                    ApplicationReferenceNumber = "MANUAL-" + DateTime.Now.Ticks,
                    Username = form["Email"].ToString(),
                    StudentStatus = Status.Registered,
                    IsAdmitted = true,
                    AdmissionDate = DateTime.Now,
                    IsRegistered = true,
                    RegistrationStatus = Status.Registered,
                    RegistrationDate = DateTime.Now,
                    NrcOrPassportCopy = "/documents/placeholder.pdf",

                    // Accommodation Fields
                    BlackListedFromAccommodationReason = string.Empty,
                    IsBlackListedFromAccommodation = false,

                    // Audit Fields
                    CreatedBy = User.Identity!.Name ?? "System",
                    CreatedAt = DateTime.Now,
                    UpdatedBy = User.Identity!.Name ?? "System",
                    UpdatedAt = DateTime.Now
                };

                // Create Address
                var address = new StudentAddress
                {
                    AddressLine1 = form["AddressLine1"].ToString(),
                    AddressLine2 = form["AddressLine2"].ToString(),
                    City = form["City"].ToString(),
                    State = form["State"].ToString(),
                    PostalCode = string.IsNullOrEmpty(form["PostalCode"].ToString()) ? "00000" : form["PostalCode"].ToString(),
                    Country = form["Country"].ToString()
                };

                _context.StudentAddresses.Add(address);
                await _context.SaveChangesAsync();

                student.AddressId = address.Id;

                // Create Next of Kin
                student.NextOfKin = new StudNextOfKin
                {
                    Name = form["NextOfKinName"].ToString(),
                    Relationship = form["NextOfKinRelation"].ToString(),
                    PhoneNumber = form["NextOfKinPhone"].ToString(),
                    Address = string.IsNullOrEmpty(form["NextOfKinAddress"].ToString()) ? "N/A" : form["NextOfKinAddress"].ToString(),
                    Email = form["NextOfKinEmail"].ToString() ?? string.Empty
                };

                // Save Student
                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                // Create user account for the student
                var defaultPassword = "Student@2025"; // Default password for all students
                var applicationUser = new ApplicationUser
                {
                    FullName = student.FullName,
                    Email = student.Email,
                    UserName = student.Email,
                    EmailConfirmed = true,
                    PhoneNumber = student.Phone
                };

                var userResult = await _userManager.CreateAsync(applicationUser, defaultPassword);
                if (userResult.Succeeded)
                {
                    // Assign Student role
                    await _userManager.AddToRoleAsync(applicationUser, "Student");
                    _logger.LogInformation("User account created for student {StudentId} with default password", studentNumber);
                }
                else
                {
                    var userErrors = string.Join(", ", userResult.Errors.Select(e => e.Description));
                    _logger.LogWarning("Student {StudentId} was created but user account creation failed: {Errors}",
                        studentNumber, userErrors);
                    // Don't fail the whole operation, student record is still created
                }

                _logger.LogInformation("Student {StudentId} added successfully by {User}",
                    student.StudentId_Number, User.Identity!.Name);

                return Json(new {
                    success = true,
                    message = $"Student {student.FullName} added successfully. Default login password: {defaultPassword}",
                    studentId = student.Id,
                    studentNumber,
                    defaultPassword
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding student");
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "An error occurred: " + innerMessage });
            }
        }
    }

    public class UnifiedTransactionDto
    {
        public int Id { get; set; }
        public int? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentNumber { get; set; }
        public decimal? Amount { get; set; }
        public string? Status { get; set; }
        public string? Reference { get; set; }
        public bool Credit { get; set; } = true;
        public string? AccountingSystemPostStatus { get; set; }
        public string? Narration { get; set; }
        public List<StudentInvoiceItem> InvoiceItems { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class IdCardRecordDto
    {
        [Required(ErrorMessage = "Student number is required")]
        public string StudentNumber { get; set; }

        public DateTime? PrintDate { get; set; }
    }

    public class IdCardDisplaySettings
    {
        public string InstitutionName { get; set; } = string.Empty;
        public string InstitutionNameHtml { get; set; } = string.Empty;
        public string ContactDetails { get; set; } = string.Empty;
        public string ContactDetailsHtml { get; set; } = string.Empty;
        public string PrimaryColor { get; set; } = "#1991cf";
        public string HeaderTextColor { get; set; } = "#ffffff";
        public string LogoPath { get; set; } = "/images/institution-logo.png";
    }
}
