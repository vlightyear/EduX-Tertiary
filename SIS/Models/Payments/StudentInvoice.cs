using SIS.Models.Fees;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using SIS.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Payments
{
    public class StudentInvoice
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string InvoiceReference { get; set; } = string.Empty;
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        public DateTime CreatedDate { get; set; }
        public int AcademicYearId { get; set; }
        public Status Status { get; set; } = Status.Pending;
        public string? BatchReference { get; set; }
         public string? Description { get; set; }
        public string? TransactionType { get; set; }
        public int? YearPeriodId { get; set; }
        public DateTime? DeletedAt {  get; set; }
        public DateTime? CreatedAt {  get; set; }
        public string? CreatedBy {  get; set; }
        public DateTime? UpdatedAt {  get; set; }
        public string? UpdatedBy {  get; set; }

        // Navigation properties
        public Student Student { get; set; }
        public AcademicYear AcademicYear { get; set; }
        public string? AccountingSystemPostStatus { get; set; } = "Pending";
        public List<StudentInvoiceItem> InvoiceItems { get; set; } = new();
        public virtual ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
        public virtual ICollection<OnlinePayments> OnlinePayments { get; set; } = new List<OnlinePayments>();

        // Computed properties (not mapped to database)
        [NotMapped]
        public decimal TotalAllocated => PaymentAllocations?.Sum(pa => pa.AllocatedAmount) ?? 0;

        [NotMapped]
        public decimal RemainingBalance => TotalAmount - TotalAllocated;

        [NotMapped]
        public bool IsFullyPaid => RemainingBalance <= 0;

        [NotMapped]
        public bool IsPartiallyPaid => TotalAllocated > 0 && RemainingBalance > 0;
    }

    public class StudentInvoiceItem
    {
        public int Id { get; set; }
        public int StudentInvoiceId { get; set; }
        public string FeeTypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        public int? FeeConfigurationId { get; set; } // Reference to original fee config

        // Navigation properties
        public StudentInvoice StudentInvoice { get; set; }
        public FeeConfiguration FeeConfiguration { get; set; }
    }
}
