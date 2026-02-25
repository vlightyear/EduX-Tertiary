using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Admin
{
    public class ProgrammeCourse
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Programme")]
        public int ProgrammeId { get; set; }
        public Programme Programme { get; set; } // Navigation property

        [Required]
        [ForeignKey("Course")]
        public int CourseId { get; set; }
        public Course Course { get; set; } // Navigation property
    }
}
