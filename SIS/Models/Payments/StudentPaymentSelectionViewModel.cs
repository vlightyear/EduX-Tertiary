using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Payments
{
    public class StudentPaymentSelectionViewModel
    {
        public string TransactionReference { get; set; }
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public decimal Amount { get; set; }
        public decimal MinRegistrationPayment { get; set; }
        public string ProgrammeName { get; set; }
        public string SchoolName { get; set; }
        public string Description { get; set; }
        public bool IsRegistered { get; set; }

        public decimal OutstandingBalance { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalFees { get; set; }
        public decimal RemainingForRegistration { get; set; }
        public List<FeeDetailViewModel> FeeDetails { get; set; } = new List<FeeDetailViewModel>();
    }

    public class StudentCardPaymentViewModelOld
    {
        public string TransactionReference { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Cardholder name is required")]
        public string CardholderName { get; set; }

        [Required(ErrorMessage = "Card number is required")]
        [RegularExpression(@"^\d{16}$", ErrorMessage = "Card number must be 16 digits")]
        public string CardNumber { get; set; }

        [Required(ErrorMessage = "Expiry month is required")]
        [Range(1, 12, ErrorMessage = "Expiry month must be between 1 and 12")]
        public int ExpiryMonth { get; set; }

        [Required(ErrorMessage = "Expiry year is required")]
        public int ExpiryYear { get; set; }

        [Required(ErrorMessage = "CVV is required")]
        [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits")]
        public string Cvv { get; set; }
    }

    public class StudentMobilePaymentViewModel
    {
        public string TransactionReference { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Mobile provider is required")]
        public string Provider { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be 10 digits")]
        public string PhoneNumber { get; set; }
    }

    public class StudentPaymentConfirmationViewModel
    {
        public string TransactionReference { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string ProgrammeName { get; set; }
        public string RegistrationStatus { get; set; }
        public decimal OutstandingBalance { get; set; }
        public string NextSteps { get; set; }
    }

    // This model is likely already defined, but including for completeness
    //public class FeeDetailViewModel
    //{
    //    public string FeeName { get; set; }
    //    public string Description { get; set; }
    //    public decimal Amount { get; set; }
    //}
}
