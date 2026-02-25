namespace SIS.Models.Assessments
{
    public class AssessmentPreviewViewModel
    {
        public AssessmentConfiguration AssessmentConfig { get; set; }
        public List<AssessmentQuestionPreviewViewModel> Questions { get; set; }
        public bool IsRandomized { get; set; }
        public bool PreventTabSwitching { get; set; }
        public bool ShowResults { get; set; }
    }

    public class AssessmentQuestionPreviewViewModel
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string QuestionType { get; set; } // MultipleChoice, TrueFalse, ShortAnswer, Essay
        public string QuestionGroupName { get; set; }
        public string Instructions { get; set; }

        public string ImagePath { get; set; }
        public string ImageDescription { get; set; }
        public string ImageDisplayPosition { get; set; }
        public List<QuestionOptionViewModel> Options { get; set; }
    }

    public class QuestionOptionViewModel
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
    }
}
