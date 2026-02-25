namespace SIS.Models.StudentApplication
{
    public class ProgrammeInvoiceViewModel
    {
        public int ProgrammeId { get; set; }
        public string ProgrammeName { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int YearOfStudy { get; set; }
        public string ApplicantName { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public List<ProgrammeFeeDetailViewModel> FeeDetails { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
    }

    public class ProgrammeFeeDetailViewModel
    {
        public string FeeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int YearApplicable { get; set; } // Added this to show which year the fee applies to
    }
}