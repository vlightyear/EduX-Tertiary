using SIS.Models.Assessments;

namespace SIS.Models.Lecturer
{
    public class LecturerAssessmentDetailsViewModel
    {
        public AssessmentConfiguration AssessmentConfig { get; set; }
        public int TotalStudents { get; set; }
        public int TotalAttempts { get; set; }
        public int CompletedAttempts { get; set; }
        public decimal AverageScore { get; set; }
        public decimal PassRate { get; set; }
        public List<StudentAssessmentStatusViewModel> StudentAttempts { get; set; }
        public List<StudentGroupViewModel> GroupedStudents { get; set; }
    }

    public class StudentAssessmentStatusViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public bool HasAttempted { get; set; }
        public string Status { get; set; }
        public decimal? Score { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? AttemptId { get; set; }
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; }
        public int ModeOfStudyId { get; set; }
        public string ModeOfStudyName { get; set; }
    }

    public class StudentGroupViewModel
    {
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; }
        public int ModeOfStudyId { get; set; }
        public string ModeOfStudyName { get; set; }
        public string GroupLabel { get; set; }
        public List<StudentAssessmentStatusViewModel> Students { get; set; }
    }

    public class GradeAttemptViewModel
    {
        public StudentAttempt StudentAttempt { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public bool NeedsGrading { get; set; }
        public List<StudentResponse> Responses { get; set; }
    }
    public class GradeResponse
    {
        public bool? IsCorrect { get; set; }
        public decimal? Score { get; set; }
        public string Feedback { get; set; }
    }
}
