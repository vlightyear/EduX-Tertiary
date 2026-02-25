using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SIS.Models.Assessments
{
    public class StudentResponse : AuditClass
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int StudentAttemptId { get; set; }
        [ForeignKey("StudentAttemptId")]
        public virtual StudentAttempt StudentAttempt { get; set; }
        [Required]
        public int QuestionId { get; set; }
        [ForeignKey("QuestionId")]
        public virtual Question Question { get; set; }
        public string ResponseText { get; set; } // Text response or selected option ID(s)
        public bool? IsCorrect { get; set; } // For auto-graded questions
        [Precision(10, 2)]
        public decimal? Score { get; set; }
        public string? FeedbackFromInstructor { get; set; }
        public bool IsGraded { get; set; } = false;
    }
}
