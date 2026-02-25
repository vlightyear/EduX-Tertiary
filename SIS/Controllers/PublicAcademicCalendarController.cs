using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Accounts;
using SIS.Models.StudentApplication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    [Authorize]
    public class PublicAcademicCalendarController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PublicAcademicCalendarController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /AcademicCalendar
        public async Task<IActionResult> Index()
        {
            // Get current and available academic years
            var academicYears = await _context.AcademicYears
                .OrderByDescending(y => y.StartDate)
                .Select(y => new SelectListItem
                {
                    Value = y.YearId.ToString(),
                    Text = y.YearValue,
                    Selected = y.IsActive
                })
                .ToListAsync();

            // Get current active academic year
            var currentAcademicYear = await _context.AcademicYears
                .FirstOrDefaultAsync(a => a.IsActive);

            if (currentAcademicYear == null && academicYears.Any())
            {
                // If no active year, select the most recent one
                currentAcademicYear = await _context.AcademicYears
                    .OrderByDescending(y => y.StartDate)
                    .FirstOrDefaultAsync();
            }

            // Prepare filter data
            ViewBag.AcademicYears = academicYears;
            ViewBag.CurrentAcademicYear = currentAcademicYear;
            ViewBag.CurrentAcademicYearId = currentAcademicYear?.YearId ?? 0;

            ViewBag.Schools = await _context.Schools
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToListAsync();

            ViewBag.ProgrammeLevels = await _context.ProgramLevels
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .ToListAsync();

            ViewBag.ModesOfStudy = await _context.ModesOfStudy
                .OrderBy(m => m.ModeName)
                .Select(m => new SelectListItem { Value = m.ModeId.ToString(), Text = m.ModeName })
                .ToListAsync();

            // Get user details
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            // Add student-specific info if the user is a student
            if (roles.Contains("Student"))
            {
                var student = await _context.Students
                    .Include(s => s.School)
                    .Include(s => s.Programme)
                    .Include(s => s.ProgrammeLevel)
                    .Include(s => s.ModeOfStudy)
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student != null)
                {
                    ViewBag.StudentInfo = new
                    {
                        SchoolId = student.SchoolId,
                        SchoolName = student.School?.Name,
                        ProgrammeId = student.ProgrammeId,
                        ProgrammeName = student.Programme?.Name,
                        ProgrammeLevelId = student.ProgrammeLevelId,
                        ProgrammeLevelName = student.ProgrammeLevel?.Name,
                        ModeOfStudyId = student.ModeOfStudyId,
                        ModeOfStudyName = student.ModeOfStudy?.ModeName,
                        StudentYear = student.StudentCurrentYear,
                        Semester = student.CurrentSemester
                    };
                }
            }

            // Return the view
            return View();
        }

        // GET: /AcademicCalendar/GetEvents
        [HttpGet]
        public async Task<JsonResult> GetEvents(
            int academicYearId,
            DateTime start,
            DateTime end,
            int? schoolId = null,
            int? programmeId = null,
            int? programmeLevelId = null,
            int? modeOfStudyId = null,
            int? studentYear = null,
            int? semester = null)
        {
            // Check if academicYearId is valid, or get the current one
            if (academicYearId <= 0)
            {
                var currentYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(a => a.IsActive);

                if (currentYear != null)
                {
                    academicYearId = currentYear.YearId;
                }
                else
                {
                    // No valid academic year
                    return Json(new object[0]);
                }
            }

            // Get user details
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            // Base query for events
            var query = _context.AcademicCalendarEvents
                .Include(e => e.EventType)
                .Where(e => e.AcademicYearId == academicYearId)
                .Where(e => e.IsPublished) // Only show published events
                .Where(e => e.StartDateTime <= end &&
                           (e.EndDateTime ?? e.StartDateTime.AddDays(e.IsAllDay ? 1 : 0)) >= start);

            // Apply filters based on user selection
            if (schoolId.HasValue && schoolId.Value > 0)
                query = query.Where(e => e.SchoolId == null || e.SchoolId == schoolId.Value);

            if (programmeId.HasValue && programmeId.Value > 0)
                query = query.Where(e => e.ProgrammeId == null || e.ProgrammeId == programmeId.Value);

            if (programmeLevelId.HasValue && programmeLevelId.Value > 0)
                query = query.Where(e => e.ProgrammeLevelId == null || e.ProgrammeLevelId == programmeLevelId.Value);

            if (modeOfStudyId.HasValue && modeOfStudyId.Value > 0)
                query = query.Where(e => e.ModeOfStudyId == null || e.ModeOfStudyId == modeOfStudyId.Value);

            if (studentYear.HasValue && studentYear.Value > 0)
                query = query.Where(e => e.StudentYear == null || e.StudentYear == studentYear.Value);

            if (semester.HasValue && semester.Value > 0)
                query = query.Where(e => e.Semester == null || e.Semester == semester.Value);

            // Additional filters for students
            if (roles.Contains("Student"))
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Username == user.UserName);

                if (student != null && !schoolId.HasValue && !programmeId.HasValue &&
                    !programmeLevelId.HasValue && !modeOfStudyId.HasValue &&
                    !studentYear.HasValue && !semester.HasValue)
                {
                    // If no filters applied by user, apply student-specific filters
                    query = query.Where(e =>
                        (e.SchoolId == null || e.SchoolId == student.SchoolId) &&
                        (e.ProgrammeId == null || e.ProgrammeId == student.ProgrammeId) &&
                        (e.ProgrammeLevelId == null || e.ProgrammeLevelId == student.ProgrammeLevelId) &&
                        (e.ModeOfStudyId == null || e.ModeOfStudyId == student.ModeOfStudyId) &&
                        (e.StudentYear == null || e.StudentYear == student.StudentCurrentYear) &&
                        (e.Semester == null || e.Semester == student.CurrentSemester)
                    );
                }
            }

            // Transform to calendar event format
            var events = await query
                .Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    description = e.Description,
                    start = e.StartDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = e.EndDateTime.HasValue ?
                        e.EndDateTime.Value.ToString("yyyy-MM-ddTHH:mm:ss") :
                        e.IsAllDay ? e.StartDateTime.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss") : null,
                    allDay = e.IsAllDay,
                    color = e.Color,
                    textColor = "#ffffff", // White text for better contrast
                    eventType = e.EventType.Name,
                    icon = e.EventType.IconName,
                    location = e.Location,
                    contactPerson = e.ContactPerson,
                    contactEmail = e.ContactEmail,
                    isSystemEvent = e.IsSystemEvent
                })
                .ToListAsync();

            return Json(events);
        }

        // GET: /PublicAcademicCalendar/GetEventDetails/5
        [HttpGet]
        public async Task<JsonResult> GetEventDetails(int id)
        {
            var calendarEvent = await _context.AcademicCalendarEvents
                .Include(e => e.EventType)
                .Include(e => e.School)
                .Include(e => e.Programme)
                .Include(e => e.ProgrammeLevel)
                .Include(e => e.ModeOfStudy)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (calendarEvent == null)
            {
                return Json(new { success = false, message = "Event not found" });
            }

            var eventDetails = new
            {
                id = calendarEvent.Id,
                title = calendarEvent.Title,
                description = calendarEvent.Description,
                startDateTime = calendarEvent.StartDateTime,
                endDateTime = calendarEvent.EndDateTime,
                isAllDay = calendarEvent.IsAllDay,
                color = calendarEvent.Color,
                eventType = calendarEvent.EventType?.Name,
                icon = calendarEvent.EventType?.IconName,
                location = calendarEvent.Location,
                contactPerson = calendarEvent.ContactPerson,
                contactEmail = calendarEvent.ContactEmail,
                school = calendarEvent.School?.Name,
                programme = calendarEvent.Programme?.Name,
                programmeLevel = calendarEvent.ProgrammeLevel?.Name,
                modeOfStudy = calendarEvent.ModeOfStudy?.ModeName,
                studentYear = calendarEvent.StudentYear,
                semester = calendarEvent.Semester,
                isSystemEvent = calendarEvent.IsSystemEvent
            };

            return Json(new { success = true, eventData = eventDetails });
        }

        // GET: /PublicAcademicCalendar/GetProgrammes/5
        [HttpGet]
        public async Task<JsonResult> GetProgrammes(int schoolId)
        {
            // First get all departments in the school
            var departmentIds = await _context.Departments
                .Where(d => d.SchoolId == schoolId && d.IsActive)
                .Select(d => d.Id)
                .ToListAsync();

            // Then get all programmes in those departments
            var programmes = await _context.Programmes
                .Where(p => departmentIds.Contains(p.DepartmentId))
                .OrderBy(p => p.Name)
                .Select(p => new { id = p.Id, name = p.Name })
                .ToListAsync();

            return Json(programmes);
        }
    }
}
