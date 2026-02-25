using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Fees;
using System.Linq;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    [Route("Admin/[action]")]
    public class OtherFeesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OtherFeesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> OtherFees()
        {
            ViewBag.AcademicYears = await _context.AcademicYears
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.YearId)
                .Select(a => new SelectListItem
                {
                    Value = a.YearId.ToString(),
                    Text = a.YearValue
                }).ToListAsync();

            ViewBag.Schools = await _context.Schools
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                }).ToListAsync();

            ViewBag.Programmes = await _context.Programmes
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                }).ToListAsync();

            ViewBag.ModesOfStudy = await _context.ModesOfStudy
                .OrderBy(m => m.ModeId)
                .Select(m => new SelectListItem
                {
                    Value = m.ModeId.ToString(),
                    Text = m.ModeName
                }).ToListAsync();

            ViewBag.ProgramLevels = await _context.ProgramLevels
                .Where(p => p.IsActive)
                .OrderBy(p => p.Id)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                }).ToListAsync();

            return View("~/Views/Admin/OtherFees.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> GetOtherFeesData([FromBody] OtherFeesFilterRequest request)
        {
            var query = _context.OtherFees
                .Include(o => o.AcademicYear)
                .Include(o => o.School)
                .Include(o => o.Programme)
                .Include(o => o.ModeOfStudy)
                .Include(o => o.ProgramLevel)
                .Where(o => o.IsActive)
                .AsQueryable();

            if (request.AcademicYearId.HasValue)
                query = query.Where(o => o.AcademicYearId == request.AcademicYearId.Value);

            if (request.Semester.HasValue)
                query = query.Where(o => o.Semester == request.Semester.Value);

            if (request.SchoolId.HasValue)
                query = query.Where(o => o.SchoolId == request.SchoolId.Value);

            if (request.ProgrammeId.HasValue)
                query = query.Where(o => o.ProgrammeId == request.ProgrammeId.Value);

            if (request.ModeOfStudyId.HasValue)
                query = query.Where(o => o.ModeOfStudyId == request.ModeOfStudyId.Value);

            if (request.ProgramLevelId.HasValue)
                query = query.Where(o => o.ProgramLevelId == request.ProgramLevelId.Value);

            // Fixed StudentType filter
            if (!string.IsNullOrEmpty(request.StudentType))
            {
                if (bool.TryParse(request.StudentType, out bool isForeign))
                {
                    query = query.Where(o => o.AppliesOnlyToForeignStudents == isForeign);
                }
            }

            if (!string.IsNullOrEmpty(request.SearchTerm))
                query = query.Where(o => o.FeeName.Contains(request.SearchTerm));

            var totalRecords = await query.CountAsync();

            query = request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(o => EF.Property<object>(o, request.SortColumn ?? "FeeName"))
                : query.OrderByDescending(o => EF.Property<object>(o, request.SortColumn ?? "FeeName"));

            var fees = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(o => new
                {
                    o.Id,
                    o.FeeName,
                    o.Amount,
                    o.Semester,
                    AcademicYear = o.AcademicYear != null ? o.AcademicYear.YearValue : "All Years",
                    SchoolName = o.School != null ? o.School.Name : "All Schools",
                    ProgrammeName = o.Programme != null ? o.Programme.Name : "All Programmes",
                    ModeName = o.ModeOfStudy != null ? o.ModeOfStudy.ModeName : "All Modes",
                    LevelName = o.ProgramLevel != null ? o.ProgramLevel.Name : "All Levels",
                    o.AppliesOnlyToForeignStudents,
                    o.CreditNCode,
                    o.DebitNCode,
                    o.AcademicYearId,
                    o.SchoolId,
                    o.ProgrammeId,
                    o.ModeOfStudyId,
                    o.ProgramLevelId,
                    RowNumber = 0
                })
                .ToListAsync();

            var feesList = fees.Select((f, index) => new
            {
                f.Id,
                f.FeeName,
                f.Amount,
                f.Semester,
                f.AcademicYear,
                f.SchoolName,
                f.ProgrammeName,
                f.ModeName,
                f.LevelName,
                f.AppliesOnlyToForeignStudents,
                f.CreditNCode,
                f.DebitNCode,
                f.AcademicYearId,
                f.SchoolId,
                f.ProgrammeId,
                f.ModeOfStudyId,
                f.ProgramLevelId,
                RowNumber = (request.Page - 1) * request.PageSize + index + 1
            }).ToList();

            return Json(new
            {
                fees = feesList,
                totalRecords,
                totalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize),
                currentPage = request.Page
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOtherFee(OtherFees model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                model.CreatedBy = User.Identity?.Name;
                model.IsActive = true;

                _context.OtherFees.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Other fee created successfully!";
                return RedirectToAction(nameof(OtherFees));
            }

            TempData["ErrorMessage"] = "Failed to create other fee. Please check the form.";
            return RedirectToAction(nameof(OtherFees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOtherFee(OtherFees model)
        {
            if (ModelState.IsValid)
            {
                var existingFee = await _context.OtherFees.FindAsync(model.Id);
                if (existingFee == null)
                {
                    TempData["ErrorMessage"] = "Other fee not found!";
                    return RedirectToAction(nameof(OtherFees));
                }

                existingFee.FeeName = model.FeeName;
                existingFee.Amount = model.Amount;
                existingFee.Semester = model.Semester;
                existingFee.AcademicYearId = model.AcademicYearId;
                existingFee.SchoolId = model.SchoolId;
                existingFee.ProgrammeId = model.ProgrammeId;
                existingFee.ModeOfStudyId = model.ModeOfStudyId;
                existingFee.ProgramLevelId = model.ProgramLevelId;
                existingFee.AppliesOnlyToForeignStudents = model.AppliesOnlyToForeignStudents;
                existingFee.CreditNCode = model.CreditNCode;
                existingFee.DebitNCode = model.DebitNCode;
                existingFee.UpdatedAt = DateTime.Now;
                existingFee.UpdatedBy = User.Identity?.Name;

                _context.OtherFees.Update(existingFee);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Other fee updated successfully!";
                return RedirectToAction(nameof(OtherFees));
            }

            TempData["ErrorMessage"] = "Failed to update other fee. Please check the form.";
            return RedirectToAction(nameof(OtherFees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOtherFee(int Id)
        {
            var fee = await _context.OtherFees.FindAsync(Id);
            if (fee == null)
            {
                TempData["ErrorMessage"] = "Other fee not found!";
                return RedirectToAction(nameof(OtherFees));
            }

            fee.IsActive = false;
            fee.UpdatedAt = DateTime.Now;
            fee.UpdatedBy = User.Identity?.Name;

            _context.OtherFees.Update(fee);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Other fee deleted successfully!";
            return RedirectToAction(nameof(OtherFees));
        }

        [HttpGet]
        public async Task<IActionResult> GetOtherFeesStatistics()
        {
            var totalFees = await _context.OtherFees.Where(o => o.IsActive).CountAsync();
            var foreignFees = await _context.OtherFees.Where(o => o.IsActive && o.AppliesOnlyToForeignStudents).CountAsync();
            var standardFees = await _context.OtherFees.Where(o => o.IsActive && !o.AppliesOnlyToForeignStudents).CountAsync();
            var semester1Fees = await _context.OtherFees.Where(o => o.IsActive && o.Semester == 1).CountAsync();
            var semester2Fees = await _context.OtherFees.Where(o => o.IsActive && o.Semester == 2).CountAsync();

            var chartData = new
            {
                labels = new[] { "Foreign Student Fees", "Standard Fees", "Semester 1", "Semester 2" },
                series = new[] { foreignFees, standardFees, semester1Fees, semester2Fees }
            };

            return Json(new
            {
                totalFees,
                foreignFees,
                standardFees,
                semester1Fees,
                semester2Fees,
                chartData
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammesBySchool(int schoolId)
        {
            try
            {
                if (schoolId == 0)
                {
                    var allProgrammes = await _context.Programmes
                        .OrderBy(p => p.Name)
                        .Select(p => new SelectListItem
                        {
                            Value = p.Id.ToString(),
                            Text = p.Name
                        })
                        .ToListAsync();
                    
                    return Json(allProgrammes);
                }

                var programmes = await (
                    from p in _context.Programmes
                    join d in _context.Departments on p.DepartmentId equals d.Id
                    where d.SchoolId == schoolId
                    orderby p.Name
                    select new SelectListItem
                    {
                        Value = p.Id.ToString(),
                        Text = p.Name
                    }
                ).ToListAsync();

                return Json(programmes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetProgrammesBySchool: {ex.Message}");
                return Json(new List<SelectListItem>());
            }
        }
    }

    public class OtherFeesFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortColumn { get; set; }
        public string? SortDirection { get; set; }
        public int? AcademicYearId { get; set; }
        public int? Semester { get; set; }
        public int? SchoolId { get; set; }
        public int? ProgrammeId { get; set; }
        public int? ModeOfStudyId { get; set; }
        public int? ProgramLevelId { get; set; }
        public string? StudentType { get; set; }
        public string? SearchTerm { get; set; }
    }
}