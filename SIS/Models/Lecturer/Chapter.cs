using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Lecturer
{
    public class Chapter : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public virtual SIS.Models.Admin.Course Course { get; set; }

        [Required(ErrorMessage = "Chapter title is required")]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public int OrderIndex { get; set; } // For ordering chapters

        public bool IsActive { get; set; } = true;

        // Navigation property for related content
        public virtual ICollection<CourseContent> Contents { get; set; }
        public virtual ICollection<ChapterProgress> ChapterProgresses { get; set; }
    }
}
