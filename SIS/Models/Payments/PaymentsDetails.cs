using SIS.Enums;
using SIS.Models.Accounts;
using SIS.Models.Fees;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.Payments
{
    public class PaymentsDetails : AuditClass
    {
        [Key]
        public int Id { get; set; }
        public required string ReferenceNumber { get; set; }
        public required int FeeTypeId { get; set; }
        public FeeType FeeType { get; set; }
        public required string PaymentTypeName { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public required decimal Amount { get; set; }
        public required Status Status { get; set; }
        public DateTime? PaymentDate { get; set; }
        public required string Description { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public required decimal OutStandingAmount { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public required decimal PaidAmount { get; set; }

    }
}
