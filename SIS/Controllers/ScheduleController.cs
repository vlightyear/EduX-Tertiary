using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.TimeTabling;
using SIS.Services.TimeTabling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SIS.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var generator = new ScheduleGenerator();
            var config = _context.TimeSlotConfigurations
                .OrderBy(tsc => tsc.Id)
                .FirstOrDefault(c => c.IsActive);

            if (config == null)
            {
                return BadRequest("No active time slot configuration found");
            }

            var rooms = _context.LearningRooms.Where(r => r.IsActive).ToList();
            if (!rooms.Any())
            {
                return BadRequest("No active rooms found");
            }

            var courseLecturers = _context.CourseLecturer
                .Include(cl => cl.Course)
                //.Take(10)
                .ToList();

            var courseIds = courseLecturers.Select(cl => cl.CourseId).ToList();

            // Load student enrollments for conflict detection
            // Get current academic year and semester (adjust this logic as needed)
            var currentAcademicYear = _context.AcademicYears
                .OrderByDescending(ay => ay.YearId == 3)
                .FirstOrDefault(ay => ay.IsActive);

            int currentSemester = 2; // You might want to determine this dynamically

            Dictionary<int, HashSet<int>> courseToStudents = new Dictionary<int, HashSet<int>>();

            if (currentAcademicYear != null)
            {
                // Query student registrations for the courses being scheduled
                var registrations = _context.StudentCourseRegistrations
                    .Where(scr => courseIds.Contains(scr.CourseId) &&
                                  scr.AcademicYearId == currentAcademicYear.YearId &&
                                  scr.Semester == currentSemester)
                    .Select(scr => new { scr.CourseId, scr.StudentId })
                    .AsNoTracking()
                    .ToList();

                // Group by course and create HashSet of student IDs for fast lookups
                courseToStudents = registrations
                    .GroupBy(r => r.CourseId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(r => r.StudentId).ToHashSet()
                    );

                Console.WriteLine($"=== Student Enrollment Data ===");
                Console.WriteLine($"Academic Year: {currentAcademicYear.YearName}, Semester: {currentSemester}");
                Console.WriteLine($"Loaded enrollments for {courseToStudents.Count} courses");
                foreach (var kvp in courseToStudents.OrderBy(x => x.Key))
                {
                    Console.WriteLine($"  Course {kvp.Key}: {kvp.Value.Count} students enrolled");
                }
            }
            else
            {
                Console.WriteLine("Warning: No active academic year found. Student conflict checking disabled.");
            }
            ViewBag.Somevaribale = 1;
            ViewData["Somevaribale"] = 2;

            // Generate multiple sessions per course based on MeetingFrequencyPerWeek
            var sessions = courseLecturers.SelectMany(cl =>
            {
                int meetingsPerWeek = cl.Course.MeetingFrequencyPerWeek > 0
                    ? cl.Course.MeetingFrequencyPerWeek
                    : 1;

                return Enumerable.Range(0, meetingsPerWeek)
                    .Select(sessionIndex => new CourseSession
                    {
                        CourseId = cl.CourseId,
                        LecturerId = cl.LecturerId,
                        SessionType = "Lecture",
                        DurationPeriods = 1,
                        MeetingFrequencyPerWeek = meetingsPerWeek,
                        PossibleRoomIds = rooms.Select(r => r.Id).ToList()
                    });
            }).ToList();

            Console.WriteLine($"\n=== Session Generation ===");
            Console.WriteLine($"Generated {sessions.Count} sessions for {courseLecturers.Count} courses");

            // Generate schedule with student conflict prevention
            var assigned = generator.GenerateSchedule(sessions, rooms, config, courseToStudents);

            if (!assigned.Any())
            {
                Console.WriteLine("No feasible schedule found. Check constraints.");
                return Ok("No schedule could be generated. Check solver output.");
            }

            // Output results
            var periods = JsonConvert.DeserializeObject<List<dynamic>>(config.PeriodsData);
            var groupedByCourse = assigned.GroupBy(a => a.Session.CourseId);

            // Count virtual vs physical room usage
            var virtualRoomIds = rooms.Where(r => r.RoomType?.ToLower() == "virtual")
                                     .Select(r => r.Id)
                                     .ToHashSet();
            int virtualCount = assigned.Count(a => virtualRoomIds.Contains(a.AssignedRoomId));

            Console.WriteLine($"\n=== Schedule Summary ===");
            Console.WriteLine($"Total sessions: {assigned.Count}");
            Console.WriteLine($"Physical room sessions: {assigned.Count - virtualCount}");
            Console.WriteLine($"Virtual room sessions: {virtualCount}");
            Console.WriteLine($"Courses scheduled: {groupedByCourse.Count()}");

            // Detect and report any student conflicts (verification)
            VerifyNoStudentConflicts(assigned, courseToStudents, periods);

            Console.WriteLine($"\n=== Detailed Schedule ===");
            foreach (var courseGroup in groupedByCourse)
            {
                var courseId = courseGroup.Key;
                var studentCount = courseToStudents.ContainsKey(courseId)
                    ? courseToStudents[courseId].Count
                    : 0;

                Console.WriteLine($"\n=== Course {courseId} ({studentCount} students) ===");
                foreach (var a in courseGroup.OrderBy(x => x.AssignedDay).ThenBy(x => x.AssignedPeriod))
                {
                    var period = periods[a.AssignedPeriod];
                    var room = rooms.FirstOrDefault(r => r.Id == a.AssignedRoomId);
                    var roomType = room?.RoomType?.ToLower() == "virtual" ? "[VIRTUAL]" : "[Physical]";

                    Console.WriteLine($"  {a.AssignedDay} {period.startTime}-{period.endTime} | " +
                                    $"Room {a.AssignedRoomId} {roomType} | Lecturer {a.Session.LecturerId}");
                }
            }

            return Ok($"Successfully scheduled {assigned.Count} sessions for {groupedByCourse.Count()} courses");
        }

        private void VerifyNoStudentConflicts(
            List<AssignedSession> assigned,
            Dictionary<int, HashSet<int>> courseToStudents,
            List<dynamic> periods)
        {
            Console.WriteLine("\n=== Verifying Student Conflicts ===");
            int conflictCount = 0;

            for (int i = 0; i < assigned.Count; i++)
            {
                for (int j = i + 1; j < assigned.Count; j++)
                {
                    var sessionA = assigned[i];
                    var sessionB = assigned[j];

                    // Skip if different days
                    if (sessionA.AssignedDay != sessionB.AssignedDay)
                        continue;

                    // Check for time overlap
                    int startA = sessionA.AssignedPeriod;
                    int endA = startA + sessionA.Session.DurationPeriods;
                    int startB = sessionB.AssignedPeriod;
                    int endB = startB + sessionB.Session.DurationPeriods;

                    bool timeOverlap = !(endA <= startB || endB <= startA);
                    if (!timeOverlap)
                        continue;

                    // Check for shared students
                    if (courseToStudents.ContainsKey(sessionA.Session.CourseId) &&
                        courseToStudents.ContainsKey(sessionB.Session.CourseId))
                    {
                        var studentsA = courseToStudents[sessionA.Session.CourseId];
                        var studentsB = courseToStudents[sessionB.Session.CourseId];
                        var sharedStudents = studentsA.Intersect(studentsB).ToList();

                        if (sharedStudents.Any())
                        {
                            conflictCount++;
                            var periodA = periods[sessionA.AssignedPeriod];
                            var periodB = periods[sessionB.AssignedPeriod];

                            Console.WriteLine($"CONFLICT FOUND:");
                            Console.WriteLine($"  Course {sessionA.Session.CourseId} on {sessionA.AssignedDay} at {periodA.startTime}");
                            Console.WriteLine($"  Course {sessionB.Session.CourseId} on {sessionB.AssignedDay} at {periodB.startTime}");
                            Console.WriteLine($"  Shared students: {sharedStudents.Count}");
                        }
                    }
                }
            }

            if (conflictCount == 0)
            {
                Console.WriteLine("✓ No student conflicts detected - all students have valid schedules!");
            }
            else
            {
                Console.WriteLine($"✗ WARNING: Found {conflictCount} student conflicts!");
            }
        }
    }
}