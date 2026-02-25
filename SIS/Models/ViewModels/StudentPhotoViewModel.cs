namespace SIS.Models.ViewModels
{
    public class StudentPhotoViewModel
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string StudentName { get; set; }
        public string? CurrentPhotoPath { get; set; }
        public bool HasExistingPhoto { get; set; }
        public DateTime? IdCardPrintedDate { get; set; }
    }
}
