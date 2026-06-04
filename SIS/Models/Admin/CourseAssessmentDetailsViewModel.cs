namespace SIS.Models.Admin
{
    // Main view model for the course assessment details page
    public class CourseAssessmentDetailsViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }

        // Adding new properties for the tabbed interface
        public List<AssessmentGroup> AssessmentGroups { get; set; } = new List<AssessmentGroup>();
        public List<AssessmentInfo> Assessments { get; set; } = new List<AssessmentInfo>();

        // Keeping original property for backward compatibility
        public List<StudentAssessment> StudentAssessments { get; set; } = new List<StudentAssessment>();

        // New property for grade configurations
        public List<GradeConfiguration> Grades { get; set; } = new List<GradeConfiguration>();
    }

    // New class to represent a group/tab of students
    public class AssessmentGroup
    {
        public string GroupId { get; set; }
        public int AcademicYearId { get; set; }
        public int? Semester { get; set; }
        public string AcademicYear { get; set; }
        public string ModeOfStudy { get; set; }
        public List<StudentAssessment> StudentAssessments { get; set; } = new List<StudentAssessment>();
    }

    // New class to hold assessment information
    public class AssessmentInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int WeightPercentage { get; set; }
    }

    // Existing class, updated with weighted calculation
    public class StudentAssessment
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public Dictionary<int, AssessmentScore> Scores { get; set; } = new Dictionary<int, AssessmentScore>();

        // New method to calculate weighted total
        public decimal CalculateWeightedTotal()
        {
            decimal total = 0;
            foreach (var score in Scores.Values)
            {
                total += score.Score * score.WeightPercentage / 100;
            }
            return Math.Round(total, 1);
        }
    }

    // Existing class, updated with weight percentage
    public class AssessmentScore
    {
        public string AssessmentName { get; set; }
        public decimal Score { get; set; }
        // New property for weight
        public int WeightPercentage { get; set; }
        public bool IsTampered { get; set; }
        public string? TamperDetails { get; set; }
        public int? ScoreId { get; set; }
    }

    // Existing class
    public class UpdateScoresRequest
    {
        public List<StudentScoreUpdateModel> Updates { get; set; }
    }

    // Existing class
    public class StudentScoreUpdateModel
    {
        public int StudentId { get; set; }
        public int CourseId { get; set; }
        public int AcademicYearId { get; set; }  // NEW
        public int? Semester { get; set; }         // NEW
        public Dictionary<int, AssessmentScoreData> AssessmentScores { get; set; }
    }

    public class AssessmentScoreData
    {
        public decimal Score { get; set; }
        public decimal WeightPercentage { get; set; }
    }

    // Missing class from your provided code, adding for completeness
    //public class AssessmentScoreUpdate
    //{
    //    public string AssessmentName { get; set; }
    //    public decimal Score { get; set; }
    //}
}