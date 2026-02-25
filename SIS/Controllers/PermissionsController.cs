using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Services;
using System.Linq;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class PermissionsController : Controller
    {
        private readonly IPermissionService _permissionService;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public PermissionsController(
            IPermissionService permissionService,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _permissionService = permissionService;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: Admin/Permissions/Index
        public async Task<IActionResult> Index()
        {
            var permissions = await _permissionService.GetAllPermissionsAsync();
            var roles = await _roleManager.Roles.ToListAsync();

            ViewBag.Roles = roles;
            return View(permissions);
        }

        // GET: Admin/Permissions/ManageRolePermissions/{roleId}
        public async Task<IActionResult> ManageRolePermissions(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Role not found";
                return RedirectToAction(nameof(Index));
            }

            var allPermissions = await _permissionService.GetAllPermissionsAsync();
            var rolePermissions = await _permissionService.GetRolePermissionsAsync(roleId);
            var rolePermissionIds = rolePermissions.Select(p => p.Id).ToList();

            var model = new ManageRolePermissionsViewModel
            {
                RoleId = roleId,
                RoleName = role.Name,
                AllPermissions = allPermissions,
                AssignedPermissionIds = rolePermissionIds
            };

            return View(model);
        }

        // POST: Admin/Permissions/UpdateRolePermissions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRolePermissions(string roleId, List<int> permissionIds)
        {
            var currentUser = User.Identity?.Name ?? "System";

            try
            {
                // Get current permissions
                var currentPermissions = await _context.RolePermissions
                    .Where(rp => rp.RoleId == roleId)
                    .ToListAsync();

                var currentPermissionIds = currentPermissions.Select(rp => rp.PermissionId).ToList();

                // Permissions to add
                var toAdd = (permissionIds ?? new List<int>()).Except(currentPermissionIds).ToList();

                // Permissions to remove
                var toRemove = currentPermissionIds.Except(permissionIds ?? new List<int>()).ToList();

                // Add new permissions
                foreach (var permissionId in toAdd)
                {
                    await _permissionService.AssignPermissionToRoleAsync(roleId, permissionId, currentUser);
                }

                // Remove old permissions
                foreach (var permissionId in toRemove)
                {
                    await _permissionService.RemovePermissionFromRoleAsync(roleId, permissionId);
                }

                TempData["SuccessMessage"] = "Role permissions updated successfully";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating permissions: {ex.Message}";
            }

            return RedirectToAction(nameof(ManageRolePermissions), new { roleId });
        }

        // GET: Admin/Permissions/UserPermissions/{userId}
        public async Task<IActionResult> UserPermissions(string userId)
        {
            var permissions = await _permissionService.GetUserPermissionsAsync(userId);
            return Json(permissions);
        }
    }

    public class ManageRolePermissionsViewModel
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public List<SIS.Models.Identity.Permission> AllPermissions { get; set; }
        public List<int> AssignedPermissionIds { get; set; }
    }
}
