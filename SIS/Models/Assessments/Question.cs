using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SIS.Models.Assessments
{
    public class Question : AuditClass
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int QuestionGroupId { get; set; }
        [ForeignKey("QuestionGroupId")]
        public virtual QuestionGroup QuestionGroup { get; set; }
        [Required]
        public string QuestionText { get; set; }
        [Required]
        public string QuestionType { get; set; } // "MultipleChoice", "ShortAnswer", "LongText", etc.
        [Precision(10, 2)]
        public decimal Points { get; set; }
        public string? AdditionalInfo { get; set; } // Instructions, hints, etc.
        public bool IsActive { get; set; } = true;

        // Image-related fields
        public string? ImagePath { get; set; }
        public string? ImageDescription { get; set; } // For accessibility (alt text)
        public string? ImageDisplayPosition { get; set; } = "Above"; // Default position: "Above" or "Below" question text
        public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>(); // For multiple choice
    }
}
