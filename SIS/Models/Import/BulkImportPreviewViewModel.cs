using SIS.Models.Assessments;

namespace SIS.Models.Import
{
    public class BulkImportPreviewViewModel
    {
        public int QuestionGroupId { get; set; }
        public QuestionGroup QuestionGroup { get; set; }
        public List<QuestionImportModel> ImportedQuestions { get; set; }
        public int TotalQuestions { get; set; }
        public int ValidQuestions { get; set; }
        public int InvalidQuestions { get; set; }
        public List<string> GlobalErrors { get; set; }

        public BulkImportPreviewViewModel()
        {
            ImportedQuestions = new List<QuestionImportModel>();
            GlobalErrors = new List<string>();
        }

        public void CalculateStatistics()
        {
            TotalQuestions = ImportedQuestions.Count;
            ValidQuestions = ImportedQuestions.Count(q => q.IsValid);
            InvalidQuestions = ImportedQuestions.Count(q => !q.IsValid);
        }
    }
}