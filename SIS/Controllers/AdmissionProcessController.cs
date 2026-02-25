using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using SIS.Notifications;
using SIS.Services.FilePreview;
using SIS.Services.StudentApplication;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using SIS.Services.Accounting;
using SIS.Services.Emails;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin,Registrar, Dean, ProgramCoordinator, VC, DVC")]
    public class AdmissionProcessController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IApplicantService _applicantService;
        private readonly EmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileService _fileService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IAccountingService _accountingService;
        private readonly ILogger<AdmissionProcessController> _logger;
        private readonly IBackgroundEmailService _backgroundEmailService;

        public AdmissionProcessController(ApplicationDbContext context, IApplicantService applicantService, EmailService emailService,
        IFileService fileService, UserManager<ApplicationUser> userManager, IWebHostEnvironment webHostEnvironment, IAccountingService accountingService, ILogger<AdmissionProcessController> logger,
            IBackgroundEmailService backgroundEmailService)
        {
            _context = context;
            _applicantService = applicantService;
            _emailService = emailService;
            _userManager = userManager;
            _fileService = fileService;
            _webHostEnvironment = webHostEnvironment;
            _accountingService = accountingService;
            _logger = logger;
            _backgroundEmailService = backgroundEmailService;
        }

        public async Task<IActionResult> PendingAdmission()
        {
            // Get the current user
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Check if user is in each role and prepare appropriate query filters
            bool isAdmin = User.IsInRole("Admin");
            bool isDean = User.IsInRole("Dean");
            bool isRegistrar = User.IsInRole("Registrar");
            bool isProgramCoordinator = User.IsInRole("ProgramCoordinator");
            bool isVC = User.IsInRole("VC");
            bool isDVC = User.IsInRole("DVC");

            // Query to get applicants - will be filtered based on role
            IQueryable<Applicant> applicantsQuery = _context.Applicants
                .Where(a => a.PaymentStatus == Status.Paid && a.Status == Status.Pending)
                .Include(a => a.School)
                .Include(a => a.Programme)
                .Include(a => a.AcademicYear)
                .Include(a => a.ModeOfStudy)
                .Include(a => a.ProgrammeLevel)
                .Include(a => a.SubjectGrades)
                    .ThenInclude(sg => sg.Grade)
                .Include(a => a.SubjectGrades)
                    .ThenInclude(sg => sg.Subject)
                .Where(a => a.IsForeigner == true || a.SubjectGrades.Any());

            // Apply role-specific filters
            if (!isAdmin && !isRegistrar && !isVC && !isDVC)
            {
                if (isDean)
                {
                    var userSchools = await _context.Schools
                        .Where(s => s.DeanId == currentUser.Id || s.AssistantDeanId == currentUser.Id)
                        .Select(s => s.Id)
                        .ToListAsync();

                    if (!userSchools.Any())
                    {
                        return NotFound();
                    }

                    applicantsQuery = applicantsQuery.Where(a => userSchools.Contains(a.SchoolId));
                }
                else if (isProgramCoordinator)
                {
                    var userProgrammes = await _context.Programmes
                        .Where(p => p.CoordinatorId == currentUser.Id)
                        .Select(p => p.Id)
                        .ToListAsync();

                    if (!userProgrammes.Any())
                    {
                        return NotFound();
                    }

                    applicantsQuery = applicantsQuery.Where(a => userProgrammes.Contains(a.ProgrammeId));
                }
                else
                {
                    return NotFound();
                }
            }

            var applicants = await applicantsQuery.ToListAsync();

            // Calculate statistics
            ViewBag.TotalApplicants = applicants.Count;
            ViewBag.QualifiedApplicants = applicants.Count(a => a.IsQualified == true);
            ViewBag.UnqualifiedApplicants = applicants.Count(a => a.IsQualified == false);
            ViewBag.ForeignApplicants = applicants.Count(a => a.IsForeigner == true);

            var schoolStats = applicants
                .GroupBy(a => a.School.Name)
                .Select(g => new
                {
                    SchoolName = g.Key,
                    Count = g.Count(),
                    Qualified = g.Count(a => a.IsQualified == true)
                })
                .ToList();
            ViewBag.SchoolStats = schoolStats;

            var programmeStats = applicants
                .GroupBy(a => a.Programme.Name)
                .Select(g => new
                {
                    ProgrammeName = g.Key,
                    Count = g.Count(),
                    Qualified = g.Count(a => a.IsQualified == true)
                })
                .ToList();
            ViewBag.ProgrammeStats = programmeStats;

            var serializedApplicants = applicants.Select(a => new
            {
                a.ApplicantId,
                a.ReferenceNumber,
                a.FullName,
                a.DateOfBirth,
                a.Gender,
                a.Phone,
                a.Email,
                a.NrcOrPassport,
                a.MaritalStatus,
                a.Nationality,
                a.Religion,
                a.IsForeigner,
                a.AddressLine1,
                a.AddressLine2,
                a.City,
                a.State,
                a.PostalCode,
                a.Country,
                a.NextOfKinName,
                a.NextOfKinRelation,
                a.NextOfKinPhone,
                a.NextOfKinEmail,
                a.NextOfKinAddress,
                a.PrimarySchoolName,
                a.PrimarySchoolAddress,
                a.PrimarySchoolPeriod,
                a.SecondarySchoolName,
                a.SecondarySchoolAddress,
                a.SecondarySchoolPeriod,
                a.FormerSchoolName,
                a.FormerSchoolAddress,
                a.FormerSchoolLevel,
                a.YearOfCompletion,
                a.ResultsAttachmentCopy,
                a.NrcOrPassportCopy,
                a.StudyPermitCopy,
                a.PassportPhotoPath,
                School = new { a.School.Id, a.School.Name },
                Programme = new
                {
                    a.Programme.Id,
                    a.Programme.Name,
                    a.Programme.MinimumPointsTop5Subjects
                },
                AcademicYear = new { a.AcademicYear.YearId, a.AcademicYear.YearValue },
                ModeOfStudy = new { a.ModeOfStudy.ModeId, a.ModeOfStudy.ModeName },
                ProgrammeLevel = new { a.ProgrammeLevel.Id, a.ProgrammeLevel.Name },
                a.IsQualified,
                SubjectGrades = a.SubjectGrades.Select(sg => new
                {
                    Subject = new { sg.Subject.SubjectId, sg.Subject.SubjectName },
                    Grade = new { sg.Grade.GradeId, sg.Grade.GradeValue, sg.Grade.GradePoint }
                }).ToList()
            }).ToList();

            ViewBag.ApplicantsJson = JsonSerializer.Serialize(serializedApplicants, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var qualifiedApplicants = applicants
                .Where(a => a.IsQualified == true)
                .Select(a => a.ApplicantId)
                .ToList();
            ViewBag.QualifiedApplicantsJson = JsonSerializer.Serialize(qualifiedApplicants);

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsDean = isDean;
            ViewBag.IsRegistrar = isRegistrar;
            ViewBag.IsProgramCoordinator = isProgramCoordinator;

            return View("/Views/AdmissionProcess/PendingAdmission.cshtml", applicants);
        }

        [HttpPost]
        public IActionResult CheckRequirements(int applicantId)
        {
            var applicant = _context.Applicants
                .Include(a => a.SubjectGrades)
                    .ThenInclude(sg => sg.Grade)
                .Include(a => a.Programme)
                .FirstOrDefault(a => a.ApplicantId == applicantId);

            if (applicant == null)
            {
                return Json(new { success = false, message = "Applicant not found." });
            }

            var programmeLevel = _context.ProgramLevels.FirstOrDefault(pl => pl.Id == applicant.ProgrammeLevelId);

            if (programmeLevel == null)
            {
                return Json(new { success = false, message = "Programme level not found." });
            }

            bool meetsRequirements = false;
            bool canBeWaitlisted = false;
            string message;
            bool _success = false;

            if (applicant.IsForeigner)
            {
                bool hasRequiredAttachments = !string.IsNullOrEmpty(applicant.NrcOrPassportCopy) &&
                                            !string.IsNullOrEmpty(applicant.ResultsAttachmentCopy) &&
                                            !string.IsNullOrEmpty(applicant.StudyPermitCopy);

                meetsRequirements = hasRequiredAttachments;

                if (meetsRequirements)
                {
                    message = "Foreign student requirements met. All required documents uploaded. Manual transcript review required.";
                    _success = true;
                }
                else
                {
                    _success = false;
                    var missingDocs = new List<string>();
                    if (string.IsNullOrEmpty(applicant.NrcOrPassportCopy)) missingDocs.Add("NRC/Passport copy");
                    if (string.IsNullOrEmpty(applicant.ResultsAttachmentCopy)) missingDocs.Add("Academic transcript");
                    if (string.IsNullOrEmpty(applicant.StudyPermitCopy)) missingDocs.Add("Study permit");

                    message = $"Missing required documents: {string.Join(", ", missingDocs)}";
                }

                canBeWaitlisted = false;
            }
            else
            {
                if (programmeLevel.Name == "Diploma" || programmeLevel.Name == "Bachelors")
                {
                    bool hasEnoughSubjects = applicant.SubjectGrades.Count >= 5;

                    var top5GradePoints = applicant.SubjectGrades
                        .OrderByDescending(sg => sg.Grade.GradePoint)
                        .Take(5)
                        .Sum(sg => sg.Grade.GradePoint);

                    bool enoughCredits = top5GradePoints >= 5 && top5GradePoints <= applicant.Programme.MinimumPointsTop5Subjects;

                    bool hasAttachments = !string.IsNullOrEmpty(applicant.NrcOrPassportCopy) &&
                                        !string.IsNullOrEmpty(applicant.ResultsAttachmentCopy);

                    meetsRequirements = hasEnoughSubjects && enoughCredits && hasAttachments;
                    canBeWaitlisted = !meetsRequirements && hasEnoughSubjects && enoughCredits;

                    if (meetsRequirements)
                    {
                        message = "Requirements met. You can admit this applicant.";
                        _success = true;
                    }
                    else if (!meetsRequirements && canBeWaitlisted)
                    {
                        _success = false;
                        message = "Requirements not fully met, but applicant can be waitlisted.";
                    }
                    else
                    {
                        _success = false;
                        message = "Requirements not met. Reject this applicant.";
                    }
                }
                else
                {
                    meetsRequirements = !string.IsNullOrEmpty(applicant.NrcOrPassportCopy) &&
                                      !string.IsNullOrEmpty(applicant.ResultsAttachmentCopy);

                    message = meetsRequirements
                        ? "Requirements met. You can admit this applicant."
                        : "Requirements not met. Reject this applicant.";

                    _success = meetsRequirements;
                }
            }

            return Json(new
            {
                success = _success,
                MeetsRequirements = meetsRequirements,
                CanBeWaitlisted = canBeWaitlisted,
                IsForeigner = applicant.IsForeigner,
                message
            });
        }

        [HttpPost]
        public IActionResult GetSchools()
        {
            var schools = _context.Schools.ToList();
            return Json(new { success = true, schools });
        }

        [HttpGet]
        public async Task<IActionResult> GetProgramsForSchool(int schoolId)
        {
            if (schoolId <= 0)
            {
                return BadRequest("Invalid school ID.");
            }

            var programs = await _applicantService.GetProgrammesAsync(schoolId);

            if (programs == null || !programs.Any())
            {
                return NotFound("No programs found for the selected school.");
            }
            return Json(new { success = true, programs });
        }

        [HttpPost]
        public async Task<IActionResult> ProcessApplicantAction(int applicantId, string action, string reason = null, int schoolId = 0, int programmeId = 0)
        {
            var applicant = await _context.Applicants
                .Include(a => a.SubjectGrades)
                .FirstOrDefaultAsync(a => a.ApplicantId == applicantId);

            if (applicant == null)
            {
                return Json(new { success = false, message = "Applicant not found." });
            }

            switch (action.ToLower())
            {
                case "admit":
                    if (schoolId != 0 && programmeId != 0)
                    {
                        applicant.SchoolId = schoolId;
                        applicant.ProgrammeId = programmeId;
                        applicant.UpdatedBy = User.Identity.Name;
                        applicant.UpdatedAt = DateTime.Now;

                        await _context.SaveChangesAsync();

                        return await Admit(applicantId);
                    }
                    return await Admit(applicantId);

                case "reject":
                    if (string.IsNullOrEmpty(reason))
                    {
                        return Json(new { success = false, message = "Rejection reason is required." });
                    }
                    return await Reject(applicantId, reason);

                case "waitlist":
                    return await Waitlist(applicantId);

                default:
                    return Json(new { success = false, message = "Invalid action." });
            }
        }

        public async Task<IActionResult> Waitlist(int applicantId)
        {
            var applicant = await _context.Applicants.FindAsync(applicantId);
            if (applicant == null)
            {
                return Json(new { success = false, message = "Applicant not found." });
            }

            applicant.Status = Status.WaitListed;
            applicant.UpdatedBy = User.Identity.Name;
            applicant.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            try
            {
                var applicantWithDetails = await _context.Applicants
                    .Include(a => a.Programme)
                    .Include(a => a.School)
                    .Include(a => a.AcademicYear)
                    .FirstOrDefaultAsync(a => a.ApplicantId == applicantId);

                if (applicantWithDetails != null)
                {
                    var academicYearStart = applicantWithDetails.AcademicYear?.StartDate ?? new DateTime(2025, 3, 15);

                    _backgroundEmailService.QueueWaitlistEmail(
                        applicantName: applicantWithDetails.FullName,
                        applicantEmail: applicantWithDetails.Email,
                        programmeName: applicantWithDetails.Programme?.Name ?? "Unknown Programme",
                        schoolName: applicantWithDetails.School?.Name ?? "Unknown School",
                        academicYearStart: academicYearStart
                    );
                    _logger.LogInformation($"Queued waitlist email for applicant {applicantId}");
                }
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, $"Error queuing waitlist email for applicant {applicantId}");
            }

            return Json(new { success = true, message = "Applicant successfully waitlisted." });
        }

        [HttpPost]
        public async Task<IActionResult> Admit(int applicantId)
        {
            try
            {
                // Step 1: Fetch and validate the applicant BEFORE the transaction
                var applicant = await _context.Applicants
                    .Include(a => a.SubjectGrades)
                    .FirstOrDefaultAsync(a => a.ApplicantId == applicantId);

                if (applicant == null)
                {
                    return NotFound("Applicant not found.");
                }

                // Step 2: Check if already a student
                var (isAlreadyStudent, existingStudentId, message) = await CheckIfAlreadyStudent(applicant.Email);
                if (isAlreadyStudent)
                {
                    applicant.Status = Status.Withdrawn;
                    applicant.UpdatedBy = User.Identity.Name;
                    applicant.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = false,
                        message = $"Cannot admit: {message}. This application has been marked as withdrawn.",
                        isAlreadyStudent = true,
                        existingStudentId = existingStudentId
                    });
                }

                // Step 3: Validate original documents
                if (!VerifyOriginalDocuments(applicant))
                {
                    return Json(new { success = false, message = "Original documents must be verified before admission." });
                }

                // Step 4: Get user account
                var user = await _userManager.FindByEmailAsync(applicant.Email);
                if (user == null)
                {
                    return Json(new { success = false, message = "User account not found." });
                }

                // Step 5: Check user roles
                var userRoles = await _userManager.GetRolesAsync(user);
                if (!userRoles.Contains("Candidate"))
                {
                    return Json(new { success = false, message = "User does not have the 'Candidate' role." });
                }

                // Step 6: Generate Student ID BEFORE the transaction to avoid nested SaveChanges conflicts
                var studentId = await _applicantService.GenerateStudentIdAsync(applicant.AcademicYearId);
                _logger.LogInformation($"Generated Student ID: {studentId} for applicant {applicantId}, AcademicYearId: {applicant.AcademicYearId}");

                // Validate the generated ID
                if (string.IsNullOrEmpty(studentId))
                {
                    return Json(new { success = false, message = "Failed to generate student ID." });
                }

                // Step 7: Now proceed with the transaction
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        // Clean up other pending applications
                        await CleanupOtherApplications(applicant.Email, applicant.ApplicantId);

                        // Create the student record
                        var student = new Student
                        {
                            FullName = applicant.FullName,
                            Email = applicant.Email,
                            DateOfBirth = applicant.DateOfBirth,
                            Phone = applicant.Phone,
                            Gender = applicant.Gender,
                            MaritalStatus = applicant.MaritalStatus,
                            Nationality = applicant.Nationality,
                            Religion = applicant.Religion,
                            NrcOrPassportNumber = applicant.NrcOrPassport,
                            IsForeigner = applicant.IsForeigner,
                            IsAdmitted = true,
                            IsRegistered = false,
                            RegistrationStatus = Status.Unregistered,
                            ApplicationReferenceNumber = applicant.ReferenceNumber,
                            StudentCurrentYear = 1,
                            CurrentSemester = 1,
                            StudentStatus = Status.Admitted,
                            StudyPermission = applicant.StudyPermitCopy,
                            NrcOrPassportCopy = applicant.NrcOrPassportCopy,
                            PassportPhotoPath = applicant.PassportPhotoPath,
                            StudentAddress = new StudentAddress
                            {
                                AddressLine1 = applicant.AddressLine1,
                                AddressLine2 = applicant.AddressLine2,
                                City = applicant.City,
                                State = applicant.State,
                                Country = applicant.Country,
                                PostalCode = applicant.PostalCode
                            },
                            NextOfKin = new StudNextOfKin
                            {
                                Name = applicant.NextOfKinName,
                                Relationship = applicant.NextOfKinRelation,
                                PhoneNumber = applicant.NextOfKinPhone,
                                Email = applicant.NextOfKinEmail,
                                Address = applicant.NextOfKinAddress
                            },
                            FormerSchool = new StudFormerSchool
                            {
                                SchoolName = applicant.FormerSchoolName,
                                SchoolLevel = applicant.FormerSchoolLevel,
                                SchoolAddress = applicant.FormerSchoolAddress,
                                YearOfCompletion = applicant.YearOfCompletion,
                                SchoolResultsCopy = applicant.ResultsAttachmentCopy,
                                PrimarySchoolAddress = applicant.PrimarySchoolAddress,
                                PrimarySchoolName = applicant.PrimarySchoolName,
                                PrimarySchoolPeriod = applicant.PrimarySchoolPeriod,
                                SecondarySchoolName = applicant.SecondarySchoolName,
                                SecondarySchoolAddress = applicant.SecondarySchoolAddress,
                                SecondarySchoolPeriod = applicant.SecondarySchoolPeriod
                            },
                            ProgrammeId = applicant.ProgrammeId,
                            ProgrammeLevelId = applicant.ProgrammeLevelId,
                            SchoolId = applicant.SchoolId,
                            ModeOfStudyId = applicant.ModeOfStudyId,
                            AcademicYearId = applicant.AcademicYearId,
                            StudentId_Number = studentId, // Use the pre-generated ID
                            Username = user.UserName,
                            CreatedBy = User.Identity.Name,
                            CreatedAt = DateTime.Now,
                            AdmissionDate = DateTime.Now,
                            OutstandingFees = 0
                        };

                        _context.Students.Add(student);
                        await _context.SaveChangesAsync();

                        // Create customer in accounting system
                        try
                        {
                            var fullAddress = $"{applicant.AddressLine1}, {applicant.AddressLine2}, {applicant.City}, {applicant.State}, {applicant.Country}".Trim(' ', ',');

                            var customerResult = await _accountingService.CreateCustomerAsync(
                                studentId: studentId,
                                fullName: applicant.FullName,
                                address: fullAddress,
                                email: applicant.Email,
                                phone: applicant.Phone);

                            if (!customerResult.Success)
                            {
                                _logger.LogWarning($"Failed to create customer in accounting system: {customerResult.Message}");
                            }
                            else
                            {
                                _logger.LogInformation($"Successfully created customer in accounting system for student {studentId}");
                            }
                        }
                        catch (Exception accountingEx)
                        {
                            _logger.LogError(accountingEx, $"Error creating customer in accounting system for student {studentId}");
                        }

                        // Update applicant status
                        applicant.Status = Status.Admitted;
                        applicant.AdmissionStatus = Status.Admitted;
                        applicant.UpdatedBy = User.Identity.Name;
                        applicant.UpdatedAt = DateTime.Now;

                        // Handle role change
                        var removeRoleResult = await _userManager.RemoveFromRoleAsync(user, "Candidate");
                        if (!removeRoleResult.Succeeded)
                        {
                            throw new Exception("Failed to remove the 'Candidate' role.");
                        }

                        var addRoleResult = await _userManager.AddToRoleAsync(user, "Student");
                        if (!addRoleResult.Succeeded)
                        {
                            throw new Exception("Failed to add the 'Student' role.");
                        }

                        await _context.SaveChangesAsync();

                        // Commit the transaction
                        await transaction.CommitAsync();

                        // Queue admission email (after successful commit)
                        try
                        {
                            var studentWithIncludes = await _context.Students
                                .Include(s => s.Programme)
                                .Include(s => s.School)
                                .Include(s => s.AcademicYear)
                                .Include(s => s.StudentAddress)
                                .FirstOrDefaultAsync(s => s.Id == student.Id);

                            if (studentWithIncludes != null)
                            {
                                _backgroundEmailService.QueueAdmissionEmail(studentWithIncludes);
                                _logger.LogInformation($"Queued admission email for student {studentId}");
                            }
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, $"Error queuing admission email for student {studentId}");
                        }
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return Json(new { success = true, message = $"Applicant admitted successfully. Student ID: {studentId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Admission failed for applicant {applicantId}");
                return Json(new { success = false, message = "Admission failed: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AdmitQualified([FromBody] List<int> applicantIds)
        {
            if (applicantIds == null || !applicantIds.Any())
            {
                return Json(new { success = false, message = "No applicants provided for admission." });
            }

            var results = new List<AdmissionResult>();
            var successCount = 0;

            try
            {
                // Filter out duplicate applications - Keep only the earliest application per email
                var applicantsToProcess = await _context.Applicants
                    .Where(a => applicantIds.Contains(a.ApplicantId))
                    .Include(a => a.Programme)
                    .Include(a => a.School)
                    .Include(a => a.AcademicYear)
                    .OrderBy(a => a.CreatedAt)
                    .ToListAsync();

                // Group by email and take only the first (earliest) application per person
                var filteredApplicants = applicantsToProcess
                    .GroupBy(a => a.Email)
                    .Select(g => g.First())
                    .ToList();

                // Log excluded applications
                var excludedApplications = applicantsToProcess.Except(filteredApplicants).ToList();
                if (excludedApplications.Any())
                {
                    _logger.LogInformation($"Excluded {excludedApplications.Count} duplicate applications from bulk admission");
                }

                // Pre-generate all student IDs BEFORE starting the transaction
                var studentIdMap = new Dictionary<int, string>();
                foreach (var applicant in filteredApplicants)
                {
                    try
                    {
                        var studentId = await _applicantService.GenerateStudentIdAsync(applicant.AcademicYearId);
                        studentIdMap[applicant.ApplicantId] = studentId;
                        _logger.LogInformation($"Pre-generated Student ID: {studentId} for applicant {applicant.ApplicantId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to generate student ID for applicant {applicant.ApplicantId}");
                        results.Add(new AdmissionResult
                        {
                            ApplicantId = applicant.ApplicantId,
                            Success = false,
                            Message = $"Failed to generate student ID: {ex.Message}"
                        });
                    }
                }

                // Now start the transaction for actual admission
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    foreach (var applicant in filteredApplicants)
                    {
                        // Skip if we failed to generate ID for this applicant
                        if (!studentIdMap.ContainsKey(applicant.ApplicantId))
                        {
                            continue;
                        }

                        var result = new AdmissionResult { ApplicantId = applicant.ApplicantId };

                        try
                        {
                            // Check if already a student
                            var (isAlreadyStudent, existingStudentId, message) = await CheckIfAlreadyStudent(applicant.Email);
                            if (isAlreadyStudent)
                            {
                                applicant.Status = Status.Withdrawn;
                                applicant.UpdatedBy = User.Identity.Name;
                                applicant.UpdatedAt = DateTime.Now;

                                result.Success = false;
                                result.Message = $"Already admitted as student {existingStudentId} - application withdrawn";
                                results.Add(result);
                                continue;
                            }

                            // Verify qualification
                            if (applicant.IsQualified != true)
                            {
                                result.Success = false;
                                result.Message = "Applicant does not meet qualification requirements";
                                results.Add(result);
                                continue;
                            }

                            // Verify payment status
                            if (applicant.PaymentStatus != Status.Paid)
                            {
                                result.Success = false;
                                result.Message = "Payment status not confirmed";
                                results.Add(result);
                                continue;
                            }

                            // Verify document submission
                            if (!VerifyOriginalDocuments(applicant))
                            {
                                result.Success = false;
                                result.Message = "Original documents not verified";
                                results.Add(result);
                                continue;
                            }

                            // Get user account
                            var user = await _userManager.FindByEmailAsync(applicant.Email);
                            if (user == null)
                            {
                                result.Success = false;
                                result.Message = "User account not found";
                                results.Add(result);
                                continue;
                            }

                            // Cleanup other applications
                            await CleanupOtherApplications(applicant.Email, applicant.ApplicantId);

                            // Get the pre-generated student ID
                            var studentId = studentIdMap[applicant.ApplicantId];

                            // Create student record
                            var student = new Student
                            {
                                ApplicationReferenceNumber = applicant.ReferenceNumber,
                                FullName = applicant.FullName,
                                DateOfBirth = applicant.DateOfBirth,
                                Gender = applicant.Gender,
                                Phone = applicant.Phone ?? string.Empty,
                                Email = applicant.Email,
                                MaritalStatus = applicant.MaritalStatus ?? "Single",
                                Nationality = applicant.Nationality ?? "Zambian",
                                Religion = applicant.Religion ?? "Not Specified",
                                NrcOrPassportNumber = applicant.NrcOrPassport,
                                NrcOrPassportCopy = applicant.NrcOrPassportCopy ?? string.Empty,
                                PassportPhotoPath = applicant.PassportPhotoPath ?? string.Empty,
                                StudentId_Number = studentId, // Use the pre-generated ID
                                Username = user.UserName ?? user.Email,
                                StudentStatus = Status.Admitted,
                                IsForeigner = applicant.IsForeigner,
                                IsAdmitted = true,
                                IsRegistered = false,
                                RegistrationStatus = Status.Pending,
                                StudentCurrentYear = 1,
                                CurrentSemester = 1,
                                StudyPermission = applicant.StudyPermitCopy,
                                AdmissionDate = DateTime.Now,
                                OutstandingFees = 0,
                                StudentAddress = new StudentAddress
                                {
                                    AddressLine1 = applicant.AddressLine1 ?? string.Empty,
                                    AddressLine2 = applicant.AddressLine2,
                                    City = applicant.City ?? string.Empty,
                                    State = applicant.State ?? string.Empty,
                                    Country = applicant.Country ?? "Zambia",
                                    PostalCode = applicant.PostalCode
                                },
                                NextOfKin = new StudNextOfKin
                                {
                                    Name = applicant.NextOfKinName ?? string.Empty,
                                    Relationship = applicant.NextOfKinRelation ?? string.Empty,
                                    PhoneNumber = applicant.NextOfKinPhone ?? string.Empty,
                                    Email = applicant.NextOfKinEmail,
                                    Address = applicant.NextOfKinAddress
                                },
                                FormerSchool = new StudFormerSchool
                                {
                                    SchoolName = applicant.FormerSchoolName ?? string.Empty,
                                    SchoolLevel = applicant.FormerSchoolLevel ?? string.Empty,
                                    SchoolAddress = applicant.FormerSchoolAddress ?? string.Empty,
                                    YearOfCompletion = applicant.YearOfCompletion,
                                    SchoolResultsCopy = applicant.ResultsAttachmentCopy ?? string.Empty,
                                    PrimarySchoolAddress = applicant.PrimarySchoolAddress ?? string.Empty,
                                    PrimarySchoolName = applicant.PrimarySchoolName ?? string.Empty,
                                    PrimarySchoolPeriod = applicant.PrimarySchoolPeriod ?? string.Empty,
                                    SecondarySchoolName = applicant.SecondarySchoolName ?? string.Empty,
                                    SecondarySchoolAddress = applicant.SecondarySchoolAddress ?? string.Empty,
                                    SecondarySchoolPeriod = applicant.SecondarySchoolPeriod ?? string.Empty
                                },
                                ProgrammeId = applicant.ProgrammeId,
                                ProgrammeLevelId = applicant.ProgrammeLevelId,
                                SchoolId = applicant.SchoolId,
                                ModeOfStudyId = applicant.ModeOfStudyId,
                                AcademicYearId = applicant.AcademicYearId,
                                CreatedBy = User.Identity.Name,
                                CreatedAt = DateTime.Now
                            };

                            _context.Students.Add(student);

                            // Create customer in accounting system
                            try
                            {
                                var fullAddress = $"{applicant.AddressLine1}, {applicant.AddressLine2}, {applicant.City}, {applicant.State}, {applicant.Country}".Trim(' ', ',');

                                var customerResult = await _accountingService.CreateCustomerAsync(
                                    studentId: studentId,
                                    fullName: applicant.FullName,
                                    address: fullAddress,
                                    email: applicant.Email,
                                    phone: applicant.Phone ?? string.Empty);

                                if (!customerResult.Success)
                                {
                                    _logger.LogWarning($"Failed to create customer in accounting system for {applicant.FullName}: {customerResult.Message}");
                                }
                                else
                                {
                                    _logger.LogInformation($"Successfully created customer in accounting system for student {studentId}");
                                }
                            }
                            catch (Exception accountingEx)
                            {
                                _logger.LogError(accountingEx, $"Error creating customer in accounting system for applicant {applicant.ApplicantId}");
                            }

                            // Queue admission email
                            try
                            {
                                _backgroundEmailService.QueueAdmissionEmail(student);
                                _logger.LogInformation($"Queued admission email for student {studentId}");
                            }
                            catch (Exception emailEx)
                            {
                                _logger.LogError(emailEx, $"Error queuing admission email for student {studentId}");
                            }

                            // Update applicant status
                            applicant.Status = Status.Admitted;
                            applicant.AdmissionStatus = Status.Admitted;
                            applicant.UpdatedBy = User.Identity.Name;
                            applicant.UpdatedAt = DateTime.Now;

                            // Update user roles
                            await _userManager.RemoveFromRoleAsync(user, "Candidate");
                            await _userManager.AddToRoleAsync(user, "Student");

                            result.Success = true;
                            result.Message = $"Admission successful. Student ID: {studentId}";
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            result.Success = false;
                            result.Message = $"Error processing admission: {ex.Message}";
                            _logger.LogError(ex, $"Error processing admission for applicant {applicant.ApplicantId}");
                        }

                        results.Add(result);
                    }

                    // Save all changes
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Bulk admission failed, all changes have been rolled back.", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during bulk admission process");
                return Json(new
                {
                    success = false,
                    message = "A critical error occurred during the admission process.",
                    error = ex.Message,
                    results
                });
            }

            var failedAdmissions = results.Where(r => !r.Success).ToList();
            var summary = new
            {
                success = true,
                message = $"Successfully admitted {successCount} out of {results.Count} qualified applicants",
                successCount,
                failureCount = results.Count - successCount,
                results = results,
                failedAdmissions = failedAdmissions.Select(f => new
                {
                    applicantId = f.ApplicantId,
                    reason = f.Message
                }).ToList()
            };

            return Json(summary);
        }

        public class AdmissionResult
        {
            public int ApplicantId { get; set; }
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        private bool VerifyOriginalDocuments(Applicant applicant)
        {
            return true;
        }

        private async Task SendAdmissionEmail(Student student)
        {
            var subject = "Admission Confirmation";
            var body = $@"
                <h1>Welcome to Our Institution</h1>
                <p>Dear {student.FullName},</p>
                <p>Congratulations! We are pleased to inform you that you have been admitted to our institution. You have been accepted into the {student.School.Name}, enrolled in the {student.Programme.Name} programme for the academic year {student.AcademicYear.YearValue}.</p>
                <p>Here are your credentials:</p>
                <ul>
                    <li><strong>Student ID:</strong> {student.StudentId_Number}</li>
                    <li><strong>Username and Password:</strong> You may use the same credentials you used during the application process.</li>
                </ul>
                <p>Please log in to the student portal to complete your registration once the registration period is open.</p>
                <p>Additionally, ensure you bring your original documents to campus for verification.</p>
                <p>We look forward to welcoming you to our community.</p>
                <p>Best regards,<br>Your Admissions Team</p>";

            await _emailService.SendEmailAsync(student.Email, subject, body);
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int applicantId, string reason)
        {
            var applicant = _context.Applicants.Find(applicantId);
            if (applicant == null)
            {
                return NotFound();
            }

            var user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();

            if (user == null)
            {
                return NotFound();
            }

            applicant.Status = Status.Rejected;
            applicant.RejectReason = reason;
            applicant.UpdatedBy = user.Id;
            _context.SaveChanges();

            try
            {
                var applicantWithDetails = await _context.Applicants
                    .Include(a => a.Programme)
                    .Include(a => a.School)
                    .FirstOrDefaultAsync(a => a.ApplicantId == applicantId);

                if (applicantWithDetails != null)
                {
                    _backgroundEmailService.QueueRejectionEmail(
                        applicantName: applicantWithDetails.FullName,
                        applicantEmail: applicantWithDetails.Email,
                        programmeName: applicantWithDetails.Programme?.Name ?? "Unknown Programme",
                        schoolName: applicantWithDetails.School?.Name ?? "Unknown School",
                        reason: reason
                    );
                    _logger.LogInformation($"Queued rejection email for applicant {applicantId}");
                }
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, $"Error queuing rejection email for applicant {applicantId}");
            }

            return Json(new { success = true, message = "Applicant rejected successfully." });
        }

        public async Task SendRejectionEmail(string email, string reason)
        {
            var subject = "Admission Rejection";
            var body = $@"
                <h1>Admission Rejection</h1>
                <p>Dear Applicant,</p>
                <p>Sorry to inform you that your admission application has been rejected for the following reason: {reason}</p>
                <p>Best regards,<br>Your Admissions Team</p>";

            await _emailService.SendEmailAsync(email, subject, body);
        }

        [HttpGet]
        [Route("AdmissionProcess/PreviewFile")]
        public IActionResult PreviewFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return BadRequest("File path is required");
            }

            try
            {
                if (!Path.IsPathRooted(filePath))
                {
                    filePath = Path.Combine(_webHostEnvironment.ContentRootPath, filePath);
                }

                var fullPath = Path.GetFullPath(filePath);

                Console.WriteLine($"Attempting to access file at: {fullPath}");

                if (!System.IO.File.Exists(fullPath))
                {
                    Console.WriteLine($"File not found at: {fullPath}");
                    return NotFound($"File not found at: {fullPath}");
                }

                string contentType;
                var extension = Path.GetExtension(fullPath).ToLower();

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

                return PhysicalFile(fullPath, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing file: {ex.Message}");
                return StatusCode(500, $"Error accessing file: {ex.Message}");
            }
        }

        public async Task<IActionResult> AdmittedStudents()
        {
            var admittedStudents = await _context.Students.Where(s => s.StudentStatus == Status.Admitted)
                .Include(s => s.AcademicYear)
                .Include(s => s.Programme)
                .Include(s => s.School)
                .Include(s => s.StudentAddress)
                .Include(s => s.ModeOfStudy)
                .Include(s => s.NextOfKin)
                .Include(s => s.FormerSchool)
                .ToListAsync();
            return View("~/Views/AdmissionProcess/AdmittedStudents.cshtml", admittedStudents);
        }

        public async Task<IActionResult> PendingWaitlist()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            bool isAdmin = User.IsInRole("Admin");
            bool isDean = User.IsInRole("Dean");
            bool isRegistrar = User.IsInRole("Registrar");
            bool isProgramCoordinator = User.IsInRole("ProgramCoordinator");
            bool isVC = User.IsInRole("VC");
            bool isDVC = User.IsInRole("DVC");

            IQueryable<Applicant> waitlistedQuery = _context.Applicants
                .Where(a => a.PaymentStatus == Status.Paid && a.Status == Status.WaitListed)
                .Include(a => a.School)
                .Include(a => a.Programme)
                .Include(a => a.AcademicYear)
                .Include(a => a.ModeOfStudy)
                .Include(a => a.ProgrammeLevel)
                .Include(a => a.SubjectGrades)
                    .ThenInclude(sg => sg.Grade)
                .Include(a => a.SubjectGrades)
                    .ThenInclude(sg => sg.Subject)
                .Where(a => a.IsForeigner == true || a.SubjectGrades.Any());

            if (!isAdmin && !isRegistrar && !isVC && !isDVC)
            {
                if (isDean)
                {
                    var userSchools = await _context.Schools
                        .Where(s => s.DeanId == currentUser.Id || s.AssistantDeanId == currentUser.Id)
                        .Select(s => s.Id)
                        .ToListAsync();

                    if (!userSchools.Any())
                    {
                        return NotFound();
                    }

                    waitlistedQuery = waitlistedQuery.Where(a => userSchools.Contains(a.SchoolId));
                }
                else if (isProgramCoordinator)
                {
                    var userProgrammes = await _context.Programmes
                        .Where(p => p.CoordinatorId == currentUser.Id)
                        .Select(p => p.Id)
                        .ToListAsync();

                    if (!userProgrammes.Any())
                    {
                        return NotFound();
                    }

                    waitlistedQuery = waitlistedQuery.Where(a => userProgrammes.Contains(a.ProgrammeId));
                }
                else
                {
                    return NotFound();
                }
            }

            var waitlistedApplicants = await waitlistedQuery.ToListAsync();

            ViewBag.TotalWaitlisted = waitlistedApplicants.Count;
            ViewBag.ForeignWaitlisted = waitlistedApplicants.Count(a => a.IsForeigner == true);

            var schoolStats = waitlistedApplicants
                .GroupBy(a => a.School.Name)
                .Select(g => new
                {
                    SchoolName = g.Key,
                    Count = g.Count()
                })
                .ToList();
            ViewBag.SchoolStats = schoolStats;

            var programmeStats = waitlistedApplicants
                .GroupBy(a => a.Programme.Name)
                .Select(g => new
                {
                    ProgrammeName = g.Key,
                    Count = g.Count()
                })
                .ToList();
            ViewBag.ProgrammeStats = programmeStats;

            var serializedWaitlisted = waitlistedApplicants.Select(a => new
            {
                a.ApplicantId,
                a.ReferenceNumber,
                a.FullName,
                a.DateOfBirth,
                a.Gender,
                a.Phone,
                a.Email,
                a.NrcOrPassport,
                a.MaritalStatus,
                a.Nationality,
                a.Religion,
                a.IsForeigner,
                a.AddressLine1,
                a.AddressLine2,
                a.City,
                a.State,
                a.PostalCode,
                a.Country,
                a.NextOfKinName,
                a.NextOfKinRelation,
                a.NextOfKinPhone,
                a.NextOfKinEmail,
                a.NextOfKinAddress,
                a.PrimarySchoolName,
                a.PrimarySchoolAddress,
                a.PrimarySchoolPeriod,
                a.SecondarySchoolName,
                a.SecondarySchoolAddress,
                a.SecondarySchoolPeriod,
                a.FormerSchoolName,
                a.FormerSchoolAddress,
                a.FormerSchoolLevel,
                a.YearOfCompletion,
                a.ResultsAttachmentCopy,
                a.NrcOrPassportCopy,
                a.StudyPermitCopy,
                School = new { a.School.Id, a.School.Name },
                Programme = new
                {
                    a.Programme.Id,
                    a.Programme.Name,
                    a.Programme.MinimumPointsTop5Subjects
                },
                AcademicYear = new { a.AcademicYear.YearId, a.AcademicYear.YearValue },
                ModeOfStudy = new { a.ModeOfStudy.ModeId, a.ModeOfStudy.ModeName },
                ProgrammeLevel = new { a.ProgrammeLevel.Id, a.ProgrammeLevel.Name },
                a.IsQualified,
                SubjectGrades = a.SubjectGrades.Select(sg => new
                {
                    Subject = new { sg.Subject.SubjectId, sg.Subject.SubjectName },
                    Grade = new { sg.Grade.GradeId, sg.Grade.GradeValue, sg.Grade.GradePoint }
                }).ToList()
            }).ToList();

            ViewBag.ApplicantsJson = JsonSerializer.Serialize(serializedWaitlisted, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var qualifiedApplicants = waitlistedApplicants
                .Where(a => a.IsQualified == true)
                .Select(a => a.ApplicantId)
                .ToList();
            ViewBag.QualifiedApplicantsJson = JsonSerializer.Serialize(qualifiedApplicants);

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsDean = isDean;
            ViewBag.IsRegistrar = isRegistrar;
            ViewBag.IsProgramCoordinator = isProgramCoordinator;

            return View("/Views/AdmissionProcess/PendingWaitlist.cshtml", waitlistedApplicants);
        }

        private async Task CleanupOtherApplications(string email, int currentApplicationId)
        {
            var otherApplications = await _context.Applicants
                .Where(a => a.Email == email &&
                           a.ApplicantId != currentApplicationId &&
                           a.Status == Status.Pending)
                .ToListAsync();

            if (otherApplications.Any())
            {
                foreach (var application in otherApplications)
                {
                    application.Status = Status.Withdrawn;
                    application.UpdatedBy = User.Identity.Name;
                    application.UpdatedAt = DateTime.Now;
                }

                _logger.LogInformation($"Cleaned up {otherApplications.Count} other applications for applicant with email: {email}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDuplicateApplicationsReport()
        {
            try
            {
                var admittedApplicants = await _context.Applicants
                    .Where(a => a.Status == Status.Admitted)
                    .Select(a => new { a.Email, a.ApplicantId, a.FullName, a.ReferenceNumber, a.CreatedAt })
                    .ToListAsync();

                var duplicateReport = new List<object>();
                var totalPendingToCleanup = 0;

                foreach (var admitted in admittedApplicants)
                {
                    var pendingApplications = await _context.Applicants
                        .Where(a => a.Email == admitted.Email &&
                                   a.Status == Status.Pending &&
                                   a.ApplicantId != admitted.ApplicantId)
                        .Include(a => a.Programme)
                        .Include(a => a.School)
                        .Select(a => new
                        {
                            a.ApplicantId,
                            a.ReferenceNumber,
                            a.CreatedAt,
                            Programme = a.Programme.Name,
                            School = a.School.Name
                        })
                        .ToListAsync();

                    if (pendingApplications.Any())
                    {
                        duplicateReport.Add(new
                        {
                            AdmittedApplicant = new
                            {
                                admitted.Email,
                                admitted.FullName,
                                admitted.ApplicantId,
                                admitted.ReferenceNumber,
                                admitted.CreatedAt
                            },
                            PendingApplications = pendingApplications,
                            PendingCount = pendingApplications.Count
                        });

                        totalPendingToCleanup += pendingApplications.Count;
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"Found {duplicateReport.Count} applicants with duplicate pending applications",
                    totalApplicantsWithDuplicates = duplicateReport.Count,
                    totalPendingApplicationsToCleanup = totalPendingToCleanup,
                    duplicates = duplicateReport
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error generating duplicate applications report",
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        public IActionResult CleanupTestPage()
        {
            return View("~/Views/AdmissionProcess/CleanupTest.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> CleanupExistingDuplicateApplications()
        {
            try
            {
                var cleanupResults = new List<object>();
                var totalCleaned = 0;

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var admittedApplicants = await _context.Applicants
                        .Where(a => a.Status == Status.Admitted)
                        .ToListAsync();

                    foreach (var admitted in admittedApplicants)
                    {
                        var pendingApplications = await _context.Applicants
                            .Where(a => a.Email == admitted.Email &&
                                       a.Status == Status.Pending &&
                                       a.ApplicantId != admitted.ApplicantId)
                            .Include(a => a.Programme)
                            .Include(a => a.School)
                            .ToListAsync();

                        if (pendingApplications.Any())
                        {
                            var cleanedApplications = new List<object>();

                            foreach (var pending in pendingApplications)
                            {
                                pending.Status = Status.Withdrawn;
                                pending.UpdatedBy = User.Identity.Name;
                                pending.UpdatedAt = DateTime.Now;

                                cleanedApplications.Add(new
                                {
                                    pending.ApplicantId,
                                    pending.ReferenceNumber,
                                    Programme = pending.Programme.Name,
                                    School = pending.School.Name,
                                    pending.CreatedAt
                                });

                                totalCleaned++;
                            }

                            cleanupResults.Add(new
                            {
                                ApplicantEmail = admitted.Email,
                                ApplicantName = admitted.FullName,
                                AdmittedApplicationId = admitted.ApplicantId,
                                CleanedApplications = cleanedApplications,
                                CleanedCount = cleanedApplications.Count
                            });

                            _logger.LogInformation($"Cleaned up {cleanedApplications.Count} duplicate applications for {admitted.FullName} ({admitted.Email})");
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"Successfully cleaned up {totalCleaned} duplicate applications for {cleanupResults.Count} applicants",
                        totalApplicationsCleaned = totalCleaned,
                        totalApplicantsAffected = cleanupResults.Count,
                        cleanupDetails = cleanupResults
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Cleanup failed, all changes have been rolled back.", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during duplicate cleanup");
                return Json(new
                {
                    success = false,
                    message = "Error occurred during cleanup process",
                    error = ex.Message
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReportAndCleanupDuplicates(bool performCleanup = false)
        {
            try
            {
                var reportResult = await GetDuplicateApplicationsReport();
                var reportData = ((JsonResult)reportResult).Value;

                if (!performCleanup)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Duplicate applications report generated. Set 'performCleanup=true' to execute cleanup.",
                        report = reportData
                    });
                }

                var cleanupResult = await CleanupExistingDuplicateApplications();
                var cleanupData = ((JsonResult)cleanupResult).Value;

                return Json(new
                {
                    success = true,
                    message = "Report generated and cleanup completed",
                    initialReport = reportData,
                    cleanupResults = cleanupData
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error in report and cleanup process",
                    error = ex.Message
                });
            }
        }

        private async Task<(bool IsAlreadyStudent, string StudentId, string Message)> CheckIfAlreadyStudent(string email)
        {
            var existingStudent = await _context.Students
                .Where(s => s.Email == email && s.StudentStatus == Status.Admitted)
                .FirstOrDefaultAsync();

            if (existingStudent != null)
            {
                return (true, existingStudent.StudentId_Number,
                        $"This applicant is already admitted as a student with ID: {existingStudent.StudentId_Number}");
            }

            return (false, "", "");
        }
    }
}