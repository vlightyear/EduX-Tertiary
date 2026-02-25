using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.StudentAccommodation;

namespace SIS.Controllers.AccommodationControllers
{
    [Authorize(Roles = "Admin")]
    public class HostelController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public HostelController(UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _signInManager = signInManager;
        }

        // GET: Hostel/Index
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Hostels));
        }

        // GET: Hostel/Hostels
        public async Task<IActionResult> Hostels()
        {
            var hostels = await _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Warden)
                .ToListAsync();

            // Get data for sidebar statistics and dropdowns
            ViewBag.TotalRooms = await _context.Rooms.CountAsync();
            ViewBag.TotalBeds = await _context.BedSpaces.CountAsync();
            ViewBag.Campuses = new SelectList(
                await _context.Campuses.Where(c => c.IsActive).ToListAsync(),
                "CampusId",
                "CampusName");

            // Get all users with the HostelManager role for the warden dropdown
            var wardenRoleId = await _context.Roles
               .Where(r => r.Name == "HostelManager")
               .Select(r => r.Id)
               .FirstOrDefaultAsync();

            var wardens = await _context.UserRoles
               .Where(ur => ur.RoleId == wardenRoleId)
               .Join(_context.Users,
                   ur => ur.UserId,
                   u => u.Id,
                   (ur, u) => new { u.Id, u.UserName })
               .ToListAsync();

            ViewBag.Wardens = new SelectList(wardens, "Id", "UserName");

            return View("~/Views/Accommodation/Hostels.cshtml", hostels);
        }

        // GET: Hostel/Details/5
        [Route("Hostel/Details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var hostel = await _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Warden)
                .FirstOrDefaultAsync(m => m.HostelId == id);

            if (hostel == null)
            {
                return NotFound();
            }

            // Get room statistics
            ViewBag.TotalRooms = await _context.Rooms.CountAsync(r => r.HostelId == id);
            ViewBag.AvailableRooms = await _context.Rooms.CountAsync(r => r.HostelId == id && r.Status == Status.Available);
            ViewBag.MaintenanceRooms = await _context.Rooms.CountAsync(r => r.HostelId == id && r.Status == Status.Maintenance);

            // Get bed statistics
            ViewBag.TotalBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id)
                .CountAsync();
            ViewBag.OccupiedBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id && b.Status == Status.Occupied)
                .CountAsync();
            ViewBag.AvailableBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id && b.Status == Status.Available)
                .CountAsync();

            // Get room type distribution
            var roomTypeDistribution = await _context.Rooms
                .Where(r => r.HostelId == id)
                .GroupBy(r => r.RoomType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.RoomTypeDistribution = roomTypeDistribution;

            // Get all rooms for this hostel
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == id)
                .OrderBy(r => r.Floor)
                .ThenBy(r => r.RoomNumber)
                .ToListAsync();
            ViewBag.Rooms = rooms;

            // Get floor distribution
            var floorDistribution = await _context.Rooms
                .Where(r => r.HostelId == id)
                .GroupBy(r => r.Floor)
                .Select(g => new { Floor = g.Key, Count = g.Count() })
                .OrderBy(f => f.Floor)
                .ToListAsync();
            ViewBag.FloorDistribution = floorDistribution;

            // Add room generation settings to ViewBag
            ViewBag.DefaultRoomType = hostel.DefaultRoomType;
            ViewBag.DefaultCapacity = hostel.DefaultCapacity;
            ViewBag.RoomsPerFloor = hostel.RoomsPerFloor;
            ViewBag.RoomNumberingPattern = hostel.RoomNumberingPattern;
            ViewBag.AutoGenerateBeds = hostel.AutoGenerateBeds;

            // Calculate number of floors
            ViewBag.TotalFloors = floorDistribution.Count > 0 ? floorDistribution.Max(f => f.Floor) + 1 : 0;

            return View("~/Views/Accommodation/HostelDetails.cshtml", hostel);
        }

        // GET: Hostel/GetHostel/5
        [HttpGet]
        public async Task<IActionResult> GetHostel(int id)
        {
            var hostel = await _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Warden)
                .FirstOrDefaultAsync(h => h.HostelId == id);

            if (hostel == null)
            {
                return NotFound();
            }

            // Get room statistics for this hostel
            int totalRooms = await _context.Rooms.CountAsync(r => r.HostelId == id);
            int availableRooms = await _context.Rooms.CountAsync(r => r.HostelId == id && r.Status == Status.Available);
            int totalBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id)
                .CountAsync();
            int occupiedBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id && b.Status == Status.Occupied)
                .CountAsync();

            var hostelDto = new
            {
                id = hostel.HostelId,
                name = hostel.HostelName,
                gender = hostel.Gender,
                campusId = hostel.CampusId,
                wardenId = hostel.WardenId,
                totalRooms = hostel.TotalRooms,
                totalCapacity = hostel.TotalCapacity,
                status = hostel.Status,
                description = hostel.Description,
                // Include the new room generation properties
                defaultRoomType = hostel.DefaultRoomType,
                defaultCapacity = hostel.DefaultCapacity,
                roomsPerFloor = hostel.RoomsPerFloor,
                roomNumberingPattern = hostel.RoomNumberingPattern,
                autoGenerateBeds = hostel.AutoGenerateBeds,
                campusName = hostel.Campus?.CampusName,
                wardenName = hostel.Warden?.UserName,
                statistics = new
                {
                    totalRooms,
                    availableRooms,
                    totalBeds,
                    occupiedBeds,
                    occupancyRate = totalBeds > 0 ? (int)((double)occupiedBeds / totalBeds * 100) : 0
                }
            };

            return Json(hostelDto);
        }

        // POST: Hostel/CreateHostel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateHostel(Hostel hostel, List<BedDistributionDto> bedDistributions)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () => {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Set audit fields
                    hostel.CreatedBy = User.Identity?.Name ?? "System";
                    hostel.CreatedAt = DateTime.Now;

                    // Set default values if not provided
                    if (string.IsNullOrEmpty(hostel.DefaultRoomType))
                        hostel.DefaultRoomType = "Single";

                    if (hostel.DefaultCapacity <= 0)
                        hostel.DefaultCapacity = 1;

                    if (hostel.RoomsPerFloor <= 0)
                        hostel.RoomsPerFloor = 10;

                    if (string.IsNullOrEmpty(hostel.RoomNumberingPattern))
                        hostel.RoomNumberingPattern = "F{0}R{1}";

                    // Add and save the hostel
                    _context.Hostels.Add(hostel);
                    await _context.SaveChangesAsync();

                    // Generate rooms for the hostel with bed distributions
                    await GenerateRoomsForHostel(hostel, bedDistributions);

                    // Calculate the total capacity from room capacities
                    int totalCapacity = await _context.Rooms
                        .Where(r => r.HostelId == hostel.HostelId)
                        .SumAsync(r => r.Capacity);

                    // Update the hostel with the calculated capacity
                    hostel.TotalCapacity = totalCapacity;
                    await _context.SaveChangesAsync();

                    // Commit the transaction
                    await transaction.CommitAsync();

                    TempData["Success"] = $"Hostel created successfully with {hostel.TotalRooms} rooms.";
                    return RedirectToAction(nameof(Hostels));
                }
                catch (Exception ex)
                {
                    // Rollback on error
                    await transaction.RollbackAsync();
                    TempData["Error"] = $"Error creating hostel: {ex.Message}";
                    throw;
                }
            });

            // If we got this far, something failed, redisplay form
            await PrepareViewBags(hostel.CampusId);
            return RedirectToAction(nameof(Hostels));
        }

        // POST: Hostel/UpdateHostel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateHostel(int id, Hostel hostel)
        {
            if (id != hostel.HostelId)
            {
                TempData["Error"] = "Invalid hostel ID.";
                return RedirectToAction(nameof(Hostels));
            }

            var existingHostel = await _context.Hostels.FindAsync(id);
            if (existingHostel == null)
            {
                TempData["Error"] = "Hostel not found.";
                return RedirectToAction(nameof(Hostels));
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    int oldTotalRooms = existingHostel.TotalRooms;

                    // Update hostel properties
                    existingHostel.HostelName = hostel.HostelName;
                    existingHostel.Gender = hostel.Gender;
                    existingHostel.CampusId = hostel.CampusId;
                    existingHostel.WardenId = hostel.WardenId;
                    existingHostel.Status = hostel.Status;
                    existingHostel.Description = hostel.Description;
                    existingHostel.TotalRooms = hostel.TotalRooms;
                    existingHostel.DefaultRoomType = hostel.DefaultRoomType;
                    existingHostel.DefaultCapacity = hostel.DefaultCapacity;
                    existingHostel.RoomsPerFloor = hostel.RoomsPerFloor;
                    existingHostel.RoomNumberingPattern = hostel.RoomNumberingPattern;
                    existingHostel.AutoGenerateBeds = hostel.AutoGenerateBeds;
                    existingHostel.UpdatedBy = User.Identity?.Name ?? "System";
                    existingHostel.UpdatedAt = DateTime.Now;

                    // Handle room count changes
                    if (hostel.TotalRooms != oldTotalRooms)
                    {
                        if (hostel.TotalRooms > oldTotalRooms)
                        {
                            // Add more rooms
                            await AddRoomsToHostel(existingHostel, hostel.TotalRooms - oldTotalRooms, null);
                        }
                        else
                        {
                            // Remove rooms
                            int roomsToRemove = oldTotalRooms - hostel.TotalRooms;
                            if (await CanRemoveRooms(id, roomsToRemove))
                            {
                                await RemoveRoomsFromHostel(id, roomsToRemove);
                            }
                            else
                            {
                                TempData["Error"] = "Cannot reduce room count. Some rooms have occupied beds.";
                                await transaction.RollbackAsync();
                                return;
                            }
                        }
                    }

                    // Recalculate total capacity
                    existingHostel.TotalCapacity = await _context.Rooms
                        .Where(r => r.HostelId == id)
                        .SumAsync(r => r.Capacity);

                    _context.Update(existingHostel);
                    await _context.SaveChangesAsync();

                    // Commit the transaction
                    await transaction.CommitAsync();

                    TempData["Success"] = "Hostel updated successfully.";
                }
                catch (Exception ex)
                {
                    // Rollback on error
                    await transaction.RollbackAsync();
                    TempData["Error"] = $"Error updating hostel: {ex.Message}";
                    throw;
                }
            });

            return RedirectToAction(nameof(Hostels));
        }

        // POST: Hostel/DeleteHostel/5
        [HttpPost, ActionName("DeleteHostel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hostel = await _context.Hostels.FindAsync(id);
            if (hostel == null)
            {
                return NotFound();
            }

            // Check if hostel has rooms
            var hasRooms = await _context.Rooms.AnyAsync(r => r.HostelId == id);
            if (hasRooms)
            {
                TempData["Error"] = "Cannot delete hostel with existing rooms. Please remove all rooms first.";
                return RedirectToAction(nameof(Hostels));
            }

            try
            {
                _context.Hostels.Remove(hostel);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Hostel deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting hostel: {ex.Message}";
            }

            return RedirectToAction(nameof(Hostels));
        }

        #region Private Helper Methods

        private async Task PrepareViewBags(int? campusId = null)
        {
            ViewBag.Campuses = new SelectList(
                await _context.Campuses.Where(c => c.IsActive).ToListAsync(),
                "CampusId",
                "CampusName",
                campusId);

            // Get all users with the HostelManager role for the warden dropdown
            var wardenRoleId = await _context.Roles
               .Where(r => r.Name == "HostelManager")
               .Select(r => r.Id)
               .FirstOrDefaultAsync();

            var wardens = await _context.UserRoles
               .Where(ur => ur.RoleId == wardenRoleId)
               .Join(_context.Users,
                   ur => ur.UserId,
                   u => u.Id,
                   (ur, u) => new { u.Id, u.UserName })
               .ToListAsync();

            ViewBag.Wardens = new SelectList(wardens, "Id", "UserName");
        }

        private async Task AddRoomsToHostel(Hostel hostel, int roomsToAdd, List<BedDistributionDto> bedDistributions)
        {
            // Get the current highest room number to continue from
            var existingRoomCount = await _context.Rooms.CountAsync(r => r.HostelId == hostel.HostelId);

            // Calculate which floor and room index to start from
            int currentFloor = existingRoomCount / hostel.RoomsPerFloor;
            int currentRoomIndex = existingRoomCount % hostel.RoomsPerFloor;

            List<Room> roomsToCreate = new List<Room>();
            List<BedSpace> bedSpacesToCreate = new List<BedSpace>();

            int roomsAdded = 0;

            while (roomsAdded < roomsToAdd)
            {
                // Check if we need to move to the next floor
                if (currentRoomIndex >= hostel.RoomsPerFloor)
                {
                    currentFloor++;
                    currentRoomIndex = 0;
                }

                string roomNumber = GenerateRoomNumber(hostel.RoomNumberingPattern, currentFloor, currentRoomIndex);

                var room = new Room
                {
                    HostelId = hostel.HostelId,
                    RoomNumber = roomNumber,
                    Floor = currentFloor,
                    RoomType = hostel.DefaultRoomType,
                    Capacity = hostel.DefaultCapacity,
                    Gender = hostel.Gender,
                    Status = Status.Available,
                    CreatedBy = hostel.UpdatedBy ?? hostel.CreatedBy,
                    CreatedAt = DateTime.Now
                };

                roomsToCreate.Add(room);

                currentRoomIndex++;
                roomsAdded++;
            }

            // Add all rooms to the database
            await _context.Rooms.AddRangeAsync(roomsToCreate);
            await _context.SaveChangesAsync();

            // If auto-generate beds is enabled, create bed spaces for each room
            if (hostel.AutoGenerateBeds)
            {
                foreach (var room in roomsToCreate)
                {
                    for (int i = 0; i < room.Capacity; i++)
                    {
                        var bedIdentifier = ConvertToAlphabeticIdentifier(i + 1);

                        // Use default values if no distribution specified
                        int bedYear = 0;
                        int bedSemester = 0;

                        // If bed distributions are provided, use them
                        if (bedDistributions != null && i < bedDistributions.Count)
                        {
                            bedYear = bedDistributions[i].Year;
                            bedSemester = bedDistributions[i].Semester;
                        }

                        var bedSpace = new BedSpace
                        {
                            RoomId = room.RoomId,
                            BedIdentifier = bedIdentifier,
                            CurrentStudentYear = bedYear,
                            CurrentStudentSemister = bedSemester,
                            Status = Status.Available,
                            CreatedBy = hostel.UpdatedBy ?? hostel.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        bedSpacesToCreate.Add(bedSpace);
                    }
                }

                // Add all bed spaces to the database
                await _context.BedSpaces.AddRangeAsync(bedSpacesToCreate);
                await _context.SaveChangesAsync();
            }
        }

        private async Task RemoveRoomsFromHostel(int hostelId, int roomsToRemove)
        {
            // Get rooms ordered by most recently added first (we'll remove newest rooms first)
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == hostelId)
                .Include(r => r.BedSpaces)
                .Include(r => r.Resources)
                .OrderByDescending(r => r.RoomId)
                .Take(roomsToRemove)
                .ToListAsync();

            foreach (var room in rooms)
            {
                // Remove associated bed spaces
                _context.BedSpaces.RemoveRange(room.BedSpaces);

                // Remove associated resources
                _context.RoomResources.RemoveRange(room.Resources);
            }

            // Save changes so far
            await _context.SaveChangesAsync();

            // Now remove the rooms
            _context.Rooms.RemoveRange(rooms);
            await _context.SaveChangesAsync();
        }

        private async Task<bool> CanRemoveRooms(int hostelId, int roomsToRemove)
        {
            // Get rooms ordered by most recently added first (we'll remove newest rooms first)
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == hostelId)
                .Include(r => r.BedSpaces)
                .OrderByDescending(r => r.RoomId)
                .Take(roomsToRemove)
                .ToListAsync();

            // Check if any of the rooms to be removed have occupied beds
            return !rooms.Any(r => r.BedSpaces.Any(b => b.Status == Status.Occupied));
        }

        private async Task GenerateRoomsForHostel(Hostel hostel, List<BedDistributionDto> bedDistributions)
        {
            // Calculate how many rooms should be on each floor
            int totalFloors = (int)Math.Ceiling((double)hostel.TotalRooms / hostel.RoomsPerFloor);
            List<Room> roomsToAdd = new List<Room>();
            List<BedSpace> bedSpacesToAdd = new List<BedSpace>();

            int roomCounter = 0;

            // Create rooms for each floor
            for (int floor = 0; floor < totalFloors && roomCounter < hostel.TotalRooms; floor++)
            {
                int roomsOnThisFloor = Math.Min(hostel.RoomsPerFloor, hostel.TotalRooms - roomCounter);

                for (int roomIndex = 0; roomIndex < roomsOnThisFloor; roomIndex++)
                {
                    string roomNumber = GenerateRoomNumber(hostel.RoomNumberingPattern, floor, roomIndex);

                    // Create new room
                    var room = new Room
                    {
                        HostelId = hostel.HostelId,
                        RoomNumber = roomNumber,
                        Floor = floor,
                        RoomType = hostel.DefaultRoomType,
                        Capacity = hostel.DefaultCapacity,
                        Gender = hostel.Gender,
                        Status = Status.Available,
                        CreatedBy = hostel.CreatedBy,
                        CreatedAt = DateTime.Now
                    };

                    roomsToAdd.Add(room);
                    roomCounter++;
                }
            }

            // Add all rooms to the database
            await _context.Rooms.AddRangeAsync(roomsToAdd);
            await _context.SaveChangesAsync();

            // If auto-generate beds is enabled, create bed spaces for each room
            if (hostel.AutoGenerateBeds)
            {
                foreach (var room in roomsToAdd)
                {
                    for (int i = 0; i < room.Capacity; i++)
                    {
                        var bedIdentifier = ConvertToAlphabeticIdentifier(i + 1);

                        // Use default values if no distribution specified
                        int bedYear = 0;
                        int bedSemester = 0;

                        // If bed distributions are provided, use them
                        if (bedDistributions != null && i < bedDistributions.Count)
                        {
                            bedYear = bedDistributions[i].Year;
                            bedSemester = bedDistributions[i].Semester;
                        }

                        var bedSpace = new BedSpace
                        {
                            RoomId = room.RoomId,
                            BedIdentifier = bedIdentifier,
                            CurrentStudentYear = bedYear,
                            CurrentStudentSemister = bedSemester,
                            Status = Status.Available,
                            CreatedBy = hostel.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        bedSpacesToAdd.Add(bedSpace);
                    }
                }

                // Add all bed spaces to the database
                await _context.BedSpaces.AddRangeAsync(bedSpacesToAdd);
                await _context.SaveChangesAsync();
            }
        }

        private string GenerateRoomNumber(string pattern, int floor, int roomIndex)
        {
            // Format: The pattern uses {0} for floor and {1} for room index
            // For example: "F{0}R{1}" with floor=1, roomIndex=2 becomes "F1R2"

            // Add leading zeros for room index if needed (e.g., 1 becomes 01)
            string roomIndexStr = (roomIndex + 1).ToString().PadLeft(2, '0');

            // Replace placeholders in the pattern
            return string.Format(pattern, floor + 1, roomIndexStr);
        }

        private string ConvertToAlphabeticIdentifier(int number)
        {
            if (number <= 26)
            {
                // For numbers 1-26, return A-Z
                return ((char)(64 + number)).ToString();
            }
            else
            {
                // For numbers > 26, return AA, AB, etc.
                int firstChar = (number - 1) / 26;
                int secondChar = (number - 1) % 26 + 1;

                return $"{(char)(64 + firstChar)}{(char)(64 + secondChar)}";
            }
        }

        private bool HostelExists(int id)
        {
            return _context.Hostels.Any(e => e.HostelId == id);
        }

        #endregion
    }

    // DTO for bed distribution
    public class BedDistributionDto
    {
        public int BedIndex { get; set; }
        public int Year { get; set; }
        public int Semester { get; set; }
    }
}