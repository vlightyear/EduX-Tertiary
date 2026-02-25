using SIS.Models.Fees;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

public class FinancialStatement
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int StudentId { get; set; }

    [Required]
    public int AcademicYearId { get; set; }

    public int? Semester { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingAmount { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; }

    [Required]
    public string PaymentMethod { get; set; }

    [Required]
    public string TransactionReference { get; set; }  // Added this field

    // Navigation properties
    public virtual Student Student { get; set; }
    public virtual AcademicYear AcademicYear { get; set; }
}