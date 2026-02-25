using SIS.Data;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Admin
{
    public class CourseLecturer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Course")]
        public int CourseId { get; set; } // The course
        public Course Course { get; set; } // Navigation property

        [Required]
        [ForeignKey("ApplicationUser")]
        public string LecturerId { get; set; } // The lecturer's User ID
        public ApplicationUser Lecturer { get; set; } // Navigation property
    }
}
