using SIS.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using SIS.Models.Payments;

namespace SIS.Models.Accounts
{
    public class DebitTransactions : AuditClass
    {
        [Key]
        public int DebitTransactionId { get; set; }

        // Can reference any entity in the system (e.g., Applicant, Student, Fee, etc.)
        public required string TransactionSource { get; set; } // e.g., "Applicant", "Student", "Course", etc.
        public int TransactionSourceId { get; set; } // ID of the entity this transaction relates to (e.g., ApplicantId, StudentId)

        // Reference to the Payment (if applicable)
        public int? PaymentId { get; set; }  // Nullable, in case it's not directly related to a payment
        public PaymentsDetails PaymentsDetails { get; set; }

        // Transaction amount (negative to represent debit)
        [Column(TypeName = "decimal(18,2)")]
        public required decimal Amount { get; set; }

        // Type of debit transaction (e.g., "Application Fee", "Course Fee", etc.)
        public string TransactionType { get; set; }

        // Status of the debit transaction (Pending, Completed, Failed, etc.)
        public Status Status { get; set; }

        // Timestamp for when the debit transaction occurred
        public DateTime TransactionDate { get; set; }

        // Optional: Description or notes about the debit
        public string Description { get; set; }
    }
}
