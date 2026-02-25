using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Admin
{
    public class CoursePrerequisite
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Course")]
        public int CourseId { get; set; } // The course that requires the prerequisite
        public Course? Course { get; set; } // Navigation property

        [Required]
        [ForeignKey("PrerequisiteCourse")]
        public int PrerequisiteCourseId { get; set; } // The prerequisite course
        public Course? PrerequisiteCourse { get; set; } // Navigation property
    }
}
