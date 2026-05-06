namespace SIS.Models.ViewModels
{
    public class AdminCourseViewModel
    {
        public int Id { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string CourseDescription { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsExaminable { get; set; }
        public bool IsSelected { get; set; }
        public bool IsCarryover { get; set; }
        public int YearTaken { get; set; }
        public int PeriodTakenId { get; set; }
        public string PeriodTakenLabel { get; set; }
        public string? CarryoverReason { get; set; }
    }

    public class AdminRegistrationModel
    {
        public int StudentId { get; set; }
        public List<int> SelectedCourseIds { get; set; } = new List<int>();
        public string? Reason { get; set; }
    }

    public class CourseRequirementsForAdmin
    {
        public int TotalRequiredCourses { get; set; }
        public int MinimumElectives { get; set; }
        public int MaximumElectives { get; set; }
        public int CarryoverCoursesCount { get; set; }
    }
}