using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.StudentAccommodation;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    public class AccommodationPeriodController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccommodationPeriodController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AccommodationPeriod/Index
        public async Task<IActionResult> Index()
        {
            var periods = await _context.AccommodationPeriods
                .Include(p => p.School)
                .Include(p => p.Programme)
                .Include(p => p.ModeOfStudy)
                .Include(p => p.Applications)
                .Include(p => p.ProgramLevel)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Load dropdown data for create/edit
            await LoadDropdownData();

            return View("~/Views/Accommodation/AccommodationPeriod_Index.cshtml", periods);
        }

        // GET: AccommodationPeriod/GetDetails/{id}
        [HttpGet]
        [Route("AccommodationPeriod/GetPeriod/{id}")]
        public async Task<IActionResult> GetDetails(int id)
        {
            try
            {
                var period = await _context.AccommodationPeriods
                    .Include(p => p.School)
                    .Include(p => p.Programme)
                    .Include(p => p.ModeOfStudy)
                    .Include(p => p.Applications)
                    .Include(p => p.ProgramLevel)
                    .FirstOrDefaultAsync(p => p.PeriodId == id);

                if (period == null)
                    return NotFound();

                // Create a clean DTO to avoid circular references
                var result = new
                {
                    periodId = period.PeriodId,
                    startDate = period.StartDate,
                    endDate = period.EndDate,
                    type = period.Type,
                    typeOfPayment = period.TypeOfPayment,
                    typeOfPaymentAmount = period.TypeOfPaymentAmount,
                    applicationStartDate = period.ApplicationStartDate,
                    applicationEndDate = period.ApplicationEndDate,
                    status = (int)period.Status,
                    schoolId = period.SchoolId,
                    school = period.School != null ? new { name = period.School.Name } : null,
                    programmeId = period.ProgrammeId,
                    programme = period.Programme != null ? new { name = period.Programme.Name } : null,
                    modeOfStudyId = period.ModeOfStudyId,
                    modeOfStudy = period.ModeOfStudy != null ? new { modeName = period.ModeOfStudy.ModeName } : null,
                    yearOfStudy = period.YearOfStudy,
                    programLevelId = period.ProgramLevelId,
                    programLevel = period.ProgramLevel != null ? new { name = period.ProgramLevel.Name } : null,
                    isPermanentUntilGraduation = period.IsPermanentUntilGraduation,
                    appliesUniversally = period.AppliesUniversally,
                    createdBy = period.CreatedBy,
                    createdAt = period.CreatedAt,
                    updatedBy = period.UpdatedBy,
                    updatedAt = period.UpdatedAt,
                    applications = period.Applications.Select(a => new
                    {
                        applicationId = a.ApplicationId,
                        status = (int)a.Status
                    }).ToList()
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading period details: {ex.Message}");
            }
        }

        // POST: AccommodationPeriod/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AccommodationPeriod model)
        {
            try
            {
                // Remove navigation properties from ModelState
                ModelState.Remove("School");
                ModelState.Remove("Programme");
                ModelState.Remove("ModeOfStudy");
                ModelState.Remove("ProgramLevel");
                ModelState.Remove("Applications");

                // Set audit fields
                model.CreatedAt = DateTime.Now.AddHours(2);
                model.CreatedBy = User.Identity?.Name ?? "System";

                // If applies universally, clear eligibility fields
                if (model.AppliesUniversally)
                {
                    model.SchoolId = null;
                    model.ProgrammeId = null;
                    model.ModeOfStudyId = null;
                    model.YearOfStudy = null;
                    model.ProgramLevelId = null;
                }

                // If permanent, clear end date
                if (model.IsPermanentUntilGraduation)
                {
                    model.EndDate = null;
                }

                _context.AccommodationPeriods.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Accommodation period created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error creating period: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: AccommodationPeriod/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AccommodationPeriod model)
        {
            try
            {
                // Remove navigation properties from ModelState
                ModelState.Remove("School");
                ModelState.Remove("Programme");
                ModelState.Remove("ModeOfStudy");
                ModelState.Remove("ProgramLevel");
                ModelState.Remove("Applications");

                var period = await _context.AccommodationPeriods
                    .FirstOrDefaultAsync(p => p.PeriodId == model.PeriodId);

                if (period == null)
                {
                    TempData["ErrorMessage"] = "Period not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Update fields
                period.Type = model.Type;
                period.TypeOfPayment = model.TypeOfPayment;
                period.TypeOfPaymentAmount = model.TypeOfPaymentAmount;
                period.StartDate = model.StartDate;
                period.EndDate = model.EndDate;
                period.ApplicationStartDate = model.ApplicationStartDate;
                period.ApplicationEndDate = model.ApplicationEndDate;
                period.IsPermanentUntilGraduation = model.IsPermanentUntilGraduation;
                period.AppliesUniversally = model.AppliesUniversally;
                period.Status = model.Status;

                // Update eligibility criteria
                if (model.AppliesUniversally)
                {
                    period.SchoolId = null;
                    period.ProgrammeId = null;
                    period.ModeOfStudyId = null;
                    period.YearOfStudy = null;
                    period.ProgramLevelId = null;
                }
                else
                {
                    period.SchoolId = model.SchoolId;
                    period.ProgrammeId = model.ProgrammeId;
                    period.ModeOfStudyId = model.ModeOfStudyId;
                    period.YearOfStudy = model.YearOfStudy;
                    period.ProgramLevelId = model.ProgramLevelId;
                }

                // If permanent, clear end date
                if (period.IsPermanentUntilGraduation)
                {
                    period.EndDate = null;
                }

                // Update audit fields
                period.UpdatedAt = DateTime.Now.AddHours(2);
                period.UpdatedBy = User.Identity?.Name ?? "System";

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Accommodation period updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating period: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: AccommodationPeriod/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var period = await _context.AccommodationPeriods
                    .Include(p => p.Applications)
                    .FirstOrDefaultAsync(p => p.PeriodId == id);

                if (period == null)
                {
                    TempData["ErrorMessage"] = "Period not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if there are any applications
                if (period.Applications != null && period.Applications.Any())
                {
                    TempData["ErrorMessage"] = "Cannot delete period with existing applications. Please close or archive the period instead.";
                    return RedirectToAction(nameof(Index));
                }

                _context.AccommodationPeriods.Remove(period);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Accommodation period deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting period: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        #region API Endpoints for Dropdowns

        // GET: AccommodationPeriod/GetSchools
        [HttpGet]
        public async Task<IActionResult> GetSchools()
        {
            try
            {
                var schools = await _context.Schools
                    .OrderBy(s => s.Name)
                    .Select(s => new { value = s.Id, text = s.Name })
                    .ToListAsync();

                return Json(schools);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading schools: {ex.Message}");
            }
        }

        // GET: AccommodationPeriod/GetProgrammes
        [HttpGet]
        public async Task<IActionResult> GetProgrammes(int schoolId)
        {
            try
            {
                var programmes = await _context.Programmes
                    .Include(p => p.Department)
                    .Where(p => p.Department.SchoolId == schoolId)
                    .OrderBy(p => p.Name)
                    .Select(p => new { value = p.Id, text = p.Name })
                    .ToListAsync();

                return Json(programmes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading programmes: {ex.Message}");
            }
        }

        // GET: AccommodationPeriod/GetModesOfStudy
        [HttpGet]
        public async Task<IActionResult> GetModesOfStudy()
        {
            try
            {
                var modes = await _context.ModesOfStudy
                    .OrderBy(m => m.ModeName)
                    .Select(m => new { value = m.ModeId, text = m.ModeName })
                    .ToListAsync();

                return Json(modes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading modes of study: {ex.Message}");
            }
        }

        // GET: AccommodationPeriod/GetProgramLevels
        [HttpGet]
        public async Task<IActionResult> GetProgramLevels()
        {
            try
            {
                var levels = await _context.ProgramLevels
                    .Where(l => l.IsActive)
                    .OrderBy(l => l.Rank)
                    .Select(l => new { value = l.Id, text = l.Name })
                    .ToListAsync();

                return Json(levels);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading program levels: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private async Task LoadDropdownData()
        {
            // Schools
            ViewBag.Schools = await _context.Schools
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToListAsync();

            // Modes of Study
            ViewBag.ModesOfStudy = await _context.ModesOfStudy
                .OrderBy(m => m.ModeName)
                .Select(m => new SelectListItem { Value = m.ModeId.ToString(), Text = m.ModeName })
                .ToListAsync();

            // Program Levels
            ViewBag.ProgramLevels = await _context.ProgramLevels
                .Where(l => l.IsActive)
                .OrderBy(l => l.Rank)
                .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Name })
                .ToListAsync();

            // Status options
            ViewBag.StatusOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "0", Text = "Inactive" },
                new SelectListItem { Value = "1", Text = "Active" },
                new SelectListItem { Value = "2", Text = "Upcoming" },
                new SelectListItem { Value = "3", Text = "Closed" }
            };

            // Type options (Period Type)
            ViewBag.TypeOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Semester", Text = "Semester" },
                new SelectListItem { Value = "Year", Text = "Year" },
                new SelectListItem { Value = "Custom", Text = "Custom" }
            };

            // Payment Type options
            ViewBag.PaymentTypeOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Semester", Text = "Per Semester" },
                new SelectListItem { Value = "Year", Text = "Per Year" },
                new SelectListItem { Value = "PerDay", Text = "Per Day" },
                new SelectListItem { Value = "Fixed", Text = "Fixed Amount" }
            };

            // Year of Study options
            ViewBag.YearOfStudyOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "All Years" },
                new SelectListItem { Value = "1", Text = "Year 1" },
                new SelectListItem { Value = "2", Text = "Year 2" },
                new SelectListItem { Value = "3", Text = "Year 3" },
                new SelectListItem { Value = "4", Text = "Year 4" },
                new SelectListItem { Value = "5", Text = "Year 5" },
                new SelectListItem { Value = "6", Text = "Year 6" },
                new SelectListItem { Value = "7", Text = "Year 7" }
            };
        }

        #endregion
    }
}