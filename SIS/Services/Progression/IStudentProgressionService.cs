using SIS.Models.Admin;
using SIS.Models.StudentApplication;

namespace SIS.Services.Progression

{
    public interface IStudentProgressionService
    {
        /// <summary>
        /// Validates if a student is eligible for progression
        /// </summary>
        Task<ProgressionResult> ValidateProgressionAsync(int studentId, int currentAcademicYearId);

        /// <summary>
        /// Executes the progression for a student
        /// </summary>
        Task<ProgressionResult> ExecuteProgressionAsync(int studentId, int currentAcademicYearId, string userId);
        Task<ProgressionRule?> GetApplicableProgressionRuleAsync(
            Student student,
            int totalFailedCourses,
            int? semester = null,
            int? attempt = null);
        Task<List<GradeConfiguration>?> GetGradeConfigurationAsync(
            int? schoolId = null,
            int? academicYearId = null);
    }

    public class ProgressionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();
        public int? NextAcademicYearId { get; set; }
        public int? NextYearOfStudy { get; set; }
        public int? NextSemester { get; set; }
    }
}