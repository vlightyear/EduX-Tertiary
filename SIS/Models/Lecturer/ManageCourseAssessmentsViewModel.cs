using SIS.Models.Admin;
using SIS.Models.Assessments;

namespace SIS.Models.Lecturer
{
    public class ManageCourseAssessmentsViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string ProgrammeName { get; set; }
        public string DepartmentName { get; set; }
        public List<Assessment> CurrentAssessments { get; set; }
        public List<Assessment> AvailableAssessments { get; set; }
        public decimal TotalWeightPercentage { get; set; }
        public string WeightStatus { get; set; } // "perfect", "under", "over"
    }
}