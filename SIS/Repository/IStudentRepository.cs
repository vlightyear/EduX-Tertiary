
using SIS.Data;
using SIS.Models.StudentApplication;

namespace SIS.Repository
{
    public interface IStudentRepository
    {
        Task<Student> GetByIdAsync(int studentId);
        Task<Student> GetByEmailAsync(string email);
    }
}
