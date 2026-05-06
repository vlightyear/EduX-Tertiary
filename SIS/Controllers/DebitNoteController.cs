// DebitNoteController.cs - COMPLETE FILE
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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SIS.Data;
using SIS.Models.Payments;
using SIS.Enums;

namespace SIS.Controllers
{
    [Route("Admin")]
    [Authorize(Roles = "Admin,Finance")]
    public class DebitNoteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DebitNoteController> _logger;

        public DebitNoteController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<DebitNoteController> logger)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("DebitNotes")]
        public async Task<IActionResult> DebitNotes()
        {
            var today = DateTime.Now.Date;
            var todaysDebitNotes = await _context.StudentInvoices
                .Include(i => i.Student)
                .Include(i => i.AcademicYear)
                .Where(i => i.TransactionType == "DN" 
                         && i.CreatedAt >= today 
                         && i.CreatedAt < today.AddDays(1))
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View("~/Views/Admin/DebitNotes.cshtml", todaysDebitNotes);
        }

        [HttpGet("VerifyStudentForDebitNote/{studentNumber}")]
        public async Task<IActionResult> VerifyStudentForDebitNote(string studentNumber)
        {
            if (string.IsNullOrWhiteSpace(studentNumber))
            {
                return Json(new { exists = false, message = "Student number is required" });
            }

            var student = await _context.Students
                .Include(s => s.Programme)
                .Include(s => s.OnlinePayments
                    .Where(p => p.Status == "Paid")
                    .OrderByDescending(p => p.CreatedAt))
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
                    isApplicant = student.ApplicationReferenceNumber == studentNumber,
                    Payments = student.OnlinePayments
                });
            }

            return Json(new { exists = false, message = "Student not found in the system" });
        }

        [HttpGet("ViewDebitNoteDocument/{id}")]
        public async Task<IActionResult> ViewDebitNoteDocument(int id)
        {
            var debitNote = await _context.StudentInvoices
                .FirstOrDefaultAsync(i => i.Id == id && i.TransactionType == "DN");

            if (debitNote == null)
            {
                TempData["ErrorMessage"] = "Debit note record not found.";
                return RedirectToAction("DebitNotes");
            }

            if (string.IsNullOrWhiteSpace(debitNote.Description))
            {
                TempData["ErrorMessage"] = "Supporting document not found for this debit note.";
                return RedirectToAction("DebitNotes");
            }

            return Redirect(debitNote.Description);
        }

        [HttpPost("ProcessDebitNote")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessDebitNote(
            string StudentNumber,
            decimal Amount,
            string ReferenceNumber,
            string ConsumerName,
            DateTime TransactionDate,
            string Reason,
            int AcademicYearId,
            int? Semester,
            int OnlinePaymentId,
            IFormFile ProofOfDocument)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(StudentNumber) || 
                    Amount <= 0 || 
                    string.IsNullOrWhiteSpace(ReferenceNumber) || 
                    string.IsNullOrWhiteSpace(ConsumerName) ||
                    string.IsNullOrWhiteSpace(Reason) ||
                    AcademicYearId <= 0)
                {
                    TempData["ErrorMessage"] = "All required fields must be completed. Please fill in all fields marked with *.";
                    return RedirectToAction("DebitNotes");
                }

                string documentFilePath = null;
                if (ProofOfDocument != null && ProofOfDocument.Length > 0)
                {
                    if (ProofOfDocument.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size exceeds 5MB. Please upload a smaller file.";
                        return RedirectToAction("DebitNotes");
                    }

                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(ProofOfDocument.FileName).ToLowerInvariant();
                    if (!Array.Exists(allowedExtensions, ext => ext == fileExtension))
                    {
                        TempData["ErrorMessage"] = "Invalid file format. Only PDF, JPG, and PNG files are allowed.";
                        return RedirectToAction("DebitNotes");
                    }

                    documentFilePath = await SaveDebitNoteDocument(ProofOfDocument, StudentNumber);
                    _logger.LogInformation($"Debit note document saved: {documentFilePath}");
                }

                var existingDebitNote = await _context.StudentInvoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.InvoiceReference == ReferenceNumber);

                if (existingDebitNote != null)
                {
                    _logger.LogWarning("Duplicate debit note detected with referenceNo: {ReferenceNo}", ReferenceNumber);
                    TempData["ErrorMessage"] = $"Debit note with reference number '{ReferenceNumber}' already exists in the system. Please verify and try again.";
                    return RedirectToAction("DebitNotes");
                }

                _logger.LogInformation("Looking up student for debit note: {ConsumerNo}", StudentNumber);
                var student = await _context.Students
                    .Include(s => s.Programme)
                    .FirstOrDefaultAsync(s =>
                        s.StudentId_Number == StudentNumber ||
                        s.ApplicationReferenceNumber == StudentNumber);

                if (student == null)
                {
                    _logger.LogWarning("No student record found for studentNo: {ConsumerNo}", StudentNumber);
                    TempData["ErrorMessage"] = $"Student with number '{StudentNumber}' not found in the system. Please verify the student number and try again.";
                    return RedirectToAction("DebitNotes");
                }

                var debitNote = new StudentInvoice
                {
                    StudentId = student.Id,
                    InvoiceReference = ReferenceNumber,
                    TotalAmount = Amount,
                    CreatedDate = TransactionDate,
                    AcademicYearId = AcademicYearId,
                    Status = Status.Pending,
                    TransactionType = "DN",
                    YearPeriodId = Semester,
                    Description = !string.IsNullOrWhiteSpace(documentFilePath) ? documentFilePath : $"Debit Note: {Reason}",
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name ?? "Admin",
                    AccountingSystemPostStatus = "Pending"
                };

                _logger.LogInformation("Saving manual debit note to StudentInvoices");
                _context.StudentInvoices.Add(debitNote);
                await _context.SaveChangesAsync();

                var payment = _context.OnlinePayments.FirstOrDefault(p => p.Id == OnlinePaymentId);

                if(payment != null)
                {
                    payment.StudentInvoiceId = debitNote.Id;
                    _context.OnlinePayments.Update(payment);
                    await _context.SaveChangesAsync();
                    
                }

                _logger.LogInformation("Manual debit note saved successfully. Invoice ID: {Id}, Reference: {Ref}", debitNote.Id, ReferenceNumber);

                var debitNoteData = new
                {
                    studentName = ConsumerName,
                    studentNumber = StudentNumber,
                    amount = Amount,
                    reference = ReferenceNumber,
                    invoiceId = debitNote.Id,
                    reason = Reason,
                    date = TransactionDate.ToString("o"),
                    postedBy = User.Identity?.Name ?? "Admin",
                    isDebitNote = true,
                    semester = Semester
                };
                
                TempData["LastDebitNoteData"] = JsonConvert.SerializeObject(debitNoteData);
                TempData["SuccessMessage"] = $"Debit note submitted successfully! Reference: {ReferenceNumber} | Student: {student.FullName} | Amount: K{Amount:N2}";
                return RedirectToAction("DebitNotes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing manual debit note");
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}. Please contact support if the problem persists.";
                return RedirectToAction("DebitNotes");
            }
        }

        [HttpPost("ProcessGroupDebitNote")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessGroupDebitNote(
            string GroupDebitNoteReference,
            string Reason,
            DateTime TransactionDate,
            int AcademicYearId,
            int? Semester,
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
                    return RedirectToAction("DebitNotes");
                }

                if (AcademicYearId <= 0)
                {
                    TempData["ErrorMessage"] = "Academic year is required.";
                    return RedirectToAction("DebitNotes");
                }

                string documentFilePath = null;
                if (ProofOfDocument != null && ProofOfDocument.Length > 0)
                {
                    if (ProofOfDocument.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size exceeds 5MB. Please upload a smaller file.";
                        return RedirectToAction("DebitNotes");
                    }

                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(ProofOfDocument.FileName).ToLowerInvariant();
                    if (!Array.Exists(allowedExtensions, ext => ext == fileExtension))
                    {
                        TempData["ErrorMessage"] = "Invalid file format. Only PDF, JPG, and PNG files are allowed.";
                        return RedirectToAction("DebitNotes");
                    }

                    documentFilePath = await SaveDebitNoteDocument(ProofOfDocument, $"GROUP-DN-{DateTime.Now:yyyyMMddHHmmss}");
                    _logger.LogInformation($"Group debit note document saved: {documentFilePath}");
                }

                var timestamp = DateTime.Now;
                var groupRef = string.IsNullOrEmpty(GroupDebitNoteReference)
                    ? $"GRP-DN-{timestamp:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
                    : GroupDebitNoteReference;

                var debitNotes = new List<StudentInvoice>();
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
                        TempData["ErrorMessage"] = $"Student '{studentNumber}' not found. Group debit note cancelled.";
                        return RedirectToAction("DebitNotes");
                    }

                    var debitNote = new StudentInvoice
                    {
                        StudentId = student.Id,
                        InvoiceReference = $"{groupRef}-{i + 1}",
                        TotalAmount = amount,
                        CreatedDate = TransactionDate,
                        AcademicYearId = AcademicYearId,
                        Status = Status.Pending,
                        BatchReference = groupRef,
                        TransactionType = "DN",
                        YearPeriodId = Semester,
                        Description = !string.IsNullOrWhiteSpace(documentFilePath) ? documentFilePath : $"Group Debit Note: {Reason}",
                        CreatedAt = timestamp,
                        CreatedBy = User.Identity?.Name ?? "Admin",
                        AccountingSystemPostStatus = "Pending"
                    };

                    debitNotes.Add(debitNote);
                    totalAmount += amount;
                }

                _logger.LogInformation($"Saving group debit note with {debitNotes.Count} students. Group Ref: {groupRef}");
                await _context.StudentInvoices.AddRangeAsync(debitNotes);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Group debit note saved successfully. Total Amount: K{totalAmount:N2}");

                TempData["SuccessMessage"] = $"Group debit note processed successfully! Group Reference: {groupRef} | {debitNotes.Count} students | Total Amount: K{totalAmount:N2}";
                return RedirectToAction("DebitNotes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing group debit note");
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}. Please contact support if the problem persists.";
                return RedirectToAction("DebitNotes");
            }
        }

        [HttpGet("GetDebitNotesList")]
        public async Task<IActionResult> GetDebitNotesList(
            int pageNumber = 1,
            int pageSize = 10,
            string searchTerm = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string status = null)
        {
            try
            {
                var query = _context.StudentInvoices
                    .Include(i => i.Student)
                    .Include(i => i.AcademicYear)
                    .Where(i => i.TransactionType == "DN")
                    .AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(i => i.CreatedDate >= startDate.Value);

                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(i => i.CreatedDate <= end);
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(i =>
                        i.Student.StudentId_Number.Contains(searchTerm) ||
                        i.Student.FullName.Contains(searchTerm) ||
                        i.InvoiceReference.Contains(searchTerm) ||
                        i.BatchReference.Contains(searchTerm));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    var statusEnum = Enum.Parse<Status>(status);
                    query = query.Where(i => i.Status == statusEnum);
                }

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(i => i.CreatedDate)
                    .ThenByDescending(i => i.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(i => new
                    {
                        i.Id,
                        i.InvoiceReference,
                        i.TotalAmount,
                        i.CreatedDate,
                        i.CreatedAt,
                        i.Status,
                        i.BatchReference,
                        i.TransactionType,
                        i.YearPeriodId,
                        i.Description,
                        i.CreatedBy,
                        StudentName = i.Student.FullName,
                        StudentNumber = i.Student.StudentId_Number,
                        AcademicYear = i.AcademicYear.YearValue
                    })
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
                _logger.LogError(ex, "Error retrieving debit notes list");
                return Json(new { error = "Error retrieving debit notes" });
            }
        }

        [HttpGet("GetAcademicYears")]
        public async Task<IActionResult> GetAcademicYears()
        {
            try
            {
                var academicYears = await _context.AcademicYears
                    .OrderByDescending(a => a.YearValue)
                    .Select(a => new
                    {
                        id = a.YearId,
                        year = a.YearValue
                    })
                    .ToListAsync();

                return Json(academicYears);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving academic years");
                return Json(new { error = "Error retrieving academic years" });
            }
        }

        private async Task<string> SaveDebitNoteDocument(IFormFile file, string identifier)
        {
            try
            {
                var yearMonth = DateTime.Now.ToString("yyyy/MM");
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "debitnotes", yearMonth);
                Directory.CreateDirectory(uploadsFolder);

                var fileExtension = Path.GetExtension(file.FileName);
                var fileName = $"DN_{identifier}_{DateTime.Now:yyyyMMdd_HHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                _logger.LogInformation($"Debit note document saved successfully: {fileName}");
                return $"/uploads/debitnotes/{yearMonth}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving debit note document");
                throw new Exception("Failed to save debit note document", ex);
            }
        }
    }
}