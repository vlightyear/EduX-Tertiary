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
    [Authorize(Roles = "Admin, Registrar")]
    public class MaintenanceController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public MaintenanceController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: Maintenance/Index
        public async Task<IActionResult> Index()
        {
            var requests = await _context.MaintenanceRequests
                .Include(mr => mr.Room)
                    .ThenInclude(r => r.Hostel)
                        .ThenInclude(h => h.Campus)
                .OrderByDescending(mr => mr.RequestDate)
                .ToListAsync();

            await PopulateViewBag();

            return View("~/Views/Accommodation/MaintenanceIndex.cshtml", requests);
        }

        // GET: Maintenance/GetMaintenanceRequest/5
        [HttpGet]
        public async Task<IActionResult> GetMaintenanceRequest(int id)
        {
            try
            {
                var request = await _context.MaintenanceRequests
                    .Include(mr => mr.Room)
                        .ThenInclude(r => r.Hostel)
                            .ThenInclude(h => h.Campus)
                    .FirstOrDefaultAsync(mr => mr.RequestId == id);

                if (request == null)
                {
                    return NotFound("Maintenance request not found");
                }

                var requestDto = new
                {
                    id = request.RequestId,
                    roomId = request.RoomId,
                    roomNumber = request.Room?.RoomNumber,
                    hostelName = request.Room?.Hostel?.HostelName,
                    campusName = request.Room?.Hostel?.Campus?.CampusName,
                    requestedBy = request.RequestedBy,
                    requesterType = request.RequesterType,
                    requestDate = request.RequestDate,
                    description = request.Description,
                    priority = request.Priority,
                    status = request.Status.ToString(),
                    resolutionDate = request.ResolutionDate,
                    resolutionNotes = request.ResolutionNotes
                };

                return Json(requestDto);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error loading maintenance request: {ex.Message}");
            }
        }

        // POST: Maintenance/CreateMaintenanceRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMaintenanceRequest(MaintenanceRequest request)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(request.RoomId);
                if (room == null)
                {
                    TempData["Error"] = "Room not found";
                    return RedirectToAction(nameof(Index));
                }

                request.ResolutionNotes = string.Empty;
                request.RequestDate = DateTime.Now;
                request.CreatedBy = User.Identity?.Name ?? "System";
                request.CreatedAt = DateTime.Now;
                request.UpdatedBy = User.Identity?.Name ?? "System";
                request.UpdatedAt = DateTime.Now;

                _context.MaintenanceRequests.Add(request);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Maintenance request created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating maintenance request: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Maintenance/UpdateMaintenanceRequest/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMaintenanceRequest(int id, MaintenanceRequest updatedRequest)
        {
            try
            {
                var request = await _context.MaintenanceRequests.FindAsync(id);
                if (request == null)
                {
                    TempData["Error"] = "Maintenance request not found";
                    return RedirectToAction(nameof(Index));
                }

                request.RoomId = updatedRequest.RoomId;
                request.RequestedBy = updatedRequest.RequestedBy;
                request.RequesterType = updatedRequest.RequesterType;
                request.Description = updatedRequest.Description;
                request.Priority = updatedRequest.Priority;
                request.Status = updatedRequest.Status;
                request.ResolutionNotes = updatedRequest?.ResolutionNotes?? string.Empty;
                request.UpdatedBy = User.Identity?.Name ?? "System";
                request.UpdatedAt = DateTime.Now;

                // Set resolution date if status is Active (completed)
                if (updatedRequest.Status == Status.Active && !request.ResolutionDate.HasValue)
                {
                    request.ResolutionDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Maintenance request updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating maintenance request try again later!";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Maintenance/DeleteMaintenanceRequest/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaintenanceRequest(int id)
        {
            try
            {
                var request = await _context.MaintenanceRequests.FindAsync(id);
                if (request == null)
                {
                    TempData["Error"] = "Maintenance request not found";
                    return RedirectToAction(nameof(Index));
                }

                _context.MaintenanceRequests.Remove(request);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Maintenance request deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting maintenance request: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Maintenance/GetRoomsByHostel/5
        [HttpGet]
        [Route("Maintenance/GetRoomsByHostel/{hostelId}")]
        public async Task<IActionResult> GetRoomsByHostel(int hostelId)
        {
            try
            {
                var rooms = await _context.Rooms
                    .Where(r => r.HostelId == hostelId)
                    .Select(r => new
                    {
                        id = r.RoomId,
                        text = $"Room {r.RoomNumber} (Floor {r.Floor})"
                    })
                    .ToListAsync();

                return Json(rooms);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error loading rooms: {ex.Message}");
            }
        }

        // GET: Maintenance/GetHostelsByCampus/5
        [HttpGet]
        [Route("Maintenance/GetHostelsByCampus/{campusId}")]
        public async Task<IActionResult> GetHostelsByCampus(int campusId)
        {
            try
            {
                var hostels = await _context.Hostels
                    .Where(h => h.CampusId == campusId)
                    .Select(h => new
                    {
                        id = h.HostelId,
                        text = h.HostelName
                    })
                    .OrderBy(h => h.text)
                    .ToListAsync();

                return Json(hostels);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error loading hostels: {ex.Message}");
            }
        }

        private async Task PopulateViewBag()
        {
            ViewBag.Campuses = await _context.Campuses
                .Where(c => c.IsActive)
                .Select(c => new SelectListItem
                {
                    Value = c.CampusId.ToString(),
                    Text = c.CampusName
                })
                .OrderBy(c => c.Text)
                .ToListAsync();

            ViewBag.Hostels = await _context.Hostels
                .Select(h => new SelectListItem
                {
                    Value = h.HostelId.ToString(),
                    Text = h.HostelName
                })
                .OrderBy(h => h.Text)
                .ToListAsync();

            // FIX: Use concatenation instead of string.Format for database translation
            ViewBag.Rooms = await _context.Rooms
                .Include(r => r.Hostel)
                .Select(r => new SelectListItem
                {
                    Value = r.RoomId.ToString(),
                    Text = r.Hostel.HostelName + " - Room " + r.RoomNumber
                })
                .OrderBy(r => r.Text)
                .ToListAsync();

            ViewBag.StatusOptions = Enum.GetValues(typeof(Status))
                .Cast<Status>()
                .Select(s => new SelectListItem
                {
                    Text = s.ToString(),
                    Value = ((int)s).ToString()
                })
                .ToList();
        }
    }
}