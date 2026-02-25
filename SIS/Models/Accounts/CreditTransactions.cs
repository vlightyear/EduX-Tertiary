using SIS.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Models.Payments;

namespace SIS.Models.Accounts
{
    public class CreditTransactions : AuditClass
    {
        [Key]
        public int CreditTransactionId { get; set; }

        // Can reference any entity in the system (e.g., Applicant, Student, Fee, etc.)
        public required string TransactionSource { get; set; } // e.g., "Applicant", "Student", "Course", etc.
        public int TransactionSourceId { get; set; } // ID of the entity this transaction relates to (e.g., ApplicantId, StudentId)

        // Reference to the Payment (if applicable)
        public int PaymentId { get; set; }  // Nullable, in case it's not directly related to a payment
        public PaymentsDetails PaymentsDetails { get; set; }

        // Transaction amount (positive to represent credit)
        [Column(TypeName = "decimal(18,2)")]
        public required decimal Amount { get; set; }

        // Type of credit transaction (e.g., "Refund", "Scholarship", etc.)
        public string TransactionType { get; set; }

        // Status of the credit transaction (Pending, Completed, Failed, etc.)
        public Status Status { get; set; }

        // Timestamp for when the credit transaction occurred
        public DateTime TransactionDate { get; set; }

        // Optional: Description or notes about the credit
        public string Description { get; set; }
    }
}
