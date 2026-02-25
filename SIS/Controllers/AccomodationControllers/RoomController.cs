using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.StudentAccommodation;
using SIS.Models.StudentApplication;

namespace SIS.Controllers.AccommodationControllers
{
    [Authorize(Roles = "Admin")]
    public class RoomController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public RoomController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _signInManager = signInManager;
        }

        // GET: Room/Index
        public async Task<IActionResult> Index()
        {
            await PopulateRoomsViewBag();
            return View("~/Views/Accommodation/RoomManagement.cshtml");
        }

        // GET: Room/GetHostelsByCampus/5
        [HttpGet]
        [Route("Room/GetHostelsByCampus/{campusId}")]
        public async Task<IActionResult> GetHostelsByCampus(int campusId)
        {
            var hostels = await _context.Hostels
                .Where(h => h.CampusId == campusId && h.Status == Status.Active)
                .Select(h => new
                {
                    id = h.HostelId,
                    name = h.HostelName
                })
                .OrderBy(h => h.name)
                .ToListAsync();

            return Json(hostels);
        }

        // GET: Room/GetRoomsByHostel/5
        [HttpGet]
        [Route("Room/GetRoomsByHostel/{hostelId}")]
        public async Task<IActionResult> GetRoomsByHostel(int hostelId)
        {
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == hostelId)
                .Include(r => r.BedSpaces)
                .Include(r => r.Resources)
                .Select(r => new
                {
                    id = r.RoomId,
                    roomNumber = r.RoomNumber,
                    floor = r.Floor,
                    roomType = r.RoomType,
                    capacity = r.Capacity,
                    gender = r.Gender,
                    status = r.Status.ToString(),
                    bedCount = r.BedSpaces.Count,
                    occupiedBeds = r.BedSpaces.Count(b => b.Status == Status.Occupied),
                    resourceCount = r.Resources.Count,
                    isSpecialReservation = r.IsSpecialReservation
                })
                .OrderBy(r => r.floor)
                .ThenBy(r => r.roomNumber)
                .ToListAsync();

            return Json(rooms);
        }

        // GET: Room/GetRoom/5
        [HttpGet]
        public async Task<IActionResult> GetRoom(int id)
        {
            var room = await _context.Rooms
                .AsNoTracking()
                .Include(r => r.Hostel)
                    .ThenInclude(h => h.Campus)
                .Include(r => r.BedSpaces)
                .Include(r => r.Resources)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
            {
                return NotFound();
            }

            var roomDto = new
            {
                id = room.RoomId,
                hostelId = room.HostelId,
                hostelName = room.Hostel?.HostelName,
                campusName = room.Hostel?.Campus?.CampusName,
                roomNumber = room.RoomNumber,
                floor = room.Floor,
                roomType = room.RoomType,
                capacity = room.Capacity,
                gender = room.Gender,
                status = room.Status.ToString(),
                isSpecialReservation = room.IsSpecialReservation
            };

            return Json(roomDto);
        }

        // GET: Room/GetRoomDetails/5
        [HttpGet]
        public async Task<IActionResult> GetRoomDetails(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Hostel)
                    .ThenInclude(h => h.Campus)
                .Include(r => r.BedSpaces)
                .Include(r => r.Resources)
                    .ThenInclude(res => res.ResourceType)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
            {
                return NotFound();
            }

            var bedIds = room.BedSpaces.Select(b => b.BedId).ToList();
            var activeAllocations = await _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                .Where(a => bedIds.Contains(a.BedId) && a.Status == Status.Active)
                .ToDictionaryAsync(a => a.BedId);

            var roomDetails = new
            {
                roomId = room.RoomId,
                roomNumber = room.RoomNumber,
                floor = room.Floor,
                roomType = room.RoomType,
                capacity = room.Capacity,
                gender = room.Gender,
                status = room.Status.ToString(),
                hostelName = room.Hostel?.HostelName,
                campusName = room.Hostel?.Campus?.CampusName,
                isSpecialReservation = room.IsSpecialReservation,
                beds = room.BedSpaces.Select(b => new
                {
                    bedId = b.BedId,
                    bedIdentifier = b.BedIdentifier,
                    status = b.Status.ToString(),
                    isSpecialReservation = b.IsSpecialReservation,
                    currentStudentYear = b.CurrentStudentYear,
                    currentStudentSemister = b.CurrentStudentSemister,
                    studentId = activeAllocations.ContainsKey(b.BedId)
                        ? activeAllocations[b.BedId].Application.StudentId
                        : (int?)null,
                    studentName = activeAllocations.ContainsKey(b.BedId)
                        ? activeAllocations[b.BedId].Application.Student.FullName
                        : null,
                    studentNumber = activeAllocations.ContainsKey(b.BedId)
                        ? activeAllocations[b.BedId].Application.Student.StudentId_Number
                        : null,
                    allocationEndDate = activeAllocations.ContainsKey(b.BedId)
                        ? activeAllocations[b.BedId].EndDate
                        : null
                }).ToList(),
                resources = room.Resources.Select(r => new
                {
                    resourceId = r.ResourceId,
                    resourceTypeId = r.ResourceTypeId,
                    name = r.ResourceType?.Name ?? "Unknown",
                    description = r.ResourceType?.Description ?? "",
                    quantity = r.Quantity,
                    status = r.Status.ToString()
                }).ToList()
            };

            return Json(roomDetails);
        }

        // GET: Room/GetOccupiedBeds
        [HttpGet]
        public async Task<IActionResult> GetOccupiedBeds(int? hostelId = null)
        {
            var query = _context.Allocations
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                .Where(a => a.Status == Status.Active);

            if (hostelId.HasValue)
            {
                query = query.Where(a => a.Bed.Room.HostelId == hostelId.Value);
            }

            var occupiedBeds = await query
                .Select(a => new
                {
                    bedId = a.BedId,
                    bedIdentifier = a.Bed.BedIdentifier,
                    roomNumber = a.Bed.Room.RoomNumber,
                    hostelName = a.Bed.Room.Hostel.HostelName,
                    campusName = a.Bed.Room.Hostel.Campus.CampusName,
                    studentName = a.Application.Student.FullName,
                    studentNumber = a.Application.Student.StudentId_Number,
                    allocationEndDate = a.EndDate,
                    email = a.Application.Student.Email,
                    phone = a.Application.Student.Phone,
                    floor = a.Bed.Room.Floor
                })
                .OrderBy(b => b.hostelName)
                .ThenBy(b => b.roomNumber)
                .ThenBy(b => b.bedIdentifier)
                .ToListAsync();

            return Json(occupiedBeds);
        }

        // POST: Room/CreateRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRoom([FromForm] Room room)
        {
            try
            {
                if (room == null)
                {
                    return BadRequest("Invalid room data");
                }

                room.CreatedBy = User.Identity?.Name ?? "System";
                room.CreatedAt = DateTime.Now;
                room.UpdatedBy = User.Identity?.Name ?? "System";
                room.UpdatedAt = DateTime.Now;

                _context.Rooms.Add(room);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, roomId = room.RoomId });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error creating room: {ex.Message}");
            }
        }

        // POST: Room/UpdateRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoom([FromForm] Room room)
        {
            try
            {
                if (room == null || room.RoomId == 0)
                {
                    return BadRequest("Invalid room data");
                }

                var existingRoom = await _context.Rooms.FindAsync(room.RoomId);
                if (existingRoom == null)
                {
                    return NotFound("Room not found");
                }

                existingRoom.HostelId = room.HostelId;
                existingRoom.RoomNumber = room.RoomNumber;
                existingRoom.Floor = room.Floor;
                existingRoom.RoomType = room.RoomType;
                existingRoom.Capacity = room.Capacity;
                existingRoom.Gender = room.Gender;
                existingRoom.Status = room.Status;
                existingRoom.IsSpecialReservation = room.IsSpecialReservation;
                existingRoom.UpdatedBy = User.Identity?.Name ?? "System";
                existingRoom.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating room: {ex.Message}");
            }
        }

        // POST: Room/DeleteRoom/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            try
            {
                var room = await _context.Rooms
                    .Include(r => r.BedSpaces)
                    .Include(r => r.Resources)
                    .FirstOrDefaultAsync(r => r.RoomId == id);

                if (room == null)
                {
                    return NotFound("Room not found");
                }

                // Check if any beds are occupied
                var hasOccupiedBeds = room.BedSpaces.Any(b => b.Status == Status.Occupied);
                if (hasOccupiedBeds)
                {
                    return BadRequest("Cannot delete room with occupied beds");
                }

                // Delete associated resources
                _context.RoomResources.RemoveRange(room.Resources);

                // Delete associated bed spaces
                _context.BedSpaces.RemoveRange(room.BedSpaces);

                // Delete the room
                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error deleting room: {ex.Message}");
            }
        }

        // POST: Room/AddBedSpace
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBedSpace(int RoomId, string BedIdentifier, int Status, bool IsSpecialReservation, int CurrentStudentYear, int CurrentStudentSemister)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BedIdentifier))
                {
                    return BadRequest("Bed identifier is required");
                }

                var room = await _context.Rooms.FindAsync(RoomId);
                if (room == null)
                {
                    return NotFound("Room not found");
                }

                var bedSpace = new BedSpace
                {
                    RoomId = RoomId,
                    BedIdentifier = BedIdentifier,
                    Status = (Status)Status,
                    IsSpecialReservation = IsSpecialReservation,
                    CurrentStudentYear = CurrentStudentYear,
                    CurrentStudentSemister = CurrentStudentSemister,
                    CreatedBy = User.Identity?.Name ?? "System",
                    CreatedAt = DateTime.Now,
                    UpdatedBy = User.Identity?.Name ?? "System",
                    UpdatedAt = DateTime.Now
                };

                _context.BedSpaces.Add(bedSpace);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, bedId = bedSpace.BedId });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error adding bed space: {ex.Message}");
            }
        }

        // POST: Room/EditBedSpace/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBedSpace(int BedId, string BedIdentifier, int Status, bool IsSpecialReservation, int CurrentStudentYear, int CurrentStudentSemister)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BedIdentifier))
                {
                    return BadRequest("Bed identifier is required");
                }

                var bedSpace = await _context.BedSpaces.FindAsync(BedId);
                if (bedSpace == null)
                {
                    return NotFound("Bed space not found");
                }

                bedSpace.BedIdentifier = BedIdentifier;
                bedSpace.Status = (Status)Status;
                bedSpace.IsSpecialReservation = IsSpecialReservation;
                bedSpace.CurrentStudentYear = CurrentStudentYear;
                bedSpace.CurrentStudentSemister = CurrentStudentSemister;
                bedSpace.UpdatedBy = User.Identity?.Name ?? "System";
                bedSpace.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating bed space: {ex.Message}");
            }
        }

        // POST: Room/AddBulkBedSpaces
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBulkBedSpaces([FromBody] BulkBedSpaceRequest request)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(request.RoomId);
                if (room == null)
                {
                    return NotFound("Room not found");
                }

                if (request.BedSpaces == null || !request.BedSpaces.Any())
                {
                    return BadRequest("No bed spaces provided");
                }

                var bedSpaces = new List<BedSpace>();

                foreach (var bed in request.BedSpaces)
                {
                    if (string.IsNullOrWhiteSpace(bed.BedIdentifier))
                    {
                        continue;
                    }

                    var bedSpace = new BedSpace
                    {
                        RoomId = request.RoomId,
                        BedIdentifier = bed.BedIdentifier,
                        Status = bed.Status,
                        IsSpecialReservation = bed.IsSpecialReservation,
                        CurrentStudentYear = bed.CurrentStudentYear,
                        CurrentStudentSemister = bed.CurrentStudentSemister,
                        CreatedBy = User.Identity?.Name ?? "System",
                        CreatedAt = DateTime.Now,
                        UpdatedBy = User.Identity?.Name ?? "System",
                        UpdatedAt = DateTime.Now
                    };
                    bedSpaces.Add(bedSpace);
                }

                _context.BedSpaces.AddRange(bedSpaces);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    bedSpacesAdded = bedSpaces.Count
                });
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                return BadRequest($"Database error: {innerMessage}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error adding bulk bed spaces: {ex.Message}");
            }
        }

        // POST: Room/DeleteBedSpace/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBedSpace(int id)
        {
            try
            {
                var bedSpace = await _context.BedSpaces.FindAsync(id);
                if (bedSpace == null)
                {
                    return NotFound("Bed space not found");
                }

                // Check if bed is occupied
                if (bedSpace.Status == Status.Occupied)
                {
                    return BadRequest("Cannot delete an occupied bed space");
                }

                _context.BedSpaces.Remove(bedSpace);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error deleting bed space: {ex.Message}");
            }
        }

        // POST: Room/AddRoomResource
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRoomResource(int RoomId, int ResourceTypeId, int Quantity, int Status)
        {
            try
            {
                var room = await _context.Rooms
                    .Include(r => r.Resources)
                    .FirstOrDefaultAsync(r => r.RoomId == RoomId);

                if (room == null)
                {
                    return NotFound("Room not found");
                }

                var resourceType = await _context.ResourceTypes.FindAsync(ResourceTypeId);
                if (resourceType == null)
                {
                    return NotFound("Resource type not found");
                }

                if (Quantity <= 0)
                {
                    return BadRequest("Quantity must be greater than 0");
                }

                // Check if resource already exists
                var existingResource = room.Resources
                    .Any(r => r.ResourceTypeId == ResourceTypeId);

                if (existingResource)
                {
                    return BadRequest("This resource type already exists in the room");
                }

                var roomResource = new RoomResource
                {
                    RoomId = RoomId,
                    ResourceTypeId = ResourceTypeId,
                    Quantity = Quantity,
                    Status = (Status)Status,
                    CreatedBy = User.Identity?.Name ?? "System",
                    CreatedAt = DateTime.Now,
                    UpdatedBy = User.Identity?.Name ?? "System",
                    UpdatedAt = DateTime.Now
                };

                _context.RoomResources.Add(roomResource);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, resourceId = roomResource.ResourceId });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error adding resource: {ex.Message}");
            }
        }

        // POST: Room/UpdateRoomResource
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoomResource(int ResourceId, int Quantity, int Status)
        {
            try
            {
                if (Quantity <= 0)
                {
                    return BadRequest("Quantity must be greater than 0");
                }

                var resource = await _context.RoomResources.FindAsync(ResourceId);
                if (resource == null)
                {
                    return NotFound("Resource not found");
                }

                resource.Quantity = Quantity;
                resource.Status = (Status)Status;
                resource.UpdatedBy = User.Identity?.Name ?? "System";
                resource.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating resource: {ex.Message}");
            }
        }

        // POST: Room/AddBulkResources
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBulkResources([FromBody] BulkResourceRequest request)
        {
            try
            {
                var room = await _context.Rooms
                    .Include(r => r.Resources)
                    .FirstOrDefaultAsync(r => r.RoomId == request.RoomId);

                if (room == null)
                {
                    return NotFound("Room not found");
                }

                if (request.Resources == null || !request.Resources.Any())
                {
                    return BadRequest("No resources provided");
                }

                var resources = new List<RoomResource>();

                foreach (var resource in request.Resources)
                {
                    var resourceType = await _context.ResourceTypes.FindAsync(resource.ResourceTypeId);
                    if (resourceType == null)
                    {
                        continue;
                    }

                    var existingResource = room.Resources
                        .Any(r => r.ResourceTypeId == resource.ResourceTypeId);

                    if (existingResource)
                    {
                        continue;
                    }

                    var roomResource = new RoomResource
                    {
                        RoomId = request.RoomId,
                        ResourceTypeId = resource.ResourceTypeId,
                        Quantity = resource.Quantity,
                        Status = resource.Status,
                        CreatedBy = User.Identity?.Name ?? "System",
                        CreatedAt = DateTime.Now,
                        UpdatedBy = User.Identity?.Name ?? "System",
                        UpdatedAt = DateTime.Now
                    };
                    resources.Add(roomResource);
                }

                _context.RoomResources.AddRange(resources);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    resourcesAdded = resources.Count
                });
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                return BadRequest($"Database error: {innerMessage}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error adding bulk resources: {ex.Message}");
            }
        }

        // POST: Room/ApplyResourcesToRooms
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyResourcesToRooms([FromBody] ApplyResourcesToRoomsRequest request)
        {
            try
            {
                if (request.RoomIds == null || !request.RoomIds.Any())
                {
                    return BadRequest("No rooms selected");
                }

                if (request.Resources == null || !request.Resources.Any())
                {
                    return BadRequest("No resources provided");
                }

                var resources = new List<RoomResource>();

                foreach (var roomId in request.RoomIds)
                {
                    var room = await _context.Rooms
                        .Include(r => r.Resources)
                        .FirstOrDefaultAsync(r => r.RoomId == roomId);

                    if (room == null) continue;

                    foreach (var resource in request.Resources)
                    {
                        var resourceType = await _context.ResourceTypes.FindAsync(resource.ResourceTypeId);
                        if (resourceType == null)
                        {
                            continue;
                        }

                        var existingResource = room.Resources
                            .Any(r => r.ResourceTypeId == resource.ResourceTypeId);

                        if (existingResource)
                        {
                            continue;
                        }

                        var roomResource = new RoomResource
                        {
                            Room = room,
                            ResourceType = resourceType,
                            RoomId = roomId,
                            ResourceTypeId = resource.ResourceTypeId,
                            Quantity = resource.Quantity,
                            Status = resource.Status,
                            CreatedBy = User.Identity?.Name ?? "System",
                            CreatedAt = DateTime.Now,
                            UpdatedBy = User.Identity?.Name ?? "System",
                            UpdatedAt = DateTime.Now
                        };
                        resources.Add(roomResource);
                    }
                }

                _context.RoomResources.AddRange(resources);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    roomsAffected = request.RoomIds.Count,
                    resourcesAdded = resources.Count
                });
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                return BadRequest($"Database error: {innerMessage}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error applying resources: {ex.Message}");
            }
        }

        // POST: Room/DeleteRoomResource/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoomResource(int id)
        {
            try
            {
                var resource = await _context.RoomResources.FindAsync(id);
                if (resource == null)
                {
                    return NotFound("Resource not found");
                }

                _context.RoomResources.Remove(resource);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error deleting resource: {ex.Message}");
            }
        }

        // Helper method to populate ViewBag
        private async Task PopulateRoomsViewBag()
        {
            ViewBag.Campuses = await _context.Campuses
                .Where(c => c.IsActive)
                .OrderBy(c => c.CampusName)
                .ToListAsync();

            ViewBag.Hostels = await _context.Hostels
                .Include(h => h.Campus)
                .Where(h => h.Status == Status.Active)
                .OrderBy(h => h.HostelName)
                .ToListAsync();

            ViewBag.ResourceTypes = await _context.ResourceTypes
                .OrderBy(rt => rt.Name)
                .ToListAsync();

            ViewBag.RoomTypes = new List<SelectListItem>
            {
                new SelectListItem { Text = "Single", Value = "Single" },
                new SelectListItem { Text = "Double", Value = "Double" },
                new SelectListItem { Text = "Triple", Value = "Triple" },
                new SelectListItem { Text = "Quad", Value = "Quad" },
                new SelectListItem { Text = "Suite", Value = "Suite" }
            };

            ViewBag.GenderOptions = new List<SelectListItem>
            {
                new SelectListItem { Text = "Male", Value = "Male" },
                new SelectListItem { Text = "Female", Value = "Female" },
                new SelectListItem { Text = "Mixed", Value = "Mixed" }
            };

            ViewBag.StatusOptions = Enum.GetValues(typeof(Status))
                .Cast<Status>()
                .Select(s => new SelectListItem
                {
                    Text = s.ToString(),
                    Value = ((int)s).ToString()
                })
                .ToList();
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////Room Maintenance //////////////////////////////////////
        // GET: Room/GetMaintenanceRequests/5
        [HttpGet]
        public async Task<IActionResult> GetMaintenanceRequests(int roomId)
        {
            try
            {
                var requests = await _context.MaintenanceRequests
                    .Include(mr => mr.Room)
                    .Where(mr => mr.RoomId == roomId)
                    .OrderByDescending(mr => mr.RequestDate)
                    .Select(mr => new
                    {
                        requestId = mr.RequestId,
                        roomId = mr.RoomId,
                        requestedBy = mr.RequestedBy,
                        requesterType = mr.RequesterType,
                        requestDate = mr.RequestDate,
                        description = mr.Description,
                        priority = mr.Priority,
                        status = mr.Status.ToString(),
                        resolutionDate = mr.ResolutionDate,
                        resolutionNotes = mr.ResolutionNotes,
                        roomNumber = mr.Room.RoomNumber,
                        floor = mr.Room.Floor
                    })
                    .ToListAsync();

                return Json(requests);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error loading maintenance requests: {ex.Message}");
            }
        }

        // POST: Room/AddMaintenanceRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMaintenanceRequest(
            int RoomId,
            string RequestedBy,
            string RequesterType,
            string Description,
            string Priority,
            int Status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Description))
                {
                    return BadRequest("Description is required");
                }

                var room = await _context.Rooms.FindAsync(RoomId);
                if (room == null)
                {
                    return NotFound("Room not found");
                }

                var request = new MaintenanceRequest
                {
                    RoomId = RoomId,
                    RequestedBy = RequestedBy,
                    RequesterType = RequesterType,
                    RequestDate = DateTime.Now,
                    Description = Description,
                    ResolutionNotes = string.Empty,
                    Priority = Priority,
                    Status = (Status)Status,
                    CreatedBy = User.Identity?.Name ?? "System",
                    CreatedAt = DateTime.Now,
                    UpdatedBy = User.Identity?.Name ?? "System",
                    UpdatedAt = DateTime.Now
                };

                _context.MaintenanceRequests.Add(request);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, requestId = request.RequestId });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error adding maintenance request: {ex.Message}");
            }
        }

        // POST: Room/UpdateMaintenanceRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMaintenanceRequest(
            int RequestId,
            string RequestedBy,
            string RequesterType,
            string Description,
            string Priority,
            int Status,
            string ResolutionNotes)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Description))
                {
                    return BadRequest("Description is required");
                }

                var request = await _context.MaintenanceRequests.FindAsync(RequestId);
                if (request == null)
                {
                    return NotFound("Maintenance request not found");
                }

                request.RequestedBy = RequestedBy;
                request.RequesterType = RequesterType;
                request.Description = Description;
                request.Priority = Priority;
                request.Status = (Status)Status;
                request.ResolutionNotes = ResolutionNotes;
                request.UpdatedBy = User.Identity?.Name ?? "System";
                request.UpdatedAt = DateTime.Now;

                // Set resolution date if status is resolved/completed
                if (Status == 1 && !request.ResolutionDate.HasValue)
                {
                    request.ResolutionDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating maintenance request: {ex.Message}");
            }
        }

        // POST: Room/DeleteMaintenanceRequest/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaintenanceRequest(int id)
        {
            try
            {
                var request = await _context.MaintenanceRequests.FindAsync(id);
                if (request == null)
                {
                    return NotFound("Maintenance request not found");
                }

                _context.MaintenanceRequests.Remove(request);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error deleting maintenance request: {ex.Message}");
            }
        }
    }

    // Request models
    public class BulkResourceRequest
    {
        public int RoomId { get; set; }
        public List<ResourceDto> Resources { get; set; }
    }

    public class ApplyResourcesToRoomsRequest
    {
        public List<int> RoomIds { get; set; }
        public List<ResourceDto> Resources { get; set; }
    }

    public class ResourceDto
    {
        public int ResourceTypeId { get; set; }
        public int Quantity { get; set; }
        public Status Status { get; set; }
    }

    public class BulkBedSpaceRequest
    {
        public int RoomId { get; set; }
        public List<BedSpaceDto> BedSpaces { get; set; }
    }

    public class BedSpaceDto
    {
        public string BedIdentifier { get; set; }
        public Status Status { get; set; }
        public bool IsSpecialReservation { get; set; }
        public int CurrentStudentYear { get; set; }
        public int CurrentStudentSemister { get; set; }
    }
}