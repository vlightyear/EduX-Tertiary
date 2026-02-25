using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Identity;
using System.Security.Claims;

namespace SIS.Services
{
    public interface IPermissionService
    {
        Task<List<Permission>> GetAllPermissionsAsync();
        Task<List<Permission>> GetRolePermissionsAsync(string roleId);
        Task<bool> AssignPermissionToRoleAsync(string roleId, int permissionId, string grantedBy);
        Task<bool> RemovePermissionFromRoleAsync(string roleId, int permissionId);
        Task<bool> UserHasPermissionAsync(string userId, string permissionName);
        Task<List<string>> GetUserPermissionsAsync(string userId);
        Task<bool> RoleHasPermissionAsync(string roleId, string permissionName);
    }

    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PermissionService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<List<Permission>> GetAllPermissionsAsync()
        {
            return await _context.Permissions
                .Where(p => p.IsActive)
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<List<Permission>> GetRolePermissionsAsync(string roleId)
        {
            return await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission)
                .ToListAsync();
        }

        public async Task<bool> AssignPermissionToRoleAsync(string roleId, int permissionId, string grantedBy)
        {
            try
            {
                var exists = await _context.RolePermissions
                    .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

                if (exists)
                    return true; // Already assigned

                var rolePermission = new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = grantedBy
                };

                _context.RolePermissions.Add(rolePermission);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemovePermissionFromRoleAsync(string roleId, int permissionId)
        {
            try
            {
                var rolePermission = await _context.RolePermissions
                    .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

                if (rolePermission == null)
                    return true; // Already removed

                _context.RolePermissions.Remove(rolePermission);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UserHasPermissionAsync(string userId, string permissionName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            var userRoles = await _userManager.GetRolesAsync(user);

            var hasPermission = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Include(rp => rp.Role)
                .AnyAsync(rp => userRoles.Contains(rp.Role.Name) &&
                               rp.Permission.Name == permissionName &&
                               rp.Permission.IsActive);

            return hasPermission;
        }

        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return new List<string>();

            var userRoles = await _userManager.GetRolesAsync(user);

            var permissions = await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Include(rp => rp.Role)
                .Where(rp => userRoles.Contains(rp.Role.Name) && rp.Permission.IsActive)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToListAsync();

            return permissions;
        }

        public async Task<bool> RoleHasPermissionAsync(string roleId, string permissionName)
        {
            return await _context.RolePermissions
                .Include(rp => rp.Permission)
                .AnyAsync(rp => rp.RoleId == roleId &&
                               rp.Permission.Name == permissionName &&
                               rp.Permission.IsActive);
        }
    }
}