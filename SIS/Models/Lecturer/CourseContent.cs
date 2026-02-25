using SIS.Data;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Lecturer
{
    public class CourseContent : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public virtual SIS.Models.Admin.Course Course { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [StringLength(255)]
        public string FilePath { get; set; }

        [Required]
        [StringLength(100)]
        public string FileName { get; set; }

        [Required]
        [StringLength(50)]
        public string FileType { get; set; }

        [Required]
        public long FileSize { get; set; }

        [StringLength(20)]
        public string FileSizeFormatted { get; set; }

        public int ViewCount { get; set; } = 0;

        public int DownloadCount { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        [StringLength(100)]
        public string ContentCategory { get; set; } = "General"; // e.g., "Lecture Notes", "Assignment", "Reading Material"

        public int? SortOrder { get; set; }

        public int? ChapterId { get; set; }

        [ForeignKey("ChapterId")]
        public virtual Chapter Chapter { get; set; }

        [NotMapped]
        public IFormFile UploadFile { get; set; }
    }
}
