using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Profile;
using Microsoft.Extensions.Logging;

namespace SIS.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            ILogger<ProfileController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
        }

        // GET: Profile
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var student = await _context.Students.Where(s => s.Username == user.UserName).FirstOrDefaultAsync();
            if (student == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var model = new ProfileViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                UserName = user.UserName,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                TwoFactorEnabled = user.TwoFactorEnabled,
                CreatedAt = user.CreatedAt,
                Role = userRoles.FirstOrDefault() ?? "User",
                LockoutEnd = user.LockoutEnd,
                AccessFailedCount = user.AccessFailedCount,
                PassportPhotoPath = student.PassportPhotoPath,
                StudentNumber = student.StudentId_Number
            };

            return View(model);
        }

        // GET: Profile/GetProfile - AJAX endpoint to get current user profile
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var profileData = new
                {
                    id = user.Id,
                    fullName = user.FullName,
                    email = user.Email,
                    phoneNumber = user.PhoneNumber ?? "",
                    userName = user.UserName
                };

                return Json(new { success = true, data = profileData });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting profile: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving profile data." });
            }
        }

        // POST: Profile/UpdateProfile - AJAX endpoint to update profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileViewModel model)
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

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                bool isStudent = userRoles.Any(r => r.Equals("Student", StringComparison.OrdinalIgnoreCase));

                bool emailChanged = !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase);
                var originalEmail = user.Email;
                var originalUserName = user.UserName;

                // Validation for email uniqueness
                if (emailChanged)
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUser != null && existingUser.Id != user.Id)
                    {
                        return Json(new { success = false, message = "Email address is already in use by another account." });
                    }

                    if (isStudent)
                    {
                        var existingStudent = await _context.Students
                            .FirstOrDefaultAsync(s => s.Email == model.Email && s.Username != user.UserName);

                        if (existingStudent != null)
                        {
                            return Json(new { success = false, message = "Email address is already in use by another student." });
                        }
                    }
                }

                // Update basic properties
                user.FullName = model.FullName?.Trim();
                user.PhoneNumber = model.PhoneNumber?.Trim();

                // Handle email change using SetEmailAsync for proper normalization
                if (emailChanged)
                {
                    var setEmailResult = await _userManager.SetEmailAsync(user, model.Email?.Trim());
                    if (!setEmailResult.Succeeded)
                    {
                        var emailErrors = setEmailResult.Errors.Select(e => e.Description);
                        _logger.LogError($"Failed to set email for user {user.UserName}: {string.Join(", ", emailErrors)}");
                        return Json(new { success = false, message = $"Failed to update email: {string.Join(", ", emailErrors)}" });
                    }

                    // Mark email as unconfirmed after change
                    user.EmailConfirmed = false;

                    // Update security stamp
                    await _userManager.UpdateSecurityStampAsync(user);

                    _logger.LogInformation($"Email updated for user {user.UserName}: {originalEmail} -> {model.Email}");
                }

                // Update other user properties
                var userResult = await _userManager.UpdateAsync(user);
                if (!userResult.Succeeded)
                {
                    var updateErrors = userResult.Errors.Select(e => e.Description);
                    _logger.LogError($"Failed to update user {user.UserName}: {string.Join(", ", updateErrors)}");
                    return Json(new { success = false, message = $"Failed to update profile: {string.Join(", ", updateErrors)}" });
                }

                // Update Student record if applicable - find by Username (stable identifier)
                if (isStudent)
                {
                    var student = await _context.Students
                        .FirstOrDefaultAsync(s => s.Username == originalUserName);

                    if (student != null)
                    {
                        // Update student email to match new email
                        student.Email = model.Email?.Trim();
                        student.FullName = model.FullName?.Trim();
                        student.Phone = model.PhoneNumber?.Trim();
                        student.UpdatedBy = user.FullName ?? "Self";
                        student.UpdatedAt = DateTime.Now;

                        _context.Students.Update(student);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Updated student record for {student.StudentId_Number}. Email: {originalEmail} -> {model.Email}");
                    }
                    else
                    {
                        _logger.LogWarning($"Student record not found for username {originalUserName} during profile update");
                    }
                }

                // Refresh sign-in to update claims
                if (emailChanged)
                {
                    await _signInManager.RefreshSignInAsync(user);
                }

                return Json(new { success = true, message = "Profile updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user");
                return Json(new { success = false, message = "An error occurred while updating your profile." });
            }
        }

        // POST: Profile/ChangePassword - AJAX endpoint to change password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordViewModel model)
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

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Get user roles to check if user is a student
                var userRoles = await _userManager.GetRolesAsync(user);
                bool isStudent = userRoles.Any(r => r.Equals("Student", StringComparison.OrdinalIgnoreCase));

                string defaultPassword = "Student@2025"; // Same default password as in login
                bool isUsingDefaultPassword = false;

                // Check if student is using default password
                if (isStudent && model.CurrentPassword == defaultPassword)
                {
                    isUsingDefaultPassword = true;
                    _logger.LogInformation($"Student {user.UserName} is changing password from default password");
                }
                else
                {
                    // Verify current password for normal users or students not using default password
                    var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
                    if (!isCurrentPasswordValid)
                    {
                        return Json(new { success = false, message = "Current password is incorrect." });
                    }
                }

                IdentityResult result;

                if (isUsingDefaultPassword)
                {
                    // For students using default password, we need to remove the old password and add new one
                    // First, remove all existing passwords
                    var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                    if (!removePasswordResult.Succeeded)
                    {
                        var removeErrors = removePasswordResult.Errors.Select(e => e.Description);
                        _logger.LogError($"Failed to remove default password for user {user.UserName}: {string.Join(", ", removeErrors)}");
                        return Json(new { success = false, message = "Error updating password. Please try again." });
                    }

                    // Add the new password
                    result = await _userManager.AddPasswordAsync(user, model.NewPassword);
                }
                else
                {
                    // Normal password change for other users or students with real passwords
                    result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                }

                if (result.Succeeded)
                {
                    // ✅ UPDATE: Set HasChangedInitialPassword to true
                    if (!user.HasChangedInitialPassword)
                    {
                        user.HasChangedInitialPassword = true;
                        await _userManager.UpdateAsync(user);
                        _logger.LogInformation($"Updated HasChangedInitialPassword flag for user {user.UserName}");
                    }

                    // Sign in the user again to refresh the security stamp
                    await _signInManager.RefreshSignInAsync(user);

                    string logMessage = isUsingDefaultPassword
                        ? $"Password successfully changed from default password for student {user.UserName}"
                        : $"Password changed successfully for user {user.UserName}";

                    _logger.LogInformation(logMessage);

                    string successMessage = isUsingDefaultPassword
                        ? "Your temporary password has been successfully updated. You can now use your new password to login."
                        : "Password changed successfully.";

                    return Json(new { success = true, message = successMessage });
                }

                var passwordErrors = result.Errors.Select(e => e.Description);
                return Json(new { success = false, message = string.Join(", ", passwordErrors) });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error changing password: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while changing your password." });
            }
        }

        // GET: Profile/GetAccountActivity - Get user's recent activity (optional)
        [HttpGet]
        public async Task<IActionResult> GetAccountActivity()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // This is a placeholder - you can implement actual activity tracking
                var activityData = new
                {
                    lastLoginDate = DateTime.Now.AddDays(-1), // Placeholder
                    loginCount = 45, // Placeholder
                    profileLastUpdated = user.CreatedAt, // You can add an UpdatedAt field to track this
                    passwordLastChanged = DateTime.Now.AddDays(-30) // Placeholder
                };

                return Json(new { success = true, data = activityData });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting account activity: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving account activity." });
            }
        }

        // Helper method to handle Identity errors (similar to your AdminController)
        private async Task HandleIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}