namespace SIS.Models.StudentApplication
{
    public class PhotoValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string Message { get; set; } = string.Empty;
    }
}