using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Interfaces;
using SIS.Models.StudentAccommodation;
using System.ComponentModel.DataAnnotations;

namespace SIS.Controllers.AccomodationControllers
{
    [Authorize(Roles = "Admin,HostelManager,AccommodationStaff")]
    public class CheckInOutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccommodationAllocationService _allocationService;

        public CheckInOutController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAccommodationAllocationService allocationService)
        {
            _context = context;
            _userManager = userManager;
            _allocationService = allocationService;
        }

        // GET: CheckInOut
        public async Task<IActionResult> Index(string searchTerm = "", string searchBy = "AllFields", string filterStatus = "All")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            // Get accommodation configuration to check DeAllocateBedSpaceUponCheckOut setting
            var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();

            // Start with base query - get all active allocations (removed AcademicYear include from Period)
            IQueryable<Allocation> allocationsQuery = _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                        .ThenInclude(s => s.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                .Include(a => a.Application)
                    .ThenInclude(app => app.Period) // Period no longer has AcademicYear
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .Include(a => a.CheckInOut)
                    .ThenInclude(c => c.CheckInStaff)
                .Include(a => a.CheckInOut)
                    .ThenInclude(c => c.CheckOutStaff)
                .Include(a => a.AllocatedBy)
                .Where(a => a.Status == Status.Active);

            // Filter by hostel if user is HostelManager (not Admin)
            if (!isAdmin)
            {
                var managedHostelIds = await _context.Hostels
                    .Where(h => h.WardenId == currentUser.Id)
                    .Select(h => h.HostelId)
                    .ToListAsync();

                allocationsQuery = allocationsQuery
                    .Where(a => a.Bed != null &&
                               a.Bed.Room != null &&
                               managedHostelIds.Contains(a.Bed.Room.HostelId));
            }

            // Apply status filter
            if (filterStatus != "All")
            {
                switch (filterStatus)
                {
                    case "NotCheckedIn":
                        allocationsQuery = allocationsQuery.Where(a => a.CheckInOut == null || a.CheckInOut.CheckInDate == null);
                        break;
                    case "CheckedIn":
                        allocationsQuery = allocationsQuery.Where(a => a.CheckInOut != null &&
                                                                      a.CheckInOut.CheckInDate != null &&
                                                                      a.CheckInOut.CheckOutDate == null);
                        break;
                    case "CheckedOut":
                        allocationsQuery = allocationsQuery.Where(a => a.CheckInOut != null &&
                                                                      a.CheckInOut.CheckOutDate != null);
                        break;
                }
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                switch (searchBy)
                {
                    case "StudentNumber":
                        allocationsQuery = allocationsQuery
                            .Where(a => a.Application.Student.StudentId_Number.Contains(searchTerm));
                        break;

                    case "StudentName":
                        allocationsQuery = allocationsQuery
                            .Where(a => a.Application.Student.FullName.Contains(searchTerm));
                        break;

                    case "RoomNumber":
                        allocationsQuery = allocationsQuery
                            .Where(a => a.Bed.Room.RoomNumber.Contains(searchTerm));
                        break;

                    case "HostelName":
                        allocationsQuery = allocationsQuery
                            .Where(a => a.Bed.Room.Hostel.HostelName.Contains(searchTerm));
                        break;

                    case "PeriodType":
                        allocationsQuery = allocationsQuery
                            .Where(a => a.Application.Period.Type.Contains(searchTerm));
                        break;

                    case "PaymentType":
                        allocationsQuery = allocationsQuery
                            .Where(a => a.Application.Period.TypeOfPayment.Contains(searchTerm));
                        break;

                    case "AllFields":
                    default:
                        allocationsQuery = allocationsQuery
                            .Where(a => a.Application.Student.StudentId_Number.Contains(searchTerm) ||
                                       a.Application.Student.FullName.Contains(searchTerm) ||
                                       a.Bed.Room.RoomNumber.Contains(searchTerm) ||
                                       a.Bed.Room.Hostel.HostelName.Contains(searchTerm) ||
                                       a.Application.Period.Type.Contains(searchTerm) ||
                                       a.Application.Period.TypeOfPayment.Contains(searchTerm));
                        break;
                }
            }

            var allocations = await allocationsQuery
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync();

            // Calculate statistics
            var totalAllocations = allocations.Count;
            var notCheckedIn = allocations.Count(a => a.CheckInOut == null || a.CheckInOut.CheckInDate == null);
            var checkedIn = allocations.Count(a => a.CheckInOut != null &&
                                                   a.CheckInOut.CheckInDate != null &&
                                                   a.CheckInOut.CheckOutDate == null);
            var checkedOut = allocations.Count(a => a.CheckInOut != null && a.CheckInOut.CheckOutDate != null);

            // Build view model
            var viewModel = new CheckInOutViewModel
            {
                Allocations = allocations.Select(a => new CheckInOutListItemViewModel
                {
                    Allocation = a,
                    CheckInOutStatus = DetermineCheckInOutStatus(a.CheckInOut),
                    CalculatedFee = CalculateAllocationFee(a)
                }).ToList(),
                SearchTerm = searchTerm,
                SearchBy = searchBy,
                FilterStatus = filterStatus,
                TotalAllocations = totalAllocations,
                NotCheckedInCount = notCheckedIn,
                CheckedInCount = checkedIn,
                CheckedOutCount = checkedOut,
                DeAllocateBedSpaceUponCheckOut = config?.DeAllocateBedSpaceUponCheckOut ?? false
            };

            return View("~/Views/Accommodation/CheckInOut/CheckInOut_Index.cshtml", viewModel);
        }

        // GET: Get Allocation Details
        [HttpGet]
        public async Task<IActionResult> GetAllocationDetails(int allocationId)
        {
            var allocation = await _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                        .ThenInclude(s => s.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                .Include(a => a.Application)
                    .ThenInclude(app => app.Period) // Period no longer has AcademicYear
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .Include(a => a.CheckInOut)
                    .ThenInclude(c => c.CheckInStaff)
                .Include(a => a.CheckInOut)
                    .ThenInclude(c => c.CheckOutStaff)
                .Include(a => a.AllocatedBy)
                .FirstOrDefaultAsync(a => a.AllocationId == allocationId);

            if (allocation == null)
            {
                return NotFound();
            }

            // Check if current user has access
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            if (!isAdmin)
            {
                var managedHostelIds = await _context.Hostels
                    .Where(h => h.WardenId == currentUser.Id)
                    .Select(h => h.HostelId)
                    .ToListAsync();

                if (!managedHostelIds.Contains(allocation.Bed.Room.HostelId))
                {
                    return Forbid();
                }
            }

            var detailsViewModel = new CheckInOutDetailsViewModel
            {
                Allocation = allocation,
                CheckInOutStatus = DetermineCheckInOutStatus(allocation.CheckInOut),
                CanCheckIn = allocation.CheckInOut == null || allocation.CheckInOut.CheckInDate == null,
                CanCheckOut = allocation.CheckInOut != null &&
                             allocation.CheckInOut.CheckInDate != null &&
                             allocation.CheckInOut.CheckOutDate == null,
                CalculatedFee = CalculateAllocationFee(allocation)
            };

            return PartialView("~/Views/Accommodation/CheckInOut/_CheckInOutDetailsPartial.cshtml", detailsViewModel);
        }

        // GET: Get fee information for an allocation
        [HttpGet]
        public async Task<IActionResult> GetAllocationFeeInfo(int allocationId)
        {
            var allocation = await _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(app => app.Period)
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                .FirstOrDefaultAsync(a => a.AllocationId == allocationId);

            if (allocation == null)
            {
                return Json(new { success = false, message = "Allocation not found" });
            }

            var period = allocation.Application?.Period;
            decimal calculatedFee = CalculateAllocationFee(allocation);

            return Json(new
            {
                success = true,
                studentName = allocation.Application?.Student?.FullName,
                periodType = period?.Type,
                paymentType = period?.TypeOfPayment,
                rateAmount = period?.TypeOfPaymentAmount ?? 0,
                numberOfDays = allocation.Application?.NumberOfDays,
                totalFee = calculatedFee,
                feeBreakdown = period?.TypeOfPayment == "PerDay"
                    ? $"K{period.TypeOfPaymentAmount:N2}/day × {allocation.Application?.NumberOfDays ?? 1} days = K{calculatedFee:N2}"
                    : $"K{calculatedFee:N2} ({period?.TypeOfPayment})",
                allocationStartDate = allocation.StartDate.ToString("MMM dd, yyyy"),
                allocationEndDate = allocation.EndDate?.ToString("MMM dd, yyyy") ?? "Until Graduation"
            });
        }

        // POST: Check In Student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckInStudent([FromBody] CheckInRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid request data" });
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var currentDate = DateTime.Now;

                // Get allocation
                var allocation = await _context.Allocations
                    .Include(a => a.CheckInOut)
                    .Include(a => a.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                    .Include(a => a.Application)
                        .ThenInclude(app => app.Student)
                    .FirstOrDefaultAsync(a => a.AllocationId == request.AllocationId);

                if (allocation == null)
                {
                    return Json(new { success = false, message = "Allocation not found" });
                }

                // Verify permissions
                var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
                if (!isAdmin)
                {
                    var managedHostelIds = await _context.Hostels
                        .Where(h => h.WardenId == currentUser.Id)
                        .Select(h => h.HostelId)
                        .ToListAsync();

                    if (!managedHostelIds.Contains(allocation.Bed.Room.HostelId))
                    {
                        return Json(new { success = false, message = "You don't have permission to check in students in this hostel" });
                    }
                }

                // Check if already checked in
                if (allocation.CheckInOut != null && allocation.CheckInOut.CheckInDate.HasValue)
                {
                    return Json(new { success = false, message = "Student is already checked in" });
                }

                // Create or update CheckInOut record
                if (allocation.CheckInOut == null)
                {
                    allocation.CheckInOut = new CheckInOut
                    {
                        AllocationId = allocation.AllocationId,
                        CheckInDate = currentDate,
                        CheckInCondition = request.Condition,
                        CheckInStaffId = currentUser.Id,
                        DamageCharges = 0
                    };
                    _context.CheckInOuts.Add(allocation.CheckInOut);
                }
                else
                {
                    allocation.CheckInOut.CheckInDate = currentDate;
                    allocation.CheckInOut.CheckInCondition = request.Condition;
                    allocation.CheckInOut.CheckInStaffId = currentUser.Id;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Student checked in successfully",
                    checkInDate = currentDate.ToString("MMMM dd, yyyy hh:mm tt"),
                    staffName = currentUser.FullName
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error checking in student: {ex.Message}" });
            }
        }

        // POST: Check Out Student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOutStudent([FromBody] CheckOutRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid request data" });
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var currentDate = DateTime.Now;

                // Get accommodation configuration to check DeAllocateBedSpaceUponCheckOut
                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();
                if (config == null)
                {
                    return Json(new { success = false, message = "Accommodation configuration not found. Please contact administration." });
                }

                // Get allocation
                var allocation = await _context.Allocations
                    .Include(a => a.CheckInOut)
                    .Include(a => a.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                    .Include(a => a.Application)
                        .ThenInclude(app => app.Student)
                    .FirstOrDefaultAsync(a => a.AllocationId == request.AllocationId);

                if (allocation == null)
                {
                    return Json(new { success = false, message = "Allocation not found" });
                }

                // Verify permissions
                var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
                if (!isAdmin)
                {
                    var managedHostelIds = await _context.Hostels
                        .Where(h => h.WardenId == currentUser.Id)
                        .Select(h => h.HostelId)
                        .ToListAsync();

                    if (!managedHostelIds.Contains(allocation.Bed.Room.HostelId))
                    {
                        return Json(new { success = false, message = "You don't have permission to check out students in this hostel" });
                    }
                }

                // Check if student is checked in
                if (allocation.CheckInOut == null || !allocation.CheckInOut.CheckInDate.HasValue)
                {
                    return Json(new { success = false, message = "Student must be checked in before checking out" });
                }

                // Check if already checked out
                if (allocation.CheckInOut.CheckOutDate.HasValue)
                {
                    return Json(new { success = false, message = "Student is already checked out" });
                }

                // Update CheckInOut record first (always done regardless of configuration)
                allocation.CheckInOut.CheckOutDate = currentDate;
                allocation.CheckInOut.CheckOutCondition = request.Condition;
                allocation.CheckInOut.CheckOutStaffId = currentUser.Id;
                allocation.CheckInOut.DamageCharges = request.DamageCharges;

                // Check if we should deallocate bed space upon checkout
                if (config.DeAllocateBedSpaceUponCheckOut)
                {
                    // OPTION 1: Full deallocation using service
                    // Use the allocation service to properly remove student from accommodation
                    var studentId = allocation.Application.StudentId;
                    string checkoutReason = $"Check-out completed by {currentUser.FullName} on {currentDate:yyyy-MM-dd HH:mm}. " +
                                          $"Room condition: {request.Condition}.";
                    if (request.DamageCharges > 0)
                    {
                        checkoutReason += $" Damage charges: K{request.DamageCharges:N2}.";
                    }

                    // Save the check-out record first
                    await _context.SaveChangesAsync();

                    // Now remove from accommodation using the service
                    var removalResult = await _allocationService.RemoveStudentFromAccommodation(
                        studentId,
                        currentUser.Id,
                        checkoutReason
                    );

                    if (!removalResult.Status)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Check-out recorded but error deallocating bed space: {removalResult.Message}"
                        });
                    }

                    return Json(new
                    {
                        success = true,
                        message = "Student checked out successfully. Bed space has been deallocated and is now available for new allocations.",
                        checkOutDate = currentDate.ToString("MMMM dd, yyyy hh:mm tt"),
                        staffName = currentUser.FullName,
                        damageCharges = request.DamageCharges,
                        bedDeallocated = true
                    });
                }
                else
                {
                    // OPTION 2: Keep allocation active - just record check-out
                    // Student keeps their bed allocation and can check back in
                    // Only the check-out record is updated (already done above)

                    // Optionally update application notes to record the check-out
                    if (allocation.Application != null)
                    {
                        allocation.Application.Notes += $" [TEMPORARY CHECK-OUT: {currentDate:yyyy-MM-dd HH:mm} by {currentUser.FullName}. " +
                                                       $"Condition: {request.Condition}.";
                        if (request.DamageCharges > 0)
                        {
                            allocation.Application.Notes += $" Damage charges: K{request.DamageCharges:N2}.";
                        }
                        allocation.Application.Notes += " Allocation remains active - student can check back in.]";
                        allocation.Application.UpdatedAt = currentDate;
                        allocation.Application.UpdatedBy = currentUser.UserName;
                    }

                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Student checked out successfully. The allocation remains active - student can check back in at any time.",
                        checkOutDate = currentDate.ToString("MMMM dd, yyyy hh:mm tt"),
                        staffName = currentUser.FullName,
                        damageCharges = request.DamageCharges,
                        bedDeallocated = false
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error checking out student: {ex.Message}" });
            }
        }

        /// <summary>
        /// Calculate fee based on period payment type and number of days
        /// </summary>
        private decimal CalculateAllocationFee(Allocation allocation)
        {
            if (allocation?.Application?.Period == null)
                return 0;

            var period = allocation.Application.Period;

            switch (period.TypeOfPayment)
            {
                case "PerDay":
                    return period.TypeOfPaymentAmount * (allocation.Application.NumberOfDays ?? 1);
                case "Semester":
                case "Year":
                case "Fixed":
                default:
                    return period.TypeOfPaymentAmount;
            }
        }

        // Helper method to determine check-in/out status
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
    }

    // ============================================
    // VIEW MODELS
    // ============================================

    public class CheckInOutViewModel
    {
        public List<CheckInOutListItemViewModel> Allocations { get; set; } = new List<CheckInOutListItemViewModel>();
        public string SearchTerm { get; set; }
        public string SearchBy { get; set; }
        public string FilterStatus { get; set; }
        public int TotalAllocations { get; set; }
        public int NotCheckedInCount { get; set; }
        public int CheckedInCount { get; set; }
        public int CheckedOutCount { get; set; }
        public bool DeAllocateBedSpaceUponCheckOut { get; set; }
    }

    public class CheckInOutListItemViewModel
    {
        public Allocation Allocation { get; set; }
        public string CheckInOutStatus { get; set; }
        public decimal CalculatedFee { get; set; }
    }

    public class CheckInOutDetailsViewModel
    {
        public Allocation Allocation { get; set; }
        public string CheckInOutStatus { get; set; }
        public bool CanCheckIn { get; set; }
        public bool CanCheckOut { get; set; }
        public decimal CalculatedFee { get; set; }
    }

    public class CheckInRequest
    {
        [Required]
        public int AllocationId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Condition { get; set; }
    }

    public class CheckOutRequest
    {
        [Required]
        public int AllocationId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Condition { get; set; }

        [Range(0, 1000000)]
        public decimal DamageCharges { get; set; } = 0;
    }
}