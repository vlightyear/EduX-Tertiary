using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Lecturer;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Models.StudentApplication;

namespace SIS.Models.Courses
{
    public class ChapterRating
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; } // This should match your Student table's primary key type

        [Required]
        public int ChapterId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars")]
        public int Rating { get; set; }

        [StringLength(500, ErrorMessage = "Review text cannot exceed 500 characters")]
        public string? ReviewText { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        // Note: Removed the ApplicationUser navigation since we're linking through Student table
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("ChapterId")]
        public virtual Chapter Chapter { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }
    }
}
