using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SIS.Data;
using SIS.DTOs.StudentApplication;
using SIS.Enums;
using SIS.Models.Accounting;
using SIS.Models.Accounts;
using SIS.Models.Fees;
using SIS.Models.Payments;
using SIS.Models.StudentApplication;
using SIS.Services;
using SIS.Services.Accounting;
using SIS.Services.Emails;
using SIS.Services.Payment;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SIS.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentService _paymentService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<PaymentsController> _logger;
        private readonly IEmailService _emailService;
        private readonly IAccountingService _accountingService;
        private readonly IBackgroundEmailService _backgroundEmailService;
        private readonly IStudentInvoiceService _studentInvoiceService;
        private readonly IPayBossService _payBoss;

        public PaymentsController(
            ApplicationDbContext context,
            IPaymentService paymentService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<PaymentsController> logger,
            IEmailService emailService,
            IAccountingService accountingService,
            IBackgroundEmailService backgroundEmailService,
            IStudentInvoiceService studentInvoiceService,
            IPayBossService payBoss)
        {
            _context = context;
            _paymentService = paymentService;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailService = emailService;
            _accountingService = accountingService;
            _backgroundEmailService = backgroundEmailService;
            _studentInvoiceService = studentInvoiceService;
            _payBoss = payBoss;
        }

        #region Existing Methods (PayNow, PaymentSuccess, GetApplicableFees, etc.)

        [HttpPost]
        public IActionResult PayNow([FromBody] string referenceNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(referenceNumber))
                {
                    return Json(new { success = false, message = "Reference number not found in your request." });
                }

                var applicant = _context.Applicants.FirstOrDefault(a => a.ReferenceNumber == referenceNumber);
                if (applicant == null)
                {
                    return Json(new { success = false, message = "Applicant not found." });
                }

                _paymentService.UpdateApplicationPayment(applicant);
                return Json(new { success = true, message = "Payment has been processed successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Payment update failed: " + ex.Message });
            }
        }

        public IActionResult PaymentSuccess(InitApplicationDto model)
        {
            ViewBag.ReferenceNumber = model.ReferenceNumber;
            return View();
        }

        // Process credit card payment for student fees - UPDATED
        /*[HttpPost]
        public async Task<IActionResult> ProcessStudentCardPayment(StudentCardPaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the errors in the form.";
                return RedirectToAction("StudentPaymentSelection");
            }

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate amount
                if (model.Amount <= 0)
                {
                    TempData["Error"] = "Payment amount must be greater than zero.";
                    return RedirectToAction("StudentPaymentSelection");
                }

                // Extract student ID from transaction reference
                var studentId = model.TransactionReference.Split('_').Last();

                // Find student by StudentId_Number with included related data
                var student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .Include(s => s.FinancialStatements)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == studentId);

                if (student == null)
                {
                    await dbTransaction.RollbackAsync();
                    TempData["Error"] = "Payment failed. Student not found.";
                    return RedirectToAction("StudentFinance");
                }

                // Validate that payment doesn't exceed outstanding balance
                if (model.Amount > student.OutstandingFees)
                {
                    await dbTransaction.RollbackAsync();
                    TempData["Error"] = $"Payment amount (K{model.Amount:F2}) cannot exceed outstanding balance (K{student.OutstandingFees:F2}).";
                    return RedirectToAction("StudentPaymentSelection");
                }

                // Create transaction reference
                string transactionReference = $"STDFEE_CARD_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}";

                // 🎯 CRITICAL: Check if transaction reference already exists
                var existingPayment = await _context.FinancialStatements
                    .AnyAsync(fs => fs.TransactionReference == transactionReference);

                if (existingPayment)
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogWarning("Duplicate transaction reference detected for card payment: {TransactionReference}", transactionReference);
                    TempData["Error"] = "This payment has already been processed. Please try again.";
                    return RedirectToAction("StudentFinance");
                }

                // 🎯 Additional check: Prevent rapid duplicate submissions by same student
                var recentPayment = await _context.FinancialStatements
                    .Where(fs => fs.StudentId == student.Id &&
                                fs.AmountPaid == model.Amount &&
                                fs.PaymentDate >= DateTime.UtcNow.AddMinutes(-5))
                    .AnyAsync();

                if (recentPayment)
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogWarning("Potential duplicate payment detected for student {StudentId} within 5 minutes", student.Id);
                    TempData["Error"] = "A similar payment was recently processed. Please check your payment history.";
                    return RedirectToAction("StudentFinance");
                }

                // 🎯 POST PAYMENT TO ACCOUNTING SYSTEM
                try
                {
                    var accountingResult = await _accountingService.PostPaymentAsync(student.StudentId_Number, model.Amount);

                    if (!accountingResult.Success)
                    {
                        await dbTransaction.RollbackAsync();
                        _logger.LogError("Failed to post payment to accounting system: {Message}", accountingResult.Message);
                        TempData["Error"] = "Payment processing failed. Please try again.";
                        return RedirectToAction("StudentPaymentSelection");
                    }
                    else
                    {
                        _logger.LogInformation("Successfully posted payment to accounting system for student {StudentId}", student.StudentId_Number);
                    }
                }
                catch (Exception accountingEx)
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogError(accountingEx, "Error posting payment to accounting system for student {StudentId}", student.StudentId_Number);
                    TempData["Error"] = "Payment processing failed. Please try again.";
                    return RedirectToAction("StudentPaymentSelection");
                }

                // Calculate total payments and registration requirements
                var feesPaid = student.FinancialStatements?.Sum(fs => fs.AmountPaid) ?? 0;
                decimal totalPaid = feesPaid + model.Amount;
                decimal totalFees = student.OutstandingFees + feesPaid;
                var minRegistrationPayment = totalFees * student.AcademicYear.MinRegistrationPaymentPercentage / 100;

                // Create financial statement entry
                var financialStatement = new FinancialStatement
                {
                    StudentId = student.Id,
                    AmountPaid = model.Amount,
                    PaymentDate = DateTime.UtcNow,
                    PaymentMethod = "Credit Card",
                    TransactionReference = transactionReference,
                    AcademicYearId = student.AcademicYearId,
                    OutstandingAmount = student.OutstandingFees - model.Amount,
                    Semester = student.CurrentSemester ?? 1
                };

                // Update student's outstanding balance
                student.OutstandingFees -= model.Amount;

                // Check and update registration status if minimum payment is met
                bool registrationCompleted = false;
                if (totalPaid >= minRegistrationPayment && student.RegistrationStatus != Status.Registered)
                {
                    student.RegistrationStatus = Status.Registered;
                    registrationCompleted = true;
                }

                // Save changes within transaction
                _context.FinancialStatements.Add(financialStatement);
                _context.Entry(student).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("Card payment processed successfully for student {StudentId} with transaction {TransactionReference}",
                    studentId, transactionReference);

                return RedirectToAction("StudentPaymentSuccess", new
                {
                    transactionReference = transactionReference,
                    amount = model.Amount,
                    registrationCompleted = registrationCompleted
                });
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error processing student card payment");
                TempData["Error"] = "An error occurred while processing the payment.";
                return RedirectToAction("StudentFinance");
            }
        }*/
        /*public async Task<IActionResult> ProcessStudentMobilePayment(StudentMobilePaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the errors in the form.";
                return RedirectToAction("StudentPaymentSelection");
            }

            try
            {
                // Validate amount
                if (model.Amount <= 0)
                {
                    TempData["Error"] = "Payment amount must be greater than zero.";
                    return RedirectToAction("Student_Dashboard", "Home");
                }

                // Extract student ID from transaction reference
                var studentId = model.TransactionReference.Split('_').Last();

                // Find student by StudentId_Number with included related data
                var student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .Include(s => s.FinancialStatements)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == studentId);

                if (student == null)
                {
                    TempData["Error"] = "Payment failed. Student not found.";
                    return RedirectToAction("Student_Dashboard", "Home");
                }

                // Create transaction reference
                string transactionReference = $"STDFEE_MOBILE_{model.Provider}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}";

                // INTEGRATE TINGG API
                try
                {
                    // Format phone number (remove any non-digit characters)
                    var formattedPhone = new string(model.PhoneNumber.Where(char.IsDigit).ToArray());

                    // Call Tingg API
                    using var httpClient = new HttpClient();
                    var tinggRequest = new
                    {
                        name = $"Student Payment - {student.StudentId_Number}",
                        phone = formattedPhone,
                        amount = model.Amount,
                        provider = model.Provider,
                        studentId = student.Id,
                        MerchantTransactionId = transactionReference
                    };

                    var jsonContent = JsonSerializer.Serialize(tinggRequest);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync("https://ecampus.edenuniversity.edu.zm/api/tingg/express-checkout", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var tinggResponse = JsonSerializer.Deserialize<TinggResponse>(responseContent);

                        if (!string.IsNullOrEmpty(tinggResponse?.CheckoutUrl))
                        {
                            // Redirect directly to Tingg checkout
                            return Redirect(tinggResponse.CheckoutUrl);
                        }
                    }

                    // Log warning if we continue to fallback processing
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Tingg API call failed with status {StatusCode}: {Error}",
                        response.StatusCode, errorContent);
                }
                catch (Exception tinggEx)
                {
                    _logger.LogError(tinggEx, "Error calling Tingg API, continuing with direct payment processing");
                }

                // Original processing logic (only reached if Tingg API fails)
                await Task.Delay(2000);

                return Json(new
                {
                    success = true,
                    redirectUrl = Url.Action("StudentPaymentSuccess", new
                    {
                        transactionReference = transactionReference,
                        amount = model.Amount,
                        registrationCompleted = false // Since STEP 2 is commented, registrationCompleted is false
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing student mobile payment");
                return Json(new
                {
                    success = false,
                    message = "An error occurred while processing the payment."
                });
            }
        }
*/
        private List<FeeConfiguration> GetApplicableFees(Student student)
        {
            bool hasActiveAllocation = _context.Allocations
                .Any(a => a.ApplicationId == student.Id &&
                         a.Status == Status.Active &&
                         a.EndDate >= DateTime.Now);

            var fees = _context.FeeConfigurations
                .Include(f => f.FeeType)
                .Where(f => (f.AcademicYearId == null || f.AcademicYearId == student.AcademicYearId) &&
                            f.FeeType.ApplicableFor == "Student")
                .ToList();

            if (student.Programme?.IsSemesterBased == true && student.CurrentSemester.HasValue)
            {
                fees = fees.Where(f => f.Semester == null || f.Semester == student.CurrentSemester.Value).ToList();
            }
            else
            {
                fees = fees.Where(f => f.Semester == null).ToList();
            }

            var filteredFees = fees.Where(f =>
                (f.AppliesOnlyToAccommodated == false || student.HasAccommodationClearance == true || hasActiveAllocation) &&
                (f.AppliesOnlyToForeignStudents == false || student.IsForeigner == true) &&
                (f.AppliesUniversally ||
                (
                    (f.SchoolId == null || f.SchoolId == student.SchoolId) &&
                    (f.ProgrammeId == null || f.ProgrammeId == student.ProgrammeId) &&
                    (f.ModeOfStudyId == null || f.ModeOfStudyId == student.ModeOfStudyId) &&
                    (f.ProgramLevelId == null || f.ProgramLevelId == student.ProgrammeLevelId) &&
                    (f.YearOfStudy == null || f.YearOfStudy == student.StudentCurrentYear)
                ))
            ).ToList();

            return filteredFees;
        }

        private List<FeeBreakdownItem> CreateFeeBreakdown(List<FeeConfiguration> fees, Student student)
        {
            var breakdown = new List<FeeBreakdownItem>();
            decimal remainingPaid = StudentTools.GetStudentTotalPaid(student.Id);

            var sortedFees = fees.OrderBy(f => f.FeeType.Name == "Tuition").ThenBy(f => f.Amount).ToList();

            foreach (var fee in sortedFees)
            {
                decimal paidForThisFee = remainingPaid >= fee.Amount ? fee.Amount : remainingPaid;
                remainingPaid = Math.Max(0, remainingPaid - fee.Amount);

                breakdown.Add(new FeeBreakdownItem
                {
                    Description = fee.FeeType.Name,
                    Amount = fee.Amount,
                    Paid = Math.Round(paidForThisFee, 2),
                    Balance = Math.Round(fee.Amount - paidForThisFee, 2)
                });
            }

            return breakdown;
        }

        private List<FeeBreakdownItem> CreateFeeBreakdownFromInvoice(List<StudentInvoiceItem> invoiceItems, decimal totalPaid)
        {
            var breakdown = new List<FeeBreakdownItem>();
            decimal remainingPaid = totalPaid;

            var sortedItems = invoiceItems.OrderBy(f => f.FeeTypeName == "Tuition").ThenBy(f => f.Amount).ToList();

            foreach (var item in sortedItems)
            {
                decimal paidForThisFee = remainingPaid >= item.Amount ? item.Amount : remainingPaid;
                remainingPaid = Math.Max(0, remainingPaid - item.Amount);

                breakdown.Add(new FeeBreakdownItem
                {
                    Description = item.FeeTypeName,
                    Amount = item.Amount,
                    Paid = Math.Round(paidForThisFee, 2),
                    Balance = Math.Round(item.Amount - paidForThisFee, 2)
                });
            }

            return breakdown;
        }

        #endregion

        #region Student Finance & Payments

        public async Task<IActionResult> StudentFinance()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                var student = await _context.Students
                    .Include(s => s.Programme).Include(s => s.School).Include(s => s.ModeOfStudy)
                    .Include(s => s.AcademicYear).Include(s => s.ProgrammeLevel)
                    .Include(s => s.FinancialStatements).Include(s => s.StudentAddress)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student == null) return NotFound("Student record not found.");

                if (!student.IsRegistered)
                {
                    var emptyViewModel = new StudentFinanceViewModel
                    {
                        Student = student,
                        TotalFees = 0,
                        AmountPaid = 0,
                        OutstandingBalance = 0,
                        MinRegistrationPayment = 0,
                        MinExamPayment = 0,
                        AcademicYear = student.AcademicYear.YearValue,
                        FeeBreakdown = new List<FeeBreakdownItem>()
                    };
                    TempData["Message"] = "Please complete your registration to view fee details.";
                    return View("~/Views/Payments/StudentFinance.cshtml", emptyViewModel);
                }

                var totalPaidForAcademicYear = student.FinancialStatements
                    .Where(fs => fs.AcademicYearId == student.AcademicYearId)
                    .Sum(fs => fs.AmountPaid);

                decimal totalFees = 0;
                decimal currentTotalFees = 0;
                List<FeeBreakdownItem> feeBreakdown = new();

                var studentInvoice = await _context.StudentInvoices
                    .Include(si => si.InvoiceItems)
                    .FirstOrDefaultAsync(si => si.StudentId == student.Id &&
                                             si.AcademicYearId == student.AcademicYearId &&
                                             (student.Programme.IsSemesterBased == false || si.Semester == student.CurrentSemester));

                if (studentInvoice == null)
                {
                    try
                    {
                        var invoiceResult = await _studentInvoiceService.GenerateStudentInvoiceAsync(student);
                        if (invoiceResult.Success)
                        {
                            studentInvoice = await _context.StudentInvoices
                                .Include(si => si.InvoiceItems)
                                .FirstOrDefaultAsync(si => si.StudentId == student.Id &&
                                                         si.AcademicYearId == student.AcademicYearId &&
                                                         (student.Programme.IsSemesterBased == false || si.Semester == student.CurrentSemester));
                            TempData["Info"] = "Your invoice has been generated successfully.";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error generating invoice for student {student.StudentId_Number}");
                    }
                }

                if (studentInvoice != null)
                {
                    currentTotalFees = await _context.StudentInvoices
                        .Where(si => si.StudentId == student.Id && si.AcademicYearId == student.AcademicYearId && si.Semester == student.CurrentSemester)
                        .SumAsync(si => si.TotalAmount);
                    feeBreakdown = CreateFeeBreakdownFromInvoice(studentInvoice.InvoiceItems.ToList(), totalPaidForAcademicYear);
                }
                else
                {
                    var applicableFees = GetApplicableFees(student);
                    currentTotalFees = applicableFees.Sum(f => f.Amount);
                    feeBreakdown = CreateFeeBreakdown(applicableFees, student);
                }

                var outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
                totalFees = StudentTools.GetStudentTotalFees(student.Id);
                totalPaidForAcademicYear = StudentTools.GetStudentTotalPaid(student.Id);

                student.OutstandingFees = outstandingBalance;
                await _context.SaveChangesAsync();

                var viewModel = new StudentFinanceViewModel
                {
                    Student = student,
                    TotalFees = totalFees,
                    CurrentTotalFees = currentTotalFees,
                    AmountPaid = totalPaidForAcademicYear,
                    OutstandingBalance = outstandingBalance,
                    MinRegistrationPayment = ((currentTotalFees * student.AcademicYear.MinRegistrationPaymentPercentage) / 100) + (outstandingBalance - currentTotalFees),
                    MinExamPayment = (totalFees * student.AcademicYear.MinExamPaymentPercentage) / 100,
                    AcademicYear = student.AcademicYear.YearValue,
                    FeeBreakdown = feeBreakdown
                };

                return View("~/Views/Payments/StudentFinance.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StudentFinance action");
                return RedirectToAction("Error", "Home");
            }
        }

        #endregion

        #region Application Payment Selection

        [HttpGet]
        public async Task<IActionResult> PaymentSelection(string referenceNumber)
        {
            try
            {
                var application = await _context.Applicants
                    .Include(a => a.Programme).Include(a => a.School).Include(a => a.ProgrammeLevel)
                    .FirstOrDefaultAsync(a => a.ReferenceNumber == referenceNumber);

                if (application == null)
                {
                    TempData["ErrorMessage"] = "Application not found.";
                    return RedirectToAction("Index", "StudentApplication");
                }

                if (application.PaymentStatus == Status.Completed)
                {
                    TempData["InfoMessage"] = "Payment has already been completed for this application.";
                    return RedirectToAction("Index", "StudentApplication");
                }

                var feeDetails = await GetCandidateApplicableFees(application);

                var viewModel = new PaymentSelectionViewModel
                {
                    ApplicationReference = referenceNumber,
                    ApplicantName = application.FullName,
                    ProgrammeName = application.Programme?.Name ?? "N/A",
                    SchoolName = application.School?.Name ?? "N/A",
                    ProgrammeLevel = application.ProgrammeLevel?.Name ?? "N/A",
                    FeeDetails = feeDetails,
                    TotalAmount = feeDetails.Sum(f => f.Amount)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment selection page");
                TempData["ErrorMessage"] = "An error occurred while loading the payment page.";
                return RedirectToAction("Index", "StudentApplication");
            }
        }

        private async Task<List<FeeDetailViewModel>> GetCandidateApplicableFees(SIS.Models.StudentApplication.Applicant application)
        {
            var result = new List<FeeDetailViewModel>();

            var applicationFees = await _context.FeeConfigurations
                .Include(f => f.FeeType)
                .Where(f =>
                    f.FeeType.ApplicableFor == "Candidate" && f.FeeType.IsActive &&
                    f.AcademicYearId == application.AcademicYearId &&
                    (f.AppliesUniversally ||
                        (f.ProgrammeId == application.ProgrammeId && f.ProgrammeId != null) ||
                        (f.SchoolId == application.SchoolId && f.SchoolId != null) ||
                        (f.ProgramLevelId == application.ProgrammeLevelId && f.ProgramLevelId != null) ||
                        (f.ModeOfStudyId == application.ModeOfStudyId && f.ModeOfStudyId != null)))
                .ToListAsync();

            foreach (var fee in applicationFees)
            {
                result.Add(new FeeDetailViewModel
                {
                    FeeName = fee.FeeType.Name,
                    Description = fee.FeeType.Description ?? fee.FeeType.Name,
                    Amount = fee.Amount
                });
            }

            if (!result.Any())
            {
                result.Add(new FeeDetailViewModel
                {
                    FeeName = "Application Fee",
                    Description = "Standard application processing fee",
                    Amount = 250.00m
                });
            }

            return result;
        }

        #endregion

        #region Mobile Payment Processing

        /*[HttpPost]
        public async Task<IActionResult> ProcessMobilePayment(MobilePaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors in the form.";
                return RedirectToAction("PaymentSelection", new { referenceNumber = model.ApplicationReference });
            }

            try
            {
                var application = await _context.Applicants
                    .Include(a => a.Programme).Include(a => a.School)
                    .FirstOrDefaultAsync(a => a.ReferenceNumber == model.ApplicationReference);

                if (application == null)
                {
                    TempData["ErrorMessage"] = "Application not found.";
                    return RedirectToAction("Index", "StudentApplication");
                }

                string transactionReference = $"APPL_MOBILE_{model.Provider}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}";

                try
                {
                    var formattedPhone = new string(model.PhoneNumber.Where(char.IsDigit).ToArray());
                    var tinggRequest = new
                    {
                        name = $"Application Payment - {application.FullName}",
                        phone = formattedPhone,
                        amount = model.Amount,
                        provider = model.Provider,
                        applicantId = application.ApplicantId,
                        MerchantTransactionId = transactionReference
                    };

                    var jsonContent = JsonSerializer.Serialize(tinggRequest);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    using var httpClient = new HttpClient();
                    var response = await httpClient.PostAsync("https://ecampus.edenuniversity.edu.zm/api/tingg/express-checkout", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var tinggResponse = JsonSerializer.Deserialize<TinggResponse>(responseContent);

                        if (!string.IsNullOrEmpty(tinggResponse?.CheckoutUrl))
                        {
                            return Redirect(tinggResponse.CheckoutUrl);
                        }
                    }
                }
                catch (Exception tinggEx)
                {
                    _logger.LogError(tinggEx, "Tingg API call failed");
                }

                TempData["Error"] = "Payment processing failed. Please try again.";
                return RedirectToAction("PaymentSelection", new { referenceNumber = model.ApplicationReference });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing mobile payment");
                TempData["Error"] = "Payment processing failed. Please try again.";
                return RedirectToAction("PaymentSelection", new { referenceNumber = model.ApplicationReference });
            }
        }*/

        [HttpPost]
        /*[ValidateAntiForgeryToken]*/
        public async Task<IActionResult> ProcessStudentMobilePayment(StudentMobilePaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the errors in the form.";
                return RedirectToAction("Student_Dashboard", "Home");
            }

            if (model.Amount <= 0)
            {
                TempData["Error"] = "Payment amount must be greater than zero.";
                return RedirectToAction("Student_Dashboard", "Home");
            }

            var studentId = model.TransactionReference.Split('_').Last();
            var student = await _context.Students
                .Include(s => s.AcademicYear)
                .Include(s => s.FinancialStatements)
                .FirstOrDefaultAsync(s => s.Id == Int32.Parse(studentId));

            if (student == null)
            {
                TempData["Error"] = "Payment failed. Student not found.";
                return RedirectToAction("/Home/Student_Dashboard");
            }

            // Build a unique, traceable transaction ID for PayBoss
            var txnId = $"STDFEE_MOB_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString()[..8].ToUpper()}";

            try
            {
                // Strip non-digits from phone number
                var phone = new string(model.PhoneNumber.Where(char.IsDigit).ToArray());
                var narration = $"Payment {student.StudentId_Number}";

                if (student == null)
                {
                    _logger.LogWarning("Payment rejected: Neither student nor applicant found with Reg: {Reg}", student.StudentId_Number);
                    return BadRequest(new { Message = "Student/Applicant not found in our system." });
                }

                var names = (student.FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var payment = new OnlinePayments
                {
                    MerchantTransactionId = txnId,
                    FullName = student.FullName,
                    CustomerFirstName = names.Length > 0 ? names[0] : "",
                    CustomerLastName = names.Length > 1 ? names[^1] : "",
                    Msisdn = phone,
                    Phone = phone,
                    AccountNumber = student.StudentId_Number,
                    Amount = model.Amount,
                    CurrencyCode = "ZMW",
                    PaymentMethod = "PayBoss Mobile",
                    RequestPayload = JsonSerializer.Serialize(model),
                    ResponsePayload = JsonSerializer.Serialize(new { Message = "Data Capture Complete" }),
                    Status = "Pending",
                    CreatedAt = DateTime.Now,
                    CallbackPayload = JsonSerializer.Serialize(model),
                    ReferenceNumber = txnId,
                    StudentId = student.Id
                };
                _context.Add(payment);
                await _context.SaveChangesAsync();

                // Step 1 (token obtained automatically in service) + Step 2A
                var result = await _payBoss.CollectMobileAsync(
                    phone: phone,
                    amount: model.Amount,
                    narration: narration,
                    txnId: txnId);

                _logger.LogInformation(
                    "PayBoss mobile initiated | Student: {Id} | TxnId: {TxnId} | Status: {Status}",
                    student.StudentId_Number, txnId, result.Status);

                // Mobile payments are async (USSD push). Redirect to a pending/polling page.
                return RedirectToAction("PaymentPending", new
                {
                    transactionId = txnId,
                    amount = model.Amount,
                    method = "Mobile Money",
                    studentId = student.StudentId_Number
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayBoss mobile payment failed for student {Id}", student.StudentId_Number);
                TempData["Error"] = "Payment could not be initiated. Please try again or contact support.";
                return RedirectToAction("/Home/Student_Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessStudentCardPayment(StudentCardPaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the card details and try again.";
                return RedirectToAction("Student_Dashboard", "Home");
            }

            if (model.Amount <= 0)
            {
                TempData["Error"] = "Payment amount must be greater than zero.";
                return RedirectToAction("Student_Dashboard", "Home");
            }

            var studentId = model.TransactionReference.Split('_').Last();
            var student = await _context.Students
                .Include(s => s.AcademicYear)
                .Include(s => s.FinancialStatements)
                .FirstOrDefaultAsync(s => s.Id == Int32.Parse(studentId));

            if (student == null)
            {
                TempData["Error"] = "Payment failed. Student not found.";
                return RedirectToAction("Student_Dashboard", "Home");
            }

            var txnId = $"STDFEE_CARD_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString()[..8].ToUpper()}";
            var narration = $"Student Fee Payment - {student.StudentId_Number}";

            // PayBoss will redirect back here after the card / 3DS flow
            var callbackUrl = Url.Action("PaymentCallback", "Payments",
                new { txnId, studentId = student.StudentId_Number }, Request.Scheme)!;

            if (student == null)
            {
                _logger.LogWarning("Payment rejected: Neither student nor applicant found with Reg: {Reg}", student.StudentId_Number);
                return BadRequest(new { Message = "Student/Applicant not found in our system." });
            }

            var names = (student.FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var payment = new OnlinePayments
            {
                MerchantTransactionId = txnId,
                FullName = student.FullName,
                CustomerFirstName = names.Length > 0 ? names[0] : "",
                CustomerLastName = names.Length > 1 ? names[^1] : "",
                Msisdn = model.PhoneNumber,
                Phone = model.PhoneNumber,
                AccountNumber = student.StudentId_Number,
                Amount = model.Amount,
                CurrencyCode = "ZMW",
                PaymentMethod = "PayBoss Card",
                RequestPayload = JsonSerializer.Serialize(model),
                ResponsePayload = JsonSerializer.Serialize(new { Message = "Data Capture Complete" }),
                Status = "Pending",
                CreatedAt = DateTime.Now,
                CallbackPayload = JsonSerializer.Serialize(model),
                ReferenceNumber = txnId,
                StudentId = student.Id
            };
            _context.Add(payment);
            await _context.SaveChangesAsync();

            try
            {
                // Step 1 (automatic) + Step 2B
                var result = await _payBoss.CollectCardAsync(
                    model: model,
                    narration: narration,
                    txnId: txnId,
                    redirectUrl: callbackUrl);

                _logger.LogInformation(
                    "PayBoss card submitted | Student: {Id} | TxnId: {TxnId} | RedirectUrl: {Url}",
                    student.StudentId_Number, txnId, result.RedirectUrl);

                // PayBoss returns a redirectUrl to its hosted 3DS card-processing page
                if (!string.IsNullOrEmpty(result.RedirectUrl))
                    return Redirect(result.RedirectUrl);

                TempData["Error"] = "Could not initiate card payment. Please try again.";
                return RedirectToAction("Student_Dashboard", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayBoss card payment failed for student {Id}", student.StudentId_Number);
                TempData["Error"] = "Card payment could not be processed. Please try again or contact support.";
                return RedirectToAction("Student_Dashboard", "Home");
            }
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallback(string txnId, string studentId)
        {
            if (string.IsNullOrEmpty(txnId))
                return RedirectToAction("Student_Dashboard", "Home");

            try
            {
                // Step 3: Status query
                var status = await _payBoss.GetStatusAsync(txnId);

                _logger.LogInformation(
                    "PayBoss callback | TxnId: {TxnId} | Status: {Status} | Code: {Code} | Ref: {Ref}",
                    txnId, status.Status, status.StatusCode, status.ServiceProviderRef);

                if (status.Status.Equals("success", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] = $"Payment successful! Reference: {status.ServiceProviderRef ?? txnId}";
                    return RedirectToAction("StudentPaymentSuccess", new
                    {
                        transactionReference = txnId,
                        providerRef = status.ServiceProviderRef
                    });
                }

                if (status.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = $"Payment failed: {status.ServiceProviderStatusDescription ?? status.Message}";
                    return RedirectToAction("Student_Dashboard", "Home");
                }

                // Still pending
                TempData["Info"] = "Your payment is being processed. We will update you shortly.";
                return RedirectToAction("PaymentPending", new
                {
                    transactionId = txnId,
                    method = "Card",
                    studentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment status for {TxnId}", txnId);
                TempData["Error"] = "Could not verify payment status. Quote reference " + txnId + " when contacting support.";
                return RedirectToAction("Student_Dashboard", "Home");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckPaymentStatus(string txnId)
        {
            try
            {
                var status = await _payBoss.GetStatusAsync(txnId);

                if(string.Equals(status.Status, "successful", StringComparison.OrdinalIgnoreCase) || string.Equals(status.Status, "success", StringComparison.OrdinalIgnoreCase) || string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    var payment = await _context.OnlinePayments.FirstOrDefaultAsync(p => p.ReferenceNumber == txnId);

                    if(payment != null)
                    {
                        if(string.Equals(status.Status, "successful", StringComparison.OrdinalIgnoreCase) || string.Equals(status.Status, "success", StringComparison.OrdinalIgnoreCase))
                        {
                            payment.Status = "Paid";
                        }
                        else
                        {
                            payment.Status = "Failed"; 
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                return Json(new
                {
                    status = status.Status,          // "success" | "failed" | "pending"
                    statusCode = status.StatusCode,
                    message = status.Message,
                    providerRef = status.ServiceProviderRef,
                    description = status.ServiceProviderStatusDescription
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling status for {TxnId}", txnId);
                return Json(new { status = "error", message = "Could not retrieve payment status." });
            }
        }

        [HttpGet]
        public IActionResult PaymentPending(string transactionId, decimal amount, string method, string studentId)
        {
            ViewBag.TransactionId = transactionId;
            ViewBag.Amount = amount;
            ViewBag.Method = method;
            ViewBag.StudentId = studentId;
            return View();
        }

        #endregion

        #region Sponsor Verification - NEW

        /// <summary>
        /// Process Sponsor Verification - Following EXACT same password logic as AccountController
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcessSponsorVerification([FromBody] SponsorVerificationViewModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.StudentId) || string.IsNullOrEmpty(model.Password))
                {
                    return Json(new { success = false, message = "Student ID and Password are required." });
                }

                // ========================================
                // STEP 1: Find the SPONSORING student
                // ========================================
                var sponsorStudent = await _context.Students
                    .FirstOrDefaultAsync(s => s.StudentId_Number == model.StudentId);

                if (sponsorStudent == null)
                {
                    return Json(new { success = false, message = "No student account found with this Student ID. Please check your details." });
                }

                // ========================================
                // STEP 2: Find SPONSOR's user account
                // ========================================
                ApplicationUser sponsorUser = await _userManager.FindByNameAsync(sponsorStudent.Username);

                // DATA CORRECTION: If username doesn't work, try by email
                if (sponsorUser == null)
                {
                    sponsorUser = await _userManager.FindByEmailAsync(sponsorStudent.Email);

                    if (sponsorUser != null)
                    {
                        try
                        {
                            _logger.LogInformation($"🔧 FIXING: Student {sponsorStudent.StudentId_Number} username mismatch - Syncing to '{sponsorUser.UserName}'");
                            sponsorStudent.Username = sponsorUser.UserName;
                            sponsorStudent.UpdatedBy = "System_AutoFix_SponsorVerification";
                            sponsorStudent.UpdatedAt = DateTime.Now;
                            _context.Students.Update(sponsorStudent);
                            await _context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"❌ ERROR fixing student username: {ex.Message}");
                        }
                    }
                }

                if (sponsorUser == null)
                {
                    return Json(new { success = false, message = "User account not found for this student. Please contact support." });
                }

                // ========================================
                // STEP 3: Verify SPONSOR is a student
                // ========================================
                var isStudent = await _userManager.IsInRoleAsync(sponsorUser, "Student");
                if (!isStudent)
                {
                    return Json(new { success = false, message = "The provided credentials do not belong to a student account." });
                }

                // ========================================
                // STEP 4: Verify SPONSOR's password
                // (Same logic as AccountController)
                // ========================================
                var defaultPasswords = new[] { "Student@1234", "Student@2025" };
                bool isDefaultPasswordAttempt = defaultPasswords.Contains(model.Password);
                bool passwordValid = false;

                if (isDefaultPasswordAttempt)
                {
                    // Has sponsor already changed their password?
                    if (sponsorUser.HasChangedInitialPassword)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "You have already set a custom password. The default password is no longer valid. Please use your custom password."
                        });
                    }
                    // Sponsor has NOT changed password - ALLOW default
                    passwordValid = true;
                }
                else
                {
                    // Verify custom password
                    var signInResult = await _signInManager.CheckPasswordSignInAsync(sponsorUser, model.Password, lockoutOnFailure: false);
                    passwordValid = signInResult.Succeeded;

                    if (!passwordValid)
                    {
                        string passwordHint = !sponsorUser.HasChangedInitialPassword
                            ? " If you haven't changed your password yet, you can use the default password: Student@2025"
                            : "";
                        return Json(new
                        {
                            success = false,
                            message = $"Incorrect password. Please check your password and try again.{passwordHint}"
                        });
                    }
                }

                // ========================================
                // STEP 5: Get the APPLICATION
                // ========================================
                var application = await _context.Applicants
                    .Include(a => a.Programme).Include(a => a.School)
                    .FirstOrDefaultAsync(a => a.ReferenceNumber == model.ApplicationReference);

                if (application == null)
                {
                    return Json(new { success = false, message = "Application not found." });
                }

                if (application.PaymentStatus == Status.Completed || application.PaymentStatus == Status.Paid)
                {
                    return Json(new { success = false, message = "This application has already been processed." });
                }

                // ========================================
                // STEP 6: Find the APPLICANT's user account
                // (The person being applied FOR - they have a Candidate account)
                // ========================================
                var applicantUser = await _userManager.FindByEmailAsync(application.Email);

                if (applicantUser == null)
                {
                    _logger.LogWarning($"No user account found for applicant email {application.Email}");
                    return Json(new
                    {
                        success = false,
                        message = "The applicant's user account was not found. Please ensure the application form was completed properly."
                    });
                }

                // ========================================
                // STEP 7: Generate NEW password for APPLICANT
                // ========================================
                string newPassword = GenerateSecurePassword();

                // ========================================
                // STEP 8: Reset the APPLICANT's password
                // (NOT the sponsor's password!)
                // ========================================
                var token = await _userManager.GeneratePasswordResetTokenAsync(applicantUser);
                var resetResult = await _userManager.ResetPasswordAsync(applicantUser, token, newPassword);

                if (!resetResult.Succeeded)
                {
                    var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                    _logger.LogError($"Failed to reset password for applicant {application.Email}: {errors}");
                    return Json(new { success = false, message = $"Failed to set up applicant credentials: {errors}" });
                }

                // Mark that applicant needs to change password on first login
                applicantUser.HasChangedInitialPassword = false;
                await _userManager.UpdateAsync(applicantUser);

                _logger.LogInformation($"✅ Password successfully reset for APPLICANT {application.Email}");

                // ========================================
                // STEP 9: Create payment record
                // ========================================
                string transactionReference = $"ASSISTED_{sponsorStudent.StudentId_Number}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}";

                var payment = new ApplicationPayment
                {
                    ApplicationId = application.ApplicantId,
                    Amount = 0,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = $"Assisted by Student ({sponsorStudent.StudentId_Number} - {sponsorStudent.FullName})",
                    TransactionReference = transactionReference,
                    Status = Status.Completed
                };

                // Update application status
                application.PaymentStatus = Status.Paid;
                application.IsSubmitted = true;

                _context.ApplicationPayments.Add(payment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Application {model.ApplicationReference} assisted by student {sponsorStudent.StudentId_Number}");

                // Send welcome email
                try
                {
                    _backgroundEmailService.QueueApplicationSubmissionEmail(
                        applicantName: application.FullName,
                        applicantEmail: application.Email,
                        programmeName: application.Programme?.Name ?? "N/A",
                        schoolName: application.School?.Name ?? "N/A",
                        referenceNumber: application.ReferenceNumber ?? "RE",
                        paymentAmount: 0,
                        transactionReference: transactionReference
                    );
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, $"Failed to send welcome email to {application.Email}");
                }

                // ========================================
                // STEP 10: Return success with APPLICANT's credentials
                // ========================================
                return Json(new
                {
                    success = true,
                    message = "Verification successful! The application has been submitted.",
                    applicantName = application.FullName,
                    applicantEmail = application.Email,      // APPLICANT's email
                    newPassword = newPassword,                // NEW password for APPLICANT
                    programmeName = application.Programme?.Name ?? "N/A",
                    schoolName = application.School?.Name ?? "N/A",
                    referenceNumber = application.ReferenceNumber,
                    sponsorName = sponsorStudent.FullName,
                    sponsorStudentId = sponsorStudent.StudentId_Number,
                    transactionReference = transactionReference
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing sponsor verification");
                return Json(new { success = false, message = "An error occurred during verification. Please try again." });
            }
        }

        /// <summary>
        /// Generate secure password meeting identity requirements
        /// </summary>
        private string GenerateSecurePassword()
        {
            const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "@#$!";

            var random = new Random();
            var password = new StringBuilder();

            // Ensure at least one of each required character type
            password.Append(upperCase[random.Next(upperCase.Length)]);
            password.Append(lowerCase[random.Next(lowerCase.Length)]);
            password.Append(digits[random.Next(digits.Length)]);
            password.Append(special[random.Next(special.Length)]);

            // Fill remaining with random characters
            const string allChars = upperCase + lowerCase + digits;
            for (int i = 0; i < 6; i++)
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Shuffle the password
            return new string(password.ToString().OrderBy(x => random.Next()).ToArray());
        }

        /// <summary>
        /// Generate PDF with login credentials using PdfSharpCore
        /// </summary>
        [HttpGet]
        public IActionResult DownloadCredentialsPdf(string referenceNumber, string email, string password,
            string applicantName, string programmeName, string sponsorName, string sponsorStudentId)
        {
            try
            {
                var pdfBytes = GenerateCredentialsPdf(applicantName, email, password, programmeName,
                    referenceNumber, sponsorName, sponsorStudentId);

                return File(pdfBytes, "application/pdf", $"LoginCredentials_{referenceNumber}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating credentials PDF");
                TempData["Error"] = "Failed to generate credentials PDF.";
                return RedirectToAction("PaymentSuccess", new { referenceNumber });
            }
        }

        private byte[] GenerateCredentialsPdf(string applicantName, string email, string password,
            string programmeName, string referenceNumber, string sponsorName, string sponsorStudentId)
        {
            using var memoryStream = new MemoryStream();

            var document = new PdfDocument();
            document.Info.Title = "Student Login Credentials";
            document.Info.Author = "Eden University";

            var page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;

            var gfx = XGraphics.FromPdfPage(page);

            // Fonts
            var titleFont = new XFont("Arial", 24, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
            var normalFont = new XFont("Arial", 12, XFontStyle.Regular);
            var smallFont = new XFont("Arial", 10, XFontStyle.Regular);
            var credentialFont = new XFont("Courier New", 14, XFontStyle.Bold);

            // Colors
            var primaryColor = XColor.FromArgb(0, 128, 128);
            var warningColor = XColor.FromArgb(220, 53, 69);
            var successColor = XColor.FromArgb(40, 167, 69);

            double yPosition = 50;
            double leftMargin = 50;
            double pageWidth = page.Width.Point;

            // Header
            gfx.DrawString("EDEN UNIVERSITY", titleFont, new XSolidBrush(primaryColor),
                new XRect(0, yPosition, pageWidth, 30), XStringFormats.TopCenter);
            yPosition += 40;

            gfx.DrawString("Student Login Credentials", headerFont, XBrushes.Black,
                new XRect(0, yPosition, pageWidth, 20), XStringFormats.TopCenter);
            yPosition += 40;

            // Divider line
            gfx.DrawLine(new XPen(primaryColor, 2), leftMargin, yPosition, pageWidth - leftMargin, yPosition);
            yPosition += 20;

            // Application Info Section
            gfx.DrawString("APPLICATION DETAILS", headerFont, new XSolidBrush(primaryColor), leftMargin, yPosition);
            yPosition += 25;

            gfx.DrawString($"Reference Number: {referenceNumber}", normalFont, XBrushes.Black, leftMargin, yPosition);
            yPosition += 20;
            gfx.DrawString($"Applicant Name: {applicantName}", normalFont, XBrushes.Black, leftMargin, yPosition);
            yPosition += 20;
            gfx.DrawString($"Programme: {programmeName}", normalFont, XBrushes.Black, leftMargin, yPosition);
            yPosition += 20;
            gfx.DrawString($"Date: {DateTime.Now:MMMM dd, yyyy}", normalFont, XBrushes.Black, leftMargin, yPosition);
            yPosition += 30;

            // Sponsor Info Section
            gfx.DrawString("SPONSORED BY", headerFont, new XSolidBrush(primaryColor), leftMargin, yPosition);
            yPosition += 25;

            gfx.DrawString($"Student Name: {sponsorName}", normalFont, XBrushes.Black, leftMargin, yPosition);
            yPosition += 20;
            gfx.DrawString($"Student ID: {sponsorStudentId}", normalFont, XBrushes.Black, leftMargin, yPosition);
            yPosition += 40;

            // Credentials Box
            var boxX = leftMargin;
            var boxY = yPosition;
            var boxWidth = pageWidth - (leftMargin * 2);
            var boxHeight = 120;

            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 240, 240)), boxX, boxY, boxWidth, boxHeight);
            gfx.DrawRectangle(new XPen(primaryColor, 2), boxX, boxY, boxWidth, boxHeight);

            yPosition += 15;
            gfx.DrawString("LOGIN CREDENTIALS", headerFont, new XSolidBrush(primaryColor),
                new XRect(0, yPosition, pageWidth, 20), XStringFormats.TopCenter);
            yPosition += 35;

            gfx.DrawString("Email:", normalFont, XBrushes.Black, leftMargin + 20, yPosition);
            gfx.DrawString(email, credentialFont, XBrushes.Black, leftMargin + 100, yPosition);
            yPosition += 30;

            gfx.DrawString("Password:", normalFont, XBrushes.Black, leftMargin + 20, yPosition);
            gfx.DrawString(password, credentialFont, new XSolidBrush(successColor), leftMargin + 100, yPosition);
            yPosition += 50;

            // Warning Section
            yPosition = boxY + boxHeight + 30;
            var warningBoxY = yPosition;
            var warningBoxHeight = 80;

            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 243, 205)), boxX, warningBoxY, boxWidth, warningBoxHeight);
            gfx.DrawRectangle(new XPen(XColor.FromArgb(255, 193, 7), 1), boxX, warningBoxY, boxWidth, warningBoxHeight);

            yPosition += 15;
            gfx.DrawString("⚠ IMPORTANT SECURITY NOTICE", headerFont, new XSolidBrush(warningColor), leftMargin + 20, yPosition);
            yPosition += 25;

            gfx.DrawString("Please change your password immediately after your first login.", normalFont, XBrushes.Black, leftMargin + 20, yPosition);
            yPosition += 20;

            gfx.DrawString("Keep this document secure and do not share your credentials with anyone.", smallFont, XBrushes.Black, leftMargin + 20, yPosition);

            yPosition = warningBoxY + warningBoxHeight + 40;

            // Login Instructions
            gfx.DrawString("HOW TO LOGIN", headerFont, new XSolidBrush(primaryColor), leftMargin, yPosition);
            yPosition += 25;

            gfx.DrawString("1. Visit: https://ecampus.edenuniversity.edu.zm", normalFont, XBrushes.Black, leftMargin + 20, yPosition);
            yPosition += 20;
            gfx.DrawString("2. Enter your email address as shown above", normalFont, XBrushes.Black, leftMargin + 20, yPosition);
            yPosition += 20;
            gfx.DrawString("3. Enter the password provided in this document", normalFont, XBrushes.Black, leftMargin + 20, yPosition);
            yPosition += 20;
            gfx.DrawString("4. Click 'Login' to access your student portal", normalFont, XBrushes.Black, leftMargin + 20, yPosition);
            yPosition += 20;
            gfx.DrawString("5. Change your password from the profile settings", normalFont, XBrushes.Black, leftMargin + 20, yPosition);

            // Footer
            yPosition = page.Height.Point - 60;
            gfx.DrawLine(new XPen(XColors.LightGray, 1), leftMargin, yPosition, pageWidth - leftMargin, yPosition);
            yPosition += 15;

            gfx.DrawString($"Generated on {DateTime.Now:MMMM dd, yyyy 'at' HH:mm}", smallFont, XBrushes.Gray,
                new XRect(0, yPosition, pageWidth, 15), XStringFormats.TopCenter);
            yPosition += 15;
            gfx.DrawString("Eden University - Student Information System", smallFont, XBrushes.Gray,
                new XRect(0, yPosition, pageWidth, 15), XStringFormats.TopCenter);

            document.Save(memoryStream, false);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Sponsor Payment Success Page
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SponsorPaymentSuccess(string referenceNumber, string sponsorStudentId)
        {
            try
            {
                var application = await _context.Applicants
                    .Include(a => a.Programme)
                    .FirstOrDefaultAsync(a => a.ReferenceNumber == referenceNumber);

                if (application == null)
                {
                    TempData["ErrorMessage"] = "Application not found.";
                    return RedirectToAction("Index", "StudentApplication");
                }

                var payment = await _context.ApplicationPayments
                    .Where(p => p.ApplicationId == application.ApplicantId)
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefaultAsync();

                if (payment == null)
                {
                    TempData["ErrorMessage"] = "Payment record not found.";
                    return RedirectToAction("Index", "StudentApplication");
                }

                var viewModel = new PaymentConfirmationViewModel
                {
                    ApplicationReference = referenceNumber,
                    ApplicantName = application.FullName,
                    TransactionReference = payment.TransactionReference,
                    Amount = payment.Amount,
                    PaymentMethod = payment.PaymentMethod,
                    PaymentDate = payment.PaymentDate,
                    ProgrammeName = application.Programme?.Name ?? "N/A",
                    NextSteps = $"Your application has been submitted successfully through sponsorship by student {sponsorStudentId}. Your application is now under review."
                };

                return View("PaymentSuccess", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sponsor payment success page");
                TempData["ErrorMessage"] = "An error occurred.";
                return RedirectToAction("Index", "StudentApplication");
            }
        }

        #endregion

        #region Payment Success Pages

        [HttpGet]
        public async Task<IActionResult> PaymentSuccess(string referenceNumber)
        {
            try
            {
                var application = await _context.Applicants
                    .Include(a => a.Programme)
                    .FirstOrDefaultAsync(a => a.ReferenceNumber == referenceNumber);

                if (application == null)
                {
                    TempData["ErrorMessage"] = "Application not found.";
                    return RedirectToAction("Index", "StudentApplication");
                }

                var payment = await _context.ApplicationPayments
                    .Where(p => p.ApplicationId == application.ApplicantId)
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefaultAsync();

                if (payment == null)
                {
                    TempData["ErrorMessage"] = "Payment record not found.";
                    return RedirectToAction("Index", "StudentApplication");
                }

                var viewModel = new PaymentConfirmationViewModel
                {
                    ApplicationReference = referenceNumber,
                    ApplicantName = application.FullName,
                    TransactionReference = payment.TransactionReference,
                    Amount = payment.Amount,
                    PaymentMethod = payment.PaymentMethod,
                    PaymentDate = payment.PaymentDate,
                    ProgrammeName = application.Programme?.Name ?? "N/A",
                    NextSteps = "Your application has been submitted successfully and is now under review."
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment success page");
                TempData["ErrorMessage"] = "An error occurred.";
                return RedirectToAction("Index", "StudentApplication");
            }
        }

        [HttpGet]
        public async Task<IActionResult> StudentPaymentSelection()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .Include(s => s.Programme)
                    .Include(s => s.School)
                    .Include(s => s.StudentAddress)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student == null)
                {
                    return NotFound("Student record not found.");
                }

                // Calculate total payments
                var currentTotalFees = await _context.StudentInvoices
                        .Where(si => si.StudentId == student.Id && si.AcademicYearId == student.AcademicYearId && si.Semester == student.CurrentSemester)
                        .SumAsync(si => si.TotalAmount);
                if(currentTotalFees <= 0)
                {
                    var applicableFees = GetApplicableFees(student);
                    currentTotalFees = applicableFees.Sum(f => f.Amount);
                }
                
                var totalFees = StudentTools.GetStudentOutstandingBalance(student.Id);
                var totalPaid = currentTotalFees - totalFees;
                var minRegistrationPayment = ((currentTotalFees * student.AcademicYear.MinRegistrationPaymentPercentage) / 100) + (totalFees - currentTotalFees);
                var remainingForRegistration = Math.Max(0, minRegistrationPayment - totalPaid);

                // Check for invoice and generate if missing with reconciliation
                List<FeeDetailViewModel> feeDetails = new();

                var studentInvoice = await _context.StudentInvoices
                    .Include(si => si.InvoiceItems)
                    .FirstOrDefaultAsync(si => si.StudentId == student.Id &&
                                             si.AcademicYearId == student.AcademicYearId &&
                                             (student.Programme.IsSemesterBased == false || si.Semester == student.CurrentSemester));

                if (studentInvoice == null && student.IsRegistered)
                {
                    _logger.LogWarning($"No invoice found for registered student {student.StudentId_Number}. Generating invoice with payment reconciliation...");

                    try
                    {
                        var invoiceResult = await GenerateInvoiceForStudentWithPaymentReconciliation(student, totalPaid);

                        if (invoiceResult.Success)
                        {
                            studentInvoice = await _context.StudentInvoices
                                .Include(si => si.InvoiceItems)
                                .FirstOrDefaultAsync(si => si.StudentId == student.Id &&
                                                         si.AcademicYearId == student.AcademicYearId &&
                                                         (student.Programme.IsSemesterBased == false || si.Semester == student.CurrentSemester));

                            TempData["Info"] = "Your invoice has been regenerated and reconciled with your payment history.";
                        }
                        else
                        {
                            TempData["Warning"] = "Unable to generate your invoice automatically. Using current fee structure.";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error generating invoice for student {student.StudentId_Number}");
                        TempData["Warning"] = "There was an issue generating your invoice. Using current fee structure.";
                    }
                }

                if (studentInvoice != null)
                {
                    feeDetails = studentInvoice.InvoiceItems.Select(item => new FeeDetailViewModel
                    {
                        FeeName = item.FeeTypeName,
                        Description = item.Description,
                        Amount = item.Amount
                    }).ToList();

                    totalFees = studentInvoice.TotalAmount;
                }
                else
                {
                    var applicableFees = GetApplicableFees(student);
                    feeDetails = applicableFees.Select(f => new FeeDetailViewModel
                    {
                        FeeName = f.FeeType.Name,
                        Description = f.FeeType.Description ?? f.FeeType.Name,
                        Amount = f.Amount
                    }).ToList();

                    totalFees = applicableFees.Sum(f => f.Amount);
                }

                // Recalculate values based on updated total fees
                var outstandingBalance = StudentTools.GetStudentOutstandingBalance(student.Id);
                minRegistrationPayment = ((currentTotalFees * student.AcademicYear.MinRegistrationPaymentPercentage) / 100) + (totalFees - currentTotalFees);
                remainingForRegistration = Math.Max(0, minRegistrationPayment - totalPaid);

                var viewModel = new StudentPaymentSelectionViewModel
                {
                    TransactionReference = $"STDFEE_{DateTime.Now:yyyyMMddHHmmss}_{student.StudentId_Number}",
                    Amount = 0,
                    MinRegistrationPayment = minRegistrationPayment,
                    StudentName = student.FullName ?? "Unknown",
                    StudentId = student.StudentId_Number,
                    ProgrammeName = student.Programme?.Name ?? "N/A",
                    SchoolName = student.School?.Name ?? "N/A",
                    Description = "Student Fee Payment",
                    IsRegistered = student.IsRegistered,
                    OutstandingBalance = outstandingBalance,
                    TotalPaid = totalPaid,
                    TotalFees = currentTotalFees,
                    RemainingForRegistration = remainingForRegistration,
                    FeeDetails = feeDetails
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student payment selection page: {Message}", ex.Message);
                TempData["Error"] = "An error occurred while loading the payment page.";
                return RedirectToAction("StudentFinance");
            }
        }

        #endregion

        #region Other Existing Methods

        [HttpGet]
        public async Task<IActionResult> ViewPayments()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                var student = await _context.Students
                    .Include(s => s.AcademicYear).Include(s => s.FinancialStatements)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student == null) return NotFound("Student record not found.");

               var paymentsQuery = _context.OnlinePayments
                    .Where(op => op.StudentId == student.Id && op.Status == "Paid")
                    .Select(p => new UnifiedTransactionDto
                    {
                        Id = p.Id,
                        StudentId = p.StudentId,
                        Amount = p.Amount,
                        Credit = true,
                        Reference = p.ReferenceNumber,
                        AccountingSystemPostStatus = p.AccountingSystemPostStatus,
                        CreatedAt = p.TransactionDate ?? p.CreatedAt,
                        Narration = "Payment",
                        InvoiceItems = null
                    })
                    .ToList();

                var invoices = _context.StudentInvoices
                    .Include(si => si.InvoiceItems)
                    .Where(si => si.StudentId == student.Id)
                    .ToList();

                var invoicesQuery = invoices.Select(i => new UnifiedTransactionDto
                {
                    Id = i.Id,
                    StudentId = i.StudentId,
                    Amount = i.TotalAmount,
                    Credit = false,
                    Reference = i.InvoiceReference,
                    AccountingSystemPostStatus = i.AccountingSystemPostStatus,
                    CreatedAt = i.CreatedDate,
                    Narration = i.InvoiceItems.Any(item => item.FeeTypeName.ToLower().Contains("tuition"))
                                                     ? "Tuition Fees"
                                                     : i.InvoiceItems.Select(item => item.FeeTypeName).FirstOrDefault() ?? "Invoice",
                    InvoiceItems = i.InvoiceItems.ToList()
                }).ToList();

                var unified = paymentsQuery
                    .Concat(invoicesQuery)
                    .OrderBy(x => x.CreatedAt)
                    .ToList();

                var viewModel = new PaymentHistoryViewModel
                {
                    StudentName = student.FullName,
                    StudentId = student.StudentId_Number,
                    AcademicYear = student.AcademicYear.YearValue,
                    Payments = [],
                    TotalPaid = StudentTools.GetStudentTotalPaid(student.Id),
                    FinancialStatement = unified
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment history");
                return RedirectToAction("Error", "Home");
            }
        }

        private async Task<AccountingApiResponse> GenerateInvoiceForStudentWithPaymentReconciliation(Student student, decimal totalPaidForAcademicYear)
        {
            try
            {
                // Get all applicable fees for this student's current programme
                var applicableFees = GetApplicableFees(student);

                if (!applicableFees.Any())
                {
                    return new AccountingApiResponse
                    {
                        Success = false,
                        Message = "No applicable fees found for student's current programme"
                    };
                }

                // Calculate total amount for current programme
                var totalNewProgrammeFees = applicableFees.Sum(f => f.Amount);

                // 🎯 RECONCILIATION LOGIC
                decimal adjustedTotalAmount;
                List<FeeConfiguration> adjustedFees;

                if (totalPaidForAcademicYear > 0)
                {
                    _logger.LogInformation($"Student {student.StudentId_Number} has existing payments of {totalPaidForAcademicYear:C2}. Reconciling with new programme fees of {totalNewProgrammeFees:C2}");

                    if (totalPaidForAcademicYear >= totalNewProgrammeFees)
                    {
                        // Student has paid more than or equal to new programme fees
                        adjustedTotalAmount = totalNewProgrammeFees; // Keep original fees
                        adjustedFees = applicableFees;

                        _logger.LogInformation($"Student {student.StudentId_Number} has overpaid or fully paid new programme fees.");
                    }
                    else
                    {
                        // Student has paid less than new programme fees - normal case
                        adjustedTotalAmount = totalNewProgrammeFees;
                        adjustedFees = applicableFees;

                        _logger.LogInformation($"Student {student.StudentId_Number} needs to pay additional {(totalNewProgrammeFees - totalPaidForAcademicYear):C2} for new programme");
                    }
                }
                else
                {
                    // No previous payments - use full fee amounts
                    adjustedTotalAmount = totalNewProgrammeFees;
                    adjustedFees = applicableFees;
                }

                // Generate unique invoice reference
                var today = DateTime.Now.Date;
                var semesterSuffix = student.Programme?.IsSemesterBased == true ? $"-S{student.CurrentSemester}" : "";
                var invoiceReference = $"INV-{today:yyyyMMdd}-{student.StudentId_Number}{semesterSuffix}-RECON";

                // Build address string
                var address = student.StudentAddress != null
                    ? $"{student.StudentAddress.AddressLine1}, {student.StudentAddress.AddressLine2}, {student.StudentAddress.City}, {student.StudentAddress.State}, {student.StudentAddress.Country}".Trim(' ', ',')
                    : "Address not provided";

                // Post invoice to accounting system
                var result = await _accountingService.PostStudentInvoiceAsync(
                    studentId: student.StudentId_Number,
                    studentName: student.FullName,
                    address: address,
                    email: student.Email,
                    phone: student.Phone,
                    totalAmount: adjustedTotalAmount,
                    fees: adjustedFees
                );

                // Save invoice to database if accounting post was successful
                if (result.Success)
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Create the main invoice record
                        var studentInvoice = new StudentInvoice
                        {
                            StudentId = student.Id,
                            InvoiceReference = invoiceReference,
                            TotalAmount = adjustedTotalAmount,
                            CreatedDate = DateTime.Now,
                            AcademicYearId = student.AcademicYearId,
                            Semester = student.Programme?.IsSemesterBased == true ? student.CurrentSemester : null,
                            Status = Status.Pending
                        };

                        _context.StudentInvoices.Add(studentInvoice);
                        await _context.SaveChangesAsync(); // Save to get the ID

                        // Create invoice items for each fee
                        foreach (var fee in adjustedFees)
                        {
                            var invoiceItem = new StudentInvoiceItem
                            {
                                StudentInvoiceId = studentInvoice.Id,
                                FeeTypeName = fee.FeeType.Name,
                                Description = fee.FeeType.Description ?? fee.FeeType.Name,
                                Amount = fee.Amount, // Use original fee amount
                                FeeConfigurationId = fee.Id
                            };

                            _context.StudentInvoiceItems.Add(invoiceItem);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation($"Successfully saved reconciled invoice {invoiceReference} with {adjustedFees.Count} items to database. Previous payments: {totalPaidForAcademicYear:C2}");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, $"Failed to save reconciled invoice to database for student {student.StudentId_Number}");
                        throw;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating reconciled invoice for student ID: {StudentId}", student.Id);
                return new AccountingApiResponse
                {
                    Success = false,
                    Message = $"An error occurred during invoice reconciliation: {ex.Message}"
                };
            }
        }

        #endregion
    }

    public class TinggResponse
    {
        [JsonPropertyName("checkoutUrl")]
        public string CheckoutUrl { get; set; }
    }

    public class SponsorVerificationViewModel
    {
        public string ApplicationReference { get; set; }
        public string StudentId { get; set; }
        public string Password { get; set; }
    }
}