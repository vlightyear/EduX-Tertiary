// ManualPaymentController.cs - COMPLETE FILE WITH UPDATES
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SIS.Data;
using SIS.Models.Payments;
using SIS.Models.StudentApplication;
using SIS.Services;
using SIS.Services.Accounting;
using SIS.Services.Emails;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    [Route("Admin")]
    [Authorize(Roles = "Admin,Finance")]
    public class ManualPaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ManualPaymentController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPaymentAllocationService _allocationService;

        public ManualPaymentController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<ManualPaymentController> logger,
            IServiceScopeFactory scopeFactory,
            IPaymentAllocationService allocationService)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _allocationService = allocationService;
        }

        [HttpGet("ManualPayment/RebuildAllStudentAllocations")]
        public async Task<IActionResult> RebuildAllStudentAllocations()
        {
            // This would rebuild for ALL students - use with caution!
            var studentIds = await _context.Students
                .Select(s => s.Id)
                .ToListAsync();

            var results = new List<string>();
            var currentUser = User.Identity?.Name ?? "System";

            foreach (var studentId in studentIds)
            {
                var result = await _allocationService.RebuildStudentAllocationsAsync(studentId, currentUser);
                results.Add($"Student {studentId}: {result.Message}");
            }

            ViewBag.RebuildResults = results;
            TempData["SuccessMessage"] = $"Rebuilt allocations for {studentIds.Count} students";

            return View("RebuildResults");
        }

        // GET: Admin/ManualPayments
        [HttpGet("ManualPayments")]
        public async Task<IActionResult> ManualPayments()
        {
            var today = DateTime.Now.Date;
            var todaysPayments = await _context.OnlinePayments
                .Where(p => p.PaymentMethod.Contains("Cash Deposit") 
                         && p.CreatedAt >= today 
                         && p.CreatedAt < today.AddDays(1))
                .OrderByDescending(p => p.CreatedAt)
                .Take(50)
                .ToListAsync();

            return View("~/Views/Admin/ManualPayments.cshtml", todaysPayments);
        }

        // GET: Admin/VerifyStudent/{studentNumber}
        [HttpGet("VerifyStudent/{studentNumber}")]
        public async Task<IActionResult> VerifyStudent(string studentNumber)
        {
            if (string.IsNullOrWhiteSpace(studentNumber))
            {
                return Json(new { exists = false, message = "Student number is required" });
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(s =>
                    s.StudentId_Number == studentNumber ||
                    s.ApplicationReferenceNumber == studentNumber);

            if (student != null)
            {
                return Json(new
                {
                    exists = true,
                    studentName = student.FullName,
                    studentId = student.Id,
                    studentIdNumber = student.StudentId_Number,
                    applicationRef = student.ApplicationReferenceNumber,
                    programme = student.Programme?.Name,
                    isApplicant = student.ApplicationReferenceNumber == studentNumber
                });
            }

            return Json(new { exists = false, message = "Student not found in the system" });
        }

        // GET: Admin/ViewProofOfPayment/{id}
        [HttpGet("ViewProofOfPayment/{id}")]
        public async Task<IActionResult> ViewProofOfPayment(int id)
        {
            var payment = await _context.OnlinePayments
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
            {
                TempData["ErrorMessage"] = "Payment record not found.";
                return RedirectToAction("ManualPayments");
            }

            if (string.IsNullOrWhiteSpace(payment.ProofOfPaymentPath))
            {
                TempData["ErrorMessage"] = "Proof of payment not found for this transaction.";
                return RedirectToAction("ManualPayments");
            }

            return Redirect(payment.ProofOfPaymentPath);
        }

        // POST: Admin/ProcessPayment
        [HttpPost("ProcessPayment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(
            string StudentNumber,
            decimal Amount,
            string ReferenceNumber,
            string ConsumerName,
            DateTime TransactionDate,
            string PaymentMethod,
            IFormFile ProofOfPayment)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(StudentNumber) || 
                    Amount <= 0 || 
                    string.IsNullOrWhiteSpace(ReferenceNumber) || 
                    string.IsNullOrWhiteSpace(ConsumerName) ||
                    string.IsNullOrWhiteSpace(PaymentMethod))
                {
                    TempData["ErrorMessage"] = "All required fields must be completed. Please fill in all fields marked with *.";
                    return RedirectToAction("ManualPayments");
                }

                string popFilePath = null;
                if (ProofOfPayment != null && ProofOfPayment.Length > 0)
                {
                    if (ProofOfPayment.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size exceeds 5MB. Please upload a smaller file.";
                        return RedirectToAction("ManualPayments");
                    }

                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(ProofOfPayment.FileName).ToLowerInvariant();
                    if (!Array.Exists(allowedExtensions, ext => ext == fileExtension))
                    {
                        TempData["ErrorMessage"] = "Invalid file format. Only PDF, JPG, and PNG files are allowed.";
                        return RedirectToAction("ManualPayments");
                    }

                    popFilePath = await SaveProofOfPayment(ProofOfPayment, StudentNumber);
                    _logger.LogInformation($"Proof of payment saved: {popFilePath}");
                }
                else
                {
                    _logger.LogInformation("No proof of payment file provided - proceeding without file");
                }

                var existingPayment = await _context.OnlinePayments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(op => op.ReferenceNumber == ReferenceNumber);

                if (existingPayment != null)
                {
                    _logger.LogWarning("Duplicate payment detected with referenceNo: {ReferenceNo}", ReferenceNumber);
                    TempData["ErrorMessage"] = $"Payment with reference number '{ReferenceNumber}' already exists in the system. Please verify and try again.";
                    return RedirectToAction("ManualPayments");
                }

                _logger.LogInformation("Looking up student for consumerNo: {ConsumerNo}", StudentNumber);
                var student = await _context.Students
                    .Include(s => s.Programme)
                    .FirstOrDefaultAsync(s =>
                        s.StudentId_Number == StudentNumber ||
                        s.ApplicationReferenceNumber == StudentNumber);

                if (student == null)
                {
                    _logger.LogWarning("No student record found for consumerNo: {ConsumerNo}", StudentNumber);
                    TempData["ErrorMessage"] = $"Student with number '{StudentNumber}' not found in the system. Please verify the student number and try again.";
                    return RedirectToAction("ManualPayments");
                }

                string txnId = GenerateTransactionId();

                var payment = new OnlinePayments
                {
                    MerchantTransactionId = txnId,
                    FullName = ConsumerName,
                    CustomerFirstName = ConsumerName,
                    CustomerLastName = "",
                    Msisdn = StudentNumber,
                    Phone = StudentNumber,
                    AccountNumber = StudentNumber,
                    Amount = Amount,
                    CurrencyCode = "ZMW",
                    PaymentMethod = PaymentMethod,
                    Status = "Paid",
                    CreatedAt = DateTime.Now,
                    TransactionDate = TransactionDate,
                    ReferenceNumber = ReferenceNumber,
                    StudentId = 0,
                    ApplicantId = null,
                    PostedBy = User.Identity?.Name ?? "Admin",
                    ProofOfPaymentPath = popFilePath,
                    CallbackPayload = $"Manual Entry by {User.Identity?.Name ?? "Admin"} on {DateTime.Now:yyyy-MM-dd HH:mm:ss}. Bank: {PaymentMethod}, Reference: {ReferenceNumber}, Amount: K{Amount:N2}"
                };

                _logger.LogInformation("Found student record: {StudentId}", student.Id);
                if (student.ApplicationReferenceNumber == StudentNumber)
                {
                    payment.ApplicantId = student.Id;
                    _logger.LogInformation("Matched ApplicationReferenceNumber. Set ApplicantId to {Id}", student.Id);
                }
                else if (student.StudentId_Number == StudentNumber)
                {
                    payment.StudentId = student.Id;
                    _logger.LogInformation("Matched StudentId_Number. Set StudentId to {Id}", student.Id);
                }

                _logger.LogInformation("Saving manual payment to OnlinePayments");
                _context.OnlinePayments.Add(payment);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Manual payment saved successfully. OnlinePayment ID: {Id}, Transaction ID: {TxnId}", payment.Id, txnId);

                var currentUser = User.Identity?.Name ?? "System";
                var result = await _allocationService.RebuildStudentAllocationsAsync(payment.StudentId, currentUser);

                if (payment.ApplicantId.HasValue || payment.StudentId > 0)
                {
                    _logger.LogInformation("Queueing background processing for manual payment");
                    _ = Task.Run(async () =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var services = scope.ServiceProvider;

                        try
                        {
                            var processor = new PaymentProcessor(
                                services.GetRequiredService<ApplicationDbContext>(),
                                services.GetRequiredService<ILogger<PaymentProcessor>>(),
                                services.GetService<IAccountingService>(),
                                services.GetService<IBackgroundEmailService>());

                            if (payment.ApplicantId.HasValue)
                            {
                                _logger.LogInformation("Processing applicant payment for manual entry");
                                await processor.ProcessApplicantPayment(payment);
                            }

                            if (payment.StudentId > 0)
                            {
                                _logger.LogInformation("Processing student payment for manual entry");
                                await processor.ProcessSuccessfulPayment(payment);
                            }
                        }
                        catch (Exception ex)
                        {
                            var logger = services.GetRequiredService<ILogger<ManualPaymentController>>();
                            logger.LogError(ex, "Background processing failed for manual payment {PaymentId}", payment.Id);
                        }
                    });
                }

                var paymentData = new
                {
                    studentName = ConsumerName,
                    studentNumber = StudentNumber,
                    amount = Amount,
                    reference = ReferenceNumber,
                    transactionId = txnId,
                    bank = PaymentMethod,
                    date = TransactionDate.ToString("o"),
                    postedBy = User.Identity?.Name ?? "Admin"
                };
                
                TempData["LastPaymentData"] = JsonConvert.SerializeObject(paymentData);
                TempData["SuccessMessage"] = $"Payment submitted successfully! Transaction ID: {txnId} | Student: {student.FullName} | Amount: K{Amount:N2}. The payment is being processed.";
                return RedirectToAction("ManualPayments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing manual payment");
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}. Please contact support if the problem persists.";
                return RedirectToAction("ManualPayments");
            }
        }

        // POST: Admin/ProcessGroupPayment
        [HttpPost("ProcessGroupPayment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessGroupPayment(
            string GroupPaymentReference,
            string PaymentMethod,
            DateTime TransactionDate,
            string StudentNumbers,
            string StudentAmounts,
            IFormFile ProofOfPayment)
        {
            try
            {
                var studentNums = StudentNumbers?.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();
                var amounts = StudentAmounts?.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();

                if (studentNums.Length == 0 || studentNums.Length != amounts.Length)
                {
                    TempData["ErrorMessage"] = "Invalid student data. Please ensure all students have number and amount.";
                    return RedirectToAction("ManualPayments");
                }

                // Validate and save proof of payment file
                string popFilePath = null;
                if (ProofOfPayment != null && ProofOfPayment.Length > 0)
                {
                    if (ProofOfPayment.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size exceeds 5MB. Please upload a smaller file.";
                        return RedirectToAction("ManualPayments");
                    }

                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(ProofOfPayment.FileName).ToLowerInvariant();
                    if (!Array.Exists(allowedExtensions, ext => ext == fileExtension))
                    {
                        TempData["ErrorMessage"] = "Invalid file format. Only PDF, JPG, and PNG files are allowed.";
                        return RedirectToAction("ManualPayments");
                    }

                    popFilePath = await SaveProofOfPayment(ProofOfPayment, $"GROUP-{DateTime.Now:yyyyMMddHHmmss}");
                    _logger.LogInformation($"Group proof of payment saved: {popFilePath}");
                }

                var timestamp = DateTime.Now;
                var groupRef = string.IsNullOrEmpty(GroupPaymentReference)
                    ? $"GRP-{timestamp:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
                    : GroupPaymentReference;

                var payments = new List<OnlinePayments>();
                decimal totalAmount = 0;

                for (int i = 0; i < studentNums.Length; i++)
                {
                    var studentNumber = studentNums[i].Trim();
                    var amount = decimal.Parse(amounts[i]);

                    var student = await _context.Students
                        .FirstOrDefaultAsync(s =>
                            s.StudentId_Number == studentNumber ||
                            s.ApplicationReferenceNumber == studentNumber);

                    if (student == null)
                    {
                        TempData["ErrorMessage"] = $"Student '{studentNumber}' not found. Group payment cancelled.";
                        return RedirectToAction("ManualPayments");
                    }

                    var txnId = GenerateTransactionId();
                    var payment = new OnlinePayments
                    {
                        MerchantTransactionId = txnId,
                        FullName = student.FullName,
                        CustomerFirstName = student.FullName,
                        CustomerLastName = "",
                        Msisdn = studentNumber,
                        Phone = studentNumber,
                        AccountNumber = studentNumber,
                        Amount = amount,
                        CurrencyCode = "ZMW",
                        PaymentMethod = PaymentMethod,
                        Status = "Paid",
                        CreatedAt = timestamp,
                        TransactionDate = TransactionDate,
                        ReferenceNumber = $"{groupRef}-{i + 1}",
                        StudentId = student.Id,
                        ApplicantId = null,
                        PostedBy = User.Identity?.Name ?? "Admin",
                        ProofOfPaymentPath = popFilePath,
                        GroupPaymentReference = groupRef,
                        IsGroupPayment = "1",
                        CallbackPayload = $"Group Payment by {User.Identity?.Name ?? "Admin"} on {timestamp:yyyy-MM-dd HH:mm:ss}. Bank: {PaymentMethod}, Group Ref: {groupRef}, Amount: K{amount:N2}"
                    };

                    if (student.ApplicationReferenceNumber == studentNumber)
                    {
                        payment.ApplicantId = student.Id;
                    }
                    else if (student.StudentId_Number == studentNumber)
                    {
                        payment.StudentId = student.Id;
                    }

                    payments.Add(payment);
                    totalAmount += amount;
                }

                _logger.LogInformation($"Saving group payment with {payments.Count} students. Group Ref: {groupRef}");
                await _context.OnlinePayments.AddRangeAsync(payments);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Group payment saved successfully. Total Amount: K{totalAmount:N2}");

                var currentUser = User.Identity?.Name ?? "System";
                foreach (var payment in payments)
                {
                    var result = await _allocationService.RebuildStudentAllocationsAsync(payment.StudentId, currentUser);
                }

                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var services = scope.ServiceProvider;

                    try
                    {
                        var processor = new PaymentProcessor(
                            services.GetRequiredService<ApplicationDbContext>(),
                            services.GetRequiredService<ILogger<PaymentProcessor>>(),
                            services.GetService<IAccountingService>(),
                            services.GetService<IBackgroundEmailService>());

                        foreach (var payment in payments)
                        {
                            if (payment.ApplicantId.HasValue)
                            {
                                await processor.ProcessApplicantPayment(payment);
                            }

                            if (payment.StudentId > 0)
                            {
                                await processor.ProcessSuccessfulPayment(payment);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = services.GetRequiredService<ILogger<ManualPaymentController>>();
                        logger.LogError(ex, "Background processing failed for group payment {GroupRef}", groupRef);
                    }
                });

                TempData["SuccessMessage"] = $"Group payment processed successfully! Group Reference: {groupRef} | {payments.Count} students | Total Amount: K{totalAmount:N2}. The payments are being processed.";
                return RedirectToAction("ManualPayments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing group payment");
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}. Please contact support if the problem persists.";
                return RedirectToAction("ManualPayments");
            }
        }

        // GET: Admin/GetPaymentsList
        [HttpGet("GetPaymentsList")]
        public async Task<IActionResult> GetPaymentsList(
            int pageNumber = 1,
            int pageSize = 10,
            string searchTerm = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string status = null)
        {
            try
            {
                var query = _context.OnlinePayments.AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(p => p.TransactionDate >= startDate.Value);

                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(p => p.TransactionDate <= end);
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(p =>
                        p.AccountNumber.Contains(searchTerm) ||
                        p.FullName.Contains(searchTerm) ||
                        p.MerchantTransactionId.Contains(searchTerm) ||
                        p.GroupPaymentReference.Contains(searchTerm));
                }

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(p => p.Status == status);

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(p => p.TransactionDate)
                    .ThenByDescending(p => p.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Json(new
                {
                    items = items,
                    totalCount = totalCount,
                    pageNumber = pageNumber,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    hasPreviousPage = pageNumber > 1,
                    hasNextPage = pageNumber < (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments list");
                return Json(new { error = "Error retrieving payments" });
            }
        }

        private async Task<string> SaveProofOfPayment(IFormFile file, string identifier)
        {
            try
            {
                var yearMonth = DateTime.Now.ToString("yyyy/MM");
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "payments", yearMonth);
                Directory.CreateDirectory(uploadsFolder);

                var fileExtension = Path.GetExtension(file.FileName);
                var fileName = $"{identifier}_{DateTime.Now:yyyyMMdd_HHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                _logger.LogInformation($"File saved successfully: {fileName}");
                return $"/uploads/payments/{yearMonth}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving proof of payment file");
                throw new Exception("Failed to save proof of payment file", ex);
            }
        }

        private string GenerateTransactionId()
        {
            var random = new Random();
            var txnId = $"MAN{random.Next(100000, 999999)}";
            _logger.LogInformation($"Generated manual transaction ID: {txnId}");
            return txnId;
        }
    }
}