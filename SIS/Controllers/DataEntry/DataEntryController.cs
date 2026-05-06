using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Fees;
using SIS.Models.Payments;
using SIS.Models.StudentApplication;
using System;
using System.Linq;

namespace SIS.Controllers.DataEntry
{
    [Authorize(Roles = "Finance, Admin")]
    public class DataEntryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DataEntryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            await LoadViewBagData();
            return View();
        }

        // Search students with invoices
        [HttpGet]
        public async Task<IActionResult> SearchStudents(string searchTerm = "", int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                var query = _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.School)
                    .Include(s => s.AcademicYear)
                    .Where(s => s.IsAdmitted)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(s =>
                        s.StudentId_Number.ToLower().Contains(searchTerm) ||
                        s.FullName.ToLower().Contains(searchTerm) ||
                        s.Email.ToLower().Contains(searchTerm) ||
                        s.Phone.ToLower().Contains(searchTerm)
                    );
                }

                var totalCount = await query.CountAsync();
                var students = await query
                    .OrderBy(s => s.FullName)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new
                    {
                        s.Id,
                        s.StudentId_Number,
                        s.FullName,
                        s.Email,
                        s.Phone,
                        ProgrammeName = s.Programme.Name,
                        SchoolName = s.School.Name,
                        AcademicYear = s.AcademicYear.YearValue,
                        OutstandingFees = StudentTools.GetStudentOutstandingBalance(s.Id),
                        s.CurrentYearPeriodId,
                        s.AcademicYearId,
                        s.SchoolId,
                        s.ProgrammeId,
                        s.ModeOfStudyId,
                        s.StudentCurrentYear,
                        s.ProgrammeLevelId,
                        s.IsForeigner,
                        HasAccommodation = s.BedId != null
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = students,
                    totalCount,
                    pageNumber,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get applicable fees for a student
        [HttpGet]
        public async Task<IActionResult> GetApplicableFees(int studentId, int? semester = null)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.School)
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.ProgrammeLevel)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                // Use the semester parameter if provided, otherwise use student's current semester
                var targetSemester = semester ?? student.CurrentYearPeriodId;

                // Query fee configurations
                var feesQuery = _context.FeeConfigurations
                    .Include(f => f.FeeType)
                    .Include(f => f.School)
                    .Include(f => f.Programme)
                    .Include(f => f.ModeOfStudy)
                    .Include(f => f.ProgramLevel)
                    .Include(f => f.AcademicYear)
                    .Where(f => f.AcademicYearId == student.AcademicYearId)
                    .Where(f => f.FeeType.Name != "Application Fee")
                    .AsQueryable();

                // Filter based on student characteristics
                var applicableFees = await feesQuery
                    .Where(f =>
                        // Universal fees ALWAYS apply
                        f.AppliesUniversally ||

                        // OR fee matches ALL specified criteria
                        (
                            // School: Either not specified OR matches student's school
                            (!f.SchoolId.HasValue || f.SchoolId == student.SchoolId) &&

                            // Programme: Either not specified OR matches student's programme
                            (!f.ProgrammeId.HasValue || f.ProgrammeId == student.ProgrammeId) &&

                            // Mode of Study: Either not specified OR matches student's mode
                            (!f.ModeOfStudyId.HasValue || f.ModeOfStudyId == student.ModeOfStudyId) &&

                            // Year of Study: Either not specified OR matches student's current year
                            (!f.YearOfStudy.HasValue || f.YearOfStudy == student.StudentCurrentYear) &&

                            // Program Level: Either not specified OR matches student's level
                            (!f.ProgramLevelId.HasValue || f.ProgramLevelId == student.ProgrammeLevelId) &&

                            // Semester: Either not specified (yearly fee) OR matches target semester OR target semester is null
                            (!f.YearPeriodId.HasValue || f.YearPeriodId == targetSemester || !targetSemester.HasValue) &&

                            // Foreign Student: Either not restricted OR student is foreign
                            (!f.AppliesOnlyToForeignStudents || student.IsForeigner) &&

                            // Accommodation: Either not restricted OR student has accommodation
                            (!f.AppliesOnlyToAccommodated || student.BedId.HasValue)
                        )
                    )
                    .ToListAsync();

                var result = applicableFees.Select(f => new
                {
                    f.Id,
                    f.FeeTypeId,
                    FeeTypeName = f.FeeType.Name,
                    f.Amount,
                    f.YearPeriodId,
                    f.YearOfStudy,
                    SchoolName = f.School != null ? f.School.Name : "All Schools",
                    ProgrammeName = f.Programme != null ? f.Programme.Name : "All Programmes",
                    ModeOfStudyName = f.ModeOfStudy != null ? f.ModeOfStudy.ModeName : "All Modes",
                    ProgramLevelName = f.ProgramLevel != null ? f.ProgramLevel.Name : "All Levels",
                    f.AppliesUniversally,
                    f.AppliesOnlyToForeignStudents,
                    f.AppliesOnlyToAccommodated,
                    f.CreditNCode,
                    f.DebitNCode,
                    Description = BuildFeeDescription(
                        f.AppliesUniversally,
                        f.School != null ? f.School.Name : "All Schools",
                        f.Programme != null ? f.Programme.Name : "All Programmes",
                        f.ModeOfStudy != null ? f.ModeOfStudy.ModeName : "All Modes",
                        f.ProgramLevel != null ? f.ProgramLevel.Name : "All Levels",
                        f.YearOfStudy,
                        f.YearPeriodId,
                        f.AppliesOnlyToForeignStudents,
                        f.AppliesOnlyToAccommodated
                    )
                })
                .OrderBy(f => f.FeeTypeName)
                .ToList();

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Helper method to build fee description - MADE STATIC
        private static string BuildFeeDescription(
            bool appliesUniversally,
            string schoolName,
            string programmeName,
            string modeOfStudyName,
            string programLevelName,
            int? yearOfStudy,
            int? semester,
            bool appliesOnlyToForeignStudents,
            bool appliesOnlyToAccommodated)
        {
            var parts = new List<string>();

            if (appliesUniversally)
            {
                parts.Add("Universal");
            }
            else
            {
                if (schoolName != "All Schools") parts.Add(schoolName);
                if (programmeName != "All Programmes") parts.Add(programmeName);
                if (modeOfStudyName != "All Modes") parts.Add(modeOfStudyName);
                if (programLevelName != "All Levels") parts.Add(programLevelName);
            }

            if (yearOfStudy.HasValue) parts.Add($"Year {yearOfStudy}");
            if (semester.HasValue) parts.Add($"Sem {semester}");
            if (appliesOnlyToForeignStudents) parts.Add("Foreign Students");
            if (appliesOnlyToAccommodated) parts.Add("Accommodated");

            return parts.Count > 0 ? string.Join(" | ", parts) : "General";
        }

        // Get student invoices
        [HttpGet]
        public async Task<IActionResult> GetStudentInvoices(int studentId)
        {
            try
            {
                var invoices = await _context.StudentInvoices
                    .Include(i => i.AcademicYear)
                    .Where(i => i.StudentId == studentId && i.DeletedAt == null)
                    .OrderByDescending(i => i.CreatedDate)
                    .Select(i => new
                    {
                        i.Id,
                        i.InvoiceReference,
                        i.TotalAmount,
                        i.CreatedDate,
                        AcademicYear = i.AcademicYear.YearValue,
                        i.Status,
                        i.YearPeriodId,
                        i.AccountingSystemPostStatus
                    })
                    .ToListAsync();

                return Json(new { success = true, data = invoices });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get single invoice details
        [HttpGet]
        public async Task<IActionResult> GetInvoice(int id)
        {
            try
            {
                var invoice = await _context.StudentInvoices
                    .Include(i => i.AcademicYear)
                    .Include(i => i.Student)
                    .Where(i => i.Id == id && i.DeletedAt == null)
                    .Select(i => new
                    {
                        i.Id,
                        i.StudentId,
                        i.InvoiceReference,
                        i.TotalAmount,
                        i.CreatedDate,
                        i.AcademicYearId,
                        i.Status,
                        i.YearPeriodId,
                        i.AccountingSystemPostStatus
                    })
                    .FirstOrDefaultAsync();

                if (invoice == null)
                {
                    return Json(new { success = false, message = "Invoice not found" });
                }

                return Json(new { success = true, data = invoice });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get student outstanding balance
        [HttpGet]
        public IActionResult GetStudentOutstandingBalance(int studentId)
        {
            try
            {
                var outstandingBalance = StudentTools.GetStudentOutstandingBalance(studentId);
                return Json(new { success = true, outstandingBalance });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Generate invoice reference
        [HttpGet]
        public async Task<IActionResult> GenerateInvoiceReference()
        {
            try
            {
                var reference = await GenerateInvoiceReferenceInternal();
                return Json(new { success = true, reference });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Create invoice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvoice([FromForm] StudentInvoice model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                // Check if invoice reference already exists
                if (!string.IsNullOrEmpty(model.InvoiceReference))
                {
                    var existingInvoice = await _context.StudentInvoices
                        .FirstOrDefaultAsync(i => i.InvoiceReference == model.InvoiceReference && i.DeletedAt == null);

                    if (existingInvoice != null)
                    {
                        return Json(new { success = false, message = "Invoice reference already exists. Please use a different reference." });
                    }
                }
                else
                {
                    // Generate invoice reference if not provided
                    model.InvoiceReference = await GenerateInvoiceReferenceInternal();
                }

                model.CreatedDate = DateTime.Now;
                model.Status = Status.Pending;
                model.AccountingSystemPostStatus = model.AccountingSystemPostStatus ?? "Pending";
                model.CreatedAt = DateTime.Now;
                model.CreatedBy = user.Id;

                _context.StudentInvoices.Add(model);
                await _context.SaveChangesAsync();

                // Get updated outstanding balance
                var updatedBalance = StudentTools.GetStudentOutstandingBalance(model.StudentId);

                TempData["Success"] = "Invoice created successfully";
                return Json(new
                {
                    success = true,
                    message = "Invoice created successfully",
                    outstandingBalance = updatedBalance
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Update invoice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInvoice(int id, [FromForm] StudentInvoice model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var invoice = await _context.StudentInvoices
                    .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt == null);

                if (invoice == null)
                {
                    return Json(new { success = false, message = "Invoice not found" });
                }

                // Check if invoice reference already exists (excluding current invoice)
                if (!string.IsNullOrEmpty(model.InvoiceReference) && model.InvoiceReference != invoice.InvoiceReference)
                {
                    var existingInvoice = await _context.StudentInvoices
                        .FirstOrDefaultAsync(i => i.InvoiceReference == model.InvoiceReference && i.DeletedAt == null && i.Id != id);

                    if (existingInvoice != null)
                    {
                        return Json(new { success = false, message = "Invoice reference already exists. Please use a different reference." });
                    }
                }

                // Update invoice properties
                invoice.InvoiceReference = model.InvoiceReference ?? invoice.InvoiceReference;
                invoice.TotalAmount = model.TotalAmount;
                invoice.AcademicYearId = model.AcademicYearId;
                invoice.Status = model.Status;
                invoice.YearPeriodId = model.YearPeriodId;
                invoice.AccountingSystemPostStatus = model.AccountingSystemPostStatus ?? "Pending";
                invoice.UpdatedAt = DateTime.Now;
                invoice.UpdatedBy = user.Id;

                await _context.SaveChangesAsync();

                // Get updated outstanding balance
                var updatedBalance = StudentTools.GetStudentOutstandingBalance(invoice.StudentId);

                TempData["Success"] = "Invoice updated successfully";
                return Json(new
                {
                    success = true,
                    message = "Invoice updated successfully",
                    outstandingBalance = updatedBalance
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Soft delete invoice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteInvoice(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var invoice = await _context.StudentInvoices
                    .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt == null);

                if (invoice == null)
                {
                    return Json(new { success = false, message = "Invoice not found" });
                }

                var studentId = invoice.StudentId;

                // Soft delete - set DeletedAt timestamp
                invoice.DeletedAt = DateTime.Now;
                invoice.UpdatedAt = DateTime.Now;
                invoice.UpdatedBy = user.Id;
                await _context.SaveChangesAsync();

                // Get updated outstanding balance
                var updatedBalance = StudentTools.GetStudentOutstandingBalance(studentId);

                TempData["Success"] = "Invoice deleted successfully";
                return Json(new
                {
                    success = true,
                    message = "Invoice deleted successfully",
                    outstandingBalance = updatedBalance
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Helper methods
        private async Task LoadViewBagData()
        {
            ViewBag.AcademicYears = await _context.AcademicYears
                .Where(ay => ay.IsActive)
                .Select(ay => new SelectListItem
                {
                    Value = ay.YearId.ToString(),
                    Text = ay.YearValue
                })
                .ToListAsync();

            ViewBag.Statuses = Enum.GetValues(typeof(Status))
                .Cast<Status>()
                .Select(s => new SelectListItem
                {
                    Value = s.ToString(),
                    Text = s.ToString()
                })
                .ToList();
        }

        private async Task<string> GenerateInvoiceReferenceInternal()
        {
            var year = DateTime.Now.Year;
            var lastInvoice = await _context.StudentInvoices
                .Where(i => i.InvoiceReference.StartsWith($"INV{year}"))
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastInvoice != null)
            {
                var lastNumber = lastInvoice.InvoiceReference.Substring(7);
                if (int.TryParse(lastNumber, out int num))
                {
                    nextNumber = num + 1;
                }
            }

            return $"INV{year}{nextNumber:D5}";
        }
    }
}