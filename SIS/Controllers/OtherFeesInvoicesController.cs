using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Payments;
using SIS.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SIS.Controllers
{
    [Route("Admin/[action]")]
    public class OtherFeesInvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OtherFeesInvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
public async Task<IActionResult> OtherFeesInvoices()
{
    var otherFees = await _context.OtherFees
        .Where(o => o.IsActive)
        .OrderBy(o => o.FeeName)
        .Select(o => new
        {
            o.Id,
            o.FeeName,
            o.AppliesOnlyToForeignStudents,
            StudentType = o.AppliesOnlyToForeignStudents ? "Foreign Student" : "Local Student",
            InvoiceType = o.AppliesOnlyToForeignStudents ? "Foreign Student Fee" : "Local Student Fee",
            BadgeClass = o.AppliesOnlyToForeignStudents ? "bg-purple-100 text-purple-700" : "bg-green-100 text-green-700",
            IconClass = o.AppliesOnlyToForeignStudents ? "language" : "person",
            o.Amount,
            SchoolName = o.School != null ? o.School.Name : "General"
        })
        .ToListAsync();
    
    ViewBag.OtherFees = otherFees;
    
    var academicYears = await _context.AcademicYears
        .Where(a => a.IsActive)
        .OrderByDescending(a => a.YearId)
        .Select(a => new SelectListItem
        {
            Value = a.YearId.ToString(),
            Text = a.YearValue
        })
        .ToListAsync();
    
    ViewBag.AcademicYears = academicYears ?? new List<SelectListItem>();
    
    return View("~/Views/Admin/OtherFeesInvoices.cshtml");
}

        [HttpPost]
        public async Task<IActionResult> ValidateStudents([FromBody] ValidateStudentsRequest request)
        {
            var studentIds = request.StudentIds
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            var students = await _context.Students
                .Where(s => studentIds.Contains(s.StudentId_Number))
                .Select(s => new
                {
                    s.Id,
                    s.StudentId_Number,
                    s.FullName,
                    s.Email,
                    s.Phone,
                    ProgrammeName = s.Programme != null ? s.Programme.Name : "",
                    SchoolName = s.School != null ? s.School.Name : "",
                    s.StudentStatus,
                    s.IsRegistered,
                    s.OutstandingFees
                })
                .ToListAsync();

            var validStudents = students.Select(s => new
            {
                s.Id,
                s.StudentId_Number,
                s.FullName,
                s.Email,
                s.Phone,
                s.ProgrammeName,
                s.SchoolName,
                s.StudentStatus,
                s.IsRegistered,
                s.OutstandingFees,
                IsValid = true
            }).ToList();

            var invalidStudentIds = studentIds
                .Except(students.Select(s => s.StudentId_Number))
                .Select(id => new
                {
                    Id = 0,
                    StudentId_Number = id,
                    FullName = "NOT FOUND",
                    Email = "",
                    Phone = "",
                    ProgrammeName = "",
                    SchoolName = "",
                    StudentStatus = "",
                    IsRegistered = false,
                    OutstandingFees = 0m,
                    IsValid = false
                })
                .ToList();

            return Json(new
            {
                validStudents,
                invalidStudents = invalidStudentIds,
                totalValid = validStudents.Count,
                totalInvalid = invalidStudentIds.Count
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateInvoices(GenerateInvoicesRequest request)
        {
            // Manual validation since we're collecting data dynamically
            if (request.SelectedFeeIds == null || !request.SelectedFeeIds.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one fee.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            if (request.AcademicYearId <= 0)
            {
                TempData["ErrorMessage"] = "Please select an academic year.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            if (string.IsNullOrWhiteSpace(request.StudentIds))
            {
                TempData["ErrorMessage"] = "Please enter at least one student ID.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            var studentIds = request.StudentIds
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            if (!studentIds.Any())
            {
                TempData["ErrorMessage"] = "No valid student IDs found.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            var students = await _context.Students
                .Where(s => studentIds.Contains(s.StudentId_Number))
                .ToListAsync();

            if (!students.Any())
            {
                TempData["ErrorMessage"] = "No valid students found.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            var otherFees = await _context.OtherFees
                .Where(o => request.SelectedFeeIds.Contains(o.Id) && o.IsActive)
                .ToListAsync();

            if (!otherFees.Any())
            {
                TempData["ErrorMessage"] = "No valid fees selected.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            var academicYear = await _context.AcademicYears
                .FirstOrDefaultAsync(a => a.YearId == request.AcademicYearId);

            if (academicYear == null)
            {
                TempData["ErrorMessage"] = "Invalid academic year.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            var invoicesCreated = 0;
            var errors = new List<string>();

            // Generate one batch reference for all invoices in this batch
            var batchReference = await GenerateBatchReference();

            foreach (var student in students)
            {
                try
                {
                    // Check if invoice already exists for this student, academic year, and semester
                    var existingInvoice = await _context.StudentInvoices
                        .AnyAsync(i => i.StudentId == student.Id
                                    && i.AcademicYearId == request.AcademicYearId
                                    && i.YearPeriodId == request.Semester
                                    && i.DeletedAt == null
                                    && (int)i.Status != 3);

                    if (existingInvoice && !request.AllowDuplicates)
                    {
                        errors.Add($"Invoice already exists for {student.StudentId_Number} - {student.FullName}");
                        continue;
                    }

                    // Calculate total amount
                    var totalAmount = otherFees.Sum(f => f.Amount);

                    // Generate unique invoice reference with batch suffix
                    var invoiceReference = await GenerateInvoiceReference(academicYear.YearValue, batchReference);

                    // Create invoice
                    var invoice = new StudentInvoice
                    {
                        StudentId = student.Id,
                        InvoiceReference = invoiceReference,
                        TotalAmount = totalAmount,
                        CreatedDate = DateTime.Now,
                        AcademicYearId = request.AcademicYearId,
                        Status = (Status)4,
                        YearPeriodId = request.Semester,
                        AccountingSystemPostStatus = "Pending",
                        BatchReference = batchReference,
                        Description = request.InvoiceDescription,
                        InvoiceItems = new List<StudentInvoiceItem>()
                    };

                    // Add invoice items
                    foreach (var fee in otherFees)
                    {
                        invoice.InvoiceItems.Add(new StudentInvoiceItem
                        {
                            FeeTypeName = fee.FeeName,
                            Description = $"{fee.FeeName} - {academicYear.YearValue}" + 
                                         (request.Semester.HasValue ? $" Semester {request.Semester}" : ""),
                            Amount = fee.Amount,
                            FeeConfigurationId = null // OtherFees don't have FeeConfiguration
                        });
                    }

                    _context.StudentInvoices.Add(invoice);
                    
                    // Save immediately to generate unique reference for next invoice
                    await _context.SaveChangesAsync();
                    
                    invoicesCreated++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Error creating invoice for {student.StudentId_Number}: {ex.Message}");
                }
            }

            if (invoicesCreated > 0)
            {
                TempData["SuccessMessage"] = $"Successfully created {invoicesCreated} invoice(s) with batch reference: {batchReference}";
            }

            if (errors.Any())
            {
                TempData["WarningMessage"] = string.Join("<br/>", errors);
            }

            if (invoicesCreated == 0 && !errors.Any())
            {
                TempData["ErrorMessage"] = "No invoices were created.";
            }

            return RedirectToAction(nameof(OtherFeesInvoices));
        }

        [HttpGet]
        public async Task<IActionResult> InvoiceList()
        {
            ViewBag.AcademicYears = await _context.AcademicYears
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.YearId)
                .Select(a => new SelectListItem
                {
                    Value = a.YearId.ToString(),
                    Text = a.YearValue
                })
                .ToListAsync();

            return View("~/Views/Admin/InvoiceList.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> GetInvoices([FromBody] InvoiceFilterRequest request)
        {
            var query = _context.StudentInvoices
                .Include(i => i.Student)
                .Include(i => i.AcademicYear)
                .Include(i => i.InvoiceItems)
                .AsQueryable();

            // Filter by deleted status
            if (request.IncludeDeleted)
            {
                // Show all including deleted
            }
            else
            {
                query = query.Where(i => i.DeletedAt == null);
            }

            if (request.AcademicYearId.HasValue)
                query = query.Where(i => i.AcademicYearId == request.AcademicYearId.Value);

            if (request.Semester.HasValue)
                query = query.Where(i => i.YearPeriodId == request.Semester.Value);

            if (!string.IsNullOrEmpty(request.Status))
            {
                if (Enum.TryParse<Status>(request.Status, out var status))
                    query = query.Where(i => i.Status == status);
            }

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                query = query.Where(i => i.Student.StudentId_Number.Contains(request.SearchTerm) ||
                                       i.Student.FullName.Contains(request.SearchTerm) ||
                                       i.InvoiceReference.Contains(request.SearchTerm));
            }

            var totalRecords = await query.CountAsync();

            query = query.OrderByDescending(i => i.CreatedDate);

            var invoices = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(i => new
                {
                    i.Id,
                    i.InvoiceReference,
                    i.BatchReference,
                    StudentId = i.Student.StudentId_Number,
                    StudentName = i.Student.FullName,
                    i.TotalAmount,
                    i.CreatedDate,
                    AcademicYear = i.AcademicYear.YearValue,
                    i.YearPeriodId,
                    Status = i.Status.ToString(),
                    i.AccountingSystemPostStatus,
                    ItemsCount = i.InvoiceItems.Count,
                    i.DeletedAt,
                    IsDeleted = i.DeletedAt != null
                })
                .ToListAsync();

            var invoicesList = invoices.Select((inv, index) => new
            {
                inv.Id,
                inv.InvoiceReference,
                inv.BatchReference,
                inv.StudentId,
                inv.StudentName,
                inv.TotalAmount,
                inv.CreatedDate,
                inv.AcademicYear,
                inv.YearPeriodId,
                inv.Status,
                inv.AccountingSystemPostStatus,
                inv.ItemsCount,
                inv.DeletedAt,
                inv.IsDeleted,
                RowNumber = (request.Page - 1) * request.PageSize + index + 1
            }).ToList();

            return Json(new
            {
                invoices = invoicesList,
                totalRecords,
                totalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize),
                currentPage = request.Page
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetInvoiceDetails(int id)
        {
            var invoice = await _context.StudentInvoices
                .Include(i => i.Student)
                .Include(i => i.AcademicYear)
                .Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt == null);

            if (invoice == null)
            {
                return NotFound();
            }

            return Json(new
            {
                invoice.Id,
                invoice.InvoiceReference,
                StudentId = invoice.Student.StudentId_Number,
                StudentName = invoice.Student.FullName,
                StudentEmail = invoice.Student.Email,
                StudentPhone = invoice.Student.Phone,
                invoice.TotalAmount,
                invoice.CreatedDate,
                AcademicYear = invoice.AcademicYear.YearValue,
                invoice.YearPeriodId,
                Status = invoice.Status.ToString(),
                invoice.AccountingSystemPostStatus,
                Items = invoice.InvoiceItems.Select(item => new
                {
                    item.Id,
                    item.FeeTypeName,
                    item.Description,
                    item.Amount
                }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelInvoice(int id)
        {
            var invoice = await _context.StudentInvoices
                .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt == null);

            if (invoice == null)
            {
                TempData["ErrorMessage"] = "Invoice not found.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            if ((int)invoice.Status == 2 || (int)invoice.Status == 1)
            {
                TempData["ErrorMessage"] = "Cannot cancel an invoice that has payments.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            // Soft delete
            invoice.DeletedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Invoice cancelled successfully.";
            return RedirectToAction(nameof(OtherFeesInvoices));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreInvoice(int id)
        {
            var invoice = await _context.StudentInvoices
                .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt != null);

            if (invoice == null)
            {
                TempData["ErrorMessage"] = "Invoice not found.";
                return RedirectToAction(nameof(OtherFeesInvoices));
            }

            // Restore
            invoice.DeletedAt = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Invoice restored successfully.";
            return RedirectToAction(nameof(OtherFeesInvoices));
        }

        private async Task<string> GenerateInvoiceReference(string academicYear, string batchReference)
        {
            var yearPrefix = academicYear.Replace("/", "");
            
            // Get the next sequence number for this academic year
            var maxReference = await _context.StudentInvoices
                .Where(i => i.InvoiceReference.StartsWith($"INV-{yearPrefix}"))
                .OrderByDescending(i => i.Id)
                .Select(i => i.InvoiceReference)
                .FirstOrDefaultAsync();

            int nextSequence = 1;
            if (maxReference != null)
            {
                // Extract sequence number from reference like "INV-20232024-000001-Batch"
                var parts = maxReference.Split('-');
                if (parts.Length >= 3)
                {
                    var sequencePart = parts[2];
                    if (int.TryParse(sequencePart, out int currentSequence))
                    {
                        nextSequence = currentSequence + 1;
                    }
                }
            }

            return $"INV-{yearPrefix}-{nextSequence:D6}-{batchReference}";
        }

        private async Task<string> GenerateBatchReference()
        {
            // Count all batch references to get next sequence
            var maxBatchRef = await _context.StudentInvoices
                .Where(i => i.BatchReference != null && i.BatchReference.StartsWith("Batch-"))
                .OrderByDescending(i => i.Id)
                .Select(i => i.BatchReference)
                .FirstOrDefaultAsync();

            int nextSequence = 1;
            if (maxBatchRef != null)
            {
                // Extract sequence from "Batch-000001"
                var parts = maxBatchRef.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out int currentSequence))
                {
                    nextSequence = currentSequence + 1;
                }
            }

            return $"Batch-{nextSequence:D6}";
        }
    }

    public class ValidateStudentsRequest
    {
        public string StudentIds { get; set; }
    }

    public class GenerateInvoicesRequest
    {
        public string StudentIds { get; set; }
        public List<int> SelectedFeeIds { get; set; }
        public int AcademicYearId { get; set; }
        public int? Semester { get; set; }
        public bool AllowDuplicates { get; set; }
        public string InvoiceDescription { get; set; }
    }

    public class InvoiceFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int? AcademicYearId { get; set; }
        public int? Semester { get; set; }
        public string Status { get; set; }
        public string SearchTerm { get; set; }
        public bool IncludeDeleted { get; set; } = false;
    }
}