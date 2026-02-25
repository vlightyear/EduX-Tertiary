using Microsoft.AspNetCore.Identity;
using SIS.Data;

namespace SIS.Services.Users
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public Task<ApplicationUser?> GetUserByIdAsync(string id)
        {
            throw new NotImplementedException();
        }

        public async Task<List<ApplicationUser>> GetUsersWithRegistrarRoleAsync()
        {
            // Get the "Admin" role from the RoleManager
            var adminRole = await _roleManager.FindByNameAsync("Registrar");

            if (adminRole == null)
            {
                return new List<ApplicationUser>(); // Return empty list if the Admin role doesn't exist
            }

            // Get all users with the "Admin" role
            var usersInAdminRole = await _userManager.GetUsersInRoleAsync(adminRole.Name);

            return usersInAdminRole.ToList(); // Return the list of users with Admin role
        }
    }
}
