using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.StudentAccommodation;
using SIS.Models.Admin;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin,HostelManager")]
    public class HostelManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HostelManagerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: HostelManager/Index
        public async Task<IActionResult> Index()
        {
            var hostels = await _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Warden)
                .Include(h => h.Rooms)
                    .ThenInclude(r => r.BedSpaces)
                .ToListAsync();

            // Calculate statistics for each hostel using ViewBag
            var hostelStats = hostels.Select(h => new
            {
                HostelId = h.HostelId,
                TotalBeds = h.Rooms.Sum(r => r.BedSpaces.Count),
                OccupiedBeds = h.Rooms.Sum(r => r.BedSpaces.Count(b => b.Status == Status.Occupied)),
                AvailableBeds = h.Rooms.Sum(r => r.BedSpaces.Count(b => b.Status == Status.Available))
            }).ToList();

            ViewBag.HostelStats = hostelStats;

            return View(hostels);
        }

        // GET: HostelManager/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // FIXED: BedSpace no longer has direct Student navigation
            var hostel = await _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Warden)
                .Include(h => h.Rooms)
                    .ThenInclude(r => r.BedSpaces)
                .FirstOrDefaultAsync(m => m.HostelId == id);

            if (hostel == null)
            {
                return NotFound();
            }

            // Calculate detailed statistics
            ViewBag.TotalRooms = hostel.Rooms.Count;
            ViewBag.TotalBeds = hostel.Rooms.Sum(r => r.BedSpaces.Count);
            ViewBag.OccupiedBeds = hostel.Rooms.Sum(r => r.BedSpaces.Count(b => b.Status == Status.Occupied));
            ViewBag.AvailableBeds = hostel.Rooms.Sum(r => r.BedSpaces.Count(b => b.Status == Status.Available));
            ViewBag.OccupancyRate = ViewBag.TotalBeds > 0
                ? Math.Round((decimal)ViewBag.OccupiedBeds / ViewBag.TotalBeds * 100, 2)
                : 0;

            // Group rooms by floor
            var roomsByFloor = hostel.Rooms
                .GroupBy(r => r.Floor)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Floor = g.Key,
                    Rooms = g.OrderBy(r => r.RoomNumber).ToList(),
                    TotalBeds = g.Sum(r => r.BedSpaces.Count),
                    OccupiedBeds = g.Sum(r => r.BedSpaces.Count(b => b.Status == Status.Occupied))
                })
                .ToList();

            ViewBag.RoomsByFloor = roomsByFloor;

            // Get allocated students for this hostel
            var allocatedStudents = await _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                .Where(a => a.Status == Status.Active &&
                           a.Bed.Room.HostelId == id)
                .Select(a => new
                {
                    StudentName = a.Application.Student.FullName,
                    StudentNumber = a.Application.Student.StudentId_Number,
                    RoomNumber = a.Bed.Room.RoomNumber,
                    BedIdentifier = a.Bed.BedIdentifier,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate
                })
                .ToListAsync();

            ViewBag.AllocatedStudents = allocatedStudents;

            return View(hostel);
        }

        // GET: HostelManager/Create
        public async Task<IActionResult> Create()
        {
            // Get campuses for dropdown
            ViewBag.Campuses = await _context.Campuses.ToListAsync();

            // Get potential wardens (users with HostelManager role)
            var wardens = await _userManager.GetUsersInRoleAsync("HostelManager");
            ViewBag.Wardens = wardens;

            return View();
        }

        // POST: HostelManager/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Hostel hostel)
        {
            if (ModelState.IsValid)
            {
                hostel.Status = Status.Active;
                hostel.CreatedAt = DateTime.Now;
                hostel.CreatedBy = User.Identity.Name;

                _context.Add(hostel);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Hostel '{hostel.HostelName}' created successfully!";
                return RedirectToAction(nameof(GenerateRooms), new { id = hostel.HostelId });
            }

            // Reload dropdowns if validation fails
            ViewBag.Campuses = await _context.Campuses.ToListAsync();
            var wardens = await _userManager.GetUsersInRoleAsync("HostelManager");
            ViewBag.Wardens = wardens;

            return View(hostel);
        }

        // GET: HostelManager/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var hostel = await _context.Hostels.FindAsync(id);
            if (hostel == null)
            {
                return NotFound();
            }

            ViewBag.Campuses = await _context.Campuses.ToListAsync();
            var wardens = await _userManager.GetUsersInRoleAsync("HostelManager");
            ViewBag.Wardens = wardens;

            return View(hostel);
        }

        // POST: HostelManager/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Hostel hostel)
        {
            if (id != hostel.HostelId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    hostel.UpdatedAt = DateTime.Now;
                    hostel.UpdatedBy = User.Identity.Name;
                    _context.Update(hostel);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Hostel '{hostel.HostelName}' updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!HostelExists(hostel.HostelId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Details), new { id = hostel.HostelId });
            }

            ViewBag.Campuses = await _context.Campuses.ToListAsync();
            var wardens = await _userManager.GetUsersInRoleAsync("HostelManager");
            ViewBag.Wardens = wardens;

            return View(hostel);
        }

        // GET: HostelManager/GenerateRooms/5
        public async Task<IActionResult> GenerateRooms(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var hostel = await _context.Hostels
                .Include(h => h.Rooms)
                .FirstOrDefaultAsync(h => h.HostelId == id);

            if (hostel == null)
            {
                return NotFound();
            }

            ViewBag.ExistingRoomsCount = hostel.Rooms.Count;
            ViewBag.RoomsToGenerate = hostel.TotalRooms - hostel.Rooms.Count;

            return View(hostel);
        }

        // POST: HostelManager/GenerateRooms/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateRooms(int id)
        {
            var hostel = await _context.Hostels
                .Include(h => h.Rooms)
                .FirstOrDefaultAsync(h => h.HostelId == id);

            if (hostel == null)
            {
                return NotFound();
            }

            try
            {
                int existingRoomsCount = hostel.Rooms.Count;
                int roomsToGenerate = hostel.TotalRooms - existingRoomsCount;

                if (roomsToGenerate <= 0)
                {
                    TempData["WarningMessage"] = "All rooms have already been generated for this hostel.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Calculate number of floors needed
                int floorsNeeded = (int)Math.Ceiling((double)hostel.TotalRooms / hostel.RoomsPerFloor);
                int roomCounter = existingRoomsCount + 1;

                for (int floor = 1; floor <= floorsNeeded; floor++)
                {
                    int roomsOnThisFloor = Math.Min(hostel.RoomsPerFloor, hostel.TotalRooms - ((floor - 1) * hostel.RoomsPerFloor));

                    for (int roomNum = 1; roomNum <= roomsOnThisFloor && roomCounter <= hostel.TotalRooms; roomNum++)
                    {
                        // Skip if room already exists
                        string roomNumber = string.Format(hostel.RoomNumberingPattern, floor, roomNum.ToString("D2"));
                        if (hostel.Rooms.Any(r => r.RoomNumber == roomNumber))
                        {
                            continue;
                        }

                        var room = new Room
                        {
                            HostelId = hostel.HostelId,
                            RoomNumber = roomNumber,
                            Floor = floor,
                            RoomType = hostel.DefaultRoomType,
                            Capacity = hostel.DefaultCapacity,
                            Gender = hostel.Gender,
                            Status = Status.Available,
                            CreatedAt = DateTime.Now,
                            CreatedBy = User.Identity.Name
                        };

                        _context.Rooms.Add(room);
                        await _context.SaveChangesAsync(); // Save to get RoomId

                        // Auto-generate bed spaces if enabled
                        if (hostel.AutoGenerateBeds)
                        {
                            for (int bedNum = 1; bedNum <= hostel.DefaultCapacity; bedNum++)
                            {
                                // Use letters for beds (A, B, C...) or numbers
                                string bedIdentifier = hostel.DefaultCapacity <= 26
                                    ? ((char)('A' + bedNum - 1)).ToString()
                                    : bedNum.ToString();

                                var bed = new BedSpace
                                {
                                    RoomId = room.RoomId,
                                    BedIdentifier = bedIdentifier,
                                    Status = Status.Available,
                                    CreatedAt = DateTime.Now,
                                    CreatedBy = User.Identity.Name
                                };

                                _context.BedSpaces.Add(bed);
                            }
                        }

                        roomCounter++;
                    }
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"{roomsToGenerate} rooms and their bed spaces generated successfully!";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating rooms: {ex.Message}";
                return RedirectToAction(nameof(GenerateRooms), new { id });
            }
        }

        // GET: HostelManager/Applications
        public async Task<IActionResult> Applications(Status? status)
        {
            var query = _context.AccommodationApplications
                .Include(a => a.Student)
                    .ThenInclude(s => s.Programme)
                .Include(a => a.Period)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            var applications = await query
                .OrderByDescending(a => a.ApplicationDate)
                .ToListAsync();

            ViewBag.SelectedStatus = status;
            return View(applications);
        }

        // GET: HostelManager/ApplicationDetails/5
        public async Task<IActionResult> ApplicationDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var application = await _context.AccommodationApplications
                .Include(a => a.Student)
                    .ThenInclude(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                .Include(a => a.Student)
                    .ThenInclude(s => s.ProgrammeLevel)
                .Include(a => a.Period)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(r => r.Hostel)
                .Include(a => a.Allocation)
                    .ThenInclude(al => al.AllocatedBy)
                .FirstOrDefaultAsync(a => a.ApplicationId == id);

            if (application == null)
            {
                return NotFound();
            }

            // Check if student already has an allocation
            ViewBag.HasAllocation = application.Allocation != null;
            ViewBag.AllocationDetails = application.Allocation;

            return View(application);
        }

        // GET: HostelManager/AllocateRoom/5
        public async Task<IActionResult> AllocateRoom(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var application = await _context.AccommodationApplications
                .Include(a => a.Student)
                .Include(a => a.Period)
                .Include(a => a.Allocation)
                .FirstOrDefaultAsync(a => a.ApplicationId == id);

            if (application == null)
            {
                return NotFound();
            }

            // Check if already allocated
            if (application.Allocation != null)
            {
                TempData["ErrorMessage"] = "This application already has a bed allocation.";
                return RedirectToAction(nameof(ApplicationDetails), new { id });
            }

            // Get available hostels matching student's gender
            var hostels = await _context.Hostels
                .Where(h => h.Status == Status.Active &&
                           (h.Gender == application.Student.Gender || h.Gender == "Mixed"))
                .Include(h => h.Rooms)
                    .ThenInclude(r => r.BedSpaces)
                .ToListAsync();

            ViewBag.Hostels = hostels;
            ViewBag.Application = application;
            ViewBag.AllocationTypes = new List<string> { "individual", "bulk", "special" };

            // Set default end date based on period
            if (application.Period != null && application.Period.IsPermanentUntilGraduation)
            {
                ViewBag.DefaultIsGraduationBased = true;
            }

            return View(application);
        }

        // GET: HostelManager/GetAvailableRooms
        [HttpGet]
        public async Task<IActionResult> GetAvailableRooms(int hostelId)
        {
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == hostelId && r.Status == Status.Available)
                .Include(r => r.BedSpaces)
                .Select(r => new
                {
                    roomId = r.RoomId,
                    roomNumber = r.RoomNumber,
                    floor = r.Floor,
                    roomType = r.RoomType,
                    capacity = r.Capacity,
                    availableBeds = r.BedSpaces.Count(b => b.Status == Status.Available)
                })
                .Where(r => r.availableBeds > 0)
                .ToListAsync();

            return Json(rooms);
        }

        // GET: HostelManager/GetAvailableBeds
        [HttpGet]
        public async Task<IActionResult> GetAvailableBeds(int roomId)
        {
            var beds = await _context.BedSpaces
                .Where(b => b.RoomId == roomId && b.Status == Status.Available)
                .Select(b => new
                {
                    bedId = b.BedId,
                    bedIdentifier = b.BedIdentifier
                })
                .ToListAsync();

            return Json(beds);
        }

        // POST: HostelManager/AllocateRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AllocateRoom(int applicationId, int bedId, string allocationType,
            DateTime startDate, DateTime? endDate, bool isGraduationBased)
        {
            var application = await _context.AccommodationApplications
                .Include(a => a.Student)
                .Include(a => a.Period)
                .Include(a => a.Allocation)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (application == null)
            {
                return NotFound();
            }

            // Check if already allocated
            if (application.Allocation != null)
            {
                TempData["ErrorMessage"] = "This application already has a bed allocation.";
                return RedirectToAction(nameof(ApplicationDetails), new { id = applicationId });
            }

            var bed = await _context.BedSpaces
                .Include(b => b.Room)
                    .ThenInclude(r => r.Hostel)
                .FirstOrDefaultAsync(b => b.BedId == bedId);

            if (bed == null || bed.Status != Status.Available)
            {
                TempData["ErrorMessage"] = "Selected bed is not available.";
                return RedirectToAction(nameof(AllocateRoom), new { id = applicationId });
            }

            // Validate gender compatibility
            if (bed.Room.Gender != application.Student.Gender && bed.Room.Gender != "Mixed")
            {
                TempData["ErrorMessage"] = "Selected bed gender does not match student gender.";
                return RedirectToAction(nameof(AllocateRoom), new { id = applicationId });
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // If graduation-based, set end date to null
                if (isGraduationBased)
                {
                    endDate = null;
                }

                // Create allocation
                var allocation = new Allocation
                {
                    ApplicationId = applicationId,
                    BedId = bedId,
                    AllocationType = allocationType.ToLower(),
                    AllocatedById = currentUser.Id,
                    AllocationDate = DateTime.Now,
                    StartDate = startDate,
                    EndDate = endDate,
                    IsGraduationBased = isGraduationBased,
                    Status = Status.Active, // Active, not Pending
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity.Name
                };

                _context.Allocations.Add(allocation);

                // Update bed status to Occupied
                bed.Status = Status.Occupied;
                bed.UpdatedAt = DateTime.Now;
                bed.UpdatedBy = User.Identity.Name;

                // Update application status
                application.Status = Status.Approved;
                application.UpdatedAt = DateTime.Now;
                application.UpdatedBy = User.Identity.Name;

                // Update student's bed information (denormalized)
                application.Student.BedId = bedId;
                application.Student.BedAllocationEndDate = endDate;
                application.Student.HasAccommodationClearance = true;
                application.Student.UpdatedAt = DateTime.Now;
                application.Student.UpdatedBy = User.Identity.Name;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Room allocated successfully to {application.Student.FullName}! " +
                    $"Bed {bed.BedIdentifier} in Room {bed.Room.RoomNumber}, {bed.Room.Hostel.HostelName}";
                return RedirectToAction(nameof(ApplicationDetails), new { id = applicationId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error allocating room: {ex.Message}";
                return RedirectToAction(nameof(AllocateRoom), new { id = applicationId });
            }
        }

        // POST: HostelManager/DeallocateBed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeallocateBed(int allocationId, string reason)
        {
            var allocation = await _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                .Include(a => a.Bed)
                .FirstOrDefaultAsync(a => a.AllocationId == allocationId);

            if (allocation == null)
            {
                return NotFound();
            }

            try
            {
                var student = allocation.Application.Student;
                var bed = allocation.Bed;

                // Update allocation status
                allocation.Status = Status.Completed;
                allocation.EndDate = DateTime.Now;
                allocation.UpdatedAt = DateTime.Now;
                allocation.UpdatedBy = User.Identity.Name;

                // Update bed status
                bed.Status = Status.Available;
                bed.UpdatedAt = DateTime.Now;
                bed.UpdatedBy = User.Identity.Name;

                // Clear student's bed reference
                student.BedId = null;
                student.BedAllocationEndDate = null;
                student.HasAccommodationClearance = false;
                student.UpdatedAt = DateTime.Now;
                student.UpdatedBy = User.Identity.Name;

                // Update application notes
                allocation.Application.Notes += $"\n\n[{DateTime.Now:yyyy-MM-dd HH:mm}] Deallocation: {reason}";
                allocation.Application.UpdatedAt = DateTime.Now;
                allocation.Application.UpdatedBy = User.Identity.Name;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Bed deallocated successfully from {student.FullName}";
                return RedirectToAction(nameof(ApplicationDetails), new { id = allocation.ApplicationId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deallocating bed: {ex.Message}";
                return RedirectToAction(nameof(ApplicationDetails), new { id = allocation.ApplicationId });
            }
        }

        // POST: HostelManager/RejectApplication/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectApplication(int id, string notes)
        {
            var application = await _context.AccommodationApplications
                .Include(a => a.Allocation)
                .FirstOrDefaultAsync(a => a.ApplicationId == id);

            if (application == null)
            {
                return NotFound();
            }

            // Cannot reject if already allocated
            if (application.Allocation != null)
            {
                TempData["ErrorMessage"] = "Cannot reject application with active allocation.";
                return RedirectToAction(nameof(ApplicationDetails), new { id });
            }

            application.Status = Status.Rejected;
            application.Notes = string.IsNullOrEmpty(application.Notes)
                ? notes
                : application.Notes + $"\n\n[{DateTime.Now:yyyy-MM-dd HH:mm}] Rejection: {notes}";
            application.UpdatedAt = DateTime.Now;
            application.UpdatedBy = User.Identity.Name;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Application rejected.";
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }

        // Helper method
        private bool HostelExists(int id)
        {
            return _context.Hostels.Any(e => e.HostelId == id);
        }
    }
}