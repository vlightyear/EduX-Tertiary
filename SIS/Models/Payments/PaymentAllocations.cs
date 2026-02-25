using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Payments
{
    public class PaymentAllocation
    {
        public int Id { get; set; }

        [Required]
        public int OnlinePaymentId { get; set; }

        [Required]
        public int StudentInvoiceId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AllocatedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InvoiceBalanceBeforeAllocation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InvoiceBalanceAfterAllocation { get; set; }

        public int AllocationSequence { get; set; }

        public DateTime AllocatedAt { get; set; } = DateTime.Now;

        public string? AllocatedBy { get; set; }

        public string? Notes { get; set; }

        // Navigation properties
        public virtual OnlinePayments OnlinePayment { get; set; }
        public virtual StudentInvoice StudentInvoice { get; set; }
    }

    internal class ProcessableInvoice
    {
        public int Id { get; set; }
        public string InvoiceReference { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal CreditNotesApplied { get; set; }
        public decimal EffectiveAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsDebitNote { get; set; }
        public StudentInvoice Invoice { get; set; }
    }

    internal class ProcessablePayment
    {
        public int Id { get; set; }
        public string MerchantTransactionId { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal DebitNotesApplied { get; set; }
        public decimal EffectiveAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public DateTime TransactionDate { get; set; }
        public bool IsCreditNote { get; set; }
        public int? TargetInvoiceId { get; set; } // For credit notes
        public OnlinePayments Payment { get; set; }
    }
}