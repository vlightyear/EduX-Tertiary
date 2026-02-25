namespace SIS.Models.Import
{
    public class QuestionImportModel
    {
        public int TemporaryId { get; set; }
        public int LineNumber { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = "MultipleChoice"; // Default to MultipleChoice
        public decimal Points { get; set; } = 1;
        public string AdditionalInfo { get; set; } = string.Empty;

        // Multiple Choice & True/False
        public List<ImportOptionModel> Options { get; set; } = new List<ImportOptionModel>();

        // True/False specific
        public bool? TrueFalseAnswer { get; set; }

        // Short Answer & Long Text specific
        public string ExpectedAnswer { get; set; } = string.Empty;
        public int? MaxLength { get; set; }
        public int? MinLength { get; set; }
        public string ExpectedKeywords { get; set; } = string.Empty;

        // Validation
        public bool IsValid { get; set; } = false;
        public List<string> ValidationErrors { get; set; } = new List<string>();

        public int GetCorrectAnswersCount()
        {
            return Options.Count(o => o.IsCorrect);
        }

        public string GetQuestionTypeDisplay()
        {
            return QuestionType switch
            {
                "MultipleChoice" => "Multiple Choice",
                "TrueFalse" => "True/False",
                "ShortAnswer" => "Short Answer",
                "LongText" => "Long Text",
                _ => "Unknown"
            };
        }
    }


}