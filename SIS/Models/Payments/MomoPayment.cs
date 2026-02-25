#nullable enable

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Payments
{
    public class MomoPayment
    {
        public int Id { get; set; }

        public string? ExternalId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; }

        public string? Currency { get; set; }

        public string? AccountNumber { get; set; }

        public string? FullName { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Email { get; set; }

        public string? Narration { get; set; }

        public string? TransactionId { get; set; }

        public string? PaymentMethod { get; set; }  

        public string? Status { get; set; }  

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
    }
}
