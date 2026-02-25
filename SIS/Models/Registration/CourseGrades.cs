using SIS.Models.Admin;

namespace SIS.Models.Registration
{
    public class CourseGrades
    {
        public int CourseId { get; set; }
        public Course Course { get; set; }

        public int GradeId { get; set; }
        public Grade Grade { get; set; }
    }
}
