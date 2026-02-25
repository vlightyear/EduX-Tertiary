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

namespace SIS.Controllers.AccommodationControllers
{
    [Authorize(Roles = "HostelManager")]
    public class HostelManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IAccommodationAllocationService _accommodationAllocationService;

        public HostelManagementController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IAccommodationAllocationService accommodationAllocationService)
        {
            _userManager = userManager;
            _context = context;
            _accommodationAllocationService = accommodationAllocationService;
        }

        // GET: HostelManager/Index
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Get hostel assigned to this manager
            var hostel = await _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Rooms)
                    .ThenInclude(r => r.BedSpaces)
                .Include(h => h.Rooms)
                    .ThenInclude(r => r.Resources)
                .FirstOrDefaultAsync(h => h.WardenId == userId);

            if (hostel == null)
            {
                return View("~/Views/Accommodation/HostelManagement/NoHostelAssigned.cshtml");
            }

            // Calculate statistics
            var totalRooms = hostel.Rooms.Count;
            var totalBeds = hostel.Rooms.Sum(r => r.BedSpaces.Count);
            var occupiedBeds = hostel.Rooms.Sum(r => r.BedSpaces.Count(b => b.Status == Status.Occupied));
            var availableBeds = totalBeds - occupiedBeds;

            // Get maintenance requests
            var pendingMaintenanceCount = await _context.MaintenanceRequests
                .Include(mr => mr.Room)
                .Where(mr => mr.Room.HostelId == hostel.HostelId && mr.Status != Status.Active)
                .CountAsync();

            // Get recent check-ins (last 7 days)
            var recentCheckIns = await _context.CheckInOuts
                .Include(c => c.Allocation)
                    .ThenInclude(a => a.Application)
                        .ThenInclude(app => app.Student)
                .Include(c => c.Allocation)
                    .ThenInclude(a => a.Bed)
                        .ThenInclude(b => b.Room)
                .Where(c => c.Allocation.Bed.Room.HostelId == hostel.HostelId
                    && c.CheckInDate.HasValue
                    && c.CheckInDate.Value >= DateTime.Now.AddDays(-7))
                .OrderByDescending(c => c.CheckInDate)
                .Take(10)
                .ToListAsync();

            var viewModel = new HostelManagerDashboardViewModel
            {
                Hostel = hostel,
                TotalRooms = totalRooms,
                TotalBeds = totalBeds,
                OccupiedBeds = occupiedBeds,
                AvailableBeds = availableBeds,
                OccupancyRate = totalBeds > 0 ? (decimal)occupiedBeds / totalBeds * 100 : 0,
                PendingMaintenanceCount = pendingMaintenanceCount,
                RecentCheckIns = recentCheckIns
            };

            return View("~/Views/Accommodation/HostelManagement/HostelManagerIndex.cshtml", viewModel);
        }

        // GET: HostelManager/Rooms
        public async Task<IActionResult> Rooms()
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return View("~/Views/Accommodation/HostelManagement/NoHostelAssigned.cshtml");
            }

            await PopulateViewBag(hostel.HostelId);
            return View("~/Views/Accommodation/HostelManagement/HostelManagerRooms.cshtml");
        }

        // GET: HostelManager/GetRooms
        [HttpGet]
        //[Route("Room/GetRooms/{hostelId}")]
        public async Task<IActionResult> GetRooms()
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return Unauthorized(new { message = "You are not assigned to any hostel" });
            }

            var rooms = await _context.Rooms
                .Where(r => r.HostelId == hostel.HostelId)
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
                    resourceCount = r.Resources.Count
                })
                .OrderBy(r => r.floor)
                .ThenBy(r => r.roomNumber)
                .ToListAsync();

            return Json(rooms);
        }

        // GET: HostelManager/GetRoomDetails/5
        [HttpGet]
        public async Task<IActionResult> GetRoomDetails(int id)
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return Unauthorized();
            }

            var room = await _context.Rooms
                .Include(r => r.Hostel)
                    .ThenInclude(h => h.Campus)
                .Include(r => r.BedSpaces)
                .Include(r => r.Resources)
                    .ThenInclude(res => res.ResourceType)
                .FirstOrDefaultAsync(r => r.RoomId == id && r.HostelId == hostel.HostelId);

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
                    studentId = activeAllocations.ContainsKey(b.BedId)
                        ? activeAllocations[b.BedId].Application.StudentId
                        : (int?)null,
                    studentName = activeAllocations.ContainsKey(b.BedId)
                        ? activeAllocations[b.BedId].Application.Student.FullName
                        : null,
                    studentNumber = activeAllocations.ContainsKey(b.BedId)
                        ? activeAllocations[b.BedId].Application.Student.StudentId_Number
                        : null,
                    studentPhone = activeAllocations.ContainsKey(b.BedId)
                        ? activeAllocations[b.BedId].Application.Student.Phone
                        : null,
                    studentEmail = activeAllocations.ContainsKey(b.BedId)
                        ? activeAllocations[b.BedId].Application.Student.Email
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

        // GET: HostelManager/Maintenance
        public async Task<IActionResult> Maintenance()
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return View("NoHostelAssigned");
            }

            await PopulateViewBag(hostel.HostelId);
            return View("~/Views/Accommodation/HostelManagement/HostelManagerMaintenance.cshtml");
        }

        // GET: HostelManager/GetMaintenanceRequests
        [HttpGet]
        public async Task<IActionResult> GetMaintenanceRequests(int? roomId = null, string priority = null, string status = null)
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return Unauthorized();
            }

            var query = _context.MaintenanceRequests
                .Include(mr => mr.Room)
                .Where(mr => mr.Room.HostelId == hostel.HostelId);

            if (roomId.HasValue)
            {
                query = query.Where(mr => mr.RoomId == roomId.Value);
            }

            if (!string.IsNullOrEmpty(priority))
            {
                query = query.Where(mr => mr.Priority == priority);
            }

            if (!string.IsNullOrEmpty(status))
            {
                var statusEnum = Enum.Parse<Status>(status);
                query = query.Where(mr => mr.Status == statusEnum);
            }

            var requests = await query
                .OrderByDescending(mr => mr.RequestDate)
                .Select(mr => new
                {
                    requestId = mr.RequestId,
                    roomId = mr.RoomId,
                    roomNumber = mr.Room.RoomNumber,
                    floor = mr.Room.Floor,
                    requestedBy = mr.RequestedBy,
                    requesterType = mr.RequesterType,
                    requestDate = mr.RequestDate,
                    description = mr.Description,
                    priority = mr.Priority,
                    status = mr.Status.ToString(),
                    resolutionDate = mr.ResolutionDate,
                    resolutionNotes = mr.ResolutionNotes
                })
                .ToListAsync();

            return Json(requests);
        }

        // POST: HostelManager/UpdateMaintenanceRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMaintenanceRequest(
            int RequestId,
            string Priority,
            int Status,
            string ResolutionNotes)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                var request = await _context.MaintenanceRequests
                    .Include(mr => mr.Room)
                    .FirstOrDefaultAsync(mr => mr.RequestId == RequestId && mr.Room.HostelId == hostel.HostelId);

                if (request == null)
                {
                    return NotFound("Maintenance request not found");
                }

                request.Priority = Priority;
                request.Status = (Status)Status;
                request.ResolutionNotes = ResolutionNotes ?? string.Empty;
                request.UpdatedBy = User.Identity?.Name ?? "System";
                request.UpdatedAt = DateTime.Now;

                // Set resolution date if status is Active (completed)
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

        // GET: HostelManager/CheckInOut
        public async Task<IActionResult> CheckInOut()
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return View("NoHostelAssigned");
            }

            await PopulateViewBag(hostel.HostelId);
            return View("~/Views/Accommodation/HostelManagement/HostelManagerCheckInOut.cshtml");
        }

        // GET: HostelManager/GetPendingCheckIns
        [HttpGet]
        public async Task<IActionResult> GetPendingCheckIns()
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return Unauthorized();
            }

            var pendingCheckIns = await _context.Allocations
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                .Where(a => a.Bed.Room.HostelId == hostel.HostelId
                    && a.Status == Status.Active
                    && !_context.CheckInOuts.Any(c => c.AllocationId == a.AllocationId && c.CheckInDate.HasValue))
                .Select(a => new
                {
                    allocationId = a.AllocationId,
                    studentId = a.Application.StudentId,
                    studentName = a.Application.Student.FullName,
                    studentNumber = a.Application.Student.StudentId_Number,
                    studentPhone = a.Application.Student.Phone,
                    studentEmail = a.Application.Student.Email,
                    roomNumber = a.Bed.Room.RoomNumber,
                    floor = a.Bed.Room.Floor,
                    bedIdentifier = a.Bed.BedIdentifier,
                    allocationDate = a.AllocationDate,
                    startDate = a.StartDate
                })
                .OrderBy(a => a.allocationDate)
                .ToListAsync();

            return Json(pendingCheckIns);
        }

        // POST: HostelManager/CheckIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(int AllocationId, string CheckInCondition)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                var allocation = await _context.Allocations
                    .Include(a => a.Bed)
                        .ThenInclude(b => b.Room)
                    .FirstOrDefaultAsync(a => a.AllocationId == AllocationId && a.Bed.Room.HostelId == hostel.HostelId);

                if (allocation == null)
                {
                    return NotFound("Allocation not found");
                }

                var existingCheckIn = await _context.CheckInOuts
                    .FirstOrDefaultAsync(c => c.AllocationId == AllocationId);

                if (existingCheckIn != null && existingCheckIn.CheckInDate.HasValue)
                {
                    return BadRequest("Student has already checked in");
                }

                var userId = _userManager.GetUserId(User);

                if (existingCheckIn == null)
                {
                    var checkInOut = new CheckInOut
                    {
                        AllocationId = AllocationId,
                        CheckInDate = DateTime.Now,
                        CheckInCondition = CheckInCondition,
                        CheckInStaffId = userId,
                        CheckOutCondition = string.Empty,
                        
                    };
                    _context.CheckInOuts.Add(checkInOut);
                }
                else
                {
                    existingCheckIn.CheckInDate = DateTime.Now;
                    existingCheckIn.CheckInCondition = CheckInCondition;
                    existingCheckIn.CheckInStaffId = userId;
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing check-in: {ex.Message}");
            }
        }

        // GET: HostelManager/GetOccupiedBeds
        [HttpGet]
        public async Task<IActionResult> GetOccupiedBeds()
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return Unauthorized();
            }

            var occupiedBeds = await _context.Allocations
                .Include(a => a.Bed)
                    .ThenInclude(b => b.Room)
                .Include(a => a.Application)
                    .ThenInclude(app => app.Student)
                .Where(a => a.Bed.Room.HostelId == hostel.HostelId && a.Status == Status.Active)
                .Select(a => new
                {
                    allocationId = a.AllocationId,
                    bedId = a.BedId,
                    bedIdentifier = a.Bed.BedIdentifier,
                    roomNumber = a.Bed.Room.RoomNumber,
                    floor = a.Bed.Room.Floor,
                    studentName = a.Application.Student.FullName,
                    studentNumber = a.Application.Student.StudentId_Number,
                    studentPhone = a.Application.Student.Phone,
                    studentEmail = a.Application.Student.Email,
                    allocationEndDate = a.EndDate,
                    hasCheckedIn = _context.CheckInOuts.Any(c => c.AllocationId == a.AllocationId && c.CheckInDate.HasValue),
                    hasCheckedOut = _context.CheckInOuts.Any(c => c.AllocationId == a.AllocationId && c.CheckOutDate.HasValue)
                })
                .OrderBy(a => a.roomNumber)
                .ThenBy(a => a.bedIdentifier)
                .ToListAsync();

            return Json(occupiedBeds);
        }

        // POST: HostelManager/CheckOut
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(int AllocationId, string CheckOutCondition, decimal DamageCharges)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                var allocation = await _context.Allocations
                    .Include(a => a.Bed)
                        .ThenInclude(b => b.Room)
                    .Include(a => a.Application)
                        .ThenInclude(app => app.Student)
                    .FirstOrDefaultAsync(a => a.AllocationId == AllocationId && a.Bed.Room.HostelId == hostel.HostelId);

                if (allocation == null)
                {
                    return NotFound("Allocation not found");
                }

                var checkInOut = await _context.CheckInOuts
                    .FirstOrDefaultAsync(c => c.AllocationId == AllocationId);

                if (checkInOut == null)
                {
                    return BadRequest("No check-in record found");
                }

                if (checkInOut.CheckOutDate.HasValue)
                {
                    return BadRequest("Student has already checked out");
                }

                var userId = _userManager.GetUserId(User);
                var userName = User.Identity?.Name ?? "System";

                // Record check-out details
                checkInOut.CheckOutDate = DateTime.Now;
                checkInOut.CheckOutCondition = CheckOutCondition;
                checkInOut.CheckOutStaffId = userId;
                checkInOut.DamageCharges = DamageCharges;

                // Get accommodation configuration to check deallocate setting
                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();

                // Update bed status to available
                var bed = await _context.BedSpaces.FindAsync(allocation.BedId);
                if (bed != null)
                {
                    bed.Status = Status.Available;
                    bed.UpdatedBy = userName;
                    bed.UpdatedAt = DateTime.Now;
                }

                // Check if we should deallocate bed space from student upon checkout
                if (config != null && config.DeAllocateBedSpaceUponCheckOut)
                {
                    // Use the service to remove student from accommodation
                    var studentId = allocation.Application.StudentId;
                    var checkoutReason = $"Student checked out on {DateTime.Now:yyyy-MM-dd HH:mm}. " +
                                        $"Condition: {CheckOutCondition}. " +
                                        (DamageCharges > 0 ? $"Damage charges: K{DamageCharges:N2}." : "No damage charges.");

                    var removalResult = await _accommodationAllocationService.RemoveStudentFromAccommodation(
                        studentId,
                        userName,
                        checkoutReason
                    );

                    if (!removalResult.Status)
                    {
                        // If removal failed, still save the check-out record but return the error
                        await _context.SaveChangesAsync();
                        return BadRequest($"Check-out recorded but deallocation failed: {removalResult.Message}");
                    }

                    // Update application notes with check-out info
                    if (allocation.Application != null)
                    {
                        allocation.Application.Notes += $" [CHECK-OUT: {checkoutReason}]";
                        allocation.Application.UpdatedBy = userName;
                        allocation.Application.UpdatedAt = DateTime.Now;
                    }
                }
                else
                {
                    // Keep allocation active, just mark as checked out
                    // The allocation remains but the bed is free for the student to come back
                    allocation.UpdatedBy = userName;
                    allocation.UpdatedAt = DateTime.Now;

                    // Update application notes
                    if (allocation.Application != null)
                    {
                        var checkoutNote = $" [CHECK-OUT: Student checked out on {DateTime.Now:yyyy-MM-dd HH:mm}. " +
                                          $"Condition: {CheckOutCondition}. " +
                                          (DamageCharges > 0 ? $"Damage charges: K{DamageCharges:N2}. " : "") +
                                          "Allocation remains active - student can check back in.]";

                        allocation.Application.Notes += checkoutNote;
                        allocation.Application.UpdatedBy = userName;
                        allocation.Application.UpdatedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = config != null && config.DeAllocateBedSpaceUponCheckOut
                        ? "Student checked out successfully and bed space has been deallocated."
                        : "Student checked out successfully. Allocation remains active."
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing check-out: {ex.Message}");
            }
        }

        // GET: HostelManager/Reports
        public async Task<IActionResult> Reports()
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return View("~/Views/Accommodation/HostelManagement/NoHostelAssigned.cshtml");
            }

            return View("~/Views/Accommodation/HostelManagement/HostelManagerReports.cshtml", hostel);
        }

        // GET: HostelManager/GetOccupancyReport
        [HttpGet]
        public async Task<IActionResult> GetOccupancyReport()
        {
            var hostel = await GetAssignedHostel();
            if (hostel == null)
            {
                return Unauthorized();
            }

            var rooms = await _context.Rooms
                .Include(r => r.BedSpaces)
                .Where(r => r.HostelId == hostel.HostelId)
                .ToListAsync();

            var report = rooms.Select(r => new
            {
                roomNumber = r.RoomNumber,
                floor = r.Floor,
                roomType = r.RoomType,
                capacity = r.Capacity,
                totalBeds = r.BedSpaces.Count,
                occupiedBeds = r.BedSpaces.Count(b => b.Status == Status.Occupied),
                availableBeds = r.BedSpaces.Count(b => b.Status == Status.Active),
                occupancyRate = r.BedSpaces.Count > 0
                    ? (decimal)r.BedSpaces.Count(b => b.Status == Status.Occupied) / r.BedSpaces.Count * 100
                    : 0
            }).ToList();

            return Json(report);
        }

        // Helper Methods
        private async Task<Hostel> GetAssignedHostel()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Hostels
                .Include(h => h.Campus)
                .FirstOrDefaultAsync(h => h.WardenId == userId);
        }

        private async Task PopulateViewBag(int hostelId)
        {
            try
            {
                ViewBag.Hostel = await _context.Hostels
                    .Include(h => h.Campus)
                    .FirstOrDefaultAsync(h => h.HostelId == hostelId);

                ViewBag.Rooms = await _context.Rooms
                    .Where(r => r.HostelId == hostelId)
                    .OrderBy(r => r.Floor)
                    .ToListAsync();

                ViewBag.ResourceTypes = await _context.ResourceTypes
                   .OrderBy(rt => rt.Name)
                   .ToListAsync();

                ViewBag.StatusOptions = Enum.GetValues(typeof(Status))
                    .Cast<Status>()
                    .Select(s => new SelectListItem
                    {
                        Text = s.ToString(),
                        Value = ((int)s).ToString()
                    })
                    .ToList();

                ViewBag.PriorityOptions = new List<SelectListItem>
            {
                new SelectListItem { Text = "Low", Value = "Low" },
                new SelectListItem { Text = "Medium", Value = "Medium" },
                new SelectListItem { Text = "High", Value = "High" }
            };
            }
            catch (Exception ex)
            {
                var message = ex.Message;
            }
        }


        // POST: HostelManagement/AddBedSpace
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBedSpace(int RoomId, string BedIdentifier, int Status)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                if (string.IsNullOrWhiteSpace(BedIdentifier))
                {
                    return BadRequest("Bed identifier is required");
                }

                var room = await _context.Rooms
                    .FirstOrDefaultAsync(r => r.RoomId == RoomId && r.HostelId == hostel.HostelId);

                if (room == null)
                {
                    return NotFound("Room not found or not in your hostel");
                }

                var bedSpace = new BedSpace
                {
                    RoomId = RoomId,
                    BedIdentifier = BedIdentifier,
                    Status = (Status)Status,
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

        // POST: HostelManagement/EditBedSpace
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBedSpace(int BedId, string BedIdentifier, int Status)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                if (string.IsNullOrWhiteSpace(BedIdentifier))
                {
                    return BadRequest("Bed identifier is required");
                }

                var bedSpace = await _context.BedSpaces
                    .Include(b => b.Room)
                    .FirstOrDefaultAsync(b => b.BedId == BedId && b.Room.HostelId == hostel.HostelId);

                if (bedSpace == null)
                {
                    return NotFound("Bed space not found or not in your hostel");
                }

                bedSpace.BedIdentifier = BedIdentifier;
                bedSpace.Status = (Status)Status;
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

        // POST: HostelManagement/DeleteBedSpace
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBedSpace(int id)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                var bedSpace = await _context.BedSpaces
                    .Include(b => b.Room)
                    .FirstOrDefaultAsync(b => b.BedId == id && b.Room.HostelId == hostel.HostelId);

                if (bedSpace == null)
                {
                    return NotFound("Bed space not found or not in your hostel");
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

        // ============================================
        // RESOURCE MANAGEMENT
        // ============================================

        // POST: HostelManagement/AddRoomResource
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRoomResource(int RoomId, int ResourceTypeId, int Quantity, int Status)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                var room = await _context.Rooms
                    .Include(r => r.Resources)
                    .FirstOrDefaultAsync(r => r.RoomId == RoomId && r.HostelId == hostel.HostelId);

                if (room == null)
                {
                    return NotFound("Room not found or not in your hostel");
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

        // POST: HostelManagement/UpdateRoomResource
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoomResource(int ResourceId, int Quantity, int Status)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                if (Quantity <= 0)
                {
                    return BadRequest("Quantity must be greater than 0");
                }

                var resource = await _context.RoomResources
                    .Include(r => r.Room)
                    .FirstOrDefaultAsync(r => r.ResourceId == ResourceId && r.Room.HostelId == hostel.HostelId);

                if (resource == null)
                {
                    return NotFound("Resource not found or not in your hostel");
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

        // POST: HostelManagement/DeleteRoomResource
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoomResource(int id)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                var resource = await _context.RoomResources
                    .Include(r => r.Room)
                    .FirstOrDefaultAsync(r => r.ResourceId == id && r.Room.HostelId == hostel.HostelId);

                if (resource == null)
                {
                    return NotFound("Resource not found or not in your hostel");
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

        // POST: HostelManagement/AddBulkResources
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBulkResources([FromBody] BulkResourceRequest request)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                var room = await _context.Rooms
                    .Include(r => r.Resources)
                    .FirstOrDefaultAsync(r => r.RoomId == request.RoomId && r.HostelId == hostel.HostelId);

                if (room == null)
                {
                    return NotFound("Room not found or not in your hostel");
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
            catch (Exception ex)
            {
                return BadRequest($"Error adding bulk resources: {ex.Message}");
            }
        }

        // ============================================
        // MAINTENANCE REQUEST MANAGEMENT
        // ============================================

        // GET: HostelManagement/GetRoomMaintenanceRequests
        [HttpGet]
        public async Task<IActionResult> GetRoomMaintenanceRequests(int roomId)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                var room = await _context.Rooms
                    .FirstOrDefaultAsync(r => r.RoomId == roomId && r.HostelId == hostel.HostelId);

                if (room == null)
                {
                    return NotFound("Room not found or not in your hostel");
                }

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

        // POST: HostelManagement/AddMaintenanceRequest
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
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                if (string.IsNullOrWhiteSpace(Description))
                {
                    return BadRequest("Description is required");
                }

                var room = await _context.Rooms
                    .FirstOrDefaultAsync(r => r.RoomId == RoomId && r.HostelId == hostel.HostelId);

                if (room == null)
                {
                    return NotFound("Room not found or not in your hostel");
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

        // POST: HostelManagement/UpdateRoomMaintenanceRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoomMaintenanceRequest(
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
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                if (string.IsNullOrWhiteSpace(Description))
                {
                    return BadRequest("Description is required");
                }

                var request = await _context.MaintenanceRequests
                    .Include(mr => mr.Room)
                    .FirstOrDefaultAsync(mr => mr.RequestId == RequestId && mr.Room.HostelId == hostel.HostelId);

                if (request == null)
                {
                    return NotFound("Maintenance request not found or not in your hostel");
                }

                request.RequestedBy = RequestedBy;
                request.RequesterType = RequesterType;
                request.Description = Description;
                request.Priority = Priority;
                request.Status = (Status)Status;
                request.ResolutionNotes = ResolutionNotes ?? string.Empty;
                request.UpdatedBy = User.Identity?.Name ?? "System";
                request.UpdatedAt = DateTime.Now;

                // Set resolution date if status is Active (completed)
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

        // POST: HostelManagement/DeleteMaintenanceRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaintenanceRequest(int id)
        {
            try
            {
                var hostel = await GetAssignedHostel();
                if (hostel == null)
                {
                    return Unauthorized();
                }

                var request = await _context.MaintenanceRequests
                    .Include(mr => mr.Room)
                    .FirstOrDefaultAsync(mr => mr.RequestId == id && mr.Room.HostelId == hostel.HostelId);

                if (request == null)
                {
                    return NotFound("Maintenance request not found or not in your hostel");
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
        // ============================================
        // UPDATE PopulateViewBag METHOD
        // ============================================


    // View Models
    public class HostelManagerDashboardViewModel
    {
        public Hostel Hostel { get; set; }
        public int TotalRooms { get; set; }
        public int TotalBeds { get; set; }
        public int OccupiedBeds { get; set; }
        public int AvailableBeds { get; set; }
        public decimal OccupancyRate { get; set; }
        public int PendingMaintenanceCount { get; set; }
        public List<CheckInOut> RecentCheckIns { get; set; }
    }


}