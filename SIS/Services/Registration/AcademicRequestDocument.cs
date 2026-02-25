using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Registration
{
    public class AcademicRequestDocument
    {
        [Key]
        public int Id { get; set; }

        public int AcademicRequestId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; }

        public long FileSize { get; set; }

        [StringLength(100)]
        public string ContentType { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("AcademicRequestId")]
        public virtual AcademicRequest AcademicRequest { get; set; }
    }
}