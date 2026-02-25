using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Assessments
{
    public class QuestionOption : AuditClass
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int QuestionId { get; set; }
        [ForeignKey("QuestionId")]
        public virtual Question Question { get; set; }
        [Required]
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
    }
}
