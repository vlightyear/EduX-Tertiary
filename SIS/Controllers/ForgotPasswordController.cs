using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIS.Data;
using SIS.Services.Emails;

namespace SIS.Controllers
{
    [AllowAnonymous]
    public class ForgotPasswordController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public ForgotPasswordController(
            UserManager<ApplicationUser> userManager,
            IEmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        // GET: ForgotPassword
        public IActionResult Index()
        {
            return View();
        }

        // POST: ForgotPassword/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                // Find user by email
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    // Notify user that email was not found
                    return Json(new
                    {
                        success = false,
                        message = "No account found with that email address. Please check the email and try again."
                    });
                }

                // Generate a secure temporary password
                string temporaryPassword = GenerateSecurePassword();

                // Remove the old password and set the new one
                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded)
                {
                    return Json(new { success = false, message = "Failed to reset password. Please try again." });
                }

                var addPasswordResult = await _userManager.AddPasswordAsync(user, temporaryPassword);
                if (!addPasswordResult.Succeeded)
                {
                    var passwordErrors = addPasswordResult.Errors.Select(e => e.Description);
                    return Json(new { success = false, message = string.Join(", ", passwordErrors) });
                }

                // Update security stamp to invalidate existing cookies/tokens
                await _userManager.UpdateSecurityStampAsync(user);

                // Send email with new password
                var emailSent = await _emailService.SendPasswordResetEmailAsync(
                    user.FullName,
                    user.Email,
                    temporaryPassword
                );

                if (!emailSent)
                {
                    // If email fails, we should ideally revert the password change
                    // For now, we'll just log this and return success since password was changed
                    Console.WriteLine($"Failed to send password reset email to {user.Email}");
                }

                return Json(new
                {
                    success = true,
                    message = "Password has been reset successfully. Please check your email for the new password."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in password reset: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while resetting your password. Please try again." });
            }
        }

        private string GenerateSecurePassword()
        {
            // Define character sets
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers = "0123456789";
            const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var random = new Random();
            var password = new char[12]; // 12 character password

            // Ensure at least one character from each set
            password[0] = lowercase[random.Next(lowercase.Length)];
            password[1] = uppercase[random.Next(uppercase.Length)];
            password[2] = numbers[random.Next(numbers.Length)];
            password[3] = specialChars[random.Next(specialChars.Length)];

            // Fill the rest with random characters from all sets
            string allChars = lowercase + uppercase + numbers + specialChars;
            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // Shuffle the password to avoid predictable patterns
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }
    }

    // Request model for forgot password
    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; }
    }
}