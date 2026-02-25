using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.StudentApplication;

namespace SIS.Repository
{
    public class CourseRepository : ICourseRepository
    {
        private readonly ApplicationDbContext _context;

        public CourseRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Course> GetByIdAsync(object value)
        {
            // Assuming "value" is an ID and Course has an Id property
            return await _context.Courses.FindAsync(value); // Adjust according to your schema
        }

        public async Task RegisterStudentForCourseAsync(int studentId, int courseId)
        {
            // Assuming you have a many-to-many relationship between Student and Course
            var studentCourse = new StudentCourse
            {
                StudentId = studentId,
                CourseId = courseId
            };

            // Add the student-course registration record to the database
            _context.StudentCourses.Add(studentCourse);
            await _context.SaveChangesAsync();
        }
    }

}
