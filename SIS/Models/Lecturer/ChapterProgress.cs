using SIS.Models.Admin;
using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Lecturer
{
    public class ChapterProgress
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int ChapterId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public bool IsCompleted { get; set; }

        public DateTime? CompletedDate { get; set; }

        // Track if assessment was attempted (if applicable)
        public bool AssessmentAttempted { get; set; }

        // Navigation properties
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("ChapterId")]
        public virtual Chapter Chapter { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
