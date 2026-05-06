using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.TimeTabling;
using System.Security.Claims;
using System.Text.Json;

namespace SIS.Controllers
{
    [Authorize]
    public class TimetableController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TimetableController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Roles = "HOD,Dean,Admin")]
        public async Task<IActionResult> TimetableManagement()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<int> departmentIds = new List<int>();

            // Check if user is Dean
            var schoolAsDean = await _context.Schools
                .Include(s => s.Departments)
                .FirstOrDefaultAsync(s => s.DeanId == userId || s.AssistantDeanId == userId);

            if (schoolAsDean != null)
            {
                // If user is Dean or Assistant Dean, get all departments in their school
                departmentIds = schoolAsDean.Departments.Select(d => d.Id).ToList();
            }
            else
            {
                // Check if user is HOD
                var department = await _context.Departments
                    .FirstOrDefaultAsync(d => d.HODId == userId);

                if (department == null)
                {
                    return NotFound("You don't have any departments or schools assigned.");
                }

                // If user is HOD, only include their department
                departmentIds.Add(department.Id);
            }

            // Get timetables for the departments' programmes
            var timetables = await _context.Timetables
                .Include(t => t.Course)
                    .ThenInclude(c => c.Programme)
                        .ThenInclude(p => p.Department)
                .Include(t => t.LearningRoom)
                .Include(t => t.AcademicYear)
                .Include(t => t.ModeOfStudy)
                .Where(t => departmentIds.Contains(t.Course.Programme.DepartmentId))
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            // Group timetables by academic year and mode of study
            var timetableGroups = timetables
                .GroupBy(t => new { t.AcademicYearId, t.ModeOfStudyId })
                .Select(g => new TimetableGroupViewModel
                {
                    AcademicYearId = g.Key.AcademicYearId,
                    AcademicYearValue = g.First().AcademicYear.YearValue,
                    ModeOfStudyId = g.Key.ModeOfStudyId,
                    ModeOfStudyName = g.First().ModeOfStudy.ModeName,
                    TimetableCount = g.Count(),
                    DraftCount = g.Count(t => t.Status == "Draft"),
                    PublishedCount = g.Count(t => t.Status == "Published"),
                    HasCurrentSemesterTimetables = g.Any(t => t.Date >= DateTime.Now.AddMonths(-4)),
                    LatestUpdate = g.Max(t => t.UpdatedAt ?? t.CreatedAt)
                })
                .OrderByDescending(g => g.LatestUpdate)
                .ToList();

            // Add info about user's role for the view
            ViewBag.UserRole = schoolAsDean != null ? "Dean" : "HOD";
            ViewBag.EntityName = schoolAsDean != null ? schoolAsDean.Name : (await _context.Departments.FirstOrDefaultAsync(d => d.HODId == userId))?.Name;

            // Get statistics for the view
            ViewBag.Statistics = new
            {
                TotalTimetables = timetables.Count,
                DraftTimetables = timetables.Count(t => t.Status == "Draft"),
                PublishedTimetables = timetables.Count(t => t.Status == "Published"),
                CurrentSemesterTimetables = timetables.Count(t => t.Date >= DateTime.Now.AddMonths(-4)),
                TotalGroups = timetableGroups.Count,
                DepartmentCount = departmentIds.Count
            };

            return View(timetableGroups);
        }

        public class RoomBookingKey
        {
            public int RoomId { get; set; }
            public DayOfWeek DayOfWeek { get; set; }
            public int TimeSlotConfigId { get; set; }
            public int PeriodNumber { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is RoomBookingKey other)
                {
                    return RoomId == other.RoomId &&
                           DayOfWeek == other.DayOfWeek &&
                           TimeSlotConfigId == other.TimeSlotConfigId &&
                           PeriodNumber == other.PeriodNumber;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(RoomId, DayOfWeek, TimeSlotConfigId, PeriodNumber);
            }
        }


        private async Task<Dictionary<string, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>>> LoadAllInstructorSchedules()
        {
            // Create a dictionary to store instructor schedules
            var instructorSchedules = new Dictionary<string, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>>();

            // Load all instructor schedules from database
            var scheduleTrackings = await _context.ScheduleTrackings
                .Where(s => s.EntityType == "Instructor")
                .Select(s => new { s.EntityId, s.Date, s.TimeSlotConfigId, s.PeriodNumber })
                .ToListAsync();

            // Organize into patterns by instructor
            foreach (var entry in scheduleTrackings)
            {
                if (!instructorSchedules.ContainsKey(entry.EntityId))
                {
                    instructorSchedules[entry.EntityId] = new HashSet<(DayOfWeek, int, int)>();
                }

                // Add this time slot pattern to the instructor's schedule
                instructorSchedules[entry.EntityId].Add((entry.Date.DayOfWeek, entry.TimeSlotConfigId, entry.PeriodNumber));
            }

            return instructorSchedules;
        }



        [Authorize(Roles = "HOD,Dean")]
        public async Task<IActionResult> Generate()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<Programme> programmes = new List<Programme>();

            // Check if user is Dean or Assistant Dean
            var schoolAsDean = await _context.Schools
                .Include(s => s.Departments)
                    .ThenInclude(d => d.Programmes)
                .FirstOrDefaultAsync(s => s.DeanId == userId || s.AssistantDeanId == userId);

            if (schoolAsDean != null)
            {
                // If user is Dean, get all programmes from all departments in the school
                foreach (var dept in schoolAsDean.Departments)
                {
                    if (dept.Programmes != null)
                    {
                        programmes.AddRange(dept.Programmes);
                    }
                }
                ViewBag.UserRole = "Dean";
                ViewBag.EntityName = schoolAsDean.Name;
            }
            else
            {
                // If user is HOD, get programmes from their department
                var department = await _context.Departments
                    .Include(d => d.Programmes)
                    .FirstOrDefaultAsync(d => d.HODId == userId);

                if (department == null)
                {
                    return NotFound("You don't have any departments or schools assigned.");
                }

                if (department.Programmes != null)
                {
                    programmes = department.Programmes.ToList();
                }
                ViewBag.UserRole = "HOD";
                ViewBag.EntityName = department.Name;
            }

            // Get active academic years
            var academicYears = await _context.AcademicYears
                .Where(ay => ay.IsActive)
                .ToListAsync();

            // Get modes of study
            var modesOfStudy = await _context.ModesOfStudy.ToListAsync();

            // Prepare view data
            ViewBag.AcademicYears = academicYears;
            ViewBag.ModesOfStudy = modesOfStudy;
            ViewBag.Programmes = programmes;

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "HOD,Dean")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(GenerateTimetableViewModel model)
        {
            // Always populate ViewBag to avoid errors when returning the view
            await PopulateViewBagForGenerateView();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Get virtual room ID
                int virtualRoomId = await GetVirtualRoomIdAsync();

                // Begin a database transaction to ensure atomic updates
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    // Create a set to keep track of all room bookings across the entire process
                    var bookedRooms = new HashSet<RoomBookingKey>();

                    // Load all existing instructor schedules centrally
                    var allInstructorSchedules = new Dictionary<string, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>>();

                    // NEW: Load instructor schedules from database
                    var instructorSchedules = await _context.ScheduleTrackings
                        .Where(s => s.EntityType == "Instructor")
                        .Select(s => new { s.EntityId, s.Date, s.TimeSlotConfigId, s.PeriodNumber })
                        .ToListAsync();

                    // Organize into patterns by instructor
                    foreach (var entry in instructorSchedules)
                    {
                        if (!allInstructorSchedules.ContainsKey(entry.EntityId))
                        {
                            allInstructorSchedules[entry.EntityId] = new HashSet<(DayOfWeek, int, int)>();
                        }

                        // Add this time slot pattern to the instructor's schedule
                        allInstructorSchedules[entry.EntityId].Add((entry.Date.DayOfWeek, entry.TimeSlotConfigId, entry.PeriodNumber));
                    }

                    // Part 1: Data Collection
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    List<int> departmentIds = new List<int>();

                    // Check if user is Dean or Assistant Dean
                    var schoolAsDean = await _context.Schools
                        .Include(s => s.Departments)
                        .FirstOrDefaultAsync(s => s.DeanId == userId || s.AssistantDeanId == userId);

                    if (schoolAsDean != null)
                    {
                        // If user is Dean, collect all department IDs in their school
                        departmentIds = schoolAsDean.Departments.Select(d => d.Id).ToList();
                    }
                    else
                    {
                        // If user is HOD, just use their department ID
                        var department = await _context.Departments
                            .FirstOrDefaultAsync(d => d.HODId == userId);

                        if (department == null)
                        {
                            ModelState.AddModelError("", "You don't have any departments or schools assigned.");
                            return View(model);
                        }

                        departmentIds.Add(department.Id);
                    }

                    // Get working days configuration
                    var workingDayConfig = await _context.WorkingDayConfigurations
                        .FirstOrDefaultAsync(w => w.AcademicYearId == model.AcademicYearId
                            && w.ModeOfStudyId == model.ModeOfStudyId && w.IsActive);

                    if (workingDayConfig == null)
                    {
                        ModelState.AddModelError("", "No working days configuration found for the selected academic year and mode of study.");
                        return View(model);
                    }

                    // Parse working days data
                    var workingDaysData = System.Text.Json.JsonSerializer.Deserialize<List<WorkingDayData>>(
                        workingDayConfig.WorkingDaysData,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (workingDaysData == null || !workingDaysData.Any(d => d.IsWorkingDay))
                    {
                        ModelState.AddModelError("", "No working days found in the configuration.");
                        return View(model);
                    }

                    // Get time slot configurations
                    var timeSlotConfigs = await _context.TimeSlotConfigurations
                        .Where(t => t.IsActive)
                        .ToListAsync();

                    if (!timeSlotConfigs.Any())
                    {
                        ModelState.AddModelError("", "No active time slot configurations found.");
                        return View(model);
                    }

                    // Load all rooms
                    var allRooms = await _context.LearningRooms
                        .Where(r => r.IsActive)
                        // NEW: Don't sort by capacity here - let our scheduling methods handle optimal room selection
                        .ToListAsync();

                    if (!allRooms.Any())
                    {
                        ModelState.AddModelError("", "No active learning rooms found.");
                        return View(model);
                    }

                    // Get all relevant courses grouped by instructor
                    var allCourses = await _context.Courses
                        .Include(c => c.Programme) // Ensure Programme is loaded
                        .Where(c => departmentIds.Contains(c.Programme.DepartmentId) &&
                                 c.Programme.ModeOfStudyId == model.ModeOfStudyId)
                        .OrderByDescending(c => c.MeetingFrequencyPerWeek) // Schedule courses with more sessions first
                        .ThenByDescending(c => c.CapacityRequired)
                        .ToListAsync();

                    if (!allCourses.Any())
                    {
                        ModelState.AddModelError("", "No courses found for the selected departments and mode of study.");
                        return View(model);
                    }

                    // Now group by instructor, but maintain the priority order
                    var coursesByInstructor = allCourses
                        .GroupBy(c => c.InstructorId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    // Create a dictionary to track student group (year) weekly schedules
                    var globalStudentGroupWeeklySchedule = new Dictionary<int, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>>();

                    // Get the academic year details
                    var academicYear = await _context.AcademicYears.FindAsync(model.AcademicYearId);
                    DateTime startDate = academicYear?.StartDate ?? DateTime.Now.Date;
                    DateTime endDate = academicYear?.EndDate ?? startDate.AddMonths(4);

                    var allTimetableEntries = new List<Timetable>();
                    var allScheduleTrackings = new List<ScheduleTracking>();

                    // NEW APPROACH: Three-phase scheduling with increased physical room prioritization
                    Console.WriteLine("Beginning improved timetable generation process");

                    // PHASE 1: First attempt to schedule ALL sessions with physical rooms
                    Console.WriteLine("PHASE 1: Attempting to schedule all sessions with physical rooms");

                    foreach (var instructorId in coursesByInstructor.Keys)
                    {
                        var instructorCourses = coursesByInstructor[instructorId];

                        foreach (var course in instructorCourses)
                        {
                            int sessionsRequired = course.MeetingFrequencyPerWeek;
                            Console.WriteLine($"Attempting to schedule {sessionsRequired} sessions for course {course.Id} - {course.CourseName}");

                            // Try to schedule all sessions with physical rooms
                            var result = await ScheduleCourseSessionsWithLimitAsync(
                                course,
                                allRooms,
                                workingDaysData,
                                timeSlotConfigs,
                                globalStudentGroupWeeklySchedule,
                                allInstructorSchedules,
                                model.AcademicYearId,
                                model.ModeOfStudyId,
                                startDate,
                                endDate,
                                bookedRooms,
                                virtualRoomId,
                                sessionsRequired  // Try to schedule ALL sessions
                            );

                            // Add scheduled sessions to our lists
                            allTimetableEntries.AddRange(result.TimetableEntries);
                            allScheduleTrackings.AddRange(result.ScheduleTrackings);

                            // Update instructor schedules
                            foreach (var tracking in result.ScheduleTrackings)
                            {
                                if (tracking.EntityType == "Instructor")
                                {
                                    if (!allInstructorSchedules.ContainsKey(tracking.EntityId))
                                    {
                                        allInstructorSchedules[tracking.EntityId] = new HashSet<(DayOfWeek, int, int)>();
                                    }

                                    allInstructorSchedules[tracking.EntityId].Add(
                                        (tracking.Date.DayOfWeek, tracking.TimeSlotConfigId, tracking.PeriodNumber));
                                }
                            }
                        }
                    }

                    // PHASE 2: Check for courses with missing sessions and try one more time with ALL rooms
                    Console.WriteLine("PHASE 2: Second attempt for courses with missing sessions");

                    // Group existing entries by course to find those with missing sessions
                    var scheduledSessionsByCourse = allTimetableEntries
                        .GroupBy(t => t.CourseId)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Find courses with missing sessions
                    var coursesWithMissingSessions = allCourses
                        .Where(c => !scheduledSessionsByCourse.ContainsKey(c.Id) ||
                                    scheduledSessionsByCourse[c.Id] < c.MeetingFrequencyPerWeek)
                        .ToList();

                    Console.WriteLine($"Found {coursesWithMissingSessions.Count} courses with missing sessions");

                    foreach (var course in coursesWithMissingSessions)
                    {
                        int scheduledCount = scheduledSessionsByCourse.ContainsKey(course.Id) ?
                            scheduledSessionsByCourse[course.Id] : 0;

                        int sessionsRemaining = course.MeetingFrequencyPerWeek - scheduledCount;

                        if (sessionsRemaining <= 0)
                            continue;

                        Console.WriteLine($"Second attempt: Scheduling {sessionsRemaining} sessions for course {course.Id} - {course.CourseName}");

                        // One more attempt with all rooms
                        var result = await ScheduleCourseSessionsWithLimitAsync(
                            course,
                            allRooms,
                            workingDaysData,
                            timeSlotConfigs,
                            globalStudentGroupWeeklySchedule,
                            allInstructorSchedules,
                            model.AcademicYearId,
                            model.ModeOfStudyId,
                            startDate,
                            endDate,
                            bookedRooms,
                            virtualRoomId,
                            sessionsRemaining
                        );

                        // Add scheduled sessions
                        allTimetableEntries.AddRange(result.TimetableEntries);
                        allScheduleTrackings.AddRange(result.ScheduleTrackings);

                        // Update instructor schedules
                        foreach (var tracking in result.ScheduleTrackings)
                        {
                            if (tracking.EntityType == "Instructor")
                            {
                                if (!allInstructorSchedules.ContainsKey(tracking.EntityId))
                                {
                                    allInstructorSchedules[tracking.EntityId] = new HashSet<(DayOfWeek, int, int)>();
                                }

                                allInstructorSchedules[tracking.EntityId].Add(
                                    (tracking.Date.DayOfWeek, tracking.TimeSlotConfigId, tracking.PeriodNumber));
                            }
                        }

                        // Update scheduled count for this course
                        if (scheduledSessionsByCourse.ContainsKey(course.Id))
                        {
                            scheduledSessionsByCourse[course.Id] += result.TimetableEntries.Count;
                        }
                        else
                        {
                            scheduledSessionsByCourse[course.Id] = result.TimetableEntries.Count;
                        }
                    }

                    // PHASE 3: Last resort - virtual sessions for any remaining unscheduled sessions
                    Console.WriteLine("PHASE 3: Final virtual sessions for remaining unscheduled sessions");

                    // Recalculate courses with missing sessions
                    scheduledSessionsByCourse = allTimetableEntries
                        .GroupBy(t => t.CourseId)
                        .ToDictionary(g => g.Key, g => g.Count());

                    coursesWithMissingSessions = allCourses
                        .Where(c => !scheduledSessionsByCourse.ContainsKey(c.Id) ||
                                    scheduledSessionsByCourse[c.Id] < c.MeetingFrequencyPerWeek)
                        .ToList();

                    Console.WriteLine($"Found {coursesWithMissingSessions.Count} courses still needing virtual sessions");

                    foreach (var course in coursesWithMissingSessions)
                    {
                        int scheduledCount = scheduledSessionsByCourse.ContainsKey(course.Id) ?
                            scheduledSessionsByCourse[course.Id] : 0;

                        int sessionsRemaining = course.MeetingFrequencyPerWeek - scheduledCount;

                        if (sessionsRemaining <= 0)
                            continue;

                        Console.WriteLine($"Creating {sessionsRemaining} virtual sessions for course {course.Id} - {course.CourseName}");

                        // Force virtual sessions as last resort
                        var result = await ForceScheduleVirtualSessionAsync(
                            course,
                            workingDaysData,
                            timeSlotConfigs,
                            globalStudentGroupWeeklySchedule,
                            allInstructorSchedules,
                            model.AcademicYearId,
                            model.ModeOfStudyId,
                            startDate,
                            endDate,
                            virtualRoomId,
                            sessionsRemaining
                        );

                        allTimetableEntries.AddRange(result.TimetableEntries);
                        allScheduleTrackings.AddRange(result.ScheduleTrackings);

                        // Update instructor schedules
                        foreach (var tracking in result.ScheduleTrackings)
                        {
                            if (tracking.EntityType == "Instructor")
                            {
                                if (!allInstructorSchedules.ContainsKey(tracking.EntityId))
                                {
                                    allInstructorSchedules[tracking.EntityId] = new HashSet<(DayOfWeek, int, int)>();
                                }

                                allInstructorSchedules[tracking.EntityId].Add(
                                    (tracking.Date.DayOfWeek, tracking.TimeSlotConfigId, tracking.PeriodNumber));
                            }
                        }
                    }

                    // Final check to ensure all courses have required sessions
                    var finalSessionCounts = allTimetableEntries
                        .GroupBy(t => t.CourseId)
                        .ToDictionary(g => g.Key, g => g.Count());

                    var coursesMissingFinalSessions = allCourses
                        .Where(c => !finalSessionCounts.ContainsKey(c.Id) ||
                               finalSessionCounts[c.Id] < c.MeetingFrequencyPerWeek)
                        .ToList();

                    if (coursesMissingFinalSessions.Any())
                    {
                        // Emergency scheduling for any remaining courses
                        foreach (var course in coursesMissingFinalSessions)
                        {
                            int scheduledCount = finalSessionCounts.ContainsKey(course.Id) ?
                                finalSessionCounts[course.Id] : 0;

                            int sessionsRemaining = course.MeetingFrequencyPerWeek - scheduledCount;

                            Console.WriteLine($"EMERGENCY: Forcing {sessionsRemaining} sessions for course {course.Id}");

                            var result = await ForceScheduleVirtualSessionAsync(
                                course,
                                workingDaysData,
                                timeSlotConfigs,
                                globalStudentGroupWeeklySchedule,
                                allInstructorSchedules,
                                model.AcademicYearId,
                                model.ModeOfStudyId,
                                startDate,
                                endDate,
                                virtualRoomId,
                                sessionsRemaining,
                                true  // Force scheduling even if conflicts occur
                            );

                            allTimetableEntries.AddRange(result.TimetableEntries);
                            allScheduleTrackings.AddRange(result.ScheduleTrackings);
                        }
                    }

                    // Generate statistics for physical vs. virtual sessions
                    int physicalCount = allTimetableEntries.Count(t => t.LearningRoomId != virtualRoomId);
                    int virtualCount = allTimetableEntries.Count(t => t.LearningRoomId == virtualRoomId);
                    double physicalPercentage = (double)physicalCount / allTimetableEntries.Count * 100;

                    Console.WriteLine($"Statistics: {physicalCount} physical sessions ({physicalPercentage:F1}%), {virtualCount} virtual sessions");

                    // Validate generated timetable (just for logging purposes)
                    var conflicts = ValidateGeneratedTimetable(allTimetableEntries);
                    if (conflicts.Any())
                    {
                        Console.WriteLine($"NOTE: Some conflicts were detected but will be saved: {string.Join(", ", conflicts)}");
                    }

                    // Save to database in batches
                    const int batchSize = 50;

                    // Save timetable entries
                    for (int i = 0; i < allTimetableEntries.Count; i += batchSize)
                    {
                        var batch = allTimetableEntries.Skip(i).Take(batchSize);
                        _context.Timetables.AddRange(batch);
                        await _context.SaveChangesAsync();
                    }

                    // Filter out duplicate schedule tracking entries
                    var existingSchedules = await _context.ScheduleTrackings
                        .Select(s => new
                        {
                            s.EntityId,
                            s.EntityType,
                            Date = s.Date.Date,
                            s.PeriodNumber,
                            s.TimeSlotConfigId
                        })
                        .ToListAsync();

                    var scheduleCombinations = new HashSet<string>(
                        existingSchedules.Select(s => $"{s.EntityId}|{s.EntityType}|{s.Date}|{s.TimeSlotConfigId}|{s.PeriodNumber}")
                    );

                    var filteredScheduleTrackings = new List<ScheduleTracking>();
                    foreach (var tracking in allScheduleTrackings)
                    {
                        string key = $"{tracking.EntityId}|{tracking.EntityType}|{tracking.Date.Date}|{tracking.TimeSlotConfigId}|{tracking.PeriodNumber}";
                        if (!scheduleCombinations.Contains(key))
                        {
                            filteredScheduleTrackings.Add(tracking);
                            scheduleCombinations.Add(key);
                        }
                    }

                    // Save schedule tracking entries
                    for (int i = 0; i < filteredScheduleTrackings.Count; i += batchSize)
                    {
                        var batch = filteredScheduleTrackings.Skip(i).Take(batchSize);
                        _context.ScheduleTrackings.AddRange(batch);
                        await _context.SaveChangesAsync();
                    }

                    // Commit transaction
                    await transaction.CommitAsync();

                    TempData["Message"] = $"Successfully generated timetables with {allTimetableEntries.Count} sessions ({physicalCount} physical, {virtualCount} virtual) for {coursesByInstructor.Count} instructors.";
                    return RedirectToAction(nameof(TimetableManagement));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error > " + ex);
                TempData["Error"] = "Error generating timetable: " + ex.Message;
                return View(model);
            }
        }

        // New method to force schedule virtual sessions for a course regardless of conflicts
        private async Task<TimetableGenerationResult> ForceScheduleVirtualSessionAsync(
             Course course,
             List<WorkingDayData> workingDaysData,
             List<TimeSlotConfiguration> timeSlotConfigs,
             Dictionary<int, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>> globalStudentGroupWeeklySchedule,
             Dictionary<string, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>> allInstructorSchedules,
             int academicYearId,
             int modeOfStudyId,
             DateTime startDate,
             DateTime endDate,
             int virtualRoomId,
             int sessionsNeeded,
             bool ignoreConflicts = false)
        {
            var result = new TimetableGenerationResult();

            if (sessionsNeeded <= 0)
                return result;

            // Initialize student group if not exists
            if (!globalStudentGroupWeeklySchedule.ContainsKey(course.YearTaken))
            {
                globalStudentGroupWeeklySchedule[course.YearTaken] = new HashSet<(DayOfWeek, int, int)>();
            }

            // Get instructor's schedule
            var instructorWeeklySchedule = new HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>();
            if (allInstructorSchedules.ContainsKey(course.InstructorId))
            {
                instructorWeeklySchedule = allInstructorSchedules[course.InstructorId];
            }
            else
            {
                allInstructorSchedules[course.InstructorId] = instructorWeeklySchedule;
            }

            // Get working days
            var workingDays = workingDaysData
                .Where(d => d.IsWorkingDay)
                .Select(d => new
                {
                    DayOfWeek = Enum.Parse<DayOfWeek>(d.Day, true),
                    TimeSlotConfigId = int.Parse(d.TimeSlotConfigId)
                })
                .ToList();

        //    if (!workingDays.Any())
        //    {
        //        // No working days configured, use reasonable defaults
        //        Console.WriteLine("Warning: No working days configured, using default days");
        //        var defaultConfigId = timeSlotConfigs.FirstOrDefault()?.Id ?? 1;
        //        workingDays = new List<dynamic>
        //{
        //    new { DayOfWeek = DayOfWeek.Monday, TimeSlotConfigId = defaultConfigId },
        //    new { DayOfWeek = DayOfWeek.Tuesday, TimeSlotConfigId = defaultConfigId },
        //    new { DayOfWeek = DayOfWeek.Wednesday, TimeSlotConfigId = defaultConfigId },
        //    new { DayOfWeek = DayOfWeek.Thursday, TimeSlotConfigId = defaultConfigId },
        //    new { DayOfWeek = DayOfWeek.Friday, TimeSlotConfigId = defaultConfigId }
        //};
        //    }

            // NEW: Log that we're using virtual sessions as a last resort
            Console.WriteLine($"LAST RESORT: Scheduling {sessionsNeeded} virtual sessions for course {course.Id} - {course.CourseName}");

            // Distribute sessions across days, even with conflicts
            int scheduledSessions = 0;
            int dayIndex = 0;

            while (scheduledSessions < sessionsNeeded && dayIndex < workingDays.Count * 2) // Try each day twice if needed
            {
                // Get the working day, with wraparound
                var workingDay = workingDays[dayIndex % workingDays.Count];

                var timeSlotConfig = timeSlotConfigs.FirstOrDefault(t => t.Id == workingDay.TimeSlotConfigId);
                if (timeSlotConfig == null)
                {
                    dayIndex++;
                    continue;
                }

                var periodsData = System.Text.Json.JsonSerializer.Deserialize<List<PeriodData>>(
                    timeSlotConfig.PeriodsData,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (periodsData == null || !periodsData.Any())
                {
                    dayIndex++;
                    continue;
                }

                // Get regular periods (exclude breaks)
                var regularPeriods = periodsData
                    .Where(p => p.Type.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p.PeriodNumber)
                    .ToList();

                if (!regularPeriods.Any())
                {
                    dayIndex++;
                    continue;
                }

                // Try each period in this day until we find one that works, or force it anyway
                bool periodFound = false;
                foreach (var period in regularPeriods)
                {
                    // Check conflicts for instructor and student group
                    var dayPeriodKey = (workingDay.DayOfWeek, timeSlotConfig.Id, period.PeriodNumber);

                    // Skip periods with conflicts unless we're ignoring conflicts
                    if (!ignoreConflicts &&
                        (instructorWeeklySchedule.Contains(dayPeriodKey) ||
                         globalStudentGroupWeeklySchedule[course.YearTaken].Contains(dayPeriodKey)))
                    {
                        continue;
                    }

                    // Get the first occurrence of this day of the week from the start date
                    DateTime firstSessionDate = GetFirstOccurrenceOfDay(startDate, workingDay.DayOfWeek);

                    // Create virtual session
                    var virtualSession = new Timetable
                    {
                        CourseId = course.Id,
                        LearningRoomId = virtualRoomId,
                        TimeSlotConfigId = timeSlotConfig.Id,
                        Date = firstSessionDate,
                        AcademicYearId = academicYearId,
                        ModeOfStudyId = modeOfStudyId,
                        PeriodNumber = period.PeriodNumber,
                        SpecialInstructions = ignoreConflicts ?
                            "FORCED Virtual Meeting - May have conflicts" :
                            "Virtual Meeting - No physical room available",
                        Status = "Draft",
                        IsRecurring = true,
                        RecurrenceEndDate = endDate,
                        CreatedBy = course.CreatedBy,
                        CreatedAt = DateTime.Now
                    };

                    result.TimetableEntries.Add(virtualSession);

                    // Add tracking records
                    var instructorTracking = new ScheduleTracking
                    {
                        EntityId = course.InstructorId,
                        EntityType = "Instructor",
                        TimeSlotConfigId = timeSlotConfig.Id,
                        Date = firstSessionDate,
                        IsOccupied = true,
                        OccupiedByCourseId = course.Id,
                        PeriodNumber = period.PeriodNumber,
                        CreatedBy = course.CreatedBy,
                        CreatedAt = DateTime.Now
                    };

                    result.ScheduleTrackings.Add(instructorTracking);

                    // Add tracking for virtual room
                    var roomTracking = new ScheduleTracking
                    {
                        EntityId = virtualRoomId.ToString(),
                        EntityType = "LearningRoom",
                        TimeSlotConfigId = timeSlotConfig.Id,
                        Date = firstSessionDate,
                        IsOccupied = true,
                        OccupiedByCourseId = course.Id,
                        PeriodNumber = period.PeriodNumber,
                        CreatedBy = course.CreatedBy,
                        CreatedAt = DateTime.Now
                    };

                    result.ScheduleTrackings.Add(roomTracking);

                    // Update pattern tracking
                    instructorWeeklySchedule.Add(dayPeriodKey);
                    globalStudentGroupWeeklySchedule[course.YearTaken].Add(dayPeriodKey);

                    scheduledSessions++;
                    periodFound = true;

                    if (scheduledSessions >= sessionsNeeded)
                    {
                        break;
                    }

                    // Try a different day for the next session
                    break;
                }

                // If we couldn't find a period in this day, or need to move to next day
                if (!periodFound || scheduledSessions < sessionsNeeded)
                {
                    dayIndex++;

                    // If we've tried all days and still need to schedule sessions,
                    // enable conflict ignoring for last resort scheduling
                    if (dayIndex >= workingDays.Count && !ignoreConflicts)
                    {
                        ignoreConflicts = true;
                        Console.WriteLine($"WARNING: Enabling conflict ignoring for course {course.Id} - absolute last resort");
                    }
                }
            }

            // If we still haven't scheduled all needed sessions, force them on the same day
            // This is the last resort to ensure every course gets its required sessions
            if (scheduledSessions < sessionsNeeded)
            {
                // Use first working day and period as default
                var lastResortDay = workingDays.First();
                var timeSlotConfig = timeSlotConfigs.First();

                var periodsData = System.Text.Json.JsonSerializer.Deserialize<List<PeriodData>>(
                    timeSlotConfig.PeriodsData,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (periodsData != null && periodsData.Any())
                {
                    // Get first regular period
                    var firstPeriod = periodsData
                        .Where(p => p.Type.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.PeriodNumber)
                        .FirstOrDefault();

                    if (firstPeriod != null)
                    {
                        DateTime firstSessionDate = GetFirstOccurrenceOfDay(startDate, lastResortDay.DayOfWeek);

                        for (int i = scheduledSessions; i < sessionsNeeded; i++)
                        {
                            // Create absolutely forced virtual session
                            var virtualSession = new Timetable
                            {
                                CourseId = course.Id,
                                LearningRoomId = virtualRoomId,
                                TimeSlotConfigId = timeSlotConfig.Id,
                                Date = firstSessionDate,
                                AcademicYearId = academicYearId,
                                ModeOfStudyId = modeOfStudyId,
                                PeriodNumber = firstPeriod.PeriodNumber,
                                SpecialInstructions = "EMERGENCY FORCED Virtual Meeting - Has conflicts, requires rescheduling",
                                Status = "Draft",
                                IsRecurring = true,
                                RecurrenceEndDate = endDate,
                                CreatedBy = course.CreatedBy,
                                CreatedAt = DateTime.Now
                            };

                            result.TimetableEntries.Add(virtualSession);

                            // Add minimal tracking records
                            var instructorTracking = new ScheduleTracking
                            {
                                EntityId = course.InstructorId,
                                EntityType = "Instructor",
                                TimeSlotConfigId = timeSlotConfig.Id,
                                Date = firstSessionDate,
                                IsOccupied = true,
                                OccupiedByCourseId = course.Id,
                                PeriodNumber = firstPeriod.PeriodNumber,
                                CreatedBy = course.CreatedBy,
                                CreatedAt = DateTime.Now
                            };

                            result.ScheduleTrackings.Add(instructorTracking);

                            Console.WriteLine($"EMERGENCY FORCED session created for course {course.Id} - {course.CourseName}");
                        }
                    }
                }
            }

            return result;
        }


        private async Task<TimetableGenerationResult> ScheduleCourseSessionsWithLimitAsync(
     Course course,
     List<LearningRoom> availableRooms,
     List<WorkingDayData> workingDaysData,
     List<TimeSlotConfiguration> timeSlotConfigs,
     Dictionary<int, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>> globalStudentGroupWeeklySchedule,
     Dictionary<string, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>> allInstructorSchedules,
     int academicYearId,
     int modeOfStudyId,
     DateTime startDate,
     DateTime endDate,
     HashSet<RoomBookingKey> bookedRooms,
     int virtualRoomId,
     int sessionLimit)
        {
            var result = new TimetableGenerationResult();

            // If no sessions requested, return empty result
            if (sessionLimit <= 0)
                return result;

            // Initialize student group if not exists
            if (!globalStudentGroupWeeklySchedule.ContainsKey(course.YearTaken))
            {
                globalStudentGroupWeeklySchedule[course.YearTaken] = new HashSet<(DayOfWeek, int, int)>();
            }

            // Get instructor's schedule
            var instructorWeeklySchedule = new HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>();
            if (allInstructorSchedules.ContainsKey(course.InstructorId))
            {
                instructorWeeklySchedule = allInstructorSchedules[course.InstructorId];
            }
            else
            {
                allInstructorSchedules[course.InstructorId] = instructorWeeklySchedule;
            }

            // Refresh from database to ensure we have the latest
            var additionalInstructorSchedules = await _context.ScheduleTrackings
                .Where(s => s.EntityId == course.InstructorId && s.EntityType == "Instructor")
                .Select(s => new { s.Date, s.TimeSlotConfigId, s.PeriodNumber })
                .ToListAsync();

            foreach (var schedule in additionalInstructorSchedules)
            {
                instructorWeeklySchedule.Add((schedule.Date.DayOfWeek, schedule.TimeSlotConfigId, schedule.PeriodNumber));
            }

            // Parse preferred venues from JSON
            List<int> preferredVenueIds = new List<int>();
            if (!string.IsNullOrEmpty(course.PreferredVenueIds))
            {
                try
                {
                    preferredVenueIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(
                        course.PreferredVenueIds,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch
                {
                    Console.WriteLine($"Error parsing preferred venues for course {course.Id}");
                }
            }

            // Get all rooms that meet capacity requirements
            var allSuitableRooms = availableRooms
                .Where(r => r.LearningCapacity >= course.CapacityRequired)
                .ToList();

            // Check if there are any suitable rooms at all
            if (!allSuitableRooms.Any())
            {
                Console.WriteLine($"No rooms found with capacity >= {course.CapacityRequired} for course {course.Id}");
                return result; // No rooms available, can't schedule
            }

            // Organize rooms in order of priority:
            // 1. Preferred rooms ordered by optimal capacity (closest to required)
            // 2. Other rooms ordered by optimal capacity
            var preferredRooms = allSuitableRooms
                .Where(r => preferredVenueIds.Contains(r.Id))
                .OrderBy(r => Math.Abs(r.LearningCapacity - course.CapacityRequired))
                .ToList();

            var alternativeRooms = allSuitableRooms
                .Where(r => !preferredVenueIds.Contains(r.Id))
                .OrderBy(r => Math.Abs(r.LearningCapacity - course.CapacityRequired))
                .ToList();

            // Get room schedules
            var roomWeeklySchedules = new Dictionary<int, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>>();
            var roomIds = allSuitableRooms.Select(r => r.Id.ToString()).ToList();

            var roomSchedules = await _context.ScheduleTrackings
                .Where(s => s.EntityType == "LearningRoom" && roomIds.Contains(s.EntityId))
                .Select(s => new { RoomId = int.Parse(s.EntityId), s.Date, s.TimeSlotConfigId, s.PeriodNumber })
                .ToListAsync();

            foreach (var schedule in roomSchedules)
            {
                if (!roomWeeklySchedules.ContainsKey(schedule.RoomId))
                {
                    roomWeeklySchedules[schedule.RoomId] = new HashSet<(DayOfWeek, int, int)>();
                }

                roomWeeklySchedules[schedule.RoomId].Add((schedule.Date.DayOfWeek, schedule.TimeSlotConfigId, schedule.PeriodNumber));
            }

            // Get working days
            var workingDays = workingDaysData
                .Where(d => d.IsWorkingDay)
                .Select(d => new
                {
                    DayOfWeek = Enum.Parse<DayOfWeek>(d.Day, true),
                    TimeSlotConfigId = int.Parse(d.TimeSlotConfigId)
                })
                .ToList();

            if (workingDays.Count == 0)
            {
                // If no working days defined, return empty result
                return result;
            }

            // Calculate distribution for the sessions
            var sessionDays = new List<(DayOfWeek DayOfWeek, int TimeSlotConfigId)>();

            // Distribute sessions evenly across working days
            if (sessionLimit <= workingDays.Count)
            {
                sessionDays.AddRange(workingDays.Take(sessionLimit).Select(d => (d.DayOfWeek, d.TimeSlotConfigId)));
            }
            else
            {
                sessionDays.AddRange(workingDays.Select(d => (d.DayOfWeek, d.TimeSlotConfigId)));

                int remaining = sessionLimit - workingDays.Count;
                for (int i = 0; i < remaining; i++)
                {
                    sessionDays.Add((workingDays[i % workingDays.Count].DayOfWeek, workingDays[i % workingDays.Count].TimeSlotConfigId));
                }
            }

            // Schedule the sessions
            int scheduledSessions = 0;

            // NEW: Multiple attempts with different room sets
            int attemptNumber = 0;
            bool stillTrying = true;

            while (stillTrying && scheduledSessions < sessionLimit && attemptNumber < 3)
            {
                attemptNumber++;
                Console.WriteLine($"Attempt #{attemptNumber} to schedule sessions for course {course.Id} - {course.CourseName}");

                List<LearningRoom> roomsToTry;

                // Different room sets for different attempts
                if (attemptNumber == 1)
                {
                    // 1st attempt: Try preferred rooms first
                    roomsToTry = preferredRooms.ToList();
                    if (roomsToTry.Count == 0)
                    {
                        // If no preferred rooms, use all alternative rooms
                        roomsToTry = alternativeRooms.ToList();
                    }
                }
                else if (attemptNumber == 2)
                {
                    // 2nd attempt: Try all rooms by capacity
                    roomsToTry = allSuitableRooms
                        .OrderBy(r => Math.Abs(r.LearningCapacity - course.CapacityRequired))
                        .ToList();
                }
                else
                {
                    // 3rd attempt: Try all rooms, including those that might normally be too large
                    roomsToTry = allSuitableRooms
                        .OrderBy(r => r.Id) // Any order, just try them all
                        .ToList();
                }

                if (!roomsToTry.Any())
                {
                    // Skip to next attempt if no rooms in this set
                    continue;
                }

                // Make a copy of session days we still need to schedule
                var remainingSessionDays = sessionDays
                    .Where((_, index) => index >= scheduledSessions)
                    .ToList();

                // Try each remaining day
                foreach (var sessionDay in remainingSessionDays)
                {
                    if (scheduledSessions >= sessionLimit)
                        break;

                    var timeSlotConfig = timeSlotConfigs.FirstOrDefault(t => t.Id == sessionDay.TimeSlotConfigId);
                    if (timeSlotConfig == null) continue;

                    var periodsData = System.Text.Json.JsonSerializer.Deserialize<List<PeriodData>>(
                        timeSlotConfig.PeriodsData,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (periodsData == null) continue;

                    var regularPeriods = periodsData
                        .Where(p => p.Type.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.PeriodNumber)
                        .ToList();

                    // Try to schedule a session
                    bool scheduled = await TryScheduleWeeklySession(
                        course,
                        sessionDay.DayOfWeek,
                        startDate,
                        endDate,
                        timeSlotConfig.Id,
                        regularPeriods,
                        roomsToTry,
                        roomWeeklySchedules,
                        globalStudentGroupWeeklySchedule[course.YearTaken],
                        instructorWeeklySchedule,
                        academicYearId,
                        modeOfStudyId,
                        result,
                        bookedRooms);

                    if (scheduled)
                    {
                        scheduledSessions++;
                    }
                }

                // If we've scheduled all required sessions, we can stop trying
                if (scheduledSessions >= sessionLimit)
                {
                    stillTrying = false;
                }
                // If we made progress in this attempt, try again with the same approach
                // but we didn't schedule any sessions in this attempt, move to next approach
                else if (scheduledSessions == 0 && attemptNumber == 3)
                {
                    // If we're on the final attempt and haven't scheduled anything,
                    // we'll stop and let virtual sessions handle it
                    stillTrying = false;
                }
            }

            // Log how many physical sessions were scheduled
            if (scheduledSessions > 0)
            {
                Console.WriteLine($"Successfully scheduled {scheduledSessions} physical sessions for course {course.Id} - {course.CourseName}");
            }
            else
            {
                Console.WriteLine($"Could not schedule any physical sessions for course {course.Id} - {course.CourseName} after multiple attempts");
            }

            return result;
        }

        // Helper method to populate ViewBag
        private async Task PopulateViewBagForGenerateView()
        {
            var academicYears = await _context.AcademicYears
                .Where(ay => ay.IsActive)
                .ToListAsync();
            var modesOfStudy = await _context.ModesOfStudy.ToListAsync();

            // Get the user's role context (department or school)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<Programme> programmes = new List<Programme>();

            // Check if user is Dean
            var schoolAsDean = await _context.Schools
                .Include(s => s.Departments)
                    .ThenInclude(d => d.Programmes)
                .FirstOrDefaultAsync(s => s.DeanId == userId || s.AssistantDeanId == userId);

            if (schoolAsDean != null)
            {
                // If user is Dean, get all programmes from all departments in the school
                foreach (var dept in schoolAsDean.Departments)
                {
                    if (dept.Programmes != null)
                    {
                        programmes.AddRange(dept.Programmes);
                    }
                }
                ViewBag.UserRole = "Dean";
                ViewBag.EntityName = schoolAsDean.Name;
            }
            else
            {
                // If user is HOD, get programmes from their department
                var department = await _context.Departments
                    .Include(d => d.Programmes)
                    .FirstOrDefaultAsync(d => d.HODId == userId);

                if (department != null && department.Programmes != null)
                {
                    programmes = department.Programmes.ToList();
                }
                ViewBag.UserRole = "HOD";
                ViewBag.EntityName = department?.Name;
            }

            ViewBag.AcademicYears = academicYears;
            ViewBag.ModesOfStudy = modesOfStudy;
            ViewBag.Programmes = programmes;
        }

        private async Task<int> GetVirtualRoomIdAsync()
        {
            var virtualRoom = await _context.LearningRooms
                .Where(r => r.Name.Contains("Virtual") || r.Name.Contains("Online"))
                .FirstOrDefaultAsync();

            if (virtualRoom == null)
            {
                throw new InvalidOperationException("No virtual room exists in the system. Please contact the administrator.");
            }

            return virtualRoom.Id;
        }


        // Add this method to the TimetableController class
        private List<string> ValidateGeneratedTimetable(List<Timetable> timetableEntries)
        {
            var conflicts = new List<string>();

            // Group by date, time slot, and period to find conflicts
            var groupedEntries = timetableEntries
                .GroupBy(t => new { t.CourseId, t.Date, t.TimeSlotConfigId, t.PeriodNumber })
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in groupedEntries)
            {
                conflicts.Add($"Course {group.Key.CourseId} has multiple entries on {group.Key.Date.ToShortDateString()} " +
                              $"at period {group.Key.PeriodNumber} in time slot {group.Key.TimeSlotConfigId}");
            }

            // Get all course information for checking instructor conflicts
            var courseIds = timetableEntries.Select(t => t.CourseId).Distinct().ToList();

            // This would require a database call in practice, but for simplicity we'll assume we have the course data
            // In a real implementation, you'd load all courses with their instructors in a single query
            var courseInstructors = _context.Courses
                .Where(c => courseIds.Contains(c.Id))
                .Select(c => new { c.Id, c.InstructorId })
                .ToDictionary(c => c.Id, c => c.InstructorId);

            // Group by date, time slot, period, and instructor to find instructor conflicts
            var instructorEntries = timetableEntries
                .Where(t => courseInstructors.ContainsKey(t.CourseId))
                .GroupBy(t => new
                {
                    InstructorId = courseInstructors[t.CourseId],
                    t.Date,
                    t.TimeSlotConfigId,
                    t.PeriodNumber
                })
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in instructorEntries)
            {
                conflicts.Add($"Instructor {group.Key.InstructorId} has multiple courses scheduled on {group.Key.Date.ToShortDateString()} " +
                              $"at period {group.Key.PeriodNumber} in time slot {group.Key.TimeSlotConfigId}");
            }

            return conflicts;
        }

        // Helper method to generate timetable for a specific programme
        // Helper method to generate timetable for a specific programme
        private async Task<TimetableGenerationResult> GenerateTimetableForProgramme(
             Programme programme,
             int academicYearId,
             int modeOfStudyId,
             List<WorkingDayData> workingDaysData,
             List<TimeSlotConfiguration> timeSlotConfigs,
             List<LearningRoom> allRooms,
             HashSet<RoomBookingKey> bookedRooms,
             Dictionary<string, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>> allInstructorSchedules,
             Dictionary<int, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>> globalStudentGroupWeeklySchedule,
             int virtualRoomId)  // Added parameter
        {
            var result = new TimetableGenerationResult();

            // Get all courses for the programme
            var courses = await _context.Courses
                .Where(c => c.ProgrammeID == programme.Id)
                .OrderByDescending(c => c.CapacityRequired) // Largest capacity first
                .ToListAsync();

            if (!courses.Any())
            {
                return result; // No courses to schedule
            }

            // Use the global student group weekly schedule instead of creating a local one
            // Initialize any missing year groups
            foreach (var course in courses)
            {
                if (!globalStudentGroupWeeklySchedule.ContainsKey(course.YearTaken))
                {
                    globalStudentGroupWeeklySchedule[course.YearTaken] = new HashSet<(DayOfWeek, int, int)>();
                }
            }

            // Get the academic year details for start/end dates
            var academicYear = await _context.AcademicYears.FindAsync(academicYearId);
            if (academicYear == null)
            {
                // Use reasonable defaults if not found
                var startDate = DateTime.Now.Date;
                var endDate = startDate.AddMonths(4);

                // Process each course with semester dates
                foreach (var course in courses)
                {
                    await ScheduleCourseSessionsAsync(
                        course,
                        allRooms,
                        workingDaysData,
                        timeSlotConfigs,
                        globalStudentGroupWeeklySchedule,
                        allInstructorSchedules,
                        academicYearId,
                        modeOfStudyId,
                        startDate,
                        endDate,
                        result,
                        bookedRooms,
                        virtualRoomId  // Pass the virtual room ID
                    );
                }
            }
            else
            {
                // Use real academic year dates if available
                var startDate = academicYear.StartDate;
                var endDate = academicYear.EndDate;

                // Process each course with actual academic year dates
                foreach (var course in courses)
                {
                    await ScheduleCourseSessionsAsync(
                        course,
                        allRooms,
                        workingDaysData,
                        timeSlotConfigs,
                        globalStudentGroupWeeklySchedule,
                        allInstructorSchedules,
                        academicYearId,
                        modeOfStudyId,
                        startDate,
                        endDate,
                        result,
                        bookedRooms,
                        virtualRoomId  // Pass the virtual room ID
                    );
                }
            }

            return result;
        }


        // Helper method to schedule sessions for a specific course
        private async Task ScheduleCourseSessionsAsync(
             Course course,
             List<LearningRoom> availableRooms,
             List<WorkingDayData> workingDaysData,
             List<TimeSlotConfiguration> timeSlotConfigs,
             Dictionary<int, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>> globalStudentGroupWeeklySchedule,
             Dictionary<string, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>> allInstructorSchedules,
             int academicYearId,
             int modeOfStudyId,
             DateTime startDate,
             DateTime endDate,
             TimetableGenerationResult result,
             HashSet<RoomBookingKey> bookedRooms,
             int virtualRoomId)  // Use the provided virtual room ID
        {
            // Determine number of sessions to schedule per week
            int sessionsToSchedule = course.MeetingFrequencyPerWeek;

            // Get working days
            var workingDays = workingDaysData
                .Where(d => d.IsWorkingDay)
                .Select(d => new
                {
                    DayOfWeek = Enum.Parse<DayOfWeek>(d.Day, true),
                    TimeSlotConfigId = int.Parse(d.TimeSlotConfigId)
                })
                .ToList();

            if (workingDays.Count == 0)
            {
                // No working days configured
                return;
            }

            // Calculate distribution of sessions to spread evenly across working days
            var sessionDays = new List<(DayOfWeek DayOfWeek, int TimeSlotConfigId)>();

            if (sessionsToSchedule <= workingDays.Count)
            {
                // Distribute one session per working day up to the number needed
                sessionDays.AddRange(workingDays.Take(sessionsToSchedule).Select(d => (d.DayOfWeek, d.TimeSlotConfigId)));
            }
            else
            {
                // First add one session per working day
                sessionDays.AddRange(workingDays.Select(d => (d.DayOfWeek, d.TimeSlotConfigId)));

                // Then distribute the remaining sessions evenly
                int remaining = sessionsToSchedule - workingDays.Count;
                for (int i = 0; i < remaining; i++)
                {
                    sessionDays.Add((workingDays[i % workingDays.Count].DayOfWeek, workingDays[i % workingDays.Count].TimeSlotConfigId));
                }
            }

            // Initialize student group if not exists - Now using the global dictionary
            if (!globalStudentGroupWeeklySchedule.ContainsKey(course.YearTaken))
            {
                globalStudentGroupWeeklySchedule[course.YearTaken] = new HashSet<(DayOfWeek, int, int)>();
            }

            // Get instructor's schedule from the global dictionary
            var instructorWeeklySchedule = new HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>();
            if (allInstructorSchedules.ContainsKey(course.InstructorId))
            {
                instructorWeeklySchedule = allInstructorSchedules[course.InstructorId];
            }
            else
            {
                // Initialize if not exists
                allInstructorSchedules[course.InstructorId] = instructorWeeklySchedule;
            }

            // Double-check against the database to make sure we have the latest
            // This is a safety check to prevent conflicts even if the in-memory tracking misses something
            var additionalInstructorSchedules = await _context.ScheduleTrackings
                .Where(s => s.EntityId == course.InstructorId && s.EntityType == "Instructor")
                .Select(s => new { s.Date, s.TimeSlotConfigId, s.PeriodNumber })
                .ToListAsync();

            // Add any schedules not already in our tracking
            foreach (var schedule in additionalInstructorSchedules)
            {
                instructorWeeklySchedule.Add((schedule.Date.DayOfWeek, schedule.TimeSlotConfigId, schedule.PeriodNumber));
            }

            // Parse preferred venues from JSON
            List<int> preferredVenueIds = new List<int>();
            if (!string.IsNullOrEmpty(course.PreferredVenueIds))
            {
                try
                {
                    preferredVenueIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(
                        course.PreferredVenueIds,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch
                {
                    Console.WriteLine($"Error parsing preferred venues for course {course.Id}");
                }
            }

            // Filter rooms based on preference and capacity
            var preferredRooms = availableRooms
                .Where(r => preferredVenueIds.Contains(r.Id) && r.LearningCapacity >= course.CapacityRequired)
                .ToList();

            var alternativeRooms = availableRooms
                .Where(r => !preferredVenueIds.Contains(r.Id) && r.LearningCapacity >= course.CapacityRequired)
                .ToList();

            // Combine rooms with preferred rooms first
            var suitableRooms = preferredRooms.Concat(alternativeRooms).ToList();

            // Load existing room schedules (converted to weekly patterns)
            var roomWeeklySchedules = new Dictionary<int, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>>();

            // Get IDs for query
            var roomIds = suitableRooms.Select(r => r.Id.ToString()).ToList();

            // Load from database
            var roomSchedules = await _context.ScheduleTrackings
                .Where(s => s.EntityType == "LearningRoom" && roomIds.Contains(s.EntityId))
                .Select(s => new { RoomId = int.Parse(s.EntityId), s.Date, s.TimeSlotConfigId, s.PeriodNumber })
                .ToListAsync();

            // Convert to pattern-based tracking
            foreach (var schedule in roomSchedules)
            {
                if (!roomWeeklySchedules.ContainsKey(schedule.RoomId))
                {
                    roomWeeklySchedules[schedule.RoomId] = new HashSet<(DayOfWeek, int, int)>();
                }

                roomWeeklySchedules[schedule.RoomId].Add((schedule.Date.DayOfWeek, schedule.TimeSlotConfigId, schedule.PeriodNumber));
            }

            // Schedule each required weekly session
            int scheduledSessions = 0;

            // Try to schedule each session on a different working day
            foreach (var sessionDay in sessionDays)
            {
                // Get time slot config for this day
                var timeSlotConfig = timeSlotConfigs.FirstOrDefault(t => t.Id == sessionDay.TimeSlotConfigId);

                if (timeSlotConfig != null)
                {
                    // Parse periods data
                    var periodsData = System.Text.Json.JsonSerializer.Deserialize<List<PeriodData>>(
                        timeSlotConfig.PeriodsData,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (periodsData != null)
                    {
                        // Get regular periods (exclude breaks)
                        var regularPeriods = periodsData
                            .Where(p => p.Type.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(p => p.PeriodNumber)
                            .ToList();

                        // Try to schedule on this day
                        bool scheduled = await TryScheduleWeeklySession(
                            course,
                            sessionDay.DayOfWeek,
                            startDate,
                            endDate,
                            timeSlotConfig.Id,
                            regularPeriods,
                            suitableRooms,
                            roomWeeklySchedules,
                            globalStudentGroupWeeklySchedule[course.YearTaken],
                            instructorWeeklySchedule,
                            academicYearId,
                            modeOfStudyId,
                            result,
                            bookedRooms);

                        if (scheduled)
                        {
                            scheduledSessions++;
                            if (scheduledSessions >= sessionsToSchedule)
                            {
                                break; // We've scheduled all required sessions
                            }
                        }
                    }
                }
            }

            // If we couldn't schedule all sessions, create virtual meetings for remaining sessions
            if (scheduledSessions < sessionsToSchedule)
            {
                // Try additional days or create virtual sessions
                foreach (var workingDay in workingDays.Where(d => !sessionDays.Any(sd => sd.DayOfWeek == d.DayOfWeek)))
                {
                    if (scheduledSessions >= sessionsToSchedule)
                        break;

                    var timeSlotConfig = timeSlotConfigs.FirstOrDefault(t => t.Id == workingDay.TimeSlotConfigId);
                    if (timeSlotConfig == null)
                        continue;

                    var periodsData = System.Text.Json.JsonSerializer.Deserialize<List<PeriodData>>(
                        timeSlotConfig.PeriodsData,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (periodsData == null)
                        continue;

                    var regularPeriods = periodsData
                        .Where(p => p.Type.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.PeriodNumber)
                        .ToList();

                    // Try virtual sessions
                    foreach (var period in regularPeriods)
                    {
                        // Check conflicts for instructor and student group
                        var dayPeriodKey = (workingDay.DayOfWeek, timeSlotConfig.Id, period.PeriodNumber);

                        // Check against global instructor schedule
                        if (instructorWeeklySchedule.Contains(dayPeriodKey) ||
                            globalStudentGroupWeeklySchedule[course.YearTaken].Contains(dayPeriodKey))
                        {
                            continue; // Skip if conflict exists
                        }

                        // Get instructor scheduling records and check locally
                        var instructorSchedules = await _context.ScheduleTrackings
                            .Where(s => s.EntityId == course.InstructorId
                                     && s.EntityType == "Instructor"
                                     && s.TimeSlotConfigId == timeSlotConfig.Id
                                     && s.PeriodNumber == period.PeriodNumber)
                            .ToListAsync();

                        // Now check the DayOfWeek in memory, not in the query
                        bool instructorConflict = instructorSchedules.Any(s => s.Date.DayOfWeek == workingDay.DayOfWeek);

                        if (instructorConflict)
                        {
                            continue; // Skip if conflict exists in database
                        }

                        // Get the first occurrence of this day of the week from the start date
                        DateTime firstSessionDate = GetFirstOccurrenceOfDay(startDate, workingDay.DayOfWeek);

                        // Create virtual session - now use the virtual room ID
                        var virtualSession = new Timetable
                        {
                            CourseId = course.Id,
                            LearningRoomId = virtualRoomId,  // Use the virtual room ID
                            TimeSlotConfigId = timeSlotConfig.Id,
                            Date = firstSessionDate,
                            AcademicYearId = academicYearId,
                            ModeOfStudyId = modeOfStudyId,
                            PeriodNumber = period.PeriodNumber,
                            SpecialInstructions = "Virtual Meeting - Online session",
                            Status = "Draft",
                            IsRecurring = true,
                            RecurrenceEndDate = endDate,
                            CreatedBy = course.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        result.TimetableEntries.Add(virtualSession);

                        // Add tracking records
                        var instructorTracking = new ScheduleTracking
                        {
                            EntityId = course.InstructorId,
                            EntityType = "Instructor",
                            TimeSlotConfigId = timeSlotConfig.Id,
                            Date = firstSessionDate,
                            IsOccupied = true,
                            OccupiedByCourseId = course.Id,
                            PeriodNumber = period.PeriodNumber,
                            CreatedBy = course.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        result.ScheduleTrackings.Add(instructorTracking);

                        // Add tracking for virtual room
                        var roomTracking = new ScheduleTracking
                        {
                            EntityId = virtualRoomId.ToString(),  // Use virtual room ID here too
                            EntityType = "LearningRoom",
                            TimeSlotConfigId = timeSlotConfig.Id,
                            Date = firstSessionDate,
                            IsOccupied = true,
                            OccupiedByCourseId = course.Id,
                            PeriodNumber = period.PeriodNumber,
                            CreatedBy = course.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        result.ScheduleTrackings.Add(roomTracking);

                        // Update pattern tracking
                        instructorWeeklySchedule.Add(dayPeriodKey);
                        globalStudentGroupWeeklySchedule[course.YearTaken].Add(dayPeriodKey);

                        scheduledSessions++;
                        break; // Move to next day
                    }
                }
            }

            // If still couldn't schedule all sessions, try finding any available slots
            if (scheduledSessions < sessionsToSchedule)
            {
                // Iterate through all possible day/period combinations
                foreach (var workingDay in workingDays)
                {
                    if (scheduledSessions >= sessionsToSchedule)
                        break;

                    var timeSlotConfig = timeSlotConfigs.FirstOrDefault(t => t.Id == workingDay.TimeSlotConfigId);
                    if (timeSlotConfig == null)
                        continue;

                    var periodsData = System.Text.Json.JsonSerializer.Deserialize<List<PeriodData>>(
                        timeSlotConfig.PeriodsData,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (periodsData == null)
                        continue;

                    var regularPeriods = periodsData
                        .Where(p => p.Type.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.PeriodNumber)
                        .ToList();

                    foreach (var period in regularPeriods)
                    {
                        // Skip periods already tried
                        if (sessionDays.Any(sd => sd.DayOfWeek == workingDay.DayOfWeek && sd.TimeSlotConfigId == timeSlotConfig.Id))
                        {
                            // If this exact day and time slot combination is already in sessionDays, skip checking this period
                            bool periodAlreadyTried = false;

                            // Check if we already tried to schedule this course in this specific period
                            var existingEntries = result.TimetableEntries.Where(t =>
                                t.CourseId == course.Id &&
                                t.Date.DayOfWeek == workingDay.DayOfWeek &&
                                t.TimeSlotConfigId == timeSlotConfig.Id &&
                                t.PeriodNumber == period.PeriodNumber).ToList();

                            if (existingEntries.Any())
                            {
                                periodAlreadyTried = true;
                            }

                            if (periodAlreadyTried)
                                continue;
                        }

                        // Check conflicts for instructor and student group
                        var dayPeriodKey = (workingDay.DayOfWeek, timeSlotConfig.Id, period.PeriodNumber);

                        if (instructorWeeklySchedule.Contains(dayPeriodKey) ||
                            globalStudentGroupWeeklySchedule[course.YearTaken].Contains(dayPeriodKey))
                        {
                            continue; // Skip if conflict exists
                        }

                        // Get the first occurrence of this day of the week from the start date
                        DateTime firstSessionDate = GetFirstOccurrenceOfDay(startDate, workingDay.DayOfWeek);

                        // Create virtual session as a last resort, using the virtual room ID
                        var virtualSession = new Timetable
                        {
                            CourseId = course.Id,
                            LearningRoomId = virtualRoomId,  // Use the virtual room ID
                            TimeSlotConfigId = timeSlotConfig.Id,
                            Date = firstSessionDate,
                            AcademicYearId = academicYearId,
                            ModeOfStudyId = modeOfStudyId,
                            PeriodNumber = period.PeriodNumber,
                            SpecialInstructions = "Virtual Meeting (Backup) - Online session",
                            Status = "Draft",
                            IsRecurring = true,
                            RecurrenceEndDate = endDate,
                            CreatedBy = course.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        result.TimetableEntries.Add(virtualSession);

                        // Add tracking records
                        var instructorTracking = new ScheduleTracking
                        {
                            EntityId = course.InstructorId,
                            EntityType = "Instructor",
                            TimeSlotConfigId = timeSlotConfig.Id,
                            Date = firstSessionDate,
                            IsOccupied = true,
                            OccupiedByCourseId = course.Id,
                            PeriodNumber = period.PeriodNumber,
                            CreatedBy = course.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        result.ScheduleTrackings.Add(instructorTracking);

                        // Add tracking for virtual room
                        var roomTracking = new ScheduleTracking
                        {
                            EntityId = virtualRoomId.ToString(),  // Use the virtual room ID here too
                            EntityType = "LearningRoom",
                            TimeSlotConfigId = timeSlotConfig.Id,
                            Date = firstSessionDate,
                            IsOccupied = true,
                            OccupiedByCourseId = course.Id,
                            PeriodNumber = period.PeriodNumber,
                            CreatedBy = course.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        result.ScheduleTrackings.Add(roomTracking);

                        // Update pattern tracking
                        instructorWeeklySchedule.Add(dayPeriodKey);
                        globalStudentGroupWeeklySchedule[course.YearTaken].Add(dayPeriodKey);

                        scheduledSessions++;
                        if (scheduledSessions >= sessionsToSchedule)
                            break;
                    }
                }
            }

            // Log if we couldn't schedule all required sessions
            if (scheduledSessions < sessionsToSchedule)
            {
                Console.WriteLine($"Warning: Could only schedule {scheduledSessions} out of {sessionsToSchedule} required sessions for course {course.Id}");
            }
        }




        // Helper method to get the first occurrence of a day from a start date
        private DateTime GetFirstOccurrenceOfDay(DateTime startDate, DayOfWeek targetDay)
        {
            int daysToAdd = ((int)targetDay - (int)startDate.DayOfWeek + 7) % 7;
            return startDate.AddDays(daysToAdd);
        }

        private async Task<bool> TryScheduleWeeklySession(
     Course course,
     DayOfWeek dayOfWeek,
     DateTime startDate,
     DateTime endDate,
     int timeSlotConfigId,
     List<PeriodData> regularPeriods,
     List<LearningRoom> availableRooms, // Now contains properly prioritized rooms
     Dictionary<int, HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)>> roomWeeklySchedules,
     HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)> studentGroupWeeklySchedule,
     HashSet<(DayOfWeek DayOfWeek, int TimeSlotConfigId, int PeriodNumber)> instructorWeeklySchedule,
     int academicYearId,
     int modeOfStudyId,
     TimetableGenerationResult result,
     HashSet<RoomBookingKey> bookedRooms)
        {
            // Calculate first occurrence of this day from the semester start date
            DateTime firstSessionDate = GetFirstOccurrenceOfDay(startDate, dayOfWeek);

            // NEW: Try multiple periods before giving up on this day
            // First, collect all viable periods for this day
            var viablePeriods = new List<(PeriodData Period, LearningRoom Room)>();

            foreach (var period in regularPeriods)
            {
                // Check if instructor is available for this weekly slot
                var currentWeeklySlot = (dayOfWeek, timeSlotConfigId, period.PeriodNumber);
                if (instructorWeeklySchedule.Contains(currentWeeklySlot))
                {
                    continue; // Instructor not available
                }

                // EXTRA CHECK: Get instructor scheduling records and check locally
                var instructorSchedules = await _context.ScheduleTrackings
                    .Where(s => s.EntityId == course.InstructorId &&
                           s.EntityType == "Instructor" &&
                           s.TimeSlotConfigId == timeSlotConfigId &&
                           s.PeriodNumber == period.PeriodNumber)
                    .ToListAsync();

                bool instructorConflict = instructorSchedules.Any(s => s.Date.DayOfWeek == dayOfWeek);

                if (instructorConflict)
                {
                    // Update in-memory tracking
                    instructorWeeklySchedule.Add(currentWeeklySlot);
                    continue; // Instructor not available according to database
                }

                // Check if student group is available for this weekly slot
                if (studentGroupWeeklySchedule.Contains(currentWeeklySlot))
                {
                    continue; // Student group not available
                }

                // Now check each room for this period
                foreach (var room in availableRooms)
                {
                    // Check the global booking registry
                    var currentBookingKey = new RoomBookingKey
                    {
                        RoomId = room.Id,
                        DayOfWeek = dayOfWeek,
                        TimeSlotConfigId = timeSlotConfigId,
                        PeriodNumber = period.PeriodNumber
                    };

                    // If already booked in our registry, skip this room
                    if (bookedRooms.Contains(currentBookingKey))
                    {
                        continue;
                    }

                    // Check room's weekly schedule
                    if (roomWeeklySchedules.ContainsKey(room.Id) &&
                        roomWeeklySchedules[room.Id].Contains(currentWeeklySlot))
                    {
                        continue; // Room already scheduled for this weekly slot
                    }

                    // Final check against database
                    var roomBookings = await _context.ScheduleTrackings
                        .Where(s =>
                            s.EntityId == room.Id.ToString() &&
                            s.EntityType == "LearningRoom" &&
                            s.TimeSlotConfigId == timeSlotConfigId &&
                            s.PeriodNumber == period.PeriodNumber)
                        .ToListAsync();

                    // Check day of week in memory
                    bool roomAlreadyBooked = roomBookings.Any(s => s.Date.DayOfWeek == dayOfWeek);

                    if (roomAlreadyBooked)
                    {
                        continue; // Room already booked for this weekly pattern in database
                    }

                    // This period and room combination is viable!
                    viablePeriods.Add((period, room));
                }
            }

            // No viable periods found for this day
            if (!viablePeriods.Any())
            {
                return false;
            }

            // Sort viable options - prefer optimal room size
            viablePeriods = viablePeriods
                .OrderBy(pr => Math.Abs(pr.Room.LearningCapacity - course.CapacityRequired))
                .ToList();

            // Choose the best option (first in the sorted list)
            var bestOption = viablePeriods.First();
            var selectedPeriod = bestOption.Period;
            var selectedRoom = bestOption.Room;

            // Mark as booked immediately
            var slotToSchedule = (dayOfWeek, timeSlotConfigId, selectedPeriod.PeriodNumber);
            var keyToBook = new RoomBookingKey
            {
                RoomId = selectedRoom.Id,
                DayOfWeek = dayOfWeek,
                TimeSlotConfigId = timeSlotConfigId,
                PeriodNumber = selectedPeriod.PeriodNumber
            };

            bookedRooms.Add(keyToBook);

            // Initialize room's weekly schedule if needed
            if (!roomWeeklySchedules.ContainsKey(selectedRoom.Id))
            {
                roomWeeklySchedules[selectedRoom.Id] = new HashSet<(DayOfWeek, int, int)>();
            }

            // Mark this weekly slot as booked for this room
            roomWeeklySchedules[selectedRoom.Id].Add(slotToSchedule);

            // Create timetable entry with the recurring flag
            var timetableEntry = new Timetable
            {
                CourseId = course.Id,
                LearningRoomId = selectedRoom.Id,
                TimeSlotConfigId = timeSlotConfigId,
                Date = firstSessionDate, // First occurrence of this day
                AcademicYearId = academicYearId,
                ModeOfStudyId = modeOfStudyId,
                PeriodNumber = selectedPeriod.PeriodNumber,
                Status = "Draft",
                IsRecurring = true,
                RecurrenceEndDate = endDate, // End of the academic term
                CreatedBy = course.CreatedBy,
                CreatedAt = DateTime.Now
            };

            result.TimetableEntries.Add(timetableEntry);

            // Create schedule tracking for instructor
            var instructorTracking = new ScheduleTracking
            {
                EntityId = course.InstructorId,
                EntityType = "Instructor",
                TimeSlotConfigId = timeSlotConfigId,
                Date = firstSessionDate, // Store first occurrence date
                IsOccupied = true,
                OccupiedByCourseId = course.Id,
                PeriodNumber = selectedPeriod.PeriodNumber,
                CreatedBy = course.CreatedBy,
                CreatedAt = DateTime.Now
            };

            result.ScheduleTrackings.Add(instructorTracking);

            // Create schedule tracking for room
            var roomTracking = new ScheduleTracking
            {
                EntityId = selectedRoom.Id.ToString(),
                EntityType = "LearningRoom",
                TimeSlotConfigId = timeSlotConfigId,
                Date = firstSessionDate, // Store first occurrence date
                IsOccupied = true,
                OccupiedByCourseId = course.Id,
                PeriodNumber = selectedPeriod.PeriodNumber,
                CreatedBy = course.CreatedBy,
                CreatedAt = DateTime.Now
            };

            result.ScheduleTrackings.Add(roomTracking);

            // Update tracking sets
            studentGroupWeeklySchedule.Add(slotToSchedule);
            instructorWeeklySchedule.Add(slotToSchedule);

            return true;
        }

        private async Task<int> GetOrCreateVirtualRoomIdAsync()
        {
            // Check if a virtual room already exists
            var virtualRoom = await _context.LearningRooms
                .FirstOrDefaultAsync(r => r.Name.Contains("Virtual") || r.Name.Contains("Online"));

            if (virtualRoom != null)
            {
                return virtualRoom.Id;
            }

            // Create a new virtual room if none exists
            var newVirtualRoom = new LearningRoom
            {
                Name = "Virtual/Online Room",
                Description = "Used for online or virtual sessions with no physical location",
                LearningCapacity = 10000, // Large capacity since it's virtual
                IsActive = true,
                RoomType = "Virtual", // Add this line to set the RoomType
                //CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
                //CreatedAt = DateTime.Now
            };

            _context.LearningRooms.Add(newVirtualRoom);
            await _context.SaveChangesAsync();

            return newVirtualRoom.Id;
        }









        [HttpPost]
        [Authorize(Roles = "HOD,Dean")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTimetableGroup(int academicYearId, int modeOfStudyId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                List<int> departmentIds = new List<int>();

                // Check if user is Dean or Assistant Dean
                var schoolAsDean = await _context.Schools
                    .Include(s => s.Departments)
                    .FirstOrDefaultAsync(s => s.DeanId == userId || s.AssistantDeanId == userId);

                if (schoolAsDean != null)
                {
                    // If user is Dean, get all departments in their school
                    departmentIds = schoolAsDean.Departments.Select(d => d.Id).ToList();
                }
                else
                {
                    // Check if user is HOD
                    var department = await _context.Departments
                        .FirstOrDefaultAsync(d => d.HODId == userId);

                    if (department == null)
                    {
                        TempData["Error"] = "You don't have any departments or schools assigned.";
                        return RedirectToAction(nameof(TimetableManagement));
                    }

                    // If user is HOD, only include their department
                    departmentIds.Add(department.Id);
                }

                // Get timetables for the specified academicYearId and modeOfStudyId within user's departments
                var timetablesToDelete = await _context.Timetables
                    .Include(t => t.Course)
                        .ThenInclude(c => c.Programme)
                    .Where(t =>
                        t.AcademicYearId == academicYearId &&
                        t.ModeOfStudyId == modeOfStudyId &&
                        departmentIds.Contains(t.Course.Programme.DepartmentId))
                    .ToListAsync();

                if (!timetablesToDelete.Any())
                {
                    TempData["Info"] = "No timetables found matching the specified criteria.";
                    return RedirectToAction(nameof(TimetableManagement));
                }

                // Get the course IDs from the timetables for schedule tracking records
                var courseIds = timetablesToDelete.Select(t => t.CourseId).Distinct().ToList();

                // Begin transaction to ensure consistency
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        int deletedTimetables = 0;
                        int deletedScheduleTrackings = 0;

                        // First delete schedule tracking records
                        var schedulesToDelete = await _context.ScheduleTrackings
                            .Where(s => courseIds.Contains(s.OccupiedByCourseId ?? 0))
                            .ToListAsync();

                        if (schedulesToDelete.Any())
                        {
                            _context.ScheduleTrackings.RemoveRange(schedulesToDelete);
                            await _context.SaveChangesAsync();
                            deletedScheduleTrackings = schedulesToDelete.Count;
                        }

                        // Then delete timetable entries
                        _context.Timetables.RemoveRange(timetablesToDelete);
                        await _context.SaveChangesAsync();
                        deletedTimetables = timetablesToDelete.Count;

                        // Commit the transaction
                        await transaction.CommitAsync();

                        TempData["Success"] = $"Successfully deleted {deletedTimetables} timetable entries and {deletedScheduleTrackings} schedule tracking records.";
                    }
                    catch (Exception ex)
                    {
                        // Roll back the transaction if an error occurs
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Error during delete operation: {ex.Message}";
                    }
                }

                // Always redirect to TimetableManagement after the operation
                return RedirectToAction(nameof(TimetableManagement));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting timetables: {ex.Message}";
                return RedirectToAction(nameof(TimetableManagement));
            }
        }



        

        [HttpPost]
        [Authorize(Roles = "HOD,Dean")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishTimetableGroup(int academicYearId, int modeOfStudyId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                List<int> departmentIds = new List<int>();

                // Check if user is Dean or Assistant Dean
                var schoolAsDean = await _context.Schools
                    .Include(s => s.Departments)
                    .FirstOrDefaultAsync(s => s.DeanId == userId || s.AssistantDeanId == userId);

                if (schoolAsDean != null)
                {
                    // If user is Dean, get all departments in their school
                    departmentIds = schoolAsDean.Departments.Select(d => d.Id).ToList();
                }
                else
                {
                    // Check if user is HOD
                    var department = await _context.Departments
                        .FirstOrDefaultAsync(d => d.HODId == userId);

                    if (department == null)
                    {
                        TempData["Error"] = "You don't have any departments or schools assigned.";
                        return RedirectToAction(nameof(TimetableManagement));
                    }

                    // If user is HOD, only include their department
                    departmentIds.Add(department.Id);
                }

                // Get timetables for the specified academicYearId and modeOfStudyId within user's departments
                var timetablesToPublish = await _context.Timetables
                    .Include(t => t.Course)
                        .ThenInclude(c => c.Programme)
                    .Where(t =>
                        t.AcademicYearId == academicYearId &&
                        t.ModeOfStudyId == modeOfStudyId &&
                        t.Status == "Draft" && // Only publish draft timetables
                        departmentIds.Contains(t.Course.Programme.DepartmentId))
                    .ToListAsync();

                if (!timetablesToPublish.Any())
                {
                    TempData["Info"] = "No draft timetables found to publish for this group.";
                    return RedirectToAction(nameof(TimetableManagement));
                }

                // Begin transaction for consistency
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Update all timetable entries to published status
                        foreach (var timetable in timetablesToPublish)
                        {
                            timetable.Status = "Published";
                            timetable.UpdatedBy = User.Identity.Name ?? "Unknown";
                            timetable.UpdatedAt = DateTime.Now;
                        }

                        await _context.SaveChangesAsync();

                        // Commit the transaction
                        await transaction.CommitAsync();

                        TempData["Success"] = $"Successfully published {timetablesToPublish.Count} timetable entries.";
                    }
                    catch (Exception ex)
                    {
                        // Roll back the transaction if an error occurs
                        await transaction.RollbackAsync();
                        TempData["Error"] = $"Error during publish operation: {ex.Message}";
                    }
                }

                // Always redirect to TimetableManagement after the operation
                return RedirectToAction(nameof(TimetableManagement));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error publishing timetables: {ex.Message}";
                return RedirectToAction(nameof(TimetableManagement));
            }
        }

        //[HttpPost]
        //[Authorize(Roles = "HOD, Dean")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Delete(int id)
        //{
        //    var timetable = await _context.Timetables.FindAsync(id);
        //    if (timetable == null)
        //    {
        //        return NotFound();
        //    }

        //    _context.Timetables.Remove(timetable);
        //    await _context.SaveChangesAsync();

        //    return RedirectToAction(nameof(Index));
        //}

        // Action for instructors to view their timetables
        [Authorize(Roles = "Dean")]
        public async Task<IActionResult> MyTimetable()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var timetables = await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.LearningRoom)
                .Include(t => t.TimeSlotConfig)
                .Include(t => t.AcademicYear)
                .Include(t => t.ModeOfStudy)
                .Where(t => t.Course.InstructorId == userId)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.PeriodNumber)
                .ToListAsync();

            return View(timetables);
        }

        private string GetInstructorName(string instructorId)
        {
            if (string.IsNullOrEmpty(instructorId))
            {
                return "Not Assigned";
            }

            // Try to get the instructor from the database
            // This assumes you have a User or ApplicationUser model
            var instructor = _context.Users.FirstOrDefault(u => u.Id == instructorId);

            if (instructor == null)
            {
                return "Unknown Instructor";
            }

            // Return instructor name - adjust property names based on your User model
            return instructor.FullName ?? instructor.Id;
        }

        [HttpGet]
        [Authorize(Roles = "HOD,Dean")]
        public async Task<IActionResult> ViewTimetables(int academicYearId, int modeOfStudyId)
        {
            // Get current user
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<int> departmentIds = new List<int>();
            string entityName = "";

            // Check if user is Dean or Assistant Dean
            var schoolAsDean = await _context.Schools
                .Include(s => s.Departments)
                .FirstOrDefaultAsync(s => s.DeanId == userId || s.AssistantDeanId == userId);

            if (schoolAsDean != null)
            {
                // If user is Dean, collect all department IDs in their school
                departmentIds = schoolAsDean.Departments.Select(d => d.Id).ToList();
                entityName = schoolAsDean.Name;
            }
            else
            {
                // If user is HOD, just use their department ID
                var department = await _context.Departments
                    .FirstOrDefaultAsync(d => d.HODId == userId);

                if (department == null)
                {
                    return NotFound("You don't have any departments or schools assigned.");
                }

                departmentIds.Add(department.Id);
                entityName = department.Name;
            }

            // Get timetables for the departments' programmes with the selected academic year and mode of study
            var timetables = await _context.Timetables
                .Include(t => t.Course)
                    .ThenInclude(c => c.Programme)
                        .ThenInclude(p => p.Department)
                .Include(t => t.LearningRoom)
                .Include(t => t.AcademicYear)
                .Include(t => t.ModeOfStudy)
                .Where(t => departmentIds.Contains(t.Course.Programme.DepartmentId)
                       && t.AcademicYearId == academicYearId
                       && t.ModeOfStudyId == modeOfStudyId)
                .ToListAsync();

            if (!timetables.Any())
            {
                TempData["Warning"] = "No timetables found for the selected criteria.";
                return RedirectToAction(nameof(TimetableManagement));
            }

            // Get working days configuration
            var workingDayConfig = await _context.WorkingDayConfigurations
                .FirstOrDefaultAsync(w => w.AcademicYearId == academicYearId
                        && w.ModeOfStudyId == modeOfStudyId && w.IsActive);

            if (workingDayConfig == null)
            {
                TempData["Error"] = "Working days configuration not found for the selected criteria.";
                return RedirectToAction(nameof(TimetableManagement));
            }

            // Parse working days data
            var workingDaysData = JsonSerializer.Deserialize<List<WorkingDayData>>(
                workingDayConfig.WorkingDaysData,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Get active working days
            var workingDays = workingDaysData
                .Where(d => d.IsWorkingDay)
                .Select(d => d.Day)
                .ToList();

            // Get time slot configurations
            var timeSlotConfigIds = workingDaysData
                .Where(d => d.IsWorkingDay && !string.IsNullOrEmpty(d.TimeSlotConfigId))
                .Select(d => int.Parse(d.TimeSlotConfigId))
                .Distinct()
                .ToList();

            var timeSlotConfigs = await _context.TimeSlotConfigurations
                .Where(t => timeSlotConfigIds.Contains(t.Id))
                .ToListAsync();

            // Get periods data from first time slot config (assuming all configs have similar periods)
            var firstConfig = timeSlotConfigs.FirstOrDefault();
            if (firstConfig == null)
            {
                TempData["Error"] = "Time slot configuration not found.";
                return RedirectToAction(nameof(TimetableManagement));
            }

            var periodsData = JsonSerializer.Deserialize<List<PeriodData>>(
                firstConfig.PeriodsData,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Filter out breaks
            var regularPeriods = periodsData
                .Where(p => p.Type.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.PeriodNumber)
                .ToList();

            // Get all venues used in the timetables
            var venueIds = timetables
                .Where(t => t.LearningRoomId > 0) // Exclude virtual meetings
                .Select(t => t.LearningRoomId)
                .Distinct()
                .ToList();

            var venues = await _context.LearningRooms
                .Where(r => venueIds.Contains(r.Id))
                .ToListAsync();

            // Create view model
            var viewModel = new TimetableViewModel
            {
                AcademicYearId = academicYearId,
                AcademicYearValue = timetables.First().AcademicYear.YearValue,
                ModeOfStudyId = modeOfStudyId,
                ModeOfStudyName = timetables.First().ModeOfStudy.ModeName,
                Periods = regularPeriods,
                WorkingDays = workingDays,
                VenueSchedules = new List<VenueScheduleViewModel>(),
                EntityName = entityName,
                UserRole = schoolAsDean != null ? "Dean" : "HOD",
                DepartmentCount = departmentIds.Count
            };

            // Group timetables by venue
            foreach (var venue in venues)
            {
                var venueTimetables = timetables
                    .Where(t => t.LearningRoomId == venue.Id)
                    .ToList();

                if (!venueTimetables.Any())
                {
                    continue;
                }

                var venueSchedule = new VenueScheduleViewModel
                {
                    VenueId = venue.Id,
                    VenueName = venue.Name,
                    DayPeriodSessions = new Dictionary<string, Dictionary<int, List<TimetableSessionViewModel>>>()
                };

                // Initialize days and periods
                foreach (var day in workingDays)
                {
                    venueSchedule.DayPeriodSessions[day] = new Dictionary<int, List<TimetableSessionViewModel>>();

                    foreach (var period in regularPeriods)
                    {
                        venueSchedule.DayPeriodSessions[day][period.PeriodNumber] = new List<TimetableSessionViewModel>();
                    }
                }

                // Populate sessions
                foreach (var timetable in venueTimetables)
                {
                    var dayOfWeek = timetable.Date.DayOfWeek.ToString();

                    // Skip if not a working day
                    if (!workingDays.Contains(dayOfWeek))
                    {
                        continue;
                    }

                    // Skip if period not in regular periods
                    if (!regularPeriods.Any(p => p.PeriodNumber == timetable.PeriodNumber))
                    {
                        continue;
                    }

                    var session = new TimetableSessionViewModel
                    {
                        TimetableId = timetable.Id,
                        CourseId = timetable.CourseId,
                        CourseName = timetable.Course.CourseName,
                        CourseCode = timetable.Course.CourseCode,
                        InstructorName = GetInstructorName(timetable.Course.InstructorId),
                        ProgrammeName = timetable.Course.Programme.Name,
                        DepartmentName = timetable.Course.Programme.Department.Name,  // Added department name
                        PeriodNumber = timetable.PeriodNumber,
                        Date = timetable.Date,
                        DayOfWeek = timetable.Date.DayOfWeek,
                        Status = timetable.Status,
                        IsRecurring = timetable.IsRecurring,
                        RecurrenceEndDate = timetable.RecurrenceEndDate
                    };

                    venueSchedule.DayPeriodSessions[dayOfWeek][timetable.PeriodNumber].Add(session);
                }

                viewModel.VenueSchedules.Add(venueSchedule);
            }

            // Add virtual meetings if any
            var virtualMeetings = timetables.Where(t => t.LearningRoomId == 0).ToList();
            if (virtualMeetings.Any())
            {
                var virtualVenueSchedule = new VenueScheduleViewModel
                {
                    VenueId = 0,
                    VenueName = "Virtual Meetings",
                    DayPeriodSessions = new Dictionary<string, Dictionary<int, List<TimetableSessionViewModel>>>()
                };

                // Initialize days and periods
                foreach (var day in workingDays)
                {
                    virtualVenueSchedule.DayPeriodSessions[day] = new Dictionary<int, List<TimetableSessionViewModel>>();

                    foreach (var period in regularPeriods)
                    {
                        virtualVenueSchedule.DayPeriodSessions[day][period.PeriodNumber] = new List<TimetableSessionViewModel>();
                    }
                }

                // Populate virtual sessions
                foreach (var timetable in virtualMeetings)
                {
                    var dayOfWeek = timetable.Date.DayOfWeek.ToString();

                    // Skip if not a working day
                    if (!workingDays.Contains(dayOfWeek))
                    {
                        continue;
                    }

                    // Skip if period not in regular periods
                    if (!regularPeriods.Any(p => p.PeriodNumber == timetable.PeriodNumber))
                    {
                        continue;
                    }

                    var session = new TimetableSessionViewModel
                    {
                        TimetableId = timetable.Id,
                        CourseId = timetable.CourseId,
                        CourseName = timetable.Course.CourseName,
                        CourseCode = timetable.Course.CourseCode,
                        InstructorName = GetInstructorName(timetable.Course.InstructorId),
                        ProgrammeName = timetable.Course.Programme.Name,
                        DepartmentName = timetable.Course.Programme.Department.Name,  // Added department name
                        PeriodNumber = timetable.PeriodNumber,
                        Date = timetable.Date,
                        DayOfWeek = timetable.Date.DayOfWeek,
                        Status = timetable.Status,
                        IsRecurring = timetable.IsRecurring,
                        RecurrenceEndDate = timetable.RecurrenceEndDate
                    };

                    virtualVenueSchedule.DayPeriodSessions[dayOfWeek][timetable.PeriodNumber].Add(session);
                }

                viewModel.VenueSchedules.Add(virtualVenueSchedule);
            }

            return View(viewModel);
        }


        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> LecturerTimetable()
        {
            // Get the current user's ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get schedule tracking records for this lecturer
            var instructorSchedules = await _context.ScheduleTrackings
                .Where(s => s.EntityId == userId &&
                            s.EntityType == "Instructor" &&
                            s.OccupiedByCourseId != null &&
                            s.IsOccupied)
                .ToListAsync();

            // Get all course IDs from schedule trackings
            var courseIds = instructorSchedules
                .Where(s => s.OccupiedByCourseId.HasValue)
                .Select(s => s.OccupiedByCourseId.Value)
                .Distinct()
                .ToList();

            // Get all timetable entries for these courses
            var timetableEntries = await _context.Timetables
                .Where(t => courseIds.Contains(t.CourseId))
                .Include(t => t.Course)
                    .ThenInclude(c => c.Programme)
                        .ThenInclude(p => p.Department)
                .Include(t => t.LearningRoom)
                .Include(t => t.AcademicYear)
                .Include(t => t.ModeOfStudy)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.PeriodNumber)
                .ToListAsync();

            // Get all working days from the timetable
            var workingDays = timetableEntries
                .Select(t => t.Date.DayOfWeek.ToString())
                .Distinct()
                .OrderBy(d => Enum.Parse<DayOfWeek>(d))
                .ToList();

            // Get all periods used in the timetable
            var periodNumbers = timetableEntries
                .Select(t => t.PeriodNumber)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // Get periods data from the time slot configurations
            var timeSlotConfigIds = timetableEntries.Select(t => t.TimeSlotConfigId).Distinct().ToList();
            var timeSlotConfigs = await _context.TimeSlotConfigurations
                .Where(t => timeSlotConfigIds.Contains(t.Id))
                .ToListAsync();

            // Create a list of period view models
            List<PeriodViewModel> periods = new List<PeriodViewModel>();
            foreach (var config in timeSlotConfigs)
            {
                if (!string.IsNullOrEmpty(config.PeriodsData))
                {
                    var periodsData = System.Text.Json.JsonSerializer.Deserialize<List<PeriodData>>(
                        config.PeriodsData,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (periodsData != null)
                    {
                        foreach (var periodData in periodsData.Where(p => periodNumbers.Contains(p.PeriodNumber)))
                        {
                            // Only add the period if it doesn't already exist in the list
                            if (!periods.Any(p => p.PeriodNumber == periodData.PeriodNumber))
                            {
                                periods.Add(new PeriodViewModel
                                {
                                    PeriodNumber = periodData.PeriodNumber,
                                    StartTime = periodData.StartTime,
                                    EndTime = periodData.EndTime,
                                    Type = periodData.Type
                                });
                            }
                        }
                    }
                }
            }

            // Sort the periods by period number
            periods = periods.OrderBy(p => p.PeriodNumber).ToList();

            // Convert timetable entries to session view models
            var sessionViewModels = timetableEntries.Select(entry => new TimetableSessionViewModel
            {
                TimetableId = entry.Id,
                CourseId = entry.CourseId,
                CourseCode = entry.Course.CourseCode,
                CourseName = entry.Course.CourseName,
                InstructorName = User.Identity.Name,
                ProgrammeName = entry.Course.Programme.Name,
                PeriodNumber = entry.PeriodNumber,
                Date = entry.Date,
                DayOfWeek = entry.Date.DayOfWeek,
                Status = entry.Status,
                IsRecurring = entry.IsRecurring,
                RecurrenceEndDate = entry.RecurrenceEndDate
            }).ToList();

            // Create a weekly timetable structure
            var weeklyTimetable = new Dictionary<string, Dictionary<int, List<TimetableSessionViewModel>>>();

            // Initialize the structure for all working days and periods
            foreach (var day in workingDays)
            {
                weeklyTimetable[day] = new Dictionary<int, List<TimetableSessionViewModel>>();
                foreach (var periodNumber in periodNumbers)
                {
                    weeklyTimetable[day][periodNumber] = new List<TimetableSessionViewModel>();
                }
            }

            // Group sessions by day of week and period
            foreach (var session in sessionViewModels)
            {
                string dayName = session.DayOfWeek.ToString();

                if (weeklyTimetable.ContainsKey(dayName) &&
                    weeklyTimetable[dayName].ContainsKey(session.PeriodNumber))
                {
                    weeklyTimetable[dayName][session.PeriodNumber].Add(session);
                }
            }

            // Create the view model
            var viewModel = new LecturerTimetableViewModel
            {
                WorkingDays = workingDays,
                Periods = periods,
                WeeklyTimetable = weeklyTimetable,
                AcademicYearValue = timetableEntries.FirstOrDefault()?.AcademicYear?.YearValue ?? "Current",
                ModeOfStudyName = timetableEntries.FirstOrDefault()?.ModeOfStudy?.ModeName ?? "All",
                TimetableEntries = timetableEntries
            };

            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> GetAvailableSlots(int timetableId)
        {
            try
            {
                // Get the current user's ID
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Get the current timetable entry with necessary inclusions
                var timetableEntry = await _context.Timetables
                    .Include(t => t.Course)
                    .Include(t => t.ModeOfStudy)
                    .Include(t => t.AcademicYear)
                    .Include(t => t.LearningRoom)
                    .FirstOrDefaultAsync(t => t.Id == timetableId);

                if (timetableEntry == null)
                {
                    return Json(new { success = false, message = "Timetable entry not found" });
                }

                // Verify that the entry is in draft status
                if (timetableEntry.Status != "Draft")
                {
                    return Json(new { success = false, message = "Only sessions in draft status can be rescheduled" });
                }

                // Verify that the instructor is assigned to this course
                if (timetableEntry.Course.InstructorId != userId)
                {
                    return Json(new { success = false, message = "You can only reschedule your own sessions" });
                }

                // Get working day configuration
                var workingDayConfig = await _context.WorkingDayConfigurations
                    .FirstOrDefaultAsync(w => w.AcademicYearId == timetableEntry.AcademicYearId &&
                                            w.ModeOfStudyId == timetableEntry.ModeOfStudyId &&
                                            w.IsActive);

                if (workingDayConfig == null)
                {
                    return Json(new { success = false, message = "No working days configuration found" });
                }

                // Parse working days data
                List<WorkingDayData> workingDaysData;
                try
                {
                    workingDaysData = System.Text.Json.JsonSerializer.Deserialize<List<WorkingDayData>>(
                        workingDayConfig.WorkingDaysData,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            AllowTrailingCommas = true,
                            ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
                        });

                    if (workingDaysData == null)
                    {
                        workingDaysData = new List<WorkingDayData>();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing WorkingDaysData: {ex.Message}");
                    return Json(new { success = false, message = "Error in working days configuration. Please contact the administrator." });
                }

                // Filter to only include working days
                var workingDays = workingDaysData
                    .Where(d => d.IsWorkingDay)
                    .ToList();

                // Get all time slot configurations used in working days
                var timeSlotConfigIds = workingDays
                    .Select(d => int.Parse(d.TimeSlotConfigId))
                    .Distinct()
                    .ToList();

                // Get all time slot configurations
                var timeSlotConfigs = await _context.TimeSlotConfigurations
                    .Where(t => timeSlotConfigIds.Contains(t.Id))
                    .ToListAsync();

                // Dictionary to store period data by timeSlotConfigId
                var periodsByTimeSlotConfig = new Dictionary<int, List<PeriodData>>();

                // Parse all period data for each time slot configuration
                foreach (var config in timeSlotConfigs)
                {
                    try
                    {
                        var periodsData = System.Text.Json.JsonSerializer.Deserialize<List<PeriodData>>(
                            config.PeriodsData,
                            new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true,
                                AllowTrailingCommas = true,
                                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
                            });

                        if (periodsData != null)
                        {
                            periodsByTimeSlotConfig[config.Id] = periodsData
                                .Where(p => p.Type.Equals("Regular", StringComparison.OrdinalIgnoreCase))
                                .ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing PeriodsData for config {config.Id}: {ex.Message}");
                        // Skip this config if there's an error
                    }
                }

                // Get suitable rooms based on required capacity
                int requiredCapacity = timetableEntry.Course.CapacityRequired;

                // Parse preferred venues if specified
                List<int> preferredVenueIds = new List<int>();
                if (!string.IsNullOrEmpty(timetableEntry.Course.PreferredVenueIds))
                {
                    try
                    {
                        // Try to parse as JSON array
                        preferredVenueIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(
                            timetableEntry.Course.PreferredVenueIds,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch
                    {
                        // If parsing fails, try simple comma-separated format
                        preferredVenueIds = timetableEntry.Course.PreferredVenueIds
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(id => int.Parse(id.Trim()))
                            .ToList();
                    }
                }

                // Get all suitable rooms
                var suitableRooms = await _context.LearningRooms
                    .Where(r => r.IsActive && r.LearningCapacity >= requiredCapacity)
                    .OrderByDescending(r => preferredVenueIds.Contains(r.Id)) // Preferred venues first
                    .ThenBy(r => Math.Abs(r.LearningCapacity - requiredCapacity)) // Then by closest capacity match
                    .ToListAsync();

                if (!suitableRooms.Any())
                {
                    return Json(new { success = false, message = "No suitable rooms found with enough capacity" });
                }

                // Store available slots
                var availableSlots = new List<object>();

                // Get current day of week for the session we are rescheduling
                string currentDayOfWeek = timetableEntry.Date.DayOfWeek.ToString();

                // Get programmeId and yearTaken for student group conflict checking
                int programmeId = timetableEntry.Course.ProgrammeID;
                int yearTaken = timetableEntry.Course.YearTaken;

                // Get all instructor sessions
                var instructorSessions = await _context.Timetables
                    .Where(t => t.Course.InstructorId == userId)
                    .ToListAsync();

                // Get all student group sessions (same programme and year)
                var studentGroupSessions = await _context.Timetables
                    .Include(t => t.Course)
                    .Where(t =>
                        t.Course.ProgrammeID == programmeId &&
                        t.Course.YearTaken == yearTaken)
                    .ToListAsync();

                // For each working day, check available periods
                foreach (var workingDay in workingDays)
                {
                    string dayOfWeek = workingDay.Day;
                    int timeSlotConfigId = int.Parse(workingDay.TimeSlotConfigId);

                    // Skip if this time slot config doesn't have periods data
                    if (!periodsByTimeSlotConfig.ContainsKey(timeSlotConfigId))
                    {
                        continue;
                    }

                    var periodsForThisDay = periodsByTimeSlotConfig[timeSlotConfigId];

                    // Check each regular period
                    foreach (var period in periodsForThisDay.Where(p => p.Type.Equals("Regular", StringComparison.OrdinalIgnoreCase)))
                    {
                        int periodNumber = period.PeriodNumber;

                        // Skip if this is the current time slot (no need to reschedule to same slot)
                        if (dayOfWeek == currentDayOfWeek &&
                            timeSlotConfigId == timetableEntry.TimeSlotConfigId &&
                            periodNumber == timetableEntry.PeriodNumber)
                        {
                            continue;
                        }

                        // Check instructor availability (excluding the current course)
                        bool instructorIsBusy = instructorSessions.Any(t =>
                            t.Date.DayOfWeek.ToString() == dayOfWeek &&
                            t.TimeSlotConfigId == timeSlotConfigId &&
                            t.PeriodNumber == periodNumber &&
                            t.CourseId != timetableEntry.CourseId);

                        if (instructorIsBusy)
                        {
                            continue; // Instructor not available
                        }

                        // Check student group availability (excluding the current course)
                        bool studentGroupIsBusy = studentGroupSessions.Any(t =>
                            t.Date.DayOfWeek.ToString() == dayOfWeek &&
                            t.TimeSlotConfigId == timeSlotConfigId &&
                            t.PeriodNumber == periodNumber &&
                            t.CourseId != timetableEntry.CourseId);

                        if (studentGroupIsBusy)
                        {
                            continue; // Student group not available
                        }

                        // Find available rooms for this time
                        var availableRoomsForSlot = new List<object>();

                        foreach (var room in suitableRooms)
                        {
                            // Get all room bookings
                            var roomBookings = await _context.Timetables
                                .Where(t =>
                                    t.LearningRoomId == room.Id &&
                                    t.TimeSlotConfigId == timeSlotConfigId &&
                                    t.PeriodNumber == periodNumber &&
                                    t.CourseId != timetableEntry.CourseId)
                                .ToListAsync();

                            // Check if room is available on this day
                            bool roomIsBooked = roomBookings.Any(t =>
                                t.Date.DayOfWeek.ToString() == dayOfWeek);

                            if (!roomIsBooked)
                            {
                                // Room is available
                                bool isPreferred = preferredVenueIds.Contains(room.Id);

                                availableRoomsForSlot.Add(new
                                {
                                    roomId = room.Id,
                                    roomName = room.Name + (isPreferred ? " (Preferred)" : ""),
                                    capacity = room.LearningCapacity,
                                    isPreferred = isPreferred
                                });

                                // Limit to 3 rooms per slot
                                if (availableRoomsForSlot.Count >= 3)
                                {
                                    break;
                                }
                            }
                        }

                        // If we found available rooms for this slot, add it to results
                        if (availableRoomsForSlot.Any())
                        {
                            foreach (dynamic room in availableRoomsForSlot)
                            {
                                availableSlots.Add(new
                                {
                                    roomId = room.roomId,
                                    roomName = room.roomName,
                                    capacity = room.capacity,
                                    day = dayOfWeek,
                                    periodNumber = periodNumber,
                                    startTime = period.StartTime,
                                    endTime = period.EndTime,
                                    timeSlotConfigId = timeSlotConfigId
                                });
                            }
                        }
                    }
                }

                // Sort available slots by day of week, then by period number
                var sortedSlots = availableSlots
                    .OrderBy(s =>
                    {
                        var day = (string)((dynamic)s).day;
                        var dayOrder = new Dictionary<string, int> {
                    {"Monday", 1}, {"Tuesday", 2}, {"Wednesday", 3},
                    {"Thursday", 4}, {"Friday", 5}, {"Saturday", 6}, {"Sunday", 7}
                        };
                        return dayOrder.ContainsKey(day) ? dayOrder[day] : 8;
                    })
                    .ThenBy(s => ((dynamic)s).periodNumber)
                    .ToList();

                return Json(new { success = true, slots = sortedSlots });
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error in GetAvailableSlots: {ex}");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Lecturer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleSession(int timetableId, int roomId, int periodNumber, string day, DateTime date)
        {
            try
            {
                // Get the user ID
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Get the original timetable entry
                var originalTimetable = await _context.Timetables
                    .Include(t => t.Course)
                    .Include(t => t.AcademicYear)
                    .Include(t => t.ModeOfStudy)
                    .FirstOrDefaultAsync(t => t.Id == timetableId);

                if (originalTimetable == null)
                {
                    TempData["Error"] = "Timetable entry not found.";
                    return RedirectToAction(nameof(LecturerTimetable));
                }

                // Verify entry is in draft status
                if (originalTimetable.Status != "Draft")
                {
                    TempData["Error"] = "Only sessions in draft status can be rescheduled.";
                    return RedirectToAction(nameof(LecturerTimetable));
                }

                // Verify the instructor is assigned to this course
                if (originalTimetable.Course.InstructorId != userId)
                {
                    TempData["Error"] = "You can only reschedule your own sessions.";
                    return RedirectToAction(nameof(LecturerTimetable));
                }

                // Verify the selected room exists and has sufficient capacity
                var room = await _context.LearningRooms.FindAsync(roomId);
                if (room == null)
                {
                    TempData["Error"] = "Selected room was not found.";
                    return RedirectToAction(nameof(LecturerTimetable));
                }

                if (room.LearningCapacity < originalTimetable.Course.CapacityRequired)
                {
                    TempData["Error"] = "Selected room does not have sufficient capacity.";
                    return RedirectToAction(nameof(LecturerTimetable));
                }

                // Get the time slot configuration for the target day
                var workingDayConfig = await _context.WorkingDayConfigurations
                    .FirstOrDefaultAsync(w => w.AcademicYearId == originalTimetable.AcademicYearId &&
                                            w.ModeOfStudyId == originalTimetable.ModeOfStudyId &&
                                            w.IsActive);

                if (workingDayConfig == null)
                {
                    TempData["Error"] = "Working day configuration not found.";
                    return RedirectToAction(nameof(LecturerTimetable));
                }

                // Parse working days data
                var workingDaysData = System.Text.Json.JsonSerializer.Deserialize<List<WorkingDayData>>(
                    workingDayConfig.WorkingDaysData,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Find the time slot config for the target day
                var targetDayConfig = workingDaysData?.FirstOrDefault(d =>
                    d.Day.Equals(day, StringComparison.OrdinalIgnoreCase) && d.IsWorkingDay);

                if (targetDayConfig == null)
                {
                    TempData["Error"] = $"Configuration for {day} not found.";
                    return RedirectToAction(nameof(LecturerTimetable));
                }

                int targetTimeSlotConfigId = int.Parse(targetDayConfig.TimeSlotConfigId);

                // Parse the target day string to DayOfWeek
                if (!Enum.TryParse<DayOfWeek>(day, true, out DayOfWeek targetDayOfWeek))
                {
                    TempData["Error"] = $"Invalid day format: {day}";
                    return RedirectToAction(nameof(LecturerTimetable));
                }

                // Start a transaction
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Get all timetable entries for this course (all recurring instances) that are in draft status
                        var allCourseEntries = await _context.Timetables
                            .Where(t => t.CourseId == originalTimetable.CourseId &&
                                       t.AcademicYearId == originalTimetable.AcademicYearId &&
                                       t.Status == "Draft") // Only reschedule draft entries
                            .ToListAsync();

                        // Store the original day of week for comparison
                        string originalDayOfWeek = originalTimetable.Date.DayOfWeek.ToString();

                        // Load the data into memory first, then filter
                        var possibleInstructorConflicts = await _context.Timetables
                            .Include(t => t.Course)
                            .Where(t => t.Course.InstructorId == userId &&
                                      t.CourseId != originalTimetable.CourseId &&
                                      t.TimeSlotConfigId == targetTimeSlotConfigId &&
                                      t.PeriodNumber == periodNumber)
                            .ToListAsync();

                        // Now filter in memory by day of week
                        bool instructorConflicts = possibleInstructorConflicts
                            .Any(t => t.Date.DayOfWeek == targetDayOfWeek);

                        if (instructorConflicts)
                        {
                            TempData["Error"] = "You already have another session scheduled for this day and period.";
                            return RedirectToAction(nameof(LecturerTimetable));
                        }

                        // Room conflicts - use the same pattern
                        var possibleRoomConflicts = await _context.Timetables
                            .Where(t => t.LearningRoomId == roomId &&
                                      t.CourseId != originalTimetable.CourseId &&
                                      t.TimeSlotConfigId == targetTimeSlotConfigId &&
                                      t.PeriodNumber == periodNumber)
                            .ToListAsync();

                        bool roomConflicts = possibleRoomConflicts
                            .Any(t => t.Date.DayOfWeek == targetDayOfWeek);

                        if (roomConflicts)
                        {
                            TempData["Error"] = "Selected room is already booked for this day and period.";
                            return RedirectToAction(nameof(LecturerTimetable));
                        }

                        // Student group conflicts - use the same pattern
                        var possibleStudentGroupConflicts = await _context.Timetables
                            .Include(t => t.Course)
                            .Where(t => t.Course.ProgrammeID == originalTimetable.Course.ProgrammeID &&
                                      t.Course.YearTaken == originalTimetable.Course.YearTaken &&
                                      t.CourseId != originalTimetable.CourseId &&
                                      t.TimeSlotConfigId == targetTimeSlotConfigId &&
                                      t.PeriodNumber == periodNumber)
                            .ToListAsync();

                        bool studentGroupConflicts = possibleStudentGroupConflicts
                            .Any(t => t.Date.DayOfWeek == targetDayOfWeek);

                        if (studentGroupConflicts)
                        {
                            TempData["Error"] = "This student group already has another session scheduled for this day and period.";
                            return RedirectToAction(nameof(LecturerTimetable));
                        }

                        // All checks passed, update timetable entries
                        int entriesUpdated = 0;

                        // Update all timetable entries for this course that match the original day of week
                        foreach (var entry in allCourseEntries.Where(e => e.Date.DayOfWeek.ToString() == originalDayOfWeek))
                        {
                            // Calculate the new date by adjusting to the target day of week
                            // Find the next occurrence of this day of week
                            var currentDate = entry.Date;
                            int daysToAdd = ((int)targetDayOfWeek - (int)currentDate.DayOfWeek + 7) % 7;

                            // If we'd land on the same day, move to next week
                            if (daysToAdd == 0)
                            {
                                daysToAdd = 7;
                            }

                            var newDate = currentDate.AddDays(daysToAdd);

                            // Store old values for tracking updates
                            var oldDate = entry.Date;
                            var oldPeriod = entry.PeriodNumber;
                            var oldRoomId = entry.LearningRoomId;
                            var oldTimeSlotConfigId = entry.TimeSlotConfigId;

                            // Update the timetable entry
                            entry.Date = newDate;
                            entry.PeriodNumber = periodNumber;
                            entry.LearningRoomId = roomId;
                            entry.TimeSlotConfigId = targetTimeSlotConfigId;
                            entry.UpdatedBy = User.Identity.Name;
                            entry.UpdatedAt = DateTime.Now;

                            entriesUpdated++;

                            // Update instructor schedule tracking
                            await UpdateScheduleTrackings(
                                userId, "Instructor", entry.CourseId,
                                oldDate, newDate,
                                oldPeriod, periodNumber,
                                oldTimeSlotConfigId, targetTimeSlotConfigId);

                            // Update room schedule tracking
                            if (oldRoomId != roomId)
                            {
                                // Delete old room tracking
                                var oldRoomTrackings = await _context.ScheduleTrackings
                                    .Where(s => s.EntityType == "LearningRoom" &&
                                              s.EntityId == oldRoomId.ToString() &&
                                              s.Date.Date == oldDate.Date &&
                                              s.TimeSlotConfigId == oldTimeSlotConfigId &&
                                              s.PeriodNumber == oldPeriod &&
                                              s.OccupiedByCourseId == entry.CourseId)
                                    .ToListAsync();

                                foreach (var tracking in oldRoomTrackings)
                                {
                                    _context.ScheduleTrackings.Remove(tracking);
                                }

                                // Create new room tracking
                                var newRoomTracking = new ScheduleTracking
                                {
                                    EntityId = roomId.ToString(),
                                    EntityType = "LearningRoom",
                                    TimeSlotConfigId = targetTimeSlotConfigId,
                                    Date = newDate,
                                    IsOccupied = true,
                                    OccupiedByCourseId = entry.CourseId,
                                    PeriodNumber = periodNumber,
                                    CreatedBy = User.Identity.Name,
                                    CreatedAt = DateTime.Now
                                };

                                _context.ScheduleTrackings.Add(newRoomTracking);
                            }
                            else
                            {
                                // Same room, just update tracking
                                await UpdateScheduleTrackings(
                                    roomId.ToString(), "LearningRoom", entry.CourseId,
                                    oldDate, newDate,
                                    oldPeriod, periodNumber,
                                    oldTimeSlotConfigId, targetTimeSlotConfigId);
                            }
                        }

                        // Save all changes
                        await _context.SaveChangesAsync();

                        // Commit transaction
                        await transaction.CommitAsync();

                        TempData["Success"] = $"Successfully rescheduled {entriesUpdated} sessions from {originalDayOfWeek} to {day}, Period {periodNumber} in {room.Name}.";
                    }
                    catch (Exception ex)
                    {
                        // Roll back transaction
                        await transaction.RollbackAsync();

                        TempData["Error"] = "Error rescheduling sessions: " + ex.Message;
                        Console.WriteLine($"Error in RescheduleSession: {ex}");
                    }
                }

                return RedirectToAction(nameof(LecturerTimetable));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                Console.WriteLine($"Unhandled error in RescheduleSession: {ex}");
                return RedirectToAction(nameof(LecturerTimetable));
            }
        }

        // Helper method to update schedule tracking records
        private async Task UpdateScheduleTrackings(
            string entityId,
            string entityType,
            int courseId,
            DateTime oldDate,
            DateTime newDate,
            int oldPeriod,
            int newPeriod,
            int oldTimeSlotConfigId,
            int newTimeSlotConfigId)
        {
            // Find existing schedule tracking records
            var existingTrackings = await _context.ScheduleTrackings
                .Where(s => s.EntityType == entityType &&
                          s.EntityId == entityId &&
                          s.Date.Date == oldDate.Date &&
                          s.TimeSlotConfigId == oldTimeSlotConfigId &&
                          s.PeriodNumber == oldPeriod &&
                          s.OccupiedByCourseId == courseId)
                .ToListAsync();

            if (existingTrackings.Any())
            {
                // Update existing tracking records
                foreach (var tracking in existingTrackings)
                {
                    tracking.Date = newDate;
                    tracking.PeriodNumber = newPeriod;
                    tracking.TimeSlotConfigId = newTimeSlotConfigId;
                    tracking.UpdatedBy = User.Identity.Name;
                    tracking.UpdatedAt = DateTime.Now;
                }
            }
            else
            {
                // Create new tracking record if none exists
                var newTracking = new ScheduleTracking
                {
                    EntityId = entityId,
                    EntityType = entityType,
                    TimeSlotConfigId = newTimeSlotConfigId,
                    Date = newDate,
                    IsOccupied = true,
                    OccupiedByCourseId = courseId,
                    PeriodNumber = newPeriod,
                    CreatedBy = User.Identity.Name,
                    CreatedAt = DateTime.Now
                };

                _context.ScheduleTrackings.Add(newTracking);
            }
        }










        // Helper method to check for instructor scheduling conflicts
        private async Task<bool> CheckInstructorConflicts(string userId, string dayOfWeek, int targetTimeSlotConfigId, int targetPeriod, int courseId, int academicYearId)
        {
            // Find instructor schedule trackings for other courses
            var otherSchedules = await _context.ScheduleTrackings
                .Where(s => s.EntityType == "Instructor" &&
                           s.EntityId == userId &&
                           (s.OccupiedByCourseId == null || s.OccupiedByCourseId != courseId))
                .ToListAsync();

            if (!otherSchedules.Any())
            {
                return false; // No other schedules, so no conflicts
            }

            // Get the course IDs
            var otherCourseIds = otherSchedules
                .Where(s => s.OccupiedByCourseId.HasValue)
                .Select(s => s.OccupiedByCourseId.Value)
                .Distinct()
                .ToList();

            // Find timetable entries for these courses
            var timetableEntries = await _context.Timetables
                .Where(t => otherCourseIds.Contains(t.CourseId) &&
                           t.AcademicYearId == academicYearId)
                .ToListAsync();

            // Check if any entry conflicts with the target day/period
            return timetableEntries.Any(t =>
                t.Date.DayOfWeek.ToString() == dayOfWeek &&
                t.TimeSlotConfigId == targetTimeSlotConfigId &&
                t.PeriodNumber == targetPeriod);
        }
        // Helper method to check for room scheduling conflicts
        private async Task<bool> CheckRoomConflicts(int roomId, string dayOfWeek, int targetTimeSlotConfigId, int targetPeriod, int courseId, int academicYearId)
        {
            // Find room schedule trackings for other courses
            var otherSchedules = await _context.ScheduleTrackings
                .Where(s => s.EntityType == "LearningRoom" &&
                           s.EntityId == roomId.ToString() &&
                           (s.OccupiedByCourseId == null || s.OccupiedByCourseId != courseId))
                .ToListAsync();

            if (!otherSchedules.Any())
            {
                return false; // No other schedules, so no conflicts
            }

            // Get the course IDs
            var otherCourseIds = otherSchedules
                .Where(s => s.OccupiedByCourseId.HasValue)
                .Select(s => s.OccupiedByCourseId.Value)
                .Distinct()
                .ToList();

            // Find timetable entries for these courses
            var timetableEntries = await _context.Timetables
                .Where(t => otherCourseIds.Contains(t.CourseId) &&
                           t.AcademicYearId == academicYearId)
                .ToListAsync();

            // Check if any entry conflicts with the target day/period
            return timetableEntries.Any(t =>
                t.Date.DayOfWeek.ToString() == dayOfWeek &&
                t.TimeSlotConfigId == targetTimeSlotConfigId &&
                t.PeriodNumber == targetPeriod);
        }

        // Helper method to check for student group scheduling conflicts
        private async Task<bool> CheckStudentGroupConflicts(int programmeId, int yearTaken, string dayOfWeek, int targetTimeSlotConfigId, int targetPeriod, int courseId, int academicYearId)
        {
            // Get all courses for this programme and year (excluding the current course)
            var coursesInSameGroup = await _context.Courses
                .Where(c => c.ProgrammeID == programmeId && c.YearTaken == yearTaken && c.Id != courseId)
                .Select(c => c.Id)
                .ToListAsync();

            if (!coursesInSameGroup.Any())
            {
                return false; // No other courses for this group, so no conflicts
            }

            // Find any timetable entries for these courses that would conflict
            var conflictingEntries = await _context.Timetables
                .Where(t => coursesInSameGroup.Contains(t.CourseId) &&
                           t.Date.DayOfWeek.ToString() == dayOfWeek &&
                           t.TimeSlotConfigId == targetTimeSlotConfigId &&
                           t.PeriodNumber == targetPeriod &&
                           t.AcademicYearId == academicYearId)
                .AnyAsync();

            return conflictingEntries;
        }

        // Helper method to update instructor schedule
        private async Task UpdateInstructorSchedule(
            string userId,
            Timetable timetableEntry,
            int courseId,
            DateTime oldDate,
            int oldPeriod,
            int oldTimeSlotConfigId)
        {
            // Find existing instructor schedule - we need to use simpler query that EF Core can translate
            var instructorTracking = await _context.ScheduleTrackings
                .Where(s => s.EntityType == "Instructor" &&
                          s.EntityId == userId &&
                          s.OccupiedByCourseId == courseId &&
                          s.TimeSlotConfigId == oldTimeSlotConfigId &&
                          s.PeriodNumber == oldPeriod)
                .ToListAsync();

            // Filter in memory to match the exact date
            var exactTracking = instructorTracking
                .FirstOrDefault(s => s.Date.Date == oldDate.Date);

            if (exactTracking != null)
            {
                // Update existing tracking
                exactTracking.Date = timetableEntry.Date;
                exactTracking.PeriodNumber = timetableEntry.PeriodNumber;
                exactTracking.TimeSlotConfigId = timetableEntry.TimeSlotConfigId;
                exactTracking.UpdatedBy = User.Identity.Name;
                exactTracking.UpdatedAt = DateTime.Now;
            }
            else
            {
                // Create new instructor tracking if none existed
                var newInstructorTracking = new ScheduleTracking
                {
                    EntityId = userId,
                    EntityType = "Instructor",
                    TimeSlotConfigId = timetableEntry.TimeSlotConfigId,
                    Date = timetableEntry.Date,
                    IsOccupied = true,
                    OccupiedByCourseId = timetableEntry.CourseId,
                    PeriodNumber = timetableEntry.PeriodNumber,
                    CreatedBy = User.Identity.Name,
                    CreatedAt = DateTime.Now
                };

                _context.ScheduleTrackings.Add(newInstructorTracking);
            }
        }

        // Helper method to update room schedule
        private async Task UpdateRoomSchedule(
            Timetable timetableEntry,
            int newRoomId,
            int oldRoomId,
            DateTime newDate,
            DateTime oldDate,
            int newPeriod,
            int oldPeriod,
            int newTimeSlotConfigId,
            int oldTimeSlotConfigId)
        {
            // Find existing room schedule for the old slot - use simpler query that EF Core can translate
            var roomTrackings = await _context.ScheduleTrackings
                .Where(s => s.EntityType == "LearningRoom" &&
                          s.EntityId == oldRoomId.ToString() &&
                          s.OccupiedByCourseId == timetableEntry.CourseId &&
                          s.TimeSlotConfigId == oldTimeSlotConfigId &&
                          s.PeriodNumber == oldPeriod)
                .ToListAsync();

            // Filter in memory to match the exact date
            var oldRoomTracking = roomTrackings
                .FirstOrDefault(s => s.Date.Date == oldDate.Date);

            // If room has changed or date/period has changed, handle the different cases
            if (oldRoomTracking != null)
            {
                if (oldRoomId != newRoomId)
                {
                    // Delete old room tracking and create new one for different room
                    _context.ScheduleTrackings.Remove(oldRoomTracking);

                    // Create new room tracking
                    var newRoomTracking = new ScheduleTracking
                    {
                        EntityId = newRoomId.ToString(),
                        EntityType = "LearningRoom",
                        TimeSlotConfigId = newTimeSlotConfigId,
                        Date = newDate,
                        IsOccupied = true,
                        OccupiedByCourseId = timetableEntry.CourseId,
                        PeriodNumber = newPeriod,
                        CreatedBy = User.Identity.Name,
                        CreatedAt = DateTime.Now
                    };

                    _context.ScheduleTrackings.Add(newRoomTracking);
                }
                else
                {
                    // Same room, just update the tracking with new date/period
                    oldRoomTracking.Date = newDate;
                    oldRoomTracking.PeriodNumber = newPeriod;
                    oldRoomTracking.TimeSlotConfigId = newTimeSlotConfigId;
                    oldRoomTracking.UpdatedBy = User.Identity.Name;
                    oldRoomTracking.UpdatedAt = DateTime.Now;
                }
            }
            else
            {
                // Create new room tracking if none existed
                var newRoomTracking = new ScheduleTracking
                {
                    EntityId = newRoomId.ToString(),
                    EntityType = "LearningRoom",
                    TimeSlotConfigId = newTimeSlotConfigId,
                    Date = newDate,
                    IsOccupied = true,
                    OccupiedByCourseId = timetableEntry.CourseId,
                    PeriodNumber = newPeriod,
                    CreatedBy = User.Identity.Name,
                    CreatedAt = DateTime.Now
                };

                _context.ScheduleTrackings.Add(newRoomTracking);
            }
        }








        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StudentTimetable()
        {
            // Get the current student's ID
            var userName = User.Identity.Name;
            var student = await _context.Students
                .Include(s => s.Programme)
                .Include(s => s.ModeOfStudy)
                .Include(s => s.AcademicYear)
                .FirstOrDefaultAsync(s => s.Username == userName);

            if (student == null)
            {
                return NotFound("Student record not found.");
            }

            // Get student's registered courses for current semester
            var registeredCourses = await _context.StudentCourseRegistrations
                .Include(r => r.Course)
                .Where(r => r.StudentId == student.Id &&
                           r.AcademicYearId == student.AcademicYearId &&
                           r.YearPeriodId == student.CurrentYearPeriodId)
                .Select(r => r.CourseId)
                .ToListAsync();

            // Get timetable entries for registered courses
            var timetableEntries = await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.LearningRoom)
                .Include(t => t.AcademicYear)
                .Include(t => t.ModeOfStudy)
                .Where(t => registeredCourses.Contains(t.CourseId) &&
                           t.AcademicYearId == student.AcademicYearId &&
                           t.Status == "Published") // Only show published timetables
                .OrderBy(t => t.Date)
                .ThenBy(t => t.PeriodNumber)
                .ToListAsync();

            // Get all working days from the timetable
            var workingDays = timetableEntries
                .Select(t => t.Date.DayOfWeek.ToString())
                .Distinct()
                .OrderBy(d => Enum.Parse<DayOfWeek>(d))
                .ToList();

            // Get all periods used in the timetable
            var periodNumbers = timetableEntries
                .Select(t => t.PeriodNumber)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // Get periods data from the time slot configurations
            var timeSlotConfigIds = timetableEntries.Select(t => t.TimeSlotConfigId).Distinct().ToList();
            var timeSlotConfigs = await _context.TimeSlotConfigurations
                .Where(t => timeSlotConfigIds.Contains(t.Id))
                .ToListAsync();

            // Create a list of period view models
            List<PeriodViewModel> periods = new List<PeriodViewModel>();
            foreach (var config in timeSlotConfigs)
            {
                if (!string.IsNullOrEmpty(config.PeriodsData))
                {
                    var periodsData = System.Text.Json.JsonSerializer.Deserialize<List<PeriodData>>(
                        config.PeriodsData,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (periodsData != null)
                    {
                        foreach (var periodData in periodsData.Where(p => periodNumbers.Contains(p.PeriodNumber)))
                        {
                            if (!periods.Any(p => p.PeriodNumber == periodData.PeriodNumber))
                            {
                                periods.Add(new PeriodViewModel
                                {
                                    PeriodNumber = periodData.PeriodNumber,
                                    StartTime = periodData.StartTime,
                                    EndTime = periodData.EndTime,
                                    Type = periodData.Type
                                });
                            }
                        }
                    }
                }
            }

            // Sort the periods by period number
            periods = periods.OrderBy(p => p.PeriodNumber).ToList();

            // Get instructor information for each course - CORRECTED VERSION
            var courseWithInstructors = await _context.Courses
                .Where(c => registeredCourses.Contains(c.Id))
                .Select(c => new { c.Id, c.InstructorId })
                .ToListAsync();

            var instructorIds = courseWithInstructors
                .Where(c => c.InstructorId != null)
                .Select(c => c.InstructorId)
                .Distinct()
                .ToList();

            var instructors = await _context.Users
                .Where(u => instructorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email);

            var courseInstructors = courseWithInstructors
                .ToDictionary(
                    c => c.Id,
                    c => c.InstructorId != null ? instructors.GetValueOrDefault(c.InstructorId, "Unknown") : "Not Assigned"
                );

            // Convert timetable entries to session view models
            var sessionViewModels = timetableEntries.Select(entry => new StudentTimetableSessionViewModel
            {
                TimetableId = entry.Id,
                CourseId = entry.CourseId,
                CourseCode = entry.Course.CourseCode,
                CourseName = entry.Course.CourseName,
                InstructorName = courseInstructors.GetValueOrDefault(entry.CourseId, "Unknown"),
                RoomName = entry.LearningRoom?.Name ?? "Online/Virtual",
                PeriodNumber = entry.PeriodNumber,
                Date = entry.Date,
                DayOfWeek = entry.Date.DayOfWeek,
                Status = entry.Status
            }).ToList();

            // Create a weekly timetable structure
            var weeklyTimetable = new Dictionary<string, Dictionary<int, List<StudentTimetableSessionViewModel>>>();

            // Initialize the structure for all working days and periods
            foreach (var day in workingDays)
            {
                weeklyTimetable[day] = new Dictionary<int, List<StudentTimetableSessionViewModel>>();
                foreach (var periodNumber in periodNumbers)
                {
                    weeklyTimetable[day][periodNumber] = new List<StudentTimetableSessionViewModel>();
                }
            }

            // Group sessions by day of week and period
            foreach (var session in sessionViewModels)
            {
                string dayName = session.DayOfWeek.ToString();

                if (weeklyTimetable.ContainsKey(dayName) &&
                    weeklyTimetable[dayName].ContainsKey(session.PeriodNumber))
                {
                    weeklyTimetable[dayName][session.PeriodNumber].Add(session);
                }
            }

            // Create the view model
            var viewModel = new StudentTimetableViewModel
            {
                Student = student,
                WorkingDays = workingDays,
                Periods = periods,
                WeeklyTimetable = weeklyTimetable,
                AcademicYearValue = student.AcademicYear.YearValue,
                ModeOfStudyName = student.ModeOfStudy.ModeName,
                Period = $"Period {student.CurrentYearPeriodLabel}"
            };

            return View(viewModel);
        }




    }





}