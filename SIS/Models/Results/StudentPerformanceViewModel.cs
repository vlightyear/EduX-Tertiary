namespace SIS.Models.Results
{
    public class StudentPerformanceViewModel
    {
        public List<CoursePerformanceViewModel> Courses { get; set; }
        public decimal YearGPA { get; set; }
        public decimal OverallGPA { get; set; }
        public int TotalFailedCourses { get; set; }
    }

    public class CoursePerformanceViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public Dictionary<string, AssessmentScore> Scores { get; set; }
        public string Status { get; set; }
        public decimal TotalScore { get; set; }
    }

    public class AssessmentScore
    {
        public string AssessmentName { get; set; }
        public decimal Score { get; set; }
    }

    public class ScoreData
    {
        public string assessment_name { get; set; }
        public decimal score { get; set; }
    }
}
