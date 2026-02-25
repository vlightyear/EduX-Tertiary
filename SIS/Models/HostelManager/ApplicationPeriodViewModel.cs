using SIS.Enums;

namespace SIS.Models.HostelManager
{
    public class ApplicationPeriodViewModel
    {
        public int PeriodId { get; set; }
        public string AcademicYearName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Type { get; set; }
        public DateTime ApplicationStartDate { get; set; }
        public DateTime ApplicationEndDate { get; set; }
        public Status Status { get; set; }
        public string StatusString => Status.ToString();
        public int ApplicationCount { get; set; }
        public bool IsPermanentUntilGraduation { get; set; }
        public bool IsUniversal { get; set; }

        // Helper properties for UI
        public string DateRange => $"{StartDate:dd/MM/yyyy} - {(EndDate.HasValue ? EndDate.Value.ToString("dd/MM/yyyy") : "Until Graduation")}";
        public string ApplicationDateRange => $"{ApplicationStartDate:dd/MM/yyyy} - {ApplicationEndDate:dd/MM/yyyy}";
        public string StatusBadgeClass => GetStatusBadgeClass(Status);

        private string GetStatusBadgeClass(Status status)
        {
            return status switch
            {
                Status.Active => "badge bg-success",
                Status.Upcoming => "badge bg-warning",
                Status.Closed => "badge bg-danger",
                _ => "badge bg-secondary"
            };
        }
    }

    public class ApplicationListViewModel
    {
        public int PeriodId { get; set; }
        public string AcademicYearName { get; set; }
        public string PeriodDateRange { get; set; }
        public Status PeriodStatus { get; set; }
        public List<StudentApplicationViewModel> Applications { get; set; } = new List<StudentApplicationViewModel>();
    }

    public class StudentApplicationViewModel
    {
        public int ApplicationId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string MatricNumber { get; set; }
        public string Gender { get; set; }
        public string Programme { get; set; }
        public int? YearOfStudy { get; set; }
        public DateTime ApplicationDate { get; set; }
        public Status Status { get; set; }
        public string StatusString => Status.ToString();
        public bool IsInternational { get; set; }
        public bool HasAllocation { get; set; }

        // Helper properties for UI
        public string StatusBadgeClass => GetStatusBadgeClass(Status);

        private string GetStatusBadgeClass(Status status)
        {
            return status switch
            {
                Status.Submitted => "badge bg-info",
                Status.Approved => "badge bg-success",
                Status.Rejected => "badge bg-danger",
                Status.WaitListed => "badge bg-warning",
                _ => "badge bg-secondary"
            };
        }
    }
}
