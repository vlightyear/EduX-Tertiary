using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.ProgramCoordinator;
using SIS.Models.StudentApplication;
using System.Security.Claims;

namespace SIS.Controllers
{
    [Authorize(Roles = "ProgramCoordinator")]
    public class ProgramCoordinatorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public ProgramCoordinatorController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get programs coordinated by the current user
            var coordinatedPrograms = await _context.Programmes
                .Where(p => p.CoordinatorId == currentUserId)
                .Include(p => p.Department)
                    .ThenInclude(d => d.School)
                .ToListAsync();

            // Get count of total students in coordinated programs
            var programIds = coordinatedPrograms.Select(p => p.Id).ToList();
            var totalStudents = await _context.Students
                .Where(s => programIds.Contains(s.ProgrammeId))
                .CountAsync();

            // Get count of pending admissions
            var pendingAdmissions = await _context.Students
                .Where(s => programIds.Contains(s.ProgrammeId) && !s.IsAdmitted)
                .CountAsync();

            // Get count of lecturers teaching in these programs
            var courseIds = await _context.ProgrammeCourses
                .Where(pc => programIds.Contains(pc.ProgrammeId))
                .Select(pc => pc.CourseId)
                .ToListAsync();

            var lecturerIds = await _context.CourseLecturer
                .Where(cl => courseIds.Contains(cl.CourseId))
                .Select(cl => cl.LecturerId)
                .Distinct()
                .CountAsync();

            // Mock data for student enrollment over time
            var enrollmentTrends = new Dictionary<string, object>();
            foreach (var program in coordinatedPrograms)
            {
                enrollmentTrends[program.Name] = new
                {
                    years = new List<string> { "2020", "2021", "2022", "2023", "2024" },
                    enrollments = GenerateRandomEnrollments(5)
                };
            }

            // Mock data for gender distribution
            var genderDistribution = new
            {
                labels = new List<string> { "Male", "Female" },
                data = new List<int> { 65, 35 }
            };

            // Mock data for recent registrations
            var recentRegistrations = new List<object>
            {
                new { name = "John Smith", program = "Computer Science", date = DateTime.Now.AddDays(-2).ToString("MMM dd, yyyy"), status = "Completed" },
                new { name = "Mary Johnson", program = "Information Technology", date = DateTime.Now.AddDays(-3).ToString("MMM dd, yyyy"), status = "Completed" },
                new { name = "Robert Garcia", program = "Cybersecurity", date = DateTime.Now.AddDays(-5).ToString("MMM dd, yyyy"), status = "Pending" },
                new { name = "Sarah Martinez", program = "Software Engineering", date = DateTime.Now.AddDays(-7).ToString("MMM dd, yyyy"), status = "Completed" },
                new { name = "David Brown", program = "Computer Science", date = DateTime.Now.AddDays(-8).ToString("MMM dd, yyyy"), status = "Pending" }
            };

            // Pass all data to view
            ViewBag.CoordinatedPrograms = coordinatedPrograms;
            ViewBag.TotalStudents = totalStudents;
            ViewBag.PendingAdmissions = pendingAdmissions;
            ViewBag.LecturersCount = lecturerIds;
            ViewBag.EnrollmentTrends = JsonConvert.SerializeObject(enrollmentTrends);
            ViewBag.GenderDistribution = JsonConvert.SerializeObject(genderDistribution);
            ViewBag.RecentRegistrations = recentRegistrations;

            return View("CoordinatorDashboard");
        }



        // Helper method to generate random enrollment data
        private List<int> GenerateRandomEnrollments(int count)
        {
            var random = new Random();
            var enrollments = new List<int>();

            for (int i = 0; i < count; i++)
            {
                enrollments.Add(random.Next(15, 60));
            }

            return enrollments;
        }







        // GET: ProgramCoordinator/ProgramManagement
        public async Task<IActionResult> ProgramManagement()
        {
            // Get the current user
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get all programs where the current user is the coordinator
            var coordinatedPrograms = await _context.Programmes
                .Include(p => p.Department)
                    .ThenInclude(d => d.School)
                .Include(p => p.ModeOfStudy)
                .Include(p => p.ProgrammeLevel)
                .Where(p => p.CoordinatorId == currentUserId)
                .OrderBy(p => p.Name)
                .ToListAsync();

            // For each program, collect additional metrics
            var programViewModels = new List<ProgramViewModel>();

            foreach (var program in coordinatedPrograms)
            {
                // Get active student count for this program
                var activeStudentCount = await _context.Students
                    .Where(s => s.ProgrammeId == program.Id && s.IsAdmitted)
                    .CountAsync();

                // Get pending applications count
                var pendingApplicationsCount = await _context.Applicants
                    .Where(a => a.ProgrammeId == program.Id && a.Status == Status.Pending)
                    .CountAsync();

                // Get graduation rate (completed students vs total enrolled students over time)
                // This may require a more complex query depending on your data structure
                // This is a simplistic example assuming you have a way to identify graduated students
                var totalEnrolledCount = program.EnrollmentCount;

                // Add to view models list
                programViewModels.Add(new ProgramViewModel
                {
                    Programme = program,
                    ActiveStudentCount = activeStudentCount,
                    PendingApplicationsCount = pendingApplicationsCount,
                    EnrollmentCount = totalEnrolledCount
                });
            }

            return View(programViewModels);
        }

        // GET: ProgramCoordinator/ProgramDetails/{id}
        public async Task<IActionResult> ProgramDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get the program and validate the current user is the coordinator
            var program = await _context.Programmes
                .Include(p => p.Department)
                    .ThenInclude(d => d.School)
                .Include(p => p.ModeOfStudy)
                .Include(p => p.ProgrammeLevel)
                .Include(p => p.ProgrammeCourses)
                    .ThenInclude(pc => pc.Course)
                .FirstOrDefaultAsync(p => p.Id == id && p.CoordinatorId == currentUserId);

            if (program == null)
            {
                return NotFound();
            }

            // Get program metrics
            var metrics = await GetProgramMetrics(program.Id);

            // Get the course structure
            var courseStructure = await GetProgramCourseStructure(program.Id);

            // Prepare view model with program, metrics, and course structure
            var viewModel = new ProgramDetailsViewModel
            {
                Programme = program,
                Metrics = metrics,
                CourseStructure = courseStructure
            };

            return View(viewModel);
        }

        // Helper method to get program metrics
        private async Task<ProgramMetricsViewModel> GetProgramMetrics(int programId)
        {
            // Current enrollment counts by year
            var enrollmentByYear = await _context.Students
                .Where(s => s.ProgrammeId == programId && s.IsAdmitted)
                .GroupBy(s => s.StudentCurrentYear)
                .Select(g => new { Year = g.Key, Count = g.Count() })
                .ToListAsync();

            // Gender distribution
            var genderDistribution = await _context.Students
                .Where(s => s.ProgrammeId == programId && s.IsAdmitted)
                .GroupBy(s => s.Gender)
                .Select(g => new { Gender = g.Key, Count = g.Count() })
                .ToListAsync();

            // Enrollment trends over time (simplified - might need more detailed implementation)
            // This assumes you have intake data per year
            var currentYear = DateTime.Now.Year;
            var enrollmentTrends = await _context.Students
                .Where(s => s.ProgrammeId == programId)
                .GroupBy(s => s.AcademicYear.YearValue)
                .Select(g => new { Year = g.Key, Count = g.Count() })
                .OrderBy(x => x.Year)
                .ToListAsync();

            // Completion rates (simplified)
            // This would require historical data - here's a placeholder approach
            var completionRates = new List<object>
    {
        new { Year = "2021", Rate = 85.5 },
        new { Year = "2022", Rate = 87.2 },
        new { Year = "2023", Rate = 86.8 }
    };

            return new ProgramMetricsViewModel
            {
                EnrollmentByYear = enrollmentByYear.ToDictionary(e => e.Year ?? 0, e => e.Count),
                GenderDistribution = genderDistribution.ToDictionary(g => g.Gender ?? "Unknown", g => g.Count),
                EnrollmentTrends = enrollmentTrends.Select(e => new { Year = e.Year, Count = e.Count }).ToList(),
                CompletionRates = completionRates
            };
        }

        // Helper method to get program course structure
        private async Task<Dictionary<int, List<SIS.Models.ProgramCoordinator.CourseViewModel>>> GetProgramCourseStructure(int programId)
        {
            var programCourses = await _context.ProgrammeCourses
                .Include(pc => pc.Course)
                .Where(pc => pc.ProgrammeId == programId)
                .ToListAsync();

            // Group courses by year
            var courseStructure = programCourses
                .GroupBy(pc => pc.Course.YearTaken)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(pc => new SIS.Models.ProgramCoordinator.CourseViewModel
                    {
                        Course = pc.Course,
                        IsMandatory = pc.Course.IsMandatory,
                        Semester = pc.Course.PeriodTakenId
                    }).OrderBy(c => c.Semester).ThenBy(c => c.Course.CourseName).ToList()
                );

            return courseStructure;
        }

        // GET: ProgramCoordinator/EditProgram/{id}
        public async Task<IActionResult> EditProgram(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get the program and validate the current user is the coordinator
            var program = await _context.Programmes
                .FirstOrDefaultAsync(p => p.Id == id && p.CoordinatorId == currentUserId);

            if (program == null)
            {
                return NotFound();
            }

            // Load dropdown data
            ViewBag.ModesOfStudy = await _context.ModesOfStudy.ToListAsync();
            ViewBag.ProgrammeLevels = await _context.ProgramLevels.ToListAsync();

            // Parse and prepare yearly requirements for the form
            // This assumes YearlyRequirements is stored as a JSON string
            ViewBag.YearlyRequirements = program.YearlyRequirements;

            return View(program);
        }

        // POST: ProgramCoordinator/EditProgram/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProgram(int id, [Bind("Id,Name,Description,MinimumPointsTop5Subjects,DurationYears,ModeOfStudyId,ProgrammeLevelId,YearlyRequirements")] Programme programModel)
        {
            if (id != programModel.Id)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get the original program from database to ensure user is authorized
            var originalProgram = await _context.Programmes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.CoordinatorId == currentUserId);

            if (originalProgram == null)
            {
                return NotFound();
            }

            // Validate the YearlyRequirements is valid JSON
            try
            {
                if (!string.IsNullOrEmpty(programModel.YearlyRequirements))
                {
                    // Attempt to parse to validate JSON using Newtonsoft.Json
                    var jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(programModel.YearlyRequirements);
                }
            }
            catch (Newtonsoft.Json.JsonException)
            {
                ModelState.AddModelError("YearlyRequirements", "The yearly requirements must be valid JSON");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Update only the fields that are allowed to be edited
                    var programToUpdate = await _context.Programmes.FindAsync(id);

                    if (programToUpdate == null || programToUpdate.CoordinatorId != currentUserId)
                    {
                        return NotFound();
                    }

                    // Update editable fields
                    programToUpdate.Name = programModel.Name;
                    programToUpdate.Description = programModel.Description;
                    programToUpdate.MinimumPointsTop5Subjects = programModel.MinimumPointsTop5Subjects;
                    programToUpdate.DurationYears = programModel.DurationYears;
                    programToUpdate.ModeOfStudyId = programModel.ModeOfStudyId;
                    programToUpdate.ProgrammeLevelId = programModel.ProgrammeLevelId;
                    programToUpdate.YearlyRequirements = programModel.YearlyRequirements;

                    // Update audit fields
                    programToUpdate.UpdatedBy = currentUserId;
                    programToUpdate.UpdatedAt = DateTime.Now;

                    _context.Update(programToUpdate);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Program successfully updated.";
                    return RedirectToAction(nameof(ProgramDetails), new { id = programToUpdate.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProgrammeExists(programModel.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            ViewBag.ModesOfStudy = await _context.ModesOfStudy.ToListAsync();
            ViewBag.ProgrammeLevels = await _context.ProgramLevels.ToListAsync();
            ViewBag.YearlyRequirements = programModel.YearlyRequirements;

            return View(programModel);
        }

        // GET: ProgramCoordinator/ManageProgramCourses/{id}
        public async Task<IActionResult> ManageProgramCourses(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get the program and validate the current user is the coordinator
            var program = await _context.Programmes
                .Include(p => p.Department)
                .Include(p => p.ProgrammeCourses)
                    .ThenInclude(pc => pc.Course)
                .FirstOrDefaultAsync(p => p.Id == id && p.CoordinatorId == currentUserId);

            if (program == null)
            {
                return NotFound();
            }

            // Get all available courses in the department
            var departmentCourses = await _context.Courses
                .Where(c => c.ProgrammeID == program.Id || c.Programme.DepartmentId == program.DepartmentId)
                .ToListAsync();

            // Prepare view model
            var viewModel = new ManageProgramCoursesViewModel
            {
                Program = program,
                ProgramCourses = program.ProgrammeCourses.ToList(),
                AvailableCourses = departmentCourses,
                CourseStructure = GetProgramCourseStructure(program.Id).Result
            };

            return View(viewModel);
        }

        // POST: ProgramCoordinator/AddCoursesToProgram
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCoursesToProgram(int programId, int[] selectedCourses)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get the program and validate the current user is the coordinator
            var program = await _context.Programmes
                .FirstOrDefaultAsync(p => p.Id == programId && p.CoordinatorId == currentUserId);

            if (program == null)
            {
                return NotFound();
            }

            if (selectedCourses != null && selectedCourses.Any())
            {
                // Get existing program courses to avoid duplicates
                var existingProgramCourses = await _context.ProgrammeCourses
                    .Where(pc => pc.ProgrammeId == programId)
                    .Select(pc => pc.CourseId)
                    .ToListAsync();

                // Add only new courses
                foreach (var courseId in selectedCourses)
                {
                    if (!existingProgramCourses.Contains(courseId))
                    {
                        var programCourse = new ProgrammeCourse
                        {
                            ProgrammeId = programId,
                            CourseId = courseId,
                            //CreatedBy = currentUserId,
                            //CreatedAt = DateTime.Now
                        };

                        _context.ProgrammeCourses.Add(programCourse);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Courses successfully added to the program.";
            }

            return RedirectToAction(nameof(ManageProgramCourses), new { id = programId });
        }

        // POST: ProgramCoordinator/RemoveCourseFromProgram
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCourseFromProgram(int programId, int courseId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Get the program and validate the current user is the coordinator
            var program = await _context.Programmes
                .FirstOrDefaultAsync(p => p.Id == programId && p.CoordinatorId == currentUserId);

            if (program == null)
            {
                return NotFound();
            }

            // Find the program course entry
            var programCourse = await _context.ProgrammeCourses
                .FirstOrDefaultAsync(pc => pc.ProgrammeId == programId && pc.CourseId == courseId);

            if (programCourse != null)
            {
                _context.ProgrammeCourses.Remove(programCourse);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Course successfully removed from the program.";
            }

            return RedirectToAction(nameof(ManageProgramCourses), new { id = programId });
        }

        private bool ProgrammeExists(int id)
        {
            return _context.Programmes.Any(e => e.Id == id);
        }














    }
}