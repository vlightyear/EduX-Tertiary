using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using SIS.Services.Accounting;
using SIS.Services.Registration;
using SIS.Services;
using System.Text;
using SIS.Services.PDF;

namespace SIS.Controllers
{
    public class CourseRegistrationController : Controller
    {
        private readonly ICourseRegistrationService _courseRegistrationService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStudentInvoiceService _studentInvoiceService;
        private readonly ILogger<CourseRegistrationController> _logger;
        private readonly ExamDocketService _examDocketService;
        private readonly IInstitutionConfigService _institutionConfig;
        private readonly IPdfInvoiceService _pdfInvoiceService;

        [ActivatorUtilitiesConstructor]
        public CourseRegistrationController(ICourseRegistrationService courseRegistrationService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<CourseRegistrationController> logger,
            IStudentInvoiceService studentInvoiceService,
            ExamDocketService examDocketService,
            IInstitutionConfigService institutionConfig,
            IPdfInvoiceService pdfInvoiceService) // Add this line
        {
            _courseRegistrationService = courseRegistrationService;
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _studentInvoiceService = studentInvoiceService;
            _examDocketService = examDocketService;
            _institutionConfig = institutionConfig;
            _pdfInvoiceService = pdfInvoiceService; // Add this line
        }

        public async Task<IActionResult> RegisterCourses()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    Console.WriteLine($"[WARNING] {DateTime.Now} - User not found during course registration attempt");
                    return RedirectToAction("Login", "Account");
                }

                // Get student information based on username
                var student = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);


                if (student == null)
                {
                    Console.WriteLine($"[WARNING] {DateTime.Now} - Student record not found for user: {user.UserName}");
                    return NotFound("Student record not found.");
                }

                List<CourseRegistrationViewModel> courses;
                Programme programmeUsedForCourses = student.Programme; // Track which programme provided the courses

                // Check if student is registered
                if (student.RegistrationStatus == Status.Registered || student.RegistrationStatus == Status.Pending)
                {
                    // Get already registered courses using AcademicYearId
                    var baseQuery = _context.StudentCourseRegistrations
                        .Where(r => r.StudentId == student.Id &&
                                   r.AcademicYearId == student.AcademicYearId);

                    // Add semester filter if programme is semester-based
                    if (student.Programme?.IsSemesterBased == true)
                    {
                        baseQuery = baseQuery.Where(r => r.YearPeriodId == student.CurrentYearPeriodId);
                    }

                    courses = await baseQuery
                        .Join(_context.Courses,
                            scr => scr.CourseId,
                            c => c.Id,
                            (scr, c) => new CourseRegistrationViewModel
                            {
                                Id = c.Id,
                                CourseCode = c.CourseCode,
                                CourseName = c.CourseName,
                                CourseDescription = c.CourseDescription,
                                IsMandatory = c.IsMandatory,
                                IsSelected = true
                            })
                        .ToListAsync();

                    // If no registered courses found but status is Registered/Pending, check for available courses
                    if (!courses.Any())
                    {
                        Console.WriteLine($"[WARNING] {DateTime.Now} - No registered courses found for student with status {student.RegistrationStatus}. Fetching available courses.");

                        // Fall back to getting available courses as if student was not registered
                        var availableCoursesResult = await GetAvailableCourses(student);
                        courses = availableCoursesResult.courses;
                        programmeUsedForCourses = availableCoursesResult.programmeUsed ?? student.Programme;
                    }
                }
                else
                {
                    // Get all available courses for student's programme and year
                    var availableCoursesResult = await GetAvailableCourses(student);
                    courses = availableCoursesResult.courses;
                    programmeUsedForCourses = availableCoursesResult.programmeUsed ?? student.Programme;
                }

                if (!courses.Any())
                {
                    var semesterInfo = student.Programme?.IsSemesterBased == true ? $" semester {student.CurrentYearPeriodId}" : "";
                    var message = $"No courses found for student {student.Id} in year {student.StudentCurrentYear}{semesterInfo}";
                    Console.WriteLine($"[WARNING] {DateTime.Now} - {message}");
                    TempData["Warning"] = $"No courses found for your current year{semesterInfo}.";
                }
                else
                {
                    var mandatoryCount = courses.Count(c => c.IsMandatory);
                    var electiveCount = courses.Count(c => !c.IsMandatory);
                    var selectedCount = courses.Count(c => c.IsSelected);
                    var semesterInfo = student.Programme?.IsSemesterBased == true ? $" for semester {student.CurrentYearPeriodId}" : "";
                    var programmeInfo = programmeUsedForCourses.Id != student.ProgrammeId ? $" from NQ programme '{programmeUsedForCourses.Name}'" : "";
                    Console.WriteLine($"[INFO] {DateTime.Now} - Found {mandatoryCount} mandatory and {electiveCount} elective courses{semesterInfo}{programmeInfo}. {selectedCount} courses are pre-selected.");
                }

                // Get programme requirements (semester-based or yearly) - USE THE PROGRAMME THAT PROVIDED THE COURSES
                var requirements = await GetProgrammeRequirements(student, programmeUsedForCourses);

                // Add academic year information to ViewBag
                ViewBag.RegistrationStatus = student.RegistrationStatus;
                ViewBag.AcademicYear = student.AcademicYear?.YearValue;
                ViewBag.Semester = student.CurrentYearPeriodId;
                ViewBag.IsSemesterBased = student.Programme?.IsSemesterBased ?? false;
                @ViewBag.StudentNumber = student.StudentId_Number;

                // Add requirements to ViewBag
                ViewBag.MinimumElectives = requirements.MinimumElectives;
                ViewBag.MaximumElectives = requirements.MaximumElectives;
                ViewBag.TotalRequiredCourses = requirements.TotalRequiredCourses;
                ViewBag.MandatoryCourseCount = courses.Count(c => c.IsMandatory);

                // Add student details to ViewBag
                ViewBag.StudentName = $"{student.FullName}";
                ViewBag.StudentId = student.Id;
                ViewBag.ProgrammeName = student.Programme?.Name;
                ViewBag.CurrentYear = student.StudentCurrentYear;
                ViewBag.CanRegister = false;

                decimal totalPaid = StudentTools.GetStudentTotalPaid(student.Id);
                decimal totalFees = StudentTools.GetStudentTotalFees(student.Id);

                if(totalFees != 0)
                {
                    decimal paidpercent = (totalPaid/totalFees) * 100;

                    if(paidpercent >= student.AcademicYear.MinRegistrationPaymentPercentage)
                    {
                        ViewBag.CanRegister = true;
                    }
                    else
                    {
                        ViewBag.CanRegister = false;
                    }
                }


                Console.WriteLine($"[INFO] {DateTime.Now} - Registration Status: {ViewBag.RegistrationStatus}, " +
                                 $"Academic Year: {ViewBag.AcademicYear}, Semester: {ViewBag.Semester}, " +
                                 $"IsSemesterBased: {ViewBag.IsSemesterBased}, " +
                                 $"MinimumElectives: {ViewBag.MinimumElectives}, MaximumElectives: {ViewBag.MaximumElectives}, " +
                                 $"TotalRequiredCourses: {ViewBag.TotalRequiredCourses}");

                // Add notification for students about course limits
                if (requirements.TotalRequiredCourses > 0)
                {
                    var periodText = student.Programme?.IsSemesterBased == true ?
                        $"this semester (Semester {student.CurrentYearPeriodId})" : "this year";
                    var mandatoryText = courses.Count(c => c.IsMandatory) > 0 ?
                        $"(including {courses.Count(c => c.IsMandatory)} mandatory courses and " : "(";

                    //TempData["Info"] = $"For your programme and year, you must select exactly {requirements.TotalRequiredCourses} courses for {periodText} " +
                    //                   $"{mandatoryText}between {requirements.MinimumElectives} and {requirements.MaximumElectives} electives).";
                }

                return View("~/Views/Registration/RegisterCourses.cshtml", courses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {DateTime.Now} - Error in course registration for user: {User?.Identity?.Name}");
                Console.WriteLine($"[ERROR] {DateTime.Now} - Exception: {ex.Message}");
                Console.WriteLine($"[ERROR] {DateTime.Now} - Stack Trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[ERROR] {DateTime.Now} - Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"[ERROR] {DateTime.Now} - Inner Stack Trace: {ex.InnerException.StackTrace}");
                }

                return RedirectToAction("Error", "Home");
            }
        }

        private async Task<(List<CourseRegistrationViewModel> courses, Programme programmeUsed)> GetAvailableCourses(Student student)
        {
            // First get carryover courses
            var carryoverCourses = await GetCarryoverCourses(student);
            var carryoverCourseIds = carryoverCourses.Select(c => c.Id).ToList();

            // Try to get regular courses from student's programme
            var regularCourses = await GetRegularProgrammeCourses(student, carryoverCourseIds);
            var programmeUsed = student.Programme;

            // If no regular courses found, try NQ programme fallback
            if (!regularCourses.Any() && student.Programme?.AssociatedNQProgrammeId.HasValue == true)
            {
                Console.WriteLine($"[INFO] {DateTime.Now} - No courses found for student {student.Id} in programme {student.Programme.Name}. Checking associated NQ programme.");

                var nqCourses = await GetNQProgrammeCourses(student, carryoverCourseIds);
                if (nqCourses.courses.Any())
                {
                    regularCourses = nqCourses.courses;
                    programmeUsed = nqCourses.nqProgramme;

                    var semesterInfo = student.Programme?.IsSemesterBased == true ? $" semester {student.CurrentYearPeriodId}" : "";
                    Console.WriteLine($"[INFO] {DateTime.Now} - Found {regularCourses.Count} courses from NQ programme '{programmeUsed.Name}' for student {student.Id} in year {student.StudentCurrentYear}{semesterInfo}.");
                }
                else
                {
                    var semesterInfo = student.Programme?.IsSemesterBased == true ? $" semester {student.CurrentYearPeriodId}" : "";
                    Console.WriteLine($"[WARNING] {DateTime.Now} - No courses found in associated NQ programme for student {student.Id} in year {student.StudentCurrentYear}{semesterInfo}.");
                }
            }

            // Combine carryover courses with regular courses
            var allCourses = new List<CourseRegistrationViewModel>();
            allCourses.AddRange(carryoverCourses);
            allCourses.AddRange(regularCourses);

            return (allCourses, programmeUsed);
        }

        private async Task<List<CourseRegistrationViewModel>> GetRegularProgrammeCourses(Student student, List<int> carryoverCourseIds)
        {
            var courseQuery = _context.Courses
                .Where(c => c.ProgrammeID == student.ProgrammeId &&
                           c.YearTaken == student.StudentCurrentYear &&
                           !carryoverCourseIds.Contains(c.Id)); // Exclude carryover courses

            // Add semester filter if programme is semester-based
            if (student.Programme?.IsSemesterBased == true)
            {
                courseQuery = courseQuery.Where(c => c.PeriodTakenId == student.CurrentYearPeriod.AcademicPeriod.Id);
            }

            var courses = await courseQuery
                .Select(c => new CourseRegistrationViewModel
                {
                    Id = c.Id,
                    CourseCode = c.CourseCode,
                    CourseName = c.CourseName,
                    CourseDescription = c.CourseDescription,
                    IsMandatory = c.IsMandatory,
                    IsSelected = c.IsMandatory || student.RegistrationStatus == Status.Pending,
                    IsCarryover = false
                })
                .ToListAsync();

            return courses;
        }

        private async Task<(List<CourseRegistrationViewModel> courses, Programme nqProgramme)> GetNQProgrammeCourses(Student student, List<int> carryoverCourseIds)
        {
            // Get the associated NQ programme
            var nqProgramme = await _context.Programmes
                .FirstOrDefaultAsync(p => p.Id == student.Programme.AssociatedNQProgrammeId.Value);

            if (nqProgramme == null)
            {
                Console.WriteLine($"[WARNING] {DateTime.Now} - Associated NQ programme not found for programme {student.Programme.Name}");
                return (new List<CourseRegistrationViewModel>(), null);
            }

            // Query courses from the NQ programme
            var courseQuery = _context.Courses
                .Where(c => c.ProgrammeID == nqProgramme.Id &&
                           c.YearTaken == student.StudentCurrentYear &&
                           !carryoverCourseIds.Contains(c.Id)); // Exclude carryover courses

            // Add semester filter if the ORIGINAL programme is semester-based
            // (we use the student's current semester regardless of NQ programme settings)
            if (student.Programme?.IsSemesterBased == true)
            {
                courseQuery = courseQuery.Where(c => c.PeriodTakenId == student.CurrentYearPeriod.AcademicPeriod.Id);
            }

            var courses = await courseQuery
                .Select(c => new CourseRegistrationViewModel
                {
                    Id = c.Id,
                    CourseCode = c.CourseCode,
                    CourseName = c.CourseName,
                    CourseDescription = $"{c.CourseDescription} (From NQ: {nqProgramme.Name})",
                    IsMandatory = c.IsMandatory,
                    IsSelected = c.IsMandatory || student.RegistrationStatus == Status.Pending,
                    IsCarryover = false
                })
                .ToListAsync();

            return (courses, nqProgramme);
        }

        private async Task<CourseRequirements> GetProgrammeRequirements(Student student, Programme programmeToUse = null)
        {
            // Use the specified programme or default to student's programme
            var programme = programmeToUse ?? student.Programme;

            if (programme == null)
            {
                Console.WriteLine($"[ERROR] {DateTime.Now} - No programme available for requirements calculation");
                return new CourseRequirements { MinimumElectives = 0, MaximumElectives = 0, TotalRequiredCourses = 0 };
            }

            // Get carryover courses count
            var carryoverCoursesCount = await _context.StudentCarryoverCourses
                .Where(scc => scc.StudentId == student.Id && scc.IsActive)
                .CountAsync();

            int totalRequiredCourses = 0;
            int minimumElectives = 0;
            int maximumElectives = 0;

            if (!string.IsNullOrEmpty(programme.YearlyRequirements))
            {
                try
                {
                    // Parse the yearly requirements JSON
                    var yearlyRequirements = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, YearRequirement>>(
                        programme.YearlyRequirements);

                    string yearKey = $"Year{student.StudentCurrentYear}";

                    if (yearlyRequirements != null && yearlyRequirements.ContainsKey(yearKey))
                    {
                        var requirement = yearlyRequirements[yearKey];

                        if (programme.IsSemesterBased)
                        {
                            // For semester-based programmes, get semester-specific requirements
                            var CurrentYearPeriodId = student.CurrentYearPeriodId ?? 1;
                            if (CurrentYearPeriodId == 1 && requirement.Semester1.HasValue)
                            {
                                totalRequiredCourses = requirement.Semester1.Value;
                            }
                            else if (CurrentYearPeriodId == 2 && requirement.Semester2.HasValue)
                            {
                                totalRequiredCourses = requirement.Semester2.Value;
                            }
                            else
                            {
                                Console.WriteLine($"[WARNING] {DateTime.Now} - No semester {CurrentYearPeriodId} requirements found for year {student.StudentCurrentYear} in programme {programme.Name}");
                                // Fallback to total required divided by 2
                                totalRequiredCourses = requirement.TotalRequired / 2;
                            }
                        }
                        else
                        {
                            // For yearly programmes, use total required
                            totalRequiredCourses = requirement.TotalRequired;
                        }

                        // Adjust for carryover courses
                        if (carryoverCoursesCount > 0)
                        {
                            // If student has carryover courses, they must register for those plus regular courses
                            // The total limit is expanded to accommodate carryovers
                            totalRequiredCourses += carryoverCoursesCount;

                            Console.WriteLine($"[INFO] Student has {carryoverCoursesCount} carryover courses. " +
                                             $"Total course limit adjusted to {totalRequiredCourses}");
                        }

                        // Calculate electives based on mandatory courses available (excluding carryovers)
                        // NOTE: We need to use the PROGRAMME that provided the courses for this calculation
                        var mandatoryCoursesQuery = _context.Courses
                            .Where(c => c.ProgrammeID == programme.Id && // Use the programme that provided courses
                                       c.YearTaken == student.StudentCurrentYear &&
                                       c.IsMandatory);

                        // Exclude carryover courses from the mandatory course count
                        var carryoverCourseIds = await _context.StudentCarryoverCourses
                            .Where(scc => scc.StudentId == student.Id && scc.IsActive)
                            .Select(scc => scc.CourseId)
                            .ToListAsync();

                        mandatoryCoursesQuery = mandatoryCoursesQuery.Where(c => !carryoverCourseIds.Contains(c.Id));

                        if (programme.IsSemesterBased)
                        {
                            mandatoryCoursesQuery = mandatoryCoursesQuery.Where(c => c.PeriodTakenId == student.CurrentYearPeriod.AcademicPeriod.Id);
                        }

                        var mandatoryCount = await mandatoryCoursesQuery.CountAsync();
                        var totalMandatory = mandatoryCount + carryoverCoursesCount; // Carryovers are treated as mandatory

                        minimumElectives = Math.Max(0, totalRequiredCourses - totalMandatory);

                        // Calculate maximum electives
                        var electiveCoursesQuery = _context.Courses
                            .Where(c => c.ProgrammeID == programme.Id && // Use the programme that provided courses
                                       c.YearTaken == student.StudentCurrentYear &&
                                       !c.IsMandatory &&
                                       !carryoverCourseIds.Contains(c.Id));

                        if (programme.IsSemesterBased)
                        {
                            electiveCoursesQuery = electiveCoursesQuery.Where(c => c.PeriodTakenId == student.CurrentYearPeriod.AcademicPeriod.Id);
                        }

                        var availableElectives = await electiveCoursesQuery.CountAsync();
                        maximumElectives = totalRequiredCourses > 0
                            ? Math.Min(availableElectives, totalRequiredCourses - totalMandatory)
                            : availableElectives;

                        var periodText = programme.IsSemesterBased ? $"Semester {student.CurrentYearPeriodId}" : $"Year {student.StudentCurrentYear}";
                        var programmeText = programme.Id != student.ProgrammeId ? $" (using NQ programme: {programme.Name})" : "";
                        Console.WriteLine($"[INFO] {DateTime.Now} - {periodText} requires {totalRequiredCourses} total courses, " +
                                         $"minimum {minimumElectives} electives, maximum {maximumElectives} electives{programmeText}");
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] {DateTime.Now} - No requirements found for {yearKey} in programme {programme.Name}");
                        // Fallback to default values
                        minimumElectives = Math.Max(1, 1 - carryoverCoursesCount);
                        maximumElectives = await _context.Courses
                            .Where(c => c.ProgrammeID == programme.Id &&
                                       c.YearTaken == student.StudentCurrentYear &&
                                       !c.IsMandatory)
                            .CountAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {DateTime.Now} - Error parsing yearly requirements for programme {programme.Name}: {ex.Message}");
                    Console.WriteLine($"[ERROR] YearlyRequirements JSON: {programme.YearlyRequirements}");

                    // Fallback logic with carryover consideration
                    minimumElectives = Math.Max(1, 1 - carryoverCoursesCount);
                    maximumElectives = await _context.Courses
                        .Where(c => c.ProgrammeID == programme.Id &&
                                   c.YearTaken == student.StudentCurrentYear &&
                                   !c.IsMandatory)
                        .CountAsync();
                }
            }
            else
            {
                Console.WriteLine($"[WARNING] {DateTime.Now} - No yearly requirements defined for programme {programme.Name}");
                // Default fallback
                minimumElectives = Math.Max(1, 1 - carryoverCoursesCount);
                maximumElectives = await _context.Courses
                    .Where(c => c.ProgrammeID == programme.Id &&
                               c.YearTaken == student.StudentCurrentYear &&
                               !c.IsMandatory)
                    .CountAsync();
            }

            return new CourseRequirements
            {
                TotalRequiredCourses = totalRequiredCourses,
                MinimumElectives = minimumElectives,
                MaximumElectives = maximumElectives,
                CarryoverCoursesCount = carryoverCoursesCount
            };
        }


        // Update the SubmitRegistration method in CourseRegistrationController

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRegistration([FromBody] List<CourseRegistrationViewModel> courses)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var student = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student record not found." });
                }

                if (student.AcademicYear == null)
                {
                    return Json(new { success = false, message = "Student's academic year is not set." });
                }

                // Get programme requirements (accounting for carryover courses)
                var requirements = await GetProgrammeRequirements(student);

                // Separate carryover and regular courses
                var carryoverCourses = courses.Where(c => c.IsCarryover).ToList();
                var regularCourses = courses.Where(c => !c.IsCarryover).ToList();
                var selectedRegularCourses = regularCourses.Where(c => c.IsSelected || c.IsMandatory).ToList();
                var selectedElectives = selectedRegularCourses.Where(c => !c.IsMandatory).ToList();

                // Validation (carryover courses don't count against limits)
                if (selectedElectives.Count < requirements.MinimumElectives)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"You must select at least {requirements.MinimumElectives} elective course{(requirements.MinimumElectives > 1 ? "s" : "")}."
                    });
                }

                var mandatoryCourses = selectedRegularCourses.Where(c => c.IsMandatory).ToList();
                int totalSelected = mandatoryCourses.Count + selectedElectives.Count + carryoverCourses.Count;

                if (requirements.TotalRequiredCourses > 0 && totalSelected > requirements.TotalRequiredCourses)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"You can only select a maximum of {requirements.TotalRequiredCourses} courses in total. You have selected {totalSelected}."
                    });
                }

                // Combine all selected courses (carryover + regular)
                var allSelectedCourses = new List<CourseRegistrationViewModel>();
                allSelectedCourses.AddRange(carryoverCourses); // All carryover courses are mandatory
                allSelectedCourses.AddRange(selectedRegularCourses);

                var selectedCourseIds = allSelectedCourses.Select(c => c.Id).ToList();

                // Get full course details from database
                var dbCoursesQuery = _context.Courses
                    .Include(c => c.CourseAssessments)
                        .ThenInclude(ca => ca.Assessment)
                    .Where(c => selectedCourseIds.Contains(c.Id));

                var dbCourses = await dbCoursesQuery.ToListAsync();

                // Create registrations for all selected courses
                foreach (var course in dbCourses)
                {
                    // Check if this is a carryover course
                    var isCarryoverCourse = carryoverCourses.Any(cc => cc.Id == course.Id);

                    // Create registration record
                    var registration = new StudentCourseRegistration
                    {
                        StudentId = student.Id,
                        CourseId = course.Id,
                        AcademicYearId = student.AcademicYearId,
                        YearPeriodId = student.CurrentYearPeriodId ?? 1,
                        RegistrationDate = DateTime.Now
                    };
                    _context.StudentCourseRegistrations.Add(registration);

                    // For examinable courses, create examinable course record
                    if (course.IsExaminable)
                    {
                        var assessmentJson = new Dictionary<int, object>();

                        // If course has assessments, create proper assessment structure
                        if (course.CourseAssessments != null && course.CourseAssessments.Any())
                        {
                            foreach (var assessment in course.CourseAssessments)
                            {
                                if (assessment?.Assessment != null)
                                {
                                    assessmentJson[assessment.AssessmentId] = new
                                    {
                                        assessment_name = assessment.Assessment.Name,
                                        score = "-" // Initial score set to '-'
                                    };
                                }
                            }
                        }

                        var examinableCourse = new StudentExaminableCourse
                        {
                            StudentId = student.Id,
                            CourseId = course.Id,
                            AcademicYearId = student.AcademicYearId,
                            YearPeriodId = student.CurrentYearPeriodId ?? 1,
                            RegistrationDate = DateTime.Now,
                            AssessmentScores = assessmentJson.Any()
                                ? System.Text.Json.JsonSerializer.Serialize(assessmentJson)
                                : "{}", // Empty JSON for courses without assessments
                            Status = Status.Unpublished
                        };
                        _context.StudentExaminableCourses.Add(examinableCourse);

                        var assessmentInfo = assessmentJson.Any()
                            ? $"with {assessmentJson.Count} assessments"
                            : "with no assessments (empty JSON)";
                        Console.WriteLine($"[INFO] {DateTime.Now} - Created examinable course record for {course.CourseCode} {assessmentInfo}");
                    }

                    // If this is a carryover course, mark the carryover as inactive (completed)
                    if (isCarryoverCourse)
                    {
                        var carryoverRecord = await _context.StudentCarryoverCourses
                            .FirstOrDefaultAsync(scc => scc.StudentId == student.Id &&
                                                       scc.CourseId == course.Id &&
                                                       scc.IsActive);

                        if (carryoverRecord != null)
                        {
                            carryoverRecord.IsActive = false;
                            carryoverRecord.Notes += $" | Reregistered on {DateTime.Now:yyyy-MM-dd}";
                        }
                    }
                }

                // Update student registration status
                student.RegistrationStatus = Status.Registered;
                student.IsRegistered = true;
                student.RegistrationDate = DateTime.Now;

                // Save all changes
                await _context.SaveChangesAsync();

                // Generate invoice with proper error handling
                var periodText = student.Programme?.IsSemesterBased == true ?
                    $" for semester {student.CurrentYearPeriodId}" : "";

                var carryoverText = carryoverCourses.Any() ?
                    $" (including {carryoverCourses.Count} carryover course{(carryoverCourses.Count != 1 ? "s" : "")})" : "";

                try
                {
                    var invoiceResult = await _studentInvoiceService.GenerateStudentInvoiceAsync(student.Id);

                    if (!invoiceResult.Success)
                    {
                        // Log the error
                        Console.WriteLine($"[WARNING] {DateTime.Now} - Invoice generation failed for student {student.StudentId_Number}: {invoiceResult.Message}");

                        // Return success for registration but with invoice warning
                        return Json(new
                        {
                            success = true,
                            message = $"Registration submitted successfully{periodText}{carryoverText}. However, invoice could not be generated: {invoiceResult.Message}. Please contact the finance office.",
                            warning = true,
                            invoiceWarning = invoiceResult.Message,
                            redirect = Url.Action("RegisterCourses", "CourseRegistration")
                        });
                    }
                    else
                    {
                        Console.WriteLine($"[SUCCESS] {DateTime.Now} - Successfully generated invoice for student {student.StudentId_Number}");
                    }
                }
                catch (Exception invoiceEx)
                {
                    Console.WriteLine($"[ERROR] {DateTime.Now} - Exception generating invoice for student {student.StudentId_Number}: {invoiceEx.Message}");
                    Console.WriteLine($"[ERROR] Stack trace: {invoiceEx.StackTrace}");

                    // Return success for registration but with generic invoice error
                    return Json(new
                    {
                        success = true,
                        message = $"Registration submitted successfully{periodText}{carryoverText}. However, there was an error generating your invoice. Please contact the finance office.",
                        warning = true,
                        invoiceWarning = "Invoice generation error",
                        redirect = Url.Action("RegisterCourses", "CourseRegistration")
                    });
                }

                return Json(new
                {
                    success = true,
                    message = $"Registration submitted successfully{periodText}{carryoverText}.",
                    redirect = Url.Action("RegisterCourses", "CourseRegistration")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {DateTime.Now} - Error in course registration: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[ERROR] Inner exception: {ex.InnerException.Message}");
                }

                return Json(new
                {
                    success = false,
                    message = "An error occurred during registration. Please try again or contact support if the problem persists."
                });
            }
        }


        private async Task<List<CourseRegistrationViewModel>> GetCarryoverCourses(Student student)
        {
            var carryoverCourses = await _context.StudentCarryoverCourses
                .Where(scc => scc.StudentId == student.Id && scc.IsActive)
                .Include(scc => scc.Course)
                .Select(scc => new CourseRegistrationViewModel
                {
                    Id = scc.Course.Id,
                    CourseCode = scc.Course.CourseCode,
                    CourseName = scc.Course.CourseName,
                    CourseDescription = $"{scc.Course.CourseDescription} (Carryover from {scc.OriginalAcademicYear.YearValue})",
                    IsMandatory = true, // Carryover courses are always mandatory
                    IsSelected = true,
                    IsCarryover = true, // Add this property to the ViewModel
                    CarryoverReason = scc.Reason
                })
                .ToListAsync();

            return carryoverCourses;
        }

        // Exam Docket
        [HttpGet]
        public async Task<IActionResult> GetExamDocketStatus()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var student = await _context.Students
                    .Include(s => s.School)
                    .Include(s => s.Programme)
                    .FirstOrDefaultAsync(s => s.Email == user.Email);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student profile not found" });
                }

                var examEvents = await _examDocketService.GetUpcomingExamEvents(student.Id);

                return Json(new
                {
                    success = true,
                    exams = examEvents,
                    studentId = student.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading exam information" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerateExamDocket(int examEventId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var student = await _context.Students
                    .Include(s => s.School)
                    .Include(s => s.Programme)
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.ProgrammeLevel)
                    .FirstOrDefaultAsync(s => s.Email == user.Email);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student profile not found" });
                }

                // Check eligibility
                var isEligible = await _examDocketService.CheckExamEligibility(student.Id, examEventId);
                if (!isEligible)
                {
                    return Json(new { success = false, message = "You are not eligible to generate this exam docket" });
                }

                // Get exam event details
                var examEvent = await _context.AcademicCalendarEvents
                    .Include(e => e.EventType)
                    .Include(e => e.AcademicYear)
                    .FirstOrDefaultAsync(e => e.Id == examEventId);

                if (examEvent == null)
                {
                    return Json(new { success = false, message = "Exam event not found" });
                }

                // Get registered courses
                var registeredCourses = await _context.StudentCourseRegistrations
                    .Include(cr => cr.Course)
                    .Where(cr => cr.StudentId == student.Id)
                    .Where(cr => cr.AcademicYearId == student.AcademicYearId)
                    .Where(cr => cr.YearPeriodId == examEvent.Semester || examEvent.Semester == null)
                    .Select(cr => new {
                        Code = cr.Course.CourseCode,
                        Name = cr.Course.CourseName,
                        Credits = "3" // Default credits, adjust as needed
                    })
                    .ToListAsync<dynamic>();

                // Generate PDF using the service
                var pdfBytes = await _pdfInvoiceService.GenerateExamDocketPdfAsync(student, examEvent, registeredCourses);

                var fileName = $"ExamDocket_{examEvent.EventType?.Name?.Replace(" ", "") ?? "Exam"}_{DateTime.Now:yyyyMMdd}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating exam docket for examEventId: {ExamEventId}", examEventId);
                return Json(new { success = false, message = "Error generating exam docket" });
            }
        }

        private object PrepareExamDocketData(Student student, AcademicCalendarEvent examEvent, dynamic courses)
        {
            var institutionName = _institutionConfig?.GetInstitutionName() ?? "Institution Name";
            var logoPath = _institutionConfig?.GetLogoPath() ?? "";

            return new
            {
                institution = new
                {
                    name = institutionName,
                    logo = logoPath
                },
                student = new
                {
                    fullName = student.FullName ?? "Unknown",
                    studentId = student.StudentId_Number ?? "N/A",
                    nrcPassport = student.NrcOrPassportNumber ?? "N/A",
                    programme = student.Programme?.Name ?? "N/A",
                    school = student.School?.Name ?? "N/A",
                    modeOfStudy = student.ModeOfStudy?.ModeName ?? "N/A",
                    level = student.ProgrammeLevel?.Name ?? "N/A",
                    currentYear = student.StudentCurrentYear ?? 1,
                    photo = student.PassportPhotoPath
                },
                exam = new
                {
                    type = examEvent.EventType?.Name ?? "Examination",
                    title = examEvent.Title ?? "Examination",
                    startDate = examEvent.StartDateTime,
                    endDate = examEvent.EndDateTime,
                    location = examEvent.Location,
                    semester = examEvent.Semester ?? 1,
                    academicYear = examEvent.AcademicYear?.YearValue ?? DateTime.Now.Year.ToString()
                },
                courses = courses,
                generatedAt = DateTime.Now
            };
        }
    }
}