namespace SIS.Models.Admin
{
    public class CourseAssessmentViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string ProgrammeName { get; set; }
        public int EnrolledStudentsCount { get; set; }
        public string AssessmentsString { get; set; }
        public string AssessmentStatus { get; set; }
        public int PendingAssessments { get; set; }
        public bool IsHOD { get; set; }
        public int SelectedYearId { get; set; }
        public List<AcademicYearCourses> AcademicYears { get; set; } = new List<AcademicYearCourses>();
    }

    public class AcademicYearCourses
    {
        public int YearId { get; set; }
        public string YearValue { get; set; }
        public bool IsPublished { get; set; }
        public List<CourseDetails> Courses { get; set; } = new List<CourseDetails>();
    }

    public class CourseDetails
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string ProgrammeName { get; set; }
        public int EnrolledStudentsCount { get; set; }
        public string AssessmentsString { get; set; }
        public string AssessmentStatus { get; set; }
        public int PendingAssessments { get; set; }
        public int TotalAssessments { get; set; }  // New property
        public int CompletedAssessments { get; set; }  // New property
        public int Semester { get; set; }
        public double GradedPercentage { get; set; }
    }


    public class AssessmentScoreUpdate
    {
        public string AssessmentName { get; set; }
        public decimal Score { get; set; }
    }

}
