using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Admin
{
    public class RegisterUserViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters")]
        public required string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [Display(Name = "Email Address")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters long")]
        public required string Password { get; set; }

        [Required(ErrorMessage = "Role is required")]
        [Display(Name = "Role")]
        public required string Role { get; set; } // Admin, Academic Officer, etc.
    }
}
