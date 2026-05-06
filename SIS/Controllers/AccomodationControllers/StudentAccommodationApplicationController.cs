using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Interfaces;
using SIS.Models.StudentAccommodation;
using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations;

namespace SIS.Controllers.AccomodationControllers
{
    [Authorize]
    public class StudentAccommodationApplicationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccommodationAllocationService _accommodationAllocationService;

        public StudentAccommodationApplicationController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAccommodationAllocationService accommodationAllocationService)
        {
            _context = context;
            _userManager = userManager;
            _accommodationAllocationService = accommodationAllocationService;
        }

        // GET: Student Accommodation Portal
        public async Task<IActionResult> Index()
        {
            // Get current student using proper identification
            var student = await GetCurrentStudentAsync();

            if (student == null)
            {
                TempData["Error"] = "Student account not found or not properly configured. Please contact support.";
                return View("~/Views/Accommodation/StudentApplication/AccessDenied.cshtml");
            }

            // Get current accommodation configuration
            var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();
            if (config == null)
            {
                TempData["Error"] = "Accommodation system is not configured. Please contact administration.";
                return View("~/Views/Accommodation/StudentApplication/Error.cshtml");
            }

            // Get current accommodation period that falls within the application date range
            var currentDate = DateTime.Now;
            var currentPeriod = await _context.AccommodationPeriods
                .Include(ap => ap.School)
                .Include(ap => ap.Programme)
                .Include(ap => ap.ModeOfStudy)
                .Include(ap => ap.ProgramLevel)
                .Where(p => p.ApplicationStartDate <= currentDate &&
                           p.ApplicationEndDate >= currentDate &&
                           p.Status == Status.Active)
                .FirstOrDefaultAsync();

            if (currentPeriod == null)
            {
                ViewBag.Message = "There is no active accommodation period at the moment. Applications are currently not being accepted.";
                return View("~/Views/Accommodation/StudentApplication/NoActivePeriod.cshtml");
            }

            // CHECK 1: Does student have an ACTIVE bed allocation from ANY period?
            var hasActiveAllocation = await _context.Allocations
                .AnyAsync(a => a.Application.StudentId == student.Id &&
                              a.Status == Status.Active &&
                              (!a.EndDate.HasValue || a.EndDate.Value > currentDate));

            if (hasActiveAllocation)
            {
                return RedirectToAction("CurrentAllocation");
            }

            // CHECK 2: Verify student doesn't have a bed assignment in the Student table (data inconsistency check)
            if (student.BedId.HasValue && student.BedAllocationEndDate.HasValue &&
                student.BedAllocationEndDate.Value > currentDate)
            {
                var bedSpace = await _context.BedSpaces
                    .Include(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                    .FirstOrDefaultAsync(b => b.BedId == student.BedId.Value);

                if (bedSpace != null)
                {
                    ViewBag.Config = config;
                    ViewBag.HasActiveAllocation = true;
                    ViewBag.InconsistentData = true;
                    ViewBag.StudentBedSpace = bedSpace;
                    ViewBag.BedAllocationEndDate = student.BedAllocationEndDate;
                    return View("~/Views/Accommodation/StudentApplication/CurrentAllocationInconsistent.cshtml");
                }
            }

            // CHECK 3: Check for existing application for the current period
            var existingApplication = await _context.AccommodationApplications
                .Include(a => a.Student)
                .Include(a => a.Period)
                .Include(a => a.BedSpace)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .Include(a => a.Allocation)
                .Where(a => a.StudentId == student.Id && a.PeriodId == currentPeriod.PeriodId)
                .OrderByDescending(a => a.ApplicationDate)
                .FirstOrDefaultAsync();

            if (existingApplication != null)
            {
                // Check the status of the existing application
                switch (existingApplication.Status)
                {
                    case Status.Active:
                    case Status.Approved:
                        // Application is approved - should have allocation, redirect to allocation view
                        if (existingApplication.Allocation != null)
                        {
                            return RedirectToAction("CurrentAllocation");
                        }
                        else
                        {
                            // Approved but no allocation record - data inconsistency
                            // Allow them to see their application status
                            return RedirectToAction("ApplicationStatus", new { applicationId = existingApplication.ApplicationId });
                        }

                    case Status.Pending:
                        // Application is pending payment/processing
                        // Check if reservation has expired
                        var reservationExpiryTime = existingApplication.ApplicationDate.AddHours(config.ReservationHoursValidity);
                        if (reservationExpiryTime < currentDate && existingApplication.Allocation == null)
                        {
                            // Reservation expired without allocation - will be cleaned up
                            // Allow them to see expired status and reapply
                            ViewBag.HasExpiredApplication = true;
                            ViewBag.ExpirationMessage = $"Your previous application submitted on {existingApplication.ApplicationDate:MMMM dd, yyyy 'at' hh:mm tt} expired because payment was not received within {config.ReservationHoursValidity} hours. You can submit a new application below.";
                            break; // Continue to eligibility check and show application form
                        }
                        else
                        {
                            // Still pending and within valid timeframe
                            return RedirectToAction("ApplicationStatus", new { applicationId = existingApplication.ApplicationId });
                        }

                    case Status.Rejected:
                    case Status.Canceled:
                        // Application was rejected or canceled - allow new application
                        ViewBag.HasExpiredApplication = true;
                        ViewBag.ExpirationMessage = $"Your previous application was {existingApplication.Status.ToString().ToLower()}. You can submit a new application below.";
                        break;

                    case Status.Completed:
                        // Application completed (accommodation period ended) - allow new application for new period
                        break;

                    default:
                        // Unknown status - show application form
                        break;
                }
            }

            // No active allocation or pending application - proceed with eligibility check
            var eligibilityResult = await CheckStudentEligibilityWithReasons(student, currentPeriod);

            // Calculate maximum days allowed for PerDay payment type
            int maxDaysAllowed = 0;
            if (currentPeriod.TypeOfPayment == "PerDay" && currentPeriod.EndDate.HasValue)
            {
                maxDaysAllowed = (int)(currentPeriod.EndDate.Value - currentPeriod.StartDate).TotalDays;
                if (maxDaysAllowed < 1) maxDaysAllowed = 1;
            }

            // Pass all data to the view
            ViewBag.Config = config;
            ViewBag.Period = currentPeriod;
            ViewBag.Student = student;
            ViewBag.IsEligible = eligibilityResult.IsEligible;
            ViewBag.IneligibilityReasons = eligibilityResult.Reasons;
            ViewBag.MaxDaysAllowed = maxDaysAllowed;

            return View("~/Views/Accommodation/StudentApplication/StudentAccommodationApplication_Index.cshtml");
        }

        // GET: Application Status View
        [HttpGet]
        public async Task<IActionResult> ApplicationStatus(int applicationId)
        {
            var student = await GetCurrentStudentAsync();

            if (student == null)
            {
                TempData["Error"] = "Student account not found or not properly configured. Please contact support.";
                return RedirectToAction("Index");
            }

            var application = await _context.AccommodationApplications
                .Include(a => a.Student)
                .Include(a => a.Period)
                .Include(a => a.BedSpace)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                                .ThenInclude(h => h.Campus)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.StudentId == student.Id);

            if (application == null)
            {
                TempData["Error"] = "Application not found.";
                return RedirectToAction("Index");
            }

            var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();

            // Calculate reservation expiry
            var reservationExpiryTime = application.ApplicationDate.AddHours(config?.ReservationHoursValidity ?? 24);
            var isReservationExpired = reservationExpiryTime < DateTime.Now && application.Allocation == null;

            // Get student balance
            var studentBalance = _accommodationAllocationService.GetStudentOutstandingBalance(student.Id);

            // Calculate accommodation fee based on period settings
            decimal accommodationFee = CalculateAccommodationFee(application.Period, application.NumberOfDays);
            var hasSufficientBalance = studentBalance >= accommodationFee;

            ViewBag.Config = config;
            ViewBag.Application = application;
            ViewBag.ReservationExpiryTime = reservationExpiryTime;
            ViewBag.IsReservationExpired = isReservationExpired;
            ViewBag.StudentBalance = studentBalance;
            ViewBag.AccommodationFee = accommodationFee;
            ViewBag.HasSufficientBalance = hasSufficientBalance;

            decimal TotalBilled = StudentTools.GetStudentTotalFees(student.Id);
            decimal TotalPaid = StudentTools.GetStudentTotalPaid(student.Id);

            ViewData["TotalBilled"] = TotalBilled;
            ViewData["TotalPaid"] = TotalPaid;
            ViewData["TransactionReference"] = "ACCOM_" + applicationId + "_" + student.Id;

            return View("~/Views/Accommodation/StudentApplication/PendingApplication.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> GetCampuses()
        {
            try
            {
                var campuses = await _context.Campuses
                    .OrderBy(c => c.CampusName)
                    .Select(c => new
                    {
                        campusId = c.CampusId,
                        campusName = c.CampusName
                    })
                    .ToListAsync();

                return Json(campuses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCampuses: {ex.Message}");
                return Json(new List<object>());
            }
        }

        // GET: Current Allocation
        [HttpGet]
        public async Task<IActionResult> CurrentAllocation()
        {
            var student = await GetCurrentStudentAsync();

            if (student == null)
            {
                TempData["Error"] = "Student account not found or not properly configured. Please contact support.";
                return RedirectToAction("Index");
            }

            var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();
            if (config == null)
            {
                TempData["Error"] = "Accommodation system is not configured. Please contact administration.";
                return View("~/Views/Accommodation/StudentApplication/Error.cshtml");
            }

            var currentDate = DateTime.Now;

            var currentActiveAllocation = await _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                    .ThenInclude(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                .Include(a => a.Application)
                    .ThenInclude(app => app.Period)
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .Include(a => a.CheckInOut)
                    .ThenInclude(c => c.CheckInStaff)
                .Include(a => a.CheckInOut)
                    .ThenInclude(c => c.CheckOutStaff)
                .Include(a => a.AllocatedBy)
                .Where(a => a.Application.StudentId == student.Id &&
                           a.Status == Status.Active &&
                           (!a.EndDate.HasValue || a.EndDate.Value > currentDate))
                .FirstOrDefaultAsync();

            if (currentActiveAllocation == null)
            {
                TempData["Info"] = "You do not currently have an active accommodation allocation.";
                return RedirectToAction("Index");
            }

            var checkInOut = currentActiveAllocation.CheckInOut;
            var checkInOutStatus = DetermineCheckInOutStatus(checkInOut);

            ViewBag.Config = config;
            ViewBag.HasActiveAllocation = true;
            ViewBag.CurrentAllocation = currentActiveAllocation;
            ViewBag.CheckInOutStatus = checkInOutStatus;
            ViewBag.CheckInOut = checkInOut;

            return View("~/Views/Accommodation/StudentApplication/StudentAccommodationCurrentAllocation_Index.cshtml", currentActiveAllocation);
        }

        /// <summary>
        /// Helper method to determine check-in/out status
        /// </summary>
        private string DetermineCheckInOutStatus(CheckInOut checkInOut)
        {
            if (checkInOut == null)
            {
                return "Not Checked In";
            }

            if (checkInOut.CheckOutDate.HasValue)
            {
                return "Checked Out";
            }

            if (checkInOut.CheckInDate.HasValue)
            {
                return "Checked In";
            }

            return "Pending Check-In";
        }

        /// <summary>
        /// Automatic cleanup method to handle expired reservations and allocations
        /// </summary>
        private async System.Threading.Tasks.Task PerformAutomaticCleanupAsync()
        {
            try
            {
                var currentDate = DateTime.Now;
                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();

                if (config == null)
                    return;

                // STEP 1: Clean up expired reservations
                var expiredApplications = await _context.AccommodationApplications
                    .Include(a => a.Allocation)
                    .Where(a => a.Status == Status.Pending &&
                               a.Allocation == null &&
                               a.ApplicationDate.AddHours(config.ReservationHoursValidity) < currentDate)
                    .ToListAsync();

                if (expiredApplications.Any())
                {
                    foreach (var application in expiredApplications)
                    {
                        application.Status = Status.Rejected;
                        application.Notes += $" [SYSTEM: Reservation expired - No payment within {config.ReservationHoursValidity} hours. Expired on {application.ApplicationDate.AddHours(config.ReservationHoursValidity):yyyy-MM-dd HH:mm}]";
                        application.UpdatedAt = currentDate;
                        application.UpdatedBy = "SYSTEM_CLEANUP";

                        // Free the selected bed if it was reserved
                        if (application.SelectedBedId.HasValue)
                        {
                            var bed = await _context.BedSpaces.FindAsync(application.SelectedBedId.Value);
                            if (bed != null && bed.Status == Status.Reserved)
                            {
                                bed.Status = Status.Available;
                                bed.UpdatedAt = currentDate;
                                bed.UpdatedBy = "SYSTEM_CLEANUP";
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                // STEP 2: Clean up expired allocations
                var expiredAllocations = await _context.Allocations
                    .Include(a => a.Application)
                        .ThenInclude(app => app.Student)
                    .Include(a => a.Bed)
                    .Where(a => a.Status == Status.Active &&
                               !a.IsGraduationBased &&
                               a.EndDate.HasValue &&
                               a.EndDate.Value < currentDate)
                    .ToListAsync();

                if (expiredAllocations.Any())
                {
                    foreach (var allocation in expiredAllocations)
                    {
                        allocation.Status = Status.Completed;
                        allocation.UpdatedAt = currentDate;
                        allocation.UpdatedBy = "SYSTEM_CLEANUP";

                        if (allocation.Bed != null)
                        {
                            allocation.Bed.Status = Status.Available;
                            allocation.Bed.UpdatedAt = currentDate;
                            allocation.Bed.UpdatedBy = "SYSTEM_CLEANUP";
                        }

                        if (allocation.Application?.Student != null)
                        {
                            var student = allocation.Application.Student;
                            student.BedId = null;
                            student.BedAllocationEndDate = null;
                            student.HasAccommodationClearance = false;
                            student.UpdatedAt = currentDate;
                            student.UpdatedBy = "SYSTEM_CLEANUP";
                        }

                        if (allocation.Application != null)
                        {
                            allocation.Application.Status = Status.Completed;
                            allocation.Application.Notes += $" [SYSTEM: Accommodation period ended on {allocation.EndDate:yyyy-MM-dd}]";
                            allocation.Application.UpdatedAt = currentDate;
                            allocation.Application.UpdatedBy = "SYSTEM_CLEANUP";
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                // STEP 3: Handle graduated students
                var graduatedStudentAllocations = await _context.Allocations
                    .Include(a => a.Application)
                        .ThenInclude(app => app.Student)
                    .Include(a => a.Bed)
                    .Where(a => a.Status == Status.Active &&
                               a.IsGraduationBased &&
                               a.Application.Student.StudentStatus == Status.Completed)
                    .ToListAsync();

                if (graduatedStudentAllocations.Any())
                {
                    foreach (var allocation in graduatedStudentAllocations)
                    {
                        allocation.Status = Status.Completed;
                        allocation.EndDate = currentDate;
                        allocation.UpdatedAt = currentDate;
                        allocation.UpdatedBy = "SYSTEM_CLEANUP_GRADUATION";

                        if (allocation.Bed != null)
                        {
                            allocation.Bed.Status = Status.Available;
                            allocation.Bed.UpdatedAt = currentDate;
                            allocation.Bed.UpdatedBy = "SYSTEM_CLEANUP_GRADUATION";
                        }

                        if (allocation.Application?.Student != null)
                        {
                            var student = allocation.Application.Student;
                            student.BedId = null;
                            student.BedAllocationEndDate = null;
                            student.HasAccommodationClearance = false;
                            student.UpdatedAt = currentDate;
                            student.UpdatedBy = "SYSTEM_CLEANUP_GRADUATION";
                        }

                        if (allocation.Application != null)
                        {
                            allocation.Application.Status = Status.Completed;
                            allocation.Application.Notes += $" [SYSTEM: Student graduated on {currentDate:yyyy-MM-dd}]";
                            allocation.Application.UpdatedAt = currentDate;
                            allocation.Application.UpdatedBy = "SYSTEM_CLEANUP_GRADUATION";
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PerformAutomaticCleanupAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to get student from current user
        /// </summary>
        private async Task<Student> GetCurrentStudentAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            var student = await _context.Students
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Include(s => s.AcademicYear)
                .Include(s => s.ModeOfStudy)
                .Include(s => s.ProgrammeLevel)
                .FirstOrDefaultAsync(s => s.Email == user.Email);

            if (student != null && !await _userManager.IsInRoleAsync(user, "Student"))
            {
                return null;
            }

            return student;
        }

        /// <summary>
        /// Alternative helper to get student by search term
        /// </summary>
        private async Task<Student> GetStudentBySearchTermAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null;

            Student student = null;
            bool isEmail = searchTerm.Contains("@");

            if (isEmail)
            {
                student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Include(s => s.AcademicYear)
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.ProgrammeLevel)
                    .FirstOrDefaultAsync(s => s.Email == searchTerm);
            }
            else
            {
                student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Include(s => s.AcademicYear)
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.ProgrammeLevel)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == searchTerm);
            }

            if (student != null)
            {
                var user = await _userManager.FindByEmailAsync(student.Email);
                if (user == null || !await _userManager.IsInRoleAsync(user, "Student"))
                {
                    return null;
                }
            }

            return student;
        }

        /// <summary>
        /// Check student eligibility with specific reasons
        /// </summary>
        private async Task<EligibilityResult> CheckStudentEligibilityWithReasons(Student student, AccommodationPeriod period)
        {
            var result = new EligibilityResult { IsEligible = true, Reasons = new List<string>() };

            // Check if student is blacklisted
            if (student.IsBlackListedFromAccommodation)
            {
                result.IsEligible = false;
                result.Reasons.Add("You are currently blacklisted from accommodation services. Please contact the accommodation office to resolve this issue.");
                return result; // Return immediately if blacklisted
            }

            if (period.AppliesUniversally)
            {
                return result;
            }

            // Check School eligibility
            if (period.SchoolId.HasValue && period.SchoolId.Value > 0)
            {
                var studentSchoolId = student.Programme?.Department?.SchoolId;
                if (!studentSchoolId.HasValue || studentSchoolId.Value != period.SchoolId.Value)
                {
                    result.IsEligible = false;
                    var periodSchoolName = period.School?.Name ?? "specified school";
                    var studentSchoolName = student.Programme?.Department?.School?.Name ?? "your school";
                    result.Reasons.Add($"This accommodation period is only available for students in {periodSchoolName}. You are enrolled in {studentSchoolName}.");
                }
            }

            // Check Programme eligibility
            if (period.ProgrammeId.HasValue && period.ProgrammeId.Value > 0)
            {
                if (student.ProgrammeId == null || student.ProgrammeId != period.ProgrammeId)
                {
                    result.IsEligible = false;
                    var periodProgrammeName = period.Programme?.Name ?? "specified programme";
                    var studentProgrammeName = student.Programme?.Name ?? "your programme";
                    result.Reasons.Add($"This accommodation period is only available for students in {periodProgrammeName}. You are enrolled in {studentProgrammeName}.");
                }
            }

            // Check Mode of Study eligibility
            if (period.ModeOfStudyId.HasValue && period.ModeOfStudyId.Value > 0)
            {
                if (student.ModeOfStudyId == null || student.ModeOfStudyId != period.ModeOfStudyId)
                {
                    result.IsEligible = false;
                    var periodModeName = period.ModeOfStudy?.ModeName ?? "specified mode of study";
                    var studentModeName = student.ModeOfStudy?.ModeName ?? "your mode of study";
                    result.Reasons.Add($"This accommodation period is only available for {periodModeName} students. You are enrolled as {studentModeName}.");
                }
            }

            // Check Year of Study eligibility
            if (period.YearOfStudy.HasValue && period.YearOfStudy.Value > 0)
            {
                if (!student.StudentCurrentYear.HasValue || student.StudentCurrentYear.Value != period.YearOfStudy.Value)
                {
                    result.IsEligible = false;
                    result.Reasons.Add($"This accommodation period is only available for Year {period.YearOfStudy.Value} students. You are currently in Year {student.StudentCurrentYear?.ToString() ?? "unknown"}.");
                }
            }

            // Check Programme Level eligibility
            if (period.ProgramLevelId.HasValue && period.ProgramLevelId.Value > 0)
            {
                if (student.ProgrammeLevelId == null || student.ProgrammeLevelId != period.ProgramLevelId)
                {
                    result.IsEligible = false;
                    var periodLevelName = period.ProgramLevel?.Name ?? "specified programme level";
                    var studentLevelName = student.ProgrammeLevel?.Name ?? "your programme level";
                    result.Reasons.Add($"This accommodation period is only available for {periodLevelName} students. You are enrolled at {studentLevelName} level.");
                }
            }

            return result;
        }

        /// <summary>
        /// Calculate accommodation fee based on period settings and number of days
        /// </summary>
        private decimal CalculateAccommodationFee(AccommodationPeriod period, int? numberOfDays)
        {
            if (period == null) return 0;

            switch (period.TypeOfPayment)
            {
                case "PerDay":
                    return period.TypeOfPaymentAmount * (numberOfDays ?? 1);
                case "Semester":
                case "Year":
                case "Fixed":
                default:
                    return period.TypeOfPaymentAmount;
            }
        }

        /// <summary>
        /// Calculate maximum days allowed for a period
        /// </summary>
        private int CalculateMaxDaysAllowed(AccommodationPeriod period)
        {
            if (period == null || !period.EndDate.HasValue) return 365; // Default max

            var maxDays = (int)(period.EndDate.Value - period.StartDate).TotalDays;
            return maxDays > 0 ? maxDays : 1;
        }

        // GET: Available rooms for application
        [HttpGet]
        public async Task<IActionResult> GetAvailableRooms(string gender, int? campusId, int? hostelId)
        {
            var student = await GetCurrentStudentAsync();

            if (student == null)
            {
                return Json(new { success = false, message = "Student not found or not authenticated properly" });
            }

            // Check if student is blacklisted
            if (student.IsBlackListedFromAccommodation)
            {
                return Json(new
                {
                    success = false,
                    message = "You are currently blacklisted from accommodation services. Please contact the accommodation office for more information."
                });
            }

            var studentGender = student.Gender ?? gender;
            var studentYear = student.StudentCurrentYear ?? 0;

            var query = _context.BedSpaces
                .Include(b => b.Room)
                    .ThenInclude(r => r.Hostel)
                        .ThenInclude(h => h.Campus)
                .Where(b => b.Status == Status.Available)
                .Where(b => b.Room.Gender == studentGender || b.Room.Gender == "Mixed")
                .Where(b => b.Room.Status == Status.Available)
                .Where(b => b.Room.Hostel.Status == Status.Active)
                // Filter by year: bed must be for all years (0) OR match student's year
                .Where(b => b.CurrentStudentYear == 0 || b.CurrentStudentYear == studentYear)
                // Filter by semester: bed must be for all semesters (0) OR match student's semester
                .Where(b => b.AcademicPeriodId == 0 || b.AcademicPeriodId == student.CurrentYearPeriod.AcademicPeriodId);

            if (campusId.HasValue)
            {
                query = query.Where(b => b.Room.Hostel.CampusId == campusId.Value);
            }

            if (hostelId.HasValue)
            {
                query = query.Where(b => b.Room.HostelId == hostelId.Value);
            }

            var beds = await query
                .Select(b => new
                {
                    bedId = b.BedId,
                    bedIdentifier = b.BedIdentifier,
                    roomNumber = b.Room.RoomNumber,
                    roomType = b.Room.RoomType,
                    floor = b.Room.Floor,
                    hostelName = b.Room.Hostel.HostelName,
                    hostelId = b.Room.HostelId,
                    roomId = b.RoomId,
                    campusName = b.Room.Hostel.Campus.CampusName,
                    isSpecialReservation = b.IsSpecialReservation,
                    currentStudentYear = b.CurrentStudentYear,
                    AcademicPeriodId = b.AcademicPeriodId
                })
                .ToListAsync();

            return Json(new { success = true, beds });
        }

        // POST: Submit accommodation application with REQUIRED bed selection
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitApplication(int periodId, int selectedBedId, string notes, int? numberOfDays)
        {
            var student = await GetCurrentStudentAsync();

            if (student == null)
            {
                return Json(new { success = false, message = "Student not found or not authenticated" });
            }

            // CRITICAL: Validate bed selection
            if (selectedBedId <= 0)
            {
                return Json(new { success = false, message = "Please select a bed space before submitting your application" });
            }

            var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();
            if (config == null)
            {
                return Json(new { success = false, message = "Accommodation configuration not found" });
            }

            var currentDate = DateTime.Now;
            var period = await _context.AccommodationPeriods
                .Include(p => p.School)
                .Include(p => p.Programme)
                .Include(p => p.ModeOfStudy)
                .Include(p => p.ProgramLevel)
                .FirstOrDefaultAsync(p => p.PeriodId == periodId &&
                                         p.Status == Status.Active &&
                                         p.ApplicationStartDate <= currentDate &&
                                         p.ApplicationEndDate >= currentDate);

            if (period == null)
            {
                return Json(new { success = false, message = "The accommodation application period has ended or is no longer active" });
            }

            // Validate numberOfDays for PerDay payment type
            if (period.TypeOfPayment == "PerDay")
            {
                if (!numberOfDays.HasValue || numberOfDays.Value <= 0)
                {
                    return Json(new { success = false, message = "Please specify the number of days for your accommodation" });
                }

                // Calculate maximum days allowed
                int maxDays = CalculateMaxDaysAllowed(period);

                if (numberOfDays.Value > maxDays)
                {
                    return Json(new { success = false, message = $"Number of days cannot exceed {maxDays} days (until the end of the accommodation period)" });
                }
            }

            // Check for existing VALID (non-expired) application
            var existingValidApp = await _context.AccommodationApplications
                .Where(a => a.StudentId == student.Id &&
                           a.PeriodId == periodId &&
                           (a.Status == Status.Pending || a.Status == Status.Active || a.Status == Status.Approved))
                .FirstOrDefaultAsync();

            if (existingValidApp != null)
            {
                // Check if it's genuinely still valid or expired
                var reservationExpiry = existingValidApp.ApplicationDate.AddHours(config.ReservationHoursValidity);
                if (reservationExpiry > currentDate)
                {
                    return Json(new { success = false, message = "You have already submitted a valid application for this period. Please wait for processing or make payment." });
                }
            }

            var eligibilityResult = await CheckStudentEligibilityWithReasons(student, period);
            if (!eligibilityResult.IsEligible)
            {
                return Json(new { success = false, message = "You are not eligible to apply for accommodation. " + string.Join(" ", eligibilityResult.Reasons) });
            }

            // Verify the selected bed is available
            var selectedBed = await _context.BedSpaces
                .Include(b => b.Room)
                    .ThenInclude(r => r.Hostel)
                .FirstOrDefaultAsync(b => b.BedId == selectedBedId);

            if (selectedBed == null)
            {
                return Json(new { success = false, message = "Selected bed space not found" });
            }

            if (selectedBed.Status != Status.Available)
            {
                return Json(new { success = false, message = "Selected bed space is no longer available. Please choose another bed" });
            }

            // Reserve the bed temporarily
            selectedBed.Status = Status.Reserved;
            selectedBed.UpdatedAt = DateTime.Now;
            selectedBed.UpdatedBy = User.Identity.Name;

            var reservationExpiryDate = DateTime.Now.AddHours(config.ReservationHoursValidity);

            // Calculate accommodation fee
            decimal accommodationFee = CalculateAccommodationFee(period, numberOfDays);

            // Create application WITH bed selection and numberOfDays
            var application = new AccommodationApplication
            {
                StudentId = student.Id,
                PeriodId = periodId,
                SelectedBedId = selectedBedId,
                ApplicationDate = DateTime.Now,
                Status = Status.Pending,
                Notes = notes ?? string.Empty,
                NumberOfDays = period.TypeOfPayment == "PerDay" ? numberOfDays : null,
                CreatedAt = DateTime.Now,
                CreatedBy = User.Identity.Name
            };

            _context.AccommodationApplications.Add(application);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Your accommodation application has been submitted successfully.",
                applicationId = application.ApplicationId,
                paymentInfo = new
                {
                    accommodationFee = accommodationFee,
                    typeOfPayment = period.TypeOfPayment,
                    numberOfDays = numberOfDays,
                    ratePerDay = period.TypeOfPayment == "PerDay" ? period.TypeOfPaymentAmount : (decimal?)null,
                    reservationValidUntil = reservationExpiryDate.ToString("MMMM dd, yyyy hh:mm tt"),
                    paymentLocation = config.LocationToTakeAccommodationPaymentReceipt,
                    reservationHours = config.ReservationHoursValidity
                }
            });
        }

        // POST: Pay from balance using IAccommodationAllocationService
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayFromBalance(int applicationId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
            {
                return Json(new { success = false, message = "Student not found" });
            }

            var application = await _context.AccommodationApplications
                .Include(a => a.Period)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.StudentId == student.Id);

            if (application == null)
            {
                return Json(new { success = false, message = "Application not found" });
            }

            if (application.Status != Status.Pending)
            {
                return Json(new { success = false, message = "Application is not in pending status" });
            }

            // Use the allocation service to verify balance and allocate
            var result = await _accommodationAllocationService.VerifyBalanceAndAssignBedSpaceStudentHasAppliedForAndCreateInvoice(student.Id);

            return Json(new
            {
                success = result.Status,
                message = result.Message
            });
        }

        // POST: Get bed space and pay later (adds to student's outstanding fees)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetBedspaceAndPayLater(int applicationId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
            {
                return Json(new { success = false, message = "Student not found" });
            }

            var application = await _context.AccommodationApplications
                .Include(a => a.Period)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.StudentId == student.Id);

            if (application == null)
            {
                return Json(new { success = false, message = "Application not found" });
            }

            if (application.Status != Status.Pending)
            {
                return Json(new { success = false, message = "Application is not in pending status" });
            }

            // Get the currently logged-in user as the allocator
            var user = await _userManager.GetUserAsync(User);
            var allocatedBy = user?.Id ?? "SYSTEM_PAY_LATER";

            // Use the allocation service to assign bed space without payment verification
            // This will create an invoice but won't verify payment
            if (!application.SelectedBedId.HasValue)
            {
                return Json(new { success = false, message = "No bed space was selected during application" });
            }

            var result = await _accommodationAllocationService.AssignBedSpaceWithoutPayment(
                student.Id,
                application.SelectedBedId.Value,
                allocatedBy
            );

            return Json(new
            {
                success = result.Status,
                message = result.Message
            });
        }

        // GET: Get hostels by campus with availability
        [HttpGet]
        public async Task<IActionResult> GetHostelsByCampusWithAvailability(int campusId, string gender)
        {
            try
            {
                var student = await GetCurrentStudentAsync();
                var studentYear = student?.StudentCurrentYear ?? 0;

                var hostels = await _context.Hostels
                    .Where(h => h.CampusId == campusId && h.Status == Status.Active)
                    .Select(h => new
                    {
                        id = h.HostelId,
                        name = h.HostelName,
                        gender = h.Gender,
                        availableBeds = h.Rooms
                            .Where(room => room.Status == Status.Available &&
                                          (room.Gender == gender || room.Gender == "Mixed"))
                            .SelectMany(room => room.BedSpaces)
                            .Count(b => b.Status == Status.Available &&
                                       (b.CurrentStudentYear == 0 || b.CurrentStudentYear == studentYear) &&
                                       (b.AcademicPeriodId == 0 || b.AcademicPeriodId == student.CurrentYearPeriod.AcademicPeriodId))
                    })
                    .Where(h => h.availableBeds > 0)
                    .OrderBy(h => h.name)
                    .ToListAsync();

                return Json(hostels);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetHostelsByCampusWithAvailability: {ex.Message}");
                return Json(new List<object>());
            }
        }

        // GET: Calculate fee for given number of days
        [HttpGet]
        public async Task<IActionResult> CalculateFee(int periodId, int numberOfDays)
        {
            try
            {
                var period = await _context.AccommodationPeriods.FindAsync(periodId);
                if (period == null)
                {
                    return Json(new { success = false, message = "Period not found" });
                }

                int maxDays = CalculateMaxDaysAllowed(period);
                if (numberOfDays > maxDays)
                {
                    return Json(new { success = false, message = $"Number of days cannot exceed {maxDays} days" });
                }

                decimal fee = CalculateAccommodationFee(period, numberOfDays);

                return Json(new
                {
                    success = true,
                    fee = fee,
                    ratePerDay = period.TypeOfPaymentAmount,
                    numberOfDays = numberOfDays,
                    maxDays = maxDays
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ADMIN/STAFF OPERATION: Search for student
        [HttpGet]
        [Authorize(Roles = "Admin,HostelManager,Registrar")]
        public async Task<IActionResult> SearchStudentForAllocation(string searchTerm)
        {
            var student = await GetStudentBySearchTermAsync(searchTerm);

            if (student == null)
            {
                string identifier = searchTerm.Contains("@") ? "email address" : "student ID";
                return Json(new
                {
                    success = false,
                    message = $"No student found with this {identifier}"
                });
            }

            return Json(new
            {
                success = true,
                studentId = student.Id,
                studentNumber = student.StudentId_Number,
                fullName = student.FullName,
                email = student.Email,
                gender = student.Gender,
                programme = student.Programme?.Name,
                school = student.Programme?.Department?.School?.Name,
                yearOfStudy = student.StudentCurrentYear,
                status = student.StudentStatus.ToString()
            });
        }

        // GET: Dashboard statistics
        [HttpGet]
        [Authorize(Roles = "Admin,HostelManager")]
        public async Task<IActionResult> Dashboard()
        {
            await PerformAutomaticCleanupAsync();

            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            IQueryable<Hostel> hostelQuery = _context.Hostels.Include(h => h.Rooms).ThenInclude(r => r.BedSpaces);

            if (!isAdmin)
            {
                hostelQuery = hostelQuery.Where(h => h.WardenId == currentUser.Id);
            }

            var hostels = await hostelQuery.ToListAsync();

            var totalBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces).Count();
            var occupiedBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces)
                .Count(b => b.Status == Status.Occupied);
            var availableBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces)
                .Count(b => b.Status == Status.Available);

            var recentAllocations = await _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                .OrderByDescending(a => a.AllocationDate)
                .Take(10)
                .ToListAsync();

            var dashboard = new HostelDashboardViewModel
            {
                TotalHostels = hostels.Count,
                TotalBeds = totalBeds,
                OccupiedBeds = occupiedBeds,
                AvailableBeds = availableBeds,
                OccupancyRate = totalBeds > 0 ? (double)occupiedBeds / totalBeds * 100 : 0,
                RecentAllocations = recentAllocations
            };

            return View("Dashboard", dashboard);
        }
    }

    // ============================================
    // HELPER CLASSES
    // ============================================

    public class EligibilityResult
    {
        public bool IsEligible { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
    }

    // ============================================
    // VIEW MODELS
    // ============================================

    public class BedAllocationFilterViewModel
    {
        [Required(ErrorMessage = "Please select an accommodation period")]
        [Display(Name = "Accommodation Period")]
        public int? SelectedAccommodationPeriodId { get; set; }

        [Display(Name = "Select Hostel")]
        public int? SelectedHostelId { get; set; }

        [Display(Name = "Select Room")]
        public int? SelectedRoomId { get; set; }

        [Display(Name = "Select Bed")]
        public int? SelectedBedId { get; set; }

        [Display(Name = "Student ID or Email")]
        public string StudentSearchTerm { get; set; }

        public List<SelectListItem> AccommodationPeriods { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Hostels { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Rooms { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Beds { get; set; } = new List<SelectListItem>();

        public StudentInfoViewModel StudentInfo { get; set; }
    }

    public class StudentInfoViewModel
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public string Programme { get; set; }
        public string School { get; set; }
        public string Department { get; set; }
        public int? YearOfStudy { get; set; }
    }

    public class BedAllocationViewModel
    {
        [Required]
        public int StudentId { get; set; }

        [Required]
        public int BedId { get; set; }

        [Required]
        public int AccommodationPeriodId { get; set; }

        public string Notes { get; set; }
    }

    public class HostelDashboardViewModel
    {
        public int TotalHostels { get; set; }
        public int TotalBeds { get; set; }
        public int OccupiedBeds { get; set; }
        public int AvailableBeds { get; set; }
        public int TotalStudents { get; set; }
        public double OccupancyRate { get; set; }
        public List<Allocation> RecentAllocations { get; set; } = new List<Allocation>();
    }
}