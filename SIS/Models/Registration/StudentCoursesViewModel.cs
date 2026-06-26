namespace SIS.Models.Registration
{
    public class StudentCoursesViewModel
    {
        public int Id { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string CourseDescription { get; set; }
        public bool IsMandatory { get; set; }
        public int? YearPeriodId { get; set; }
        public string YearPeriodLabel { get; set; } = "Annual";
    }
}
