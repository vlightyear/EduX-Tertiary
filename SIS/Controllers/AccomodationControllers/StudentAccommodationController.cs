using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.StudentAccommodation;
using SIS.Models.StudentApplication;
using System.Security.Claims;

namespace SIS.Controllers
{
    public class StudentAccommodationController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public StudentAccommodationController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }



        #region Accommodation Periods

        // GET: StudentAccommodation/Periods
        [Authorize(Roles = "Admin,AccommodationManager")]
        public async Task<IActionResult> Periods()
        {
            // Removed AcademicYear include from Period
            var periods = await _context.AccommodationPeriods
                .Include(p => p.Applications)
                .Include(p => p.School)
                .Include(p => p.Programme)
                .Include(p => p.ModeOfStudy)
                .Include(p => p.ProgramLevel)
                .ToListAsync();

            // Get data for sidebar statistics
            ViewBag.TotalPeriods = periods.Count;
            ViewBag.ActivePeriods = periods.Count(p => p.Status == Status.Active);
            ViewBag.UpcomingPeriods = periods.Count(p => p.Status == Status.Upcoming);
            ViewBag.ClosedPeriods = periods.Count(p => p.Status == Status.Closed);

            // Get additional data for dropdowns
            ViewBag.Schools = await _context.Schools.ToListAsync();
            ViewBag.Programmes = await _context.Programmes.ToListAsync();
            ViewBag.ModesOfStudy = await _context.ModesOfStudy.ToListAsync();
            ViewBag.ProgramLevels = await _context.ProgramLevels.ToListAsync();
            ViewBag.FeeConfigurations = await _context.FeeConfigurations
                .Include(f => f.FeeType)
                .ToListAsync();

            // Payment type options for dropdown
            ViewBag.PaymentTypeOptions = new List<string> { "Semester", "Year", "PerDay", "Fixed" };

            return View(periods);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,AccommodationManager")]
        public async Task<IActionResult> GetPeriod(int id)
        {
            // Removed AcademicYear include from Period
            var period = await _context.AccommodationPeriods
                .Include(p => p.School)
                .Include(p => p.Programme)
                .Include(p => p.ModeOfStudy)
                .Include(p => p.ProgramLevel)
                .FirstOrDefaultAsync(p => p.PeriodId == id);

            if (period == null)
            {
                return NotFound();
            }

            // Updated to use TypeOfPayment instead of AcademicYear
            var periodDto = new
            {
                id = period.PeriodId,
                startDate = period.StartDate.ToString("yyyy-MM-dd"),
                endDate = period.EndDate.HasValue ? period.EndDate.Value.ToString("yyyy-MM-dd") : null,
                type = period.Type,
                typeOfPayment = period.TypeOfPayment,
                typeOfPaymentAmount = period.TypeOfPaymentAmount,
                applicationStartDate = period.ApplicationStartDate.ToString("yyyy-MM-dd"),
                applicationEndDate = period.ApplicationEndDate.ToString("yyyy-MM-dd"),
                status = period.Status.ToString(),
                schoolId = period.SchoolId,
                schoolName = period.School?.Name,
                programmeId = period.ProgrammeId,
                programmeName = period.Programme?.Name,
                modeOfStudyId = period.ModeOfStudyId,
                modeOfStudyName = period.ModeOfStudy?.ModeName,
                yearOfStudy = period.YearOfStudy,
                programLevelId = period.ProgramLevelId,
                programLevelName = period.ProgramLevel?.Name,
                isPermanentUntilGraduation = period.IsPermanentUntilGraduation,
                appliesUniversally = period.AppliesUniversally,
                applicationCount = _context.AccommodationApplications.Count(a => a.PeriodId == id),
                // Fee display helper
                feeDisplay = period.TypeOfPayment == "PerDay"
                    ? $"K{period.TypeOfPaymentAmount:N2}/day"
                    : $"K{period.TypeOfPaymentAmount:N2} ({period.TypeOfPayment})"
            };

            return Json(periodDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AccommodationManager")]
        public async Task<IActionResult> CreatePeriod(AccommodationPeriod period)
        {
            try
            {
                // Log received values
                Console.WriteLine($"Received IsPermanentUntilGraduation: {period.IsPermanentUntilGraduation}");
                Console.WriteLine($"Received AppliesUniversally: {period.AppliesUniversally}");
                Console.WriteLine($"Received TypeOfPayment: {period.TypeOfPayment}");
                Console.WriteLine($"Received TypeOfPaymentAmount: {period.TypeOfPaymentAmount}");

                // Set audit fields
                period.CreatedBy = User.Identity.Name;
                period.CreatedAt = DateTime.Now;

                // Set initial status
                if (period.StartDate > DateTime.Now)
                {
                    period.Status = Status.Upcoming;
                }
                else if (period.EndDate.HasValue && period.EndDate < DateTime.Now)
                {
                    period.Status = Status.Closed;
                }
                else
                {
                    period.Status = Status.Active;
                }

                // If applies universally, clear specific settings
                if (period.AppliesUniversally)
                {
                    period.SchoolId = null;
                    period.ProgrammeId = null;
                    period.ModeOfStudyId = null;
                    period.YearOfStudy = null;
                    period.ProgramLevelId = null;
                }

                // If permanent until graduation, clear end date
                if (period.IsPermanentUntilGraduation)
                {
                    period.EndDate = null;
                }

                // Validate TypeOfPayment
                if (string.IsNullOrEmpty(period.TypeOfPayment))
                {
                    period.TypeOfPayment = "Semester"; // Default
                }

                // Validate TypeOfPaymentAmount
                if (period.TypeOfPaymentAmount <= 0)
                {
                    TempData["Error"] = "Payment amount must be greater than zero.";
                    return RedirectToAction(nameof(Periods));
                }

                _context.AccommodationPeriods.Add(period);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Accommodation period created successfully.";
                return RedirectToAction(nameof(Periods));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating accommodation period. Please try again.";
                ModelState.AddModelError("", "Error creating accommodation period. Please try again.");
            }

            // If we got this far, something failed, redisplay form
            ViewBag.Schools = await _context.Schools.ToListAsync();
            ViewBag.Programmes = await _context.Programmes.ToListAsync();
            ViewBag.ModesOfStudy = await _context.ModesOfStudy.ToListAsync();
            ViewBag.ProgramLevels = await _context.ProgramLevels.ToListAsync();
            ViewBag.FeeConfigurations = await _context.FeeConfigurations.ToListAsync();
            ViewBag.PaymentTypeOptions = new List<string> { "Semester", "Year", "PerDay", "Fixed" };

            return RedirectToAction(nameof(Periods));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AccommodationManager")]
        public async Task<IActionResult> UpdatePeriod(AccommodationPeriod period)
        {
            try
            {
                var existingPeriod = await _context.AccommodationPeriods
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PeriodId == period.PeriodId);

                if (existingPeriod == null)
                {
                    TempData["Error"] = "Accommodation period not found.";
                    return NotFound();
                }

                // Preserve the creation audit fields
                period.CreatedBy = existingPeriod.CreatedBy;
                period.CreatedAt = existingPeriod.CreatedAt;

                // Update the modification audit fields
                period.UpdatedAt = DateTime.Now;
                period.UpdatedBy = User.Identity.Name;

                // If applies universally, clear specific settings
                if (period.AppliesUniversally)
                {
                    period.SchoolId = null;
                    period.ProgrammeId = null;
                    period.ModeOfStudyId = null;
                    period.YearOfStudy = null;
                    period.ProgramLevelId = null;
                }

                // If permanent until graduation, clear end date
                if (period.IsPermanentUntilGraduation)
                {
                    period.EndDate = null;
                }

                // Validate TypeOfPayment
                if (string.IsNullOrEmpty(period.TypeOfPayment))
                {
                    period.TypeOfPayment = existingPeriod.TypeOfPayment ?? "Semester";
                }

                // Validate TypeOfPaymentAmount
                if (period.TypeOfPaymentAmount <= 0)
                {
                    TempData["Error"] = "Payment amount must be greater than zero.";
                    return RedirectToAction(nameof(Periods));
                }

                _context.Entry(period).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Accommodation period updated successfully.";
                return RedirectToAction(nameof(Periods));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PeriodExists(period.PeriodId))
                {
                    TempData["Error"] = "Accommodation period not found.";
                    return NotFound();
                }
                else
                {
                    TempData["Error"] = "Error updating accommodation period. Please try again.";
                    ModelState.AddModelError("", "Error updating accommodation period. Please try again.");
                }
            }

            return RedirectToAction(nameof(Periods));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AccommodationManager")]
        public async Task<IActionResult> DeletePeriod(int id)
        {
            var period = await _context.AccommodationPeriods.FindAsync(id);
            if (period == null)
            {
                TempData["Error"] = "Accommodation period not found.";
                return NotFound();
            }

            // Check if there are any applications for this period
            var hasApplications = await _context.AccommodationApplications
                .AnyAsync(a => a.PeriodId == id);

            if (hasApplications)
            {
                TempData["Error"] = "Cannot delete period with existing applications.";
                return RedirectToAction(nameof(Periods));
            }

            try
            {
                _context.AccommodationPeriods.Remove(period);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Accommodation period deleted successfully.";
                return RedirectToAction(nameof(Periods));
            }
            catch (Exception ex)
            {
                // Log the error
                TempData["Error"] = "Error deleting accommodation period. Please try again.";
                ModelState.AddModelError("", "Error deleting accommodation period. Please try again.");
                return RedirectToAction(nameof(Periods));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AccommodationManager")]
        public async Task<IActionResult> ChangePeriodStatus(int id, Status status)
        {
            var period = await _context.AccommodationPeriods.FindAsync(id);

            if (period == null)
            {
                TempData["Error"] = "Accommodation period not found.";
                return NotFound();
            }

            try
            {
                period.Status = status;
                period.UpdatedAt = DateTime.Now;
                period.UpdatedBy = User.Identity.Name;

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Accommodation period status changed to {status}.";
                return RedirectToAction(nameof(Periods));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error changing period status. Please try again.";
                ModelState.AddModelError("", "Error changing period status. Please try again.");
                return RedirectToAction(nameof(Periods));
            }
        }

        private bool PeriodExists(int id)
        {
            return _context.AccommodationPeriods.Any(p => p.PeriodId == id);
        }

        #endregion


        #region Student Application Process

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Dashboard()
        {
            // Get current student
            var username = User.Identity.Name; // This will get the username of the logged-in user
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Username == username);

            if (student == null)
            {
                TempData["Error"] = "Student record not found.";
                return RedirectToAction("Index", "Home");
            }

            // Check if student has an active allocation
            var activeAllocation = await _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(a => a.Period)
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                .FirstOrDefaultAsync(a =>
                    a.Application.StudentId == student.Id &&
                    a.Status == Status.Active);

            if (activeAllocation != null)
            {
                // Student has accommodation, display current details
                ViewBag.HasAccommodation = true;
                ViewBag.Allocation = activeAllocation;

                // Calculate fee information for display
                if (activeAllocation.Application?.Period != null)
                {
                    var period = activeAllocation.Application.Period;
                    decimal fee = period.TypeOfPayment == "PerDay"
                        ? period.TypeOfPaymentAmount * (activeAllocation.Application.NumberOfDays ?? 1)
                        : period.TypeOfPaymentAmount;

                    ViewBag.AccommodationFee = fee;
                    ViewBag.PaymentType = period.TypeOfPayment;
                    ViewBag.NumberOfDays = activeAllocation.Application.NumberOfDays;
                }

                return View();
            }

            // Get all active periods for which the student is eligible (removed AcademicYear include)
            var activePeriods = await _context.AccommodationPeriods
                .Include(p => p.School)
                .Include(p => p.Programme)
                .Include(p => p.ModeOfStudy)
                .Include(p => p.ProgramLevel)
                .Where(p => p.Status == Status.Active &&
                          p.ApplicationStartDate <= DateTime.Now &&
                          p.ApplicationEndDate >= DateTime.Now)
                .ToListAsync();

            // Filter periods based on student eligibility
            var eligiblePeriods = new List<AccommodationPeriod>();

            foreach (var period in activePeriods)
            {
                if (period.AppliesUniversally)
                {
                    eligiblePeriods.Add(period);
                    continue;
                }

                // Check specific criteria
                bool isEligible = true;

                if (period.SchoolId.HasValue && period.SchoolId != student.SchoolId)
                    isEligible = false;

                if (period.ProgrammeId.HasValue && period.ProgrammeId != student.ProgrammeId)
                    isEligible = false;

                if (period.ModeOfStudyId.HasValue && period.ModeOfStudyId != student.ModeOfStudyId)
                    isEligible = false;

                if (period.YearOfStudy.HasValue && period.YearOfStudy != student.StudentCurrentYear)
                    isEligible = false;

                if (period.ProgramLevelId.HasValue && period.ProgramLevelId != student.ProgrammeLevelId)
                    isEligible = false;

                if (isEligible)
                    eligiblePeriods.Add(period);
            }

            // Check if student already has pending applications
            var existingApplications = await _context.AccommodationApplications
                .Where(a => a.StudentId == student.Id &&
                         (a.Status == Status.Pending || a.Status == Status.Active))
                .Select(a => a.PeriodId)
                .ToListAsync();

            // Remove periods where the student already has an application
            eligiblePeriods = eligiblePeriods
                .Where(p => !existingApplications.Contains(p.PeriodId))
                .ToList();

            ViewBag.HasAccommodation = false;
            ViewBag.EligiblePeriods = eligiblePeriods;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Apply(int periodId)
        {
            try
            {
                // Get current student
                var username = User.Identity.Name; // This will get the username of the logged-in user
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Username == username);

                if (student == null)
                {
                    TempData["Error"] = "Student record not found.";
                    return RedirectToAction("Dashboard");
                }

                // Check if period exists and is active
                var period = await _context.AccommodationPeriods
                    .FirstOrDefaultAsync(p => p.PeriodId == periodId &&
                                          p.Status == Status.Active &&
                                          p.ApplicationStartDate <= DateTime.Now &&
                                          p.ApplicationEndDate >= DateTime.Now);

                if (period == null)
                {
                    TempData["Error"] = "Accommodation period not found or not active.";
                    return RedirectToAction("Dashboard");
                }

                // Check student eligibility for this period
                bool isEligible = true;

                if (!period.AppliesUniversally)
                {
                    if (period.SchoolId.HasValue && period.SchoolId != student.SchoolId)
                        isEligible = false;

                    if (period.ProgrammeId.HasValue && period.ProgrammeId != student.ProgrammeId)
                        isEligible = false;

                    if (period.ModeOfStudyId.HasValue && period.ModeOfStudyId != student.ModeOfStudyId)
                        isEligible = false;

                    if (period.YearOfStudy.HasValue && period.YearOfStudy != student.StudentCurrentYear)
                        isEligible = false;

                    if (period.ProgramLevelId.HasValue && period.ProgramLevelId != student.ProgrammeLevelId)
                        isEligible = false;
                }

                if (!isEligible)
                {
                    TempData["Error"] = "You are not eligible for this accommodation period.";
                    return RedirectToAction("Dashboard");
                }

                // Check if student already has an application for this period
                var existingApplication = await _context.AccommodationApplications
                    .FirstOrDefaultAsync(a => a.StudentId == student.Id &&
                                      a.PeriodId == periodId &&
                                      (a.Status == Status.Pending || a.Status == Status.Active));

                if (existingApplication != null)
                {
                    TempData["Error"] = "You already have an active application for this period.";
                    return RedirectToAction("Dashboard");
                }

                // Create new application
                var application = new AccommodationApplication
                {
                    StudentId = student.Id,
                    PeriodId = periodId,
                    ApplicationDate = DateTime.Now,
                    Status = Status.Pending,
                    Notes = "Applied through student portal",
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity.Name
                };

                _context.AccommodationApplications.Add(application);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Application submitted successfully. You can check the status in My Applications.";
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while submitting your application. Please try again.";
                // Log the exception
                return RedirectToAction("Dashboard");
            }
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyApplications()
        {
            // Get current student
            var username = User.Identity.Name; // This will get the username of the logged-in user
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Username == username);

            if (student == null)
            {
                TempData["Error"] = "Student record not found.";
                return RedirectToAction("Index", "Home");
            }

            // Get all applications for the student (removed AcademicYear include from Period)
            var applications = await _context.AccommodationApplications
                .Include(a => a.Period)
                .Include(a => a.Allocation)
                .Where(a => a.StudentId == student.Id)
                .OrderByDescending(a => a.ApplicationDate)
                .ToListAsync();

            // Get statistics for dashboard
            ViewBag.TotalApplications = applications.Count;
            ViewBag.PendingApplications = applications.Count(a => a.Status == Status.Pending);
            ViewBag.ApprovedApplications = applications.Count(a => a.Status == Status.Approved);
            ViewBag.RejectedApplications = applications.Count(a => a.Status == Status.Rejected);
            ViewBag.ActiveAllocations = applications.Count(a => a.Allocation != null && a.Allocation.Status == Status.Active);

            return View(applications);
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> ApplicationDetails(int id)
        {
            // Get current student
            var username = User.Identity.Name; // This will get the username of the logged-in user
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Username == username);

            if (student == null)
            {
                TempData["Error"] = "Student record not found.";
                return RedirectToAction("Index", "Home");
            }

            var studentId = student?.Id;

            // Find the application with details (removed AcademicYear include from Period)
            var application = await _context.AccommodationApplications
                .Include(a => a.Period)
                .Include(a => a.Allocation)
                    .ThenInclude(a => a.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                .FirstOrDefaultAsync(a => a.ApplicationId == id && a.StudentId == Convert.ToInt32(studentId));

            if (application == null)
            {
                TempData["Error"] = "Application not found or you don't have permission to view it.";
                return RedirectToAction("MyApplications");
            }

            // Calculate fee information
            if (application.Period != null)
            {
                decimal fee = application.Period.TypeOfPayment == "PerDay"
                    ? application.Period.TypeOfPaymentAmount * (application.NumberOfDays ?? 1)
                    : application.Period.TypeOfPaymentAmount;

                ViewBag.AccommodationFee = fee;
                ViewBag.PaymentType = application.Period.TypeOfPayment;
                ViewBag.PaymentAmount = application.Period.TypeOfPaymentAmount;
                ViewBag.NumberOfDays = application.NumberOfDays;
                ViewBag.FeeBreakdown = application.Period.TypeOfPayment == "PerDay"
                    ? $"K{application.Period.TypeOfPaymentAmount:N2}/day × {application.NumberOfDays ?? 1} days = K{fee:N2}"
                    : $"K{fee:N2} ({application.Period.TypeOfPayment})";
            }

            // Get check-in/out information if available
            if (application.Allocation != null)
            {
                var checkInfo = await _context.CheckInOuts
                    .FirstOrDefaultAsync(c => c.AllocationId == application.Allocation.AllocationId);

                ViewBag.CheckInOutInfo = checkInfo;
            }

            return View(application);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CancelApplication(int id)
        {
            // Get current student
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Find the application
            var application = await _context.AccommodationApplications
                .FirstOrDefaultAsync(a => a.ApplicationId == id &&
                                  a.StudentId == Convert.ToInt32(studentId) &&
                                  a.Status == Status.Pending);

            if (application == null)
            {
                TempData["Error"] = "Application not found, already processed, or you don't have permission to cancel it.";
                return RedirectToAction("MyApplications");
            }

            try
            {
                // Update application status
                application.Status = Status.Canceled;
                application.UpdatedAt = DateTime.Now;
                application.UpdatedBy = User.Identity.Name;
                application.Notes += $"\nCancelled by student on {DateTime.Now}.";

                await _context.SaveChangesAsync();

                TempData["Success"] = "Application cancelled successfully.";
                return RedirectToAction("MyApplications");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while cancelling your application. Please try again.";
                // Log the exception
                return RedirectToAction("MyApplications");
            }
        }

        // GET: StudentAccommodation/GetRecentApplications
        [HttpGet]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetRecentApplications()
        {
            // Get current student using Username
            var username = User.Identity.Name;
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Username == username);

            if (student == null)
            {
                return Json(new { error = "Student record not found" });
            }

            // Get 3 most recent applications (updated to use TypeOfPayment instead of AcademicYear)
            var recentApplications = await _context.AccommodationApplications
                .Include(a => a.Period)
                .Where(a => a.StudentId == student.Id)
                .OrderByDescending(a => a.ApplicationDate)
                .Take(3)
                .Select(a => new
                {
                    applicationId = a.ApplicationId,
                    periodId = a.PeriodId,
                    // Updated to use Type and TypeOfPayment instead of AcademicYear
                    periodName = a.Period.Type + " - " + a.Period.TypeOfPayment,
                    applicationDate = a.ApplicationDate,
                    status = a.Status.ToString(),
                    fee = a.Period.TypeOfPayment == "PerDay"
                        ? a.Period.TypeOfPaymentAmount * (a.NumberOfDays ?? 1)
                        : a.Period.TypeOfPaymentAmount,
                    feeDisplay = a.Period.TypeOfPayment == "PerDay"
                        ? $"K{a.Period.TypeOfPaymentAmount:N2}/day"
                        : $"K{a.Period.TypeOfPaymentAmount:N2}"
                })
                .ToListAsync();

            return Json(recentApplications);
        }

        #endregion



        // GET: StudentAccommodation/HostelManagerDashboard
        [Authorize(Roles = "HostelManager")]
        public async Task<IActionResult> HostelManagerDashboard()
        {
            // Get currently logged in user
            var username = User.Identity.Name;

            // Fetch hostels managed by this user
            var managedHostels = await _context.Hostels
                .Where(h => h.Warden.UserName == username)
                .ToListAsync();

            if (managedHostels == null || !managedHostels.Any())
            {
                TempData["Error"] = "No hostels assigned to your account.";
                return View(new HostelManagerDashboardViewModel
                {
                    ManagedHostels = new List<Hostel>(),
                    TotalBeds = 0,
                    OccupiedBeds = 0,
                    AvailableBeds = 0,
                    MaintenanceRequests = new List<MaintenanceRequest>(),
                    PendingApplications = 0,
                    RecentCheckIns = new List<CheckInOut>(),
                    ResourcesNeedingRepair = 0
                });
            }

            var hostelIds = managedHostels.Select(h => h.HostelId).ToList();

            // Get rooms in these hostels
            var rooms = await _context.Rooms
                .Where(r => hostelIds.Contains(r.HostelId))
                .ToListAsync();

            var roomIds = rooms.Select(r => r.RoomId).ToList();

            // Get bed spaces
            var beds = await _context.BedSpaces
                .Where(b => roomIds.Contains(b.RoomId))
                .ToListAsync();

            // Get maintenance requests
            var maintenanceRequests = await _context.MaintenanceRequests
                .Where(m => roomIds.Contains(m.RoomId))
                .OrderByDescending(m => m.RequestDate)
                .Take(5)
                .ToListAsync();

            // Get resources needing repair
            var resourcesNeedingRepair = await _context.RoomResources
                .CountAsync(r => roomIds.Contains(r.RoomId) &&
                              r.Status == Status.NeedsRepair);

            // Get active accommodation period
            var activePeriod = await _context.AccommodationPeriods
                .Where(p => p.Status == Status.Active)
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            int pendingApplications = 0;

            if (activePeriod != null)
            {
                // Get pending applications for the current period
                pendingApplications = await _context.AccommodationApplications
                    .CountAsync(a => a.PeriodId == activePeriod.PeriodId &&
                                 a.Status == Status.Pending);
            }

            // Get recent check-ins/check-outs
            var recentChecks = await _context.CheckInOuts
                .Include(c => c.Allocation)
                    .ThenInclude(a => a.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                .Include(c => c.Allocation.Application.Student)
                .Where(c => hostelIds.Contains(c.Allocation.Bed.Room.HostelId))
                .OrderByDescending(c => c.CheckInDate ?? c.CheckOutDate)
                .Take(5)
                .ToListAsync();

            // Calculate totals and metrics
            int totalBeds = beds.Count();
            int occupiedBeds = beds.Count(b => b.Status == Status.Occupied);
            int availableBeds = beds.Count(b => b.Status == Status.Available);
            int maintenanceBeds = beds.Count(b => b.Status == Status.Maintenance);
            int reservedBeds = beds.Count(b => b.Status == Status.Reserved);

            // Prepare room status data
            var roomStatusData = new
            {
                available = rooms.Count(r => r.Status == Status.Available),
                maintenance = rooms.Count(r => r.Status == Status.Maintenance),
                reserved = rooms.Count(r => r.Status == Status.Reserved)
            };

            // Create view model
            var viewModel = new HostelManagerDashboardViewModel
            {
                ManagedHostels = managedHostels,
                TotalBeds = totalBeds,
                OccupiedBeds = occupiedBeds,
                AvailableBeds = availableBeds,
                MaintenanceBeds = maintenanceBeds,
                ReservedBeds = reservedBeds,
                MaintenanceRequests = maintenanceRequests,
                PendingApplications = pendingApplications,
                RecentCheckIns = recentChecks,
                ResourcesNeedingRepair = resourcesNeedingRepair,
                RoomStatusData = roomStatusData
            };

            // Add chart data
            ViewBag.OccupancyData = Newtonsoft.Json.JsonConvert.SerializeObject(new[] {
        occupiedBeds, availableBeds, maintenanceBeds, reservedBeds
    });

            ViewBag.HostelNames = Newtonsoft.Json.JsonConvert.SerializeObject(
                managedHostels.Select(h => h.HostelName).ToArray()
            );

            ViewBag.HostelOccupancyData = Newtonsoft.Json.JsonConvert.SerializeObject(
                managedHostels.Select(h => {
                    var hostelRooms = rooms.Where(r => r.HostelId == h.HostelId).ToList();
                    var hostelRoomIds = hostelRooms.Select(r => r.RoomId).ToList();
                    var hostelBeds = beds.Where(b => hostelRoomIds.Contains(b.RoomId)).ToList();
                    var occupancy = hostelBeds.Count() > 0
                        ? (double)hostelBeds.Count(b => b.Status == Status.Occupied) / hostelBeds.Count * 100
                        : 0;
                    return Math.Round(occupancy, 1);
                }).ToArray()
            );

            return View(viewModel);
        }



    }
}