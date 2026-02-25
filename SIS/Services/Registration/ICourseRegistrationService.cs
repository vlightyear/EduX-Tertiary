using SIS.Models.Admin;

namespace SIS.Services.Registration
{
    public interface ICourseRegistrationService
    {
        Task<List<Course>> GetStudentCoursesAsync();
        Task<string> RegisterCoursesAsync(int studentId, List<int> courseIds);

    }
}