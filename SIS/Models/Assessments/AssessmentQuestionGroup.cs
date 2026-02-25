using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Assessments
{
    public class AssessmentQuestionGroup
    {
        [Key]
        public int Id { get; set; }
        public int AssessmentConfigurationId { get; set; }
        [ForeignKey("AssessmentConfigurationId")]
        public virtual AssessmentConfiguration AssessmentConfiguration { get; set; }
        public int QuestionGroupId { get; set; }
        [ForeignKey("QuestionGroupId")]
        public virtual QuestionGroup QuestionGroup { get; set; }
        public int NumberOfQuestionsToUse { get; set; } // If random selection is enabled
    }
}
