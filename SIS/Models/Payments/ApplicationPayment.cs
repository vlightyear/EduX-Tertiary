using System.ComponentModel.DataAnnotations.Schema;
using SIS.Enums;
using SIS.Models.StudentApplication;

namespace SIS.Models.Payments
{
    public class ApplicationPayment
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionReference { get; set; }
        public Status Status { get; set; }
    }
}
