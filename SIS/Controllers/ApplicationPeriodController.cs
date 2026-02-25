using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Applications;
using SIS.Models.Registration;
using SIS.Models.Admin;
using SIS.Models.StudentApplication;

namespace SIS.Controllers.ApplicationControllers
{
    [Authorize(Roles = "Admin, Registrar")]
    public class ApplicationPeriodController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ApplicationPeriodController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: ApplicationPeriod/Index
        public async Task<IActionResult> Index()
        {
            var periods = await _context.ApplicationPeriods
                .Include(ap => ap.ModeOfStudies)
                .Include(ap => ap.Programms)
                .Include(ap => ap.Applicants)
                .OrderByDescending(ap => ap.Year)
                .ThenByDescending(ap => ap.StartOfApplication)
                .ToListAsync();

            await PopulateViewBag();

            return View("~/Views/ApplicationPeriod/ApplicationPeriodIndex.cshtml", periods);
        }

        // GET: ApplicationPeriod/GetApplicationPeriod/5
        [HttpGet]
        public async Task<IActionResult> GetApplicationPeriod(int id)
        {
            try
            {
                var period = await _context.ApplicationPeriods
                    .Include(ap => ap.ModeOfStudies)
                    .Include(ap => ap.Programms)
                    .Include(ap => ap.Applicants)
                    .FirstOrDefaultAsync(ap => ap.Id == id);

                if (period == null)
                {
                    return NotFound("Application period not found");
                }

                var periodDto = new
                {
                    id = period.Id,
                    name = period.Name,
                    description = period.Description,
                    startOfApplication = period.StartOfApplication,
                    endOfApplication = period.EndOfApplication,
                    year = period.Year,
                    modeOfStudyIds = period.ModeOfStudies?.Select(m => m.ModeId).ToList() ?? new List<int>(),
                    programmeIds = period.Programms?.Select(p => p.Id).ToList() ?? new List<int>(),
                    applicantCount = period.Applicants?.Count ?? 0,
                    isActive = period.EndOfApplication >= DateTime.Now,
                    daysRemaining = (period.EndOfApplication - DateTime.Now).Days
                };

                return Json(periodDto);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error loading application period: {ex.Message}");
            }
        }

        // POST: ApplicationPeriod/CreateApplicationPeriod
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateApplicationPeriod(ApplicationPeriod period, List<int> selectedModes, List<int> selectedProgrammes)
        {
            try
            {
                // Validate dates
                if (period.StartOfApplication >= period.EndOfApplication)
                {
                    TempData["Error"] = "End date must be after start date";
                    return RedirectToAction(nameof(Index));
                }

                // Check for overlapping periods
                var overlapping = await _context.ApplicationPeriods
                    .AnyAsync(ap => ap.Year == period.Year &&
                        ((period.StartOfApplication >= ap.StartOfApplication && period.StartOfApplication <= ap.EndOfApplication) ||
                         (period.EndOfApplication >= ap.StartOfApplication && period.EndOfApplication <= ap.EndOfApplication) ||
                         (period.StartOfApplication <= ap.StartOfApplication && period.EndOfApplication >= ap.EndOfApplication)));

                if (overlapping)
                {
                    TempData["Error"] = "Application period overlaps with an existing period for this year";
                    return RedirectToAction(nameof(Index));
                }

                // Initialize collections
                period.ModeOfStudies = new List<ModeOfStudy>();
                period.Programms = new List<Programme>();
                period.Applicants = new List<Applicant>();

                // Add selected modes of study
                if (selectedModes != null && selectedModes.Any())
                {
                    var modes = await _context.ModesOfStudy
                        .Where(m => selectedModes.Contains(m.ModeId))
                        .ToListAsync();
                    period.ModeOfStudies = modes;
                }

                // Add selected programmes
                if (selectedProgrammes != null && selectedProgrammes.Any())
                {
                    var programmes = await _context.Programmes
                        .Where(p => selectedProgrammes.Contains(p.Id))
                        .ToListAsync();
                    period.Programms = programmes;
                }

                _context.ApplicationPeriods.Add(period);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Application period created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating application period: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: ApplicationPeriod/UpdateApplicationPeriod/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateApplicationPeriod(int id, ApplicationPeriod updatedPeriod, List<int> selectedModes, List<int> selectedProgrammes)
        {
            try
            {
                var period = await _context.ApplicationPeriods
                    .Include(ap => ap.ModeOfStudies)
                    .Include(ap => ap.Programms)
                    .FirstOrDefaultAsync(ap => ap.Id == id);

                if (period == null)
                {
                    TempData["Error"] = "Application period not found";
                    return RedirectToAction(nameof(Index));
                }

                // Validate dates
                if (updatedPeriod.StartOfApplication >= updatedPeriod.EndOfApplication)
                {
                    TempData["Error"] = "End date must be after start date";
                    return RedirectToAction(nameof(Index));
                }

                // Check for overlapping periods (excluding current period)
                var overlapping = await _context.ApplicationPeriods
                    .AnyAsync(ap => ap.Id != id && ap.Year == updatedPeriod.Year &&
                        ((updatedPeriod.StartOfApplication >= ap.StartOfApplication && updatedPeriod.StartOfApplication <= ap.EndOfApplication) ||
                         (updatedPeriod.EndOfApplication >= ap.StartOfApplication && updatedPeriod.EndOfApplication <= ap.EndOfApplication) ||
                         (updatedPeriod.StartOfApplication <= ap.StartOfApplication && updatedPeriod.EndOfApplication >= ap.EndOfApplication)));

                if (overlapping)
                {
                    TempData["Error"] = "Application period overlaps with an existing period for this year";
                    return RedirectToAction(nameof(Index));
                }

                // Update basic properties
                period.Name = updatedPeriod.Name;
                period.Description = updatedPeriod.Description;
                period.StartOfApplication = updatedPeriod.StartOfApplication;
                period.EndOfApplication = updatedPeriod.EndOfApplication;
                period.Year = updatedPeriod.Year;

                // Update modes of study
                period.ModeOfStudies.Clear();
                if (selectedModes != null && selectedModes.Any())
                {
                    var modes = await _context.ModesOfStudy
                        .Where(m => selectedModes.Contains(m.ModeId))
                        .ToListAsync();
                    foreach (var mode in modes)
                    {
                        period.ModeOfStudies.Add(mode);
                    }
                }

                // Update programmes
                period.Programms.Clear();
                if (selectedProgrammes != null && selectedProgrammes.Any())
                {
                    var programmes = await _context.Programmes
                        .Where(p => selectedProgrammes.Contains(p.Id))
                        .ToListAsync();
                    foreach (var programme in programmes)
                    {
                        period.Programms.Add(programme);
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Application period updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating application period: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: ApplicationPeriod/DeleteApplicationPeriod/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteApplicationPeriod(int id)
        {
            try
            {
                var period = await _context.ApplicationPeriods
                    .Include(ap => ap.Applicants)
                    .FirstOrDefaultAsync(ap => ap.Id == id);

                if (period == null)
                {
                    TempData["Error"] = "Application period not found";
                    return RedirectToAction(nameof(Index));
                }

                // Check if there are applicants
                if (period.Applicants != null && period.Applicants.Any())
                {
                    TempData["Error"] = "Cannot delete application period with existing applicants. Please archive instead.";
                    return RedirectToAction(nameof(Index));
                }

                _context.ApplicationPeriods.Remove(period);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Application period deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting application period: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: ApplicationPeriod/GetProgrammesByMode/5
        [HttpGet]
        [Route("ApplicationPeriod/GetProgrammesByMode/{modeId}")]
        public async Task<IActionResult> GetProgrammesByMode(int modeId)
        {
            try
            {
                var programmes = await _context.Programmes
                    .Where(p => p.ModeOfStudyId == modeId)
                    .Include(p => p.Department)
                    .Select(p => new
                    {
                        id = p.Id,
                        text = p.Name + " - " + p.Department.Name
                    })
                    .OrderBy(p => p.text)
                    .ToListAsync();

                return Json(programmes);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error loading programmes: {ex.Message}");
            }
        }

        // GET: ApplicationPeriod/GetStatistics/5
        [HttpGet]
        public async Task<IActionResult> GetStatistics(int id)
        {
            try
            {
                var period = await _context.ApplicationPeriods
                    .Include(ap => ap.Applicants)
                    .FirstOrDefaultAsync(ap => ap.Id == id);

                if (period == null)
                {
                    return NotFound("Application period not found");
                }

                var stats = new
                {
                    totalApplicants = period.Applicants?.Count ?? 0,
                    submittedApplications = period.Applicants?.Count(a => a.IsSubmitted) ?? 0,
                    pendingApplications = period.Applicants?.Count(a => !a.IsSubmitted) ?? 0,
                    qualifiedApplicants = period.Applicants?.Count(a => a.IsQualified == true) ?? 0,
                    paidApplicants = period.Applicants?.Count(a => a.PaymentStatus == Status.Active) ?? 0,
                    acceptedApplicants = period.Applicants?.Count(a => a.Status == Status.Active) ?? 0
                };

                return Json(stats);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error loading statistics: {ex.Message}");
            }
        }

        private async Task PopulateViewBag()
        {
            ViewBag.ModeOfStudies = await _context.ModesOfStudy
                .Select(m => new SelectListItem
                {
                    Value = m.ModeId.ToString(),
                    Text = m.ModeName + " (" + m.Code + ")"
                })
                .OrderBy(m => m.Text)
                .ToListAsync();

            ViewBag.Programmes = await _context.Programmes
                .Include(p => p.Department)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name + " - " + p.Department.Name
                })
                .OrderBy(p => p.Text)
                .ToListAsync();

            // Generate year options (current year + 5 years back and forward)
            var currentYear = DateTime.Now.Year;
            ViewBag.Years = Enumerable.Range(currentYear - 5, 11)
                .Select(y => new SelectListItem
                {
                    Value = y.ToString(),
                    Text = y.ToString()
                })
                .OrderByDescending(y => y.Value)
                .ToList();
        }
    }
}