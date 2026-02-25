namespace SIS.Models.ViewModels
{
    public class StudentListExportOptions
    {
        public string Title { get; set; } = "Student List";
        public List<string> SelectedColumns { get; set; } = new List<string>();
        public Dictionary<string, string> FilterSummary { get; set; } = new Dictionary<string, string>();
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = string.Empty;
        public int TotalRecords { get; set; }
    }

    public class ExportColumnOption
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public int Width { get; set; } = 100; // Width in PDF units
    }

    public class StudentListExportRequest
    {
        public List<string> SelectedColumns { get; set; } = new List<string>();
        public string Title { get; set; } = "Student List";
        public StudentListFiltersViewModel Filters { get; set; } = new StudentListFiltersViewModel();
    }

    public class StudentListExportResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
