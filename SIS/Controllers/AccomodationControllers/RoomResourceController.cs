using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.StudentAccommodation;
using SIS.Enums;

namespace SIS.Controllers.AccomodationControllers
{
    public class RoomResourceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomResourceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: RoomResource
        public async Task<IActionResult> Index()
        {
            try
            {
                var resources = await _context.RoomResources
                    .Include(r => r.Room)
                        .ThenInclude(room => room.Hostel)
                            .ThenInclude(hostel => hostel.Campus)
                    .Include(r => r.ResourceType)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                // Calculate statistics
                ViewBag.TotalResources = resources.Count;
                ViewBag.ActiveResources = resources.Count(r => r.Status == Status.Active);
                ViewBag.MaintenanceResources = resources.Count(r => r.Status == Status.Maintenance);
                ViewBag.RoomsWithResources = resources.Select(r => r.RoomId).Distinct().Count();

                // Load campuses for filter
                ViewBag.Campuses = await _context.Campuses
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.CampusName)
                    .ToListAsync();

                return View("~/Views/Accommodation/RoomResource_Index.cshtml", resources);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading resources: {ex.Message}";
                return View("~/Views/Accommodation/RoomResource_Index.cshtml", new List<RoomResource>());
            }
        }

        // GET: RoomResource/Create
        public async Task<IActionResult> Create()
        {
            await LoadViewBagData();
            return View("~/Views/Accommodation/RoomResource_Create.cshtml");
        }

        // POST: RoomResource/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RoomResourceDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await LoadViewBagData();
                    return View("~/Views/Accommodation/RoomResource_Create.cshtml", dto);
                }

                // Check if ResourceType exists
                var resourceType = await _context.ResourceTypes.FindAsync(dto.ResourceTypeId);
                if (resourceType == null)
                {
                    ModelState.AddModelError("ResourceTypeId", "Invalid resource type selected.");
                    await LoadViewBagData();
                    return View("~/Views/Accommodation/RoomResource_Create.cshtml", dto);
                }

                // Check if this resource type already exists in the room
                var existingResource = await _context.RoomResources
                    .AnyAsync(r => r.RoomId == dto.RoomId && r.ResourceTypeId == dto.ResourceTypeId);

                if (existingResource)
                {
                    ModelState.AddModelError("ResourceTypeId", $"Resource type '{resourceType.Name}' already exists in this room.");
                    await LoadViewBagData();
                    return View("~/Views/Accommodation/RoomResource_Create.cshtml", dto);
                }

                var resource = new RoomResource
                {
                    RoomId = dto.RoomId,
                    ResourceTypeId = dto.ResourceTypeId,
                    Quantity = dto.Quantity,
                    Status = dto.Status,
                    CreatedBy = User.Identity?.Name ?? "System",
                    CreatedAt = DateTime.Now,
                    UpdatedBy = User.Identity?.Name ?? "System",
                    UpdatedAt = DateTime.Now
                };

                _context.RoomResources.Add(resource);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Resource '{resourceType.Name}' added successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error creating resource: {ex.Message}";
                await LoadViewBagData();
                return View("~/Views/Accommodation/RoomResource_Create.cshtml", dto);
            }
        }

        // GET: RoomResource/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var resource = await _context.RoomResources
                    .Include(r => r.Room)
                        .ThenInclude(r => r.Hostel)
                    .Include(r => r.ResourceType)
                    .FirstOrDefaultAsync(r => r.ResourceId == id);

                if (resource == null)
                {
                    TempData["ErrorMessage"] = "Resource not found.";
                    return RedirectToAction(nameof(Index));
                }

                await LoadViewBagData();

                // Set the selected campus and hostel for the dropdowns
                ViewBag.SelectedCampusId = resource.Room.Hostel.CampusId;
                ViewBag.SelectedHostelId = resource.Room.HostelId;

                var dto = new RoomResourceDto
                {
                    ResourceId = resource.ResourceId,
                    RoomId = resource.RoomId,
                    ResourceTypeId = resource.ResourceTypeId,
                    Quantity = resource.Quantity,
                    Status = resource.Status
                };

                return View("~/Views/Accommodation/RoomResource_Edit.cshtml", dto);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading resource: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: RoomResource/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RoomResourceDto dto)
        {
            if (id != dto.ResourceId)
            {
                TempData["ErrorMessage"] = "Invalid resource ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    await LoadViewBagData();
                    return View("~/Views/Accommodation/RoomResource_Edit.cshtml", dto);
                }

                var existingResource = await _context.RoomResources.FindAsync(id);
                if (existingResource == null)
                {
                    TempData["ErrorMessage"] = "Resource not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if ResourceType exists
                var resourceType = await _context.ResourceTypes.FindAsync(dto.ResourceTypeId);
                if (resourceType == null)
                {
                    ModelState.AddModelError("ResourceTypeId", "Invalid resource type selected.");
                    await LoadViewBagData();
                    return View("~/Views/Accommodation/RoomResource_Edit.cshtml", dto);
                }

                // Check if changing to a resource type that already exists in the room
                var duplicateResource = await _context.RoomResources
                    .AnyAsync(r => r.RoomId == dto.RoomId &&
                                   r.ResourceTypeId == dto.ResourceTypeId &&
                                   r.ResourceId != id);

                if (duplicateResource)
                {
                    ModelState.AddModelError("ResourceTypeId", $"Resource type '{resourceType.Name}' already exists in this room.");
                    await LoadViewBagData();
                    return View("~/Views/Accommodation/RoomResource_Edit.cshtml", dto);
                }

                // Update fields
                existingResource.RoomId = dto.RoomId;
                existingResource.ResourceTypeId = dto.ResourceTypeId;
                existingResource.Quantity = dto.Quantity;
                existingResource.Status = dto.Status;
                existingResource.UpdatedBy = User.Identity?.Name ?? "System";
                existingResource.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Resource '{resourceType.Name}' updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating resource: {ex.Message}";
                await LoadViewBagData();
                return View("~/Views/Accommodation/RoomResource_Edit.cshtml", dto);
            }
        }

        // GET: RoomResource/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var resource = await _context.RoomResources
                    .Include(r => r.Room)
                        .ThenInclude(room => room.Hostel)
                            .ThenInclude(hostel => hostel.Campus)
                    .Include(r => r.ResourceType)
                    .FirstOrDefaultAsync(r => r.ResourceId == id);

                if (resource == null)
                {
                    TempData["ErrorMessage"] = "Resource not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View("~/Views/Accommodation/RoomResource_Delete.cshtml", resource);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading resource: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: RoomResource/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var resource = await _context.RoomResources
                    .Include(r => r.ResourceType)
                    .FirstOrDefaultAsync(r => r.ResourceId == id);

                if (resource == null)
                {
                    TempData["ErrorMessage"] = "Resource not found.";
                    return RedirectToAction(nameof(Index));
                }

                var resourceName = resource.ResourceType?.Name ?? "Unknown";
                _context.RoomResources.Remove(resource);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Resource '{resourceName}' deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting resource: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: RoomResource/GetHostelsByCampus/5
        [HttpGet]
        public async Task<IActionResult> GetHostelsByCampus(int campusId)
        {
            try
            {
                var hostels = await _context.Hostels
                    .Where(h => h.CampusId == campusId && h.Status == Status.Active)
                    .OrderBy(h => h.HostelName)
                    .Select(h => new
                    {
                        value = h.HostelId,
                        text = h.HostelName
                    })
                    .ToListAsync();

                return Json(hostels);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // GET: RoomResource/GetRoomsByHostel/5
        [HttpGet]
        public async Task<IActionResult> GetRoomsByHostel(int hostelId)
        {
            try
            {
                var rooms = await _context.Rooms
                    .Where(r => r.HostelId == hostelId)
                    .OrderBy(r => r.RoomNumber)
                    .Select(r => new
                    {
                        value = r.RoomId,
                        text = $"Room {r.RoomNumber} (Floor {r.Floor})"
                    })
                    .ToListAsync();

                return Json(rooms);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // GET: RoomResource/GetFilteredResources
        [HttpGet]
        public async Task<IActionResult> GetFilteredResources(int? campusId, int? hostelId, int? roomId, string status)
        {
            try
            {
                var query = _context.RoomResources
                    .Include(r => r.Room)
                        .ThenInclude(room => room.Hostel)
                            .ThenInclude(hostel => hostel.Campus)
                    .Include(r => r.ResourceType)
                    .AsQueryable();

                // Apply filters
                if (campusId.HasValue && campusId.Value > 0)
                {
                    query = query.Where(r => r.Room.Hostel.CampusId == campusId.Value);
                }

                if (hostelId.HasValue && hostelId.Value > 0)
                {
                    query = query.Where(r => r.Room.HostelId == hostelId.Value);
                }

                if (roomId.HasValue && roomId.Value > 0)
                {
                    query = query.Where(r => r.RoomId == roomId.Value);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    if (Enum.TryParse<Status>(status, out var statusEnum))
                    {
                        query = query.Where(r => r.Status == statusEnum);
                    }
                }

                var resources = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        resourceId = r.ResourceId,
                        resourceTypeName = r.ResourceType.Name,
                        resourceTypeDescription = r.ResourceType.Description,
                        quantity = r.Quantity,
                        status = r.Status.ToString(),
                        roomNumber = r.Room.RoomNumber,
                        hostelName = r.Room.Hostel.HostelName,
                        campusName = r.Room.Hostel.Campus.CampusName,
                        updatedAt = r.UpdatedAt
                    })
                    .ToListAsync();

                return Json(resources);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // POST: RoomResource/UpdateRoomResource
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoomResource([FromForm] int ResourceId, [FromForm] int ResourceTypeId,
            [FromForm] int Quantity, [FromForm] int Status)
        {
            try
            {
                var resource = await _context.RoomResources.FindAsync(ResourceId);
                if (resource == null)
                {
                    return NotFound("Resource not found");
                }

                // Check if ResourceType exists
                var resourceType = await _context.ResourceTypes.FindAsync(ResourceTypeId);
                if (resourceType == null)
                {
                    return BadRequest("Invalid resource type");
                }

                // Check for duplicates
                var duplicate = await _context.RoomResources
                    .AnyAsync(r => r.RoomId == resource.RoomId &&
                                   r.ResourceTypeId == ResourceTypeId &&
                                   r.ResourceId != ResourceId);

                if (duplicate)
                {
                    return BadRequest($"Resource type '{resourceType.Name}' already exists in this room");
                }

                resource.ResourceTypeId = ResourceTypeId;
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

        // GET: RoomResource/GetResource/5
        [HttpGet]
        public async Task<IActionResult> GetResource(int id)
        {
            try
            {
                var resource = await _context.RoomResources
                    .Include(r => r.Room)
                        .ThenInclude(room => room.Hostel)
                    .Include(r => r.ResourceType)
                    .Where(r => r.ResourceId == id)
                    .Select(r => new
                    {
                        resourceId = r.ResourceId,
                        roomId = r.RoomId,
                        resourceTypeId = r.ResourceTypeId,
                        resourceTypeName = r.ResourceType.Name,
                        resourceTypeDescription = r.ResourceType.Description,
                        quantity = r.Quantity,
                        status = (int)r.Status,
                        campusId = r.Room.Hostel.CampusId,
                        hostelId = r.Room.HostelId
                    })
                    .FirstOrDefaultAsync();

                if (resource == null)
                {
                    return NotFound();
                }

                return Json(resource);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: RoomResource/UpdateStatus
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, Status status)
        {
            try
            {
                var resource = await _context.RoomResources.FindAsync(id);
                if (resource == null)
                {
                    return Json(new { success = false, message = "Resource not found" });
                }

                resource.Status = status;
                resource.UpdatedBy = User.Identity?.Name ?? "System";
                resource.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Status updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Private helper methods
        private async Task LoadViewBagData()
        {
            ViewBag.Campuses = await _context.Campuses
                .Where(c => c.IsActive)
                .OrderBy(c => c.CampusName)
                .Select(c => new { c.CampusId, c.CampusName })
                .ToListAsync();

            ViewBag.ResourceTypes = await _context.ResourceTypes
                .OrderBy(rt => rt.Name)
                .Select(rt => new { rt.ResourceTypeId, rt.Name, rt.Description })
                .ToListAsync();

            ViewBag.StatusList = Enum.GetValues(typeof(Status))
                .Cast<Status>()
                .Select(s => new { Value = (int)s, Text = s.ToString() })
                .ToList();
        }
    }

    // DTO for RoomResource to avoid binding issues with navigation properties
    public class RoomResourceDto
    {
        public int ResourceId { get; set; }
        public int RoomId { get; set; }
        public int ResourceTypeId { get; set; }
        public int Quantity { get; set; }
        public Status Status { get; set; }
    }
}