using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SIS.Models.Assessments
{
    public class StudentAttempt : AuditClass
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string StudentId { get; set; }
        [Required]
        public int AssessmentConfigurationId { get; set; }
        [ForeignKey("AssessmentConfigurationId")]
        public virtual AssessmentConfiguration AssessmentConfiguration { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } // "InProgress", "Submitted", "TimedOut", "Unattempted"
        [Precision(10, 2)]
        public decimal? TotalScore { get; set; }
        [Precision(5, 2)]
        public decimal? Percentage { get; set; }
        public bool? Passed { get; set; }
        public string? FeedbackFromInstructor { get; set; }
        public virtual ICollection<StudentResponse> Responses { get; set; } = new List<StudentResponse>();
    }
}
