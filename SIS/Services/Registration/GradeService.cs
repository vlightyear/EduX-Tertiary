using SIS.Repository;
using System.Text;

namespace SIS.Services.Registration
{
    public class GradeService : IGradeService
    {
        private readonly IStudentRepository _studentRepository;

        public GradeService(IStudentRepository studentRepository)
        {
            _studentRepository = studentRepository;
        }

        public async Task<string> GetGradesAsync(int studentId)
        {
            var student = await _studentRepository.GetByIdAsync(studentId);

            // Check payment status before showing grades
            if (!student.HasPaidFullFees)
            {
                return "Grades are restricted until full payment is made.";
            }

            // Return grades if payment is complete
            var grades = student.CourseGrades.Select(g => new { g.Course.CourseName, g.Grade });
            if (!grades.Any())
            {
                return "No grades available.";
            }

            var gradesList = new StringBuilder();
            foreach (var grade in grades)
            {
                gradesList.AppendLine($"{grade.CourseName}: {grade.Grade.Code}");
            }

            return gradesList.ToString();
        }
    }

}
