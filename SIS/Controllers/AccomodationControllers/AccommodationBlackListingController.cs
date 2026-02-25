using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Interfaces;
using SIS.Models.StudentApplication;
using System.Text.Json;

namespace SIS.Controllers.AccomodationControllers
{
    [Authorize(Roles = "Admin,HostelManager")]
    public class AccommodationBlackListingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccommodationAllocationService _allocationService;

        public AccommodationBlackListingController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAccommodationAllocationService allocationService)
        {
            _context = context;
            _userManager = userManager;
            _allocationService = allocationService;
        }

        // GET: Index
        [HttpGet]
        public async Task<IActionResult> Index(
            string searchTerm = "",
            string searchBy = "AllFields",
            string filterStatus = "Blacklisted",
            int? selectedHostelId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
            var isHostelManager = await _userManager.IsInRoleAsync(currentUser, "HostelManager");

            // Get assigned hostels for hostel managers
            var assignedHostels = new List<SIS.Models.StudentAccommodation.Hostel>();
            SIS.Models.StudentAccommodation.Hostel assignedHostel = null;

            if (isHostelManager)
            {
                assignedHostels = await _context.Hostels
                    .Include(h => h.Campus)
                    .Where(h => h.WardenId == currentUser.Id && h.Status == Status.Active)
                    .ToListAsync();

                if (selectedHostelId.HasValue)
                {
                    assignedHostel = assignedHostels.FirstOrDefault(h => h.HostelId == selectedHostelId.Value);
                }
                else
                {
                    assignedHostel = assignedHostels.FirstOrDefault();
                }
            }

            // Base query
            var query = _context.Students.AsNoTracking()
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Include(s => s.ProgrammeLevel)
                .Include(s => s.ModeOfStudy)
                .Include(s => s.AcademicYear)
                .Include(s => s.BedSpace)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .AsQueryable();

            // Apply hostel manager filter
            if (isHostelManager && assignedHostel != null)
            {
                var hostelId = assignedHostel.HostelId;
                query = query.Where(s => s.BedSpace == null || s.BedSpace.Room.HostelId == hostelId);
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                switch (searchBy)
                {
                    case "StudentNumber":
                        query = query.Where(s => s.StudentId_Number.Contains(searchTerm));
                        break;
                    case "StudentName":
                        query = query.Where(s => s.FullName.Contains(searchTerm));
                        break;
                    case "Email":
                        query = query.Where(s => s.Email.Contains(searchTerm));
                        break;
                    default: // AllFields
                        query = query.Where(s =>
                            s.StudentId_Number.Contains(searchTerm) ||
                            s.FullName.Contains(searchTerm) ||
                            s.Email.Contains(searchTerm));
                        break;
                }
            }

            // Apply status filter
            if (filterStatus == "Blacklisted")
            {
                query = query.Where(s => s.IsBlackListedFromAccommodation);
            }
            else if (filterStatus == "NotBlacklisted")
            {
                query = query.Where(s => !s.IsBlackListedFromAccommodation);
            }

            var students = await query
                .OrderBy(s => s.FullName)
                .ToListAsync();

            // Get statistics
            var totalQuery = _context.Students.AsQueryable();

            if (isHostelManager && assignedHostel != null)
            {
                var hostelId = assignedHostel.HostelId;
                totalQuery = totalQuery.Where(s => s.BedSpace == null || s.BedSpace.Room.HostelId == hostelId);
            }

            var totalStudents = await totalQuery.CountAsync();
            var blacklistedCount = await totalQuery.CountAsync(s => s.IsBlackListedFromAccommodation);
            var notBlacklistedCount = totalStudents - blacklistedCount;

            var model = new BlackListingIndexViewModel
            {
                Students = students,
                SearchTerm = searchTerm,
                SearchBy = searchBy,
                FilterStatus = filterStatus,
                TotalStudents = students.Count,
                BlacklistedCount = blacklistedCount,
                NotBlacklistedCount = notBlacklistedCount,
                IsAdmin = isAdmin,
                IsHostelManager = isHostelManager,
                AssignedHostels = assignedHostels,
                AssignedHostel = assignedHostel
            };

            return View("~/Views/Accommodation/BlackListing/Index.cshtml", model);
        }

        // GET: Get Student Details Partial
        [HttpGet]
        public async Task<IActionResult> GetStudentDetails(int studentId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
            var isHostelManager = await _userManager.IsInRoleAsync(currentUser, "HostelManager");

            var student = await _context.Students
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Include(s => s.ProgrammeLevel)
                .Include(s => s.ModeOfStudy)
                .Include(s => s.AcademicYear)
                .Include(s => s.BedSpace)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                return NotFound();
            }

            var model = new BlackListingStudentDetailsViewModel
            {
                Student = student,
                IsBlacklisted = student.IsBlackListedFromAccommodation,
                BlacklistReason = student.BlackListedFromAccommodationReason,
                IsAccommodated = student.BedId.HasValue,
                CanBlacklist = !student.IsBlackListedFromAccommodation,
                CanUnblacklist = student.IsBlackListedFromAccommodation,
                IsAdmin = isAdmin,
                IsHostelManager = isHostelManager
            };

            return PartialView("~/Views/Accommodation/BlackListing/_StudentDetails.cshtml", model);
        }

        // POST: Blacklist Student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlacklistStudent([FromBody] BlacklistRequest request)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.BedSpace)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                                .ThenInclude(h => h.Campus)
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                if (student.IsBlackListedFromAccommodation)
                {
                    return Json(new { success = false, message = "Student is already blacklisted" });
                }

                var wasAccommodated = student.BedId.HasValue;
                var previousBedLocation = "";

                // If student currently has accommodation, use the allocation service to remove them
                if (wasAccommodated)
                {
                    // Store bed location before removal
                    var bedSpace = student.BedSpace;
                    if (bedSpace != null)
                    {
                        previousBedLocation = $"{bedSpace.Room.Hostel.HostelName} - Room {bedSpace.Room.RoomNumber} - Bed {bedSpace.BedIdentifier}";
                    }

                    // Use the allocation service to properly remove student from accommodation
                    var removalResult = await _allocationService.RemoveStudentFromAccommodation(
                        student.Id,
                        User.Identity.Name,
                        $"BLACKLISTED: {request.Reason}"
                    );

                    // If removal fails, check if it's because student is not accommodated
                    // (which means they were removed between our check and service call - acceptable)
                    if (!removalResult.Status)
                    {
                        // If the error is NOT about "no active allocation", then it's a real error
                        if (!removalResult.Message.Contains("No active accommodation allocation"))
                        {
                            return Json(new
                            {
                                success = false,
                                message = $"Failed to remove student from accommodation: {removalResult.Message}"
                            });
                        }
                        // Otherwise, student was already removed - continue with blacklisting
                        wasAccommodated = false;
                        previousBedLocation = "Accommodation was already removed";
                    }

                    // Refresh student data after removal
                    await _context.Entry(student).ReloadAsync();
                }

                // Cancel any pending applications
                var pendingApplications = await _context.AccommodationApplications
                    .Where(a => a.StudentId == student.Id && a.Status == Status.Pending)
                    .ToListAsync();

                foreach (var app in pendingApplications)
                {
                    app.Status = Status.Canceled;
                    app.Notes += $" | [CANCELED: Student blacklisted on {DateTime.Now:yyyy-MM-dd HH:mm}]";
                    app.UpdatedAt = DateTime.Now;
                    app.UpdatedBy = User.Identity.Name;
                }

                // Blacklist the student with reason
                student.IsBlackListedFromAccommodation = true;
                student.BlackListedFromAccommodationReason = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] By: {User.Identity.Name} | Reason: {request.Reason}";
                student.UpdatedAt = DateTime.Now;
                student.UpdatedBy = User.Identity.Name;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Student {student.FullName} has been blacklisted from accommodation services" +
                              (wasAccommodated ? " and their accommodation has been deallocated." : "."),
                    data = new
                    {
                        studentId = student.Id,
                        studentName = student.FullName,
                        blacklistReason = request.Reason,
                        wasAccommodated = wasAccommodated,
                        previousBedLocation = previousBedLocation
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Unblacklist Student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblacklistStudent([FromBody] UnblacklistRequest request)
        {
            try
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                if (!student.IsBlackListedFromAccommodation)
                {
                    return Json(new { success = false, message = "Student is not blacklisted" });
                }

                var previousReason = student.BlackListedFromAccommodationReason;

                // Unblacklist the student and update reason
                student.IsBlackListedFromAccommodation = false;
                student.BlackListedFromAccommodationReason += $" | [REMOVED: {DateTime.Now:yyyy-MM-dd HH:mm}] By: {User.Identity.Name} | Removal Reason: {request.Reason}";
                student.UpdatedAt = DateTime.Now;
                student.UpdatedBy = User.Identity.Name;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Student {student.FullName} has been removed from the blacklist and can now apply for accommodation",
                    data = new
                    {
                        studentId = student.Id,
                        studentName = student.FullName,
                        unblacklistReason = request.Reason,
                        previousReason = previousReason
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }

    // ============================================
    // REQUEST MODELS
    // ============================================

    public class BlacklistRequest
    {
        public int StudentId { get; set; }
        public string Reason { get; set; }
    }

    public class UnblacklistRequest
    {
        public int StudentId { get; set; }
        public string Reason { get; set; }
    }

    // ============================================
    // VIEW MODELS
    // ============================================

    public class BlackListingIndexViewModel
    {
        public List<Student> Students { get; set; } = new List<Student>();
        public string SearchTerm { get; set; }
        public string SearchBy { get; set; }
        public string FilterStatus { get; set; }
        public int TotalStudents { get; set; }
        public int BlacklistedCount { get; set; }
        public int NotBlacklistedCount { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsHostelManager { get; set; }
        public List<SIS.Models.StudentAccommodation.Hostel> AssignedHostels { get; set; }
        public SIS.Models.StudentAccommodation.Hostel AssignedHostel { get; set; }
    }

    public class BlackListingStudentDetailsViewModel
    {
        public Student Student { get; set; }
        public bool IsBlacklisted { get; set; }
        public string BlacklistReason { get; set; }
        public bool IsAccommodated { get; set; }
        public bool CanBlacklist { get; set; }
        public bool CanUnblacklist { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsHostelManager { get; set; }
    }
}