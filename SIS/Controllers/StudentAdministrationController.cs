using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Accounts;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using SIS.Models.ViewModels;
using SIS.Services.Accounting;
using SIS.Services.PhotoValidation;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin,Registrar, VC, DVC")]
    public class StudentAdministrationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StudentAdministrationController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IPhotoValidationService _photoValidationService;
        private readonly IStudentInvoiceService _studentInvoiceService;

        public StudentAdministrationController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<StudentAdministrationController> logger,
            IWebHostEnvironment webHostEnvironment,
            IPhotoValidationService photoValidationService,
            IStudentInvoiceService studentInvoiceService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _photoValidationService = photoValidationService;
            _studentInvoiceService = studentInvoiceService;
        }

        [HttpGet("StudentAdministration/Index/{studentId:int}")]
        public async Task<IActionResult> Index(int studentId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                var student = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Include(s => s.CurrentYearPeriod)
                        .ThenInclude(cyp => cyp.AcademicPeriod)
                    .Include(s => s.School)
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.ProgrammeLevel)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    TempData["Error"] = "Student not found.";
                    return RedirectToAction("Index", "StudentLookup");
                }

                // Get registered courses count
                var registeredCoursesCount = await _context.StudentCourseRegistrations
                    .Where(scr => scr.StudentId == studentId && scr.AcademicYearId == student.AcademicYearId && scr.YearPeriodId == student.CurrentYearPeriodId)
                    .CountAsync();

                var viewModel = new StudentAdministrationViewModel
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentId_Number,
                    FullName = student.FullName,
                    Email = student.Email,
                    Phone = student.Phone,
                    ProgrammeName = student.Programme?.Name ?? "N/A",
                    SchoolName = student.School?.Name ?? "N/A",
                    DepartmentName = student.Programme?.Department?.Name ?? "N/A",
                    ModeOfStudyName = student.ModeOfStudy?.ModeName ?? "N/A",
                    ProgrammeLevelName = student.ProgrammeLevel?.Name ?? "N/A",
                    AcademicYear = student.AcademicYear?.YearValue ?? "N/A",
                    CurrentYear = student.StudentCurrentYear ?? 0,
                    CurrentPeriodLabel = student.CurrentYearPeriod.FullLabel,
                    CurrentPeriodId = student.CurrentYearPeriodId,
                    StudentStatus = student.StudentStatus.ToString(),
                    RegistrationStatus = student.RegistrationStatus.ToString(),
                    IsRegistered = student.IsRegistered,
                    OutstandingFees = StudentTools.GetStudentOutstandingBalance(student.Id),
                    RegistrationDate = student.RegistrationDate,
                    AdmissionDate = student.AdmissionDate,
                    RegisteredCoursesCount = registeredCoursesCount,
                    UserRole = primaryRole,
                    AdminName = user.FullName,
                    PassportPhotoPath = student.PassportPhotoPath,
                    IsForeigner = student.IsForeigner
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student administration for student ID: {StudentId}", studentId);
                TempData["Error"] = "An error occurred while loading student details.";
                return RedirectToAction("Index", "StudentLookup");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStudentDetails(int studentId)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.School)
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.ProgrammeLevel)
                    .Include(s => s.AcademicYear)
                    .Include(s => s.StudentAddress)
                    .Include(s => s.NextOfKin)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                var studentData = new
                {
                    id = student.Id,
                    studentNumber = student.StudentId_Number,
                    fullName = student.FullName,
                    email = student.Email,
                    phone = student.Phone,
                    nrcOrPassportNumber = student.NrcOrPassportNumber,
                    dateOfBirth = student.DateOfBirth.ToString("yyyy-MM-dd"),
                    gender = student.Gender,
                    nationality = student.Nationality,
                    maritalStatus = student.MaritalStatus,
                    religion = student.Religion,
                    programmeId = student.ProgrammeId,
                    schoolId = student.SchoolId,
                    modeOfStudyId = student.ModeOfStudyId,
                    programmeLevelId = student.ProgrammeLevelId,
                    academicYearId = student.AcademicYearId,
                    currentYear = student.StudentCurrentYear ?? 1,
                    currentPeriodId = student.CurrentYearPeriodId,
                    currentPeriodLabel = student.CurrentYearPeriod?.FullLabel,
                    outstandingFees = student.OutstandingFees,
                    passportPhotoPath = student.PassportPhotoPath,
                    IsForeigner = student.IsForeigner,
                    address = student.StudentAddress != null ? new
                    {
                        addressLine1 = student.StudentAddress.AddressLine1,
                        addressLine2 = student.StudentAddress.AddressLine2,
                        city = student.StudentAddress.City,
                        state = student.StudentAddress.State,
                        country = student.StudentAddress.Country,
                        postalCode = student.StudentAddress.PostalCode
                    } : null,
                    nextOfKin = student.NextOfKin != null ? new
                    {
                        name = student.NextOfKin.Name,
                        relationship = student.NextOfKin.Relationship,
                        phone = student.NextOfKin.PhoneNumber,
                        email = student.NextOfKin.Email,
                        address = student.NextOfKin.Address
                    } : null
                };

                return Json(new { success = true, data = studentData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting student details for ID: {StudentId}", studentId);
                return Json(new { success = false, message = "Error retrieving student details" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStudentProfile(StudentAdminUpdateModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                // Use execution strategy to handle the transaction
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    // Begin transaction within the execution strategy
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        var student = await _context.Students.FindAsync(model.StudentId);
                        if (student == null)
                        {
                            throw new InvalidOperationException("Student not found");
                        }

                        // Log the change
                        var adminUser = await _userManager.GetUserAsync(User);
                        var originalData = $"Name: {student.FullName}, Email: {student.Email}, Phone: {student.Phone}";

                        // Check if email is changing
                        bool emailChanged = !string.Equals(student.Email, model.Email, StringComparison.OrdinalIgnoreCase);

                        // Update student record
                        student.FullName = model.FullName;
                        student.Email = model.Email;
                        student.Phone = model.Phone;
                        student.NrcOrPassportNumber = model.NrcOrPassportNumber;
                        student.DateOfBirth = model.DateOfBirth;
                        student.Gender = model.Gender;
                        student.Nationality = model.Nationality;
                        student.MaritalStatus = model.MaritalStatus;
                        student.Religion = model.Religion;
                        student.IsForeigner = model.IsForeigner;

                        // Handle photo upload
                        if (model.PassportPhoto != null && model.PassportPhoto.Length > 0)
                        {
                            // Validate photo first
                            var validationResult = await _photoValidationService.ValidatePassportPhotoAsync(model.PassportPhoto);
                            if (!validationResult.IsValid)
                            {
                                throw new InvalidOperationException($"Photo validation failed: {string.Join(", ", validationResult.Errors)}");
                            }

                            // Use same directory structure as application process
                            var photoDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "PassportPhotos", student.NrcOrPassportNumber ?? student.StudentId_Number);
                            Directory.CreateDirectory(photoDirectory);

                            var fileName = Path.GetFileName(model.PassportPhoto.FileName);
                            var filePath = Path.Combine(photoDirectory, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await model.PassportPhoto.CopyToAsync(stream);
                            }

                            // Delete old photo if exists
                            if (!string.IsNullOrEmpty(student.PassportPhotoPath) && System.IO.File.Exists(student.PassportPhotoPath))
                            {
                                try
                                {
                                    System.IO.File.Delete(student.PassportPhotoPath);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Could not delete old photo file: {FilePath}", student.PassportPhotoPath);
                                }
                            }

                            // Store absolute path (same as application process)
                            student.PassportPhotoPath = filePath;
                        }

                        // Update or create address
                        if (!string.IsNullOrEmpty(model.AddressLine1) || !string.IsNullOrEmpty(model.City))
                        {
                            if (student.StudentAddress == null)
                            {
                                student.StudentAddress = new StudentAddress
                                {
                                    AddressLine1 = model.AddressLine1 ?? "",
                                    AddressLine2 = model.AddressLine2,
                                    City = model.City ?? "",
                                    State = model.State ?? "",
                                    Country = model.Country ?? "",
                                    PostalCode = model.PostalCode ?? ""
                                };
                            }
                            else
                            {
                                student.StudentAddress.AddressLine1 = model.AddressLine1 ?? "";
                                student.StudentAddress.AddressLine2 = model.AddressLine2;
                                student.StudentAddress.City = model.City ?? "";
                                student.StudentAddress.State = model.State ?? "";
                                student.StudentAddress.Country = model.Country ?? "";
                                student.StudentAddress.PostalCode = model.PostalCode ?? "";
                            }
                        }

                        // Update or create next of kin
                        if (!string.IsNullOrEmpty(model.NextOfKinName))
                        {
                            if (student.NextOfKin == null)
                            {
                                student.NextOfKin = new StudNextOfKin
                                {
                                    Name = model.NextOfKinName,
                                    Relationship = model.NextOfKinRelationship ?? "",
                                    PhoneNumber = model.NextOfKinPhone ?? "",
                                    Email = model.NextOfKinEmail ?? "",
                                    Address = model.NextOfKinAddress ?? ""
                                };
                            }
                            else
                            {
                                student.NextOfKin.Name = model.NextOfKinName;
                                student.NextOfKin.Relationship = model.NextOfKinRelationship ?? "";
                                student.NextOfKin.PhoneNumber = model.NextOfKinPhone ?? "";
                                student.NextOfKin.Email = model.NextOfKinEmail ?? "";
                                student.NextOfKin.Address = model.NextOfKinAddress ?? "";
                            }
                        }


                        // Update username if email changed (assuming email is used as username)
                        if (emailChanged)
                        {
                            student.Username = model.Email;
                        }

                        // Update audit fields
                        student.UpdatedBy = adminUser?.FullName ?? "System";
                        student.UpdatedAt = DateTime.Now;

                        _context.Students.Update(student);

                        // Find ApplicationUser using multiple strategies
                        ApplicationUser appUser = null;

                        // Strategy 1: Try by current username
                        if (!string.IsNullOrEmpty(student.Username))
                        {
                            appUser = await _userManager.FindByNameAsync(student.Username);
                            _logger.LogInformation($"Lookup by username '{student.Username}': {(appUser != null ? "Found" : "Not found")}");
                        }

                        // Strategy 2: If not found, try by current email
                        if (appUser == null && !string.IsNullOrEmpty(student.Email))
                        {
                            appUser = await _userManager.FindByEmailAsync(student.Email);
                            _logger.LogInformation($"Lookup by current email '{student.Email}': {(appUser != null ? "Found" : "Not found")}");
                        }

                        // Strategy 3: If email is changing and still not found, try by old email
                        if (appUser == null && emailChanged && !string.IsNullOrEmpty(originalData))
                        {
                            // Extract original email from originalData or use a different approach
                            var originalEmail = ExtractEmailFromOriginalData(originalData);
                            if (!string.IsNullOrEmpty(originalEmail))
                            {
                                appUser = await _userManager.FindByEmailAsync(originalEmail);
                                _logger.LogInformation($"Lookup by original email '{originalEmail}': {(appUser != null ? "Found" : "Not found")}");
                            }
                        }

                        // Strategy 4: If still not found, try by student ID as username (if your system uses this pattern)
                        if (appUser == null && !string.IsNullOrEmpty(student.StudentId_Number))
                        {
                            appUser = await _userManager.FindByNameAsync(student.StudentId_Number);
                            _logger.LogInformation($"Lookup by student ID '{student.StudentId_Number}': {(appUser != null ? "Found" : "Not found")}");
                        }

                        // Strategy 5: As a last resort, search through all users by FullName match (use with caution)
                        if (appUser == null)
                        {
                            var possibleUsers = await _userManager.Users
                                .Where(u => u.FullName == student.FullName)
                                .ToListAsync();

                            if (possibleUsers.Count == 1)
                            {
                                appUser = possibleUsers.First();
                                _logger.LogInformation($"Found user by FullName match: {appUser.UserName}");
                            }
                            else if (possibleUsers.Count > 1)
                            {
                                _logger.LogWarning($"Multiple users found with FullName '{student.FullName}'. Skipping ApplicationUser update.");
                            }
                        }

                        if (appUser != null)
                        {
                            // Store original username for lookup if email changed
                            var originalUsername = appUser.UserName;

                            // Update basic fields
                            appUser.FullName = model.FullName;
                            appUser.Email = model.Email;
                            appUser.PhoneNumber = model.Phone;

                            // If email changed, update username and related fields
                            if (emailChanged)
                            {
                                appUser.UserName = model.Email;
                                appUser.NormalizedUserName = _userManager.NormalizeName(model.Email);
                                appUser.NormalizedEmail = _userManager.NormalizeEmail(model.Email);

                                // Reset email confirmation if email changed
                                appUser.EmailConfirmed = false;

                                // Update security stamp to invalidate existing tokens/cookies
                                await _userManager.UpdateSecurityStampAsync(appUser);
                            }
                            else
                            {
                                // Even if email didn't change, ensure normalized email is correct
                                appUser.NormalizedEmail = _userManager.NormalizeEmail(model.Email);
                            }

                            var updateResult = await _userManager.UpdateAsync(appUser);

                            if (!updateResult.Succeeded)
                            {
                                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                                throw new InvalidOperationException($"Failed to update user account: {errors}");
                            }

                            // If username changed, ensure the student's Username field matches
                            if (emailChanged && originalUsername != model.Email)
                            {
                                student.Username = model.Email;
                                _context.Students.Update(student);
                            }

                            _logger.LogInformation($"Successfully updated ApplicationUser {appUser.UserName} for student {student.StudentId_Number}");
                        }
                        else
                        {
                            // Log detailed information for debugging
                            _logger.LogWarning($"ApplicationUser not found for student {student.StudentId_Number}. " +
                                             $"Searched by: Username='{student.Username}', Email='{student.Email}', " +
                                             $"StudentId='{student.StudentId_Number}', FullName='{student.FullName}'");

                            // Don't fail the operation, but consider if you want to create a user account here
                            // or if this indicates a data integrity issue that should be addressed
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Log the action
                        _logger.LogInformation($"Admin {adminUser?.FullName} updated student {student.StudentId_Number} profile. " +
                                             $"Original: {originalData}. Email changed: {emailChanged}. " +
                                             $"ApplicationUser found: {appUser != null}");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error updating student profile for ID: {StudentId}", model.StudentId);
                        throw; // Re-throw to be handled by outer catch
                    }
                });

                return Json(new { success = true, message = "Student profile updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating student profile for ID: {StudentId}", model.StudentId);
                return Json(new { success = false, message = "An error occurred while updating the student profile" });
            }
        }

        // Helper method to extract email from original data string
        private string ExtractEmailFromOriginalData(string originalData)
        {
            try
            {
                // Parse the originalData string to extract the original email
                // Format: "Name: John Doe, Email: john@example.com, Phone: 123456789"
                var emailMatch = System.Text.RegularExpressions.Regex.Match(originalData, @"Email:\s*([^,]+)");
                if (emailMatch.Success)
                {
                    return emailMatch.Groups[1].Value.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting email from original data: {OriginalData}", originalData);
            }
            return null;
        }

        [HttpPost]
        public async Task<IActionResult> ValidatePassportPhoto(IFormFile passportPhoto)
        {
            try
            {
                var validationResult = await _photoValidationService.ValidatePassportPhotoAsync(passportPhoto);

                return Json(new
                {
                    success = validationResult.IsValid,
                    message = validationResult.Message,
                    errors = validationResult.Errors,
                    warnings = validationResult.Warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating passport photo in admin");
                return Json(new
                {
                    success = false,
                    message = "Error validating photo. Please try again.",
                    errors = new[] { "Validation service error" },
                    warnings = new string[0]
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeProgramme([FromBody] ProgrammeChangeModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                // Use the execution strategy to handle the transaction
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    // Begin transaction within the execution strategy
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        var student = await _context.Students
                            .Include(s => s.Programme)
                            .Include(s => s.School)
                            .FirstOrDefaultAsync(s => s.Id == model.StudentId);

                        if (student == null)
                        {
                            throw new InvalidOperationException("Student not found");
                        }

                        // Validate the new programme exists and relationships
                        var newProgramme = await _context.Programmes
                            .Include(p => p.Department)
                            .FirstOrDefaultAsync(p => p.Id == model.ProgrammeId);

                        if (newProgramme == null)
                        {
                            throw new InvalidOperationException("Selected programme not found");
                        }

                        var adminUser = await _userManager.GetUserAsync(User);
                        var originalData = $"Programme: {student.Programme?.Name}, School: {student.School?.Name}, Year: {student.StudentCurrentYear}";

                        // Update student academic details
                        student.ProgrammeId = model.ProgrammeId;
                        student.SchoolId = model.SchoolId;
                        student.ModeOfStudyId = model.ModeOfStudyId;
                        student.ProgrammeLevelId = model.ProgrammeLevelId;
                        student.AcademicYearId = model.AcademicYearId;
                        student.StudentCurrentYear = model.CurrentYear;
                        student.CurrentYearPeriodId = model.CurrentPeriodId;

                        // Update audit fields
                        student.UpdatedBy = adminUser?.FullName ?? "System";
                        student.UpdatedAt = DateTime.Now;

                        // If programme changed, might need to clear current registrations
                        if (student.RegistrationStatus == Status.Registered || student.RegistrationStatus == Status.Pending)
                        {
                            // Clear current academic year registrations
                            var currentRegistrations = await _context.StudentCourseRegistrations
                                .Where(scr => scr.StudentId == student.Id && scr.AcademicYearId == student.AcademicYearId)
                                .ToListAsync();

                            var currentExaminableCourses = await _context.StudentExaminableCourses
                                .Where(sec => sec.StudentId == student.Id && sec.AcademicYearId == student.AcademicYearId)
                                .ToListAsync();

                            _context.StudentCourseRegistrations.RemoveRange(currentRegistrations);
                            _context.StudentExaminableCourses.RemoveRange(currentExaminableCourses);

                            // Reset registration status
                            student.RegistrationStatus = Status.Unregistered;
                            student.IsRegistered = false;
                            student.RegistrationDate = null;
                        }

                        _context.Students.Update(student);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation($"Admin {adminUser?.FullName} changed programme for student {student.StudentId_Number}. Original: {originalData}, Reason: {model.ChangeReason}");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error changing programme for student ID: {StudentId}", model.StudentId);
                        throw; // Re-throw to be handled by outer catch
                    }
                });

                return Json(new { success = true, message = "Programme changed successfully. Student will need to re-register for courses." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing programme for student ID: {StudentId}", model.StudentId);
                return Json(new { success = false, message = "An error occurred while changing the programme" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRegistration([FromBody] RegistrationToggleModel model)
        {
            try
            {
                // Use execution strategy to handle the transaction properly
                var strategy = _context.Database.CreateExecutionStrategy();

                var result = await strategy.ExecuteAsync(async () =>
                {
                    // Begin transaction within the execution strategy
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        var student = await _context.Students
                            .Include(s => s.AcademicYear)
                            .FirstOrDefaultAsync(s => s.Id == model.StudentId);

                        if (student == null)
                        {
                            throw new InvalidOperationException("Student not found");
                        }

                        var adminUser = await _userManager.GetUserAsync(User);

                        if (model.EnableRegistration)
                        {
                            // Enable registration
                            student.RegistrationStatus = Status.Unregistered;
                            student.IsRegistered = false;
                            student.RegistrationDate = null;

                            // Update audit fields
                            student.UpdatedBy = adminUser?.FullName ?? "System";
                            student.UpdatedAt = DateTime.Now;

                            _logger.LogInformation($"Admin {adminUser?.FullName} enabled registration for student {student.StudentId_Number}. Reason: {model.Reason}");
                        }
                        else
                        {
                            // Disable registration and cleanup
                            var registrationsToDelete = await _context.StudentCourseRegistrations
                                .Where(scr => scr.StudentId == student.Id && scr.AcademicYearId == student.AcademicYearId)
                                .ToListAsync();

                            var examinableCoursesToDelete = await _context.StudentExaminableCourses
                                .Where(sec => sec.StudentId == student.Id && sec.AcademicYearId == student.AcademicYearId)
                                .ToListAsync();

                            var invoicesToDelete = await _context.StudentInvoices
                                .Where(si => si.StudentId == student.Id
                                          && si.AcademicYearId == student.AcademicYearId
                                          && !si.OnlinePayments.Any())   // no credit notes
                                .ToListAsync();

                            // Remove related invoice items first
                            foreach (var invoice in invoicesToDelete)
                            {
                                /*var invoiceItems = await _context.StudentInvoiceItems
                                    .Where(sii => sii.StudentInvoiceId == invoice.Id)
                                    .ToListAsync();
                                _context.StudentInvoiceItems.RemoveRange(invoiceItems);*/
                                invoice.DeletedAt = DateTime.Now;
                                _context.Update(invoice);
                            }

                            _context.StudentCourseRegistrations.RemoveRange(registrationsToDelete);
                            _context.StudentExaminableCourses.RemoveRange(examinableCoursesToDelete);
                            //_context.StudentInvoices.RemoveRange(invoicesToDelete);

                            student.RegistrationStatus = Status.Unregistered;
                            student.IsRegistered = false;
                            student.RegistrationDate = null;

                            // Update audit fields
                            student.UpdatedBy = adminUser?.FullName ?? "System";
                            student.UpdatedAt = DateTime.Now;

                            _logger.LogInformation($"Admin {adminUser?.FullName} disabled registration for student {student.StudentId_Number}. Deleted {registrationsToDelete.Count} course registrations, {examinableCoursesToDelete.Count} examinable courses, {invoicesToDelete.Count} invoices. Reason: {model.Reason}");
                        }

                        _context.Students.Update(student);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        var action = model.EnableRegistration ? "enabled" : "disabled";
                        return new { success = true, message = $"Registration {action} successfully" };
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error toggling registration for student ID: {StudentId}", model.StudentId);
                        throw; // Re-throw to be handled by outer catch
                    }
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling registration for student ID: {StudentId}", model.StudentId);
                return Json(new { success = false, message = "An error occurred while updating registration status" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int studentId)
        {
            try
            {
                Console.WriteLine($"Resetting password for student ID: {studentId}");
                var student = await _context.Students.FindAsync(studentId);
                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                var appUser = await _userManager.FindByNameAsync(student.Username);
                if (appUser == null)
                {
                    return Json(new { success = false, message = "User account not found" });
                }

                // Generate a temporary password
                var tempPassword = GenerateTemporaryPassword();

                // Reset password
                var token = await _userManager.GeneratePasswordResetTokenAsync(appUser);
                var result = await _userManager.ResetPasswordAsync(appUser, token, tempPassword);

                if (result.Succeeded)
                {
                    var adminUser = await _userManager.GetUserAsync(User);
                    _logger.LogInformation($"Admin {adminUser?.FullName} reset password for student {student.StudentId_Number}");

                    return Json(new
                    {
                        success = true,
                        message = "Password reset successfully",
                        temporaryPassword = tempPassword,
                        expiresAt = DateTime.Now.AddHours(24)
                    });
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Json(new { success = false, message = $"Failed to reset password: {errors}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for student ID: {StudentId}", studentId);
                return Json(new { success = false, message = "An error occurred while resetting the password" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRegistrationInfo(int studentId)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                var registeredCourses = await _context.StudentCourseRegistrations
                    .Where(scr => scr.StudentId == studentId && scr.AcademicYearId == student.AcademicYearId)
                    .Join(_context.Courses, scr => scr.CourseId, c => c.Id, (scr, c) => new RegisteredCourseInfo
                    {
                        CourseId = c.Id,
                        CourseCode = c.CourseCode,
                        CourseName = c.CourseName,
                        IsMandatory = c.IsMandatory,
                        IsExaminable = c.IsExaminable,
                        RegistrationDate = scr.RegistrationDate
                    })
                    .ToListAsync();

                var registrationInfo = new StudentRegistrationInfo
                {
                    IsCurrentlyRegistered = student.IsRegistered,
                    RegistrationStatus = student.RegistrationStatus.ToString(),
                    RegistrationDate = student.RegistrationDate,
                    RegisteredCoursesCount = registeredCourses.Count,
                    AcademicYear = student.AcademicYear?.YearValue ?? "N/A",
                    CurrentYearPeriodId = student.CurrentYearPeriodId ?? 0,
                    OutstandingFees = student.OutstandingFees,
                    RegisteredCourses = registeredCourses
                };

                return Json(new { success = true, data = registrationInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting registration info for student ID: {StudentId}", studentId);
                return Json(new { success = false, message = "Error retrieving registration information" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableProgrammes()
        {
            try
            {
                var programmes = await _context.Programmes
                    .Include(p => p.Department)
                        .ThenInclude(d => d.School)
                    .Select(p => new FilterOption
                    {
                        Id = p.Id,
                        Name = $"{p.Name} ({p.Department.School.Name})",
                        Value = p.Id.ToString()
                    })
                    .ToListAsync();

                return Json(new { success = true, data = programmes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available programmes");
                return Json(new { success = false, message = "Error loading programmes" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSchools()
        {
            try
            {
                var schools = await _context.Schools
                    .Select(s => new FilterOption
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Value = s.Id.ToString()
                    })
                    .ToListAsync();

                return Json(new { success = true, data = schools });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schools");
                return Json(new { success = false, message = "Error loading schools" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllModes()
        {
            try
            {
                var modes = await _context.ModesOfStudy
                    .Select(m => new FilterOption
                    {
                        Id = m.ModeId,
                        Name = m.ModeName,
                        Value = m.ModeId.ToString()
                    })
                    .ToListAsync();

                return Json(new { success = true, data = modes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting modes of study");
                return Json(new { success = false, message = "Error loading modes of study" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammeLevels()
        {
            try
            {
                var levels = await _context.ProgramLevels
                    .Where(pl => pl.IsActive)
                    .OrderBy(pl => pl.Rank)
                    .Select(pl => new FilterOption
                    {
                        Id = pl.Id,
                        Name = pl.Name,
                        Value = pl.Id.ToString()
                    })
                    .ToListAsync();

                return Json(new { success = true, data = levels });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting programme levels");
                return Json(new { success = false, message = "Error loading programme levels" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammesBySchool(int schoolId)
        {
            try
            {
                var programmes = await _context.Programmes
                    .Include(p => p.Department)
                    .Where(p => p.Department.SchoolId == schoolId)
                    .Select(p => new FilterOption
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Value = p.Id.ToString()
                    })
                    .ToListAsync();

                return Json(new { success = true, data = programmes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting programmes for school ID: {SchoolId}", schoolId);
                return Json(new { success = false, message = "Error loading programmes" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAcademicYears()
        {
            try
            {
                var years = await _context.AcademicYears
                    .OrderByDescending(ay => ay.YearValue)
                    .Select(ay => new FilterOption
                    {
                        Id = ay.YearId,
                        Name = ay.YearValue,
                        Value = ay.YearId.ToString()
                    })
                    .ToListAsync();

                return Json(new { success = true, data = years });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting academic years");
                return Json(new { success = false, message = "Error loading academic years" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStudentAvailableCourses(int studentId)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.AcademicYear)
                    .Include(s => s.CurrentYearPeriod)
                        .ThenInclude(cyp => cyp.AcademicPeriod)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                // Get available courses using similar logic to CourseRegistrationController
                var availableCoursesResult = await GetAvailableCoursesForStudent(student);
                var requirements = await GetProgrammeRequirementsForStudent(student, availableCoursesResult.programmeUsed);

                var result = new
                {
                    success = true,
                    data = new
                    {
                        courses = availableCoursesResult.courses,
                        requirements = new
                        {
                            totalRequired = requirements.TotalRequiredCourses,
                            minimumElectives = requirements.MinimumElectives,
                            maximumElectives = requirements.MaximumElectives,
                            carryoverCount = requirements.CarryoverCoursesCount
                        },
                        studentInfo = new
                        {
                            currentYear = student.StudentCurrentYear,
                            currentYearPeriodId = student.CurrentYearPeriodId,
                            currentYearPeriodLabel = student.CurrentYearPeriod?.FullLabel,
                            programmeName = student.Programme?.Name,
                            isSemesterBased = student.Programme?.IsSemesterBased ?? false,
                            academicYear = student.AcademicYear?.YearValue
                        },
                        programmeUsed = availableCoursesResult.programmeUsed?.Name
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available courses for student ID: {StudentId}", studentId);
                return Json(new { success = false, message = "Error retrieving available courses" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SearchCoursesToAdd(int studentId, string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
                {
                    return Json(new { success = false, message = "Please enter at least 2 characters to search" });
                }

                var student = await _context.Students
                    .Include(s => s.Programme)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                // Get courses that match search criteria from ALL programmes with programme info
                var searchResults = await _context.Courses
                    .Include(c => c.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Where(c => c.CourseCode.Contains(searchTerm) || c.CourseName.Contains(searchTerm))
                    .Select(c => new
                    {
                        id = c.Id,
                        courseCode = c.CourseCode,
                        courseName = c.CourseName,
                        courseDescription = c.CourseDescription,
                        yearTaken = c.YearTaken,
                        periodTaken = c.PeriodTakenId,
                        periodTakenLabel = c.PeriodTakenLabel,
                        isMandatory = c.IsMandatory,
                        isExaminable = c.IsExaminable,
                        programmeId = c.ProgrammeID,
                        programmeName = c.Programme.Name,
                        schoolName = c.Programme.Department.School.Name,
                        isFromStudentProgramme = c.ProgrammeID == student.ProgrammeId ||
                                                (student.Programme.AssociatedNQProgrammeId.HasValue &&
                                                 c.ProgrammeID == student.Programme.AssociatedNQProgrammeId.Value)
                    })
                    .OrderBy(c => c.isFromStudentProgramme ? 0 : 1) // Prioritize student's programme courses
                    .ThenBy(c => c.courseCode)
                    .Take(50)
                    .ToListAsync();

                return Json(new { success = true, data = searchResults });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching courses for student ID: {StudentId}", studentId);
                return Json(new { success = false, message = "Error searching courses" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteStudentRegistration([FromBody] AdminRegistrationModel model)
        {
            try
            {
                // Use execution strategy to handle the transaction properly
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    // Begin transaction within the execution strategy
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        var student = await _context.Students
                            .Include(s => s.Programme)
                            .Include(s => s.AcademicYear)
                            .FirstOrDefaultAsync(s => s.Id == model.StudentId);

                        if (student == null)
                        {
                            throw new InvalidOperationException("Student not found");
                        }

                        if (student.AcademicYear == null)
                        {
                            throw new InvalidOperationException("Student's academic year is not set");
                        }

                        // Clear existing registrations for this academic year if any
                        var existingRegistrations = await _context.StudentCourseRegistrations
                            .Where(scr => scr.StudentId == student.Id && scr.AcademicYearId == student.AcademicYearId)
                            .ToListAsync();

                        var existingExaminableCourses = await _context.StudentExaminableCourses
                            .Where(sec => sec.StudentId == student.Id && sec.AcademicYearId == student.AcademicYearId)
                            .ToListAsync();

                        _context.StudentCourseRegistrations.RemoveRange(existingRegistrations);
                        _context.StudentExaminableCourses.RemoveRange(existingExaminableCourses);

                        // Get full course details from database
                        var dbCourses = await _context.Courses
                            .Include(c => c.CourseAssessments)
                                .ThenInclude(ca => ca.Assessment)
                            .Where(c => model.SelectedCourseIds.Contains(c.Id))
                            .ToListAsync();

                        // Get carryover courses
                        var carryoverCourseIds = await _context.StudentCarryoverCourses
                            .Where(scc => scc.StudentId == student.Id && scc.IsActive)
                            .Select(scc => scc.CourseId)
                            .ToListAsync();

                        // Create registrations for all selected courses
                        foreach (var course in dbCourses)
                        {
                            var isCarryoverCourse = carryoverCourseIds.Contains(course.Id);

                            // Create registration record
                            var registration = new StudentCourseRegistration
                            {
                                StudentId = student.Id,
                                CourseId = course.Id,
                                AcademicYearId = student.AcademicYearId,
                                YearPeriodId = student.CurrentYearPeriodId ?? 1,
                                RegistrationDate = DateTime.Now
                            };
                            _context.StudentCourseRegistrations.Add(registration);
                            await _context.SaveChangesAsync();

                            // For examinable courses, create examinable course record
                            if (course.IsExaminable)
                            {
                                var assessmentJson = new Dictionary<int, object>();

                                if (course.CourseAssessments != null && course.CourseAssessments.Any())
                                {
                                    foreach (var assessment in course.CourseAssessments)
                                    {
                                        if (assessment?.Assessment != null)
                                        {
                                            assessmentJson[assessment.AssessmentId] = new
                                            {
                                                assessment_name = assessment.Assessment.Name,
                                                score = "-"
                                            };
                                        }
                                    }
                                }

                                var examinableCourse = new StudentExaminableCourse
                                {
                                    StudentId = student.Id,
                                    CourseId = course.Id,
                                    AcademicYearId = student.AcademicYearId,
                                    YearPeriodId = student.CurrentYearPeriodId ?? 1,
                                    RegistrationDate = DateTime.Now,
                                    AssessmentScores = assessmentJson.Any()
                                        ? System.Text.Json.JsonSerializer.Serialize(assessmentJson)
                                        : "{}",
                                    Status = Status.Unpublished
                                };
                                _context.StudentExaminableCourses.Add(examinableCourse);
                            }

                            // Handle carryover courses
                            if (isCarryoverCourse)
                            {
                                var carryoverRecord = await _context.StudentCarryoverCourses
                                    .FirstOrDefaultAsync(scc => scc.StudentId == student.Id &&
                                                               scc.CourseId == course.Id &&
                                                               scc.IsActive);

                                if (carryoverRecord != null)
                                {
                                    carryoverRecord.IsActive = false;
                                    carryoverRecord.Notes += $" | Admin registered on {DateTime.Now:yyyy-MM-dd}";
                                }
                            }
                        }

                        // Update student registration status
                        student.RegistrationStatus = Status.Registered;
                        student.IsRegistered = true;
                        student.RegistrationDate = DateTime.Now;

                        // Update audit fields
                        var adminUser = await _userManager.GetUserAsync(User);
                        student.UpdatedBy = adminUser?.FullName ?? "System";
                        student.UpdatedAt = DateTime.Now;

                        _context.Students.Update(student);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation($"Admin {adminUser?.FullName} registered {model.SelectedCourseIds.Count} courses for student {student.StudentId_Number}. Auto-registered by system.");

                        // Generate invoice with proper error handling
                        var periodText = student.Programme?.IsSemesterBased == true ?
                            $" for period {student.CurrentPeriodLabel}" : "";

                        /*var carryoverText = carryoverCourses.Any() ?
                            $" (including {carryoverCourses.Count} carryover course{(carryoverCourses.Count != 1 ? "s" : "")})" : "";*/

                        try
                        {
                            var invoiceResult = await _studentInvoiceService.GenerateStudentInvoiceAsync(student.Id);

                            if (!invoiceResult.Success)
                            {
                                // Log the error
                                Console.WriteLine($"[WARNING] {DateTime.Now} - Invoice generation failed for student {student.StudentId_Number}: {invoiceResult.Message}");
                            }
                            else
                            {
                                Console.WriteLine($"[SUCCESS] {DateTime.Now} - Successfully generated invoice for student {student.StudentId_Number}");
                            }
                        }
                        catch (Exception invoiceEx)
                        {
                            Console.WriteLine($"[ERROR] {DateTime.Now} - Exception generating invoice for student {student.StudentId_Number}: {invoiceEx.Message}");
                            Console.WriteLine($"[ERROR] Stack trace: {invoiceEx.StackTrace}");
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw; // Re-throw to be handled by outer catch
                    }
                });

                return Json(new
                {
                    success = true,
                    message = $"Student successfully registered for {model.SelectedCourseIds.Count} course{(model.SelectedCourseIds.Count != 1 ? "s" : "")}."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing student registration for student ID: {StudentId}", model.StudentId);
                return Json(new { success = false, message = "An error occurred during registration" });
            }
        }

        #region Helper Methods for Course Registration

        private async Task<(List<AdminCourseViewModel> courses, Programme programmeUsed)> GetAvailableCoursesForStudent(Student student)
        {
            // Get carryover courses first
            var carryoverCourses = await GetCarryoverCoursesForAdmin(student);
            var carryoverCourseIds = carryoverCourses.Select(c => c.Id).ToList();

            // Get regular courses from student's programme
            var regularCourses = await GetRegularProgrammeCoursesForAdmin(student, carryoverCourseIds);
            var programmeUsed = student.Programme;

            // If no regular courses found, try NQ programme fallback
            if (!regularCourses.Any() && student.Programme?.AssociatedNQProgrammeId.HasValue == true)
            {
                var nqCourses = await GetNQProgrammeCoursesForAdmin(student, carryoverCourseIds);
                if (nqCourses.courses.Any())
                {
                    regularCourses = nqCourses.courses;
                    programmeUsed = nqCourses.nqProgramme;
                }
            }

            // Combine all courses
            var allCourses = new List<AdminCourseViewModel>();
            allCourses.AddRange(carryoverCourses);
            allCourses.AddRange(regularCourses);

            return (allCourses, programmeUsed);
        }

        private async Task<List<AdminCourseViewModel>> GetCarryoverCoursesForAdmin(Student student)
        {
            var carryoverCourses = await _context.StudentCarryoverCourses
                .Where(scc => scc.StudentId == student.Id && scc.IsActive)
                .Include(scc => scc.Course)
                .Include(scc => scc.OriginalAcademicYear)
                .Select(scc => new AdminCourseViewModel
                {
                    Id = scc.Course.Id,
                    CourseCode = scc.Course.CourseCode,
                    CourseName = scc.Course.CourseName,
                    CourseDescription = $"{scc.Course.CourseDescription} (Carryover from {scc.OriginalAcademicYear.YearValue})",
                    IsMandatory = true,
                    IsExaminable = scc.Course.IsExaminable,
                    IsSelected = true,
                    IsCarryover = true,
                    YearTaken = scc.Course.YearTaken,
                    PeriodTakenId = scc.Course.PeriodTakenId,
                    PeriodTakenLabel = scc.Course.PeriodTakenLabel,
                    CarryoverReason = scc.Reason
                })
                .ToListAsync();

            return carryoverCourses;
        }

        private async Task<List<AdminCourseViewModel>> GetRegularProgrammeCoursesForAdmin(Student student, List<int> carryoverCourseIds)
        {
            var currentPeriodId = student.CurrentYearPeriod.AcademicPeriod.Id;

            var courseQuery = _context.Courses
                .Where(c => c.ProgrammeID == student.ProgrammeId &&
                           c.YearTaken == student.StudentCurrentYear &&
                           (c.PeriodTakenId == currentPeriodId ||
                            c.PeriodTaken.AcademicType == AcademicType.Annual) &&
                           !carryoverCourseIds.Contains(c.Id));


            var courses = await courseQuery
                .Select(c => new AdminCourseViewModel
                {
                    Id = c.Id,
                    CourseCode = c.CourseCode,
                    CourseName = c.CourseName,
                    CourseDescription = c.CourseDescription,
                    IsMandatory = c.IsMandatory,
                    IsExaminable = c.IsExaminable,
                    IsSelected = c.IsMandatory,
                    IsCarryover = false,
                    YearTaken = c.YearTaken,
                    PeriodTakenId = c.PeriodTakenId,
                    PeriodTakenLabel = c.PeriodTakenLabel,
                })
                .ToListAsync();

            return courses;
        }

        private async Task<(List<AdminCourseViewModel> courses, Programme nqProgramme)> GetNQProgrammeCoursesForAdmin(Student student, List<int> carryoverCourseIds)
        {
            var nqProgramme = await _context.Programmes
                .FirstOrDefaultAsync(p => p.Id == student.Programme.AssociatedNQProgrammeId.Value);

            if (nqProgramme == null)
            {
                return (new List<AdminCourseViewModel>(), null);
            }

            var courseQuery = _context.Courses
                .Where(c => c.ProgrammeID == nqProgramme.Id &&
                           c.YearTaken == student.StudentCurrentYear &&
                           !carryoverCourseIds.Contains(c.Id));

            if (student.Programme?.IsSemesterBased == true)
            {
                var currentPeriodId = student.CurrentYearPeriod.AcademicPeriod.Id;
                courseQuery = courseQuery.Where(c =>
                    c.PeriodTakenId == currentPeriodId ||
                    c.PeriodTaken.AcademicType == AcademicType.Annual);
            }

            var courses = await courseQuery
                .Select(c => new AdminCourseViewModel
                {
                    Id = c.Id,
                    CourseCode = c.CourseCode,
                    CourseName = c.CourseName,
                    CourseDescription = $"{c.CourseDescription} (From NQ: {nqProgramme.Name})",
                    IsMandatory = c.IsMandatory,
                    IsExaminable = c.IsExaminable,
                    IsSelected = c.IsMandatory,
                    IsCarryover = false,
                    YearTaken = c.YearTaken,
                    PeriodTakenId = c.PeriodTakenId,
                    PeriodTakenLabel = c.PeriodTakenLabel
                })
                .ToListAsync();

            return (courses, nqProgramme);
        }

        private async Task<CourseRequirementsForAdmin> GetProgrammeRequirementsForStudent(Student student, Programme programmeToUse = null)
        {
            var programme = programmeToUse ?? student.Programme;

            if (programme == null)
            {
                return new CourseRequirementsForAdmin { MinimumElectives = 0, MaximumElectives = 0, TotalRequiredCourses = 0 };
            }

            var carryoverCoursesCount = await _context.StudentCarryoverCourses
                .Where(scc => scc.StudentId == student.Id && scc.IsActive)
                .CountAsync();

            int totalRequiredCourses = 0;
            int minimumElectives = 0;
            int maximumElectives = 0;

            if (!string.IsNullOrEmpty(programme.YearlyRequirements))
            {
                try
                {
                    var yearlyRequirements = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, YearRequirement>>(
                        programme.YearlyRequirements);

                    string yearKey = $"Year{student.StudentCurrentYear}";

                    if (yearlyRequirements != null && yearlyRequirements.ContainsKey(yearKey))
                    {
                        var requirement = yearlyRequirements[yearKey];

                        if (programme.IsSemesterBased)
                        {
                            var currentPeriod = student.CurrentYearPeriod?.AcademicPeriod?.Id ?? 1;
                            if (currentPeriod == 1 && requirement.Semester1.HasValue)
                            {
                                totalRequiredCourses = requirement.Semester1.Value;
                            }
                            else if (currentPeriod == 2 && requirement.Semester2.HasValue)
                            {
                                totalRequiredCourses = requirement.Semester2.Value;
                            }
                            else
                            {
                                totalRequiredCourses = requirement.TotalRequired / 2;
                            }
                        }
                        else
                        {
                            totalRequiredCourses = requirement.TotalRequired;
                        }

                        totalRequiredCourses += carryoverCoursesCount;

                        var mandatoryCoursesQuery = _context.Courses
                            .Where(c => c.ProgrammeID == programme.Id &&
                                       c.YearTaken == student.StudentCurrentYear &&
                                       c.IsMandatory);

                        var carryoverCourseIds = await _context.StudentCarryoverCourses
                            .Where(scc => scc.StudentId == student.Id && scc.IsActive)
                            .Select(scc => scc.CourseId)
                            .ToListAsync();

                        mandatoryCoursesQuery = mandatoryCoursesQuery.Where(c => !carryoverCourseIds.Contains(c.Id));

                        if (programme.IsSemesterBased)
                        {
                            mandatoryCoursesQuery = mandatoryCoursesQuery.Where(c => c.PeriodTakenId == student.CurrentYearPeriod.AcademicPeriod.Id);
                        }

                        var mandatoryCount = await mandatoryCoursesQuery.CountAsync();
                        var totalMandatory = mandatoryCount + carryoverCoursesCount;

                        minimumElectives = Math.Max(0, totalRequiredCourses - totalMandatory);

                        var electiveCoursesQuery = _context.Courses
                            .Where(c => c.ProgrammeID == programme.Id &&
                                       c.YearTaken == student.StudentCurrentYear &&
                                       !c.IsMandatory &&
                                       !carryoverCourseIds.Contains(c.Id));

                        if (programme.IsSemesterBased)
                        {
                            electiveCoursesQuery = electiveCoursesQuery.Where(c => c.PeriodTakenId == student.CurrentYearPeriod.AcademicPeriod.Id);
                        }

                        var availableElectives = await electiveCoursesQuery.CountAsync();
                        maximumElectives = totalRequiredCourses > 0
                            ? Math.Min(availableElectives, totalRequiredCourses - totalMandatory)
                            : availableElectives;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing yearly requirements for programme {ProgrammeName}", programme.Name);
                }
            }

            return new CourseRequirementsForAdmin
            {
                TotalRequiredCourses = totalRequiredCourses,
                MinimumElectives = minimumElectives,
                MaximumElectives = maximumElectives,
                CarryoverCoursesCount = carryoverCoursesCount
            };
        }

        #endregion


        #region Helper Methods

        private string GenerateTemporaryPassword()
        {
            // Character sets for password generation
            const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lowercase = "abcdefghijkmnpqrstuvwxyz";
            const string numbers = "123456789";
            const string specialChars = "@#$%&*!";

            var random = new Random();
            var password = new StringBuilder();

            // Ensure at least one character from each required set
            password.Append(uppercase[random.Next(uppercase.Length)]);     // 1 uppercase
            password.Append(lowercase[random.Next(lowercase.Length)]);     // 1 lowercase
            password.Append(numbers[random.Next(numbers.Length)]);         // 1 number
            password.Append(specialChars[random.Next(specialChars.Length)]); // 1 special char

            // Fill remaining 4-6 characters randomly from all sets
            const string allChars = uppercase + lowercase + numbers + specialChars;
            int remainingLength = random.Next(4, 7); // Generate 8-10 character password total

            for (int i = 0; i < remainingLength; i++)
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Shuffle the password to randomize character positions
            var passwordArray = password.ToString().ToCharArray();
            for (int i = passwordArray.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (passwordArray[i], passwordArray[j]) = (passwordArray[j], passwordArray[i]);
            }

            return new string(passwordArray);
        }

        #endregion

        [HttpGet]
        [Route("StudentAdministration/PreviewFile")]
        public IActionResult PreviewFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return BadRequest("File path is required");
            }

            try
            {
                string fullPath;

                // Handle both old and new photo storage paths
                if (Path.IsPathRooted(filePath))
                {
                    // Absolute path (from application process or updated admin process)
                    fullPath = filePath;
                }
                else
                {
                    // Relative path (from old admin storage)
                    fullPath = Path.Combine(_webHostEnvironment.WebRootPath, filePath.TrimStart('/'));

                    // If not found in wwwroot, try the application uploads directory
                    if (!System.IO.File.Exists(fullPath))
                    {
                        fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath.TrimStart('/'));
                    }
                }

                // Security check: Validate and sanitize the file path
                var resolvedPath = Path.GetFullPath(fullPath);

                // Log the path for debugging
                _logger.LogInformation($"Attempting to access student photo at: {resolvedPath}");

                // Ensure the file exists
                if (!System.IO.File.Exists(resolvedPath))
                {
                    _logger.LogWarning($"Student photo not found at: {resolvedPath}");
                    return NotFound($"File not found");
                }

                // Determine content type based on file extension
                string contentType;
                var extension = Path.GetExtension(resolvedPath).ToLower();

                switch (extension)
                {
                    case ".pdf":
                        contentType = "application/pdf";
                        break;
                    case ".jpg":
                    case ".jpeg":
                        contentType = "image/jpeg";
                        break;
                    case ".png":
                        contentType = "image/png";
                        break;
                    case ".gif":
                        contentType = "image/gif";
                        break;
                    case ".webp":
                        contentType = "image/webp";
                        break;
                    default:
                        contentType = "application/octet-stream";
                        break;
                }

                // Return file with content type
                return PhysicalFile(resolvedPath, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error accessing student photo file: {filePath}");
                return StatusCode(500, $"Error accessing file");
            }
        }
    }
}