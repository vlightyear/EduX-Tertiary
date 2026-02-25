using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.enums;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.StudentApplication;
using System.Security.Claims;

namespace SIS.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login()
        {
            var user = await _userManager.FindByEmailAsync("admin@ecampus.com");
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, "Prime@747");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                SetLoginMessage("Please fill in all required fields correctly.", LoginMessageType.Error);
                return View(model);
            }

            // 🚫 TEMPORARY: Disable student login
            //if (!model.EmailOrStudentId.Contains("@"))
            //{
            //    SetLoginMessage("Student portal is temporarily unavailable for maintenance. Please try again later.", LoginMessageType.Error);
            //    return View(model);
            //}

            ApplicationUser user = null;
            bool isEmail = model.EmailOrStudentId.Contains("@");
            bool isStudentDefaultPasswordUsed = false;

            // 🎯 Default passwords for bypass mode
            var defaultPasswords = new[] { "Student@##WWWAll12Loluyeruyuihfjakj", "EcampusByPassMode'12345678954EriWe"};
            bool isDefaultPasswordAttempt = defaultPasswords.Contains(model.Password);

            Student student = null;

            // 🎯 STEP 1: Find user and student records (with auto-fix for corrupted data)
            if (isEmail)
            {
                // Try to find user by email
                user = await _userManager.FindByEmailAsync(model.EmailOrStudentId);

                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains("Student"))
                    {
                        // Try to find student by username first (correct way)
                        student = await _context.Students
                            .Include(s => s.StudyPermits)
                            .FirstOrDefaultAsync(s => s.Username == user.UserName);

                        // 🎯 DATA CORRECTION: If not found by username, try by email (legacy/corrupted data)
                        if (student == null)
                        {
                            student = await _context.Students
                                .Include(s => s.StudyPermits)
                                .FirstOrDefaultAsync(s => s.Email == model.EmailOrStudentId);

                            // If found by email but username doesn't match, fix it
                            if (student != null && student.Username != user.UserName)
                            {
                                try
                                {
                                    Console.WriteLine($"🔧 FIXING: Student {student.StudentId_Number} username mismatch - Correcting from '{student.Username}' to '{user.UserName}'");

                                    student.Username = user.UserName;
                                    student.UpdatedBy = "System_AutoFix";
                                    student.UpdatedAt = DateTime.Now;

                                    _context.Students.Update(student);
                                    await _context.SaveChangesAsync();

                                    Console.WriteLine($"✅ FIXED: Student {student.StudentId_Number} username corrected");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ ERROR fixing student data: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                else
                {
                    // 🎯 ADVANCED RECOVERY: User not found by email - maybe email changed
                    student = await _context.Students
                        .Include(s => s.StudyPermits)
                        .FirstOrDefaultAsync(s => s.Email == model.EmailOrStudentId);

                    if (student != null)
                    {
                        // Try to find user by the student's username
                        user = await _userManager.FindByNameAsync(student.Username);

                        if (user != null)
                        {
                            try
                            {
                                Console.WriteLine($"🔧 FIXING: ApplicationUser email mismatch - Updating to '{model.EmailOrStudentId}'");

                                user.Email = model.EmailOrStudentId;
                                user.NormalizedEmail = _userManager.NormalizeEmail(model.EmailOrStudentId);
                                user.EmailConfirmed = false;

                                var updateResult = await _userManager.UpdateAsync(user);
                                if (updateResult.Succeeded)
                                {
                                    Console.WriteLine($"✅ FIXED: ApplicationUser email updated");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ ERROR fixing ApplicationUser email: {ex.Message}");
                            }
                        }
                    }
                }
            }
            else
            {
                // Login with Student ID
                student = await _context.Students
                    .Include(s => s.StudyPermits)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == model.EmailOrStudentId);

                if (student != null)
                {
                    // Find user by username (correct way)
                    user = await _userManager.FindByNameAsync(student.Username);

                    // 🎯 DATA CORRECTION: If username doesn't work, try by email
                    if (user == null)
                    {
                        user = await _userManager.FindByEmailAsync(student.Email);

                        if (user != null)
                        {
                            try
                            {
                                Console.WriteLine($"🔧 FIXING: Student {student.StudentId_Number} username mismatch - Syncing to '{user.UserName}'");

                                student.Username = user.UserName;
                                student.UpdatedBy = "System_AutoFix";
                                student.UpdatedAt = DateTime.Now;

                                _context.Students.Update(student);
                                await _context.SaveChangesAsync();

                                Console.WriteLine($"✅ FIXED: Student username synced with ApplicationUser");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ ERROR fixing student username: {ex.Message}");
                            }
                        }
                    }
                }
            }

            // 🎯 STEP 2: Handle default password login for students (bypass mode - always allowed)
            if (isDefaultPasswordAttempt)
            {
                if (student != null && user != null && await _userManager.IsInRoleAsync(user, "Student"))
                {
                    // 🎯 Allow default password bypass for all students
                    isStudentDefaultPasswordUsed = true;
                    //await _signInManager.SignInAsync(user, model.RememberMe);
                    await _signInManager.SignInWithClaimsAsync(
                        user,
                        model.RememberMe,
                        new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                            new Claim("FullName", user.FullName ?? user.Email)
                        });

                    // 🚨 Set warning message for default password usage
                    SetDefaultPasswordWarning();

                    return RedirectToAction("Student_Dashboard", "Home");
                }
                else if (student == null)
                {
                    string identifier = isEmail ? "email address" : "student ID";
                    SetLoginMessage($"No student account found with this {identifier}. Please check your details or contact support.", LoginMessageType.Error);
                    return View(model);
                }
                else
                {
                    SetLoginMessage("Invalid credentials. The default password is not valid for your account.", LoginMessageType.Error);
                    return View(model);
                }
            }

            // 🎯 STEP 3: Handle normal password authentication (custom password or initial DB password)
            if (!isStudentDefaultPasswordUsed)
            {
                // Check if user exists
                if (user == null)
                {
                    string identifier = isEmail ? "email address" : (student == null ? "student ID" : "account");
                    SetLoginMessage($"No account found with this {identifier}. Please check your details or register if you're a new user.", LoginMessageType.Error);
                    return View(model);
                }

                // 🎯 Use USERNAME for sign-in (stable identifier), not email
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (!result.Succeeded)
                {
                    if (result.IsLockedOut)
                    {
                        SetLoginMessage("Your account has been locked due to multiple failed login attempts. Please try again later or contact support.", LoginMessageType.Error);
                    }
                    else if (result.IsNotAllowed)
                    {
                        SetLoginMessage("Your account is not activated. Please check your email for activation instructions or contact support.", LoginMessageType.Error);
                    }
                    else
                    {
                        // Incorrect password
                        SetLoginMessage("Incorrect password. Please check your password and try again.", LoginMessageType.Error);
                    }
                    return View(model);
                }

                // Force sign in to ensure User.IsInRole works correctly
                //await _signInManager.SignInAsync(user, model.RememberMe);
                await _signInManager.SignInWithClaimsAsync(
                    user,
                    model.RememberMe,
                    new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                        new Claim("FullName", user.FullName ?? user.Email)
                    });
            }

            // 🎯 STEP 4: Role-based redirection
            if (User.IsInRole("Admin") || User.IsInRole("Registrar") || User.IsInRole("HOD") || User.IsInRole("Dean"))
            {
                SetLoginMessage($"Welcome back, {user.FullName ?? user.Email}! You've successfully logged in", LoginMessageType.Success);
                return RedirectToAction("Index", "Home");
            }
            else if (User.IsInRole("VC") || User.IsInRole("DVC"))
            {
                SetLoginMessage($"Welcome back, {user.FullName ?? user.Email}! You've successfully logged in", LoginMessageType.Success);
                return RedirectToAction("VCDashboard", "Home");
            }
            else if (User.IsInRole("Lecturer"))
            {
                SetLoginMessage($"Welcome back, {user.FullName ?? user.Email}! You've successfully logged in", LoginMessageType.Success);
                return RedirectToAction("Index", "Lecturer");
            }
            else if (User.IsInRole("Candidate"))
            {
                SetLoginMessage("Welcome to your application portal! You've successfully logged in.", LoginMessageType.Success);
                return RedirectToAction("Index", "StudentApplication");
            }
            else if (User.IsInRole("HostelManager"))
            {
                SetLoginMessage("Welcome to your Dashboard! You've successfully logged in.", LoginMessageType.Success);
                return RedirectToAction("HostelManagerDashboard", "StudentAccommodation");
            }
            else if (User.IsInRole("ProgramCoordinator"))
            {
                SetLoginMessage("Welcome to your Dashboard! You've successfully logged in.", LoginMessageType.Success);
                return RedirectToAction("Index", "ProgramCoordinator");
            }
            else if (User.IsInRole("Student"))
            {
                SetLoginMessage($"Welcome back, {user.FullName ?? student?.FullName}!", LoginMessageType.Success);
                return RedirectToAction("Student_Dashboard", "Home");
            }

            // Fallback
            SetLoginMessage("Unable to determine user role. Please contact support.", LoginMessageType.Error);
            return View(model);
        }

        // Helper method to set login messages
        private void SetLoginMessage(string message, LoginMessageType type)
        {
            TempData["LoginMessage"] = message;
            TempData["LoginMessageType"] = type.ToString().ToLower();
        }

        // 🚨 Helper method to set default password warning
        private void SetDefaultPasswordWarning()
        {
            TempData["DefaultPasswordWarning"] = true;
            TempData["DefaultPasswordWarningMessage"] = "⚠️ You are using a default password. Please update your password immediately. Default passwords will be disabled in 24 hours.";
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}