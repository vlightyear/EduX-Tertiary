using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.StudentAccommodation;

namespace SIS.Controllers
{
    public class ResourceTypeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ResourceTypeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var resourceTypes = await _context.ResourceTypes
                .Include(rt => rt.RoomResources)
                .OrderBy(rt => rt.Name)
                .ToListAsync();
            return View("~/Views/ResourceType/Index.cshtml", resourceTypes);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ResourceTypeDto model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    return Json(new { success = false, message = "Resource type name is required." });
                }

                // Check for duplicate name
                var exists = await _context.ResourceTypes
                    .AnyAsync(rt => rt.Name.ToLower() == model.Name.Trim().ToLower());

                if (exists)
                {
                    return Json(new { success = false, message = "A resource type with this name already exists." });
                }

                var resourceType = new ResourceType
                {
                    Name = model.Name.Trim(),
                    Description = model.Description?.Trim(),
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity.Name ?? "System"
                };

                _context.ResourceTypes.Add(resourceType);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Resource type created successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error creating resource type: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var resourceType = await _context.ResourceTypes.FindAsync(id);
                if (resourceType == null)
                {
                    return NotFound();
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        resourceType.ResourceTypeId,
                        resourceType.Name,
                        resourceType.Description
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error retrieving resource type: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] ResourceTypeDto model)
        {
            try
            {
                if (model.ResourceTypeId <= 0)
                {
                    return Json(new { success = false, message = "Invalid resource type ID." });
                }

                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    return Json(new { success = false, message = "Resource type name is required." });
                }

                var resourceType = await _context.ResourceTypes.FindAsync(model.ResourceTypeId);
                if (resourceType == null)
                {
                    return Json(new { success = false, message = "Resource type not found." });
                }

                // Check for duplicate name (excluding current record)
                var exists = await _context.ResourceTypes
                    .AnyAsync(rt => rt.Name.ToLower() == model.Name.Trim().ToLower()
                                 && rt.ResourceTypeId != model.ResourceTypeId);

                if (exists)
                {
                    return Json(new { success = false, message = "A resource type with this name already exists." });
                }

                resourceType.Name = model.Name.Trim();
                resourceType.Description = model.Description?.Trim();
                resourceType.UpdatedAt = DateTime.Now;
                resourceType.UpdatedBy = User.Identity.Name ?? "System";

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Resource type updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating resource type: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var resourceType = await _context.ResourceTypes
                    .Include(rt => rt.RoomResources)
                    .FirstOrDefaultAsync(rt => rt.ResourceTypeId == id);

                if (resourceType == null)
                {
                    return Json(new { success = false, message = "Resource type not found." });
                }

                // Check if resource type is in use
                if (resourceType.RoomResources.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot delete this resource type. It is currently assigned to {resourceType.RoomResources.Count} room(s)."
                    });
                }

                _context.ResourceTypes.Remove(resourceType);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Resource type deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting resource type: " + ex.Message });
            }
        }
    }

    // DTO class to receive data from client
    public class ResourceTypeDto
    {
        public int ResourceTypeId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}