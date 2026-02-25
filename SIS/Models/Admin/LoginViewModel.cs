using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Admin
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Please enter your email or student ID")]
        public required string EmailOrStudentId { get; set; }

        [Required(ErrorMessage = "Please enter your password")]
        [DataType(DataType.Password)]
        public required string Password { get; set; }

        public bool RememberMe { get; set; }
    }
}
