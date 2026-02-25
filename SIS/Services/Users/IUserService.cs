using SIS.Data;

namespace SIS.Services.Users
{
    public interface IUserService
    {
        Task<List<ApplicationUser>> GetUsersWithRegistrarRoleAsync();
        Task<ApplicationUser?> GetUserByIdAsync(string id);
    }
}
