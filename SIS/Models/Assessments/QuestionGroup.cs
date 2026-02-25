using SIS.Models.Admin;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Assessments
{
    public class QuestionGroup : AuditClass
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public int CourseId { get; set; }
        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }
        public string? Topics { get; set; }
        public string? Description { get; set; }
        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}
