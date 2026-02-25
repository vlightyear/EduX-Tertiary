using CountryData.Standard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIS.Data;
using SIS.DTOs.StudentApplication;
using SIS.Enums;
using SIS.Models;
using SIS.Models.Payments;
using SIS.Models.StudentApplication;
using SIS.Services.Payment;
using SIS.Services.PDF;
using SIS.Services.StudentApplication;
using SIS.Services.Users;
using System.Text;
using SIS.Services.PhotoValidation;

namespace SIS.Controllers
{
    [Authorize(Roles = "Candidate, Student")]
    public class StudentApplicationController : Controller
    {
        private readonly IApplicantService _applicantService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StudentApplicationController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPaymentService _paymentService;
        private readonly UserService _userService;
        private readonly HttpClient _httpClient;
        private int _currentRegistrarIndex = 0;
        private readonly IProgrammeService _programmeService;
        private readonly IPdfInvoiceService _pdfInvoiceService;
        private readonly IPhotoValidationService _photoValidationService;

        public StudentApplicationController(IApplicantService applicantService, ApplicationDbContext context, ILogger<StudentApplicationController> logger,
            UserManager<ApplicationUser> userManager, IPaymentService paymentService, UserService userService, HttpClient httpClient, IProgrammeService programmeService, IPdfInvoiceService pdfInvoiceService, IPhotoValidationService photoValidationService)
        {
            _applicantService = applicantService;
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _paymentService = paymentService;
            _userService = userService;
            _httpClient = httpClient;
            _programmeService = programmeService;
            _pdfInvoiceService = pdfInvoiceService;
            _photoValidationService = photoValidationService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Get all schools with departments and programmes
            var schools = await _context.Schools
                .Include(s => s.Departments)
                    .ThenInclude(d => d.Programmes)
                        .ThenInclude(p => p.ProgrammeLevel)
                .ToListAsync();

            // Fetch all applications for the logged-in candidate
            var applications = await _context.Applicants
                .Include(a => a.School)
                .Include(a => a.Programme)
                .Include(a => a.ModeOfStudy)
                .Include(a => a.AcademicYear)
                .Include(a => a.ProgrammeLevel)
                .Where(a => a.Email == user.Email)
                .ToListAsync();

            // Get current academic year
            var currentAcademicYear = await _context.AcademicYears
                .Where(a => a.IsActive)
                .FirstOrDefaultAsync();

            var userRoles = await _userManager.GetRolesAsync(user);
            var notifications = await _context.Notifications
                .Where(n => n.IsActive &&
                          (n.ExpiryDate >= DateTime.Now) &&
                          (string.IsNullOrEmpty(n.TargetUserEmail) || n.TargetUserEmail == user.Email) &&
                          (string.IsNullOrEmpty(n.TargetRole) || userRoles.Contains(n.TargetRole)))
                .OrderBy(n => n.ExpiryDate)
                .Take(5)
                .ToListAsync();

            // Check if there are any incomplete applications
            var incompleteApplications = applications.Where(a => !a.IsSubmitted).ToList();

            // Get application statistics
            var applicationStatsByStatus = applications
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Status, x => x.Count);

            // Create the view model
            var dashboardViewModel = new ApplicantDashboardViewModel
            {
                Applications = applications,
                Schools = schools,
                CurrentAcademicYear = currentAcademicYear,
                Notifications = notifications,
                ApplicationStatistics = applicationStatsByStatus,
                RecentApplications = applications.OrderByDescending(a => a.DateSubmitted).Take(5).ToList(),
                HasIncompleteApplications = incompleteApplications.Any(),
                IncompleteApplicationsCount = incompleteApplications.Count
            };

            // Prepare school and program data for charts
            dashboardViewModel.SchoolProgramData = schools
                .Select(s => new SchoolProgramCount
                {
                    SchoolName = s.Name,
                    ProgramCount = s.Departments.Sum(d => d.Programmes.Count)
                })
                .ToList();

            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            TempData["Success"] = $"Welcome Applicant, you have logged in Successfully!";

            // Store the application count in TempData
            TempData["PendingApplicationsCount"] = applications.Count(a => a.Status == Status.Pending);
            TempData["TotalApplicationsCount"] = applications.Count;

            return View(dashboardViewModel);
        }

        public IActionResult ViewPendingApplications()
        {
            var pendingApplicationsJson = HttpContext.Session.GetString("PendingApplications");
            var pendingApplications = JsonConvert.DeserializeObject<List<Applicant>>(pendingApplicationsJson);
            return View(pendingApplications);
        }

        // GET: StudentApplication/PersonalDetails
        public async Task<IActionResult> PersonalDetails()
        {
            var helper = new CountryHelper();
            List<string> countries = helper.GetCountries();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new PersonalDetailsViewModel
            {
                Applicant = new ApplicantDto { FullName = user.FullName, Email = user.Email }
            };

            // Get current date
            var currentDate = DateTime.Now;

            // ✅ FIX: Get active application periods with ALL necessary includes
            var activeApplicationPeriods = await _context.ApplicationPeriods
                .Include(ap => ap.ModeOfStudies)
                .Include(ap => ap.Programms)
                    .ThenInclude(p => p.Department)  // ✅ CRITICAL: Include Department
                        .ThenInclude(d => d.School)
                .Include(ap => ap.Programms)
                    .ThenInclude(p => p.ProgrammeLevel)
                .Where(ap => ap.StartOfApplication <= currentDate && ap.EndOfApplication >= currentDate)
                .ToListAsync();

            if (!activeApplicationPeriods.Any())
            {
                TempData["Error"] = "No active application periods at the moment. Please check back later.";
                return RedirectToAction("Index");
            }

            // ✅ FIX: Get distinct schools with proper null checking
            var activeSchools = activeApplicationPeriods
                .SelectMany(ap => ap.Programms)
                .Where(p => p.Department != null && p.Department.School != null)  // ✅ Null safety
                .Select(p => p.Department.School)
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .Select(s => new DropdownOptionDto
                {
                    Id = s.Id,
                    Name = s.Name
                })
                .OrderBy(s => s.Name)
                .ToList();

            // ✅ FIX: Get distinct modes of study
            var activeModes = activeApplicationPeriods
                .SelectMany(ap => ap.ModeOfStudies)
                .GroupBy(m => m.ModeId)
                .Select(g => g.First())
                .Select(m => new DropdownOptionDto
                {
                    Id = m.ModeId,
                    Name = $"{m.ModeName} ({m.Code})"
                })
                .OrderBy(m => m.Name)
                .ToList();

            // ✅ FIX: Get programme levels that are actually available in active periods
            var activeProgrammeLevelIds = activeApplicationPeriods
                .SelectMany(ap => ap.Programms)
                .Where(p => p.ProgrammeLevel != null)
                .Select(p => p.ProgrammeLevelId)
                .Distinct()
                .ToList();

            var activeProgrammeLevels = await _context.ProgramLevels
                .Where(pl => activeProgrammeLevelIds.Contains(pl.Id))
                .Select(pl => new DropdownOptionDto
                {
                    Id = pl.Id,
                    Name = pl.Name
                })
                .OrderBy(pl => pl.Name)
                .ToListAsync();

            // ✅ ENHANCED: Store detailed programme data for frontend filtering
            var activeProgrammeData = activeApplicationPeriods
                .SelectMany(ap => ap.Programms)
                .Where(p => p.Department != null)  // ✅ Null safety
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .Select(p => new
                {
                    Id = p.Id,
                    Name = p.Name,
                    SchoolId = p.Department.SchoolId,
                    ProgrammeLevelId = p.ProgrammeLevelId,
                    ModeOfStudyId = p.ModeOfStudyId,
                    ApplicationPeriodIds = activeApplicationPeriods
                        .Where(ap => ap.Programms.Any(prog => prog.Id == p.Id))
                        .Select(ap => ap.Id)
                        .ToList()
                })
                .ToList();

            // ✅ Store data in ViewBag for JavaScript filtering
            ViewBag.ActiveApplicationPeriodIds = activeApplicationPeriods.Select(ap => ap.Id).ToList();
            ViewBag.ActiveProgrammes = await _context.Programmes.OrderBy(p => p.Name).ToListAsync(); //System.Text.Json.JsonSerializer.Serialize(activeProgrammeData);
            ViewBag.ActiveModeIds = activeModes.Select(m => m.Id).ToList();
            ViewBag.ActiveSchoolIds = activeSchools.Select(s => s.Id).ToList();
            ViewBag.ActiveProgrammeLevelIds = activeProgrammeLevelIds;

            // Retrieve data asynchronously
            model.Subjects = await _applicantService.GetSubjectsAsync();
            model.Grades = await _applicantService.GetGradesAsync();
            model.Schools = activeSchools; // Use filtered schools
            model.ModesOfStudy = activeModes; // Use filtered modes of study
            model.ProgrammeLevel = activeProgrammeLevels; // ✅ Use filtered levels

            // Get academic years and format them to show month intake
            var academicYears = await _applicantService.GetAcademicYearsAsync();
            var formattedAcademicYears = new List<DropdownOptionDto>();

            foreach (var ay in academicYears)
            {
                var academicYear = await _context.AcademicYears.FindAsync(ay.Id);
                if (academicYear != null)
                {
                    var formattedName = FormatIntakeName(ay.Name, academicYear.StartDate);
                    formattedAcademicYears.Add(new DropdownOptionDto
                    {
                        Id = ay.Id,
                        Name = formattedName
                    });
                }
            }

            model.AcademicYears = formattedAcademicYears;
            model.Countries = countries;

            // Enhanced informational message about active application periods
            if (activeApplicationPeriods.Count == 1)
            {
                var period = activeApplicationPeriods.First();
                TempData["Info"] = $"Currently accepting applications for: {period.Name} (Closes: {period.EndOfApplication:MMM dd, yyyy})";
            }
            else
            {
                TempData["Info"] = $"Currently {activeApplicationPeriods.Count} active application periods available.";
            }

            return View(model);
        }

        private string FormatIntakeName(string yearValue, DateTime startDate)
        {
            string monthName = startDate.ToString("MMMM");
            return $"{yearValue}, {monthName} Intake";
        }

        [HttpPost]
        public async Task<IActionResult> ValidatePassportPhoto(IFormFile passportPhoto)
        {
            try
            {
                var validationResult = await _photoValidationService.ValidatePassportPhotoAsync(passportPhoto);

                // ✅ FIX: Extract values first, then use in anonymous object
                var errors = validationResult.Errors?.ToArray() ?? Array.Empty<string>();
                var warnings = validationResult.Warnings?.ToArray() ?? Array.Empty<string>();

                return Json(new
                {
                    success = true,
                    isValid = validationResult.IsValid,
                    message = validationResult.Message,
                    errors = errors,
                    warnings = warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating passport photo");
                return Json(new
                {
                    success = true,
                    isValid = false,
                    message = "Photo validation temporarily unavailable. You may proceed with submission.",
                    errors = Array.Empty<string>(),
                    warnings = new string[] { "Unable to validate photo quality - please ensure it meets requirements" }
                });
            }
        }

        // POST: StudentApplication/PersonalDetails
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitApplication(PersonalDetailsViewModel model)
        {
            var academicYear = await _context.AcademicYears.FindAsync(model.Applicant.AcademicYearId);
            if (academicYear == null)
            {
                ModelState.AddModelError("AcademicYearId", "Academic year not found.");
                return View("PersonalDetails", model);
            }

            try
            {
                // VALIDATION: Check if selected programme is in an active application period
                var currentDate = DateTime.Now;
                var programmeInActivePeriod = await _context.ApplicationPeriods
                    .Where(ap =>
                        ap.StartOfApplication <= currentDate &&
                        ap.EndOfApplication >= currentDate &&
                        ap.Programms.Any(p => p.Id == model.Applicant.ProgrammeId))
                    .AnyAsync();

                if (!programmeInActivePeriod)
                {
                    return Json(new
                    {
                        success = false,
                        message = "The selected programme is not currently accepting applications. Please select a different programme or check active application periods."
                    });
                }

                // VALIDATION: Check if selected mode of study is in an active application period
                var modeInActivePeriod = await _context.ApplicationPeriods
                    .Where(ap =>
                        ap.StartOfApplication <= currentDate &&
                        ap.EndOfApplication >= currentDate &&
                        ap.ModeOfStudies.Any(m => m.ModeId == model.Applicant.ModeOfStudyId))
                    .AnyAsync();

                if (!modeInActivePeriod)
                {
                    return Json(new
                    {
                        success = false,
                        message = "The selected mode of study is not currently available for applications. Please select a different mode."
                    });
                }

                // Get the active application period for this submission
                var applicationPeriod = await _context.ApplicationPeriods
                    .Where(ap =>
                        ap.StartOfApplication <= currentDate &&
                        ap.EndOfApplication >= currentDate &&
                        ap.Programms.Any(p => p.Id == model.Applicant.ProgrammeId) &&
                        ap.ModeOfStudies.Any(m => m.ModeId == model.Applicant.ModeOfStudyId))
                    .FirstOrDefaultAsync();

                if (applicationPeriod == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No active application period found for the selected programme and mode of study."
                    });
                }

                // Map the ViewModel data to the Applicant model
                var ProgrammeId = model.Applicant.ProgrammeId;
                var programme = await _context.Programmes.FirstOrDefaultAsync(p => p.Id == model.Applicant.ProgrammeId);

                if(programme != null)
                {
                    if (programme.AssociatedNQProgrammeId.HasValue)
                    {
                        ProgrammeId = programme.AssociatedNQProgrammeId.Value;
                    }
                }

                var applicant = new Applicant
                {
                    NrcOrPassport = model.Applicant.NRCOrPassport,
                    FullName = model.Applicant.FullName,
                    Email = model.Applicant.Email,
                    DateOfBirth = model.Applicant.DateOfBirth,
                    Phone = Request.Form["Applicant.Phone"].FirstOrDefault() ?? model.Applicant.Phone,
                    Gender = model.Applicant.Gender,
                    MaritalStatus = model.Applicant.MaritalStatus,
                    Nationality = model.Applicant.Nationality,
                    Religion = model.Applicant.Religion,
                    FormerSchoolName = model.Applicant.FormerSchoolName,
                    YearOfCompletion = model.Applicant.YearOfCompletion,
                    FormerSchoolAddress = model.Applicant.FormerSchoolAddress,
                    FormerSchoolLevel = model.Applicant.FormerSchoolLevel,
                    PrimarySchoolName = model.Applicant.PrimarySchoolName ?? string.Empty,
                    PrimarySchoolAddress = model.Applicant.PrimarySchoolAddress ?? string.Empty,
                    PrimarySchoolPeriod = model.Applicant.PrimarySchoolPeriod ?? string.Empty,
                    SecondarySchoolName = model.Applicant.SecondarySchoolName,
                    SecondarySchoolAddress = model.Applicant.SecondarySchoolAddress,
                    SecondarySchoolPeriod = model.Applicant.SecondarySchoolPeriod,
                    SchoolId = model.Applicant.SchoolId,
                    ProgrammeId = ProgrammeId,
                    ModeOfStudyId = model.Applicant.ModeOfStudyId,
                    AcademicYearId = model.Applicant.AcademicYearId,
                    ApplicationPeriodId = applicationPeriod.Id, // ASSIGN APPLICATION PERIOD
                    AddressLine1 = model.Applicant.AddressLine1,
                    AddressLine2 = model.Applicant.AddressLine2,
                    PostalCode = model.Applicant.PostalCode,
                    City = model.Applicant.City,
                    AcademicYear = academicYear,
                    State = model.Applicant.State,
                    Country = model.Applicant.Country,
                    NextOfKinName = model.Applicant.NextOfKinName,
                    NextOfKinRelation = model.Applicant.NextOfKinRelation,
                    NextOfKinPhone = Request.Form["Applicant.NextOfKinPhone"].FirstOrDefault() ?? model.Applicant.NextOfKinPhone,
                    NextOfKinAddress = model.Applicant.NextOfKinAddress,
                    NextOfKinEmail = model.Applicant.NextOfKinEmail,
                    ProgrammeLevelId = model.Applicant.ProgrammeLevel,
                    Status = Status.Pending,
                    CreatedAt = DateTime.Now,
                    CreatedBy = _userManager.GetUserId(User),
                    PaymentStatus = Status.Pending,
                    IsForeigner = model.Applicant.IsForeigner,
                    IsSubmitted = false,
                    DateSubmitted = DateTime.Now
                };

                // Save the uploaded files if applicable
                if (model.Applicant.NrcOrPassportCopy != null)
                {
                    var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "NRCs", model.Applicant.NRCOrPassport);
                    Directory.CreateDirectory(uploadsDirectory);
                    var nrcFilePath = Path.Combine(uploadsDirectory, Path.GetFileName(model.Applicant.NrcOrPassportCopy.FileName));
                    using (var stream = new FileStream(nrcFilePath, FileMode.Create))
                    {
                        model.Applicant.NrcOrPassportCopy.CopyTo(stream);
                    }
                    applicant.NrcOrPassportCopy = nrcFilePath;
                }

                if (model.Applicant.ResultsAttachment != null)
                {
                    var resultsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Results", model.Applicant.NRCOrPassport);
                    Directory.CreateDirectory(resultsDirectory);
                    var resultsFilePath = Path.Combine(resultsDirectory, Path.GetFileName(model.Applicant.ResultsAttachment.FileName));
                    using (var stream = new FileStream(resultsFilePath, FileMode.Create))
                    {
                        model.Applicant.ResultsAttachment.CopyTo(stream);
                    }
                    applicant.ResultsAttachmentCopy = resultsFilePath;
                }

                if (model.Applicant.PassportPhoto != null)
                {
                    var photoDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "PassportPhotos", model.Applicant.NRCOrPassport);
                    Directory.CreateDirectory(photoDirectory);
                    var photoFilePath = Path.Combine(photoDirectory, Path.GetFileName(model.Applicant.PassportPhoto.FileName));
                    using (var stream = new FileStream(photoFilePath, FileMode.Create))
                    {
                        model.Applicant.PassportPhoto.CopyTo(stream);
                    }
                    applicant.PassportPhotoPath = photoFilePath;
                }

                if (model.Applicant.IsForeigner)
                {
                    if (model.Applicant.StudyPermit != null)
                    {
                        var foreignerFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Permits", model.Applicant.NRCOrPassport);
                        Directory.CreateDirectory(foreignerFilePath);
                        var resultsFilePath = Path.Combine(foreignerFilePath, Path.GetFileName(model.Applicant.StudyPermit.FileName));
                        using (var stream = new FileStream(resultsFilePath, FileMode.Create))
                        {
                            model.Applicant.StudyPermit.CopyTo(stream);
                        }
                        applicant.StudyPermitCopy = resultsFilePath;
                    }
                }

                // assign student id
                applicant.ReferenceNumber = _applicantService.GenerateReferenceNumber();

                // Add secondary school subjects and grades
                if (!model.Applicant.IsForeigner)
                {
                    if (model.SelectedSubjects != null && model.SelectedSubjects.Any() && model.SelectedSubjects.Count >= 5)
                    {
                        programme = await _context.Programmes
                            .FirstOrDefaultAsync(p => p.Id == model.Applicant.ProgrammeId);

                        if (programme != null)
                        {
                            var grades = await _context.Grades
                                .Where(g => model.SelectedSubjects.Select(s => s.GradeId).Contains(g.GradeId))
                                .ToDictionaryAsync(g => g.GradeId, g => g.GradePoint);

                            var totalPoints = model.SelectedSubjects
                                .Select(s => grades.GetValueOrDefault(s.GradeId, 0))
                                .OrderByDescending(points => points)
                                .Take(5)
                                .Sum();

                            applicant.IsQualified = totalPoints <= programme.MinimumPointsTop5Subjects;
                        }

                        applicant.SubjectGrades = model.SelectedSubjects.Select(sg => new ApplicantSubject
                        {
                            ApplicantId = applicant.ApplicantId,
                            ReferenceNumber = applicant.ReferenceNumber,
                            SubjectId = sg.SubjectId,
                            GradeId = sg.GradeId
                        }).ToList();
                    }
                    else
                    {
                        applicant.IsQualified = false;
                    }
                }
                else
                {
                    applicant.IsQualified = null;
                    applicant.SubjectGrades = new List<ApplicantSubject>();
                }

                // Fetch the admin users
                List<ApplicationUser> adminUsers = await _userService.GetUsersWithRegistrarRoleAsync();

                if (adminUsers == null || !adminUsers.Any())
                {
                    Console.WriteLine("No admin users found.");
                    return NotFound();
                }

                applicant.AssistantRegistrarId = adminUsers[_currentRegistrarIndex].Id;
                _currentRegistrarIndex = (_currentRegistrarIndex + 1) % adminUsers.Count;

                _context.Applicants.Add(applicant);
                await _context.SaveChangesAsync();

                // Check if application is free and auto-process
                bool isFreeApplication = await CheckAndProcessFreeApplication(applicant);

                ApplicationPayment pendingPayment;

                if (isFreeApplication)
                {
                    applicant.PaymentStatus = Status.Paid;
                    pendingPayment = new ApplicationPayment
                    {
                        ApplicationId = applicant.ApplicantId,
                        Amount = 0,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = "FREE",
                        TransactionReference = $"FREE_{DateTime.Now:yyyyMMddHHmmss}_{applicant.ReferenceNumber}",
                        Status = Status.Completed
                    };
                }
                else
                {
                    pendingPayment = new ApplicationPayment
                    {
                        ApplicationId = applicant.ApplicantId,
                        Amount = 0,
                        PaymentDate = DateTime.MinValue,
                        PaymentMethod = "Pending",
                        TransactionReference = "PENDING",
                        Status = Status.Pending
                    };
                }

                _context.ApplicationPayments.Add(pendingPayment);
                await _context.SaveChangesAsync();

                var referenceNumber = applicant.ReferenceNumber;

                return Json(new
                {
                    success = true,
                    referenceNumber = applicant.ReferenceNumber,
                    isFreeApplication = isFreeApplication,
                    message = isFreeApplication
                        ? "Application submitted successfully - no payment required!"
                        : $"Application created successfully for {applicationPeriod.Name} - payment required."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting application");

                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " | " + ex.InnerException.Message;
                }

                return Json(new { success = false, message = "An error occurred: " + errorMessage });
            }

        }

        public IActionResult ApplicationSuccess(string referenceNumber, bool isFreeApplication = false)
        {
            if (referenceNumber == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var initialDto = new InitApplicationDto
            {
                ReferenceNumber = referenceNumber
            };

            ViewBag.IsFreeApplication = isFreeApplication;

            return View("ApplicationSuccess", initialDto);
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammes(int schoolId, int programmeLevelId)
        {
            try
            {
                if (schoolId <= 0 || programmeLevelId <= 0)
                {
                    return BadRequest(new { message = "Invalid parameters. Both school and programme level are required." });
                }

                var currentDate = DateTime.Now;

                // ✅ FIX: Get active application period IDs first
                var activeApplicationPeriodIds = await _context.ApplicationPeriods
                    .Where(ap => ap.StartOfApplication <= currentDate && ap.EndOfApplication >= currentDate)
                    .Select(ap => ap.Id)
                    .ToListAsync();

                if (!activeApplicationPeriodIds.Any())
                {
                    return Json(new List<object>()); // Return empty list if no active periods
                }

                // ✅ FIX: Query programmes directly with proper filtering
                var programs = await _context.Programmes
                    .Include(p => p.Department)
                    .Include(p => p.ProgrammeLevel)
                    .Include(p => p.ModeOfStudy)
                    .Where(p =>
                        p.Department != null &&
                        p.Department.SchoolId == schoolId &&
                        p.ProgrammeLevelId == programmeLevelId &&
                        activeApplicationPeriodIds.Contains(p.ApplicationPeriodId.Value))
                    .Select(p => new
                    {
                        Id = p.Id,
                        Name = p.Name,
                        ModeOfStudyId = p.ModeOfStudyId,
                        ProgrammeLevelName = p.ProgrammeLevel != null ? p.ProgrammeLevel.Name : "",
                        Description = p.Description,
                        DurationYears = p.DurationYears
                    })
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                return Json(programs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading programmes for SchoolId: {SchoolId}, ProgrammeLevelId: {ProgrammeLevelId}",
                    schoolId, programmeLevelId);
                return BadRequest(new
                {
                    message = "Failed to load programmes. Please try again.",
                    error = ex.Message
                });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetStatesByCountry(string country)
        {
            if (string.IsNullOrEmpty(country))
            {
                return BadRequest("Country is required.");
            }

            var client = new HttpClient();
            var requestBody = new { country };
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://countriesnow.space/api/v0.1/countries/states")
            {
                Content = content
            };

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(responseString);

                if (apiResponse != null && !apiResponse.Error && apiResponse.Data?.States != null)
                {
                    var states = apiResponse.Data.States.Select(s => s.Name).ToList();
                    return Json(states);
                }

                return Json(new List<string>());
            }
            else
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                return BadRequest($"Error fetching cities data: {errorMessage}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCitiesByState(string country, string state)
        {
            if (string.IsNullOrEmpty(country) || string.IsNullOrEmpty(state))
            {
                return BadRequest("Country and state are required.");
            }

            var requestBody = new { country, state };
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var client = _httpClient;
            var request = new HttpRequestMessage(HttpMethod.Post, "https://countriesnow.space/api/v0.1/countries/state/cities")
            {
                Content = content
            };
            request.Headers.Add("Accept", "application/json");

            HttpResponseMessage response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var citiesResponse = JsonConvert.DeserializeObject<CitiesResponse>(responseString);

                if (citiesResponse != null && !citiesResponse.Error && citiesResponse.Data != null)
                {
                    return Json(citiesResponse.Data);
                }

                return Json(new List<string>());
            }
            else
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                return BadRequest($"Error fetching cities data: {errorMessage}");
            }
        }

        public async Task<IActionResult> ViewAll(string search = "", string status = "", string school = "", int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            int pageSize = 10;
            var query = _context.Applicants
                .Include(a => a.School)
                .Include(a => a.Programme)
                .Include(a => a.AcademicYear)
                .Include(a => a.ModeOfStudy)
                .Where(a => a.Email == user.Email);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a => a.ReferenceNumber.Contains(search) ||
                                        a.Programme.Name.Contains(search) ||
                                        a.School.Name.Contains(search));
            }

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<Status>(status, out var statusEnum))
            {
                query = query.Where(a => a.Status == statusEnum);
            }

            if (!string.IsNullOrEmpty(school) && int.TryParse(school, out var schoolId))
            {
                query = query.Where(a => a.SchoolId == schoolId);
            }

            var totalItems = await query.CountAsync();
            var applications = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var schools = await _context.Schools
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToListAsync();

            var statusOptions = Enum.GetValues<Status>()
                .Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() })
                .ToList();

            var viewModel = new ApplicationsListViewModel
            {
                Applications = applications,
                SearchTerm = search,
                SelectedStatus = status,
                SelectedSchool = school,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                StatusOptions = statusOptions,
                SchoolOptions = schools
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var application = await _context.Applicants
                .Include(a => a.School)
                .Include(a => a.Programme)
                    .ThenInclude(p => p.ProgrammeLevel)
                .Include(a => a.ModeOfStudy)
                .Include(a => a.AcademicYear)
                .Include(a => a.ProgrammeLevel)
                .Include(a => a.SubjectGrades)
                    .ThenInclude(sg => sg.Subject)
                .Include(a => a.SubjectGrades)
                    .ThenInclude(sg => sg.Grade)
                .FirstOrDefaultAsync(a => a.ApplicantId == id && a.Email == user.Email);

            if (application == null)
            {
                return NotFound();
            }

            var paymentHistory = await _context.ApplicationPayments
                .Where(p => p.ApplicationId == application.ApplicantId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var viewModel = new ApplicationDetailsViewModel
            {
                Application = application,
                PaymentHistory = paymentHistory,
                CanMakePayment = application.PaymentStatus == Status.Pending
            };

            return View(viewModel);
        }

        public async Task<IActionResult> DownloadDocument(int id, string documentType, bool inline = true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var application = await _context.Applicants
                .FirstOrDefaultAsync(a => a.ApplicantId == id && a.Email == user.Email);

            if (application == null)
            {
                return NotFound();
            }

            string filePath = null;
            string fileName = null;

            switch (documentType.ToLower())
            {
                case "nrc":
                case "passport":
                    filePath = application.NrcOrPassportCopy;
                    fileName = $"NRC_Passport_{application.ReferenceNumber}.pdf";
                    break;
                case "results":
                case "transcript":
                    filePath = application.ResultsAttachmentCopy;
                    fileName = $"Results_{application.ReferenceNumber}.pdf";
                    break;
                case "studypermit":
                    filePath = application.StudyPermitCopy;
                    fileName = $"StudyPermit_{application.ReferenceNumber}.pdf";
                    break;
                default:
                    return NotFound();
            }

            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            if (inline)
            {
                Response.Headers.Add("Content-Disposition", $"inline; filename=\"{fileName}\"");
                return File(memory, "application/pdf");
            }
            else
            {
                return File(memory, "application/pdf", fileName);
            }
        }

        public async Task<IActionResult> ViewPrograms(string search = "", int? schoolId = null, int? programmeLevelId = null, int page = 1)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                var viewModel = await _programmeService.GetProgrammesGroupedBySchoolAsync(search, schoolId, programmeLevelId);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading programmes view");
                TempData["Error"] = "An error occurred while loading programmes.";
                return RedirectToAction("Index");
            }
        }

        public async Task<IActionResult> ProgrammeDetails(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                var viewModel = await _programmeService.GetProgrammeDetailsWithCoursesAsync(id);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading programme details for ID: {ProgrammeId}", id);
                TempData["Error"] = "An error occurred while loading programme details.";
                return RedirectToAction("ViewPrograms");
            }
        }

        public async Task<IActionResult> GenerateProgrammeInvoice(int programmeId, int yearOfStudy)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                var pdfBytes = await _pdfInvoiceService.GenerateProgrammeFeesInvoiceAsync(programmeId, yearOfStudy, user.FullName);

                var programme = await _context.Programmes.FindAsync(programmeId);
                var fileName = $"Programme_Fees_Invoice_{programme?.Name.Replace(" ", "_")}_Year_{yearOfStudy}_{DateTime.Now:yyyyMMdd}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating programme invoice for Programme ID: {ProgrammeId}, Year: {YearOfStudy}", programmeId, yearOfStudy);
                TempData["Error"] = "An error occurred while generating the invoice. Please try again.";
                return RedirectToAction("ProgrammeDetails", new { id = programmeId });
            }
        }

        private async Task<bool> CheckAndProcessFreeApplication(Applicant applicant)
        {
            try
            {
                var applicableFees = await _context.FeeConfigurations
                    .Include(f => f.FeeType)
                    .Where(f =>
                        f.FeeType.ApplicableFor == "Candidate" &&
                        f.FeeType.IsActive &&
                        f.AcademicYearId == applicant.AcademicYearId &&
                        (
                            f.AppliesUniversally ||
                            (f.ProgrammeId == applicant.ProgrammeId && f.ProgrammeId != null) ||
                            (f.SchoolId == applicant.SchoolId && f.SchoolId != null) ||
                            (f.ProgramLevelId == applicant.ProgrammeLevelId && f.ProgramLevelId != null) ||
                            (f.ModeOfStudyId == applicant.ModeOfStudyId && f.ModeOfStudyId != null)
                        )
                    )
                    .ToListAsync();

                var totalFees = applicableFees.Sum(f => f.Amount);

                if (totalFees == 0)
                {
                    applicant.PaymentStatus = Status.Paid;
                    applicant.IsSubmitted = true;

                    _logger.LogInformation($"Free application auto-processed for {applicant.ReferenceNumber}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking fees for applicant {applicant.ReferenceNumber}");
                return false;
            }
        }
    }
}