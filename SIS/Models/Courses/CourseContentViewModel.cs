using SIS.Models.Lecturer;

namespace SIS.Models.Courses
{
    public class CourseContentViewModel
    {
        public int Id { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string CourseDescription { get; set; }
        public bool IsMandatory { get; set; }
        // Dictionary with category as key and list of content as value
        public Dictionary<string, List<CourseContent>> GroupedContent { get; set; }
    }
}
