using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Appeals;
using SIS.Models.StudentApplication;

namespace SIS.Controllers
{
    [Authorize]
    public class ResultAppealController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private const decimal REMARK_FEE = 500.00m;

        public ResultAppealController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        #region Student Views

        // Student: View their appeals
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyAppeals()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .Include(s => s.Programme)
                .Include(s => s.AcademicYear)
                .FirstOrDefaultAsync(s => s.Email == user.Email || s.Username == user.UserName);

            if (student == null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Student = student;
            await LoadStudentViewBagData(student.Id);
            return View();
        }

        // Student: Get their appeals
        [HttpGet]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyAppeals()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email == user.Email || s.Username == user.UserName);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                var appeals = await _context.ResultAppeals
                    .Include(a => a.Course)
                    .Include(a => a.AcademicYear)
                    .Where(a => a.StudentId == student.Id && a.DeletedAt == null)
                    .OrderByDescending(a => a.SubmissionDate)
                    .Select(a => new
                    {
                        a.Id,
                        a.CourseId,
                        CourseCode = a.Course != null ? a.Course.CourseCode : "N/A",
                        CourseName = a.Course != null ? a.Course.CourseName : "N/A",
                        AcademicYear = a.AcademicYear != null ? a.AcademicYear.YearValue : "N/A",
                        a.Semester,
                        a.AppealType,
                        a.Reason,
                        a.Status,
                        a.SubmissionDate,
                        a.AppealFee,
                        a.FeePaid,
                        a.OriginalMark,
                        a.RevisedMark,
                        a.Response,
                        a.ResponseDate,
                        a.FinalDecision,
                        a.DecisionDate
                    })
                    .ToListAsync();

                return Json(new { success = true, data = appeals });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Student: Get their registered courses for appeal
        [HttpGet]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyCourses(int? academicYearId = null, int? semester = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Email == user.Email || s.Username == user.UserName);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                var targetAcademicYearId = academicYearId ?? student.AcademicYearId;
                var targetSemester = semester ?? student.CurrentSemester;

                // Get registered courses with grades
                var courses = await _context.StudentCourseRegistrations
                    .Include(cr => cr.Course)
                    .Where(cr => cr.StudentId == student.Id &&
                                 cr.AcademicYearId == targetAcademicYearId &&
                                 (targetSemester == null || cr.Semester == targetSemester))
                    .Select(cr => new
                    {
                        cr.Course.Id,
                        cr.Course.CourseCode,
                        cr.Course.CourseName,
                        cr.Course.CourseType,
                        cr.Semester,
                        cr.AcademicYearId
                    })
                    .Distinct()
                    .OrderBy(c => c.CourseCode)
                    .ToListAsync();

                // If no registered courses, get programme courses
                if (!courses.Any())
                {
                    courses = await _context.Courses
                        .Where(c => c.ProgrammeID == student.ProgrammeId &&
                                    (targetSemester == null || c.SemesterTaken == targetSemester))
                        .Select(c => new
                        {
                            c.Id,
                            c.CourseCode,
                            c.CourseName,
                            c.CourseType,
                            Semester = c.SemesterTaken,
                            AcademicYearId = targetAcademicYearId
                        })
                        .OrderBy(c => c.CourseCode)
                        .ToListAsync();
                }

                return Json(new { success = true, data = courses });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Student: Submit an appeal
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SubmitAppeal([FromForm] ResultAppeal model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email == user.Email || s.Username == user.UserName);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                // Check for existing pending appeal for same course/academic year/semester
                var existingAppeal = await _context.ResultAppeals
                    .FirstOrDefaultAsync(a =>
                        a.StudentId == student.Id &&
                        a.CourseId == model.CourseId &&
                        a.AcademicYearId == model.AcademicYearId &&
                        a.Semester == model.Semester &&
                        a.Status != "Cancelled" &&
                        a.Status != "Rejected" &&
                        a.Status != "Completed" &&
                        a.DeletedAt == null);

                if (existingAppeal != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "You already have an active appeal for this course in the selected academic period."
                    });
                }

                model.StudentId = student.Id;
                model.SubmissionDate = DateTime.Now.AddHours(2);
                model.Status = "Pending";
                model.CreatedAt = DateTime.Now.AddHours(2);
                model.CreatedBy = user?.Id ?? "System";

                // Set fee for Remark appeals
                if (model.AppealType == "Remark")
                {
                    model.AppealFee = REMARK_FEE;
                    model.FeePaid = false;
                }
                else
                {
                    model.AppealFee = 0;
                    model.FeePaid = true; // No fee required
                }

                _context.ResultAppeals.Add(model);
                await _context.SaveChangesAsync();

                // Log status history
                await LogStatusChange(model.Id, "", "Pending", "Appeal submitted", user?.Id);

                return Json(new
                {
                    success = true,
                    message = model.AppealType == "Remark"
                        ? $"Appeal submitted successfully. Please pay the remark fee of K{REMARK_FEE:N2} to proceed."
                        : "Appeal submitted successfully.",
                    id = model.Id,
                    requiresFee = model.AppealType == "Remark",
                    fee = model.AppealFee
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Student: Cancel their own appeal
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CancelAppeal(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email == user.Email || s.Username == user.UserName);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found" });
                }

                var appeal = await _context.ResultAppeals
                    .FirstOrDefaultAsync(a => a.Id == id && a.StudentId == student.Id && a.DeletedAt == null);

                if (appeal == null)
                {
                    return Json(new { success = false, message = "Appeal not found" });
                }

                if (appeal.Status != "Pending")
                {
                    return Json(new { success = false, message = "Only pending appeals can be cancelled" });
                }

                var oldStatus = appeal.Status;
                appeal.Status = "Cancelled";
                appeal.UpdatedAt = DateTime.Now.AddHours(2);
                appeal.UpdatedBy = user?.Id;

                await _context.SaveChangesAsync();
                await LogStatusChange(id, oldStatus, "Cancelled", "Cancelled by student", user?.Id);

                return Json(new { success = true, message = "Appeal cancelled successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Admin/Staff Views

        // Admin: Manage all appeals
        [Authorize(Roles = "Admin, Registrar, Dean, HOD, Lecturer")]
        public async Task<IActionResult> Index()
        {
            await LoadAdminViewBagData();
            return View();
        }

        // Admin: Get all appeals with filters
        [HttpGet]
        [Authorize(Roles = "Admin, Registrar, Dean, HOD, Lecturer")]
        public async Task<IActionResult> GetAppeals(
            int? schoolId = null,
            int? departmentId = null,
            int? programmeId = null,
            int? academicYearId = null,
            int? semester = null,
            string? status = null,
            string? appealType = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            try
            {
                var query = _context.ResultAppeals
                    .Include(a => a.Student)
                        .ThenInclude(s => s.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                    .Include(a => a.Course)
                    .Include(a => a.AcademicYear)
                    .Where(a => a.DeletedAt == null)
                    .AsQueryable();

                // Apply filters
                if (schoolId.HasValue)
                {
                    query = query.Where(a => a.Student != null &&
                                             a.Student.Programme != null &&
                                             a.Student.Programme.Department != null &&
                                             a.Student.Programme.Department.SchoolId == schoolId.Value);
                }

                if (departmentId.HasValue)
                {
                    query = query.Where(a => a.Student != null &&
                                             a.Student.Programme != null &&
                                             a.Student.Programme.DepartmentId == departmentId.Value);
                }

                if (programmeId.HasValue)
                {
                    query = query.Where(a => a.Student != null &&
                                             a.Student.ProgrammeId == programmeId.Value);
                }

                if (academicYearId.HasValue)
                {
                    query = query.Where(a => a.AcademicYearId == academicYearId.Value);
                }

                if (semester.HasValue)
                {
                    query = query.Where(a => a.Semester == semester.Value);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(a => a.Status == status);
                }

                if (!string.IsNullOrEmpty(appealType))
                {
                    query = query.Where(a => a.AppealType == appealType);
                }

                var totalCount = await query.CountAsync();

                var appeals = await query
                    .OrderByDescending(a => a.SubmissionDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new
                    {
                        a.Id,
                        StudentId = a.Student != null ? a.Student.StudentId_Number : "N/A",
                        StudentName = a.Student != null ? a.Student.FullName : "N/A",
                        Programme = a.Student != null && a.Student.Programme != null ? a.Student.Programme.Name : "N/A",
                        Department = a.Student != null && a.Student.Programme != null && a.Student.Programme.Department != null
                            ? a.Student.Programme.Department.Name : "N/A",
                        School = a.Student != null && a.Student.Programme != null && a.Student.Programme.Department != null && a.Student.Programme.Department.School != null
                            ? a.Student.Programme.Department.School.Name : "N/A",
                        CourseCode = a.Course != null ? a.Course.CourseCode : "N/A",
                        CourseName = a.Course != null ? a.Course.CourseName : "N/A",
                        AcademicYear = a.AcademicYear != null ? a.AcademicYear.YearValue : "N/A",
                        a.Semester,
                        a.AppealType,
                        a.Reason,
                        a.Status,
                        a.SubmissionDate,
                        a.AppealFee,
                        a.FeePaid,
                        a.OriginalMark,
                        a.RevisedMark,
                        a.Response,
                        a.ResponseDate
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = appeals,
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

        // Admin: Get single appeal details
        [HttpGet]
        [Authorize(Roles = "Admin, Registrar, Dean, HOD, Lecturer, Student")]
        public async Task<IActionResult> GetAppeal(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var isStudent = User.IsInRole("Student");

                var appeal = await _context.ResultAppeals
                    .Include(a => a.Student)
                        .ThenInclude(s => s.Programme)
                    .Include(a => a.Course)
                    .Include(a => a.AcademicYear)
                    .Where(a => a.Id == id && a.DeletedAt == null)
                    .Select(a => new
                    {
                        a.Id,
                        a.StudentId,
                        StudentIdNumber = a.Student != null ? a.Student.StudentId_Number : "N/A",
                        StudentName = a.Student != null ? a.Student.FullName : "N/A",
                        StudentEmail = a.Student != null ? a.Student.Email : "N/A",
                        Programme = a.Student != null && a.Student.Programme != null ? a.Student.Programme.Name : "N/A",
                        a.CourseId,
                        CourseCode = a.Course != null ? a.Course.CourseCode : "N/A",
                        CourseName = a.Course != null ? a.Course.CourseName : "N/A",
                        a.AcademicYearId,
                        AcademicYear = a.AcademicYear != null ? a.AcademicYear.YearValue : "N/A",
                        a.Semester,
                        a.AppealType,
                        a.Reason,
                        a.SupportingDocuments,
                        a.Status,
                        a.SubmissionDate,
                        a.AppealFee,
                        a.FeePaid,
                        a.FeePaymentDate,
                        a.PaymentReference,
                        a.OriginalMark,
                        a.RevisedMark,
                        a.OriginalGrade,
                        a.RevisedGrade,
                        a.Response,
                        a.ResponseDate,
                        a.FinalDecision,
                        a.DecisionDate,
                        a.IsEscalated,
                        a.EscalationReason,
                        a.EscalatedDate
                    })
                    .FirstOrDefaultAsync();

                if (appeal == null)
                {
                    return Json(new { success = false, message = "Appeal not found" });
                }

                // If student, verify they own this appeal
                if (isStudent)
                {
                    var student = await _context.Students
                        .FirstOrDefaultAsync(s => s.Email == user.Email || s.Username == user.UserName);

                    if (student == null || appeal.StudentId != student.Id)
                    {
                        return Json(new { success = false, message = "Unauthorized access" });
                    }
                }

                return Json(new { success = true, data = appeal });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Admin: Respond to appeal
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar, Dean, HOD, Lecturer")]
        public async Task<IActionResult> RespondToAppeal(int id, [FromForm] string response, [FromForm] string status,
            [FromForm] decimal? revisedMark = null, [FromForm] string? revisedGrade = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var appeal = await _context.ResultAppeals
                    .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null);

                if (appeal == null)
                {
                    return Json(new { success = false, message = "Appeal not found" });
                }

                var oldStatus = appeal.Status;
                appeal.Response = response;
                appeal.ResponseBy = user?.Id;
                appeal.ResponseDate = DateTime.Now.AddHours(2);
                appeal.Status = status;
                appeal.UpdatedAt = DateTime.Now.AddHours(2);
                appeal.UpdatedBy = user?.Id;

                if (revisedMark.HasValue)
                {
                    appeal.RevisedMark = revisedMark.Value;
                }

                if (!string.IsNullOrEmpty(revisedGrade))
                {
                    appeal.RevisedGrade = revisedGrade;
                }

                await _context.SaveChangesAsync();
                await LogStatusChange(id, oldStatus, status, response, user?.Id);

                return Json(new { success = true, message = "Response submitted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Admin: Make final decision
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar, Dean")]
        public async Task<IActionResult> MakeDecision(int id, [FromForm] string decision, [FromForm] string status,
            [FromForm] decimal? revisedMark = null, [FromForm] string? revisedGrade = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var appeal = await _context.ResultAppeals
                    .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null);

                if (appeal == null)
                {
                    return Json(new { success = false, message = "Appeal not found" });
                }

                var oldStatus = appeal.Status;
                appeal.FinalDecision = decision;
                appeal.DecisionBy = user?.Id;
                appeal.DecisionDate = DateTime.Now.AddHours(2);
                appeal.Status = status;
                appeal.UpdatedAt = DateTime.Now.AddHours(2);
                appeal.UpdatedBy = user?.Id;

                if (revisedMark.HasValue)
                {
                    appeal.RevisedMark = revisedMark.Value;
                }

                if (!string.IsNullOrEmpty(revisedGrade))
                {
                    appeal.RevisedGrade = revisedGrade;
                }

                await _context.SaveChangesAsync();
                await LogStatusChange(id, oldStatus, status, decision, user?.Id);

                return Json(new { success = true, message = "Decision recorded successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Admin: Confirm fee payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Registrar, Accounts")]
        public async Task<IActionResult> ConfirmPayment(int id, [FromForm] string paymentReference)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var appeal = await _context.ResultAppeals
                    .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null);

                if (appeal == null)
                {
                    return Json(new { success = false, message = "Appeal not found" });
                }

                appeal.FeePaid = true;
                appeal.FeePaymentDate = DateTime.Now.AddHours(2);
                appeal.PaymentReference = paymentReference;
                appeal.UpdatedAt = DateTime.Now.AddHours(2);
                appeal.UpdatedBy = user?.Id;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Payment confirmed successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Admin: Get statistics
        [HttpGet]
        [Authorize(Roles = "Admin, Registrar, Dean, HOD")]
        public async Task<IActionResult> GetStatistics(int? academicYearId = null, int? schoolId = null)
        {
            try
            {
                var query = _context.ResultAppeals
                    .Include(a => a.Student)
                        .ThenInclude(s => s.Programme)
                            .ThenInclude(p => p.Department)
                    .Where(a => a.DeletedAt == null)
                    .AsQueryable();

                if (academicYearId.HasValue)
                {
                    query = query.Where(a => a.AcademicYearId == academicYearId.Value);
                }

                if (schoolId.HasValue)
                {
                    query = query.Where(a => a.Student != null &&
                                             a.Student.Programme != null &&
                                             a.Student.Programme.Department != null &&
                                             a.Student.Programme.Department.SchoolId == schoolId.Value);
                }

                var total = await query.CountAsync();
                var pending = await query.CountAsync(a => a.Status == "Pending");
                var underReview = await query.CountAsync(a => a.Status == "UnderReview");
                var approved = await query.CountAsync(a => a.Status == "Approved");
                var rejected = await query.CountAsync(a => a.Status == "Rejected");
                var completed = await query.CountAsync(a => a.Status == "Completed");

                var byType = await query
                    .GroupBy(a => a.AppealType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                var pendingPayment = await query.CountAsync(a => a.AppealType == "Remark" && !a.FeePaid);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        total,
                        pending,
                        underReview,
                        approved,
                        rejected,
                        completed,
                        pendingPayment,
                        byType
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get departments by school
        [HttpGet]
        public async Task<IActionResult> GetDepartmentsBySchool(int schoolId)
        {
            try
            {
                var departments = await _context.Departments
                    .Where(d => d.SchoolId == schoolId)
                    .OrderBy(d => d.Name)
                    .Select(d => new { d.Id, d.Name })
                    .ToListAsync();

                return Json(new { success = true, data = departments });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get programmes by department
        [HttpGet]
        public async Task<IActionResult> GetProgrammesByDepartment(int departmentId)
        {
            try
            {
                var programmes = await _context.Programmes
                    .Where(p => p.DepartmentId == departmentId)
                    .OrderBy(p => p.Name)
                    .Select(p => new { p.Id, p.Name })
                    .ToListAsync();

                return Json(new { success = true, data = programmes });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Export appeals to Excel
        [HttpGet]
        [Authorize(Roles = "Admin,Registrar,Dean,HOD")]
        public async Task<IActionResult> ExportToExcel(
            int? schoolId = null,
            int? departmentId = null,
            int? programmeId = null,
            int? academicYearId = null,
            string? status = null,
            string? appealType = null)
        {
            try
            {
                var query = _context.ResultAppeals
                    .Include(a => a.Student)
                        .ThenInclude(s => s.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                    .Include(a => a.Course)
                    .Include(a => a.AcademicYear)
                    .Where(a => a.DeletedAt == null);

                // Apply filters
                if (schoolId.HasValue)
                {
                    query = query.Where(a => a.Student.Programme.Department.SchoolId == schoolId);
                }

                if (departmentId.HasValue)
                {
                    query = query.Where(a => a.Student.Programme.DepartmentId == departmentId);
                }

                if (programmeId.HasValue)
                {
                    query = query.Where(a => a.Student.ProgrammeId == programmeId);
                }

                if (academicYearId.HasValue)
                {
                    query = query.Where(a => a.AcademicYearId == academicYearId);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(a => a.Status == status);
                }

                if (!string.IsNullOrEmpty(appealType))
                {
                    query = query.Where(a => a.AppealType == appealType);
                }

                var appeals = await query
                    .OrderByDescending(a => a.SubmissionDate)
                    .ToListAsync();

                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Result Appeals");

                // Header row styling
                var headerRow = 1;
                var headers = new[]
                {
                    "Student ID", "Student Name", "Programme", "School",
                    "Course Code", "Course Name", "Academic Year", "Semester",
                    "Appeal Type", "Status", "Submission Date", "Original Mark",
                    "Revised Mark", "Fee Required", "Fee Paid", "Reason",
                    "Response", "Response Date", "Final Decision", "Decision Date"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(headerRow, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#4F46E5");
                    cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                    cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                }

                // Data rows
                var row = 2;
                foreach (var appeal in appeals)
                {
                    worksheet.Cell(row, 1).Value = appeal.Student?.StudentId_Number ?? "N/A";
                    worksheet.Cell(row, 2).Value = appeal.Student?.FullName ?? "N/A";
                    worksheet.Cell(row, 3).Value = appeal.Student?.Programme?.Name ?? "N/A";
                    worksheet.Cell(row, 4).Value = appeal.Student?.Programme?.Department?.School?.Name ?? "N/A";
                    worksheet.Cell(row, 5).Value = appeal.Course?.CourseCode ?? "N/A";
                    worksheet.Cell(row, 6).Value = appeal.Course?.CourseName ?? "N/A";
                    worksheet.Cell(row, 7).Value = appeal.AcademicYear?.YearValue ?? "N/A";
                    worksheet.Cell(row, 8).Value = appeal.Semester;
                    worksheet.Cell(row, 9).Value = FormatAppealType(appeal.AppealType);
                    worksheet.Cell(row, 10).Value = FormatStatus(appeal.Status);
                    worksheet.Cell(row, 11).Value = appeal.SubmissionDate.ToString("yyyy-MM-dd HH:mm");
                    worksheet.Cell(row, 12).Value = appeal.OriginalMark?.ToString("F2") ?? "-";
                    worksheet.Cell(row, 13).Value = appeal.RevisedMark?.ToString("F2") ?? "-";
                    worksheet.Cell(row, 14).Value = appeal.AppealFee > 0 ? $"K{appeal.AppealFee:N2}" : "No";
                    worksheet.Cell(row, 15).Value = appeal.FeePaid ? "Yes" : "No";
                    worksheet.Cell(row, 16).Value = appeal.Reason ?? "";
                    worksheet.Cell(row, 17).Value = appeal.Response ?? "";
                    worksheet.Cell(row, 18).Value = appeal.ResponseDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
                    worksheet.Cell(row, 19).Value = appeal.FinalDecision ?? "";
                    worksheet.Cell(row, 20).Value = appeal.DecisionDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";

                    // Alternate row coloring
                    if (row % 2 == 0)
                    {
                        worksheet.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F8FAFC");
                    }

                    row++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                // Add filters
                worksheet.RangeUsed().SetAutoFilter();

                // Generate file
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"ResultAppeals_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private string FormatAppealType(string type)
        {
            return type switch
            {
                "Remark" => "Remark",
                "Review" => "Review",
                "Recalculation" => "Recalculation",
                "MissingMarks" => "Missing Marks",
                "GradeDispute" => "Grade Dispute",
                "Other" => "Other",
                _ => type
            };
        }

        private string FormatStatus(string status)
        {
            return status switch
            {
                "Pending" => "Pending",
                "UnderReview" => "Under Review",
                "Approved" => "Approved",
                "Rejected" => "Rejected",
                "Completed" => "Completed",
                "Cancelled" => "Cancelled",
                _ => status
            };
        }

        #endregion

        #region Helper Methods

        private async Task LogStatusChange(int appealId, string fromStatus, string toStatus, string? comments, string? userId)
        {
            var history = new AppealStatusHistory
            {
                AppealId = appealId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                Comments = comments,
                ChangedAt = DateTime.Now.AddHours(2),
                ChangedBy = userId,
                CreatedAt = DateTime.Now.AddHours(2),
                CreatedBy = userId ?? "System"
            };

            _context.AppealStatusHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        private async Task LoadStudentViewBagData(int studentId)
        {
            var student = await _context.Students
                .Include(s => s.AcademicYear)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            ViewBag.AcademicYears = await _context.AcademicYears
                .Where(ay => ay.IsActive)
                .OrderByDescending(ay => ay.YearValue)
                .Select(ay => new SelectListItem
                {
                    Value = ay.YearId.ToString(),
                    Text = ay.YearValue,
                    Selected = student != null && ay.YearId == student.AcademicYearId
                })
                .ToListAsync();

            ViewBag.AppealTypes = GetAppealTypes();
            ViewBag.RemarkFee = REMARK_FEE;
        }

        private async Task LoadAdminViewBagData()
        {
            ViewBag.Schools = await _context.Schools
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                })
                .ToListAsync();

            ViewBag.AcademicYears = await _context.AcademicYears
                .Where(ay => ay.IsActive)
                .OrderByDescending(ay => ay.YearValue)
                .Select(ay => new SelectListItem
                {
                    Value = ay.YearId.ToString(),
                    Text = ay.YearValue
                })
                .ToListAsync();

            ViewBag.AppealTypes = GetAppealTypes();
            ViewBag.Statuses = GetStatuses();
            ViewBag.RemarkFee = REMARK_FEE;
        }

        private List<SelectListItem> GetAppealTypes()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Remark", Text = "Remark (Re-marking of script) - K500 Fee" },
                new SelectListItem { Value = "Review", Text = "Review (Review of marking)" },
                new SelectListItem { Value = "Recalculation", Text = "Recalculation (Check totals)" },
                new SelectListItem { Value = "MissingMarks", Text = "Missing Marks" },
                new SelectListItem { Value = "GradeDispute", Text = "Grade Dispute" },
                new SelectListItem { Value = "Other", Text = "Other" }
            };
        }

        private List<SelectListItem> GetStatuses()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Pending", Text = "Pending" },
                new SelectListItem { Value = "UnderReview", Text = "Under Review" },
                new SelectListItem { Value = "Approved", Text = "Approved" },
                new SelectListItem { Value = "Rejected", Text = "Rejected" },
                new SelectListItem { Value = "Completed", Text = "Completed" }
            };
        }

        #endregion
    }
}
