using SIS.Models.Admin;
using SIS.Repository;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace SIS.Services.Registration
{
    public class CourseRegistrationService : ICourseRegistrationService
    {
        private readonly ICourseRepository _courseRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CourseRegistrationService(ICourseRepository courseRepository, IStudentRepository studentRepository, IHttpContextAccessor httpContextAccessor)
        {
            _courseRepository = courseRepository;
            _studentRepository = studentRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<List<Course>> GetStudentCoursesAsync()
        {
            var userEmail = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                // Handle case where email is not found
                throw new Exception("User email is not available.");
            }

            // Retrieve the student by email
            var student = await _studentRepository.GetByEmailAsync(userEmail);

            if (student == null)
            {
                throw new Exception("Student not found.");
            }

            // Flatten the ProgrammeCourses into a single list of Courses
            var studentCourses = student.Programme.ProgrammeCourses
                .Select(pc => pc.Course)
                .ToList();

            return studentCourses;
        }


        public async Task<string> RegisterCoursesAsync(int studentId, List<int> courseIds)
        {
            var student = await _studentRepository.GetByIdAsync(studentId);

            // Check if student is registered and has paid necessary fees
            if (!student.IsRegistered)
            {
                return "Student is not registered yet.";
            }

            if (!student.HasPaidFullFees)
            {
                return "Course registration is restricted until full payment is made.";
            }

            var courseRegistrationResult = new List<string>();

            foreach (var courseId in courseIds)
            {
                var course = await _courseRepository.GetByIdAsync(courseId);

                // Check if the course exists
                if (course == null)
                {
                    courseRegistrationResult.Add($"Course with ID {courseId} does not exist.");
                    continue;
                }

                // Register the student for the course
                await _courseRepository.RegisterStudentForCourseAsync(studentId, courseId); // No variable needed
                courseRegistrationResult.Add($"{course.CourseName} successfully registered.");
            }

            return string.Join("\n", courseRegistrationResult);
        }
    }


}
