using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.Lecturer;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Assessments
{
    public class AssessmentConfiguration : AuditClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AssessmentId { get; set; }

        [ForeignKey("AssessmentId")]
        public virtual Assessment Assessment { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [Required]
        public int AcademicYearId { get; set; }

        [ForeignKey("AcademicYearId")]
        public virtual AcademicYear AcademicYear { get; set; }

        [Required]
        public int ModeOfStudyId { get; set; }

        [ForeignKey("ModeOfStudyId")]
        public virtual ModeOfStudy ModeOfStudy { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public DateTime EndDateTime { get; set; }

        [Required]
        public int DurationMinutes { get; set; }

        public bool RandomizeQuestions { get; set; }

        public bool PreventTabSwitching { get; set; }

        public bool ShowResults { get; set; }

        public bool IsPublished { get; set; } = false;

        public int? ChapterId { get; set; }

        [ForeignKey("ChapterId")]
        public virtual Chapter Chapter { get; set; }

        public virtual ICollection<AssessmentQuestionGroup> QuestionGroups { get; set; } = new List<AssessmentQuestionGroup>();
    }
}