using SIS.Services.StudentImport;

namespace SIS.Models.ViewModels
{
    public class StudentImportPreviewViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int TotalRows { get; set; }
        public int ValidStudentCount => ValidStudents?.Count ?? 0;
        public int InvalidStudentCount => InvalidStudents?.Count ?? 0;
        public List<StudentImportDto> ValidStudents { get; set; } = new List<StudentImportDto>();
        public List<StudentImportDto> InvalidStudents { get; set; } = new List<StudentImportDto>();
        public List<StudentValidationResult> ValidationResults { get; set; } = new List<StudentValidationResult>();

        // Additional properties for the view
        public bool HasErrors => InvalidStudents?.Any() == true;
        public bool HasValidData => ValidStudents?.Any() == true;
        public double ValidationSuccessRate => TotalRows > 0 ? (double)ValidStudentCount / TotalRows * 100 : 0;
    }
}
