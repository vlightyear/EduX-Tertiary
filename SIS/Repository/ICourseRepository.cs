using SIS.Models.Admin;

namespace SIS.Repository
{
    public interface ICourseRepository
    {
        Task<Course> GetByIdAsync(object value); // This should return a Course (or whatever type you're using)
        Task RegisterStudentForCourseAsync(int studentId, int courseId); // No return value (void)
    }

}
