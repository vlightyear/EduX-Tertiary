using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.StudentAccommodation;
using SIS.Models.Payments;
using SIS.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace SIS.Controllers.AccomodationControllers
{
    [Authorize]
    public class AllocationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AllocationController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AllocationController(
            ApplicationDbContext context,
            ILogger<AllocationController> logger,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            // Check and revoke expired bed allocations before loading page
            await CheckAndRevokeExpiredAllocations();

            var campuses = await _context.Campuses
                .Where(c => c.IsActive)
                .OrderBy(c => c.CampusName)
                .ToListAsync();

            return View("~/Views/Hostel/BedSpaceAllocation.cshtml", campuses);
        }

        /// <summary>
        /// Check and automatically revoke expired bed allocations
        /// </summary>
        private async Task CheckAndRevokeExpiredAllocations()
        {
            try
            {
                var today = DateTime.Now.Date;

                // FIXED: Find expired allocations through the Allocation model
                var expiredAllocations = await _context.Allocations
                    .Include(a => a.Application)
                        .ThenInclude(app => app.Student)
                    .Include(a => a.Bed)
                        .ThenInclude(b => b.Room)
                    .Where(a => a.Status == Status.Active &&
                                a.EndDate.HasValue &&
                                a.EndDate.Value.Date < today)
                    .ToListAsync();

                if (expiredAllocations.Any())
                {
                    foreach (var allocation in expiredAllocations)
                    {
                        var student = allocation.Application.Student;
                        var bed = allocation.Bed;
                        var studentNumber = student.StudentId_Number;
                        var endDate = allocation.EndDate?.ToString("yyyy-MM-dd");

                        // Update allocation status
                        allocation.Status = Status.Completed;
                        allocation.UpdatedAt = DateTime.Now;
                        allocation.UpdatedBy = "System-AutoExpiry";

                        // Clear student's bed reference (denormalized data)
                        student.BedId = null;
                        student.BedAllocationEndDate = null;
                        student.UpdatedAt = DateTime.Now;
                        student.UpdatedBy = "System-AutoExpiry";

                        // Update bed space status back to Available
                        bed.Status = Status.Available;
                        bed.UpdatedAt = DateTime.Now;
                        bed.UpdatedBy = "System-AutoExpiry";

                        // Remove AccommodatedStudent role
                        var user = await _userManager.FindByEmailAsync(student.Email);
                        if (user != null)
                        {
                            var hasRole = await _userManager.IsInRoleAsync(user, "AccommodatedStudent");
                            if (hasRole)
                            {
                                await _userManager.RemoveFromRoleAsync(user, "AccommodatedStudent");
                                _logger.LogInformation($"Revoked 'AccommodatedStudent' role from {studentNumber}");
                            }
                        }

                        _logger.LogInformation($"Revoked expired bed allocation for student {studentNumber}. " +
                                             $"BedId: {bed.BedId}, End Date: {endDate}");
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Successfully revoked {expiredAllocations.Count} expired bed allocations");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and revoking expired bed allocations");
                // Don't throw - this is a background check, shouldn't break the page load
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchStudents(int campusId, string searchTerm = "", bool includeReservedOnly = false)
        {
            try
            {
                // Check and revoke expired allocations before searching
                await CheckAndRevokeExpiredAllocations();

                // Get active accommodation periods
                var activePeriods = await _context.AccommodationPeriods
                    .Where(p => p.Status == Status.Active &&
                                p.ApplicationStartDate <= DateTime.Now &&
                                p.ApplicationEndDate >= DateTime.Now)
                    .Select(p => p.PeriodId)
                    .ToListAsync();

                if (!activePeriods.Any())
                {
                    return Json(new { success = false, message = "No active accommodation periods found." });
                }

                // FIXED: Get students with pending applications (no bed assigned yet in application)
                var query = _context.AccommodationApplications
                    .Include(a => a.Student)
                        .ThenInclude(s => s.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                    .Include(a => a.Student)
                        .ThenInclude(s => s.ProgrammeLevel)
                    .Include(a => a.Period)
                    .Include(a => a.Allocation)
                    .Where(a => activePeriods.Contains(a.PeriodId) &&
                                a.Status == Status.Pending &&
                                a.Allocation == null); // Not yet allocated

                // Apply search term if provided
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(a =>
                        a.Student.StudentId_Number.Contains(searchTerm) ||
                        a.Student.FullName.Contains(searchTerm) ||
                        a.Student.Email.Contains(searchTerm));
                }

                var students = await query
                    .Select(a => new
                    {
                        a.ApplicationId,
                        a.StudentId,
                        StudentNumber = a.Student.StudentId_Number,
                        StudentName = a.Student.FullName,
                        Email = a.Student.Email,
                        Phone = a.Student.Phone,
                        Gender = a.Student.Gender,
                        ProgrammeName = a.Student.Programme.Name,
                        SchoolName = a.Student.Programme.Department.School.Name,
                        ProgramLevel = a.Student.ProgrammeLevel.Name,
                        ApplicationDate = a.ApplicationDate,
                        PeriodType = a.Period.Type,
                        a.Period.StartDate,
                        a.Period.EndDate,
                        a.PeriodId,
                        a.Notes,
                        CurrentBedId = a.Student.BedId // Check if student already has a bed
                    })
                    .OrderBy(a => a.ApplicationDate)
                    .ToListAsync();

                // Apply search term filtering in memory if needed
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    students = students.Where(s =>
                        s.StudentNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        s.StudentName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        s.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                return Json(new { success = true, data = students });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching students for allocation");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableBeds(int campusId, string gender)
        {
            try
            {
                var beds = await _context.BedSpaces
                    .Include(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                    .Where(b => b.Status == Status.Available &&
                                b.Room.Status == Status.Available &&
                                b.Room.Hostel.Status == Status.Active &&
                                b.Room.Hostel.CampusId == campusId &&
                                (b.Room.Gender == gender || b.Room.Gender == "Mixed"))
                    .Select(b => new
                    {
                        b.BedId,
                        b.BedIdentifier,
                        b.RoomId,
                        RoomNumber = b.Room.RoomNumber,
                        RoomType = b.Room.RoomType,
                        Floor = b.Room.Floor,
                        HostelId = b.Room.HostelId,
                        HostelName = b.Room.Hostel.HostelName,
                        IsReserved = b.Room.IsSpecialReservation
                    })
                    .OrderBy(b => b.HostelName)
                        .ThenBy(b => b.Floor)
                        .ThenBy(b => b.RoomNumber)
                        .ThenBy(b => b.BedIdentifier)
                    .ToListAsync();

                return Json(new { success = true, data = beds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available beds");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AllocateBed([FromBody] AllocationRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate inputs
                if (request.ApplicationId <= 0 || request.BedId <= 0)
                {
                    return Json(new { success = false, message = "Invalid application or bed ID." });
                }

                // Get the application
                var application = await _context.AccommodationApplications
                    .Include(a => a.Student)
                    .Include(a => a.Period)
                    .Include(a => a.Allocation)
                    .FirstOrDefaultAsync(a => a.ApplicationId == request.ApplicationId);

                if (application == null)
                {
                    return Json(new { success = false, message = "Application not found." });
                }

                // Check if already allocated
                if (application.Allocation != null)
                {
                    return Json(new { success = false, message = "This application already has a bed allocation." });
                }

                // Get the bed
                var bed = await _context.BedSpaces
                    .Include(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                    .FirstOrDefaultAsync(b => b.BedId == request.BedId);

                if (bed == null)
                {
                    return Json(new { success = false, message = "Bed not found." });
                }

                // Validate bed availability
                if (bed.Status != Status.Available)
                {
                    return Json(new { success = false, message = "Selected bed is not available." });
                }

                // Validate gender compatibility
                if (bed.Room.Gender != application.Student.Gender && bed.Room.Gender != "Mixed")
                {
                    return Json(new { success = false, message = "Bed gender does not match student gender." });
                }

                var currentUser = await _userManager.GetUserAsync(User);
                var isReservedBed = bed.Room.IsSpecialReservation;
                var allocationType = isReservedBed ? "special" : "individual";

                // Determine allocation dates
                DateTime allocationStartDate = request.StartDate ?? application.Period.StartDate;
                DateTime? allocationEndDate = null;

                if (request.IsUntilGraduation)
                {
                    allocationEndDate = null; // Open-ended
                }
                else if (request.EndDate.HasValue)
                {
                    allocationEndDate = request.EndDate.Value;
                }
                else
                {
                    allocationEndDate = application.Period.EndDate;
                }

                // Create allocation record
                var allocation = new Allocation
                {
                    ApplicationId = application.ApplicationId,
                    BedId = bed.BedId,
                    AllocationType = allocationType,
                    AllocatedById = currentUser.Id,
                    AllocationDate = DateTime.Now,
                    StartDate = allocationStartDate,
                    EndDate = allocationEndDate,
                    IsGraduationBased = request.IsUntilGraduation,
                    Status = Status.Active,
                    CreatedAt = DateTime.Now,
                    CreatedBy = currentUser.UserName
                };

                _context.Allocations.Add(allocation);

                // Update bed status
                bed.Status = Status.Occupied;
                bed.UpdatedAt = DateTime.Now;
                bed.UpdatedBy = currentUser.UserName;

                // Update application status
                application.Status = Status.Approved;
                application.UpdatedAt = DateTime.Now;
                application.UpdatedBy = currentUser.UserName;

                // Update student's bed info (denormalized for quick access)
                application.Student.BedId = bed.BedId;
                application.Student.BedAllocationEndDate = allocationEndDate;
                application.Student.UpdatedAt = DateTime.Now;
                application.Student.UpdatedBy = currentUser.UserName;

                // Assign AccommodatedStudent role
                var user = await _userManager.FindByEmailAsync(application.Student.Email);
                if (user != null)
                {
                    var hasRole = await _userManager.IsInRoleAsync(user, "AccommodatedStudent");
                    if (!hasRole)
                    {
                        // Ensure role exists
                        if (!await _roleManager.RoleExistsAsync("AccommodatedStudent"))
                        {
                            await _roleManager.CreateAsync(new IdentityRole("AccommodatedStudent"));
                        }

                        await _userManager.AddToRoleAsync(user, "AccommodatedStudent");
                        _logger.LogInformation($"Assigned 'AccommodatedStudent' role to {application.Student.StudentId_Number}");
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Successfully allocated bed {bed.BedIdentifier} in room {bed.Room.RoomNumber} " +
                                     $"to student {application.Student.StudentId_Number}");

                return Json(new
                {
                    success = true,
                    message = $"Bed successfully allocated to {application.Student.FullName}",
                    data = new
                    {
                        allocationId = allocation.AllocationId,
                        hostelName = bed.Room.Hostel.HostelName,
                        roomNumber = bed.Room.RoomNumber,
                        bedIdentifier = bed.BedIdentifier,
                        floor = bed.Room.Floor,
                        isReserved = isReservedBed,
                        allocationType,
                        startDate = allocationStartDate.ToString("yyyy-MM-dd"),
                        endDate = allocationEndDate?.ToString("yyyy-MM-dd"),
                        isUntilGraduation = request.IsUntilGraduation,
                        roleAssigned = user != null && await _userManager.IsInRoleAsync(user, "AccommodatedStudent")
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error allocating bed");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Deallocate a bed from a student
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeallocateBed(int allocationId, string reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var allocation = await _context.Allocations
                    .Include(a => a.Application)
                        .ThenInclude(app => app.Student)
                    .Include(a => a.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                    .FirstOrDefaultAsync(a => a.AllocationId == allocationId);

                if (allocation == null)
                {
                    return Json(new { success = false, message = "Allocation not found." });
                }

                var student = allocation.Application.Student;
                var bed = allocation.Bed;
                var currentUser = await _userManager.GetUserAsync(User);

                // Update allocation status
                allocation.Status = Status.Completed;
                allocation.EndDate = DateTime.Now;
                allocation.UpdatedAt = DateTime.Now;
                allocation.UpdatedBy = currentUser.UserName;

                // Update bed status
                bed.Status = Status.Available;
                bed.UpdatedAt = DateTime.Now;
                bed.UpdatedBy = currentUser.UserName;

                // Clear student's bed reference
                student.BedId = null;
                student.BedAllocationEndDate = null;
                student.UpdatedAt = DateTime.Now;
                student.UpdatedBy = currentUser.UserName;

                // Update application notes
                allocation.Application.Notes += $"\n\n[{DateTime.Now:yyyy-MM-dd HH:mm}] Deallocation: {reason}";
                allocation.Application.UpdatedAt = DateTime.Now;
                allocation.Application.UpdatedBy = currentUser.UserName;

                // Remove AccommodatedStudent role
                var user = await _userManager.FindByEmailAsync(student.Email);
                if (user != null)
                {
                    var hasRole = await _userManager.IsInRoleAsync(user, "AccommodatedStudent");
                    if (hasRole)
                    {
                        await _userManager.RemoveFromRoleAsync(user, "AccommodatedStudent");
                        _logger.LogInformation($"Removed 'AccommodatedStudent' role from {student.StudentId_Number}");
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Successfully deallocated bed from student {student.StudentId_Number}. Reason: {reason}");

                return Json(new
                {
                    success = true,
                    message = $"Bed successfully deallocated from {student.FullName}",
                    data = new
                    {
                        studentNumber = student.StudentId_Number,
                        studentName = student.FullName,
                        hostelName = bed.Room.Hostel.HostelName,
                        roomNumber = bed.Room.RoomNumber,
                        bedIdentifier = bed.BedIdentifier
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deallocating bed");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Manually trigger check and revocation of expired allocations
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CheckExpiredAllocations()
        {
            try
            {
                await CheckAndRevokeExpiredAllocations();
                return Json(new { success = true, message = "Expired allocations checked and revoked successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking expired allocations");
                return Json(new { success = false, message = "An error occurred while checking expired allocations." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllocationStatistics(int campusId)
        {
            try
            {
                var activePeriods = await _context.AccommodationPeriods
                    .Where(p => p.Status == Status.Active)
                    .Select(p => p.PeriodId)
                    .ToListAsync();

                // Get pending applications count
                var pendingCount = await _context.AccommodationApplications
                    .Where(a => activePeriods.Contains(a.PeriodId) &&
                                a.Status == Status.Pending &&
                                a.Allocation == null)
                    .CountAsync();

                // Get active allocations by campus
                var activeAllocations = await _context.Allocations
                    .Include(a => a.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                    .Where(a => a.Status == Status.Active &&
                                a.Bed.Room.Hostel.CampusId == campusId)
                    .CountAsync();

                // Get available beds by campus
                var availableBeds = await _context.BedSpaces
                    .Include(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                    .Where(b => b.Status == Status.Available &&
                                b.Room.Hostel.CampusId == campusId)
                    .CountAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalPending = pendingCount,
                        activeAllocations,
                        availableBeds
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting allocation statistics");
                return Json(new { success = false, message = "An error occurred while fetching statistics." });
            }
        }

        /// <summary>
        /// Get list of students with expired allocations
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetExpiredAllocations()
        {
            try
            {
                var today = DateTime.Now.Date;

                var expiredAllocations = await _context.Allocations
                    .Include(a => a.Application)
                        .ThenInclude(app => app.Student)
                    .Include(a => a.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                    .Where(a => a.Status == Status.Active &&
                                a.EndDate.HasValue &&
                                a.EndDate.Value.Date < today)
                    .Select(a => new
                    {
                        allocationId = a.AllocationId,
                        studentNumber = a.Application.Student.StudentId_Number,
                        studentName = a.Application.Student.FullName,
                        email = a.Application.Student.Email,
                        bedId = a.BedId,
                        hostelName = a.Bed.Room.Hostel.HostelName,
                        roomNumber = a.Bed.Room.RoomNumber,
                        bedIdentifier = a.Bed.BedIdentifier,
                        allocationEndDate = a.EndDate,
                        daysExpired = (today - a.EndDate.Value.Date).Days
                    })
                    .OrderBy(a => a.allocationEndDate)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = expiredAllocations,
                    count = expiredAllocations.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expired allocations");
                return Json(new { success = false, message = "An error occurred while fetching expired allocations." });
            }
        }

        /// <summary>
        /// Get list of students with allocations expiring soon
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetExpiringAllocations(int daysAhead = 30)
        {
            try
            {
                var today = DateTime.Now.Date;
                var futureDate = today.AddDays(daysAhead);

                var expiringAllocations = await _context.Allocations
                    .Include(a => a.Application)
                        .ThenInclude(app => app.Student)
                    .Include(a => a.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                    .Where(a => a.Status == Status.Active &&
                                a.EndDate.HasValue &&
                                a.EndDate.Value.Date >= today &&
                                a.EndDate.Value.Date <= futureDate)
                    .Select(a => new
                    {
                        allocationId = a.AllocationId,
                        studentNumber = a.Application.Student.StudentId_Number,
                        studentName = a.Application.Student.FullName,
                        email = a.Application.Student.Email,
                        phone = a.Application.Student.Phone,
                        bedId = a.BedId,
                        hostelName = a.Bed.Room.Hostel.HostelName,
                        roomNumber = a.Bed.Room.RoomNumber,
                        bedIdentifier = a.Bed.BedIdentifier,
                        allocationEndDate = a.EndDate,
                        daysRemaining = (a.EndDate.Value.Date - today).Days
                    })
                    .OrderBy(a => a.allocationEndDate)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = expiringAllocations,
                    count = expiringAllocations.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expiring allocations");
                return Json(new { success = false, message = "An error occurred while fetching expiring allocations." });
            }
        }
    }

    // Request model for bed allocation
    public class AllocationRequest
    {
        public int ApplicationId { get; set; }
        public int BedId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsUntilGraduation { get; set; }
    }
}