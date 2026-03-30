using System;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Services.Emails;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Assessments;
using SIS.Models.Fees;
using SIS.Models.Registration;
using SIS.Models.StudentAccommodation;
using System.Text;

namespace SIS.Controllers
{
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;


        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context, SignInManager<ApplicationUser> signInManager, IEmailService emailService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _signInManager = signInManager;
            _emailService = emailService;
        }

        // load page for creating a candidate user
        public IActionResult CandidateRegistration() {
            return View("~/Views/Account/CandidateRegistration.cshtml"); }

        [HttpPost]
        public async Task<IActionResult> CandidateRegistration(IFormCollection collection)
        {
            string fullName = collection["FullName"];
            string email = collection["Email"];
            string role = "Candidate";
            string password = collection["Password"];

            // Check if email already exists
            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ModelState.AddModelError(string.Empty, "Email address is already in use.");

                // For AJAX requests, return error response
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Email address is already in use."
                    });
                }

                // Return the same view with errors for non-AJAX requests
                return View("~/Views/Account/CandidateRegistration.cshtml");
            }

            // Check if role exists
            var roleExists = await _roleManager.RoleExistsAsync(role);
            if (!roleExists)
            {
                ModelState.AddModelError(string.Empty, "The specified role does not exist.");

                // For AJAX requests, return error response
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "The specified role does not exist."
                    });
                }

                return View("~/Views/Account/CandidateRegistration.cshtml");
            }

            // Create new user
            var user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                UserName = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);

                // For AJAX requests, return a successful response
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Ok(new { success = true });
                }

                // For non-AJAX requests, sign in the user directly
                await _signInManager.SignInAsync(user, isPersistent: true);
                return RedirectToAction("Index", "StudentApplication");
            }
            else
            {
                await HandleIdentityErrors(result);

                // For AJAX requests, return error response
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var errors = string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    return BadRequest(new
                    {
                        success = false,
                        message = errors
                    });
                }
            }

            // Return to the same view if registration fails
            return View("~/Views/Account/CandidateRegistration.cshtml");
        }

        // GET: Admin/Users
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Users()
        {
            // Get all users for statistics
            var allUsers = await _userManager.Users.ToListAsync();

            // Get user-role mapping in a single batch operation
            var userRoleMap = new Dictionary<string, List<string>>();
            var adminUsers = new List<ApplicationUser>();

            // Batch get all roles for all users
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var roleList = roles.ToList();
                userRoleMap[user.Id] = roleList;

                // Filter admin users (non-student, non-candidate)
                if (!roleList.Contains("Student") && !roleList.Contains("Candidate"))
                {
                    adminUsers.Add(user);
                }
            }

            // Calculate statistics
            var totalUsers = allUsers.Count;
            var activeUsers = allUsers.Count(u => u.LockoutEnd == null || u.LockoutEnd <= DateTime.Now);

            // Count users by role efficiently
            var roleStats = new Dictionary<string, int>
            {
                ["Admin"] = 0,
                ["SuperAdmin"] = 0,
                ["Student"] = 0,
                ["Candidate"] = 0,
                ["Lecturer"] = 0,
                ["HOD"] = 0,
                ["Dean"] = 0,
                ["Registrar"] = 0,
                ["HostelManager"] = 0,
                ["ProgramCoordinator"] = 0,
                ["VC"] = 0,
                ["DVC"] = 0
            };

            foreach (var roles in userRoleMap.Values)
            {
                foreach (var role in roles)
                {
                    if (roleStats.ContainsKey(role))
                        roleStats[role]++;
                }
            }

            // Get actual student count from Students table (matches StudentImport stats)
            var actualStudentCount = await _context.Students.CountAsync();

            // Calculate totals for compatibility
            var adminUserCount = roleStats["Admin"] + roleStats["SuperAdmin"];
            var candidateUserCount = roleStats["Candidate"];

            // Create user roles dictionary for the view (now shows all roles)
            var userRoles = new Dictionary<string, List<string>>();
            foreach (var user in adminUsers)
            {
                userRoles[user.Id] = userRoleMap[user.Id];
            }

            // Pass statistics to view
            ViewBag.TotalUsers = totalUsers;
            ViewBag.ActiveUsers = activeUsers;
            ViewBag.AdminUsers = adminUserCount;
            ViewBag.StudentUsers = actualStudentCount;
            ViewBag.CandidateUsers = candidateUserCount;
            ViewBag.UserRoles = userRoles;
            ViewBag.RoleStats = roleStats;

            return View(adminUsers);
        }

        // GET: Admin/GetUser
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var model = new
            {
                id = user.Id,
                fullName = user.FullName,
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                role = userRoles.ToList(),
                emailConfirmed = user.EmailConfirmed,
                phoneNumberConfirmed = user.PhoneNumberConfirmed,
                twoFactorEnabled = user.TwoFactorEnabled,
                lockoutEnabled = user.LockoutEnabled,
                lockoutEnd = user.LockoutEnd
            };

            return Json(model);
        }

        // GET: Admin/GetRoles
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRoles()
        {
            // Only return administrative roles, exclude Student and Candidate
            var roles = await _roleManager.Roles
                .Where(r => r.Name != "Student" && r.Name != "Candidate" && r.Name != "Dev")
                .Select(r => new { name = r.Name })
                .ToListAsync();

            return Json(roles);
        }

        // POST: Admin/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser(IFormCollection collection)
        {
            try
            {
                string fullName = collection["FullName"];
                string email = collection["Email"];
                string phoneNumber = collection["PhoneNumber"];


                // Handle multiple roles - expect comma-separated values or array
                var selectedRoles = collection["Roles"].ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList();

                if (!selectedRoles.Any())
                {
                    return Json(new { success = false, message = "Please select at least one role." });
                }


                bool emailConfirmed = collection["EmailConfirmed"].ToString().Contains("true");
                bool twoFactorEnabled = collection["TwoFactorEnabled"].ToString().Contains("true");

                if (await _userManager.FindByEmailAsync(email) != null)
                {
                    return Json(new { success = false, message = "Email address is already in use." });
                }

                // Generate a secure temporary password
                string temporaryPassword = GenerateSecurePassword();

                // Create new user
                var user = new ApplicationUser
                {
                    FullName = fullName,
                    Email = email,
                    UserName = email, // Email as username
                    PhoneNumber = phoneNumber,
                    EmailConfirmed = emailConfirmed,
                    TwoFactorEnabled = twoFactorEnabled,
                    CreatedAt = DateTime.Now,
                    //CreatedBy = User.Identity.Name
                };

                var result = await _userManager.CreateAsync(user, temporaryPassword);

                if (result.Succeeded)
                {
                    foreach (var role in selectedRoles)
                    {
                        await _userManager.AddToRoleAsync(user, role);
                    }

                    // Send welcome email with temporary password
                    try
                    {
                        string rolesString = string.Join(", ", selectedRoles);
                        bool emailSent = await _emailService.SendUserCreationEmailAsync(
                            fullName,
                            email,
                            rolesString,
                            temporaryPassword,
                            "Please change this password upon your first login for security."
                        );

                        if (!emailSent)
                        {
                            Console.WriteLine($"Warning: Welcome email failed to send to {email}");
                            // You could add a flag to the user record or log this for follow-up
                        }
                    }
                    catch (Exception emailEx)
                    {
                        Console.WriteLine($"Error sending welcome email to {email}: {emailEx.Message}");
                        // Don't fail the user creation if email fails - just log it
                    }

                    return RedirectToAction(nameof(Users));
                }

                var errors = result.Errors.Select(e => e.Description);
                return Json(new { success = false, message = string.Join(", ", errors) });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating user: {ex}");
                return Json(new { success = false, message = "An error occurred while creating the user." });
            }
        }

        // POST: Admin/UpdateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUser(UpdateUserViewModel model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                {
                    return NotFound();
                }

                // Update user properties
                user.FullName = model.FullName;
                user.Email = model.Email;
                user.UserName = model.Email;
                user.PhoneNumber = model.PhoneNumber;
                user.EmailConfirmed = model.EmailConfirmed;
                user.PhoneNumberConfirmed = model.PhoneNumberConfirmed;
                user.TwoFactorEnabled = model.TwoFactorEnabled;
                user.LockoutEnabled = model.LockoutEnabled;
                user.LockoutEnd = model.LockoutEnd;

                // Update roles - remove all current roles and add new ones
                var currentRoles = await _userManager.GetRolesAsync(user);
                var newRoles = model.Roles ?? new List<string>();

                // Remove roles that are not in the new list
                var rolesToRemove = currentRoles.Except(newRoles).ToList();
                if (rolesToRemove.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                }

                // Add roles that are not in the current list
                var rolesToAdd = newRoles.Except(currentRoles).ToList();
                if (rolesToAdd.Any())
                {
                    await _userManager.AddToRolesAsync(user, rolesToAdd);
                }

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    return RedirectToAction(nameof(Users));
                }

                var errors = result.Errors.Select(e => e.Description);
                return Json(new { success = false, message = string.Join(", ", errors) });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating user: {ex}");
                return Json(new { success = false, message = "An error occurred while updating the user." });
            }
        }

        // POST: Admin/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound();
                }

                // Check if user is trying to delete themselves
                if (user.Id == _userManager.GetUserId(User))
                {
                    return Json(new { success = false, message = "You cannot delete your own account." });
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    return RedirectToAction(nameof(Users));
                }

                var errors = result.Errors.Select(e => e.Description);
                return Json(new { success = false, message = string.Join(", ", errors) });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting user: {ex}");
                return Json(new { success = false, message = "An error occurred while deleting the user." });
            }
        }

        // Helper method to generate secure password
        private string GenerateSecurePassword()
        {
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string specialChars = "!@#$%^&*";

            var random = new Random();
            var password = new StringBuilder();

            // Ensure at least one character from each required category
            password.Append(lowercase[random.Next(lowercase.Length)]);
            password.Append(uppercase[random.Next(uppercase.Length)]);
            password.Append(digits[random.Next(digits.Length)]);
            password.Append(specialChars[random.Next(specialChars.Length)]);

            // Fill the rest randomly from all categories
            const string allChars = lowercase + uppercase + digits + specialChars;
            for (int i = 4; i < 12; i++) // Total length of 12 characters
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Shuffle the password to avoid predictable patterns
            return new string(password.ToString().OrderBy(x => random.Next()).ToArray());
        }


        // GET: Admin/Schools
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Schools()
        {
            // Get all users with the Dean role for dropdown selection
            var userManager = HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var deanUsers = await userManager.GetUsersInRoleAsync("Dean");
            var arUsers = await userManager.GetUsersInRoleAsync("Assistant Registrar");

            // Convert to SelectList for dropdowns
            ViewBag.DeanUsers = new SelectList(deanUsers, "Id", "FullName");
            ViewBag.ArUsers = new SelectList(arUsers, "Id", "FullName");

            var schools = await _context.Schools
                .Include(s => s.Dean)
                .Include(s => s.AssistantDean)
                .Include(s => s.AssistantRegistrar)
                .ToListAsync();

            // Get statistics for the sidebar
            var totalStudents = await _context.Students.CountAsync();
            var totalLecturers = (await userManager.GetUsersInRoleAsync("Lecturer")).Count;

            // Get student distribution by school
            var studentDistribution = await _context.Students
                .Include(s => s.School)
                .GroupBy(s => s.School.Name)
                .Select(g => new {
                    SchoolName = g.Key,
                    StudentCount = g.Count()
                })
                .OrderByDescending(x => x.StudentCount)
                .Take(5) // Get top 5 schools
                .ToListAsync();

            // Pass statistics to the view
            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalLecturers = totalLecturers;
            ViewBag.StudentDistribution = studentDistribution;

            return View(schools);
        }

        // GET: Admin/GetSchool/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSchool(int id)
        {
            var school = await _context.Schools
                .Include(s => s.Dean)
                .Include(s => s.AssistantDean)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (school == null)
            {
                return NotFound();
            }
            return Json(school);
        }

        // POST: Admin/CreateSchool
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateSchool(School school, IFormFile fileUpload, string mode)
        {
            try
            {
                if(mode == "single")
                {
                    if (school != null)
                    {
                        school.CreatedAt = DateTime.Now;
                        school.CreatedBy = User.Identity.Name;
                        _context.Schools.Add(school);
                        await _context.SaveChangesAsync();
                        return RedirectToAction("Schools");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Invalid school details.");
                        return View("Schools");
                    }
                }
                else if (mode == "bulk" && fileUpload != null)
                {
                    var schools = new List<School>();
                    using (var stream = new MemoryStream())
                    {
                        await fileUpload.CopyToAsync(stream);
                        
                        stream.Position = 0;

                        using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
                        {
                            while (reader.Read())
                            {
                                // Assuming the first row is the header, skip it
                                if (reader.Depth == 0) continue;

                                schools.Add(new School
                                {
                                    Name = reader.GetString(0).ToUpper(),
                                    Description = reader.GetString(1),
                                    CreatedAt = DateTime.Now,
                                    CreatedBy = User.Identity.Name
                                });
                            }
                        }


                    }
                    if (schools.Any())
                    {
                        _context.Schools.AddRange(schools);
                        await _context.SaveChangesAsync();
                        return RedirectToAction("Schools");
                    }
                    else
                    {
                        ModelState.AddModelError("", "No valid school details found in the file.");
                        return View("Schools");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Invalid mode or file upload missing.");
                    return View("Schools");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                return View();
            }
        }

        // POST: Admin/UpdateSchool
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateSchool(School school)
        {
            try
            {
                var existingSchool = await _context.Schools.FindAsync(school.Id);
                if (existingSchool == null)
                {
                    return NotFound();
                }

                existingSchool.Name = school.Name;
                existingSchool.Description = school.Description;
                existingSchool.DeanId = school.DeanId;
                existingSchool.AssistantDeanId = school.AssistantDeanId;
                existingSchool.AssistantRegistrarId = school.AssistantRegistrarId;
                existingSchool.UpdatedAt = DateTime.Now;
                existingSchool.UpdatedBy = User.Identity.Name;

                await _context.SaveChangesAsync();
                return RedirectToAction("Schools");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating school: {ex}");
                return RedirectToAction("Schools");
            }
        }

        // POST: Admin/DeleteSchool
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteSchool(int id)
        {
            try
            {
                var school = await _context.Schools.FindAsync(id);
                if (school == null)
                {
                    return NotFound();
                }

                _context.Schools.Remove(school);
                await _context.SaveChangesAsync();
                return RedirectToAction("Schools");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting school: {ex}");
                return RedirectToAction("Schools");
            }
        }

        // GET: Programmes
        [Authorize(Roles = "Admin")]
        public IActionResult Programmes()
        {
            try
            {
                var programmes = _context.Programmes
                    .Include(p => p.Department)
                    .ThenInclude(d => d.School) // Ensure related entities are loaded
                    .Include(p => p.ModeOfStudy)
                    .Include(p => p.ProgrammeLevel)
                    .Include(p => p.Coordinator) // Include Programme Coordinator
                    .Include(p => p.AssociatedNQProgramme) // Include associated NQ programme
                    .ToList();

                // Calculate statistics safely
                ViewBag.TotalProgrammes = programmes.Count;
                ViewBag.TotalRegularProgrammes = programmes.Count(p => !p.IsNonQuota);
                ViewBag.TotalNQProgrammes = programmes.Count(p => p.IsNonQuota);
                ViewBag.TotalSchools = programmes
                    .Where(p => p.Department?.School != null)
                    .Select(p => p.Department.School.Id)
                    .Distinct()
                    .Count();
                ViewBag.TotalCoordinators = programmes
                    .Where(p => p.CoordinatorId != null)
                    .Select(p => p.CoordinatorId)
                    .Distinct()
                    .Count();
                ViewBag.AverageDuration = programmes.Any() ? programmes.Average(p => p.DurationYears) : 0.0;
                ViewBag.TotalDepartments = programmes
                    .Where(p => p.Department != null)
                    .Select(p => p.DepartmentId)
                    .Distinct()
                    .Count();
                ViewBag.TotalEnrollments = programmes.Sum(p => p.EnrollmentCount);

                // Add chart data for programme distribution
                var programmeLevels = _context.Programmes
                    .Include(p => p.ProgrammeLevel)
                    .Where(p => p.ProgrammeLevel != null)
                    .GroupBy(p => p.ProgrammeLevel.Name)
                    .Select(g => new { Level = g.Key, Count = g.Count() })
                    .ToList();

                ViewBag.ChartData = programmeLevels;

                // Add programme type distribution for additional chart
                var programmeTypes = programmes
                    .GroupBy(p => p.IsNonQuota ? "Non-Quota" : "Regular")
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToList();

                ViewBag.ProgrammeTypeData = programmeTypes;

                return View(programmes);
            }
            catch (Exception ex)
            {
                // Log the error (use your logging framework)
                Console.WriteLine($"Error in Programmes action: {ex.Message}");

                // Return empty list to prevent view errors
                ViewBag.TotalProgrammes = 0;
                ViewBag.TotalRegularProgrammes = 0;
                ViewBag.TotalNQProgrammes = 0;
                ViewBag.TotalSchools = 0;
                ViewBag.TotalCoordinators = 0;
                ViewBag.AverageDuration = 0.0;
                ViewBag.TotalDepartments = 0;
                ViewBag.TotalEnrollments = 0;

                return View(new List<Programme>());
            }
        }

        // GET: Programmes/Create
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult CreateProgramme()
        {
            // Fetch users excluding those in the "Candidate" or "Student" roles
            var coordinatorUsers = _context.Users
                .Where(u => !_context.UserRoles
                    .Any(ur => ur.UserId == u.Id &&
                               (_context.Roles.Any(r => r.Id == ur.RoleId &&
                                                       (r.Name == "Candidate" || r.Name == "Student")))))
                .Select(u => new { u.Id, DisplayName = u.FullName ?? u.UserName })
                .ToList();

            ViewBag.DepartmentId = new SelectList(_context.Departments, "Id", "Name");
            ViewBag.ModeOfStudyId = new SelectList(_context.ModesOfStudy, "ModeId", "ModeName");
            ViewBag.ProgrammeLevelId = new SelectList(_context.ProgramLevels, "Id", "Name");
            ViewBag.CoordinatorId = new SelectList(coordinatorUsers, "Id", "DisplayName");

            // Add NQ Programmes dropdown (only programmes marked as IsNonQuota = true)
            var nqProgrammes = _context.Programmes
                .Where(p => p.IsNonQuota == true)
                .Include(p => p.Department)
                .ThenInclude(d => d.School)
                .Select(p => new {
                    p.Id,
                    DisplayName = $"{p.Name} ({p.Department.School.Name})"
                })
                .ToList();

            ViewBag.AssociatedNQProgrammeId = new SelectList(nqProgrammes, "Id", "DisplayName");

            // Add statistics
            var programmes = _context.Programmes.Include(p => p.Department).ThenInclude(d => d.School).ToList();
            ViewBag.TotalProgrammes = programmes.Count;
            ViewBag.TotalRegularProgrammes = programmes.Count(p => !p.IsNonQuota);
            ViewBag.TotalNQProgrammes = programmes.Count(p => p.IsNonQuota);
            ViewBag.TotalSchools = programmes
                .Where(p => p.Department?.School != null)
                .Select(p => p.Department.School.Id)
                .Distinct()
                .Count();
            ViewBag.TotalCoordinators = programmes
                .Where(p => p.CoordinatorId != null)
                .Select(p => p.CoordinatorId)
                .Distinct()
                .Count();
            ViewBag.AverageDuration = programmes.Any() ? programmes.Average(p => p.DurationYears) : 0.0;
            ViewBag.TotalDepartments = programmes
                .Where(p => p.Department != null)
                .Select(p => p.DepartmentId)
                .Distinct()
                .Count();

            // Add chart data for programme distribution
            var programmeLevels = _context.Programmes
                .Include(p => p.ProgrammeLevel)
                .Where(p => p.ProgrammeLevel != null)
                .GroupBy(p => p.ProgrammeLevel.Name)
                .Select(g => new { Level = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.ChartData = programmeLevels;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProgramme(Programme model)
        {
            try
            {
                if (model != null)
                {
                    // Validation: Non-Quota programmes cannot have associated NQ programmes
                    if (model.IsNonQuota && model.AssociatedNQProgrammeId.HasValue)
                    {
                        ModelState.AddModelError("AssociatedNQProgrammeId", "Non-Quota programmes cannot be associated with other NQ programmes.");
                    }

                    // Validation: Regular programmes can only be associated with NQ programmes
                    if (!model.IsNonQuota && model.AssociatedNQProgrammeId.HasValue)
                    {
                        var associatedProgramme = await _context.Programmes
                            .FirstOrDefaultAsync(p => p.Id == model.AssociatedNQProgrammeId.Value);

                        if (associatedProgramme != null && !associatedProgramme.IsNonQuota)
                        {
                            ModelState.AddModelError("AssociatedNQProgrammeId", "Regular programmes can only be associated with Non-Quota programmes.");
                        }
                    }

                    // Initialize YearlyRequirements based on form input
                    var yearlyRequirements = new Dictionary<string, object>();

                    for (int i = 1; i <= model.DurationYears; i++)
                    {
                        string yearKey = $"Year{i}";

                        if (model.IsSemesterBased)
                        {
                            // Handle semester-based requirements
                            string semester1Field = $"Semester1_Year{i}";
                            string semester2Field = $"Semester2_Year{i}";

                            int semester1 = 0, semester2 = 0;

                            if (Request.Form.ContainsKey(semester1Field) &&
                                int.TryParse(Request.Form[semester1Field], out int sem1Value))
                            {
                                semester1 = sem1Value;
                            }

                            if (Request.Form.ContainsKey(semester2Field) &&
                                int.TryParse(Request.Form[semester2Field], out int sem2Value))
                            {
                                semester2 = sem2Value;
                            }

                            int totalRequired = semester1 + semester2;

                            yearlyRequirements.Add(yearKey, new
                            {
                                TotalRequired = totalRequired,
                                Semester1 = semester1,
                                Semester2 = semester2
                            });
                        }
                        else
                        {
                            // Handle yearly requirements (existing logic)
                            string fieldName = $"YearlyRequirements_Year{i}";
                            int totalRequired = 0;

                            if (Request.Form.ContainsKey(fieldName) &&
                                int.TryParse(Request.Form[fieldName], out int value))
                            {
                                totalRequired = value;
                            }

                            yearlyRequirements.Add(yearKey, new { TotalRequired = totalRequired });
                        }
                    }

                    // Serialize to JSON
                    model.YearlyRequirements = System.Text.Json.JsonSerializer.Serialize(yearlyRequirements);

                    // Set audit fields
                    model.CreatedAt = DateTime.Now;
                    model.CreatedBy = User.Identity.Name;

                    // Add to context and save
                    _context.Add(model);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Programme has been created successfully.";
                    return RedirectToAction("Programmes");
                }

                TempData["Error"] = "Invalid programme data.";
                await LoadViewBagData(model);
                return View(model);
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error creating programme: {ex}");

                // Reload ViewBag data for the form
                await LoadViewBagData(model);

                TempData["Error"] = "An error occurred while creating the programme. Please try again.";
                return View(model);
            }
        }

        // GET: Programmes/Update/5
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateProgramme(int id)
        {
            var programme = _context.Programmes
                .Include(p => p.AssociatedNQProgramme) // Include associated NQ programme
                .FirstOrDefault(p => p.Id == id);

            if (programme == null)
            {
                return NotFound();
            }

            // Parse YearlyRequirements for the view
            ViewBag.YearlyRequirements = new Dictionary<string, object>();
            ViewBag.SemesterRequirements = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(programme.YearlyRequirements))
            {
                try
                {
                    var yearlyRequirements = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                        programme.YearlyRequirements);

                    var yearlyReqs = new Dictionary<string, int>();
                    var semesterReqs = new Dictionary<string, object>();

                    foreach (var year in yearlyRequirements)
                    {
                        var requirementJson = ((System.Text.Json.JsonElement)year.Value).GetRawText();
                        var requirement = System.Text.Json.JsonSerializer.Deserialize<YearRequirement>(requirementJson);

                        yearlyReqs[year.Key] = requirement.TotalRequired;

                        // Check if semester data exists
                        if (requirement.Semester1.HasValue && requirement.Semester2.HasValue)
                        {
                            semesterReqs[year.Key] = new
                            {
                                Semester1 = requirement.Semester1.Value,
                                Semester2 = requirement.Semester2.Value,
                                Total = requirement.TotalRequired
                            };
                        }
                    }

                    ViewBag.YearlyRequirements = yearlyReqs;
                    ViewBag.SemesterRequirements = semesterReqs;
                }
                catch
                {
                    ViewBag.YearlyRequirements = new Dictionary<string, int>();
                    ViewBag.SemesterRequirements = new Dictionary<string, object>();
                }
            }

            // Load ViewBag data
            LoadViewBagDataForUpdate(programme);

            return View(programme);
        }

        private class YearRequirement
        {
            public int TotalRequired { get; set; }
            public int? Semester1 { get; set; }
            public int? Semester2 { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateProgramme(int id, Programme programme)
        {
            if (id != programme.Id)
            {
                return BadRequest();
            }

            try
            {
                // Get the existing programme to preserve CreatedBy and CreatedAt
                var existingProgramme = await _context.Programmes.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (existingProgramme == null)
                {
                    return NotFound();
                }

                // Validation: Non-Quota programmes cannot have associated NQ programmes
                if (programme.IsNonQuota && programme.AssociatedNQProgrammeId.HasValue)
                {
                    ModelState.AddModelError("AssociatedNQProgrammeId", "Non-Quota programmes cannot be associated with other NQ programmes.");
                }

                // Validation: Regular programmes can only be associated with NQ programmes
                if (!programme.IsNonQuota && programme.AssociatedNQProgrammeId.HasValue)
                {
                    var associatedProgramme = await _context.Programmes
                        .FirstOrDefaultAsync(p => p.Id == programme.AssociatedNQProgrammeId.Value);

                    if (associatedProgramme != null && !associatedProgramme.IsNonQuota)
                    {
                        ModelState.AddModelError("AssociatedNQProgrammeId", "Regular programmes can only be associated with Non-Quota programmes.");
                    }
                }

                // Create new yearly requirements dictionary
                var yearlyRequirements = new Dictionary<string, object>();

                // Get values from the form for each year
                for (int i = 1; i <= programme.DurationYears; i++)
                {
                    string yearKey = $"Year{i}";

                    if (programme.IsSemesterBased)
                    {
                        // Handle semester-based requirements
                        string semester1Field = $"Semester1_Year{i}";
                        string semester2Field = $"Semester2_Year{i}";

                        int semester1 = 0, semester2 = 0;

                        if (Request.Form.ContainsKey(semester1Field) &&
                            int.TryParse(Request.Form[semester1Field], out int sem1Value))
                        {
                            semester1 = sem1Value;
                        }

                        if (Request.Form.ContainsKey(semester2Field) &&
                            int.TryParse(Request.Form[semester2Field], out int sem2Value))
                        {
                            semester2 = sem2Value;
                        }

                        int totalRequired = semester1 + semester2;

                        yearlyRequirements.Add(yearKey, new
                        {
                            TotalRequired = totalRequired,
                            Semester1 = semester1,
                            Semester2 = semester2
                        });
                    }
                    else
                    {
                        // Handle yearly requirements
                        string fieldName = $"YearlyRequirements_Year{i}";
                        int totalRequired = 0;

                        if (Request.Form.ContainsKey(fieldName) &&
                            int.TryParse(Request.Form[fieldName], out int value))
                        {
                            totalRequired = value;
                        }

                        yearlyRequirements.Add(yearKey, new { TotalRequired = totalRequired });
                    }
                }

                // Update the model with the new requirements
                programme.YearlyRequirements = System.Text.Json.JsonSerializer.Serialize(yearlyRequirements);

                // Preserve the creation audit fields
                programme.CreatedBy = existingProgramme.CreatedBy;
                programme.CreatedAt = existingProgramme.CreatedAt;

                // Update the modification audit fields
                programme.UpdatedAt = DateTime.Now;
                programme.UpdatedBy = User.Identity.Name;

                // Update the entity
                _context.Entry(programme).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Programme has been updated successfully.";
                return RedirectToAction("Programmes");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Programmes.Any(e => e.Id == programme.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error updating programme: {ex}");

                // Reload ViewBag data
                LoadViewBagDataForUpdate(programme);

                ModelState.AddModelError("", "An error occurred while updating the programme. Please try again.");
                TempData["Error"] = "An error occurred while updating the programme. Please try again.";
                return View(programme);
            }
        }


        // Helper method to load ViewBag data for update
        private void LoadViewBagDataForUpdate(Programme programme)
        {
            // Fetch users excluding those in the "Candidate" or "Student" roles
            var coordinatorUsers = _context.Users
                .Where(u => !_context.UserRoles
                    .Any(ur => ur.UserId == u.Id &&
                               (_context.Roles.Any(r => r.Id == ur.RoleId &&
                                                       (r.Name == "Candidate" || r.Name == "Student")))))
                .Select(u => new { u.Id, DisplayName = u.FullName ?? u.UserName })
                .ToList();

            // Populate dropdowns
            ViewBag.DepartmentId = new SelectList(_context.Departments, "Id", "Name", programme?.DepartmentId);
            ViewBag.ModeOfStudyId = new SelectList(_context.ModesOfStudy, "ModeId", "ModeName", programme?.ModeOfStudyId);
            ViewBag.ProgrammeLevelId = new SelectList(_context.ProgramLevels, "Id", "Name", programme?.ProgrammeLevelId);
            ViewBag.CoordinatorId = new SelectList(coordinatorUsers, "Id", "DisplayName", programme?.CoordinatorId);

            // Add NQ Programmes dropdown (exclude current programme and only show NQ programmes)
            var nqProgrammes = _context.Programmes
                .Where(p => p.IsNonQuota == true && p.Id != programme.Id)
                .Include(p => p.Department)
                .ThenInclude(d => d.School)
                .Select(p => new {
                    p.Id,
                    DisplayName = $"{p.Name} ({p.Department.School.Name})"
                })
                .ToList();

            ViewBag.AssociatedNQProgrammeId = new SelectList(nqProgrammes, "Id", "DisplayName", programme?.AssociatedNQProgrammeId);

            // Add statistics
            var programmes = _context.Programmes.Include(p => p.Department).ThenInclude(d => d.School).ToList();
            ViewBag.TotalProgrammes = programmes.Count;
            ViewBag.TotalRegularProgrammes = programmes.Count(p => !p.IsNonQuota);
            ViewBag.TotalNQProgrammes = programmes.Count(p => p.IsNonQuota);
            ViewBag.TotalSchools = programmes
                .Where(p => p.Department?.School != null)
                .Select(p => p.Department.School.Id)
                .Distinct()
                .Count();
            ViewBag.TotalCoordinators = programmes
                .Where(p => p.CoordinatorId != null)
                .Select(p => p.CoordinatorId)
                .Distinct()
                .Count();
            ViewBag.AverageDuration = programmes.Any() ? programmes.Average(p => p.DurationYears) : 0.0;
            ViewBag.TotalDepartments = programmes
                .Where(p => p.Department != null)
                .Select(p => p.DepartmentId)
                .Distinct()
                .Count();

            // Add chart data for programme distribution
            var programmeLevels = _context.Programmes
                .Include(p => p.ProgrammeLevel)
                .Where(p => p.ProgrammeLevel != null)
                .GroupBy(p => p.ProgrammeLevel.Name)
                .Select(g => new { Level = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.ChartData = programmeLevels;
        }

        // GET: Programmes/Delete/5
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteProgramme(int id)
        {
            var programme = _context.Programmes
                .Include(p => p.Department)
                    .ThenInclude(d => d.School)  // Include School details
                .Include(p => p.ModeOfStudy)     // Include Mode of Study
                .Include(p => p.ProgrammeLevel)   // Include Programme Level
                .Include(p => p.Coordinator)      // Include Coordinator
                .Include(p => p.AssociatedNQProgramme) // Include associated NQ programme
                .Include(p => p.AssociatedRegularProgrammes) // Include programmes that depend on this one
                .FirstOrDefault(p => p.Id == id);

            if (programme == null)
            {
                return NotFound();
            }

            // Check if this programme has dependent programmes
            if (programme.AssociatedRegularProgrammes.Any())
            {
                TempData["Error"] = $"Cannot delete this programme because it is associated with {programme.AssociatedRegularProgrammes.Count} other programme(s).";
                return RedirectToAction("Programmes");
            }

            // Add summary statistics for the sidebar
            ViewBag.TotalProgrammes = _context.Programmes.Count();
            ViewBag.TotalRegularProgrammes = _context.Programmes.Count(p => !p.IsNonQuota);
            ViewBag.TotalNQProgrammes = _context.Programmes.Count(p => p.IsNonQuota);
            ViewBag.TotalSchools = _context.Departments.Select(d => d.SchoolId).Distinct().Count();
            ViewBag.TotalCoordinators = _context.Programmes.Where(p => p.CoordinatorId != null).Select(p => p.CoordinatorId).Distinct().Count();
            ViewBag.AverageDuration = _context.Programmes.Average(p => p.DurationYears);
            ViewBag.TotalDepartments = _context.Departments.Count();
            ViewBag.TotalEnrollments = _context.Programmes.Sum(p => p.EnrollmentCount);

            return View(programme);
        }

        // POST: Programmes/Delete/5
        [HttpPost, ActionName("DeleteProgramme")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProgrammeConfirmed(int id)
        {
            var programme = await _context.Programmes
                .Include(p => p.Department)
                .Include(p => p.ModeOfStudy)
                .Include(p => p.ProgrammeLevel)
                .Include(p => p.Coordinator)
                .Include(p => p.AssociatedNQProgramme)
                .Include(p => p.AssociatedRegularProgrammes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (programme == null)
            {
                return NotFound();
            }

            try
            {
                // Check if this programme has dependent programmes
                if (programme.AssociatedRegularProgrammes.Any())
                {
                    TempData["Error"] = $"Cannot delete this programme because it is associated with {programme.AssociatedRegularProgrammes.Count} other programme(s).";
                    return RedirectToAction("Programmes");
                }

                _context.Programmes.Remove(programme);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Programme has been deleted successfully.";
                return RedirectToAction("Programmes");
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error deleting programme: {ex}");

                ModelState.AddModelError("", "An error occurred while deleting the programme. Please try again.");
                TempData["Error"] = "An error occurred while deleting the programme. Please try again.";

                // Reload statistics for the sidebar
                ViewBag.TotalProgrammes = _context.Programmes.Count();
                ViewBag.TotalRegularProgrammes = _context.Programmes.Count(p => !p.IsNonQuota);
                ViewBag.TotalNQProgrammes = _context.Programmes.Count(p => p.IsNonQuota);
                ViewBag.TotalSchools = _context.Departments.Select(d => d.SchoolId).Distinct().Count();
                ViewBag.TotalCoordinators = _context.Programmes.Where(p => p.CoordinatorId != null).Select(p => p.CoordinatorId).Distinct().Count();
                ViewBag.AverageDuration = _context.Programmes.Average(p => p.DurationYears);
                ViewBag.TotalDepartments = _context.Departments.Count();

                return View(programme);
            }
        }

        // Helper method to load ViewBag data
        private async Task LoadViewBagData(Programme model = null)
        {
            var coordinatorUsers = _context.Users
                .Where(u => !_context.UserRoles
                    .Any(ur => ur.UserId == u.Id &&
                               (_context.Roles.Any(r => r.Id == ur.RoleId &&
                                                       (r.Name == "Candidate" || r.Name == "Student")))))
                .Select(u => new { u.Id, DisplayName = u.FullName ?? u.UserName })
                .ToList();

            ViewBag.DepartmentId = new SelectList(_context.Departments, "Id", "Name", model?.DepartmentId);
            ViewBag.ModeOfStudyId = new SelectList(_context.ModesOfStudy, "ModeId", "ModeName", model?.ModeOfStudyId);
            ViewBag.ProgrammeLevelId = new SelectList(_context.ProgramLevels, "Id", "Name", model?.ProgrammeLevelId);
            ViewBag.CoordinatorId = new SelectList(coordinatorUsers, "Id", "DisplayName", model?.CoordinatorId);

            // Add statistics
            var programmes = await _context.Programmes
                .Include(p => p.Department)
                .ThenInclude(d => d.School)
                .ToListAsync();

            ViewBag.TotalProgrammes = programmes.Count;
            ViewBag.TotalSchools = programmes
                .Where(p => p.Department?.School != null)
                .Select(p => p.Department.School.Id)
                .Distinct()
                .Count();
            ViewBag.TotalCoordinators = programmes
                .Where(p => p.CoordinatorId != null)
                .Select(p => p.CoordinatorId)
                .Distinct()
                .Count();
            ViewBag.AverageDuration = programmes.Any() ? programmes.Average(p => p.DurationYears) : 0.0;
            ViewBag.TotalDepartments = programmes
                .Where(p => p.Department != null)
                .Select(p => p.DepartmentId)
                .Distinct()
                .Count();

            // Add chart data for programme distribution
            var programmeLevels = await _context.Programmes
                .Include(p => p.ProgrammeLevel)
                .Where(p => p.ProgrammeLevel != null)
                .GroupBy(p => p.ProgrammeLevel.Name)
                .Select(g => new { Level = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.ChartData = programmeLevels;
        }


        // GET: Admin/Buildings
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Buildings()
        {
            // Load buildings with their related schools
            var buildings = await _context.Buildings
                .Include(b => b.School)
                .ToListAsync();

            // Prepare schools for dropdowns
            ViewBag.Schools = new SelectList(_context.Schools, "Id", "Name");

            return View(buildings);
        }

        // POST: Admin/CreateBuilding
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateBuilding(Building building)
        {
            try
            {
                if (building != null)
                {
                    //building.CreatedAt = DateTime.Now;
                    //building.CreatedBy = User.Identity.Name;

                    _context.Buildings.Add(building);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Buildings));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating building: {ex}");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Error: {error.ErrorMessage}");
                }
            }

            // If we get here, something went wrong
            ViewBag.Schools = new SelectList(_context.Schools, "Id", "Name", building?.SchoolId);
            return RedirectToAction(nameof(Buildings));
        }

        // POST: Admin/UpdateBuilding
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateBuilding(Building building)
        {
            try
            {
                var existingBuilding = await _context.Buildings.FindAsync(building.Id);
                if (existingBuilding == null)
                {
                    return NotFound();
                }

                existingBuilding.Name = building.Name;
                existingBuilding.Description = building.Description;
                existingBuilding.SchoolId = building.SchoolId;
                //existingBuilding.UpdatedAt = DateTime.Now;
                //existingBuilding.UpdatedBy = User.Identity.Name;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Buildings));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating building: {ex}");
                ViewBag.Schools = new SelectList(_context.Schools, "Id", "Name", building.SchoolId);
                return RedirectToAction(nameof(Buildings));
            }
        }

        // POST: Admin/DeleteBuilding
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteBuilding(int id)
        {
            try
            {
                var building = await _context.Buildings.FindAsync(id);
                if (building == null)
                {
                    return NotFound();
                }

                _context.Buildings.Remove(building);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Buildings));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting building: {ex}");
                return RedirectToAction(nameof(Buildings));
            }
        }

        // GET: LearningRooms
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LearningRooms()
        {
            ViewBag.BuildingId = new SelectList(_context.Buildings, "Id", "Name");
            return View(await _context.LearningRooms
                .Include(lr => lr.Building)
                .ToListAsync());
        }

        // POST: LearningRooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateLearningRoom([Bind("Name,Description,BuildingId,RoomType,LearningCapacity,ExamCapacity,Area")] LearningRoom learningRoom)
        {
            learningRoom.Building = await _context.Buildings.FindAsync(learningRoom.BuildingId);
            if (ModelState.IsValid)
            {
                _context.Add(learningRoom);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Learning room created successfully.";
                return RedirectToAction(nameof(LearningRooms));
            }

            TempData["Error"] = "Error creating learning room. Please check your input.";
            return RedirectToAction(nameof(LearningRooms));
        }

        // POST: LearningRooms/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateLearningRoom([Bind("Id,Name,Description,BuildingId,RoomType,LearningCapacity,ExamCapacity,Area")] LearningRoom learningRoom)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(learningRoom);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Learning room updated successfully.";
                    return RedirectToAction(nameof(LearningRooms));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.LearningRooms.Any(e => e.Id == learningRoom.Id))
                    {
                        TempData["Error"] = "Learning room not found.";
                        return RedirectToAction(nameof(LearningRooms));
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            TempData["Error"] = "Error updating learning room. Please check your input.";
            return RedirectToAction(nameof(LearningRooms));
        }

        // POST: LearningRooms/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteLearningRoom(int id)
        {
            var learningRoom = await _context.LearningRooms.FindAsync(id);
            if (learningRoom == null)
            {
                TempData["Error"] = "Learning room not found.";
                return RedirectToAction(nameof(LearningRooms));
            }

            try
            {
                _context.LearningRooms.Remove(learningRoom);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Learning room deleted successfully.";
            }
            catch (Exception)
            {
                TempData["Error"] = "Error deleting learning room.";
            }

            return RedirectToAction(nameof(LearningRooms));
        }


        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TimeSlotConfigurations()
        {
            var configurations = await _context.TimeSlotConfigurations
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // Get data for sidebar statistics
            ViewBag.TotalConfigurations = configurations.Count;
            ViewBag.ActiveConfigurations = configurations.Count(c => c.IsActive);
            ViewBag.TotalPeriods = configurations.Sum(c =>
                System.Text.Json.JsonSerializer.Deserialize<List<dynamic>>(c.PeriodsData).Count);

            return View(configurations);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTimeSlotConfig(int id)
        {
            var config = await _context.TimeSlotConfigurations
                .FirstOrDefaultAsync(t => t.Id == id);

            if (config == null)
            {
                return NotFound();
            }

            return Json(config);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateTimeSlotConfig(TimeSlotConfiguration config)
        {
            try
            {
                config.CreatedBy = User.Identity.Name;
                config.CreatedAt = DateTime.Now;

                _context.TimeSlotConfigurations.Add(config);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Time slot configuration created successfully.";
                return RedirectToAction(nameof(TimeSlotConfigurations));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating time slot configuration. Please try again.";
                ModelState.AddModelError("", "Error creating time slot configuration. Please try again.");
            }

            return RedirectToAction(nameof(TimeSlotConfigurations));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult CreateTimeSlotConfig()
        {
            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTimeSlotConfig(int id)
        {
            var config = await _context.TimeSlotConfigurations
                .FirstOrDefaultAsync(t => t.Id == id);

            if (config == null)
            {
                return NotFound();
            }

            return View(config);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTimeSlotConfig(TimeSlotConfiguration config)
        {
            try
            {
                var existingConfig = await _context.TimeSlotConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == config.Id);

                if (existingConfig == null)
                {
                    TempData["Error"] = "Time slot configuration not found.";
                    return NotFound();
                }

                // Preserve the creation audit fields
                config.CreatedBy = existingConfig.CreatedBy;
                config.CreatedAt = existingConfig.CreatedAt;

                // Update the modification audit fields
                config.UpdatedAt = DateTime.Now;
                config.UpdatedBy = User.Identity.Name;

                _context.Entry(config).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Time slot configuration updated successfully.";
                return RedirectToAction(nameof(TimeSlotConfigurations));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TimeSlotConfigExists(config.Id))
                {
                    TempData["Error"] = "Time slot configuration not found.";
                    return NotFound();
                }
                else
                {
                    TempData["Error"] = "Error updating time slot configuration. Please try again.";
                    ModelState.AddModelError("", "Error updating time slot configuration. Please try again.");
                }
            }

            return RedirectToAction(nameof(TimeSlotConfigurations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteTimeSlotConfig(int id)
        {
            var config = await _context.TimeSlotConfigurations.FindAsync(id);
            if (config == null)
            {
                TempData["Error"] = "Time slot configuration not found.";
                return NotFound();
            }

            try
            {
                _context.TimeSlotConfigurations.Remove(config);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Time slot configuration deleted successfully.";
                return RedirectToAction(nameof(TimeSlotConfigurations));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting time slot configuration. Please try again.";
                ModelState.AddModelError("", "Error deleting time slot configuration. Please try again.");
                return RedirectToAction(nameof(TimeSlotConfigurations));
            }
        }

        private bool TimeSlotConfigExists(int id)
        {
            return _context.TimeSlotConfigurations.Any(e => e.Id == id);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> WorkingDayConfigurations()
        {
            var configurations = await _context.WorkingDayConfigurations
                .Include(w => w.AcademicYear)
                .Include(w => w.ModeOfStudy)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            // Get data for sidebar statistics
            ViewBag.TotalConfigurations = configurations.Count;
            ViewBag.ActiveConfigurations = configurations.Count(c => c.IsActive);

            // Group by mode of study, handling null values
            ViewBag.ConfigurationsByMode = configurations
                .Where(c => c.ModeOfStudy != null)
                .GroupBy(c => c.ModeOfStudy.ModeName ?? "Unspecified")
                .ToDictionary(g => g.Key, g => g.Count());

            return View(configurations);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateWorkingDayConfig()
        {
            // Get time slot configurations for dropdowns
            ViewBag.TimeSlotConfigs = await _context.TimeSlotConfigurations
                .Where(t => t.IsActive)
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name
                })
                .ToListAsync();

            // Get academic years
            ViewBag.AcademicYears = await _context.AcademicYears
                .Where(a => a.IsActive)
                .Select(a => new SelectListItem
                {
                    Value = a.YearId.ToString(),
                    Text = a.YearValue
                })
                .ToListAsync();

            // Get modes of study
            ViewBag.ModesOfStudy = await _context.ModesOfStudy
                .Select(m => new SelectListItem
                {
                    Value = m.ModeId.ToString(),
                    Text = m.ModeName
                })
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateWorkingDayConfig(WorkingDayConfiguration config)
        {
            try
            {
                // Check for existing configuration
                var existingConfig = await _context.WorkingDayConfigurations
                    .FirstOrDefaultAsync(w =>
                        w.AcademicYearId == config.AcademicYearId &&
                        w.ModeOfStudyId == config.ModeOfStudyId);

                if (existingConfig != null)
                {
                    ModelState.AddModelError("", "A configuration already exists for this Academic Year and Mode of Study combination.");

                    TempData["Error"] = "A configuration already exists for this Academic Year and Mode of Study combination.";
                    await PopulateDropdowns();
                    return View(config);
                }

                // Validate that selected IDs exist
                var academicYear = await _context.AcademicYears.FindAsync(config.AcademicYearId);
                var modeOfStudy = await _context.ModesOfStudy.FindAsync(config.ModeOfStudyId);

                if (academicYear == null || modeOfStudy == null)
                {
                    ModelState.AddModelError("", "Selected Academic Year or Mode of Study is invalid.");
                    await PopulateDropdowns();
                    return View(config);
                }

                config.CreatedBy = User.Identity.Name;
                config.CreatedAt = DateTime.Now;

                _context.WorkingDayConfigurations.Add(config);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Working day configuration created successfully.";
                return RedirectToAction(nameof(WorkingDayConfigurations));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating working day configuration. Please try again.";
                ModelState.AddModelError("", "Error creating working day configuration. Please try again.");
            }

            await PopulateDropdowns();
            return View(config);
        }

        // Helper method to populate dropdowns
        private async Task PopulateDropdowns()
        {
            ViewBag.TimeSlotConfigs = await _context.TimeSlotConfigurations
                .Where(t => t.IsActive)
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name
                })
                .ToListAsync();

            ViewBag.AcademicYears = await _context.AcademicYears
                .Where(a => a.IsActive)
                .Select(a => new SelectListItem
                {
                    Value = a.YearId.ToString(),
                    Text = a.YearValue
                })
                .ToListAsync();

            ViewBag.ModesOfStudy = await _context.ModesOfStudy
                .Select(m => new SelectListItem
                {
                    Value = m.ModeId.ToString(),
                    Text = m.ModeName
                })
                .ToListAsync();
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateWorkingDayConfig(int id)
        {
            var config = await _context.WorkingDayConfigurations
                .FirstOrDefaultAsync(m => m.Id == id);

            if (config == null)
            {
                TempData["Error"] = "Configuration not found.";
                return RedirectToAction(nameof(WorkingDayConfigurations));
            }

            await PopulateDropdowns();
            return View(config);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateWorkingDayConfig(int id, WorkingDayConfiguration config)
        {
            try
            {
                if (id != config.Id)
                {
                    TempData["Error"] = "Invalid configuration ID.";
                    return RedirectToAction(nameof(WorkingDayConfigurations));
                }

                var existingConfig = await _context.WorkingDayConfigurations
                    .FirstOrDefaultAsync(w => w.Id == id);

                if (existingConfig == null)
                {
                    TempData["Error"] = "Configuration not found.";
                    return RedirectToAction(nameof(WorkingDayConfigurations));
                }

                // Check for duplicate configuration
                var duplicateConfig = await _context.WorkingDayConfigurations
                    .FirstOrDefaultAsync(w =>
                        w.Id != id &&
                        w.AcademicYearId == config.AcademicYearId &&
                        w.ModeOfStudyId == config.ModeOfStudyId);

                if (duplicateConfig != null)
                {
                    ModelState.AddModelError("", "A configuration already exists for this Academic Year and Mode of Study combination.");
                    TempData["Error"] = "A configuration already exists for this Academic Year and Mode of Study combination.";
                    await PopulateDropdowns();
                    return View(config);
                }

                // Validate that selected IDs exist
                var academicYear = await _context.AcademicYears.FindAsync(config.AcademicYearId);
                var modeOfStudy = await _context.ModesOfStudy.FindAsync(config.ModeOfStudyId);

                if (academicYear == null || modeOfStudy == null)
                {
                    ModelState.AddModelError("", "Selected Academic Year or Mode of Study is invalid.");
                    await PopulateDropdowns();
                    return View(config);
                }

                // Update existing configuration
                existingConfig.AcademicYearId = config.AcademicYearId;
                existingConfig.ModeOfStudyId = config.ModeOfStudyId;
                existingConfig.IsActive = config.IsActive;
                existingConfig.WorkingDaysData = config.WorkingDaysData;
                existingConfig.UpdatedBy = User.Identity.Name;
                existingConfig.UpdatedAt = DateTime.Now;

                _context.Entry(existingConfig).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Working day configuration updated successfully.";
                return RedirectToAction(nameof(WorkingDayConfigurations));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!WorkingDayConfigurationExists(id))
                {
                    TempData["Error"] = "Configuration not found.";
                    return RedirectToAction(nameof(WorkingDayConfigurations));
                }
                else
                {
                    TempData["Error"] = "The configuration was modified by another user. Please try again.";
                    await PopulateDropdowns();
                    return View(config);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating working day configuration. Please try again.";
                ModelState.AddModelError("", "Error updating working day configuration. Please try again.");
                await PopulateDropdowns();
                return View(config);
            }
        }

        private bool WorkingDayConfigurationExists(int id)
        {
            return _context.WorkingDayConfigurations.Any(e => e.Id == id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteWorkingDayConfig(int id)
        {
            var config = await _context.WorkingDayConfigurations.FindAsync(id);
            if (config == null)
            {
                TempData["Error"] = "Working day configuration not found.";
                return NotFound();
            }

            try
            {
                _context.WorkingDayConfigurations.Remove(config);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Working day configuration deleted successfully.";
                return RedirectToAction(nameof(WorkingDayConfigurations));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting working day configuration. Please try again.";
                return RedirectToAction(nameof(WorkingDayConfigurations));
            }
        }



        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Departments()
        {
            var departments = await _context.Departments
                .Include(d => d.School)
                .Include(d => d.HOD)
                .ToListAsync();

            // Get data for sidebar statistics
            ViewBag.TotalPrograms = await _context.Programmes.CountAsync();

            // Populate ViewBag for dropdowns
            var coordinators = await _context.Users
                .Where(u => !_context.UserRoles
                    .Any(ur => ur.UserId == u.Id &&
                              (_context.Roles.Any(r => r.Id == ur.RoleId &&
                                                      (r.Name == "Candidate" || r.Name == "Student")))))
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync();

            ViewBag.Schools = new SelectList(await _context.Schools.ToListAsync(), "Id", "Name");
            ViewBag.HODs = new SelectList(coordinators, "Id", "UserName");

            return View(departments);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDepartment(int id)
        {
            var department = await _context.Departments
                .Include(d => d.School)
                .Include(d => d.HOD)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
            {
                return NotFound();
            }

            var departmentDto = new
            {
                id = department.Id,
                name = department.Name,
                description = department.Description,
                schoolId = department.SchoolId,
                hodId = department.HODId,
                schoolName = department.School?.Name,
                hodName = department.HOD?.UserName
            };

            return Json(departmentDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateDepartment(Department department)
        {
            
            try
            {
                department.CreatedBy = User.Identity.Name;
                department.CreatedAt = DateTime.Now;

                _context.Departments.Add(department);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Departments));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating department. Please try again.");
            }
            

            // If we got this far, something failed, redisplay form
            return RedirectToAction(nameof(Departments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateDepartment(Department department)
        {
            try
            {
                var existingDepartment = await _context.Departments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == department.Id);

                if (existingDepartment == null)
                {
                    return NotFound();
                }

                // Preserve the creation audit fields
                department.CreatedBy = existingDepartment.CreatedBy;
                department.CreatedAt = existingDepartment.CreatedAt;

                // Update the modification audit fields
                department.UpdatedAt = DateTime.Now;
                department.UpdatedBy = User.Identity.Name;

                _context.Entry(department).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Departments));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DepartmentExists(department.Id))
                {
                    return NotFound();
                }
                else
                {
                    ModelState.AddModelError("", "Error updating department. Please try again.");
                }
            }
            

            return RedirectToAction(nameof(Departments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
            {
                return NotFound();
            }

            try
            {
                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Departments));
            }
            catch (Exception ex)
            {
                // Log the error
                ModelState.AddModelError("", "Error deleting department. Please try again.");
                return RedirectToAction(nameof(Departments));
            }
        }

        private bool DepartmentExists(int id)
        {
            return _context.Departments.Any(e => e.Id == id);
        }

        // GET: Admin/Courses - Updated main method
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Courses()
        {
            // Load only filter dropdown data
            var yearTakenValues = await _context.Courses
                .Select(c => c.YearTaken)
                .Distinct()
                .OrderBy(y => y)
                .ToListAsync();

            var programmes = await _context.Programmes
                .Select(p => new { p.Id, p.Name })
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.YearTakenValues = yearTakenValues;
            ViewBag.Programmes = programmes;

            return View();
        }

        // NEW: AJAX endpoint for course data
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<JsonResult> GetCoursesData([FromBody] CourseFilterRequest request)
        {
            try
            {
                var query = _context.Courses
                    .Include(c => c.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    var searchTerm = request.SearchTerm.ToLower();
                    query = query.Where(c =>
                        c.CourseCode.ToLower().Contains(searchTerm) ||
                        c.CourseName.ToLower().Contains(searchTerm));
                }

                if (request.CourseType != "all")
                {
                    query = query.Where(c => c.CourseType == request.CourseType);
                }

                if (request.Year != "all" && int.TryParse(request.Year, out int year))
                {
                    query = query.Where(c => c.YearTaken == year);
                }

                if (request.Programme != "all")
                {
                    query = query.Where(c => c.Programme.Name == request.Programme);
                }

                // Get total count before pagination
                var totalRecords = await query.CountAsync();

                // Apply sorting
                switch (request.SortColumn.ToLower())
                {
                    case "code":
                        query = request.SortDirection == "asc"
                            ? query.OrderBy(c => c.CourseCode)
                            : query.OrderByDescending(c => c.CourseCode);
                        break;
                    case "name":
                        query = request.SortDirection == "asc"
                            ? query.OrderBy(c => c.CourseName)
                            : query.OrderByDescending(c => c.CourseName);
                        break;
                    case "type":
                        query = request.SortDirection == "asc"
                            ? query.OrderBy(c => c.CourseType)
                            : query.OrderByDescending(c => c.CourseType);
                        break;
                    case "year":
                        query = request.SortDirection == "asc"
                            ? query.OrderBy(c => c.YearTaken)
                            : query.OrderByDescending(c => c.YearTaken);
                        break;
                    case "semester":
                        query = request.SortDirection == "asc"
                            ? query.OrderBy(c => c.SemesterTaken)
                            : query.OrderByDescending(c => c.SemesterTaken);
                        break;
                    case "mandatory":
                        query = request.SortDirection == "asc"
                            ? query.OrderBy(c => c.IsMandatory)
                            : query.OrderByDescending(c => c.IsMandatory);
                        break;
                    default:
                        query = query.OrderBy(c => c.Programme.Department.School.Name);
                        query = query.OrderBy(c => c.Programme.Name);
                        break;
                }

                // Apply pagination
                var courses = await query
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(c => new CourseViewModel
                    {
                        Id = c.Id,
                        CourseCode = c.CourseCode,
                        CourseName = c.CourseName,
                        CourseType = c.CourseType,
                        YearTaken = c.YearTaken,
                        SemesterTaken = c.SemesterTaken,
                        IsMandatory = c.IsMandatory,
                        ProgrammeName = c.Programme.Name,
                        SchoolName = c.Programme.Department.School.Name
                    })
                    .ToListAsync();

                // Add row numbers
                var startIndex = (request.Page - 1) * request.PageSize;
                for (int i = 0; i < courses.Count; i++)
                {
                    courses[i].RowNumber = startIndex + i + 1;
                }

                var totalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize);

                var response = new CourseListResponse
                {
                    Courses = courses,
                    TotalRecords = totalRecords,
                    TotalPages = totalPages,
                    CurrentPage = request.Page,
                    PageSize = request.PageSize
                };

                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // NEW: AJAX endpoint for statistics
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<JsonResult> GetCourseStatistics()
        {
            try
            {
                var totalCourses = await _context.Courses.CountAsync();
                var fullCourses = await _context.Courses.CountAsync(c => c.CourseType == "Full Course");
                var mandatoryCourses = await _context.Courses.CountAsync(c => c.IsMandatory);

                var courseDistribution = await _context.Courses
                    .GroupBy(c => c.CourseType)
                    .Select(g => new CourseTypeDistribution
                    {
                        Type = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var statistics = new CourseStatistics
                {
                    TotalCourses = totalCourses,
                    FullCourses = fullCourses,
                    HalfCourses = totalCourses - fullCourses,
                    MandatoryCourses = mandatoryCourses,
                    CourseDistribution = courseDistribution
                };

                return Json(statistics);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Admin/CreateCourse
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCourse()
        {
            // Populate all select lists with materialized data to ensure proper binding
            ViewBag.Programmes = new SelectList(await _context.Programmes.ToListAsync(), "Id", "Name");

            var lecturers = await _context.Users
                .Where(u => _context.UserRoles
                    .Any(ur => ur.UserId == u.Id &&
                        _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Lecturer")))
                .ToListAsync();

            ViewBag.Lecturers = new MultiSelectList(lecturers, "Id", "FullName");
            ViewBag.Instructors = new SelectList(lecturers, "Id", "FullName");

            var courses = await _context.Courses.ToListAsync();
            ViewBag.Courses = new MultiSelectList(courses, "Id", "CourseName");

            var assessments = await _context.Assessments.ToListAsync();
            ViewBag.Assessments = new MultiSelectList(assessments, "Id", "Name");

            var venues = await _context.LearningRooms.ToListAsync();
            ViewBag.Venues = new MultiSelectList(venues, "Id", "Name");

            return View();
        }

        // POST: Admin/CreateCourse
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCourse(Course course,
            [FromForm] string[] LecturerIds,
            [FromForm] int[] PrerequisiteCourseIds,
            [FromForm] int[] AssessmentIds,
            [FromForm] int[] PreferredVenues)
        {
            try
            {
                // Set audit fields
                course.CreatedAt = DateTime.Now;
                course.CreatedBy = User.Identity.Name;
                course.UpdatedAt = DateTime.Now;
                course.UpdatedBy = User.Identity.Name;

                // Handle JSON serialized arrays
                if (PrerequisiteCourseIds != null && PrerequisiteCourseIds.Any())
                {
                    course.PrerequisiteCourseIds = System.Text.Json.JsonSerializer.Serialize(PrerequisiteCourseIds);
                }

                if (PreferredVenues != null && PreferredVenues.Any())
                {
                    course.PreferredVenueIds = System.Text.Json.JsonSerializer.Serialize(PreferredVenues);
                }

                // Add course to get the ID
                _context.Courses.Add(course);
                await _context.SaveChangesAsync();

                // Now add related records with the new course ID

                // Add lecturers
                if (LecturerIds != null && LecturerIds.Any())
                {
                    foreach (var lecturerId in LecturerIds)
                    {
                        _context.CourseLecturer.Add(new CourseLecturer
                        {
                            CourseId = course.Id,
                            LecturerId = lecturerId
                        });
                    }
                }

                // Add assessments
                if (AssessmentIds != null && AssessmentIds.Any())
                {
                    foreach (var assessmentId in AssessmentIds)
                    {
                        _context.CourseAssessment.Add(new CourseAssessment
                        {
                            CourseId = course.Id,
                            AssessmentId = assessmentId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Course created successfully.";
                return RedirectToAction("Courses");
                
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while creating the course: " + ex.Message);
            }

            // If we got this far, something failed, redisplay form with selected values
            var lecturers = await _context.Users
                .Where(u => _context.UserRoles
                    .Any(ur => ur.UserId == u.Id &&
                        _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Lecturer")))
                .ToListAsync();

            ViewBag.Programmes = new SelectList(await _context.Programmes.ToListAsync(), "Id", "Name", course.ProgrammeID);
            ViewBag.Lecturers = new MultiSelectList(lecturers, "Id", "FullName", LecturerIds);
            ViewBag.Instructors = new SelectList(lecturers, "Id", "FullName", course.InstructorId);

            var allCourses = await _context.Courses.ToListAsync();
            ViewBag.Courses = new MultiSelectList(allCourses, "Id", "CourseName", PrerequisiteCourseIds);

            var assessments = await _context.Assessments.ToListAsync();
            ViewBag.Assessments = new MultiSelectList(assessments, "Id", "Name", AssessmentIds);

            var venues = await _context.LearningRooms.ToListAsync();
            ViewBag.Venues = new MultiSelectList(venues, "Id", "Name", PreferredVenues);

            return View(course);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCourse(int? id)
        {
            if (id == null) return NotFound();

            var course = await _context.Courses
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .Include(c => c.CourseAssessments)
                    .ThenInclude(ca => ca.Assessment)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            // Populate ViewBag data for all dropdowns with materialized lists
            ViewBag.Programmes = new SelectList(await _context.Programmes.ToListAsync(), "Id", "Name", course.ProgrammeID);

            // Get lecturers and selected lecturer IDs
            var lecturers = await _context.Users
                .Where(u => _context.UserRoles
                    .Any(ur => ur.UserId == u.Id &&
                        _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Lecturer")))
                .ToListAsync();

            var selectedLecturerIds = course.CourseLecturers.Select(cl => cl.LecturerId).ToList();
            ViewBag.Lecturers = new MultiSelectList(lecturers, "Id", "FullName", selectedLecturerIds);
            ViewBag.Instructors = new SelectList(lecturers, "Id", "FullName", course.InstructorId);

            // Get prerequisite course IDs from JSON if available
            List<int> selectedPrereqIds = new List<int>();
            if (!string.IsNullOrEmpty(course.PrerequisiteCourseIds))
            {
                try
                {
                    selectedPrereqIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(course.PrerequisiteCourseIds);
                }
                catch
                {
                    // If deserialization fails, just use empty list
                    selectedPrereqIds = new List<int>();
                }
            }

            var availableCourses = await _context.Courses.Where(c => c.Id != id).ToListAsync();
            ViewBag.Courses = new MultiSelectList(availableCourses, "Id", "CourseName", selectedPrereqIds);

            // Get assessments and selected assessment IDs
            var assessments = await _context.Assessments.ToListAsync();
            var selectedAssessmentIds = course.CourseAssessments.Select(ca => ca.AssessmentId).ToList();
            ViewBag.Assessments = new MultiSelectList(assessments, "Id", "Name", selectedAssessmentIds);

            // Get venues and selected venue IDs
            var venues = await _context.LearningRooms.ToListAsync();
            List<int> selectedVenueIds = new List<int>();

            if (!string.IsNullOrEmpty(course.PreferredVenueIds))
            {
                try
                {
                    selectedVenueIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(course.PreferredVenueIds);
                }
                catch
                {
                    selectedVenueIds = new List<int>();
                }
            }

            ViewBag.Venues = new MultiSelectList(venues, "Id", "Name", selectedVenueIds);

            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCourse(Course course,
            [FromForm] string[] LecturerIds,
            [FromForm] int[] PrerequisiteCourseIds,
            [FromForm] int[] AssessmentIds,
            [FromForm] int[] PreferredVenues)
        {
            try
            {
                //if (!ModelState.IsValid)
                //{
                //    // Log model state errors
                //    foreach (var state in ModelState)
                //    {
                //        if (state.Value.Errors.Count > 0)
                //        {
                //            Console.WriteLine($"Field {state.Key} has errors: {string.Join(", ", state.Value.Errors.Select(e => e.ErrorMessage))}");
                //        }
                //    }
                //    throw new Exception("Model validation failed");
                //}

                var existingCourse = await _context.Courses
                    .Include(c => c.CourseLecturers)
                    .Include(c => c.CourseAssessments)
                    .FirstOrDefaultAsync(c => c.Id == course.Id);

                if (existingCourse == null) return NotFound();

                // Update basic properties
                existingCourse.CourseCode = course.CourseCode;
                existingCourse.CourseName = course.CourseName;
                existingCourse.CourseDescription = course.CourseDescription;
                existingCourse.CourseType = course.CourseType;
                existingCourse.YearTaken = course.YearTaken;
                existingCourse.SemesterTaken = course.SemesterTaken;
                existingCourse.ProgrammeID = course.ProgrammeID;
                existingCourse.PassMark = course.PassMark;
                existingCourse.IsMandatory = course.IsMandatory;
                existingCourse.IsExaminable = course.IsExaminable;

                // Update timetabling fields
                existingCourse.InstructorId = course.InstructorId;
                existingCourse.MeetingFrequencyPerWeek = course.MeetingFrequencyPerWeek;
                existingCourse.CapacityRequired = course.CapacityRequired;

                // Update PreferredVenueIds
                if (PreferredVenues != null && PreferredVenues.Any())
                {
                    existingCourse.PreferredVenueIds = System.Text.Json.JsonSerializer.Serialize(PreferredVenues);
                }
                else
                {
                    existingCourse.PreferredVenueIds = null;
                }

                // Update prerequisite courses
                if (PrerequisiteCourseIds != null && PrerequisiteCourseIds.Any())
                {
                    existingCourse.PrerequisiteCourseIds = System.Text.Json.JsonSerializer.Serialize(PrerequisiteCourseIds);
                }
                else
                {
                    existingCourse.PrerequisiteCourseIds = null;
                }

                // Update audit fields
                existingCourse.UpdatedAt = DateTime.Now;
                existingCourse.UpdatedBy = User.Identity.Name;

                // Clear and recreate lecturers
                _context.CourseLecturer.RemoveRange(existingCourse.CourseLecturers);
                await _context.SaveChangesAsync(); // Save to avoid constraint issues

                if (LecturerIds != null && LecturerIds.Any())
                {
                    foreach (var lecturerId in LecturerIds)
                    {
                        _context.CourseLecturer.Add(new CourseLecturer
                        {
                            CourseId = existingCourse.Id,
                            LecturerId = lecturerId
                        });
                    }
                }

                // Clear and recreate assessments
                _context.CourseAssessment.RemoveRange(existingCourse.CourseAssessments);
                await _context.SaveChangesAsync(); // Save to avoid constraint issues

                if (AssessmentIds != null && AssessmentIds.Any())
                {
                    foreach (var assessmentId in AssessmentIds)
                    {
                        _context.CourseAssessment.Add(new CourseAssessment
                        {
                            CourseId = existingCourse.Id,
                            AssessmentId = assessmentId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Course updated successfully.";
                return RedirectToAction("Courses");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while updating the course: " + ex.Message);

                // Repopulate ViewBag with properly materialized lists
                var lecturers = await _context.Users
                    .Where(u => _context.UserRoles
                        .Any(ur => ur.UserId == u.Id &&
                            _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Lecturer")))
                    .ToListAsync();

                ViewBag.Programmes = new SelectList(await _context.Programmes.ToListAsync(), "Id", "Name", course.ProgrammeID);
                ViewBag.Lecturers = new MultiSelectList(lecturers, "Id", "FullName", LecturerIds);
                ViewBag.Instructors = new SelectList(lecturers, "Id", "FullName", course.InstructorId);

                var availableCourses = await _context.Courses.Where(c => c.Id != course.Id).ToListAsync();
                ViewBag.Courses = new MultiSelectList(availableCourses, "Id", "CourseName", PrerequisiteCourseIds);

                var assessments = await _context.Assessments.ToListAsync();
                ViewBag.Assessments = new MultiSelectList(assessments, "Id", "Name", AssessmentIds);

                var venues = await _context.LearningRooms.ToListAsync();
                ViewBag.Venues = new MultiSelectList(venues, "Id", "Name", PreferredVenues);

                return View(course);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCourse(int? id)
        {
            if (id == null) return NotFound();

            var course = await _context.Courses
                .Include(c => c.Programme)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .Include(c => c.Prerequisites)
                .Include(c => c.CourseAssessments)
                    .ThenInclude(ca => ca.Assessment)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost, ActionName("DeleteCourse")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCourseConfirmed(int id)
        {
            try
            {
                var course = await _context.Courses
                    .Include(c => c.CourseLecturers)
                    .Include(c => c.Prerequisites)
                    .Include(c => c.CourseAssessments)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (course == null) return NotFound();

                // Remove all related records first
                _context.CourseLecturer.RemoveRange(course.CourseLecturers);
                _context.CoursePrerequisites.RemoveRange(course.Prerequisites);
                _context.CourseAssessment.RemoveRange(course.CourseAssessments);

                // Save changes to ensure no constraint violations
                await _context.SaveChangesAsync();

                // Then remove the course
                _context.Courses.Remove(course);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Course deleted successfully.";
                return RedirectToAction("Courses");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting course: {ex.Message}";
                return RedirectToAction("Courses");
            }
        }

        /// <summary>
        /// Provides statistics about courses for dashboard displays
        /// </summary>
        /// <returns>JSON object with course statistics</returns>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetCourseStats()
        {
            try
            {
                // Get all courses with their related data
                var courses = await _context.Courses
                    .Include(c => c.Programme)
                    .ToListAsync();

                // Get total courses count
                int totalCourses = courses.Count;

                // Get full courses count
                int fullCourses = courses.Count(c => c.CourseType == "Full Course");

                // Get half courses count
                int halfCourses = courses.Count(c => c.CourseType == "Half Course");

                // Get mandatory courses count
                int mandatoryCourses = courses.Count(c => c.IsMandatory);

                // Get optional courses count
                int optionalCourses = courses.Count(c => !c.IsMandatory);

                // Get distinct count of lecturers assigned to courses
                int lecturersCount = await _context.CourseLecturer
                    .Select(cl => cl.LecturerId)
                    .Distinct()
                    .CountAsync();

                // Get total count of assessments used across all courses
                int assessmentsCount = await _context.CourseAssessment
                    .CountAsync();

                // Get courses by year for additional stats
                var coursesByYear = courses
                    .GroupBy(c => c.YearTaken)
                    .Select(g => new { Year = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Year)
                    .ToList();

                // Get courses by programme
                var coursesByProgramme = courses
                    .GroupBy(c => c.Programme.Name)
                    .Select(g => new { Programme = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Programme)
                    .ToList();

                // Prepare chart data - this will be used for the donut chart
                var chartData = new
                {
                    // Course type distribution
                    courseTypes = new
                    {
                        labels = new[] { "Full Course", "Half Course" },
                        data = new[] { fullCourses, halfCourses },
                        colors = new[] { "#0891b2", "#10b981" }
                    },
                    // Mandatory vs Optional
                    mandatoryDistribution = new
                    {
                        labels = new[] { "Mandatory", "Optional" },
                        data = new[] { mandatoryCourses, optionalCourses },
                        colors = new[] { "#f59e0b", "#ef4444" }
                    },
                    // Years distribution
                    yearDistribution = new
                    {
                        labels = coursesByYear.Select(x => $"Year {x.Year}").ToArray(),
                        data = coursesByYear.Select(x => x.Count).ToArray(),
                        colors = new[] { "#0891b2", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6", "#06b6d4" }
                    }
                };

                // Return comprehensive stats
                return Json(new
                {
                    totalCourses,
                    fullCourses,
                    halfCourses,
                    mandatoryCourses,
                    optionalCourses,
                    lecturers = lecturersCount,
                    assessments = assessmentsCount,
                    chartData,
                    coursesByYear,
                    coursesByProgramme
                });
            }
            catch (Exception ex)
            {
                // Log the error (you should use proper logging here)
                Console.WriteLine($"Error fetching course statistics: {ex.Message}");

                // Return minimal stats to prevent UI errors
                return Json(new
                {
                    totalCourses = 0,
                    fullCourses = 0,
                    halfCourses = 0,
                    mandatoryCourses = 0,
                    optionalCourses = 0,
                    lecturers = 0,
                    assessments = 0,
                    chartData = new
                    {
                        courseTypes = new
                        {
                            labels = new[] { "Full Course", "Half Course" },
                            data = new[] { 0, 0 },
                            colors = new[] { "#0891b2", "#10b981" }
                        }
                    },
                    error = "Failed to load statistics"
                });
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> FeeTypes()
        {
            var feeTypes = await _context.FeeTypes.ToListAsync();
            return View(feeTypes);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetFeeType(int id)
        {
            var feeType = await _context.FeeTypes.FindAsync(id);
            if (feeType == null)
            {
                return NotFound();
            }
            var feeTypeDto = new
            {
                id = feeType.Id,
                name = feeType.Name,
                description = feeType.Description,
                applicableFor = feeType.ApplicableFor,
                isActive = feeType.IsActive
            };
            return Json(feeTypeDto);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetFeeTypeStatistics()
        {
            var feeTypes = await _context.FeeTypes.ToListAsync();

            var statistics = new
            {
                totalFeeTypes = feeTypes.Count,
                activeFeeTypes = feeTypes.Count(f => f.IsActive),
                inactiveFeeTypes = feeTypes.Count(f => !f.IsActive),
                studentFeeTypes = feeTypes.Count(f => f.ApplicableFor == "Student"),
                candidateFeeTypes = feeTypes.Count(f => f.ApplicableFor == "Candidate"),
                chartData = new
                {
                    labels = new[] { "Student Fees", "Candidate Fees", "Inactive Fees" },
                    series = new[]
                    {
                feeTypes.Count(f => f.ApplicableFor == "Student"),
                feeTypes.Count(f => f.ApplicableFor == "Candidate"),
                feeTypes.Count(f => !f.IsActive)
            }
                }
            };

            return Json(statistics);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateFeeType(FeeType feeType)
        {
            try
            {
                // Validate required fields manually
                if (string.IsNullOrWhiteSpace(feeType.Name))
                {
                    TempData["ErrorMessage"] = "Fee type name is required.";
                    return RedirectToAction(nameof(FeeTypes));
                }

                if (string.IsNullOrWhiteSpace(feeType.ApplicableFor))
                {
                    TempData["ErrorMessage"] = "Applicable For field is required.";
                    return RedirectToAction(nameof(FeeTypes));
                }

                // Check for duplicate names
                var duplicateName = await _context.FeeTypes
                    .Where(f => f.Name.ToLower() == feeType.Name.ToLower())
                    .FirstOrDefaultAsync();

                if (duplicateName != null)
                {
                    TempData["ErrorMessage"] = "A fee type with this name already exists.";
                    return RedirectToAction(nameof(FeeTypes));
                }

                // Set audit fields
                feeType.CreatedBy = User.Identity.Name;
                feeType.CreatedAt = DateTime.Now;

                // Add to context and save
                _context.FeeTypes.Add(feeType);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Fee type created successfully.";
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (DbUpdateException ex)
            {
                // Handle database update errors (constraints, etc.)
                TempData["ErrorMessage"] = "A database error occurred while creating the fee type. Please try again.";

                // Log the exception
                Console.WriteLine($"Database error creating fee type: {ex.Message}");
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (InvalidOperationException ex)
            {
                // Handle invalid operations
                TempData["ErrorMessage"] = "An invalid operation occurred. Please check your data and try again.";

                // Log the exception
                Console.WriteLine($"Invalid operation creating fee type: {ex.Message}");
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (ArgumentException ex)
            {
                // Handle argument-related errors
                TempData["ErrorMessage"] = "Invalid data provided. Please check your input and try again.";

                // Log the exception
                Console.WriteLine($"Argument error creating fee type: {ex.Message}");
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (Exception ex)
            {
                // Handle any other unexpected errors
                TempData["ErrorMessage"] = "An unexpected error occurred while creating the fee type. Please try again.";

                // Log the exception with full details
                Console.WriteLine($"Unexpected error creating fee type: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return RedirectToAction(nameof(FeeTypes));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateFeeType(FeeType feeType)
        {
            try
            {
                // Validate required fields manually
                if (string.IsNullOrWhiteSpace(feeType.Name))
                {
                    TempData["Error"] = "Fee type name is required.";
                    return RedirectToAction(nameof(FeeTypes));
                }

                if (string.IsNullOrWhiteSpace(feeType.ApplicableFor))
                {
                    TempData["Error"] = "Applicable For field is required.";
                    return RedirectToAction(nameof(FeeTypes));
                }

                // Check if fee type exists
                var existingFeeType = await _context.FeeTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == feeType.Id);

                if (existingFeeType == null)
                {
                    TempData["Error"] = "Fee type not found.";
                    return RedirectToAction(nameof(FeeTypes));
                }

                // Check for duplicate names (excluding current record)
                var duplicateName = await _context.FeeTypes
                    .Where(f => f.Id != feeType.Id && f.Name.ToLower() == feeType.Name.ToLower())
                    .FirstOrDefaultAsync();

                if (duplicateName != null)
                {
                    TempData["Error"] = "A fee type with this name already exists.";
                    return RedirectToAction(nameof(FeeTypes));
                }

                // Preserve creation audit fields
                feeType.CreatedBy = existingFeeType.CreatedBy;
                feeType.CreatedAt = existingFeeType.CreatedAt;

                // Update modification audit fields
                feeType.UpdatedAt = DateTime.Now;
                feeType.UpdatedBy = User.Identity.Name;

                // Update the entity
                _context.Entry(feeType).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Fee type updated successfully.";
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Handle concurrency conflicts
                if (!FeeTypeExists(feeType.Id))
                {
                    TempData["Error"] = "Fee type not found. It may have been deleted by another user.";
                }
                else
                {
                    TempData["Error"] = "The fee type was modified by another user. Please reload and try again.";
                }

                // Log the exception if you have logging configured
                Console.WriteLine($"Concurrency error updating fee type {feeType.Id}: {ex.Message}");
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (DbUpdateException ex)
            {
                // Handle database update errors
                TempData["Error"] = "A database error occurred while updating the fee type. Please try again.";

                // Log the exception
                Console.WriteLine($"Database error updating fee type {feeType.Id}: {ex.Message}");
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (InvalidOperationException ex)
            {
                // Handle invalid operations
                TempData["Error"] = "An invalid operation occurred. Please check your data and try again.";

                // Log the exception
                Console.WriteLine($"Invalid operation updating fee type {feeType.Id}: {ex.Message}");
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (ArgumentException ex)
            {
                // Handle argument-related errors
                TempData["Error"] = "Invalid data provided. Please check your input and try again.";

                // Log the exception
                Console.WriteLine($"Argument error updating fee type {feeType.Id}: {ex.Message}");
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (Exception ex)
            {
                // Handle any other unexpected errors
                TempData["Error"] = "An unexpected error occurred while updating the fee type. Please try again.";

                // Log the exception with full details
                Console.WriteLine($"Unexpected error updating fee type {feeType.Id}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return RedirectToAction(nameof(FeeTypes));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFeeType(int id)
        {
            try
            {
                var feeType = await _context.FeeTypes.FindAsync(id);
                if (feeType == null)
                {
                    TempData["Error"] = "Fee type not found.";
                    return RedirectToAction(nameof(FeeTypes));
                }

                // Check if fee type is being used by any fees before deletion
                // Uncomment and modify this if you have a Fees table that references FeeTypes
                // var isInUse = await _context.Fees.AnyAsync(f => f.FeeTypeId == id);
                // if (isInUse)
                // {
                //     TempData["ErrorMessage"] = "Cannot delete fee type as it is being used by existing fees.";
                //     return RedirectToAction(nameof(FeeTypes));
                // }

                _context.FeeTypes.Remove(feeType);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Fee type deleted successfully.";
                return RedirectToAction(nameof(FeeTypes));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting fee type. Please try again.";
                // Log the exception if you have logging configured
                return RedirectToAction(nameof(FeeTypes));
            }
        }

        private bool FeeTypeExists(int id)
        {
            return _context.FeeTypes.Any(e => e.Id == id);
        }

// GET: Admin/FeeConfigurations
[Authorize(Roles = "Admin")]
public async Task<IActionResult> FeeConfigurations()
{
    try
    {
        await PrepareViewBagData();
        return View(new List<FeeConfiguration>());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading fee configurations: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
        TempData["Error"] = "An error occurred while loading fee configurations.";
        return View(new List<FeeConfiguration>());
    }
}

// Get fee configurations data via AJAX
[HttpPost]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> GetFeeConfigurationsData([FromBody] FeeConfigFilterRequest request)
{
    try
    {
        var query = _context.FeeConfigurations.AsQueryable();

        // Apply filters
        if (request.FeeTypeId.HasValue)
            query = query.Where(fc => fc.FeeTypeId == request.FeeTypeId.Value);

        if (request.AcademicYearId.HasValue)
            query = query.Where(fc => fc.AcademicYearId == request.AcademicYearId.Value);

        if (request.Semester.HasValue)
            query = query.Where(fc => fc.Semester == request.Semester.Value);

        if (request.SchoolId.HasValue)
            query = query.Where(fc => fc.SchoolId == request.SchoolId.Value);

        if (request.ProgrammeId.HasValue)
            query = query.Where(fc => fc.ProgrammeId == request.ProgrammeId.Value);

        if (request.ModeOfStudyId.HasValue)
            query = query.Where(fc => fc.ModeOfStudyId == request.ModeOfStudyId.Value);

        if (request.YearOfStudy.HasValue)
            query = query.Where(fc => fc.YearOfStudy == request.YearOfStudy.Value);

        if (request.ProgramLevelId.HasValue)
            query = query.Where(fc => fc.ProgramLevelId == request.ProgramLevelId.Value);

        if (!string.IsNullOrEmpty(request.StudentType))
        {
            switch (request.StudentType.ToLower())
            {
                case "universal":
                    query = query.Where(fc => fc.AppliesUniversally);
                    break;
                case "accommodated":
                    query = query.Where(fc => fc.AppliesOnlyToAccommodated);
                    break;
                case "foreign":
                    query = query.Where(fc => fc.AppliesOnlyToForeignStudents);
                    break;
                case "local":
                    query = query.Where(fc => fc.AppliesOnlyToLocalStudents);
                    break;
                case "standard":
                    query = query.Where(fc => !fc.AppliesUniversally && !fc.AppliesOnlyToAccommodated && !fc.AppliesOnlyToForeignStudents);
                    break;
            }
        }

        var totalRecords = await query.CountAsync();

        // Get only IDs first with ordering
        var feeConfigIds = await query
            .OrderByDescending(fc => fc.AcademicYearId)
            .ThenBy(fc => fc.FeeTypeId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(fc => fc.Id)
            .ToListAsync();

        // Now fetch full data only for the IDs we need
        var feeConfigurations = await _context.FeeConfigurations
            .Where(fc => feeConfigIds.Contains(fc.Id))
            .Select(fc => new
            {
                fc.Id,
                fc.FeeTypeId,
                FeeTypeName = fc.FeeType.Name,
                fc.AcademicYearId,
                AcademicYear = fc.AcademicYear != null ? fc.AcademicYear.YearValue + "/" + (fc.AcademicYear.SemesterId != null ? fc.AcademicYear.SemesterId.ToString() : "") : "N/A",
                fc.Semester,
                fc.SchoolId,
                SchoolName = fc.School != null ? fc.School.Name : "All Schools",
                fc.ProgrammeId,
                ProgrammeName = fc.Programme != null ? fc.Programme.Name : "All Programmes",
                fc.ModeOfStudyId,
                ModeName = fc.ModeOfStudy != null ? fc.ModeOfStudy.ModeName : "All Modes",
                fc.YearOfStudy,
                fc.ProgramLevelId,
                ProgramLevelName = fc.ProgramLevel != null ? fc.ProgramLevel.Name : "All Levels",
                fc.Amount,
                fc.AppliesUniversally,
                fc.AppliesOnlyToAccommodated,
                fc.AppliesOnlyToForeignStudents,
                fc.AppliesOnlyToLocalStudents,
                fc.CreditNCode,
                fc.DebitNCode,
                fc.RegistrationPaymentRequired
            })
            .OrderBy(fc => fc.SchoolName)
                .ThenBy(fc => fc.ProgrammeName)
            .ToListAsync();

        // Add row numbers
        var orderedConfigs = feeConfigurations
            .OrderByDescending(fc => fc.AcademicYearId)
            .ThenBy(fc => fc.FeeTypeName)
            .Select((fc, index) => new
            {
                fc.Id,
                RowNumber = (request.Page - 1) * request.PageSize + index + 1,
                fc.FeeTypeId,
                fc.FeeTypeName,
                fc.AcademicYearId,
                fc.AcademicYear,
                fc.Semester,
                fc.SchoolId,
                fc.SchoolName,
                fc.ProgrammeId,
                fc.ProgrammeName,
                fc.ModeOfStudyId,
                fc.ModeName,
                fc.YearOfStudy,
                fc.ProgramLevelId,
                fc.ProgramLevelName,
                fc.Amount,
                fc.AppliesUniversally,
                fc.AppliesOnlyToAccommodated,
                fc.AppliesOnlyToForeignStudents,
                fc.AppliesOnlyToLocalStudents,
                fc.CreditNCode,
                fc.DebitNCode,
                fc.RegistrationPaymentRequired
            })
            .ToList();

        var totalPages = (int)Math.Ceiling(totalRecords / (double)request.PageSize);

        return Json(new
        {
            configurations = orderedConfigs,
            totalRecords = totalRecords,
            totalPages = totalPages,
            currentPage = request.Page
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading fee configurations data: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Json(new { error = "Failed to load fee configurations", details = ex.Message });
    }
}

// GET: Admin/GetProgrammesBySchool
[HttpGet]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> GetProgrammesBySchool(int schoolId)
{
    try
    {
        if (schoolId == 0)
        {
            var allProgrammes = await _context.Programmes
                .OrderBy(p => p.Name)
                .Select(p => new { value = p.Id, text = p.Name })
                .ToListAsync();
            return Json(allProgrammes);
        }

        // Join Programmes with Departments to filter by SchoolId
        var programmes = await (from p in _context.Programmes
                               join d in _context.Departments on p.DepartmentId equals d.Id
                               where d.SchoolId == schoolId
                               orderby p.Name
                               select new { value = p.Id, text = p.Name })
                               .ToListAsync();

        return Json(programmes);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error getting programmes by school: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Json(new { error = "Unable to load programmes", details = ex.Message });
    }
}

// GET: Admin/GetFeeConfigurationStatistics
[HttpGet]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> GetFeeConfigurationStatistics()
{
    try
    {
        var statistics = await _context.FeeConfigurations
            .GroupBy(f => 1)
            .Select(g => new
            {
                totalConfigurations = g.Count(),
                universalConfigurations = g.Count(f => f.AppliesUniversally),
                accommodationConfigurations = g.Count(f => f.AppliesOnlyToAccommodated),
                foreignStudentConfigurations = g.Count(f => f.AppliesOnlyToForeignStudents),
                standardConfigurations = g.Count(f => !f.AppliesUniversally && !f.AppliesOnlyToAccommodated && !f.AppliesOnlyToForeignStudents),
                averageAmount = g.Average(f => f.Amount),
                totalAmount = g.Sum(f => f.Amount)
            })
            .FirstOrDefaultAsync();

        if (statistics == null)
        {
            return Json(new
            {
                totalConfigurations = 0,
                universalConfigurations = 0,
                accommodationConfigurations = 0,
                foreignStudentConfigurations = 0,
                standardConfigurations = 0,
                averageAmount = 0,
                totalAmount = 0,
                chartData = new
                {
                    labels = new[] { "Universal", "Accommodation", "Foreign", "Standard" },
                    series = new[] { 0, 0, 0, 0 }
                }
            });
        }

        return Json(new
        {
            statistics.totalConfigurations,
            statistics.universalConfigurations,
            statistics.accommodationConfigurations,
            statistics.foreignStudentConfigurations,
            statistics.standardConfigurations,
            statistics.averageAmount,
            statistics.totalAmount,
            chartData = new
            {
                labels = new[] { "Universal", "Accommodation", "Foreign", "Standard" },
                series = new[]
                {
                    statistics.universalConfigurations,
                    statistics.accommodationConfigurations,
                    statistics.foreignStudentConfigurations,
                    statistics.standardConfigurations
                }
            }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error getting fee configuration statistics: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Json(new { error = "Unable to load statistics" });
    }
}

// POST: Admin/CreateFeeConfiguration
[HttpPost]
[Authorize(Roles = "Admin")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateFeeConfiguration(FeeConfiguration feeConfiguration)
{
    try
    {
        Console.WriteLine("=== CreateFeeConfiguration Started ===");
        Console.WriteLine($"Received FeeConfiguration - AcademicYearId: {feeConfiguration.AcademicYearId}, FeeTypeId: {feeConfiguration.FeeTypeId}");
        Console.WriteLine($"SchoolId: {feeConfiguration.SchoolId}, ProgrammeId: {feeConfiguration.ProgrammeId}");
        Console.WriteLine($"ModeOfStudyId: {feeConfiguration.ModeOfStudyId}, YearOfStudy: {feeConfiguration.YearOfStudy}");
        Console.WriteLine($"ProgramLevelId: {feeConfiguration.ProgramLevelId}, Semester: {feeConfiguration.Semester}");
        Console.WriteLine($"Amount: {feeConfiguration.Amount}, AppliesUniversally: {feeConfiguration.AppliesUniversally}");
        Console.WriteLine($"AppliesOnlyToAccommodated: {feeConfiguration.AppliesOnlyToAccommodated}");
        Console.WriteLine($"AppliesOnlyToForeignStudents: {feeConfiguration.AppliesOnlyToForeignStudents}");
        Console.WriteLine($"AppliesOnlyToLocalStudents: {feeConfiguration.AppliesOnlyToLocalStudents}");
        Console.WriteLine($"CreditNCode: {feeConfiguration.CreditNCode}, DebitNCode: {feeConfiguration.DebitNCode}");
        Console.WriteLine($"RegistrationPaymentRequired: {feeConfiguration.RegistrationPaymentRequired}");

        // Validate required fields
        Console.WriteLine("Validating required fields...");
        if (!feeConfiguration.SchoolId.HasValue || feeConfiguration.SchoolId.Value == 0)
        {
            Console.WriteLine("Validation failed: SchoolId is required");
            TempData["Error"] = "School is required.";
            await PrepareViewBagData();
            return RedirectToAction(nameof(FeeConfigurations));
        }

        if (!feeConfiguration.ProgrammeId.HasValue || feeConfiguration.ProgrammeId.Value == 0)
        {
            Console.WriteLine("Validation failed: ProgrammeId is required");
            TempData["Error"] = "Programme is required.";
            await PrepareViewBagData();
            return RedirectToAction(nameof(FeeConfigurations));
        }

        if (!feeConfiguration.ModeOfStudyId.HasValue || feeConfiguration.ModeOfStudyId.Value == 0)
        {
            Console.WriteLine("Validation failed: ModeOfStudyId is required");
            TempData["Error"] = "Mode of Study is required.";
            await PrepareViewBagData();
            return RedirectToAction(nameof(FeeConfigurations));
        }

        if (!feeConfiguration.YearOfStudy.HasValue || feeConfiguration.YearOfStudy.Value == 0)
        {
            Console.WriteLine("Validation failed: YearOfStudy is required");
            TempData["Error"] = "Year of Study is required.";
            await PrepareViewBagData();
            return RedirectToAction(nameof(FeeConfigurations));
        }

        if (!feeConfiguration.ProgramLevelId.HasValue || feeConfiguration.ProgramLevelId.Value == 0)
        {
            Console.WriteLine("Validation failed: ProgramLevelId is required");
            TempData["Error"] = "Program Level is required.";
            await PrepareViewBagData();
            return RedirectToAction(nameof(FeeConfigurations));
        }

        Console.WriteLine("Required fields validation passed");

        Console.WriteLine("Running custom validation...");
        var validationResult = ValidateFeeConfiguration(feeConfiguration);
        if (!validationResult.IsValid)
        {
            Console.WriteLine($"Custom validation failed: {validationResult.ErrorMessage}");
            TempData["Error"] = validationResult.ErrorMessage;
            await PrepareViewBagData();
            return RedirectToAction(nameof(FeeConfigurations));
        }
        Console.WriteLine("Custom validation passed");

        Console.WriteLine("Checking for existing configuration...");
        var existingConfig = await _context.FeeConfigurations
            .Where(fc => fc.FeeTypeId == feeConfiguration.FeeTypeId &&
                        fc.AcademicYearId == feeConfiguration.AcademicYearId &&
                        fc.SchoolId == feeConfiguration.SchoolId &&
                        fc.ProgrammeId == feeConfiguration.ProgrammeId &&
                        fc.ModeOfStudyId == feeConfiguration.ModeOfStudyId &&
                        fc.YearOfStudy == feeConfiguration.YearOfStudy &&
                        fc.ProgramLevelId == feeConfiguration.ProgramLevelId &&
                        fc.Semester == feeConfiguration.Semester &&
                        fc.AppliesUniversally == feeConfiguration.AppliesUniversally &&
                        fc.AppliesOnlyToAccommodated == feeConfiguration.AppliesOnlyToAccommodated &&
                        fc.AppliesOnlyToForeignStudents == feeConfiguration.AppliesOnlyToForeignStudents &&
                        fc.AppliesOnlyToLocalStudents == feeConfiguration.AppliesOnlyToLocalStudents &&
                        fc.RegistrationPaymentRequired == feeConfiguration.RegistrationPaymentRequired)
            .Select(fc => fc.Id)
            .FirstOrDefaultAsync();

        if (existingConfig != 0)
        {
            Console.WriteLine($"Duplicate configuration found with ID: {existingConfig}");
            TempData["Error"] = "A fee configuration with these parameters already exists.";
            await PrepareViewBagData();
            return RedirectToAction(nameof(FeeConfigurations));
        }
        Console.WriteLine("No duplicate configuration found");

        Console.WriteLine("Processing N-Codes...");
        feeConfiguration.CreditNCode = feeConfiguration.CreditNCode?.Trim().ToUpper();
        feeConfiguration.DebitNCode = feeConfiguration.DebitNCode?.Trim().ToUpper();
        Console.WriteLine($"Processed CreditNCode: {feeConfiguration.CreditNCode}");
        Console.WriteLine($"Processed DebitNCode: {feeConfiguration.DebitNCode}");

        Console.WriteLine("Setting audit fields...");
        feeConfiguration.CreatedAt = DateTime.Now;
        feeConfiguration.CreatedBy = User.Identity?.Name ?? "System";
        feeConfiguration.UpdatedAt = null;
        feeConfiguration.UpdatedBy = null;
        Console.WriteLine($"CreatedAt: {feeConfiguration.CreatedAt}");
        Console.WriteLine($"CreatedBy: {feeConfiguration.CreatedBy}");

        Console.WriteLine("Adding fee configuration to context...");
        _context.FeeConfigurations.Add(feeConfiguration);
        
        Console.WriteLine("Saving changes to database...");
        var saveResult = await _context.SaveChangesAsync();
        Console.WriteLine($"SaveChanges result: {saveResult} record(s) affected");
        Console.WriteLine($"Generated ID: {feeConfiguration.Id}");

        Console.WriteLine("Fee configuration created successfully");
        TempData["Success"] = "Fee configuration created successfully.";
        return RedirectToAction(nameof(FeeConfigurations));
    }
    catch (Exception ex)
    {
        Console.WriteLine("=== ERROR in CreateFeeConfiguration ===");
        Console.WriteLine($"Error Type: {ex.GetType().Name}");
        Console.WriteLine($"Error Message: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner Exception Type: {ex.InnerException.GetType().Name}");
            Console.WriteLine($"Inner Exception Message: {ex.InnerException.Message}");
            Console.WriteLine($"Inner Exception Stack Trace: {ex.InnerException.StackTrace}");
        }

        // Log entity state information
        try
        {
            var entries = _context.ChangeTracker.Entries()
                .Where(e => e.State != Microsoft.EntityFrameworkCore.EntityState.Unchanged)
                .ToList();
            
            Console.WriteLine($"ChangeTracker has {entries.Count} modified entries");
            foreach (var entry in entries)
            {
                Console.WriteLine($"Entity: {entry.Entity.GetType().Name}, State: {entry.State}");
            }
        }
        catch (Exception trackingEx)
        {
            Console.WriteLine($"Error while checking ChangeTracker: {trackingEx.Message}");
        }

        TempData["Error"] = $"An error occurred while creating the fee configuration: {ex.Message}";
    }

    Console.WriteLine("Preparing ViewBag data and returning to FeeConfigurations");
    await PrepareViewBagData();
    return RedirectToAction(nameof(FeeConfigurations));
}

// POST: Admin/UpdateFeeConfiguration
[HttpPost]
[Authorize(Roles = "Admin")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UpdateFeeConfiguration(FeeConfiguration feeConfiguration)
{
    try
    {
        var validationResult = ValidateFeeConfiguration(feeConfiguration);
        if (!validationResult.IsValid)
        {
            TempData["Error"] = validationResult.ErrorMessage;
            await PrepareViewBagData();
            return RedirectToAction(nameof(FeeConfigurations));
        }

        var existingConfig = await _context.FeeConfigurations.FindAsync(feeConfiguration.Id);
        if (existingConfig == null)
        {
            TempData["Error"] = "Fee configuration not found.";
            return RedirectToAction(nameof(FeeConfigurations));
        }

        var duplicateConfig = await _context.FeeConfigurations
            .Where(fc => fc.Id != feeConfiguration.Id &&
                        fc.FeeTypeId == feeConfiguration.FeeTypeId &&
                        fc.AcademicYearId == feeConfiguration.AcademicYearId &&
                        fc.SchoolId == feeConfiguration.SchoolId &&
                        fc.ProgrammeId == feeConfiguration.ProgrammeId &&
                        fc.ModeOfStudyId == feeConfiguration.ModeOfStudyId &&
                        fc.YearOfStudy == feeConfiguration.YearOfStudy &&
                        fc.ProgramLevelId == feeConfiguration.ProgramLevelId &&
                        fc.Semester == feeConfiguration.Semester &&
                        fc.AppliesUniversally == feeConfiguration.AppliesUniversally &&
                        fc.AppliesOnlyToAccommodated == feeConfiguration.AppliesOnlyToAccommodated &&
                        fc.AppliesOnlyToForeignStudents == feeConfiguration.AppliesOnlyToForeignStudents &&
                        fc.AppliesOnlyToLocalStudents == feeConfiguration.AppliesOnlyToLocalStudents &&
                        fc.RegistrationPaymentRequired == feeConfiguration.RegistrationPaymentRequired
                        )
            .Select(fc => fc.Id)
            .FirstOrDefaultAsync();

        if (duplicateConfig != 0)
        {
            TempData["Error"] = "A fee configuration with these parameters already exists.";
            await PrepareViewBagData();
            return RedirectToAction(nameof(FeeConfigurations));
        }

        feeConfiguration.CreditNCode = feeConfiguration.CreditNCode?.Trim().ToUpper();
        feeConfiguration.DebitNCode = feeConfiguration.DebitNCode?.Trim().ToUpper();
        feeConfiguration.CreatedAt = existingConfig.CreatedAt;
        feeConfiguration.CreatedBy = existingConfig.CreatedBy;
        feeConfiguration.UpdatedAt = DateTime.Now;
        feeConfiguration.UpdatedBy = User.Identity?.Name ?? "System";

        _context.Entry(existingConfig).CurrentValues.SetValues(feeConfiguration);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Fee configuration updated successfully.";
        return RedirectToAction(nameof(FeeConfigurations));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating fee configuration: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
        TempData["Error"] = "An error occurred while updating the fee configuration.";
    }

    await PrepareViewBagData();
    return RedirectToAction(nameof(FeeConfigurations));
}

// POST: Admin/DeleteFeeConfiguration
[HttpPost]
[Authorize(Roles = "Admin")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteFeeConfiguration(int id)
{
    try
    {
        var feeConfiguration = await _context.FeeConfigurations.FindAsync(id);
        if (feeConfiguration == null)
        {
            TempData["Error"] = "Fee configuration not found.";
            return RedirectToAction(nameof(FeeConfigurations));
        }

        _context.FeeConfigurations.Remove(feeConfiguration);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Fee configuration deleted successfully.";
        return RedirectToAction(nameof(FeeConfigurations));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error deleting fee configuration: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        TempData["Error"] = "An error occurred while deleting the fee configuration.";
        return RedirectToAction(nameof(FeeConfigurations));
    }
}

private async Task PrepareViewBagData()
{
    try
    {
        var academicYears = await _context.AcademicYears
            .Where(a => a.IsActive == true || a.IsActive == null)
            .OrderByDescending(a => a.YearValue)
            .Select(a => new
            {
                Id = a.YearId,
                Name = a.YearValue + "/" + (a.SemesterId != null ? a.SemesterId.ToString() : "")
            })
            .ToListAsync();

        ViewBag.FeeTypes = new SelectList(
            await _context.FeeTypes
                .Where(ft => ft.IsActive)
                .OrderBy(ft => ft.Name)
                .Select(ft => new { ft.Id, ft.Name })
                .ToListAsync(),
            "Id", "Name");

        ViewBag.AcademicYears = new SelectList(academicYears, "Id", "Name");

        ViewBag.Programmes = new SelectList(
            await _context.Programmes
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(),
            "Id", "Name");

        ViewBag.Schools = new SelectList(
            await _context.Schools
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync(),
            "Id", "Name");

        ViewBag.ModesOfStudy = new SelectList(
            await _context.ModesOfStudy
                .OrderBy(m => m.ModeName)
                .Select(m => new { m.ModeId, m.ModeName })
                .ToListAsync(),
            "ModeId", "ModeName");

        ViewBag.ProgramLevels = new SelectList(
            await _context.ProgramLevels
                .Where(pl => pl.IsActive)
                .OrderBy(pl => pl.Rank)
                .Select(pl => new { pl.Id, pl.Name })
                .ToListAsync(),
            "Id", "Name");

        ViewBag.Semesters = new SelectList(
            new List<object> {
                new { Value = "", Text = "Yearly (Both Semesters)" },
                new { Value = 1, Text = "Semester 1" },
                new { Value = 2, Text = "Semester 2" }
            },
            "Value", "Text");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error preparing ViewBag data: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        ViewBag.FeeTypes = new SelectList(new List<object>(), "Id", "Name");
        ViewBag.AcademicYears = new SelectList(new List<object>(), "Id", "Name");
        ViewBag.Programmes = new SelectList(new List<object>(), "Id", "Name");
        ViewBag.Schools = new SelectList(new List<object>(), "Id", "Name");
        ViewBag.ModesOfStudy = new SelectList(new List<object>(), "Id", "Name");
        ViewBag.ProgramLevels = new SelectList(new List<object>(), "Id", "Name");
        ViewBag.Semesters = new SelectList(new List<object>(), "Value", "Text");
    }
}

private (bool IsValid, string ErrorMessage) ValidateFeeConfiguration(FeeConfiguration feeConfiguration)
{
    if (feeConfiguration.AppliesUniversally &&
        (feeConfiguration.AppliesOnlyToAccommodated || feeConfiguration.AppliesOnlyToForeignStudents))
    {
        return (false, "A universal fee cannot also be specific to accommodation or foreign students.");
    }

    if (feeConfiguration.Amount <= 0)
    {
        return (false, "Fee amount must be greater than zero.");
    }

    if (string.IsNullOrWhiteSpace(feeConfiguration.CreditNCode))
    {
        return (false, "Credit N-Code is required.");
    }

    if (string.IsNullOrWhiteSpace(feeConfiguration.DebitNCode))
    {
        return (false, "Debit N-Code is required.");
    }

    if (feeConfiguration.CreditNCode.Trim().Equals(feeConfiguration.DebitNCode.Trim(), StringComparison.OrdinalIgnoreCase))
    {
        return (false, "Credit and Debit N-Codes must be different.");
    }

    if (feeConfiguration.CreditNCode.Trim().Length < 3)
    {
        return (false, "Credit N-Code must be at least 3 characters long.");
    }

    if (feeConfiguration.DebitNCode.Trim().Length < 3)
    {
        return (false, "Debit N-Code must be at least 3 characters long.");
    }

    if (!feeConfiguration.AppliesUniversally &&
        !feeConfiguration.SchoolId.HasValue &&
        !feeConfiguration.ProgrammeId.HasValue &&
        !feeConfiguration.ModeOfStudyId.HasValue &&
        !feeConfiguration.YearOfStudy.HasValue &&
        !feeConfiguration.ProgramLevelId.HasValue)
    {
        return (false, "Non-universal fees must specify at least one filtering criteria (School, Programme, Mode, Year, or Level).");
    }

    if (feeConfiguration.YearOfStudy.HasValue &&
        (feeConfiguration.YearOfStudy < 1 || feeConfiguration.YearOfStudy > 7))
    {
        return (false, "Year of study must be between 1 and 7.");
    }

    return (true, string.Empty);
}





        // GET: Admin/ProgramLevels
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ProgramLevels()
        {
            var programLevels = await _context.ProgramLevels
                .OrderBy(pl => pl.Rank)
                .ToListAsync();

            return View(programLevels);
        }

        // POST: Admin/CreateProgramLevel
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProgramLevel(ProgramLevel programLevel)
        {
            try
            {
                if (true)
                {
                    programLevel.IsActive = true;
                    _context.ProgramLevels.Add(programLevel);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Program level created successfully.";
                    return RedirectToAction(nameof(ProgramLevels));
                }

                TempData["Error"] = "Please check your input and try again.";
                return RedirectToAction(nameof(ProgramLevels));
            }
            catch (Exception ex)
            {
                // Log error (use proper logging in production)
                Console.WriteLine($"Error creating program level: {ex}");
                TempData["Error"] = "An error occurred while creating the program level.";
                return RedirectToAction(nameof(ProgramLevels));
            }
        }

        // POST: Admin/UpdateProgramLevel
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgramLevel(ProgramLevel programLevel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Update(programLevel);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Program level updated successfully.";
                    return RedirectToAction(nameof(ProgramLevels));
                }

                TempData["Error"] = "Please check your input and try again.";
                return RedirectToAction(nameof(ProgramLevels));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating program level: {ex}");
                TempData["Error"] = "An error occurred while updating the program level.";
                return RedirectToAction(nameof(ProgramLevels));
            }
        }

        // POST: Admin/DeleteProgramLevel
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProgramLevel(int id)
        {
            try
            {
                var programLevel = await _context.ProgramLevels.FindAsync(id);
                if (programLevel == null)
                {
                    TempData["Error"] = "Program level not found.";
                    return RedirectToAction(nameof(ProgramLevels));
                }

                // Check if the program level is in use
                var hasRelatedRecords = await _context.Programmes
                    .AnyAsync(p => p.ProgrammeLevelId == id);

                if (hasRelatedRecords)
                {
                    TempData["Error"] = "This program level cannot be deleted as it is associated with existing programs.";
                    return RedirectToAction(nameof(ProgramLevels));
                }

                _context.ProgramLevels.Remove(programLevel);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Program level deleted successfully.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting program level: {ex}");
                TempData["Error"] = "An error occurred while deleting the program level.";
            }

            return RedirectToAction(nameof(ProgramLevels));
        }

        // GET: Admin/ProgressionRules
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ProgressionRules()
        {
            var progressionRules = await _context.ProgressionRules
                .Include(pr => pr.School) // Include School navigation property
                .OrderBy(r => r.SchoolId.HasValue ? 1 : 0) // Global rules first (SchoolId = null)
                .ThenBy(r => r.School.Name) // Then by school name
                .ThenBy(r => r.PercentFailedOfCourseLoad)
                .ThenBy(r => r.Action)
                .ToListAsync();

            return View(progressionRules);
        }

        // GET: Admin/CreateProgressionRule
        public async Task<IActionResult> CreateProgressionRule()
        {
            // Get statistics for the sidebar
            ViewBag.TotalRules = _context.ProgressionRules.Count();
            ViewBag.ActiveRules = _context.ProgressionRules.Count(x => x.IsActive);
            ViewBag.InactiveRules = _context.ProgressionRules.Count(x => !x.IsActive);

            // Get schools for dropdown
            ViewBag.Schools = await _context.Schools
                .Select(s => new { s.Id, s.Name })
                .OrderBy(s => s.Name)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProgressionRule(ProgressionRule progressionRule)
        {
            try
            {
                // Validate that if SchoolId is provided, the school exists
                if (progressionRule.SchoolId.HasValue)
                {
                    var schoolExists = await _context.Schools
                        .AnyAsync(s => s.Id == progressionRule.SchoolId.Value);

                    if (!schoolExists)
                    {
                        ModelState.AddModelError("SchoolId", "Selected school does not exist.");
                        return await ReloadCreateView(progressionRule);
                    }
                }

                progressionRule.CreatedAt = DateTime.Now;
                progressionRule.CreatedBy = User.Identity.Name;

                // Add and save the ProgressionRule
                _context.ProgressionRules.Add(progressionRule);
                await _context.SaveChangesAsync();

                var scopeText = progressionRule.SchoolId.HasValue ?
                    $" for {(await _context.Schools.FindAsync(progressionRule.SchoolId.Value))?.Name}" :
                    " (Global rule)";

                TempData["Success"] = $"Progression rule '{progressionRule.Name}'{scopeText} was created successfully.";
                return RedirectToAction("ProgressionRules");
            }
            catch (Exception ex)
            {
                // Log error and provide user feedback
                Console.WriteLine($"Error creating progression rule: {ex}");
                ViewBag.ErrorMessage = "An error occurred while creating the progression rule. Please try again.";
                TempData["Error"] = "An error occurred while creating the progression rule. Please try again.";

                return await ReloadCreateView(progressionRule);
            }
        }

        // Helper method to reload create view with necessary data
        private async Task<IActionResult> ReloadCreateView(ProgressionRule progressionRule)
        {
            ViewBag.TotalRules = _context.ProgressionRules.Count();
            ViewBag.ActiveRules = _context.ProgressionRules.Count(x => x.IsActive);
            ViewBag.InactiveRules = _context.ProgressionRules.Count(x => !x.IsActive);

            ViewBag.Schools = await _context.Schools
                .Select(s => new { s.Id, s.Name })
                .OrderBy(s => s.Name)
                .ToListAsync();

            return View(progressionRule);
        }

        // GET: Admin/UpdateProgressionRule
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetProgressionRule(int id)
        {
            var rule = await _context.ProgressionRules
                .Include(pr => pr.School) // Include school information
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (rule == null)
            {
                return NotFound();
            }

            // Return rule with school information
            var result = new
            {
                rule.Id,
                rule.Name,
                rule.PercentFailedOfCourseLoad,
                rule.Description,
                rule.Action,
                rule.IsActive,
                rule.SchoolId,
                SchoolName = rule.School?.Name,
                rule.Semester,
                rule.Attempt
            };

            return Json(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateProgressionRule(ProgressionRule progressionRule)
        {
            try
            {
                var existingRule = await _context.ProgressionRules.FindAsync(progressionRule.Id);
                if (existingRule == null)
                {
                    TempData["Error"] = "Progression rule not found.";
                    return NotFound();
                }

                // Validate that if SchoolId is provided, the school exists
                if (progressionRule.SchoolId.HasValue)
                {
                    var schoolExists = await _context.Schools
                        .AnyAsync(s => s.Id == progressionRule.SchoolId.Value);

                    if (!schoolExists)
                    {
                        TempData["Error"] = "Selected school does not exist.";
                        return RedirectToAction("ProgressionRules");
                    }
                }

                existingRule.Name = progressionRule.Name;
                existingRule.PercentFailedOfCourseLoad = progressionRule.PercentFailedOfCourseLoad;
                existingRule.Description = progressionRule.Description;
                existingRule.IsActive = progressionRule.IsActive;
                existingRule.Action = progressionRule.Action;
                existingRule.Semester = progressionRule.Semester;
                existingRule.Attempt = progressionRule.Attempt;
                existingRule.SchoolId = progressionRule.SchoolId; // Add SchoolId update
                existingRule.UpdatedBy = User.Identity.Name;
                existingRule.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                var scopeText = progressionRule.SchoolId.HasValue ?
                    $" for {(await _context.Schools.FindAsync(progressionRule.SchoolId.Value))?.Name}" :
                    " (Global rule)";

                TempData["Success"] = $"Progression rule '{progressionRule.Name}'{scopeText} was updated successfully.";
                return RedirectToAction("ProgressionRules");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while updating the progression rule. Please try again.";
                Console.WriteLine($"Error updating progression rule: {ex}");
                return RedirectToAction("ProgressionRules");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProgressionRule(int id)
        {
            try
            {
                var rule = await _context.ProgressionRules
                    .Include(pr => pr.School) // Include school for better messaging
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (rule == null)
                {
                    TempData["Error"] = "Progression rule not found.";
                    return NotFound();
                }

                var scopeText = rule.SchoolId.HasValue ?
                    $" for {rule.School?.Name}" :
                    " (Global rule)";

                _context.ProgressionRules.Remove(rule);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Progression rule '{rule.Name}'{scopeText} was deleted successfully.";
                return RedirectToAction("ProgressionRules");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting progression rule: {ex}");
                TempData["Error"] = "An error occurred while deleting the progression rule. Please try again.";
                return RedirectToAction("ProgressionRules");
            }
        }

        // NEW: Helper method to get schools for AJAX calls (optional)
        [HttpGet]
        public async Task<IActionResult> GetSchools()
        {
            var schools = await _context.Schools
                .Where(s => s.Id > 0) // Assuming active schools have positive IDs
                .Select(s => new { s.Id, s.Name })
                .OrderBy(s => s.Name)
                .ToListAsync();

            return Json(schools);
        }

        // GET: Admin/Assessments
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Assessments()
        {
            var assessments = await _context.Assessments
                .OrderBy(a => a.Name)
                .ToListAsync();

            ViewBag.TotalAssessments = assessments.Count;
            ViewBag.ActiveAssessments = assessments.Count(x => x.IsActive);
            ViewBag.PendingAssessments = assessments.Count(x =>
                x.DueDate.HasValue && x.DueDate > DateTime.Now);

            return View(assessments);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> CreateAssessment(Assessment assessment)
        {
            try
            {
                assessment.CreatedBy = User.Identity.Name;
                assessment.CreatedAt = DateTime.Now;

                _context.Assessments.Add(assessment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Assessment created successfully";
                return RedirectToAction("Assessments");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating assessment: {ex}");
                TempData["Error"] = "An error occurred while creating the assessment";
                return RedirectToAction("Assessments");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> GetAssessment(int id)
        {
            var assessment = await _context.Assessments
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assessment == null)
            {
                return NotFound();
            }

            return Json(assessment);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> UpdateAssessment(Assessment assessment)
        {
            try
            {
                var existingAssessment = await _context.Assessments.FindAsync(assessment.Id);
                if (existingAssessment == null)
                {
                    return NotFound();
                }

                existingAssessment.Name = assessment.Name;
                existingAssessment.Type = assessment.Type;
                existingAssessment.WeightPercentage = assessment.WeightPercentage;
                existingAssessment.PassMark = assessment.PassMark;
                existingAssessment.Description = assessment.Description;
                existingAssessment.IsActive = assessment.IsActive;
                existingAssessment.RequiresSubmission = assessment.RequiresSubmission;
                existingAssessment.DueDate = assessment.DueDate;
                existingAssessment.SubmissionInstructions = assessment.SubmissionInstructions;
                existingAssessment.AllowResit = assessment.AllowResit;
                existingAssessment.MaximumResitMark = assessment.MaximumResitMark;
                existingAssessment.UpdatedBy = User.Identity.Name;
                existingAssessment.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Assessment updated successfully";
                return RedirectToAction("Assessments");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating assessment: {ex}");
                TempData["Error"] = "An error occurred while updating the assessment";
                return RedirectToAction("Assessments");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> DeleteAssessment(int id)
        {
            try
            {
                var assessment = await _context.Assessments.FindAsync(id);
                if (assessment == null)
                {
                    return NotFound();
                }

                _context.Assessments.Remove(assessment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Assessment deleted successfully";
                return RedirectToAction("Assessments");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting assessment: {ex}");
                TempData["Error"] = "An error occurred while deleting the assessment";
                return RedirectToAction("Assessments");
            }
        }

        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> Grades()
        {
            var grades = await _context.Grades.ToListAsync();
            return View(grades);
        }

        [Authorize(Roles = "Admin, Registrar")]
        public ActionResult CreateGrade()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> CreateGrade(Grade model, IFormFile fileUpload, string mode)
        {
            try
            {
                if (mode == "single")
                {
                    if (model != null)
                    {
                        _context.Add(model);
                        await _context.SaveChangesAsync();
                        return RedirectToAction("Grades");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Invalid grade details.");
                        return View(model);
                    }
                }
                else if (mode == "bulk" && fileUpload != null)
                {
                    var grades = new List<Grade>();
                    using (var stream = new MemoryStream())
                    {
                        await fileUpload.CopyToAsync(stream);
                        stream.Position = 0;

                        using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
                        {
                            while (reader.Read())
                            {
                                // Assuming the first row is the header, skip it
                                if (reader.Depth == 0) continue;

                                grades.Add(new Grade
                                {
                                    CreatedAt = DateTime.Now,
                                    CreatedBy = User.Identity.Name,
                                    GradeValue = reader.GetString(0).ToUpper(),
                                    GradePoint = (int)reader.GetDouble(1),
                                    Code = reader.GetString(2),
                                    Description = reader.GetString(3).ToUpper(),
                                });
                            }
                        }
                    }

                    if (grades.Any())
                    {
                        await _context.Grades.AddRangeAsync(grades);
                        await _context.SaveChangesAsync();
                        return RedirectToAction("Grades");
                    }
                    else
                    {
                        ModelState.AddModelError("", "No valid grades found in the uploaded file.");
                        return View(model);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Invalid mode or file upload missing.");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                return View(model);
            }
        }



        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> UpdateGrade(int id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var grade = await _context.Grades.FindAsync(id);

            if (grade == null)
            {
                return NotFound();
            }
            return View(grade);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> UpdateGrade(Grade model)
        {
            var grade = model;
            if (grade != null)
            {
                grade.UpdatedAt = DateTime.Now;
                grade.UpdatedBy = User.Identity.Name;
                _context.Update(grade);
                await _context.SaveChangesAsync();
                return RedirectToAction("Grades");
            }
            else
            {
                // Log each error in ModelState to the console
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Error: {error.ErrorMessage}");
                    if (error.Exception != null)
                    {
                        Console.WriteLine($"Exception: {error.Exception.Message}");
                    }
                }
            }
            return View(grade);
        }

        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> DeleteGrade(int id)
        {
            var grade = await _context.Grades.FindAsync(id);
            if (grade == null)
            {
                return NotFound();
            }
            _context.Grades.Remove(grade);
            await _context.SaveChangesAsync();
            return RedirectToAction("Grades");
        }

        // GET: Admin/Subjects
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> Subjects()
        {
            var subjects = await _context.Subjects.ToListAsync();
            return View(subjects);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> CreateSubject(Subject model, IFormFile fileUpload, string mode)
        {
            try
            {
                if (mode == "single")
                {
                    // Check if subject with same code exists
                    var exists = await _context.Subjects
                        .AnyAsync(s => s.SubjectCode == model.SubjectCode);

                    if (exists)
                    {
                        TempData["Error"] = "A subject with this code already exists.";
                        return RedirectToAction(nameof(Subjects));
                    }

                    model.CreatedAt = DateTime.Now;
                    model.CreatedBy = User.Identity.Name;
                    model.SubjectName = model.SubjectName.ToUpper();

                    _context.Add(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Subject created successfully.";
                    
                }
                else if (mode == "bulk" && fileUpload != null)
                {
                    var subjects = new List<Subject>();
                    var duplicates = new List<string>();
                    var errors = new List<string>();

                    using (var stream = new MemoryStream())
                    {
                        await fileUpload.CopyToAsync(stream);
                        stream.Position = 0;

                        using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
                        {
                            int rowNumber = 0;
                            while (reader.Read())
                            {
                                rowNumber++;
                                if (rowNumber == 1) continue; // Skip header row

                                var subjectName = reader.GetValue(0)?.ToString();
                                var subjectCode = reader.GetValue(1)?.ToString();

                                if (string.IsNullOrWhiteSpace(subjectName) || string.IsNullOrWhiteSpace(subjectCode))
                                {
                                    errors.Add($"Row {rowNumber}: Invalid data");
                                    continue;
                                }

                                // Check for duplicates in database
                                var exists = await _context.Subjects.AnyAsync(s => s.SubjectCode == subjectCode);
                                if (exists)
                                {
                                    duplicates.Add(subjectCode);
                                    continue;
                                }

                                subjects.Add(new Subject
                                {
                                    CreatedAt = DateTime.Now,
                                    CreatedBy = User.Identity.Name,
                                    SubjectName = subjectName.ToUpper(),
                                    SubjectCode = subjectCode,
                                });
                            }
                        }
                    }

                    if (subjects.Any())
                    {
                        await _context.Subjects.AddRangeAsync(subjects);
                        await _context.SaveChangesAsync();
                        TempData["Success"] = $"{subjects.Count} subjects imported successfully.";

                        if (duplicates.Any() || errors.Any())
                        {
                            TempData["Warning"] = $"Skipped {duplicates.Count} duplicate subjects and {errors.Count} invalid rows.";
                        }
                    }
                    else
                    {
                        TempData["Error"] = "No valid subjects found in the uploaded file.";
                    }
                }
                else
                {
                    TempData["Error"] = "Invalid mode or file upload missing.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";
                // Log the exception
                Console.WriteLine($"Error: {ex.Message}");
            }

            return RedirectToAction(nameof(Subjects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> UpdateSubject(Subject model)
        {
            try
            {
                var existingSubject = await _context.Subjects.FindAsync(model.SubjectId);
                if (existingSubject == null)
                {
                    TempData["Error"] = "Subject not found.";
                    return RedirectToAction(nameof(Subjects));
                }

                // Check if another subject with the same code exists
                var duplicateExists = await _context.Subjects
                    .AnyAsync(s => s.SubjectId != model.SubjectId &&
                                    s.SubjectCode == model.SubjectCode);

                if (duplicateExists)
                {
                    TempData["Error"] = "A subject with this code already exists.";
                    return RedirectToAction(nameof(Subjects));
                }

                existingSubject.SubjectName = model.SubjectName.ToUpper();
                existingSubject.SubjectCode = model.SubjectCode;
                existingSubject.UpdatedAt = DateTime.Now;
                existingSubject.UpdatedBy = User.Identity.Name;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Subject updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating subject.";
                Console.WriteLine($"Error: {ex.Message}");
            }

            return RedirectToAction(nameof(Subjects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> DeleteSubject(int SubjectId)
        {
            try
            {
                var subject = await _context.Subjects.FindAsync(SubjectId);
                if (subject == null)
                {
                    TempData["Error"] = "Subject not found.";
                    return RedirectToAction(nameof(Subjects));
                }

                // Add check for related records here if needed
                // Example: if (await _context.StudentSubjects.AnyAsync(ss => ss.SubjectId == SubjectId))

                _context.Subjects.Remove(subject);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Subject deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting subject.";
                Console.WriteLine($"Error: {ex.Message}");
            }

            return RedirectToAction(nameof(Subjects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> CreateModeOfStudy(ModeOfStudy model)
        {
            try
            {
                _context.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Mode of Study created successfully.";
                return RedirectToAction(nameof(ModeOfStudy));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating Mode of Study.";
                // Log the exception
                Console.WriteLine($"Error: {ex.Message}");
            }
           

            // If we got this far, something failed
            TempData["Error"] = "Failed to create Mode of Study. Please check your input.";
            return RedirectToAction(nameof(ModeOfStudy));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> UpdateModeOfStudy(ModeOfStudy model)
        {
            try
            {
                var existingMode = await _context.ModesOfStudy.FindAsync(model.ModeId);
                if (existingMode == null)
                {
                    TempData["Error"] = "Mode of Study not found.";
                    return RedirectToAction(nameof(ModeOfStudy));
                }

                existingMode.ModeName = model.ModeName;
                existingMode.Code = model.Code;

                _context.Update(existingMode);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Mode of Study updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating Mode of Study.";
                // Log the exception
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            return RedirectToAction(nameof(ModeOfStudy));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> DeleteModeOfStudy(int ModeId)
        {
            try
            {
                var modeOfStudy = await _context.ModesOfStudy.FindAsync(ModeId);
                if (modeOfStudy == null)
                {
                    TempData["Error"] = "Mode of Study not found.";
                    return RedirectToAction(nameof(ModeOfStudy));
                }

                _context.ModesOfStudy.Remove(modeOfStudy);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Mode of Study deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting Mode of Study.";
                // Log the exception
                Console.WriteLine($"Error: {ex.Message}");
            }

            return RedirectToAction(nameof(ModeOfStudy));
        }


        // GET: Admin/AcademicYears
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> AcademicYear()
        {
            var academicYears = await _context.AcademicYears
                .Include(a => a.NextAcademicYear)
                .ToListAsync();
            return View(academicYears);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> CreateAcademicYear(AcademicYear model)
        {
            try
            {
                // Validate basic date requirements
                if (model.StartDate >= model.EndDate)
                {
                    return Json(new { success = false, message = "Start Date must be earlier than End Date." });
                }

                // Validate based on academic type
                if (model.AcademicType == AcademicType.Semester)
                {
                    // For semester-based, semester dates are required
                    if (!model.Semester1StartDate.HasValue || !model.Semester1EndDate.HasValue ||
                        !model.Semester2StartDate.HasValue || !model.Semester2EndDate.HasValue)
                    {
                        return Json(new { success = false, message = "All semester dates are required for semester-based academic years." });
                    }

                    // Validate semester date sequence
                    if (model.Semester1StartDate >= model.Semester1EndDate)
                    {
                        return Json(new { success = false, message = "Semester 1 Start Date must be earlier than Semester 1 End Date." });
                    }

                    if (model.Semester2StartDate >= model.Semester2EndDate)
                    {
                        return Json(new { success = false, message = "Semester 2 Start Date must be earlier than Semester 2 End Date." });
                    }

                    if (model.Semester1EndDate >= model.Semester2StartDate)
                    {
                        return Json(new { success = false, message = "Semester 1 must end before Semester 2 starts." });
                    }

                    // Validate semester dates are within academic year bounds
                    if (model.Semester1StartDate < model.StartDate || model.Semester2EndDate > model.EndDate)
                    {
                        return Json(new { success = false, message = "Semester dates must be within the academic year period." });
                    }
                }

                // Validate registration dates if provided
                if (model.RegistrationStartDate.HasValue && model.RegistrationEndDate.HasValue)
                {
                    if (model.RegistrationStartDate >= model.RegistrationEndDate)
                    {
                        return Json(new { success = false, message = "Registration Start Date must be earlier than Registration End Date." });
                    }
                }

                // Validate final exam dates if provided
                if (model.FinalExamStartDate.HasValue && model.FinalExamEndDate.HasValue)
                {
                    if (model.FinalExamStartDate >= model.FinalExamEndDate)
                    {
                        return Json(new { success = false, message = "Final Exam Start Date must be earlier than Final Exam End Date." });
                    }
                }

                // Validate grade submission dates if provided
                if (model.GradeSubmissionStartDate.HasValue && model.GradeSubmissionEndDate.HasValue)
                {
                    if (model.GradeSubmissionStartDate >= model.GradeSubmissionEndDate)
                    {
                        return Json(new { success = false, message = "Grade Submission Start Date must be earlier than Grade Submission End Date." });
                    }
                }

                // Validate NextAcademicYear if provided
                if (model.NextAcademicYearId.HasValue)
                {
                    var nextYear = await _context.AcademicYears
                        .FirstOrDefaultAsync(y => y.YearId == model.NextAcademicYearId.Value);

                    if (nextYear == null)
                    {
                        return Json(new { success = false, message = "Selected next academic year does not exist." });
                    }

                    // Validate that next year starts after or at the end of current year
                    if (nextYear.StartDate < model.EndDate)
                    {
                        return Json(new { success = false, message = "Next academic year must start on or after the current year's end date." });
                    }

                    // Check for circular reference (pointing to itself)
                    if (model.NextAcademicYearId.Value == model.YearId)
                    {
                        return Json(new { success = false, message = "Academic year cannot be linked to itself." });
                    }
                }

                // Check if a year with the same YearValue and AcademicType already exists
                var yearExists = await _context.AcademicYears
                    .AnyAsync(y => y.YearValue == model.YearValue && y.AcademicType == model.AcademicType);
                if (yearExists)
                {
                    return Json(new { success = false, message = "An academic year with this year value and type already exists." });
                }

                // Remove deprecated fields
                model.SemesterId = null;
                model.ModeId = null;

                // Add the new academic year
                _context.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Academic year created successfully.";

                return Json(new { success = true, message = "Academic Year created successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error creating Academic Year." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> UpdateAcademicYear(AcademicYear model)
        {
            try
            {
                var existingYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(y => y.YearId == model.YearId);
                if (existingYear == null)
                {
                    return Json(new { success = false, message = "Academic Year not found." });
                }

                // Validate basic date requirements
                if (model.StartDate >= model.EndDate)
                {
                    return Json(new { success = false, message = "Start Date must be earlier than End Date." });
                }

                // Validate based on academic type
                if (model.AcademicType == AcademicType.Semester)
                {
                    // For semester-based, semester dates are required
                    if (!model.Semester1StartDate.HasValue || !model.Semester1EndDate.HasValue ||
                        !model.Semester2StartDate.HasValue || !model.Semester2EndDate.HasValue)
                    {
                        return Json(new { success = false, message = "All semester dates are required for semester-based academic years." });
                    }

                    // Validate semester date sequence
                    if (model.Semester1StartDate >= model.Semester1EndDate)
                    {
                        return Json(new { success = false, message = "Semester 1 Start Date must be earlier than Semester 1 End Date." });
                    }

                    if (model.Semester2StartDate >= model.Semester2EndDate)
                    {
                        return Json(new { success = false, message = "Semester 2 Start Date must be earlier than Semester 2 End Date." });
                    }

                    if (model.Semester1EndDate >= model.Semester2StartDate)
                    {
                        return Json(new { success = false, message = "Semester 1 must end before Semester 2 starts." });
                    }

                    // Validate semester dates are within academic year bounds
                    if (model.Semester1StartDate < model.StartDate || model.Semester2EndDate > model.EndDate)
                    {
                        return Json(new { success = false, message = "Semester dates must be within the academic year period." });
                    }
                }

                // Validate registration dates if provided
                if (model.RegistrationStartDate.HasValue && model.RegistrationEndDate.HasValue)
                {
                    if (model.RegistrationStartDate >= model.RegistrationEndDate)
                    {
                        return Json(new { success = false, message = "Registration Start Date must be earlier than Registration End Date." });
                    }
                }

                // Validate final exam dates if provided
                if (model.FinalExamStartDate.HasValue && model.FinalExamEndDate.HasValue)
                {
                    if (model.FinalExamStartDate >= model.FinalExamEndDate)
                    {
                        return Json(new { success = false, message = "Final Exam Start Date must be earlier than Final Exam End Date." });
                    }
                }

                // Validate grade submission dates if provided
                if (model.GradeSubmissionStartDate.HasValue && model.GradeSubmissionEndDate.HasValue)
                {
                    if (model.GradeSubmissionStartDate >= model.GradeSubmissionEndDate)
                    {
                        return Json(new { success = false, message = "Grade Submission Start Date must be earlier than Grade Submission End Date." });
                    }
                }

                // Validate NextAcademicYear if provided
                if (model.NextAcademicYearId.HasValue)
                {
                    var nextYear = await _context.AcademicYears
                        .FirstOrDefaultAsync(y => y.YearId == model.NextAcademicYearId.Value);

                    if (nextYear == null)
                    {
                        return Json(new { success = false, message = "Selected next academic year does not exist." });
                    }

                    // Validate that next year starts after or at the end of current year
                    if (nextYear.StartDate < model.EndDate)
                    {
                        return Json(new { success = false, message = "Next academic year must start on or after the current year's end date." });
                    }

                    // Check for circular reference (pointing to itself)
                    if (model.NextAcademicYearId.Value == model.YearId)
                    {
                        return Json(new { success = false, message = "Academic year cannot be linked to itself." });
                    }

                    // Check for circular chain (A→B→C→A)
                    var hasCircularReference = await HasCircularReferenceAsync(model.YearId, model.NextAcademicYearId.Value);
                    if (hasCircularReference)
                    {
                        return Json(new { success = false, message = "This would create a circular reference in the academic year progression chain." });
                    }
                }

                // Check if another year with the same values exists
                var duplicateExists = await _context.AcademicYears
                    .AnyAsync(y => y.YearId != model.YearId &&
                                   y.YearValue == model.YearValue &&
                                   y.AcademicType == model.AcademicType);
                if (duplicateExists)
                {
                    return Json(new { success = false, message = "An academic year with this year value and type already exists." });
                }

                // If setting this year as active, deactivate others of the same type
                if (model.IsActive && !existingYear.IsActive)
                {
                    var activeYears = await _context.AcademicYears
                        .Where(y => y.IsActive && y.YearId != model.YearId && y.AcademicType == model.AcademicType)
                        .ToListAsync();
                    foreach (var year in activeYears)
                    {
                        year.IsActive = false;
                    }
                }

                // Update properties
                existingYear.YearValue = model.YearValue;
                existingYear.AcademicType = model.AcademicType;
                existingYear.StartDate = model.StartDate;
                existingYear.EndDate = model.EndDate;
                existingYear.MinRegistrationPaymentPercentage = model.MinRegistrationPaymentPercentage;
                existingYear.MinExamPaymentPercentage = model.MinExamPaymentPercentage;
                existingYear.IsActive = model.IsActive;

                // Update semester dates
                existingYear.Semester1StartDate = model.Semester1StartDate;
                existingYear.Semester1EndDate = model.Semester1EndDate;
                existingYear.Semester2StartDate = model.Semester2StartDate;
                existingYear.Semester2EndDate = model.Semester2EndDate;

                // Update optional period dates
                existingYear.RegistrationStartDate = model.RegistrationStartDate;
                existingYear.RegistrationEndDate = model.RegistrationEndDate;
                existingYear.FinalExamStartDate = model.FinalExamStartDate;
                existingYear.FinalExamEndDate = model.FinalExamEndDate;
                existingYear.GradeSubmissionStartDate = model.GradeSubmissionStartDate;
                existingYear.GradeSubmissionEndDate = model.GradeSubmissionEndDate;

                // Update next academic year
                existingYear.NextAcademicYearId = model.NextAcademicYearId;

                // Clear deprecated fields
                existingYear.SemesterId = null;
                existingYear.ModeId = null;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Academic year updated successfully.";
                return Json(new { success = true, message = "Academic Year updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating Academic Year." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar")]
        public async Task<IActionResult> DeleteAcademicYear(int YearId)
        {
            try
            {
                var academicYear = await _context.AcademicYears.FindAsync(YearId);
                if (academicYear == null)
                {
                    return Json(new { success = false, message = "Academic Year not found." });
                }

                if (academicYear.IsActive)
                {
                    return Json(new { success = false, message = "Cannot delete the active academic year." });
                }

                // Check if this year is referenced as NextAcademicYear by any other year
                var isReferencedByOthers = await _context.AcademicYears
                    .AnyAsync(y => y.NextAcademicYearId == YearId);

                if (isReferencedByOthers)
                {
                    return Json(new { success = false, message = "Cannot delete this academic year because it is set as the next academic year for other year(s). Please update those references first." });
                }

                _context.AcademicYears.Remove(academicYear);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Academic year deleted successfully.";
                return Json(new { success = true, message = "Academic Year deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting Academic Year." });
            }
        }

        // Helper method to check for circular references in academic year progression
        private async Task<bool> HasCircularReferenceAsync(int currentYearId, int nextYearId)
        {
            var visitedYears = new HashSet<int> { currentYearId };
            var checkYearId = nextYearId;

            while (checkYearId != 0)
            {
                if (visitedYears.Contains(checkYearId))
                {
                    return true; // Circular reference detected
                }

                visitedYears.Add(checkYearId);

                var year = await _context.AcademicYears
                    .AsNoTracking()
                    .FirstOrDefaultAsync(y => y.YearId == checkYearId);

                if (year?.NextAcademicYearId == null)
                {
                    break; // End of chain
                }

                checkYearId = year.NextAcademicYearId.Value;
            }

            return false;
        }

        // GET: Admin/GradeConfigurations
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GradeConfigurations()
        {
            var gradeConfigs = await _context.GradeConfigurations
                .Include(gc => gc.School)
                .OrderByDescending(g => g.GPAValue)
                .ToListAsync();

            // Populate Schools dropdown
            var schools = await _context.Schools
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                })
                .ToListAsync();
            ViewBag.Schools = schools;

            // Also store the schools list for displaying names in the table
            ViewBag.SchoolsList = await _context.Schools.ToListAsync();

            // Populate Academic Years dropdown
            var academicYears = await _context.AcademicYears
                .OrderByDescending(a => a.YearValue)
                .Select(a => new SelectListItem
                {
                    Value = a.YearId.ToString(),
                    Text = a.YearValue
                })
                .ToListAsync();
            ViewBag.AcademicYears = academicYears;

            // Also store the academic years list for displaying values in the table
            ViewBag.AcademicYearsList = await _context.AcademicYears.ToListAsync();

            return View(gradeConfigs);
        }

        // GET: Admin/GetGradeConfig/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetGradeConfig(int id)
        {
            var gradeConfig = await _context.GradeConfigurations.FindAsync(id);
            if (gradeConfig == null)
            {
                return NotFound();
            }

            // Return all fields including SchoolId and AcademicYearId
            var result = new
            {
                id = gradeConfig.Id,
                gradeLetter = gradeConfig.GradeLetter,
                minScore = gradeConfig.MinScore,
                maxScore = gradeConfig.MaxScore,
                gpaValue = gradeConfig.GPAValue,
                description = gradeConfig.Description,
                isPassingGrade = gradeConfig.IsPassingGrade,
                isActive = gradeConfig.IsActive,
                schoolId = gradeConfig.SchoolId,
                academicYearId = gradeConfig.AcademicYearId
            };

            return Json(result);
        }

        // POST: Admin/CreateGradeConfig
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGradeConfig(GradeConfiguration gradeConfig)
        {
            try
            {
                // Validate Academic Year is required
                if (!gradeConfig.AcademicYearId.HasValue || gradeConfig.AcademicYearId.Value == 0)
                {
                    TempData["Error"] = "Academic Year is required.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Validate score range
                if (gradeConfig.MinScore >= gradeConfig.MaxScore)
                {
                    TempData["Error"] = "Minimum score must be less than maximum score.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Check for overlapping ranges within the same school and academic year
                var overlapping = await _context.GradeConfigurations
                    .Where(g => g.SchoolId == gradeConfig.SchoolId &&
                                g.AcademicYearId == gradeConfig.AcademicYearId)
                    .AnyAsync(g => (gradeConfig.MinScore >= g.MinScore && gradeConfig.MinScore <= g.MaxScore) ||
                                  (gradeConfig.MaxScore >= g.MinScore && gradeConfig.MaxScore <= g.MaxScore) ||
                                  (g.MinScore >= gradeConfig.MinScore && g.MinScore <= gradeConfig.MaxScore));

                if (overlapping)
                {
                    TempData["Error"] = "The score range overlaps with an existing grade configuration for the same school and academic year.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Check for duplicate grade letter within the same school and academic year
                var existingGrade = await _context.GradeConfigurations
                    .FirstOrDefaultAsync(g => g.GradeLetter.ToLower() == gradeConfig.GradeLetter.ToLower() &&
                                              g.SchoolId == gradeConfig.SchoolId &&
                                              g.AcademicYearId == gradeConfig.AcademicYearId);

                if (existingGrade != null)
                {
                    TempData["Error"] = $"A grade with letter '{gradeConfig.GradeLetter}' already exists for this school and academic year.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Validate that the academic year exists
                var academicYearExists = await _context.AcademicYears
                    .AnyAsync(a => a.YearId == gradeConfig.AcademicYearId.Value);
                if (!academicYearExists)
                {
                    TempData["Error"] = "Selected Academic Year does not exist.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Validate that the school exists if provided
                if (gradeConfig.SchoolId.HasValue && gradeConfig.SchoolId.Value > 0)
                {
                    var schoolExists = await _context.Schools
                        .AnyAsync(s => s.Id == gradeConfig.SchoolId.Value);
                    if (!schoolExists)
                    {
                        TempData["Error"] = "Selected School does not exist.";
                        return RedirectToAction(nameof(GradeConfigurations));
                    }
                }
                else
                {
                    // Ensure SchoolId is null for global configs (not 0)
                    gradeConfig.SchoolId = null;
                }

                // If this is marked as passing grade, update other grades within the same scope
                if (gradeConfig.IsPassingGrade)
                {
                    var existingPassingGrades = await _context.GradeConfigurations
                        .Where(g => g.IsPassingGrade &&
                                    g.SchoolId == gradeConfig.SchoolId &&
                                    g.AcademicYearId == gradeConfig.AcademicYearId)
                        .ToListAsync();

                    foreach (var grade in existingPassingGrades)
                    {
                        grade.IsPassingGrade = false;
                    }
                }

                // Set creation metadata
                gradeConfig.CreatedAt = DateTime.Now;
                gradeConfig.CreatedBy = User.Identity.Name;

                _context.GradeConfigurations.Add(gradeConfig);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Grade configuration '{gradeConfig.GradeLetter}' was created successfully.";
                return RedirectToAction(nameof(GradeConfigurations));
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error creating grade configuration: {ex}");
                TempData["Error"] = "An error occurred while creating the grade configuration. Please try again.";
                return RedirectToAction(nameof(GradeConfigurations));
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGradeConfig(GradeConfiguration gradeConfig)
        {
            try
            {
                // Validate Academic Year is required
                if (!gradeConfig.AcademicYearId.HasValue || gradeConfig.AcademicYearId.Value == 0)
                {
                    TempData["Error"] = "Academic Year is required.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                var existingConfig = await _context.GradeConfigurations.FindAsync(gradeConfig.Id);
                if (existingConfig == null)
                {
                    TempData["Error"] = "Grade configuration not found.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Validate score range
                if (gradeConfig.MinScore >= gradeConfig.MaxScore)
                {
                    TempData["Error"] = "Minimum score must be less than maximum score.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Check for overlapping ranges (excluding current grade) within the same school and academic year
                var overlapping = await _context.GradeConfigurations
                    .Where(g => g.Id != gradeConfig.Id &&
                                g.SchoolId == gradeConfig.SchoolId &&
                                g.AcademicYearId == gradeConfig.AcademicYearId)
                    .AnyAsync(g => (gradeConfig.MinScore >= g.MinScore && gradeConfig.MinScore <= g.MaxScore) ||
                                  (gradeConfig.MaxScore >= g.MinScore && gradeConfig.MaxScore <= g.MaxScore) ||
                                  (g.MinScore >= gradeConfig.MinScore && g.MinScore <= gradeConfig.MaxScore));

                if (overlapping)
                {
                    TempData["Error"] = "The score range overlaps with an existing grade configuration for the same school and academic year.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Check for duplicate grade letter (excluding current grade) within the same school and academic year
                var duplicateGrade = await _context.GradeConfigurations
                    .FirstOrDefaultAsync(g => g.Id != gradeConfig.Id &&
                                            g.GradeLetter.ToLower() == gradeConfig.GradeLetter.ToLower() &&
                                            g.SchoolId == gradeConfig.SchoolId &&
                                            g.AcademicYearId == gradeConfig.AcademicYearId);

                if (duplicateGrade != null)
                {
                    TempData["Error"] = $"A grade with letter '{gradeConfig.GradeLetter}' already exists for this school and academic year.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Validate that the academic year exists
                var academicYearExists = await _context.AcademicYears
                    .AnyAsync(a => a.YearId == gradeConfig.AcademicYearId.Value);
                if (!academicYearExists)
                {
                    TempData["Error"] = "Selected Academic Year does not exist.";
                    return RedirectToAction(nameof(GradeConfigurations));
                }

                // Validate that the school exists if provided
                if (gradeConfig.SchoolId.HasValue && gradeConfig.SchoolId.Value > 0)
                {
                    var schoolExists = await _context.Schools
                        .AnyAsync(s => s.Id == gradeConfig.SchoolId.Value);
                    if (!schoolExists)
                    {
                        TempData["Error"] = "Selected School does not exist.";
                        return RedirectToAction(nameof(GradeConfigurations));
                    }
                }
                else
                {
                    // Ensure SchoolId is null for global configs (not 0)
                    gradeConfig.SchoolId = null;
                }

                // Handle passing grade changes within the same scope
                if (gradeConfig.IsPassingGrade && !existingConfig.IsPassingGrade)
                {
                    var existingPassingGrades = await _context.GradeConfigurations
                        .Where(g => g.IsPassingGrade &&
                                    g.SchoolId == gradeConfig.SchoolId &&
                                    g.AcademicYearId == gradeConfig.AcademicYearId)
                        .ToListAsync();

                    foreach (var grade in existingPassingGrades)
                    {
                        grade.IsPassingGrade = false;
                    }
                }

                // Update properties
                existingConfig.GradeLetter = gradeConfig.GradeLetter;
                existingConfig.MinScore = gradeConfig.MinScore;
                existingConfig.MaxScore = gradeConfig.MaxScore;
                existingConfig.GPAValue = gradeConfig.GPAValue;
                existingConfig.Description = gradeConfig.Description;
                existingConfig.IsPassingGrade = gradeConfig.IsPassingGrade;
                existingConfig.IsActive = gradeConfig.IsActive;
                existingConfig.SchoolId = gradeConfig.SchoolId;
                existingConfig.AcademicYearId = gradeConfig.AcademicYearId;
                existingConfig.UpdatedAt = DateTime.Now;
                existingConfig.UpdatedBy = User.Identity.Name;

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Grade configuration '{gradeConfig.GradeLetter}' was updated successfully.";
                return RedirectToAction(nameof(GradeConfigurations));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating grade configuration: {ex}");
                TempData["Error"] = "An error occurred while updating the grade configuration. Please try again.";
                return RedirectToAction(nameof(GradeConfigurations));
            }
        }

        // POST: Admin/DeleteGradeConfig
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGradeConfig(int id)
        {
            try
            {
                var gradeConfig = await _context.GradeConfigurations.FindAsync(id);
                if (gradeConfig == null)
                {
                    TempData["Error"] = "Grade configuration not found.";
                    return NotFound();
                }

                // Check if there are any student grades using this configuration
                // This would require a relationship with a StudentGrade table
                // var hasStudentGrades = await _context.StudentGrades.AnyAsync(sg => sg.GradeConfigurationId == id);
                // if (hasStudentGrades)
                // {
                //     TempData["Error"] = "Cannot delete grade configuration as it is being used by student grades.";
                //     return RedirectToAction(nameof(GradeConfigurations));
                // }

                _context.GradeConfigurations.Remove(gradeConfig);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Grade configuration '{gradeConfig.GradeLetter}' was deleted successfully.";
                return RedirectToAction(nameof(GradeConfigurations));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting grade configuration: {ex}");
                TempData["Error"] = "An error occurred while deleting the grade configuration. Please try again.";
                return RedirectToAction(nameof(GradeConfigurations));
            }
        }


        // GET: Admin/Campuses
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Campuses()
        {
            var campuses = await _context.Campuses
                .ToListAsync();

            // Get data for sidebar statistics
            ViewBag.TotalHostels = await _context.Hostels.CountAsync();

            return View(campuses);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetCampus(int id)
        {
            var campus = await _context.Campuses
                .FirstOrDefaultAsync(c => c.CampusId == id);

            if (campus == null)
            {
                return NotFound();
            }

            var campusDto = new
            {
                id = campus.CampusId,
                name = campus.CampusName,
                location = campus.Location,
                description = campus.Description,
                isActive = campus.IsActive
            };

            return Json(campusDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCampus(Campus campus)
        {
            try
            {
                campus.CreatedBy = User.Identity.Name;
                campus.CreatedAt = DateTime.Now;

                _context.Campuses.Add(campus);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Campus created successfully.";
                return RedirectToAction(nameof(Campuses));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating campus. Please try again.";
                ModelState.AddModelError("", "Error creating campus. Please try again.");
            }

            // If we got this far, something failed, redisplay form
            return RedirectToAction(nameof(Campuses));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCampus(Campus campus)
        {
            try
            {
                var existingCampus = await _context.Campuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CampusId == campus.CampusId);

                if (existingCampus == null)
                {
                    TempData["Error"] = "Campus not found.";
                    return NotFound();
                }

                // Preserve the creation audit fields
                campus.CreatedBy = existingCampus.CreatedBy;
                campus.CreatedAt = existingCampus.CreatedAt;

                // Update the modification audit fields
                campus.UpdatedAt = DateTime.Now;
                campus.UpdatedBy = User.Identity.Name;

                _context.Entry(campus).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Campus updated successfully.";
                return RedirectToAction(nameof(Campuses));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CampusExists(campus.CampusId))
                {
                    TempData["Error"] = "Campus not found.";
                    return NotFound();
                }
                else
                {
                    TempData["Error"] = "Error updating campus. Please try again.";
                    ModelState.AddModelError("", "Error updating campus. Please try again.");
                }
            }

            return RedirectToAction(nameof(Campuses));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCampus(int id)
        {
            var campus = await _context.Campuses.FindAsync(id);
            if (campus == null)
            {
                TempData["Error"] = "Campus not found.";
                return NotFound();
            }

            try
            {
                _context.Campuses.Remove(campus);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Campus deleted successfully.";
                return RedirectToAction(nameof(Campuses));
            }
            catch (Exception ex)
            {
                // Log the error
                TempData["Error"] = "Error deleting campus. Please try again.";
                ModelState.AddModelError("", "Error deleting campus. Please try again.");
                return RedirectToAction(nameof(Campuses));
            }
        }


        private bool CampusExists(int id)
        {
            return _context.Campuses.Any(c => c.CampusId == id);
        }


        // GET: Admin/Hostels
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Hostels()
        {
            var hostels = await _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Warden)
                .ToListAsync();

            // Get data for sidebar statistics and dropdowns
            ViewBag.TotalRooms = await _context.Rooms.CountAsync();
            ViewBag.TotalBeds = await _context.BedSpaces.CountAsync();
            ViewBag.Campuses = new SelectList(await _context.Campuses.Where(c => c.IsActive).ToListAsync(), "CampusId", "CampusName");

            // For Warden dropdown, get all staff users
            // Get all users with the HostelManager role for the warden dropdown
            var wardenRoleId = await _context.Roles
               .Where(r => r.Name == "HostelManager")
               .Select(r => r.Id)
               .FirstOrDefaultAsync();

            var wardens = await _context.UserRoles
               .Where(ur => ur.RoleId == wardenRoleId)
               .Join(_context.Users,
                   ur => ur.UserId,
                   u => u.Id,
                   (ur, u) => new { u.Id, u.UserName })
               .ToListAsync();

            ViewBag.Wardens = new SelectList(wardens, "Id", "UserName");

            return View(hostels);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetHostel(int id)
        {
            var hostel = await _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Warden)
                .FirstOrDefaultAsync(h => h.HostelId == id);

            if (hostel == null)
            {
                return NotFound();
            }

            // Get room statistics for this hostel
            int totalRooms = await _context.Rooms.CountAsync(r => r.HostelId == id);
            int availableRooms = await _context.Rooms.CountAsync(r => r.HostelId == id && r.Status == Enums.Status.Available);
            int totalBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id)
                .CountAsync();
            int occupiedBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id && b.Status == Enums.Status.Occupied)
                .CountAsync();

            var hostelDto = new
            {
                id = hostel.HostelId,
                name = hostel.HostelName,
                gender = hostel.Gender,
                campusId = hostel.CampusId,
                wardenId = hostel.WardenId,
                totalRooms = hostel.TotalRooms,
                totalCapacity = hostel.TotalCapacity,
                status = hostel.Status,
                description = hostel.Description,
                // Include the new room generation properties
                defaultRoomType = hostel.DefaultRoomType,
                defaultCapacity = hostel.DefaultCapacity,
                roomsPerFloor = hostel.RoomsPerFloor,
                roomNumberingPattern = hostel.RoomNumberingPattern,
                autoGenerateBeds = hostel.AutoGenerateBeds,
                campusName = hostel.Campus?.CampusName,
                wardenName = hostel.Warden?.UserName,
                statistics = new
                {
                    totalRooms,
                    availableRooms,
                    totalBeds,
                    occupiedBeds,
                    occupancyRate = totalBeds > 0 ? (int)((double)occupiedBeds / totalBeds * 100) : 0
                }
            };

            return Json(hostelDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateHostel(Hostel hostel)
        {
            try
            {
                // Begin transaction to ensure all operations succeed or fail together
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Set audit fields
                    hostel.CreatedBy = User.Identity.Name;
                    hostel.CreatedAt = DateTime.Now;

                    // Set default values if not provided
                    if (string.IsNullOrEmpty(hostel.DefaultRoomType))
                        hostel.DefaultRoomType = "Single";

                    if (hostel.DefaultCapacity <= 0)
                        hostel.DefaultCapacity = 1;

                    if (hostel.RoomsPerFloor <= 0)
                        hostel.RoomsPerFloor = 10;

                    if (string.IsNullOrEmpty(hostel.RoomNumberingPattern))
                        hostel.RoomNumberingPattern = "F{0}R{1}";

                    // Add and save the hostel
                    _context.Hostels.Add(hostel);
                    await _context.SaveChangesAsync();

                    // Generate rooms for the hostel
                    await GenerateRoomsForHostel(hostel);

                    // Calculate the total capacity from room capacities
                    int totalCapacity = await _context.Rooms
                        .Where(r => r.HostelId == hostel.HostelId)
                        .SumAsync(r => r.Capacity);

                    // Update the hostel with the calculated capacity
                    hostel.TotalCapacity = totalCapacity;
                    await _context.SaveChangesAsync();

                    // Commit the transaction
                    await transaction.CommitAsync();

                    TempData["Success"] = "Hostel created successfully with " + hostel.TotalRooms + " rooms.";
                    return RedirectToAction(nameof(Hostels));
                }
                catch (Exception ex)
                {
                    // Rollback on error
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating hostel. Please try again.";
                ModelState.AddModelError("", "Error creating hostel. Please try again.");
            }

            // If we got this far, something failed, redisplay form
            ViewBag.Campuses = new SelectList(await _context.Campuses.Where(c => c.IsActive).ToListAsync(), "CampusId", "CampusName", hostel.CampusId);

            // Get all users with the HostelManager role for the warden dropdown
            var wardenRoleId = await _context.Roles
               .Where(r => r.Name == "HostelManager")
               .Select(r => r.Id)
               .FirstOrDefaultAsync();

            var wardens = await _context.UserRoles
               .Where(ur => ur.RoleId == wardenRoleId)
               .Join(_context.Users,
                   ur => ur.UserId,
                   u => u.Id,
                   (ur, u) => new { u.Id, u.UserName })
               .ToListAsync();

            ViewBag.Wardens = new SelectList(wardens, "Id", "UserName", hostel.WardenId);

            return RedirectToAction(nameof(Hostels));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateHostel(Hostel hostel)
        {
            try
            {
                var existingHostel = await _context.Hostels
                    .Include(h => h.Rooms)
                        .ThenInclude(r => r.BedSpaces)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HostelId == hostel.HostelId);

                if (existingHostel == null)
                {
                    TempData["Error"] = "Hostel not found.";
                    return NotFound();
                }

                // Begin transaction
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Preserve the creation audit fields
                    hostel.CreatedBy = existingHostel.CreatedBy;
                    hostel.CreatedAt = existingHostel.CreatedAt;

                    // Update the modification audit fields
                    hostel.UpdatedAt = DateTime.Now;
                    hostel.UpdatedBy = User.Identity.Name;

                    // Handle room count changes
                    if (hostel.TotalRooms != existingHostel.Rooms.Count())
                    {
                        // Check if we need to add or remove rooms
                        if (hostel.TotalRooms > existingHostel.Rooms.Count())
                        {
                            // Add more rooms
                            int roomsToAdd = hostel.TotalRooms - existingHostel.Rooms.Count();
                            await AddRoomsToHostel(hostel, roomsToAdd, existingHostel.Rooms.Count());
                        }
                        else if (hostel.TotalRooms < existingHostel.Rooms.Count())
                        {
                            // Remove rooms if possible
                            int roomsToRemove = existingHostel.Rooms.Count() - hostel.TotalRooms;
                            bool canRemoveRooms = await CanRemoveRooms(hostel.HostelId, roomsToRemove);

                            if (!canRemoveRooms)
                            {
                                TempData["Error"] = "Cannot reduce room count. Some rooms that would be removed have occupied beds.";
                                return RedirectToAction(nameof(Hostels));
                            }

                            await RemoveRoomsFromHostel(hostel.HostelId, roomsToRemove);
                        }
                    }

                    // Update the hostel
                    _context.Entry(hostel).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    // Calculate the total capacity from room capacities
                    int totalCapacity = await _context.Rooms
                        .Where(r => r.HostelId == hostel.HostelId)
                        .SumAsync(r => r.Capacity);

                    // Update the hostel with the calculated capacity
                    hostel.TotalCapacity = totalCapacity;
                    await _context.SaveChangesAsync();

                    // Commit the transaction
                    await transaction.CommitAsync();

                    TempData["Success"] = "Hostel updated successfully.";
                    return RedirectToAction(nameof(Hostels));
                }
                catch (Exception ex)
                {
                    // Rollback on error
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HostelExists(hostel.HostelId))
                {
                    TempData["Error"] = "Hostel not found.";
                    return NotFound();
                }
                else
                {
                    TempData["Error"] = "Error updating hostel. Please try again.";
                    ModelState.AddModelError("", "Error updating hostel. Please try again.");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating hostel. Please try again.";
                ModelState.AddModelError("", "Error updating hostel. Please try again.");
            }

            // If we got this far, something failed, redisplay form
            ViewBag.Campuses = new SelectList(await _context.Campuses.Where(c => c.IsActive).ToListAsync(), "CampusId", "CampusName", hostel.CampusId);

            // Get all users with the HostelManager role for the warden dropdown
            var wardenRoleId = await _context.Roles
               .Where(r => r.Name == "HostelManager")
               .Select(r => r.Id)
               .FirstOrDefaultAsync();

            var wardens = await _context.UserRoles
               .Where(ur => ur.RoleId == wardenRoleId)
               .Join(_context.Users,
                   ur => ur.UserId,
                   u => u.Id,
                   (ur, u) => new { u.Id, u.UserName })
               .ToListAsync();

            ViewBag.Wardens = new SelectList(wardens, "Id", "UserName", hostel.WardenId);

            return RedirectToAction(nameof(Hostels));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteHostel(int id)
        {
            var hostel = await _context.Hostels.FindAsync(id);
            if (hostel == null)
            {
                TempData["Error"] = "Hostel not found.";
                return NotFound();
            }

            // Check if the hostel has rooms or allocations
            bool hasRooms = await _context.Rooms.AnyAsync(r => r.HostelId == id);

            if (hasRooms)
            {
                TempData["Error"] = "Cannot delete hostel with assigned rooms. Remove all rooms first.";
                return RedirectToAction(nameof(Hostels));
            }

            try
            {
                _context.Hostels.Remove(hostel);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Hostel deleted successfully.";
                return RedirectToAction(nameof(Hostels));
            }
            catch (Exception ex)
            {
                // Log the error
                TempData["Error"] = "Error deleting hostel. Please try again.";
                ModelState.AddModelError("", "Error deleting hostel. Please try again.");
                return RedirectToAction(nameof(Hostels));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRooms(int hostelId)
        {
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == hostelId)
                .Select(r => new {
                    r.RoomId,
                    r.RoomNumber,
                    r.Floor,
                    r.RoomType,
                    r.Capacity,
                    r.Status,
                    BedCount = r.BedSpaces.Count,
                    OccupiedBeds = r.BedSpaces.Count(b => b.Status == Enums.Status.Occupied)
                })
                .ToListAsync();

            return Json(rooms);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> HostelDetails(int id)
        {
            var hostel = await _context.Hostels
                .Include(h => h.Campus)
                .Include(h => h.Warden)
                .FirstOrDefaultAsync(h => h.HostelId == id);

            if (hostel == null)
            {
                TempData["Error"] = "Hostel not found.";
                return RedirectToAction(nameof(Hostels));
            }

            // Get room statistics
            ViewBag.TotalRooms = await _context.Rooms.CountAsync(r => r.HostelId == id);
            ViewBag.AvailableRooms = await _context.Rooms.CountAsync(r => r.HostelId == id && r.Status == Enums.Status.Available);
            ViewBag.MaintenanceRooms = await _context.Rooms.CountAsync(r => r.HostelId == id && r.Status == Enums.Status.Maintenance);

            // Get bed statistics
            ViewBag.TotalBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id)
                .CountAsync();
            ViewBag.OccupiedBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id && b.Status == Enums.Status.Occupied)
                .CountAsync();
            ViewBag.AvailableBeds = await _context.BedSpaces
                .Where(b => b.Room.HostelId == id && b.Status == Enums.Status.Available)
                .CountAsync();

            // Get maintenance requests
            //ViewBag.MaintenanceRequests = await _context.MaintenanceRequests
            //    .Where(m => m.Room.HostelId == id)
            //    .CountAsync();

            // Get room type distribution
            var roomTypeDistribution = await _context.Rooms
                .Where(r => r.HostelId == id)
                .GroupBy(r => r.RoomType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.RoomTypeDistribution = roomTypeDistribution;

            // Get all rooms for this hostel
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == id)
                .OrderBy(r => r.Floor)
                .ThenBy(r => r.RoomNumber)
                .ToListAsync();
            ViewBag.Rooms = rooms;

            // Get floor distribution
            var floorDistribution = await _context.Rooms
                .Where(r => r.HostelId == id)
                .GroupBy(r => r.Floor)
                .Select(g => new { Floor = g.Key, Count = g.Count() })
                .OrderBy(f => f.Floor)
                .ToListAsync();
            ViewBag.FloorDistribution = floorDistribution;

            // Add room generation settings to ViewBag
            ViewBag.DefaultRoomType = hostel.DefaultRoomType;
            ViewBag.DefaultCapacity = hostel.DefaultCapacity;
            ViewBag.RoomsPerFloor = hostel.RoomsPerFloor;
            ViewBag.RoomNumberingPattern = hostel.RoomNumberingPattern;
            ViewBag.AutoGenerateBeds = hostel.AutoGenerateBeds;

            // Calculate number of floors
            ViewBag.TotalFloors = floorDistribution.Count > 0 ? floorDistribution.Max(f => f.Floor) + 1 : 0;

            return View(hostel);
        }

        private bool HostelExists(int id)
        {
            return _context.Hostels.Any(e => e.HostelId == id);
        }


     
        // GET: Admin/Rooms
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Rooms(int? hostelId)
        {
            IQueryable<Room> roomsQuery = _context.Rooms
                .Include(r => r.Hostel)
                .Include(r => r.BedSpaces);

            // Filter by hostel if provided
            if (hostelId.HasValue)
            {
                roomsQuery = roomsQuery.Where(r => r.HostelId == hostelId.Value);
                ViewBag.CurrentHostel = await _context.Hostels.FindAsync(hostelId.Value);
            }

            var rooms = await roomsQuery.OrderBy(r => r.HostelId).ThenBy(r => r.RoomNumber).ToListAsync();

            // Prepare dropdowns and statistics
            ViewBag.Hostels = new SelectList(await _context.Hostels.OrderBy(h => h.HostelName).ToListAsync(), "HostelId", "HostelName");
            ViewBag.TotalBeds = await _context.BedSpaces.CountAsync();
            ViewBag.OccupiedBeds = await _context.BedSpaces.CountAsync(b => b.Status == Status.Occupied);
            ViewBag.AvailableBeds = await _context.BedSpaces.CountAsync(b => b.Status == Status.Available);

            // Room type statistics for charts
            var roomTypeStats = await _context.Rooms
                .GroupBy(r => r.RoomType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.RoomTypeStats = roomTypeStats;

            return View(rooms);
        }

        // GET: Admin/GetRoom/5
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRoom(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Hostel)
                .Include(r => r.BedSpaces)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
            {
                return NotFound();
            }

            // Get room resources
            var resources = await _context.RoomResources
                .Where(rr => rr.RoomId == id)
                .ToListAsync();

            var roomDto = new
            {
                id = room.RoomId,
                hostelId = room.HostelId,
                hostelName = room.Hostel?.HostelName,
                roomNumber = room.RoomNumber,
                floor = room.Floor,
                roomType = room.RoomType,
                capacity = room.Capacity,
                gender = room.Gender,
                status = room.Status.ToString(),
                isSpecialReservation = room.IsSpecialReservation,
                bedSpaces = room.BedSpaces.Select(b => new
                {
                    id = b.BedId,
                    identifier = b.BedIdentifier,
                    status = b.Status.ToString()
                }).ToList(),
                resources = resources.Select(r => new
                {
                    id = r.ResourceId,
                    quantity = r.Quantity,
                    status = r.Status.ToString()
                }).ToList(),
                bedCount = room.BedSpaces.Count,
                occupiedBedCount = room.BedSpaces.Count(b => b.Status == Status.Occupied)
            };

            return Json(roomDto);
        }

        // GET: Admin/RoomDetails/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RoomDetails(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Hostel)
                .Include(r => r.BedSpaces)
                .Include(r => r.Resources)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
            {
                TempData["Error"] = "Room not found.";
                return RedirectToAction(nameof(Rooms));
            }

            // Get occupancy statistics
            ViewBag.OccupiedBeds = room.BedSpaces.Count(b => b.Status == Status.Occupied);
            ViewBag.AvailableBeds = room.BedSpaces.Count(b => b.Status == Status.Available);
            ViewBag.OccupancyRate = room.BedSpaces.Count > 0
                ? (int)((double)room.BedSpaces.Count(b => b.Status == Status.Occupied) / room.BedSpaces.Count * 100)
                : 0;

            // Get maintenance stats
            ViewBag.ResourcesNeedingRepair = room.Resources.Count(r => r.Status == Status.Maintenance);

            return View(room);
        }

        // POST: Admin/CreateRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateRoom(Room room, int[]? resourceTypeIds, int[]? resourceQuantities)
        {
            try
            {
                // Set audit fields
                room.CreatedBy = User.Identity.Name;
                room.CreatedAt = DateTime.Now;

                // Begin transaction
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Add the room
                    _context.Rooms.Add(room);
                    await _context.SaveChangesAsync();

                    // Create bed spaces based on capacity
                    for (int i = 0; i < room.Capacity; i++)
                    {
                        var bedIdentifier = ConvertToAlphabeticIdentifier(i + 1);
                        var bedSpace = new BedSpace
                        {
                            RoomId = room.RoomId,
                            BedIdentifier = bedIdentifier,
                            Status = Status.Available,
                            CreatedBy = User.Identity.Name,
                            CreatedAt = DateTime.Now
                        };

                        _context.BedSpaces.Add(bedSpace);
                    }

                    // Save bed spaces
                    await _context.SaveChangesAsync();

                    // Add resources if provided
                    if (resourceTypeIds != null && resourceQuantities != null && resourceTypeIds.Length == resourceQuantities.Length)
                    {
                        for (int i = 0; i < resourceTypeIds.Length; i++)
                        {
                            if (resourceTypeIds[i] > 0 && resourceQuantities[i] > 0)
                            {
                                var roomResource = new RoomResource
                                {
                                    RoomId = room.RoomId,
                                    Quantity = resourceQuantities[i],
                                    Status = Status.Available,
                                    CreatedBy = User.Identity.Name,
                                    CreatedAt = DateTime.Now
                                };

                                _context.RoomResources.Add(roomResource);
                            }
                        }

                        await _context.SaveChangesAsync();
                    }

                    // Commit the transaction
                    await transaction.CommitAsync();

                    TempData["Success"] = "Room created successfully.";
                    return RedirectToAction(nameof(HostelDetails), new { id = room.HostelId });
                }
                catch (Exception ex)
                {
                    // Rollback on error
                    await transaction.RollbackAsync();
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating room. Please try again.";
                ModelState.AddModelError("", "Error creating room. Please try again.");
            }

            // If we got this far, something failed, redisplay form
            ViewBag.Hostels = new SelectList(await _context.Hostels.OrderBy(h => h.HostelName).ToListAsync(), "HostelId", "HostelName", room.HostelId);
            return RedirectToAction(nameof(HostelDetails), new { id = room.HostelId });
        }

        // POST: Admin/UpdateRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateRoom(Room room, int[]? resourceTypeIds, int[]? resourceQuantities, int[]? resourceIds)
        {
            try
            {
                var existingRoom = await _context.Rooms
                    .Include(r => r.BedSpaces)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RoomId == room.RoomId);

                if (existingRoom == null)
                {
                    TempData["Error"] = "Room not found.";
                    return NotFound();
                }

                // Check if capacity changed and handle bed spaces
                if (room.Capacity != existingRoom.Capacity)
                {
                    // Cannot reduce capacity if beds are occupied
                    int occupiedBeds = existingRoom.BedSpaces.Count(b => b.Status == Status.Occupied);
                    if (room.Capacity < occupiedBeds)
                    {
                        TempData["Error"] = $"Cannot reduce room capacity below the number of occupied beds ({occupiedBeds}).";
                        return RedirectToAction(nameof(Rooms), new { hostelId = room.HostelId });
                    }
                }

                // Start transaction
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Preserve the creation audit fields
                    room.CreatedBy = existingRoom.CreatedBy;
                    room.CreatedAt = existingRoom.CreatedAt;

                    // Update the modification audit fields
                    room.UpdatedAt = DateTime.Now;
                    room.UpdatedBy = User.Identity.Name;

                    // Update room
                    _context.Entry(room).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    // Update bed spaces if capacity changed
                    if (room.Capacity != existingRoom.Capacity)
                    {
                        if (room.Capacity > existingRoom.Capacity)
                        {
                            // Add new bed spaces
                            for (int i = existingRoom.Capacity; i < room.Capacity; i++)
                            {
                                var bedIdentifier = ConvertToAlphabeticIdentifier(i + 1);
                                var bedSpace = new BedSpace
                                {
                                    RoomId = room.RoomId,
                                    BedIdentifier = bedIdentifier,
                                    Status = Status.Available,
                                    CreatedBy = User.Identity.Name,
                                    CreatedAt = DateTime.Now
                                };

                                _context.BedSpaces.Add(bedSpace);
                            }
                        }
                        else if (room.Capacity < existingRoom.Capacity)
                        {
                            // Remove unoccupied bed spaces
                            var bedSpacesToRemove = await _context.BedSpaces
                                .Where(b => b.RoomId == room.RoomId && b.Status != Status.Occupied)
                                .OrderByDescending(b => b.BedIdentifier)
                                .Take(existingRoom.Capacity - room.Capacity)
                                .ToListAsync();

                            _context.BedSpaces.RemoveRange(bedSpacesToRemove);
                        }

                        await _context.SaveChangesAsync();
                    }

                    // Update resources
                    if (resourceTypeIds != null && resourceQuantities != null)
                    {
                        // Get existing resources
                        var existingResources = await _context.RoomResources
                            .Where(rr => rr.RoomId == room.RoomId)
                            .ToListAsync();

                        // Process each resource
                        for (int i = 0; i < resourceTypeIds.Length; i++)
                        {
                            if (resourceTypeIds[i] > 0 && resourceQuantities[i] > 0)
                            {
                                // Check if this is an existing resource
                                int resourceId = resourceIds != null && i < resourceIds.Length ? resourceIds[i] : 0;
                                var existingResource = existingResources.FirstOrDefault(r => r.ResourceId == resourceId);

                                if (existingResource != null)
                                {
                                    // Update existing resource
                                    existingResource.Quantity = resourceQuantities[i];
                                    existingResource.UpdatedBy = User.Identity.Name;
                                    existingResource.UpdatedAt = DateTime.Now;
                                    _context.Entry(existingResource).State = EntityState.Modified;
                                }
                                else
                                {
                                    // Add new resource
                                    var roomResource = new RoomResource
                                    {
                                        RoomId = room.RoomId,
                                        Quantity = resourceQuantities[i],
                                        Status = Status.Available,
                                        CreatedBy = User.Identity.Name,
                                        CreatedAt = DateTime.Now
                                    };

                                    _context.RoomResources.Add(roomResource);
                                }
                            }
                        }

                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    TempData["Success"] = "Room updated successfully.";
                    return RedirectToAction(nameof(RoomDetails), new { id = room.RoomId });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw ex;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RoomExists(room.RoomId))
                {
                    TempData["Error"] = "Room not found.";
                    return NotFound();
                }
                else
                {
                    TempData["Error"] = "Error updating room. Please try again.";
                    ModelState.AddModelError("", "Error updating room. Please try again.");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating room. Please try again.";
                ModelState.AddModelError("", ex.Message);
            }


            return RedirectToAction(nameof(HostelDetails), new { id = room.HostelId });
        }

        // POST: Admin/DeleteRoom/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.BedSpaces)
                .Include(r => r.Resources)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
            {
                TempData["Error"] = "Room not found.";
                return NotFound();
            }

            // Check for occupied beds
            if (room.BedSpaces.Any(b => b.Status == Status.Occupied))
            {
                TempData["Error"] = "Cannot delete a room with occupied beds. Please relocate all occupants first.";
                return RedirectToAction(nameof(Rooms), new { hostelId = room.HostelId });
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Remove all resources associated with the room
                    _context.RoomResources.RemoveRange(room.Resources);
                    await _context.SaveChangesAsync();

                    // Remove all bed spaces
                    _context.BedSpaces.RemoveRange(room.BedSpaces);
                    await _context.SaveChangesAsync();

                    // Remove the room
                    _context.Rooms.Remove(room);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Room deleted successfully.";
                    return RedirectToAction(nameof(HostelDetails), new { id = room.HostelId });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting room. Please try again.";
                ModelState.AddModelError("", "Error deleting room. Please try again.");
                return RedirectToAction(nameof(HostelDetails), new { id = room.HostelId });
            }
        }

        // GET: Admin/GetRoomsByHostel/5
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRoomsByHostel(int hostelId)
        {
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == hostelId)
                .Select(r => new
                {
                    id = r.RoomId,
                    roomNumber = r.RoomNumber,
                    floor = r.Floor,
                    roomType = r.RoomType,
                    capacity = r.Capacity,
                    gender = r.Gender,
                    status = r.Status.ToString(),
                    bedCount = r.BedSpaces.Count,
                    occupiedBeds = r.BedSpaces.Count(b => b.Status == Status.Occupied)
                })
                .OrderBy(r => r.roomNumber)
                .ToListAsync();

            return Json(rooms);
        }

        // Helper method for bed identifiers
        private string ConvertToAlphabeticIdentifier(int number)
        {
            if (number <= 26)
            {
                // For numbers 1-26, return A-Z
                return ((char)(64 + number)).ToString();
            }
            else
            {
                // For numbers > 26, return AA, AB, etc.
                int firstChar = (number - 1) / 26;
                int secondChar = (number - 1) % 26 + 1;

                return $"{(char)(64 + firstChar)}{(char)(64 + secondChar)}";
            }
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(r => r.RoomId == id);
        }

        // Generates rooms for a newly created hostel
        private async Task GenerateRoomsForHostel(Hostel hostel)
        {
            // Calculate how many rooms should be on each floor
            int totalFloors = (int)Math.Ceiling((double)hostel.TotalRooms / hostel.RoomsPerFloor);
            List<Room> roomsToAdd = new List<Room>();
            List<BedSpace> bedSpacesToAdd = new List<BedSpace>();

            int roomCounter = 0;

            // Create rooms for each floor
            for (int floor = 0; floor < totalFloors && roomCounter < hostel.TotalRooms; floor++)
            {
                int roomsOnThisFloor = Math.Min(hostel.RoomsPerFloor, hostel.TotalRooms - roomCounter);

                for (int roomIndex = 0; roomIndex < roomsOnThisFloor; roomIndex++)
                {
                    string roomNumber = GenerateRoomNumber(hostel.RoomNumberingPattern, floor, roomIndex);

                    // Create new room
                    var room = new Room
                    {
                        HostelId = hostel.HostelId,
                        RoomNumber = roomNumber,
                        Floor = floor,
                        RoomType = hostel.DefaultRoomType,
                        Capacity = hostel.DefaultCapacity,
                        Gender = hostel.Gender, // Default to hostel gender
                        Status = Status.Available,
                        CreatedBy = hostel.CreatedBy,
                        CreatedAt = DateTime.Now
                    };

                    roomsToAdd.Add(room);
                    roomCounter++;
                }
            }

            // Add all rooms to the database
            await _context.Rooms.AddRangeAsync(roomsToAdd);
            await _context.SaveChangesAsync();

            // If auto-generate beds is enabled, create bed spaces for each room
            if (hostel.AutoGenerateBeds)
            {
                foreach (var room in roomsToAdd)
                {
                    for (int i = 0; i < room.Capacity; i++)
                    {
                        var bedIdentifier = ConvertToAlphabeticIdentifier(i + 1);
                        var bedSpace = new BedSpace
                        {
                            RoomId = room.RoomId,
                            BedIdentifier = bedIdentifier,
                            Status = Status.Available,
                            CreatedBy = hostel.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        bedSpacesToAdd.Add(bedSpace);
                    }
                }

                // Add all bed spaces to the database
                await _context.BedSpaces.AddRangeAsync(bedSpacesToAdd);
                await _context.SaveChangesAsync();
            }
        }

        // Adds additional rooms to an existing hostel
        private async Task AddRoomsToHostel(Hostel hostel, int roomsToAdd, int existingRoomCount)
        {
            // Get the current highest floor and room index
            var existingRooms = await _context.Rooms
                .Where(r => r.HostelId == hostel.HostelId)
                .OrderByDescending(r => r.Floor)
                .ThenByDescending(r => r.RoomNumber)
                .ToListAsync();

            int highestFloor = 0;
            int roomsOnHighestFloor = 0;

            if (existingRooms.Any())
            {
                highestFloor = existingRooms.Max(r => r.Floor);
                roomsOnHighestFloor = existingRooms.Count(r => r.Floor == highestFloor);
            }

            // Check if current highest floor has capacity for more rooms
            int roomsLeftOnHighestFloor = hostel.RoomsPerFloor - roomsOnHighestFloor;

            List<Room> roomsToCreate = new List<Room>();
            List<BedSpace> bedSpacesToCreate = new List<BedSpace>();

            int roomsAdded = 0;
            int currentFloor = highestFloor;
            int currentRoomIndex = roomsOnHighestFloor;

            // Create new rooms
            while (roomsAdded < roomsToAdd)
            {
                // If current floor is full, move to next floor
                if (currentRoomIndex >= hostel.RoomsPerFloor)
                {
                    currentFloor++;
                    currentRoomIndex = 0;
                }

                string roomNumber = GenerateRoomNumber(hostel.RoomNumberingPattern, currentFloor, currentRoomIndex);

                // Create new room
                var room = new Room
                {
                    HostelId = hostel.HostelId,
                    RoomNumber = roomNumber,
                    Floor = currentFloor,
                    RoomType = hostel.DefaultRoomType,
                    Capacity = hostel.DefaultCapacity,
                    Gender = hostel.Gender, // Default to hostel gender
                    Status = Status.Available,
                    CreatedBy = hostel.UpdatedBy ?? hostel.CreatedBy,
                    CreatedAt = DateTime.Now
                };

                roomsToCreate.Add(room);

                currentRoomIndex++;
                roomsAdded++;
            }

            // Add all rooms to the database
            await _context.Rooms.AddRangeAsync(roomsToCreate);
            await _context.SaveChangesAsync();

            // If auto-generate beds is enabled, create bed spaces for each room
            if (hostel.AutoGenerateBeds)
            {
                foreach (var room in roomsToCreate)
                {
                    for (int i = 0; i < room.Capacity; i++)
                    {
                        var bedIdentifier = ConvertToAlphabeticIdentifier(i + 1);
                        var bedSpace = new BedSpace
                        {
                            RoomId = room.RoomId,
                            BedIdentifier = bedIdentifier,
                            Status = Status.Available,
                            CreatedBy = hostel.UpdatedBy ?? hostel.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        bedSpacesToCreate.Add(bedSpace);
                    }
                }

                // Add all bed spaces to the database
                await _context.BedSpaces.AddRangeAsync(bedSpacesToCreate);
                await _context.SaveChangesAsync();
            }
        }

        // Checks if it's safe to remove rooms (no occupied beds)
        private async Task<bool> CanRemoveRooms(int hostelId, int roomsToRemove)
        {
            // Get rooms ordered by most recently added first (we'll remove newest rooms first)
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == hostelId)
                .Include(r => r.BedSpaces)
                .OrderByDescending(r => r.RoomId) // Assume higher IDs are newer rooms
                .Take(roomsToRemove)
                .ToListAsync();

            // Check if any of the rooms to be removed have occupied beds
            return !rooms.Any(r => r.BedSpaces.Any(b => b.Status == Status.Occupied));
        }

        // Removes rooms from a hostel
        private async Task RemoveRoomsFromHostel(int hostelId, int roomsToRemove)
        {
            // Get rooms ordered by most recently added first (we'll remove newest rooms first)
            var rooms = await _context.Rooms
                .Where(r => r.HostelId == hostelId)
                .Include(r => r.BedSpaces)
                .Include(r => r.Resources)
                .OrderByDescending(r => r.RoomId) // Assume higher IDs are newer rooms
                .Take(roomsToRemove)
                .ToListAsync();

            foreach (var room in rooms)
            {
                // Remove associated bed spaces
                _context.BedSpaces.RemoveRange(room.BedSpaces);

                // Remove associated resources
                _context.RoomResources.RemoveRange(room.Resources);
            }

            // Save changes so far
            await _context.SaveChangesAsync();

            // Now remove the rooms
            _context.Rooms.RemoveRange(rooms);
            await _context.SaveChangesAsync();
        }

        // Generates a room number based on the pattern, floor, and room index
        private string GenerateRoomNumber(string pattern, int floor, int roomIndex)
        {
            // Format: The pattern uses {0} for floor and {1} for room index
            // For example: "F{0}R{1}" with floor=1, roomIndex=2 becomes "F1R2"

            // Add leading zeros for room index if needed (e.g., 1 becomes 01)
            string roomIndexStr = (roomIndex + 1).ToString().PadLeft(2, '0');

            // Replace placeholders in the pattern
            return string.Format(pattern, floor + 1, roomIndexStr);
        }

        // This method is already in your code, but included here for completeness
        //private string ConvertToAlphabeticIdentifier(int index)
        //{
        //    // Convert numbers to alphabet identifiers (1 -> A, 2 -> B, etc.)
        //    if (index <= 26)
        //    {
        //        // Single letter for 1-26
        //        return ((char)(64 + index)).ToString();
        //    }
        //    else
        //    {
        //        // Double letters for higher numbers (27 -> AA, 28 -> AB, etc.)
        //        int firstLetter = (index - 1) / 26;
        //        int secondLetter = (index - 1) % 26 + 1;
        //        return ((char)(64 + firstLetter)).ToString() + ((char)(64 + secondLetter)).ToString();
        //    }
        //}



























































        // New action to test Tailwind CSS
        public IActionResult TailwindTest()
        {

            return View();
        }













        private async Task HandleIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }









        // UPDATED: AJAX endpoint for filtered fee configurations with Program Level filter
      

    }

    // Add these classes for the filter request
    public class FeeConfigFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortColumn { get; set; } = "academicYear";
        public string SortDirection { get; set; } = "desc";

        // Filter properties
        public int? FeeTypeId { get; set; }
        public int? AcademicYearId { get; set; }
        public int? Semester { get; set; }
        public int? SchoolId { get; set; }
        public int? ProgrammeId { get; set; }
        public int? ModeOfStudyId { get; set; }
        public int? YearOfStudy { get; set; }
        public int? ProgramLevelId { get; set; }
        public string StudentType { get; set; }

    }
}
