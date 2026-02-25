namespace SIS.Models.Admin
{
    public class StudentResultsViewModel
    {
        public decimal OutstandingFees { get; set; }
        public decimal OverallGPA { get; set; }
        public List<AcademicYearResults> AcademicYears { get; set; } = new List<AcademicYearResults>();

        // Add grade configurations for grade determination
        public List<GradeConfiguration> Grades { get; set; } = new List<GradeConfiguration>();

        // Add course pass marks for pass/fail determination
        public Dictionary<int, double> CoursePassMarks { get; set; } = new Dictionary<int, double>();

        // Keep for backward compatibility but make it dynamic
        public List<string> AllAssessmentTypes { get; set; } = new List<string>();

        // New property to check if student can view complete results
        public bool CanViewCompleteResults { get; set; } = false; //=> OutstandingFees <= 0;

        // New properties for transcript and multi-year support
        public int TotalCreditsAttempted { get; set; }
        public int TotalCreditsEarned { get; set; }
        public int? SelectedAcademicYearId { get; set; }
    }

    public class AcademicYearResults
    {
        public int YearId { get; set; }
        public string YearValue { get; set; }
        public decimal GPA { get; set; }
        public int CreditsAttempted { get; set; }
        public int CreditsEarned { get; set; }
        public int FailedCourses { get; set; }
        public string AcademicStanding { get; set; }
        public List<SemesterResults> Semesters { get; set; } = new List<SemesterResults>();

        public int GetTotalCourses()
        {
            return Semesters.Sum(s => s.Courses.Count(c => c.IsPublished));
        }

        public int GetPassedCourses()
        {
            return Semesters.Sum(s => s.Courses.Count(c => c.IsPublished && c.IsPassed && c.CanViewComplete));
        }

        // Check if any complete results can be viewed
        public bool HasCompleteResults()
        {
            return Semesters.SelectMany(s => s.Courses).Any(c => c.CanViewComplete);
        }
    }

    /*public class SemesterResults
    {
        public int SemesterId { get; set; }
        public List<CourseResult> Courses { get; set; } = new List<CourseResult>();
    }*/

    public class SemesterResults
    {
        public int SemesterId { get; set; }
        public List<CourseResult> Courses { get; set; } = new List<CourseResult>();

        // New semester-level properties
        public decimal GPA { get; set; }
        public int CreditsAttempted { get; set; }
        public int CreditsEarned { get; set; }
        public int FailedCourses { get; set; }
        public string ProgressionStatus { get; set; }
        public string AcademicStanding { get; set; }
    }

    public class CourseResult
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public int Credits { get; set; }

        // Changed from AssessmentScores to Scores
        public Dictionary<string, AssessmentScoreInfo> Scores { get; set; } = new Dictionary<string, AssessmentScoreInfo>();

        public decimal? TotalScore { get; set; }  // Changed from Total
        public string Grade { get; set; }
        public string Remark { get; set; }
        public bool IsPublished { get; set; }
        public bool IsPassed { get; set; }

        // Changed from CanViewCompleteResults to CanViewComplete
        public bool CanViewComplete { get; set; }

        // Helper methods for dynamic assessment handling
        public List<string> GetAssessmentTypes()
        {
            return Scores.Keys.ToList();
        }

        // Get non-exam assessments (always visible)
        public List<string> GetNonExamAssessmentTypes()
        {
            return Scores.Keys.Where(k => !k.Equals("Exam", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Get exam assessments (only visible when complete results can be viewed)
        public List<string> GetExamAssessmentTypes()
        {
            return Scores.Keys.Where(k => k.Equals("Exam", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public List<StudentAssessmentInfo> GetAssessmentInfos()
        {
            return Scores.Select(kvp => new StudentAssessmentInfo
            {
                Name = kvp.Key,
                Score = kvp.Value.Score,
                WeightPercentage = kvp.Value.WeightPercentage
            }).ToList();
        }

        // Get the reason why complete results are not visible
        public string GetResultsHiddenReason(bool hasOutstandingFees)
        {
            if (hasOutstandingFees && !IsPublished)
                return "Clear outstanding fees and await publication to view complete results";
            else if (hasOutstandingFees)
                return "Clear outstanding fees to view complete results";
            else if (!IsPublished)
                return "Results pending publication";
            else
                return "";
        }
    }

    public class AssessmentScoreInfo
    {
        public decimal Score { get; set; }
        public decimal WeightPercentage { get; set; }
    }

    public class StudentAssessmentInfo
    {
        public string Name { get; set; }
        public decimal Score { get; set; }
        public decimal WeightPercentage { get; set; }
    }
}