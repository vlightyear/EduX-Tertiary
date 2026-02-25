using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.StudentAccommodation;
using System.Linq;

namespace SIS.Controllers.AccomodationControllers
{
    [Authorize(Roles = "Admin,HostelManager")]
    public class AccommodationApplicationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccommodationApplicationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: AccommodationApplications
        public async Task<IActionResult> Index(string searchTerm = "", string searchBy = "AllFields")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            // Start with base query - removed AcademicYear include from Period
            IQueryable<AccommodationApplication> applicationsQuery = _context.AccommodationApplications
                .Include(a => a.Student)
                    .ThenInclude(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                .Include(a => a.Student)
                    .ThenInclude(s => s.AcademicYear)
                .Include(a => a.Period) // Period no longer has AcademicYear
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                                .ThenInclude(h => h.Campus)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.CheckInOut);

            // CRITICAL: Filter by hostel if user is HostelManager (not Admin)
            if (!isAdmin)
            {
                // Get the hostel managed by this user
                var managedHostel = await _context.Hostels
                    .FirstOrDefaultAsync(h => h.WardenId == currentUser.Id);

                if (managedHostel == null)
                {
                    // If no hostel is assigned, return empty result
                    ViewBag.NoHostelAssigned = true;
                    ViewBag.Message = "You are not assigned to manage any hostel.";

                    var emptyViewModel = new AccommodationApplicationsViewModel
                    {
                        Applications = new List<ApplicationListItemViewModel>(),
                        SearchTerm = searchTerm,
                        SearchBy = searchBy,
                        TotalApplications = 0,
                        AllocatedCount = 0,
                        PendingCount = 0,
                        CheckedInCount = 0
                    };

                    return View("~/Views/Accommodation/AccommodationApplicants/AccommodationApplications_Index.cshtml", emptyViewModel);
                }

                // Filter applications to ONLY show those allocated to rooms in the managed hostel
                applicationsQuery = applicationsQuery
                    .Where(a => a.Allocation != null &&
                               a.Allocation.Bed != null &&
                               a.Allocation.Bed.Room != null &&
                               a.Allocation.Bed.Room.HostelId == managedHostel.HostelId);

                // Store hostel info for the view
                ViewBag.ManagedHostel = managedHostel;
            }

            // Order by application date (most recent first)
            applicationsQuery = applicationsQuery.OrderByDescending(a => a.ApplicationDate);

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                switch (searchBy)
                {
                    case "StudentNumber":
                        applicationsQuery = applicationsQuery
                            .Where(a => a.Student.StudentId_Number.Contains(searchTerm));
                        break;

                    case "StudentName":
                        applicationsQuery = applicationsQuery
                            .Where(a => a.Student.FullName.Contains(searchTerm));
                        break;

                    case "Programme":
                        applicationsQuery = applicationsQuery
                            .Where(a => a.Student.Programme.Name.Contains(searchTerm));
                        break;

                    case "School":
                        applicationsQuery = applicationsQuery
                            .Where(a => a.Student.Programme.Department.School.Name.Contains(searchTerm));
                        break;

                    case "PeriodType":
                        applicationsQuery = applicationsQuery
                            .Where(a => a.Period.Type.Contains(searchTerm));
                        break;

                    case "PaymentType":
                        applicationsQuery = applicationsQuery
                            .Where(a => a.Period.TypeOfPayment.Contains(searchTerm));
                        break;

                    case "RoomNumber":
                        applicationsQuery = applicationsQuery
                            .Where(a => a.Allocation != null &&
                                       a.Allocation.Bed.Room.RoomNumber.Contains(searchTerm));
                        break;

                    case "AllFields":
                    default:
                        applicationsQuery = applicationsQuery
                            .Where(a => a.Student.StudentId_Number.Contains(searchTerm) ||
                                       a.Student.FullName.Contains(searchTerm) ||
                                       a.Student.Programme.Name.Contains(searchTerm) ||
                                       a.Student.Programme.Department.School.Name.Contains(searchTerm) ||
                                       a.Period.Type.Contains(searchTerm) ||
                                       a.Period.TypeOfPayment.Contains(searchTerm) ||
                                       (a.Allocation != null && a.Allocation.Bed.Room.RoomNumber.Contains(searchTerm)));
                        break;
                }
            }

            var applications = await applicationsQuery.ToListAsync();

            // Prepare view model
            var viewModel = new AccommodationApplicationsViewModel
            {
                Applications = applications.Select(a => new ApplicationListItemViewModel
                {
                    Application = a,
                    CheckInOutStatus = DetermineCheckInOutStatus(a.Allocation?.CheckInOut),
                    HasBedAllocation = a.Allocation != null && a.Allocation.BedId > 0,
                    AllocationStatus = a.Allocation?.Status.ToString() ?? "Not Allocated",
                    CalculatedFee = CalculateApplicationFee(a)
                }).ToList(),
                SearchTerm = searchTerm,
                SearchBy = searchBy,
                TotalApplications = applications.Count,
                AllocatedCount = applications.Count(a => a.Allocation != null),
                PendingCount = applications.Count(a => a.Status == Status.Pending),
                CheckedInCount = applications.Count(a => a.Allocation?.CheckInOut?.CheckInDate != null &&
                                                         a.Allocation?.CheckInOut?.CheckOutDate == null),
                IsAdmin = isAdmin
            };

            return View("~/Views/Accommodation/AccommodationApplicants/AccommodationApplications_Index.cshtml", viewModel);
        }

        // GET: Application Details
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var application = await _context.AccommodationApplications
                .Include(a => a.Student)
                    .ThenInclude(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                .Include(a => a.Student)
                    .ThenInclude(s => s.AcademicYear)
                .Include(a => a.Student)
                    .ThenInclude(s => s.ModeOfStudy)
                .Include(a => a.Student)
                    .ThenInclude(s => s.ProgrammeLevel)
                .Include(a => a.Period) // Period no longer has AcademicYear
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                                .ThenInclude(h => h.Campus)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.AllocatedBy)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.CheckInOut)
                        .ThenInclude(c => c.CheckInStaff)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.CheckInOut)
                        .ThenInclude(c => c.CheckOutStaff)
                .FirstOrDefaultAsync(a => a.ApplicationId == id);

            if (application == null)
            {
                return NotFound();
            }

            // Check if current user has access to this application
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            if (!isAdmin)
            {
                // Verify this application is in a hostel managed by this user
                if (application.Allocation?.Bed?.Room?.Hostel?.WardenId != currentUser.Id)
                {
                    return Json(new
                    {
                        error = true,
                        message = "You do not have permission to view this application."
                    });
                }

                // Additional check: ensure allocation exists
                if (application.Allocation == null)
                {
                    return Json(new
                    {
                        error = true,
                        message = "This application is not allocated to your hostel."
                    });
                }
            }

            var detailsViewModel = new ApplicationDetailsViewModel
            {
                Application = application,
                CheckInOutStatus = DetermineCheckInOutStatus(application.Allocation?.CheckInOut),
                HasBedAllocation = application.Allocation != null && application.Allocation.BedId > 0,
                CalculatedFee = CalculateApplicationFee(application)
            };

            return PartialView("~/Views/Accommodation/AccommodationApplicants/_ApplicationDetailsPartial.cshtml", detailsViewModel);
        }

        /// <summary>
        /// Calculate accommodation fee based on period payment type and number of days
        /// </summary>
        private decimal CalculateApplicationFee(AccommodationApplication application)
        {
            if (application?.Period == null)
                return 0;

            var period = application.Period;

            switch (period.TypeOfPayment)
            {
                case "PerDay":
                    return period.TypeOfPaymentAmount * (application.NumberOfDays ?? 1);
                case "Semester":
                case "Year":
                case "Fixed":
                default:
                    return period.TypeOfPaymentAmount;
            }
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
        /// Export applications to Excel or CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Export(string format = "excel")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            IQueryable<AccommodationApplication> applicationsQuery = _context.AccommodationApplications
                .Include(a => a.Student)
                    .ThenInclude(s => s.Programme)
                .Include(a => a.Period)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel);

            if (!isAdmin)
            {
                // Get the hostel managed by this user
                var managedHostel = await _context.Hostels
                    .FirstOrDefaultAsync(h => h.WardenId == currentUser.Id);

                if (managedHostel == null)
                {
                    TempData["Error"] = "You are not assigned to manage any hostel.";
                    return RedirectToAction(nameof(Index));
                }

                // Filter to only applications in the managed hostel
                applicationsQuery = applicationsQuery
                    .Where(a => a.Allocation != null &&
                               a.Allocation.Bed != null &&
                               a.Allocation.Bed.Room != null &&
                               a.Allocation.Bed.Room.HostelId == managedHostel.HostelId);
            }

            var applications = await applicationsQuery.ToListAsync();

            // Implementation for export would go here
            // For now, return a simple message
            TempData["Info"] = $"Export functionality coming soon. {applications.Count} applications would be exported.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Get statistics for the managed hostel
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStatistics()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            IQueryable<AccommodationApplication> applicationsQuery = _context.AccommodationApplications
                .Include(a => a.Period)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.Bed)
                        .ThenInclude(b => b.Room)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.CheckInOut);

            if (!isAdmin)
            {
                var managedHostel = await _context.Hostels
                    .FirstOrDefaultAsync(h => h.WardenId == currentUser.Id);

                if (managedHostel == null)
                {
                    return Json(new { error = true, message = "No hostel assigned" });
                }

                applicationsQuery = applicationsQuery
                    .Where(a => a.Allocation != null &&
                               a.Allocation.Bed.Room.HostelId == managedHostel.HostelId);
            }

            var applications = await applicationsQuery.ToListAsync();

            // Calculate total fees collected based on payment type
            decimal totalFeesExpected = applications.Sum(a => CalculateApplicationFee(a));

            var stats = new
            {
                totalApplications = applications.Count,
                allocatedCount = applications.Count(a => a.Allocation != null),
                pendingCount = applications.Count(a => a.Status == Status.Pending),
                checkedInCount = applications.Count(a => a.Allocation?.CheckInOut?.CheckInDate != null &&
                                                         a.Allocation?.CheckInOut?.CheckOutDate == null),
                checkedOutCount = applications.Count(a => a.Allocation?.CheckInOut?.CheckOutDate != null),
                maleStudents = applications.Count(a => a.Student?.Gender?.ToLower() == "male"),
                femaleStudents = applications.Count(a => a.Student?.Gender?.ToLower() == "female"),
                totalFeesExpected = totalFeesExpected,
                perDayApplications = applications.Count(a => a.Period?.TypeOfPayment == "PerDay"),
                semesterApplications = applications.Count(a => a.Period?.TypeOfPayment == "Semester"),
                yearApplications = applications.Count(a => a.Period?.TypeOfPayment == "Year"),
                fixedApplications = applications.Count(a => a.Period?.TypeOfPayment == "Fixed")
            };

            return Json(stats);
        }

        /// <summary>
        /// Get fee breakdown for a specific application
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFeeBreakdown(int applicationId)
        {
            var application = await _context.AccommodationApplications
                .Include(a => a.Period)
                .Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (application == null)
            {
                return Json(new { success = false, message = "Application not found" });
            }

            var period = application.Period;
            decimal calculatedFee = CalculateApplicationFee(application);

            return Json(new
            {
                success = true,
                studentName = application.Student?.FullName,
                periodType = period?.Type,
                paymentType = period?.TypeOfPayment,
                rateAmount = period?.TypeOfPaymentAmount ?? 0,
                numberOfDays = application.NumberOfDays,
                totalFee = calculatedFee,
                feeBreakdown = period?.TypeOfPayment == "PerDay"
                    ? $"K{period.TypeOfPaymentAmount:N2}/day × {application.NumberOfDays ?? 1} days = K{calculatedFee:N2}"
                    : $"K{calculatedFee:N2} ({period?.TypeOfPayment})",
                periodStartDate = period?.StartDate.ToString("MMM dd, yyyy"),
                periodEndDate = period?.EndDate?.ToString("MMM dd, yyyy") ?? "Until Graduation"
            });
        }
    }

    // ============================================
    // VIEW MODELS
    // ============================================

    public class AccommodationApplicationsViewModel
    {
        public List<ApplicationListItemViewModel> Applications { get; set; } = new List<ApplicationListItemViewModel>();
        public string SearchTerm { get; set; }
        public string SearchBy { get; set; }
        public int TotalApplications { get; set; }
        public int AllocatedCount { get; set; }
        public int PendingCount { get; set; }
        public int CheckedInCount { get; set; }
        public bool IsAdmin { get; set; }
    }

    public class ApplicationListItemViewModel
    {
        public AccommodationApplication Application { get; set; }
        public string CheckInOutStatus { get; set; }
        public bool HasBedAllocation { get; set; }
        public string AllocationStatus { get; set; }
        public decimal CalculatedFee { get; set; }
    }

    public class ApplicationDetailsViewModel
    {
        public AccommodationApplication Application { get; set; }
        public string CheckInOutStatus { get; set; }
        public bool HasBedAllocation { get; set; }
        public decimal CalculatedFee { get; set; }
    }
}