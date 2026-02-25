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
    [Authorize(Roles = "Admin,HostelManager,Registrar")]
    public class BedAllocationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccommodationAllocationService _allocationService;

        public BedAllocationController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAccommodationAllocationService allocationService)
        {
            _context = context;
            _userManager = userManager;
            _allocationService = allocationService;
        }


        // GET: BedAllocation/Index - Automatic allocation with student list
        public async Task<IActionResult> Index(string searchTerm = "", string searchBy = "AllFields", string filterStatus = "NotAccommodated", int? selectedHostelId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
            var isHostelManager = await _userManager.IsInRoleAsync(currentUser, "HostelManager");

            // Get user's assigned hostel(s) if Hostel Manager
            Hostel assignedHostel = null;
            List<Hostel> assignedHostels = new List<Hostel>();

            if (isHostelManager)
            {
                // Get all hostels assigned to this manager
                assignedHostels = await _context.Hostels
                    .Include(h => h.Campus)
                    .Where(h => h.WardenId == currentUser.Id && h.Status == Status.Active)
                    .OrderBy(h => h.HostelName)
                    .ToListAsync();

                if (!assignedHostels.Any())
                {
                    TempData["Error"] = "You are not assigned to any hostel. Please contact administration.";
                    return View("Error");
                }

                // If manager has multiple hostels, use selected one or default to first
                if (assignedHostels.Count > 1)
                {
                    if (selectedHostelId.HasValue)
                    {
                        assignedHostel = assignedHostels.FirstOrDefault(h => h.HostelId == selectedHostelId.Value);
                    }

                    // If no valid selection, use first hostel
                    if (assignedHostel == null)
                    {
                        assignedHostel = assignedHostels.First();
                    }
                }
                else
                {
                    // Only one hostel assigned
                    assignedHostel = assignedHostels.First();
                }
            }

            // Get campuses
            var campuses = await _context.Campuses
                .Where(c => c.IsActive)
                .ToListAsync();

            // Build student query - ALWAYS include all navigation properties
            IQueryable<Student> studentQuery = _context.Students
                .AsNoTracking()
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Include(s => s.AcademicYear)
                .Include(s => s.ProgrammeLevel)
                .Include(s => s.ModeOfStudy)
                .Include(s => s.BedSpace)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .Where(s => s.RegistrationStatus == Status.Registered);

            // Filter by accommodation status
            if (filterStatus == "NotAccommodated")
            {
                studentQuery = studentQuery.Where(s => s.BedId == null || s.BedId == 0);
            }
            else if (filterStatus == "Accommodated")
            {
                studentQuery = studentQuery.Where(s => s.BedId != null && s.BedId > 0);

                // If Hostel Manager, filter to only their hostel's students
                if (isHostelManager && assignedHostel != null)
                {
                    // Get list of BedIds in the hostel manager's hostel
                    var hostelBedIds = await _context.BedSpaces
                        .Where(b => b.Room.HostelId == assignedHostel.HostelId)
                        .Select(b => b.BedId)
                        .ToListAsync();

                    if (hostelBedIds.Any())
                    {
                        studentQuery = studentQuery.Where(s => hostelBedIds.Contains(s.BedId.Value));
                    }
                    else
                    {
                        // No beds in this hostel
                        studentQuery = studentQuery.Where(s => false);
                    }
                }
            }

            // Apply search
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                if (searchBy == "StudentNumber")
                {
                    studentQuery = studentQuery.Where(s => s.StudentId_Number.Contains(searchTerm));
                }
                else if (searchBy == "StudentName")
                {
                    studentQuery = studentQuery.Where(s => s.FullName.Contains(searchTerm));
                }
                else if (searchBy == "Email")
                {
                    studentQuery = studentQuery.Where(s => s.Email.Contains(searchTerm));
                }
                else // AllFields
                {
                    studentQuery = studentQuery.Where(s =>
                        s.StudentId_Number.Contains(searchTerm) ||
                        s.FullName.Contains(searchTerm) ||
                        s.Email.Contains(searchTerm) ||
                        (s.Phone != null && s.Phone.Contains(searchTerm)));
                }
            }

            // DEBUG: Log the query before execution
            var debugCount = await studentQuery.CountAsync();
            Console.WriteLine($"DEBUG Filter={filterStatus}: Query will return {debugCount} students");

            // Execute query
            var students = await studentQuery
                .OrderBy(s => s.FullName)
                .ToListAsync();

            Console.WriteLine($"DEBUG Filter={filterStatus}: Actually loaded {students.Count} students");

            if (students.Any())
            {
                var firstStudent = students.First();
                Console.WriteLine($"DEBUG First student: {firstStudent.StudentId_Number}, BedId={firstStudent.BedId}, HasBedSpace={firstStudent.BedSpace != null}");
            }

            // Get statistics
            var totalStudents = await _context.Students
                .Where(s => s.RegistrationStatus == Status.Registered)
                .CountAsync();

            var accommodatedCount = await _context.Students
                .Where(s => s.RegistrationStatus == Status.Registered &&
                           s.BedId != null &&
                           s.BedId > 0)
                .CountAsync();

            var notAccommodatedCount = await _context.Students
                .Where(s => s.RegistrationStatus == Status.Registered &&
                           (s.BedId == null || s.BedId == 0))
                .CountAsync();

            // Get bed statistics
            IQueryable<Hostel> hostelQuery = _context.Hostels
                .Include(h => h.Rooms)
                    .ThenInclude(r => r.BedSpaces)
                .Where(h => h.Status == Status.Active);

            if (isHostelManager && assignedHostel != null)
            {
                hostelQuery = hostelQuery.Where(h => h.HostelId == assignedHostel.HostelId);
            }

            var hostels = await hostelQuery.ToListAsync();
            var totalBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces).Count();
            var occupiedBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces)
                .Count(b => b.Status == Status.Occupied);
            var availableBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces)
                .Count(b => b.Status == Status.Available);

            var viewModel = new BedAllocationIndexViewModel
            {
                Students = students,
                Campuses = campuses,
                AssignedHostel = assignedHostel,
                AssignedHostels = assignedHostels,
                IsAdmin = isAdmin,
                IsHostelManager = isHostelManager,
                SearchTerm = searchTerm,
                SearchBy = searchBy,
                FilterStatus = filterStatus,
                TotalStudents = totalStudents,
                AccommodatedCount = accommodatedCount,
                NotAccommodatedCount = notAccommodatedCount,
                TotalBeds = totalBeds,
                OccupiedBeds = occupiedBeds,
                AvailableBeds = availableBeds
            };

            return View("~/Views/Accommodation/BedAllocations/BedAllocation_Index.cshtml", viewModel);
        }

        // GET: Get Student Details
        [HttpGet]
        public async Task<IActionResult> GetStudentDetails(int studentId)
        {
            var student = await _context.Students
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Include(s => s.AcademicYear)
                .Include(s => s.ProgrammeLevel)
                .Include(s => s.ModeOfStudy)
                .Include(s => s.BedSpace)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
            var isHostelManager = await _userManager.IsInRoleAsync(currentUser, "HostelManager");

            // Get campuses
            var campuses = await _context.Campuses
                .Where(c => c.IsActive)
                .ToListAsync();

            var viewModel = new BedAllocationStudentDetailsViewModel
            {
                Student = student,
                IsAccommodated = student.BedId.HasValue,
                CanAllocate = !student.BedId.HasValue,
                CanDeallocate = student.BedId.HasValue,
                IsAdmin = isAdmin,
                IsHostelManager = isHostelManager,
                Campuses = campuses
            };

            return PartialView("~/Views/Accommodation/BedAllocations/_StudentDetails.cshtml", viewModel);
        }

        // GET: Get Available Hostels by Campus
        [HttpGet]
        public async Task<IActionResult> GetHostelsByCampus(int campusId, string gender)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            IQueryable<Hostel> query = _context.Hostels
                .Include(h => h.Rooms)
                    .ThenInclude(r => r.BedSpaces)
                .Where(h => h.Status == Status.Active &&
                           (h.Gender == gender || h.Gender == "Mixed"));

            // If campus is specified, filter by campus
            if (campusId > 0)
            {
                query = query.Where(h => h.CampusId == campusId);
            }

            // Filter by hostel manager if not admin
            if (!isAdmin)
            {
                query = query.Where(h => h.WardenId == currentUser.Id);
            }

            var hostels = await query
                .Select(h => new
                {
                    hostelId = h.HostelId,
                    hostelName = h.HostelName,
                    gender = h.Gender,
                    availableBeds = h.Rooms
                        .Where(r => r.Status == Status.Available &&
                                   (r.Gender == gender || r.Gender == "Mixed"))
                        .SelectMany(r => r.BedSpaces)
                        .Count(b => b.Status == Status.Available),
                    totalBeds = h.Rooms
                        .SelectMany(r => r.BedSpaces)
                        .Count()
                })
                .OrderBy(h => h.hostelName)
                .ToListAsync();

            return Json(new { success = true, hostels });
        }

        // GET: Get Available Rooms by Hostel
        [HttpGet]
        public async Task<IActionResult> GetRoomsByHostel(int hostelId, string gender)
        {
            var rooms = await _context.Rooms
                .Include(r => r.BedSpaces)
                .Where(r => r.HostelId == hostelId &&
                           r.Status == Status.Available &&
                           (r.Gender == gender || r.Gender == "Mixed"))
                .Select(r => new
                {
                    roomId = r.RoomId,
                    roomNumber = r.RoomNumber,
                    floor = r.Floor,
                    roomType = r.RoomType,
                    capacity = r.Capacity,
                    availableBeds = r.BedSpaces.Count(b => b.Status == Status.Available),
                    totalBeds = r.BedSpaces.Count
                })
                .OrderBy(r => r.roomNumber)
                .ToListAsync();

            return Json(new { success = true, rooms });
        }

        // GET: Get Available Beds by Room
        [HttpGet]
        public async Task<IActionResult> GetBedsByRoom(int roomId)
        {
            var beds = await _context.BedSpaces
                .Where(b => b.RoomId == roomId && b.Status == Status.Available)
                .Select(b => new
                {
                    bedId = b.BedId,
                    bedIdentifier = b.BedIdentifier,
                    status = b.Status.ToString()
                })
                .OrderBy(b => b.bedIdentifier)
                .ToListAsync();

            return Json(new { success = true, beds });
        }

        // POST: Allocate Bed Space (Automatic System)
        [HttpPost]
        public async Task<IActionResult> AllocateBed([FromBody] AllocateBedRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid request data" });
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // Use the NEW direct allocation service method
                var result = await _allocationService.DirectBedAllocation(
                    request.StudentId,
                    request.BedId,
                    currentUser.Id,
                    "Direct allocation via Bed Allocation Management"
                );

                if (result.Status)
                {
                    // Get bed details for response
                    var bed = await _context.BedSpaces
                        .Include(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                                .ThenInclude(h => h.Campus)
                        .FirstOrDefaultAsync(b => b.BedId == request.BedId);

                    return Json(new
                    {
                        success = true,
                        message = result.Message,
                        data = new
                        {
                            bedLocation = $"{bed.Room.Hostel.HostelName} - Room {bed.Room.RoomNumber} - Bed {bed.BedIdentifier}",
                            campusName = bed.Room.Hostel.Campus.CampusName,
                            hostelName = bed.Room.Hostel.HostelName,
                            roomNumber = bed.Room.RoomNumber,
                            bedIdentifier = bed.BedIdentifier,
                            floor = bed.Room.Floor
                        }
                    });
                }

                return Json(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error allocating bed: {ex.Message}" });
            }
        }

        // POST: Deallocate Bed Space
        [HttpPost]
        public async Task<IActionResult> DeallocateBed([FromBody] DeallocateBedRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid request data" });
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // Use the allocation service
                var result = await _allocationService.RemoveStudentFromAccommodation(
                    request.StudentId,
                    currentUser.Id,
                    request.Reason
                );

                return Json(new { success = result.Status, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deallocating bed: {ex.Message}" });
            }
        }

        // ============================================
        // MANUAL ALLOCATION SYSTEM (OLD - KEPT FOR COMPATIBILITY)
        // ============================================

        // GET: BedAllocation/ManualAllocation - Manual allocation with form
        public async Task<IActionResult> ManualAllocation()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            // Get accommodation periods - removed AcademicYear include
            var periods = await _context.AccommodationPeriods
                .Where(p => p.Status == Status.Active)
                .OrderByDescending(p => p.ApplicationStartDate)
                .ToListAsync();

            // Get campuses with available beds
            var campuses = await _context.Campuses
                .Where(c => c.IsActive)
                .ToListAsync();

            // Get hostels based on user role
            IQueryable<Hostel> hostelQuery = _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Rooms)
                    .ThenInclude(r => r.BedSpaces)
                .Where(h => h.Status == Status.Active);

            if (!isAdmin)
            {
                hostelQuery = hostelQuery.Where(h => h.WardenId == currentUser.Id);
            }

            var hostels = await hostelQuery.ToListAsync();

            var viewModel = new BedAllocationManualViewModel
            {
                // Updated to use TypeOfPayment instead of AcademicYear
                AccommodationPeriods = periods.Select(p => new SelectListItem
                {
                    Value = p.PeriodId.ToString(),
                    Text = $"{p.Type} - {p.TypeOfPayment} (K{p.TypeOfPaymentAmount:N2}{(p.TypeOfPayment == "PerDay" ? "/day" : "")}) | {p.StartDate:MMM dd, yyyy} - {(p.EndDate.HasValue ? p.EndDate.Value.ToString("MMM dd, yyyy") : "Graduation")}"
                }).ToList(),
                Campuses = campuses.Select(c => new SelectListItem
                {
                    Value = c.CampusId.ToString(),
                    Text = c.CampusName
                }).ToList(),
                Hostels = hostels.Select(h => new SelectListItem
                {
                    Value = h.HostelId.ToString(),
                    Text = $"{h.HostelName} ({h.Campus.CampusName})"
                }).ToList(),
                AllocationStatistics = await GetAllocationStatistics(isAdmin, currentUser.Id)
            };

            return View("~/Views/Accommodation/BedAllocation_Manual.cshtml", viewModel);
        }

        // GET: Search Student for Manual Allocation
        [HttpGet]
        public async Task<IActionResult> SearchStudent(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Json(new { success = false, message = "Please enter a search term" });
            }

            searchTerm = searchTerm.Trim();
            bool isEmail = searchTerm.Contains("@");

            Student student = null;

            // Search by email or student ID
            if (isEmail)
            {
                student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Include(s => s.AcademicYear)
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.ProgrammeLevel)
                    .Include(s => s.BedSpace)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
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
                    .Include(s => s.BedSpace)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == searchTerm);
            }

            if (student == null)
            {
                string identifier = isEmail ? "email address" : "student ID";
                return Json(new
                {
                    success = false,
                    message = $"No student found with this {identifier}"
                });
            }

            // Verify user has Student role
            var user = await _userManager.FindByEmailAsync(student.Email);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Student"))
            {
                return Json(new
                {
                    success = false,
                    message = "This user account is not registered as a student"
                });
            }

            // Check for existing active allocation - updated to not rely on AcademicYear
            var currentDate = DateTime.Now;
            var existingAllocation = await _context.Allocations
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .Include(a => a.Application)
                    .ThenInclude(app => app.Period)
                .FirstOrDefaultAsync(a => a.Application.StudentId == student.Id &&
                                         a.Status == Status.Active &&
                                         (!a.EndDate.HasValue || a.EndDate.Value > currentDate));

            return Json(new
            {
                success = true,
                student = new
                {
                    studentId = student.Id,
                    studentNumber = student.StudentId_Number,
                    fullName = student.FullName,
                    email = student.Email,
                    phone = student.Phone,
                    gender = student.Gender,
                    programme = student.Programme?.Name,
                    school = student.Programme?.Department?.School?.Name,
                    department = student.Programme?.Department?.Name,
                    programmeLevel = student.ProgrammeLevel?.Name,
                    modeOfStudy = student.ModeOfStudy?.ModeName,
                    yearOfStudy = student.StudentCurrentYear,
                    academicYear = student.AcademicYear?.YearValue,
                    status = student.StudentStatus.ToString(),
                    passportPhoto = student.PassportPhotoPath,
                    outstandingFees = student.OutstandingFees
                },
                hasActiveAllocation = existingAllocation != null,
                existingAllocation = existingAllocation != null ? new
                {
                    allocationId = existingAllocation.AllocationId,
                    hostelName = existingAllocation.Bed?.Room?.Hostel?.HostelName,
                    campusName = existingAllocation.Bed?.Room?.Hostel?.Campus?.CampusName,
                    roomNumber = existingAllocation.Bed?.Room?.RoomNumber,
                    bedIdentifier = existingAllocation.Bed?.BedIdentifier,
                    startDate = existingAllocation.StartDate.ToString("yyyy-MM-dd"),
                    endDate = existingAllocation.EndDate?.ToString("yyyy-MM-dd"),
                    isGraduationBased = existingAllocation.IsGraduationBased,
                    // Updated to use TypeOfPayment instead of YearOfStudy from AcademicYear
                    periodType = existingAllocation.Application?.Period?.Type,
                    paymentType = existingAllocation.Application?.Period?.TypeOfPayment,
                    paymentAmount = existingAllocation.Application?.Period?.TypeOfPaymentAmount ?? 0
                } : null
            });
        }

        // GET: Get period details for fee calculation
        [HttpGet]
        public async Task<IActionResult> GetPeriodDetails(int periodId)
        {
            var period = await _context.AccommodationPeriods
                .FirstOrDefaultAsync(p => p.PeriodId == periodId);

            if (period == null)
            {
                return Json(new { success = false, message = "Period not found" });
            }

            return Json(new
            {
                success = true,
                period = new
                {
                    periodId = period.PeriodId,
                    type = period.Type,
                    typeOfPayment = period.TypeOfPayment,
                    typeOfPaymentAmount = period.TypeOfPaymentAmount,
                    startDate = period.StartDate.ToString("yyyy-MM-dd"),
                    endDate = period.EndDate?.ToString("yyyy-MM-dd"),
                    isPerDay = period.TypeOfPayment == "PerDay",
                    maxDays = period.EndDate.HasValue ? (int)(period.EndDate.Value - period.StartDate).TotalDays : 365,
                    feeDisplay = period.TypeOfPayment == "PerDay"
                        ? $"K{period.TypeOfPaymentAmount:N2}/day"
                        : $"K{period.TypeOfPaymentAmount:N2} ({period.TypeOfPayment})"
                }
            });
        }

        // POST: Allocate Bed Space (Manual System)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AllocateBedManual([FromBody] ManualBedAllocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid request data" });
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // Use the allocation service
                var result = await _allocationService.AssignBedSpaceWithoutPayment(
                    request.StudentId,
                    request.BedId,
                    currentUser.Id
                );

                if (result.Status)
                {
                    // Get student and bed details for response
                    var student = await _context.Students.FindAsync(request.StudentId);
                    var bed = await _context.BedSpaces
                        .Include(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                        .FirstOrDefaultAsync(b => b.BedId == request.BedId);

                    // Get period for fee information
                    var period = await _context.AccommodationPeriods
                        .FirstOrDefaultAsync(p => p.PeriodId == request.AccommodationPeriodId);

                    decimal calculatedFee = 0;
                    if (period != null)
                    {
                        calculatedFee = period.TypeOfPayment == "PerDay"
                            ? period.TypeOfPaymentAmount * (request.NumberOfDays ?? 1)
                            : period.TypeOfPaymentAmount;
                    }

                    return Json(new
                    {
                        success = true,
                        message = result.Message,
                        allocation = new
                        {
                            allocationId = 0,
                            studentName = student?.FullName,
                            hostelName = bed?.Room?.Hostel?.HostelName,
                            roomNumber = bed?.Room?.RoomNumber,
                            bedIdentifier = bed?.BedIdentifier,
                            startDate = (request.StartDate ?? DateTime.Now).ToString("yyyy-MM-dd"),
                            endDate = request.CustomEndDate?.ToString("yyyy-MM-dd"),
                            periodType = period?.Type,
                            paymentType = period?.TypeOfPayment,
                            calculatedFee = calculatedFee
                        }
                    });
                }

                return Json(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error allocating bed: {ex.Message}" });
            }
        }

        // POST: Deallocate Bed Space (Manual System)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeallocateBedManual(int allocationId, string reason)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                var allocation = await _context.Allocations
                    .Include(a => a.Application)
                    .FirstOrDefaultAsync(a => a.AllocationId == allocationId);

                if (allocation == null)
                {
                    return Json(new { success = false, message = "Allocation not found" });
                }

                var result = await _allocationService.RemoveStudentFromAccommodation(
                    allocation.Application.StudentId,
                    currentUser.Id,
                    reason
                );

                return Json(new { success = result.Status, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deallocating bed: {ex.Message}" });
            }
        }

        // Helper method for statistics
        private async Task<AllocationStatisticsViewModel> GetAllocationStatistics(bool isAdmin, string userId)
        {
            IQueryable<Hostel> hostelQuery = _context.Hostels
                .Include(h => h.Rooms)
                    .ThenInclude(r => r.BedSpaces)
                .Where(h => h.Status == Status.Active);

            if (!isAdmin)
            {
                hostelQuery = hostelQuery.Where(h => h.WardenId == userId);
            }

            var hostels = await hostelQuery.ToListAsync();

            var totalBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces).Count();
            var occupiedBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces)
                .Count(b => b.Status == Status.Occupied);
            var availableBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces)
                .Count(b => b.Status == Status.Available);
            var reservedBeds = hostels.SelectMany(h => h.Rooms).SelectMany(r => r.BedSpaces)
                .Count(b => b.Status == Status.Reserved);

            return new AllocationStatisticsViewModel
            {
                TotalBeds = totalBeds,
                OccupiedBeds = occupiedBeds,
                AvailableBeds = availableBeds,
                ReservedBeds = reservedBeds,
                OccupancyRate = totalBeds > 0 ? (double)occupiedBeds / totalBeds * 100 : 0
            };
        }
    }

    // ============================================
    // VIEW MODELS
    // ============================================

    // Automatic System View Models
    public class BedAllocationIndexViewModel
    {
        public List<Student> Students { get; set; } = new List<Student>();
        public List<Campus> Campuses { get; set; } = new List<Campus>();
        public Hostel AssignedHostel { get; set; }
        public List<Hostel> AssignedHostels { get; set; } = new List<Hostel>();
        public bool IsAdmin { get; set; }
        public bool IsHostelManager { get; set; }
        public string SearchTerm { get; set; }
        public string SearchBy { get; set; }
        public string FilterStatus { get; set; }
        public int TotalStudents { get; set; }
        public int AccommodatedCount { get; set; }
        public int NotAccommodatedCount { get; set; }
        public int TotalBeds { get; set; }
        public int OccupiedBeds { get; set; }
        public int AvailableBeds { get; set; }
    }

    public class BedAllocationStudentDetailsViewModel
    {
        public Student Student { get; set; }
        public bool IsAccommodated { get; set; }
        public bool CanAllocate { get; set; }
        public bool CanDeallocate { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsHostelManager { get; set; }
        public List<Campus> Campuses { get; set; } = new List<Campus>();
    }

    public class AllocateBedRequest
    {
        [Required]
        public int StudentId { get; set; }

        [Required]
        public int BedId { get; set; }
    }

    public class DeallocateBedRequest
    {
        [Required]
        public int StudentId { get; set; }

        [Required]
        public string Reason { get; set; }
    }

    // Manual System View Models
    public class BedAllocationManualViewModel
    {
        public List<SelectListItem> AccommodationPeriods { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Campuses { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Hostels { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Rooms { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Beds { get; set; } = new List<SelectListItem>();
        public AllocationStatisticsViewModel AllocationStatistics { get; set; }
    }

    public class AllocationStatisticsViewModel
    {
        public int TotalBeds { get; set; }
        public int OccupiedBeds { get; set; }
        public int AvailableBeds { get; set; }
        public int ReservedBeds { get; set; }
        public double OccupancyRate { get; set; }
    }

    public class ManualBedAllocationRequest
    {
        [Required]
        public int StudentId { get; set; }

        [Required]
        public int BedId { get; set; }

        [Required]
        public int AccommodationPeriodId { get; set; }

        [Required]
        public string AllocationType { get; set; } // "period", "graduation", "custom"

        public DateTime? StartDate { get; set; }

        public DateTime? CustomEndDate { get; set; }

        public int? NumberOfDays { get; set; } // For PerDay payment type

        public string Notes { get; set; }
    }
}