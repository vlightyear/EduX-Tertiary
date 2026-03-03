using SIS.Models.StudentApplication;
using System;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Payments
{
    public class OnlinePayments 
    {
        public int Id { get; set; }

        [Required]
        public string MerchantTransactionId { get; set; }
        public int StudentId { get; set; } 

        public int?    ApplicantId   { get; set; }
        public string? FullName { get; set; }

        public string? CustomerFirstName { get; set; }

        public string? CustomerLastName { get; set; }

        public string? Email { get; set; } 

        public string? Msisdn { get; set; }  

        public string? Phone { get; set; }

        public string? AccountNumber { get; set; }
        public string? ReferenceNumber { get; set; }
      
     
        public decimal? Amount { get; set; }
    

        public string? CurrencyCode { get; set; }

        public string? PaymentMethod { get; set; }

        public string? CheckoutUrl { get; set; }

        public string? RequestPayload { get; set; }

        public string? ResponsePayload { get; set; }
        public string? CallbackPayload { get; set; }
        public string? AcknowledgementReceipt { get; set; }
        public string Status { get; set; } = "Pending";

        public string? TransactionType { get; set; }
        public int? StudentInvoiceId { get; set; }
        public virtual StudentInvoice StudentInvoice { get; set; }
        public virtual Student Student{ get; set; }

        public string? PostedBy { get; set; }  // Username of admin who posted the manual payment
        public string? ProofOfPaymentPath { get; set; }  // Path to uploaded proof of payment file
        public string? SageSystemPostStatus { get; set; } = "Pending";
        public string? AccountingSystemPostStatus { get; set; } = "Pending";
        public string? GroupPaymentReference { get; set; }
        public string? IsGroupPayment { get; set; }
        public DateTime? TransactionDate { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
    }

}
