namespace SIS.Models.Assessments
{
    // View Model for the Take action
    public class AssessmentTakeViewModel
    {
        public StudentAttempt StudentAttempt { get; set; }
        public StudentResponse CurrentResponse { get; set; }
        public Question CurrentQuestion { get; set; }
        public int CurrentIndex { get; set; }
        public int TotalQuestions { get; set; }
        public TimeSpan TimeRemaining { get; set; }
        public bool RequiresManualGrading { get; set; }
        public List<ResponseNavigationItem> ResponsesNavigation { get; set; }
    }

    public class ResponseNavigationItem
    {
        public int Index { get; set; }
        public int QuestionId { get; set; }
        public bool HasResponse { get; set; }
        public bool IsCurrentQuestion { get; set; }
    }

    // View Model for the Results action
    public class AssessmentResultsViewModel
    {
        public StudentAttempt StudentAttempt { get; set; }
        public List<StudentResponse> Responses { get; set; }
        public int TotalQuestions { get; set; }
        public int TotalAnswered { get; set; }
        public int TotalCorrect { get; set; }
        public decimal TotalScore { get; set; }
        public decimal Percentage { get; set; }
        public TimeSpan TimeTaken { get; set; }
        public bool RequiresManualGrading { get; set; }
        public bool HasUnGradedResponses { get; set; }
        public decimal PassMark { get; set; }
    }


}
