namespace SIS.Models.Registration
{
    public class CourseRequirements
    {
        public int TotalRequiredCourses { get; set; }
        public int MinimumElectives { get; set; }
        public int MaximumElectives { get; set; }
        public int CarryoverCoursesCount { get; set; } = 0;
    }
}
