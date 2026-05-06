using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Compliance;
using SIS.Models.StudentApplication;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin, Compliance, Registrar, Dean")]
    public class DisqualificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DisqualificationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            await LoadViewBagData();
            return View();
        }

        // Search students
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
                        ProgrammeName = s.Programme != null ? s.Programme.Name : "N/A",
                        SchoolName = s.School != null ? s.School.Name : "N/A",
                        AcademicYear = s.AcademicYear != null ? s.AcademicYear.YearValue : "N/A",
                        s.CurrentYearPeriodId,
                        s.AcademicYearId,
                        s.ProgrammeId,
                        s.StudentCurrentYear
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

        // Get student's registered courses
        [HttpGet]
        public async Task<IActionResult> GetStudentCourses(int studentId, int? academicYearId = null, int? period = null)
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

                // Get the target academic year and semester
                var targetAcademicYearId = academicYearId ?? student.AcademicYearId;
                var targetPeriod = period ?? student.CurrentYearPeriod.AcademicPeriod.Id;

                // Get registered courses for the student
                var registeredCourses = await _context.StudentCourseRegistrations
                    .Include(cr => cr.Course)
                    .ThenInclude(c => c.Programme)
                    .Where(cr => cr.StudentId == studentId &&
                                 cr.AcademicYearId == targetAcademicYearId &&
                                 (targetPeriod == null || cr.YearPeriodId == targetPeriod))
                    .Select(cr => new
                    {
                        cr.Course.Id,
                        cr.Course.CourseCode,
                        cr.Course.CourseName,
                        cr.Course.CourseType,
                        cr.Course.YearTaken,
                        cr.Course.PeriodTakenId,
                        ProgrammeName = cr.Course.Programme != null ? cr.Course.Programme.Name : "N/A",
                        cr.YearPeriodId,
                        cr.AcademicYearId
                    })
                    .Distinct()
                    .OrderBy(c => c.CourseCode)
                    .ToListAsync();

                // If no registered courses found, get courses from the student's programme
                if (!registeredCourses.Any())
                {
                    var programmeCourses = await _context.Courses
                        .Include(c => c.Programme)
                        .Where(c => c.ProgrammeID == student.ProgrammeId &&
                                    (targetPeriod == null || c.PeriodTakenId == targetPeriod))
                        .Select(c => new
                        {
                            c.Id,
                            c.CourseCode,
                            c.CourseName,
                            c.CourseType,
                            c.YearTaken,
                            c.PeriodTakenId,
                            ProgrammeName = c.Programme != null ? c.Programme.Name : "N/A",
                            Period = c.PeriodTakenId,
                            AcademicYearId = targetAcademicYearId
                        })
                        .OrderBy(c => c.CourseCode)
                        .ToListAsync();

                    return Json(new { success = true, data = programmeCourses, source = "programme" });
                }

                return Json(new { success = true, data = registeredCourses, source = "registration" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get course details
        [HttpGet]
        public async Task<IActionResult> GetCourseDetails(int courseId)
        {
            try
            {
                var course = await _context.Courses
                    .Include(c => c.Programme)
                    .Where(c => c.Id == courseId)
                    .Select(c => new
                    {
                        c.Id,
                        c.CourseCode,
                        c.CourseName,
                        c.CourseType,
                        c.CourseDescription,
                        c.YearTaken,
                        c.PeriodTakenId,
                        ProgrammeName = c.Programme != null ? c.Programme.Name : "N/A"
                    })
                    .FirstOrDefaultAsync();

                if (course == null)
                {
                    return Json(new { success = false, message = "Course not found" });
                }

                return Json(new { success = true, data = course });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get student's disqualifications
        [HttpGet]
        public async Task<IActionResult> GetStudentDisqualifications(int studentId)
        {
            try
            {
                var disqualifications = await _context.StudentDisqualifications
                    .Include(d => d.Course)
                    .Include(d => d.AcademicYear)
                    .Where(d => d.StudentId == studentId && d.DeletedAt == null)
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new
                    {
                        d.Id,
                        d.CourseId,
                        CourseCode = d.Course != null ? d.Course.CourseCode : "N/A",
                        CourseName = d.Course != null ? d.Course.CourseName : "N/A",
                        AcademicYear = d.AcademicYear != null ? d.AcademicYear.YearValue : "N/A",
                        d.YearPeriodId,
                        d.DisqualificationType,
                        d.Description,
                        d.IncidentDate,
                        d.DisqualificationDate,
                        d.Status,
                        d.PenaltyDescription,
                        d.IsBannedFromCourse,
                        d.IsSuspended,
                        d.YearsSuspended,
                        d.AppealStatus,
                        d.CreatedAt
                    })
                    .ToListAsync();

                return Json(new { success = true, data = disqualifications });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get single disqualification details
        [HttpGet]
        public async Task<IActionResult> GetDisqualification(int id)
        {
            try
            {
                var disqualification = await _context.StudentDisqualifications
                    .Include(d => d.Course)
                    .Include(d => d.AcademicYear)
                    .Include(d => d.Student)
                    .Where(d => d.Id == id && d.DeletedAt == null)
                    .Select(d => new
                    {
                        d.Id,
                        d.StudentId,
                        d.CourseId,
                        CourseCode = d.Course != null ? d.Course.CourseCode : "N/A",
                        CourseName = d.Course != null ? d.Course.CourseName : "N/A",
                        d.AcademicYearId,
                        d.YearPeriodId,
                        d.DisqualificationType,
                        d.Description,
                        d.EvidenceReference,
                        d.IncidentDate,
                        d.DisqualificationDate,
                        d.Status,
                        d.PenaltyDescription,
                        d.PenaltyDurationSemesters,
                        d.IsBannedFromCourse,
                        d.IsSuspended,
                        d.YearsSuspended,
                        d.AppealDate,
                        d.AppealDescription,
                        d.AppealStatus,
                        d.AppealDecision,
                        d.AppealDecisionDate,
                        d.ResolvedDate,
                        d.ResolutionNotes
                    })
                    .FirstOrDefaultAsync();

                if (disqualification == null)
                {
                    return Json(new { success = false, message = "Disqualification record not found" });
                }

                return Json(new { success = true, data = disqualification });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Create disqualification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDisqualification([FromForm] StudentDisqualification model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                // Check if student already has a disqualification for this course in the same academic year/semester
                var existingDisqualification = await _context.StudentDisqualifications
                    .FirstOrDefaultAsync(d =>
                        d.StudentId == model.StudentId &&
                        d.CourseId == model.CourseId &&
                        d.AcademicYearId == model.AcademicYearId &&
                        d.YearPeriodId == model.YearPeriodId &&
                        d.DeletedAt == null);

                if (existingDisqualification != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "A disqualification record already exists for this student in this course for the selected academic year and semester."
                    });
                }

                model.CreatedAt = DateTime.Now.AddHours(2);
                model.CreatedBy = user?.Id ?? "System";
                model.Status = "Pending";

                // Set default YearsSuspended if suspended but value not provided
                if (model.IsSuspended && model.YearsSuspended <= 0)
                {
                    model.YearsSuspended = 2;
                }

                // If banned from course, clear suspension fields
                if (model.IsBannedFromCourse)
                {
                    model.IsSuspended = false;
                    model.YearsSuspended = 0;
                }

                _context.StudentDisqualifications.Add(model);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Disqualification record created successfully",
                    id = model.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Update disqualification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDisqualification(int id, [FromForm] StudentDisqualification model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var disqualification = await _context.StudentDisqualifications
                    .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null);

                if (disqualification == null)
                {
                    return Json(new { success = false, message = "Disqualification record not found" });
                }

                // Update properties
                disqualification.CourseId = model.CourseId;
                disqualification.AcademicYearId = model.AcademicYearId;
                disqualification.YearPeriodId = model.YearPeriodId;
                disqualification.DisqualificationType = model.DisqualificationType;
                disqualification.Description = model.Description;
                disqualification.EvidenceReference = model.EvidenceReference;
                disqualification.IncidentDate = model.IncidentDate;
                disqualification.DisqualificationDate = model.DisqualificationDate;
                disqualification.Status = model.Status;
                disqualification.PenaltyDescription = model.PenaltyDescription;
                disqualification.PenaltyDurationSemesters = model.PenaltyDurationSemesters;
                disqualification.IsBannedFromCourse = model.IsBannedFromCourse;
                disqualification.IsSuspended = model.IsSuspended;
                disqualification.YearsSuspended = model.YearsSuspended;
                disqualification.UpdatedAt = DateTime.Now.AddHours(2);
                disqualification.UpdatedBy = user?.Id;

                // Set default YearsSuspended if suspended but value not provided
                if (disqualification.IsSuspended && disqualification.YearsSuspended <= 0)
                {
                    disqualification.YearsSuspended = 2;
                }

                // If banned from course, clear suspension fields
                if (disqualification.IsBannedFromCourse)
                {
                    disqualification.IsSuspended = false;
                    disqualification.YearsSuspended = 0;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Disqualification record updated successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Update appeal status
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAppeal(int id, [FromForm] string appealDescription, [FromForm] string appealStatus, [FromForm] string? appealDecision)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var disqualification = await _context.StudentDisqualifications
                    .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null);

                if (disqualification == null)
                {
                    return Json(new { success = false, message = "Disqualification record not found" });
                }

                disqualification.AppealDate = DateTime.Now.AddHours(2);
                disqualification.AppealDescription = appealDescription;
                disqualification.AppealStatus = appealStatus;

                if (!string.IsNullOrEmpty(appealDecision))
                {
                    disqualification.AppealDecision = appealDecision;
                    disqualification.AppealDecisionDate = DateTime.Now.AddHours(2);

                    if (appealStatus == "Approved")
                    {
                        disqualification.Status = "Overturned";
                        disqualification.ResolvedDate = DateTime.Now.AddHours(2);

                        // Clear suspension/ban if appeal is approved
                        disqualification.IsBannedFromCourse = false;
                        disqualification.IsSuspended = false;
                        disqualification.YearsSuspended = 0;
                    }
                }

                disqualification.UpdatedAt = DateTime.Now.AddHours(2);
                disqualification.UpdatedBy = user?.Id;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Appeal updated successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Confirm disqualification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDisqualification(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var disqualification = await _context.StudentDisqualifications
                    .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null);

                if (disqualification == null)
                {
                    return Json(new { success = false, message = "Disqualification record not found" });
                }

                disqualification.Status = "Confirmed";
                disqualification.UpdatedAt = DateTime.Now.AddHours(2);
                disqualification.UpdatedBy = user?.Id;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Disqualification confirmed successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Soft delete disqualification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDisqualification(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var disqualification = await _context.StudentDisqualifications
                    .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null);

                if (disqualification == null)
                {
                    return Json(new { success = false, message = "Disqualification record not found" });
                }

                disqualification.DeletedAt = DateTime.Now.AddHours(2);
                disqualification.UpdatedAt = DateTime.Now.AddHours(2);
                disqualification.UpdatedBy = user?.Id;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Disqualification record deleted successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get statistics for dashboard
        [HttpGet]
        public async Task<IActionResult> GetStatistics(int? academicYearId = null, int? yearPeriod = null, string? status = null)
        {
            try
            {
                var query = _context.StudentDisqualifications
                    .Where(d => d.DeletedAt == null)
                    .AsQueryable();

                if (academicYearId.HasValue)
                {
                    query = query.Where(d => d.AcademicYearId == academicYearId.Value);
                }

                if (yearPeriod.HasValue)
                {
                    query = query.Where(d => d.YearPeriodId == yearPeriod.Value);
                }

                var total = await query.CountAsync();
                var pending = await query.CountAsync(d => d.Status == "Pending");
                var confirmed = await query.CountAsync(d => d.Status == "Confirmed");
                var appealed = await query.CountAsync(d => d.Status == "Appealed");
                var overturned = await query.CountAsync(d => d.Status == "Overturned");
                var completed = await query.CountAsync(d => d.Status == "Completed");

                // Get suspension statistics
                var suspended = await query.CountAsync(d => d.IsSuspended && d.Status == "Confirmed");
                var bannedFromCourse = await query.CountAsync(d => d.IsBannedFromCourse && d.Status == "Confirmed");

                // Get top disqualification types
                var topTypes = await query
                    .GroupBy(d => d.DisqualificationType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        total,
                        pending,
                        confirmed,
                        appealed,
                        overturned,
                        completed,
                        suspended,
                        bannedFromCourse,
                        topTypes
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get all disqualifications (for reports) - UPDATED VERSION
        [HttpGet]
        public async Task<IActionResult> GetAllDisqualifications(
            int? academicYearId = null,
            int? yearPeriod = null,
            string? status = null,
            string? type = null,
            string? searchTerm = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            try
            {
                var query = _context.StudentDisqualifications
                    .Include(d => d.Student)
                    .Include(d => d.Course)
                    .Include(d => d.AcademicYear)
                    .Where(d => d.DeletedAt == null)
                    .AsQueryable();

                if (academicYearId.HasValue)
                {
                    query = query.Where(d => d.AcademicYearId == academicYearId.Value);
                }

                if (yearPeriod.HasValue)
                {
                    query = query.Where(d => d.YearPeriodId == yearPeriod.Value);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(d => d.Status == status);
                }

                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(d => d.DisqualificationType == type);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(d =>
                        d.Student.StudentId_Number.ToLower().Contains(searchTerm) ||
                        d.Student.FullName.ToLower().Contains(searchTerm) ||
                        d.Course.CourseCode.ToLower().Contains(searchTerm) ||
                        d.Course.CourseName.ToLower().Contains(searchTerm)
                    );
                }

                var totalCount = await query.CountAsync();
                var disqualifications = await query
                    .OrderByDescending(d => d.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(d => new
                    {
                        d.Id,
                        StudentId = d.Student != null ? d.Student.StudentId_Number : "N/A",
                        StudentName = d.Student != null ? d.Student.FullName : "N/A",
                        CourseCode = d.Course != null ? d.Course.CourseCode : "N/A",
                        CourseName = d.Course != null ? d.Course.CourseName : "N/A",
                        AcademicYear = d.AcademicYear != null ? d.AcademicYear.YearValue : "N/A",
                        d.YearPeriodId,
                        d.DisqualificationType,
                        d.Status,
                        d.IsBannedFromCourse,
                        d.IsSuspended,
                        d.YearsSuspended,
                        d.IncidentDate,
                        d.CreatedAt
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = disqualifications,
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

        // Get all disqualifications (for reports)
        /*[HttpGet]
        public async Task<IActionResult> GetAllDisqualifications(int? academicYearId = null, string? status = null, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                var query = _context.StudentDisqualifications
                    .Include(d => d.Student)
                    .Include(d => d.Course)
                    .Include(d => d.AcademicYear)
                    .Where(d => d.DeletedAt == null)
                    .AsQueryable();

                if (academicYearId.HasValue)
                {
                    query = query.Where(d => d.AcademicYearId == academicYearId.Value);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(d => d.Status == status);
                }

                var totalCount = await query.CountAsync();
                var disqualifications = await query
                    .OrderByDescending(d => d.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(d => new
                    {
                        d.Id,
                        StudentId = d.Student != null ? d.Student.StudentId_Number : "N/A",
                        StudentName = d.Student != null ? d.Student.FullName : "N/A",
                        CourseCode = d.Course != null ? d.Course.CourseCode : "N/A",
                        CourseName = d.Course != null ? d.Course.CourseName : "N/A",
                        AcademicYear = d.AcademicYear != null ? d.AcademicYear.YearValue : "N/A",
                        d.Semester,
                        d.DisqualificationType,
                        d.Status,
                        d.IsBannedFromCourse,
                        d.IsSuspended,
                        d.YearsSuspended,
                        d.IncidentDate,
                        d.CreatedAt
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = disqualifications,
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
        }*/

        // Get suspended students
        [HttpGet]
        public async Task<IActionResult> GetSuspendedStudents(int? academicYearId = null)
        {
            try
            {
                var query = _context.StudentDisqualifications
                    .Include(d => d.Student)
                    .ThenInclude(s => s.Programme)
                    .Include(d => d.Course)
                    .Include(d => d.AcademicYear)
                    .Where(d => d.DeletedAt == null && d.IsSuspended && d.Status == "Confirmed")
                    .AsQueryable();

                if (academicYearId.HasValue)
                {
                    query = query.Where(d => d.AcademicYearId == academicYearId.Value);
                }

                var suspendedStudents = await query
                    .OrderByDescending(d => d.DisqualificationDate)
                    .Select(d => new
                    {
                        d.Id,
                        StudentId = d.Student != null ? d.Student.StudentId_Number : "N/A",
                        StudentName = d.Student != null ? d.Student.FullName : "N/A",
                        Programme = d.Student != null && d.Student.Programme != null ? d.Student.Programme.Name : "N/A",
                        CourseCode = d.Course != null ? d.Course.CourseCode : "N/A",
                        CourseName = d.Course != null ? d.Course.CourseName : "N/A",
                        AcademicYear = d.AcademicYear != null ? d.AcademicYear.YearValue : "N/A",
                        d.YearPeriodId,
                        d.DisqualificationType,
                        d.YearsSuspended,
                        d.DisqualificationDate,
                        SuspensionEndDate = d.DisqualificationDate.AddYears(d.YearsSuspended)
                    })
                    .ToListAsync();

                return Json(new { success = true, data = suspendedStudents });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Check if student is currently suspended
        [HttpGet]
        public async Task<IActionResult> CheckStudentSuspension(int studentId)
        {
            try
            {
                var activeSuspension = await _context.StudentDisqualifications
                    .Include(d => d.Course)
                    .Include(d => d.AcademicYear)
                    .Where(d => d.StudentId == studentId &&
                                d.DeletedAt == null &&
                                d.IsSuspended &&
                                d.Status == "Confirmed")
                    .OrderByDescending(d => d.DisqualificationDate)
                    .FirstOrDefaultAsync();

                if (activeSuspension == null)
                {
                    return Json(new { success = true, isSuspended = false });
                }

                var suspensionEndDate = activeSuspension.DisqualificationDate.AddYears(activeSuspension.YearsSuspended);
                var isCurrentlySuspended = DateTime.Now.AddHours(2) < suspensionEndDate;

                return Json(new
                {
                    success = true,
                    isSuspended = isCurrentlySuspended,
                    suspensionDetails = isCurrentlySuspended ? new
                    {
                        activeSuspension.Id,
                        CourseCode = activeSuspension.Course?.CourseCode ?? "N/A",
                        CourseName = activeSuspension.Course?.CourseName ?? "N/A",
                        AcademicYear = activeSuspension.AcademicYear?.YearValue ?? "N/A",
                        activeSuspension.DisqualificationType,
                        activeSuspension.YearsSuspended,
                        activeSuspension.DisqualificationDate,
                        SuspensionEndDate = suspensionEndDate,
                        RemainingDays = (suspensionEndDate - DateTime.Now.AddHours(2)).Days
                    } : null
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Helper method to load ViewBag data
        private async Task LoadViewBagData()
        {
            ViewBag.AcademicYears = await _context.AcademicYears
                .Where(ay => ay.IsActive)
                .OrderByDescending(ay => ay.YearValue)
                .Select(ay => new SelectListItem
                {
                    Value = ay.YearId.ToString(),
                    Text = ay.YearValue
                })
                .ToListAsync();

            ViewBag.DisqualificationTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Malpractice", Text = "Exam Malpractice" },
                new SelectListItem { Value = "Plagiarism", Text = "Plagiarism" },
                new SelectListItem { Value = "Cheating", Text = "Cheating" },
                new SelectListItem { Value = "Impersonation", Text = "Impersonation" },
                new SelectListItem { Value = "UnauthorizedMaterial", Text = "Unauthorized Material" },
                new SelectListItem { Value = "Collusion", Text = "Collusion" },
                new SelectListItem { Value = "Other", Text = "Other" }
            };

            ViewBag.Statuses = new List<SelectListItem>
            {
                new SelectListItem { Value = "Pending", Text = "Pending" },
                new SelectListItem { Value = "Confirmed", Text = "Confirmed" },
                new SelectListItem { Value = "Appealed", Text = "Appealed" },
                new SelectListItem { Value = "Overturned", Text = "Overturned" },
                new SelectListItem { Value = "Completed", Text = "Completed" }
            };
        }
    }
}