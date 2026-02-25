using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Admin
{
    public class CourseAssessment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CourseId { get; set; }
        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [Required]
        public int AssessmentId { get; set; }
        [ForeignKey("AssessmentId")]
        public virtual Assessment Assessment { get; set; }
    }
}
