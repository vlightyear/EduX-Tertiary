using System.ComponentModel.DataAnnotations;
using SIS.Models.Fees;

namespace SIS.Models.Payments
{
    // Base payment details shared across all payment methods
    public class PaymentSelectionViewModel
    {
        public string ApplicationReference { get; set; }
        public string ApplicantName { get; set; }
        public string ProgrammeName { get; set; }
        public string SchoolName { get; set; }
        public string ProgrammeLevel { get; set; }
        public List<FeeDetailViewModel> FeeDetails { get; set; } = new List<FeeDetailViewModel>();
        public decimal TotalAmount { get; set; }
    }

    // Individual fee item for display
    public class FeeDetailViewModel
    {
        public string FeeName { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
    }

    // Credit Card payment specifics
    public class CardPaymentViewModel
    {
        public string ApplicationReference { get; set; }
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Card number is required")]
        [RegularExpression(@"^\d{16}$", ErrorMessage = "Card number must be 16 digits")]
        [Display(Name = "Card Number")]
        public string CardNumber { get; set; }

        [Required(ErrorMessage = "Cardholder name is required")]
        [Display(Name = "Cardholder Name")]
        public string CardholderName { get; set; }

        [Required(ErrorMessage = "Expiry month is required")]
        [Range(1, 12, ErrorMessage = "Expiry month must be between 1 and 12")]
        [Display(Name = "Expiry Month")]
        public int ExpiryMonth { get; set; }

        [Required(ErrorMessage = "Expiry year is required")]
        [Range(2025, 2035, ErrorMessage = "Expiry year must be between 2025 and 2035")]
        [Display(Name = "Expiry Year")]
        public int ExpiryYear { get; set; }

        [Required(ErrorMessage = "CVV is required")]
        [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits")]
        [Display(Name = "CVV")]
        public string Cvv { get; set; }
    }

    // Mobile Money payment specifics
    public class MobilePaymentViewModel
    {
        public string ApplicationReference { get; set; }
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Mobile provider is required")]
        [Display(Name = "Mobile Provider")]
        public string Provider { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be 10 digits")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }
    }

    // Payment confirmation details
    public class PaymentConfirmationViewModel
    {
        public string ApplicationReference { get; set; }
        public string ApplicantName { get; set; }
        public string TransactionReference { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; }
        public string ProgrammeName { get; set; }
        public string NextSteps { get; set; }
    }
}