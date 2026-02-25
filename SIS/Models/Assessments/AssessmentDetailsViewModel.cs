
namespace SIS.Models.Assessments
{
    public class AssessmentDetailsViewModel
    {
        public AssessmentConfiguration Assessment { get; set; }
        public StudentAttempt StudentAttempt { get; set; }
        public TimeSpan TimeRemaining { get; set; } // Time until assessment window closes
        public TimeSpan? AttemptTimeRemaining { get; set; } // Time remaining for an in-progress attempt
        public bool HasExistingAttempt { get; set; }
        public bool CanStart { get; set; }
        public string ActionButtonText { get; set; }
        public string ActionButtonUrl { get; set; }
        public string TimeExpiryWarning { get; set; }
    }
}