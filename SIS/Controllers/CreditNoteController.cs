// CreditNoteController.cs - COMPLETE FILE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
using SIS.Services.Accounting;
using SIS.Services.Emails;

namespace SIS.Controllers
{
    [Route("Admin")]
    [Authorize(Roles = "Admin,Finance")]
    public class CreditNoteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CreditNoteController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public CreditNoteController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<CreditNoteController> logger,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        // GET: Admin/CreditNotes
        [HttpGet("CreditNotes")]
        public async Task<IActionResult> CreditNotes()
        {
            var today = DateTime.Now.Date;
            var todaysCreditNotes = await _context.OnlinePayments
                .Where(p => p.TransactionType == "CRN" 
                         && p.CreatedAt >= today 
                         && p.CreatedAt < today.AddDays(1))
                .OrderByDescending(p => p.CreatedAt)
                .Take(50)
                .ToListAsync();

            return View("~/Views/Admin/CreditNotes.cshtml", todaysCreditNotes);
        }


        [HttpGet("VerifyStudentForCreditNote/{studentNumber}")]
        public async Task<IActionResult> VerifyStudentForCreditNote(string studentNumber)
        {
            if (string.IsNullOrWhiteSpace(studentNumber))
            {
                return Json(new { exists = false, message = "Student number is required" });
            }

            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.StudentId_Number == studentNumber ||
                    s.ApplicationReferenceNumber == studentNumber);

            if (student != null)
            {

                var invoices = await _context.StudentInvoices
                    .AsNoTracking()
                    .Where(si => si.StudentId == student.Id)
                    .OrderByDescending(si => si.CreatedDate)
                    .ToListAsync();

                return Json(new
                {
                    exists = true,
                    studentName = student.FullName,
                    studentId = student.Id,
                    studentIdNumber = student.StudentId_Number,
                    applicationRef = student.ApplicationReferenceNumber,
                    programme = student.Programme?.Name,
                    isApplicant = student.ApplicationReferenceNumber == studentNumber,
                    invoices = invoices
                });
            }

            return Json(new { exists = false, message = "Student not found in the system" });
        }

        // GET: Admin/ViewCreditNoteDocument/{id}
        [HttpGet("ViewCreditNoteDocument/{id}")]
        public async Task<IActionResult> ViewCreditNoteDocument(int id)
        {
            var creditNote = await _context.OnlinePayments
                .FirstOrDefaultAsync(p => p.Id == id && p.TransactionType == "CRN");

            if (creditNote == null)
            {
                TempData["ErrorMessage"] = "Credit note record not found.";
                return RedirectToAction("CreditNotes");
            }

            if (string.IsNullOrWhiteSpace(creditNote.ProofOfPaymentPath))
            {
                TempData["ErrorMessage"] = "Supporting document not found for this credit note.";
                return RedirectToAction("CreditNotes");
            }

            return Redirect(creditNote.ProofOfPaymentPath);
        }

        // POST: Admin/ProcessCreditNote
        [HttpPost("ProcessCreditNote")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCreditNote(
            string StudentNumber,
            decimal Amount,
            string ReferenceNumber,
            string ConsumerName,
            DateTime TransactionDate,
            string Reason,
            int InvoiceId,
            IFormFile ProofOfDocument)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(StudentNumber) || 
                    Amount <= 0 || 
                    string.IsNullOrWhiteSpace(ReferenceNumber) || 
                    string.IsNullOrWhiteSpace(ConsumerName) ||
                    string.IsNullOrWhiteSpace(Reason))
                {
                    TempData["ErrorMessage"] = "All required fields must be completed. Please fill in all fields marked with *.";
                    return RedirectToAction("CreditNotes");
                }

                string documentFilePath = null;
                if (ProofOfDocument != null && ProofOfDocument.Length > 0)
                {
                    if (ProofOfDocument.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size exceeds 5MB. Please upload a smaller file.";
                        return RedirectToAction("CreditNotes");
                    }

                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(ProofOfDocument.FileName).ToLowerInvariant();
                    if (!Array.Exists(allowedExtensions, ext => ext == fileExtension))
                    {
                        TempData["ErrorMessage"] = "Invalid file format. Only PDF, JPG, and PNG files are allowed.";
                        return RedirectToAction("CreditNotes");
                    }

                    documentFilePath = await SaveCreditNoteDocument(ProofOfDocument, StudentNumber);
                    _logger.LogInformation($"Credit note document saved: {documentFilePath}");
                }
                else
                {
                    _logger.LogInformation("No supporting document provided - proceeding without file");
                }

                var existingCreditNote = await _context.OnlinePayments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(op => op.ReferenceNumber == ReferenceNumber);

                if (existingCreditNote != null)
                {
                    _logger.LogWarning("Duplicate credit note detected with referenceNo: {ReferenceNo}", ReferenceNumber);
                    TempData["ErrorMessage"] = $"Credit note with reference number '{ReferenceNumber}' already exists in the system. Please verify and try again.";
                    return RedirectToAction("CreditNotes");
                }

                _logger.LogInformation("Looking up student for credit note: {ConsumerNo}", StudentNumber);
                var student = await _context.Students
                    .Include(s => s.Programme)
                    .FirstOrDefaultAsync(s =>
                        s.StudentId_Number == StudentNumber ||
                        s.ApplicationReferenceNumber == StudentNumber);

                if (student == null)
                {
                    _logger.LogWarning("No student record found for consumerNo: {ConsumerNo}", StudentNumber);
                    TempData["ErrorMessage"] = $"Student with number '{StudentNumber}' not found in the system. Please verify the student number and try again.";
                    return RedirectToAction("CreditNotes");
                }

                string txnId = GenerateCreditNoteTransactionId();

                var creditNote = new OnlinePayments
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
                    PaymentMethod = "Credit Note",
                    Status = "Paid",
                    CreatedAt = DateTime.Now,
                    TransactionDate = TransactionDate,
                    ReferenceNumber = ReferenceNumber,
                    StudentId = 0,
                    ApplicantId = null,
                    PostedBy = User.Identity?.Name ?? "Admin",
                    ProofOfPaymentPath = documentFilePath,
                    TransactionType = "CRN",
                    StudentInvoiceId = InvoiceId,
                    CallbackPayload = $"Manual Credit Note by {User.Identity?.Name ?? "Admin"} on {DateTime.Now:yyyy-MM-dd HH:mm:ss}. Reason: {Reason}, Reference: {ReferenceNumber}, Amount: K{Amount:N2}"
                };

                _logger.LogInformation("Found student record: {StudentId}", student.Id);
                if (student.ApplicationReferenceNumber == StudentNumber)
                {
                    creditNote.ApplicantId = student.Id;
                    _logger.LogInformation("Matched ApplicationReferenceNumber. Set ApplicantId to {Id}", student.Id);
                }
                else if (student.StudentId_Number == StudentNumber)
                {
                    creditNote.StudentId = student.Id;
                    _logger.LogInformation("Matched StudentId_Number. Set StudentId to {Id}", student.Id);
                }

                _logger.LogInformation("Saving manual credit note to OnlinePayments");
                _context.OnlinePayments.Add(creditNote);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Manual credit note saved successfully. OnlinePayment ID: {Id}, Transaction ID: {TxnId}", creditNote.Id, txnId);

                if (creditNote.ApplicantId.HasValue || creditNote.StudentId > 0)
                {
                    _logger.LogInformation("Queueing background processing for manual credit note");
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

                            if (creditNote.ApplicantId.HasValue)
                            {
                                _logger.LogInformation("Processing applicant credit note for manual entry");
                                await processor.ProcessApplicantPayment(creditNote);
                            }

                            if (creditNote.StudentId > 0)
                            {
                                _logger.LogInformation("Processing student credit note for manual entry");
                                await processor.ProcessSuccessfulPayment(creditNote);
                            }
                        }
                        catch (Exception ex)
                        {
                            var logger = services.GetRequiredService<ILogger<CreditNoteController>>();
                            logger.LogError(ex, "Background processing failed for manual credit note {CreditNoteId}", creditNote.Id);
                        }
                    });
                }

                var creditNoteData = new
                {
                    studentName = ConsumerName,
                    studentNumber = StudentNumber,
                    amount = Amount,
                    reference = ReferenceNumber,
                    transactionId = txnId,
                    reason = Reason,
                    date = TransactionDate.ToString("o"),
                    postedBy = User.Identity?.Name ?? "Admin",
                    isCreditNote = true
                };
                
                TempData["LastCreditNoteData"] = JsonConvert.SerializeObject(creditNoteData);
                TempData["SuccessMessage"] = $"Credit note submitted successfully! Transaction ID: {txnId} | Student: {student.FullName} | Amount: K{Amount:N2}. The credit note is being processed.";
                return RedirectToAction("CreditNotes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing manual credit note");
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}. Please contact support if the problem persists.";
                return RedirectToAction("CreditNotes");
            }
        }

        // POST: Admin/ProcessGroupCreditNote
        [HttpPost("ProcessGroupCreditNote")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessGroupCreditNote(
            string GroupCreditNoteReference,
            string Reason,
            DateTime TransactionDate,
            string StudentNumbers,
            string StudentAmounts,
            IFormFile ProofOfDocument)
        {
            try
            {
                var studentNums = StudentNumbers?.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();
                var amounts = StudentAmounts?.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();

                if (studentNums.Length == 0 || studentNums.Length != amounts.Length)
                {
                    TempData["ErrorMessage"] = "Invalid student data. Please ensure all students have number and amount.";
                    return RedirectToAction("CreditNotes");
                }

                string documentFilePath = null;
                if (ProofOfDocument != null && ProofOfDocument.Length > 0)
                {
                    if (ProofOfDocument.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size exceeds 5MB. Please upload a smaller file.";
                        return RedirectToAction("CreditNotes");
                    }

                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(ProofOfDocument.FileName).ToLowerInvariant();
                    if (!Array.Exists(allowedExtensions, ext => ext == fileExtension))
                    {
                        TempData["ErrorMessage"] = "Invalid file format. Only PDF, JPG, and PNG files are allowed.";
                        return RedirectToAction("CreditNotes");
                    }

                    documentFilePath = await SaveCreditNoteDocument(ProofOfDocument, $"GROUP-CRN-{DateTime.Now:yyyyMMddHHmmss}");
                    _logger.LogInformation($"Group credit note document saved: {documentFilePath}");
                }

                var timestamp = DateTime.Now;
                var groupRef = string.IsNullOrEmpty(GroupCreditNoteReference)
                    ? $"GRP-CRN-{timestamp:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
                    : GroupCreditNoteReference;

                var creditNotes = new List<OnlinePayments>();
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
                        TempData["ErrorMessage"] = $"Student '{studentNumber}' not found. Group credit note cancelled.";
                        return RedirectToAction("CreditNotes");
                    }

                    var txnId = GenerateCreditNoteTransactionId();
                    var creditNote = new OnlinePayments
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
                        PaymentMethod = "Credit Note",
                        Status = "Paid",
                        CreatedAt = timestamp,
                        TransactionDate = TransactionDate,
                        ReferenceNumber = $"{groupRef}-{i + 1}",
                        StudentId = 0,
                        ApplicantId = null,
                        PostedBy = User.Identity?.Name ?? "Admin",
                        ProofOfPaymentPath = documentFilePath,
                        GroupPaymentReference = groupRef,
                        IsGroupPayment = "1",
                        TransactionType = "CRN",
                        CallbackPayload = $"Group Credit Note by {User.Identity?.Name ?? "Admin"} on {timestamp:yyyy-MM-dd HH:mm:ss}. Reason: {Reason}, Group Ref: {groupRef}, Amount: K{amount:N2}"
                    };

                    if (student.ApplicationReferenceNumber == studentNumber)
                    {
                        creditNote.ApplicantId = student.Id;
                    }
                    else if (student.StudentId_Number == studentNumber)
                    {
                        creditNote.StudentId = student.Id;
                    }

                    creditNotes.Add(creditNote);
                    totalAmount += amount;
                }

                _logger.LogInformation($"Saving group credit note with {creditNotes.Count} students. Group Ref: {groupRef}");
                await _context.OnlinePayments.AddRangeAsync(creditNotes);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Group credit note saved successfully. Total Amount: K{totalAmount:N2}");

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

                        foreach (var creditNote in creditNotes)
                        {
                            if (creditNote.ApplicantId.HasValue)
                            {
                                await processor.ProcessApplicantPayment(creditNote);
                            }

                            if (creditNote.StudentId > 0)
                            {
                                await processor.ProcessSuccessfulPayment(creditNote);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = services.GetRequiredService<ILogger<CreditNoteController>>();
                        logger.LogError(ex, "Background processing failed for group credit note {GroupRef}", groupRef);
                    }
                });

                TempData["SuccessMessage"] = $"Group credit note processed successfully! Group Reference: {groupRef} | {creditNotes.Count} students | Total Amount: K{totalAmount:N2}. The credit notes are being processed.";
                return RedirectToAction("CreditNotes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing group credit note");
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}. Please contact support if the problem persists.";
                return RedirectToAction("CreditNotes");
            }
        }

        // GET: Admin/GetCreditNotesList
        [HttpGet("GetCreditNotesList")]
        public async Task<IActionResult> GetCreditNotesList(
            int pageNumber = 1,
            int pageSize = 10,
            string searchTerm = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string status = null)
        {
            try
            {
                var query = _context.OnlinePayments
                    .Where(p => p.TransactionType == "CRN")
                    .AsQueryable();

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
                _logger.LogError(ex, "Error retrieving credit notes list");
                return Json(new { error = "Error retrieving credit notes" });
            }
        }

        private async Task<string> SaveCreditNoteDocument(IFormFile file, string identifier)
        {
            try
            {
                var yearMonth = DateTime.Now.ToString("yyyy/MM");
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "creditnotes", yearMonth);
                Directory.CreateDirectory(uploadsFolder);

                var fileExtension = Path.GetExtension(file.FileName);
                var fileName = $"CRN_{identifier}_{DateTime.Now:yyyyMMdd_HHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                _logger.LogInformation($"Credit note document saved successfully: {fileName}");
                return $"/uploads/creditnotes/{yearMonth}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving credit note document");
                throw new Exception("Failed to save credit note document", ex);
            }
        }

        private string GenerateCreditNoteTransactionId()
        {
            var random = new Random();
            var txnId = $"CRN{random.Next(100000, 999999)}";
            _logger.LogInformation($"Generated credit note transaction ID: {txnId}");
            return txnId;
        }
    }
}