using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using System.ComponentModel.DataAnnotations;
using SIS.Services;
using SIS.Services.PDF;
using Newtonsoft.Json.Linq;
using SIS.DTOs;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin,Registrar,Academic Officer")]
    public class AcademicCalendarController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AcademicCalendarService _calendarService;

        public AcademicCalendarController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AcademicCalendarService calendarService)
        {
            _context = context;
            _userManager = userManager;
            _calendarService = calendarService;
        }

        // GET: Admin/AcademicCalendar
        public async Task<IActionResult> Index()
        {
            // Get current and available academic years
            var academicYears = await _context.AcademicYears
                .Where(ay => ay.IsActive)
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

            ViewBag.EventTypes = await _context.AcademicEventTypes
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = t.Name })
                .ToListAsync();

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

            // Return the view
            return View();
        }

        // GET: Admin/AcademicCalendar/GetEvents
        [HttpGet]
        public async Task<JsonResult> GetEvents(
            int academicYearId,
            DateTime start,
            DateTime end,
            bool includeUnpublished = true)
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

            // Base query for events
            var query = _context.AcademicCalendarEvents
                .Include(e => e.EventType)
                .Where(e => e.AcademicYearId == academicYearId)
                .Where(e => includeUnpublished || e.IsPublished) // Conditionally include unpublished events
                .Where(e => e.StartDateTime <= end &&
                           (e.EndDateTime ?? e.StartDateTime.AddDays(e.IsAllDay ? 1 : 0)) >= start);

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
                    borderColor = e.IsPublished ? null : "#999999", // Gray border for unpublished
                    textColor = "#ffffff", // White text for better contrast
                    eventType = e.EventType.Name,
                    icon = e.EventType.IconName,
                    location = e.Location,
                    isSystemEvent = e.IsSystemEvent,
                    isPublished = e.IsPublished,
                    // Include additional filtering fields to allow event editing
                    schoolId = e.SchoolId,
                    programmeId = e.ProgrammeId,
                    programmeLevelId = e.ProgrammeLevelId,
                    modeOfStudyId = e.ModeOfStudyId,
                    studentYear = e.StudentYear,
                    semester = e.Semester,
                    eventTypeId = e.EventTypeId,
                    // Add a class for unpublished events
                    className = e.IsPublished ? "" : "unpublished-event"
                })
                .ToListAsync();

            return Json(events);
        }

        // GET: Admin/AcademicCalendar/GetEventDetails/5
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
                eventTypeId = calendarEvent.EventTypeId,
                eventType = calendarEvent.EventType?.Name,
                icon = calendarEvent.EventType?.IconName,
                location = calendarEvent.Location,
                contactPerson = calendarEvent.ContactPerson,
                contactEmail = calendarEvent.ContactEmail,
                schoolId = calendarEvent.SchoolId,
                school = calendarEvent.School?.Name,
                programmeId = calendarEvent.ProgrammeId,
                programme = calendarEvent.Programme?.Name,
                programmeLevelId = calendarEvent.ProgrammeLevelId,
                programmeLevel = calendarEvent.ProgrammeLevel?.Name,
                modeOfStudyId = calendarEvent.ModeOfStudyId,
                modeOfStudy = calendarEvent.ModeOfStudy?.ModeName,
                studentYear = calendarEvent.StudentYear,
                semester = calendarEvent.Semester,
                isSystemEvent = calendarEvent.IsSystemEvent,
                isPublished = calendarEvent.IsPublished,
                academicYearId = calendarEvent.AcademicYearId
            };

            return Json(new { success = true, eventData = eventDetails });
        }

        // POST: Admin/AcademicCalendar/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> CreateEvent([FromBody] JObject data)
        {
            try
            {
                if (data == null)
                {
                    return Json(new { success = false, message = "No data received" });
                }

                Console.WriteLine($"Received data: {data}");
                var user = await _userManager.GetUserAsync(User);
                if(user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }
                // Extract values from JObject
                var model = new AcademicCalendarEvent
                {
                    Title = data["Title"]?.ToString(),
                    Description = data["Description"]?.ToString(),
                    IsAllDay = data["IsAllDay"]?.ToObject<bool>() ?? false,
                    AcademicYearId = data["AcademicYearId"]?.ToObject<int>() ?? 0,
                    EventTypeId = data["EventTypeId"]?.ToObject<int>() ?? 0,
                    Color = data["Color"]?.ToString(),
                    Location = data["Location"]?.ToString(),
                    ContactPerson = data["ContactPerson"]?.ToString(),
                    ContactEmail = data["ContactEmail"]?.ToString(),
                    SchoolId = data["SchoolId"]?.ToObject<int?>(),
                    ProgrammeId = data["ProgrammeId"]?.ToObject<int?>(),
                    ProgrammeLevelId = data["ProgrammeLevelId"]?.ToObject<int?>(),
                    ModeOfStudyId = data["ModeOfStudyId"]?.ToObject<int?>(),
                    StudentYear = data["StudentYear"]?.ToObject<int?>(),
                    Semester = data["Semester"]?.ToObject<int?>(),
                    IsPublished = data["IsPublished"]?.ToObject<bool>() ?? true,
                    IsSystemEvent = data["IsSystemEvent"]?.ToObject<bool>() ?? false,
                    CreatedAt = DateTime.Now,
                    CreatedBy = user?.UserName ?? "System" // Default value, will be updated later
                };

                // Parse dates
                string startDateStr = data["StartDateTime"]?.ToString();
                if (!string.IsNullOrEmpty(startDateStr))
                {
                    model.StartDateTime = DateTime.Parse(startDateStr);
                }

                string endDateStr = data["EndDateTime"]?.ToString();
                if (!string.IsNullOrEmpty(endDateStr))
                {
                    model.EndDateTime = DateTime.Parse(endDateStr);
                }

                // Validation
                if (string.IsNullOrEmpty(model.Title))
                {
                    return Json(new { success = false, message = "Title is required" });
                }

                if (model.AcademicYearId <= 0)
                {
                    return Json(new { success = false, message = "Academic Year is required" });
                }

                if (model.EventTypeId <= 0)
                {
                    return Json(new { success = false, message = "Event Type is required" });
                }

                // Set default color if needed
                if (string.IsNullOrEmpty(model.Color))
                {
                    var eventType = await _context.AcademicEventTypes.FindAsync(model.EventTypeId);
                    model.Color = eventType?.DefaultColor ?? "#3788d8";
                }


                // Save to database
                _context.Add(model);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = model.Id, message = "Event created successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating event: {ex}");
                return Json(new { success = false, message = $"Error creating event: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateEvent([FromBody] JObject data)
        {
            try
            {
                if (data == null)
                {
                    return Json(new { success = false, message = "No data received" });
                }

                int id = data["Id"]?.ToObject<int>() ?? 0;
                if (id <= 0)
                {
                    return Json(new { success = false, message = "Invalid ID" });
                }

                // Find existing event
                var existingEvent = await _context.AcademicCalendarEvents.FindAsync(id);
                if (existingEvent == null)
                {
                    return Json(new { success = false, message = "Event not found" });
                }

                // Don't update system events
                if (existingEvent.IsSystemEvent)
                {
                    return Json(new { success = false, message = "System events cannot be modified" });
                }

                // Update properties
                existingEvent.Title = data["Title"]?.ToString() ?? existingEvent.Title;
                existingEvent.Description = data["Description"]?.ToString();
                existingEvent.IsAllDay = data["IsAllDay"]?.ToObject<bool>() ?? existingEvent.IsAllDay;
                existingEvent.EventTypeId = data["EventTypeId"]?.ToObject<int>() ?? existingEvent.EventTypeId;
                existingEvent.Color = data["Color"]?.ToString() ?? existingEvent.Color;
                existingEvent.Location = data["Location"]?.ToString();
                existingEvent.ContactPerson = data["ContactPerson"]?.ToString();
                existingEvent.ContactEmail = data["ContactEmail"]?.ToString();
                existingEvent.SchoolId = data["SchoolId"]?.ToObject<int?>();
                existingEvent.ProgrammeId = data["ProgrammeId"]?.ToObject<int?>();
                existingEvent.ProgrammeLevelId = data["ProgrammeLevelId"]?.ToObject<int?>();
                existingEvent.ModeOfStudyId = data["ModeOfStudyId"]?.ToObject<int?>();
                existingEvent.StudentYear = data["StudentYear"]?.ToObject<int?>();
                existingEvent.Semester = data["Semester"]?.ToObject<int?>();
                existingEvent.IsPublished = data["IsPublished"]?.ToObject<bool>() ?? existingEvent.IsPublished;

                // Parse dates
                string startDateStr = data["StartDateTime"]?.ToString();
                if (!string.IsNullOrEmpty(startDateStr))
                {
                    existingEvent.StartDateTime = DateTime.Parse(startDateStr);
                }

                string endDateStr = data["EndDateTime"]?.ToString();
                if (!string.IsNullOrEmpty(endDateStr))
                {
                    existingEvent.EndDateTime = DateTime.Parse(endDateStr);
                }
                else
                {
                    existingEvent.EndDateTime = null;
                }

                // Set audit fields
                var user = await _userManager.GetUserAsync(User);
                existingEvent.UpdatedBy = user?.UserName ?? "System";
                existingEvent.UpdatedAt = DateTime.Now;

                // Save changes
                _context.Update(existingEvent);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Event updated successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating event: {ex}");
                return Json(new { success = false, message = $"Error updating event: {ex.Message}" });
            }
        }


        [HttpPost]
        [Route("AcademicCalendar/DeleteEvent/{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            try
            {
                Console.WriteLine($"Delete request for ID: {id}");

                if (id <= 0)
                {
                    return Json(new { success = false, message = "Invalid event ID" });
                }

                // Find the event
                var eventToDelete = await _context.AcademicCalendarEvents.FindAsync(id);
                Console.WriteLine($"Event found: {eventToDelete != null}");

                if (eventToDelete == null)
                {
                    return Json(new { success = false, message = "Event not found" });
                }

                // Don't allow deleting system events
                if (eventToDelete.IsSystemEvent)
                {
                    return Json(new { success = false, message = "System events cannot be deleted" });
                }

                // Remove and save
                _context.AcademicCalendarEvents.Remove(eventToDelete);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Event deleted successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting event: {ex}");
                return Json(new { success = false, message = $"Error deleting event: {ex.Message}" });
            }
        }




        // POST: Admin/AcademicCalendar/Delete
        [HttpPost]
        public async Task<JsonResult> Delete(int id)
        {
            try
            {
                var calendarEvent = await _context.AcademicCalendarEvents.FindAsync(id);

                if (calendarEvent == null)
                {
                    return Json(new { success = false, message = "Event not found" });
                }

                // Don't allow deleting system events
                if (calendarEvent.IsSystemEvent)
                {
                    return Json(new { success = false, message = "System events cannot be deleted" });
                }

                _context.AcademicCalendarEvents.Remove(calendarEvent);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Event deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting event: {ex.Message}" });
            }
        }




        [HttpPost]
        [Route("AcademicCalendar/CreateWithDto")]
        public async Task<IActionResult> CreateWithDto([FromBody] EventCreateDto dto)
        {
            try
            {
                // Log receiving of data
                Console.WriteLine($"Received DTO: {(dto == null ? "null" : "not null")}");

                if (dto == null)
                {
                    return Json(new { success = false, message = "No data received" });
                }

                // Log DTO data
                Console.WriteLine($"DTO data: Title={dto.Title}, Start={dto.StartDateTime}");



                // Set audit fields
                var user = await _userManager.GetUserAsync(User);

                // Map DTO to entity
                var model = new AcademicCalendarEvent
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    IsAllDay = dto.IsAllDay,
                    AcademicYearId = dto.AcademicYearId,
                    EventTypeId = dto.EventTypeId,
                    Color = dto.Color,
                    Location = dto.Location,
                    ContactPerson = dto.ContactPerson,
                    ContactEmail = dto.ContactEmail,
                    SchoolId = dto.SchoolId,
                    ProgrammeId = dto.ProgrammeId,
                    ProgrammeLevelId = dto.ProgrammeLevelId,
                    ModeOfStudyId = dto.ModeOfStudyId,
                    StudentYear = dto.StudentYear,
                    Semester = dto.Semester,
                    IsPublished = dto.IsPublished,
                    IsSystemEvent = dto.IsSystemEvent,
                    CreatedBy = user?.UserName ?? "System",
                    CreatedAt = DateTime.Now,
                };

                // Parse dates
                if (!string.IsNullOrEmpty(dto.StartDateTime))
                {
                    model.StartDateTime = DateTime.Parse(dto.StartDateTime);
                }

                if (!string.IsNullOrEmpty(dto.EndDateTime))
                {
                    model.EndDateTime = DateTime.Parse(dto.EndDateTime);
                }

                // Set default color if needed
                if (string.IsNullOrEmpty(model.Color))
                {
                    var eventType = await _context.AcademicEventTypes.FindAsync(model.EventTypeId);
                    model.Color = eventType?.DefaultColor ?? "#3788d8";
                }



                // Save to database
                _context.Add(model);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = model.Id, message = "Event created successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating event: {ex}");
                return Json(new { success = false, message = $"Error creating event: {ex.Message}" });
            }
        }

        [HttpPost]
        [Route("AcademicCalendar/UpdateWithDto")]
        public async Task<IActionResult> UpdateWithDto([FromBody] EventCreateDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return Json(new { success = false, message = "No data received" });
                }

                if (dto.Id <= 0)
                {
                    return Json(new { success = false, message = "Invalid ID" });
                }

                // Find existing event
                var existingEvent = await _context.AcademicCalendarEvents.FindAsync(dto.Id);
                if (existingEvent == null)
                {
                    return Json(new { success = false, message = "Event not found" });
                }

                // Don't update system events
                if (existingEvent.IsSystemEvent)
                {
                    return Json(new { success = false, message = "System events cannot be modified" });
                }

                // Update properties
                existingEvent.Title = dto.Title;
                existingEvent.Description = dto.Description;
                existingEvent.IsAllDay = dto.IsAllDay;
                existingEvent.EventTypeId = dto.EventTypeId;
                existingEvent.Color = dto.Color;
                existingEvent.Location = dto.Location;
                existingEvent.ContactPerson = dto.ContactPerson;
                existingEvent.ContactEmail = dto.ContactEmail;
                existingEvent.SchoolId = dto.SchoolId;
                existingEvent.ProgrammeId = dto.ProgrammeId;
                existingEvent.ProgrammeLevelId = dto.ProgrammeLevelId;
                existingEvent.ModeOfStudyId = dto.ModeOfStudyId;
                existingEvent.StudentYear = dto.StudentYear;
                existingEvent.Semester = dto.Semester;
                existingEvent.IsPublished = dto.IsPublished;

                // Parse dates
                if (!string.IsNullOrEmpty(dto.StartDateTime))
                {
                    existingEvent.StartDateTime = DateTime.Parse(dto.StartDateTime);
                }

                if (!string.IsNullOrEmpty(dto.EndDateTime))
                {
                    existingEvent.EndDateTime = DateTime.Parse(dto.EndDateTime);
                }
                else
                {
                    existingEvent.EndDateTime = null;
                }

                // Set audit fields
                var user = await _userManager.GetUserAsync(User);
                existingEvent.UpdatedBy = user?.UserName ?? "System";
                existingEvent.UpdatedAt = DateTime.Now;

                // Save changes
                _context.Update(existingEvent);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Event updated successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating event: {ex}");
                return Json(new { success = false, message = $"Error updating event: {ex.Message}" });
            }
        }






        // POST: Admin/AcademicCalendar/TogglePublishStatus
        [HttpPost]
        public async Task<JsonResult> TogglePublishStatus(int id)
        {
            try
            {
                var calendarEvent = await _context.AcademicCalendarEvents.FindAsync(id);

                if (calendarEvent == null)
                {
                    return Json(new { success = false, message = "Event not found" });
                }

                // Toggle the published status
                calendarEvent.IsPublished = !calendarEvent.IsPublished;

                // Set audit fields
                var user = await _userManager.GetUserAsync(User);
                calendarEvent.UpdatedBy = user.UserName;
                calendarEvent.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                string statusMessage = calendarEvent.IsPublished ? "published" : "unpublished";
                return Json(new
                {
                    success = true,
                    message = $"Event {statusMessage} successfully",
                    isPublished = calendarEvent.IsPublished
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error updating event status: {ex.Message}" });
            }
        }

        // GET: Admin/AcademicCalendar/GetEventTypes
        [HttpGet]
        public async Task<JsonResult> GetEventTypes()
        {
            var eventTypes = await _context.AcademicEventTypes
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    color = t.DefaultColor,
                    icon = t.IconName
                })
                .ToListAsync();

            return Json(eventTypes);
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

        // POST: Admin/AcademicCalendar/GenerateSystemEvents/5
        [HttpPost]
        public async Task<JsonResult> GenerateSystemEvents(int academicYearId)
        {
            try
            {
                if (academicYearId <= 0)
                {
                    return Json(new { success = false, message = "Invalid academic year" });
                }

                await _calendarService.GenerateSystemEventsForAcademicYear(academicYearId);

                return Json(new { success = true, message = "System events generated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error generating system events: {ex.Message}" });
            }
        }

        // GET: Admin/AcademicCalendar/ManageEventTypes
        public async Task<IActionResult> ManageEventTypes()
        {
            var eventTypes = await _context.AcademicEventTypes
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(eventTypes);
        }

        // GET: Admin/AcademicCalendar/GetEventType/5
        [HttpGet]
        public async Task<JsonResult> GetEventType(int id)
        {
            var eventType = await _context.AcademicEventTypes.FindAsync(id);

            if (eventType == null)
            {
                return Json(new { success = false, message = "Event type not found" });
            }

            return Json(new
            {
                success = true,
                eventType = new
                {
                    id = eventType.Id,
                    name = eventType.Name,
                    defaultColor = eventType.DefaultColor,
                    iconName = eventType.IconName
                }
            });
        }

        // POST: Admin/AcademicCalendar/CreateEventType
        [HttpPost]
        public async Task<JsonResult> CreateEventType(AcademicEventType model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(model);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, id = model.Id, message = "Event type created successfully" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error creating event type: {ex.Message}" });
                }
            }

            return Json(new
            {
                success = false,
                message = "Invalid form data",
                errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList()
            });
        }

        // POST: Admin/AcademicCalendar/UpdateEventType
        [HttpPost]
        public async Task<JsonResult> UpdateEventType(AcademicEventType model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingEventType = await _context.AcademicEventTypes.FindAsync(model.Id);

                    if (existingEventType == null)
                    {
                        return Json(new { success = false, message = "Event type not found" });
                    }

                    // Update properties
                    existingEventType.Name = model.Name;
                    existingEventType.DefaultColor = model.DefaultColor;
                    existingEventType.IconName = model.IconName;

                    _context.Update(existingEventType);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Event type updated successfully" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error updating event type: {ex.Message}" });
                }
            }

            return Json(new
            {
                success = false,
                message = "Invalid form data",
                errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList()
            });
        }

        // POST: Admin/AcademicCalendar/DeleteEventType
        [HttpPost]
        public async Task<JsonResult> DeleteEventType(int id)
        {
            try
            {
                var eventType = await _context.AcademicEventTypes.FindAsync(id);

                if (eventType == null)
                {
                    return Json(new { success = false, message = "Event type not found" });
                }

                // Check if this event type is in use
                var eventsUsingType = await _context.AcademicCalendarEvents
                    .AnyAsync(e => e.EventTypeId == id);

                if (eventsUsingType)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Cannot delete this event type because it is being used by one or more events. Update those events first."
                    });
                }

                _context.AcademicEventTypes.Remove(eventType);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Event type deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting event type: {ex.Message}" });
            }
        }

        // GET: Admin/AcademicCalendar/GetGoogleIcons
        [HttpGet]
        public JsonResult GetGoogleIcons()
        {
            // Return a list of common Google Material Icons that are relevant for academic events
            var icons = new List<string>
            {
                "event", "event_available", "event_busy", "event_note",
                "calendar_today", "today", "date_range", "schedule",
                "school", "book", "menu_book", "import_contacts", "history_edu",
                "assignment", "assignment_turned_in", "grading", "fact_check",
                "edit_note", "rate_review", "engineering", "science", "biotech",
                "psychology", "self_improvement", "person", "groups", "diversity_3",
                "handshake", "emoji_events", "military_tech", "workspace_premium",
                "tour", "place", "luggage", "flight", "hiking", "nightlife",
                "house", "meeting_room", "corporate_fare", "sports_score",
                "timer", "hourglass_top", "alarm", "access_time",
                "celebration", "cake", "sports_bar", "festival",
                "work", "business_center", "payments", "request_quote",
                "favorite", "bolt", "star", "public", "share", "connect_without_contact",
                "smart_display", "wysiwyg", "desktop_mac", "laptop_mac",
                "notifications", "notifications_active", "mark_email_unread", "email",
                "help", "info", "announcement", "campaign",
                "gavel", "policy", "privacy_tip", "warning", "report_problem",
                "architecture", "brush", "palette", "theaters", "music_note",
                "sports_soccer", "sports_basketball", "sports", "fitness_center",
                "restaurant", "local_cafe", "dining", "coffee", "free_breakfast",
                "hotel", "spa", "health_and_safety", "medical_services", "volunteer_activism",
                "wifi", "network_check", "settings", "build", "construction"
            };

            return Json(icons);
        }

        // GET: /AcademicCalendar/Export/5
        public async Task<IActionResult> Export(int academicYearId, string format = "html")
        {
            if (academicYearId <= 0)
            {
                // Get current academic year
                var currentYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(a => a.IsActive);

                if (currentYear != null)
                {
                    academicYearId = currentYear.YearId;
                }
                else
                {
                    return NotFound("No active academic year found");
                }
            }

            var academicYear = await _context.AcademicYears
                .FirstOrDefaultAsync(y => y.YearId == academicYearId);

            if (academicYear == null)
            {
                return NotFound("Academic year not found");
            }

            if (format.ToLower() == "pdf")
            {
                // Generate PDF
                var pdfService = HttpContext.RequestServices.GetRequiredService<ICalendarPdfService>();
                var pdfBytes = await pdfService.GenerateCalendarPdfAsync(academicYearId);

                return File(pdfBytes, "application/pdf", $"Academic_Calendar_{academicYear.YearValue}.pdf");
            }
            else
            {
                // Display HTML view
                var events = await _context.AcademicCalendarEvents
                    .Include(e => e.EventType)
                    .Include(e => e.School)
                    .Include(e => e.Programme)
                    .Include(e => e.ProgrammeLevel)
                    .Include(e => e.ModeOfStudy)
                    .Where(e => e.AcademicYearId == academicYearId && e.IsPublished)
                    .OrderBy(e => e.StartDateTime)
                    .ToListAsync();

                ViewBag.AcademicYear = academicYear;
                ViewBag.EventCount = events.Count;
                ViewBag.InstitutionName = "University"; // Customize as needed

                return View(events);
            }
        }

        // POST: Admin/AcademicCalendar/DuplicateEvent
        [HttpPost]
        public async Task<JsonResult> DuplicateEvent(int id)
        {
            try
            {
                var existingEvent = await _context.AcademicCalendarEvents.FindAsync(id);

                if (existingEvent == null)
                {
                    return Json(new { success = false, message = "Event not found" });
                }

                // Set audit fields
                var user = await _userManager.GetUserAsync(User);
                if(user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Create a new event with the same properties
                var newEvent = new AcademicCalendarEvent
                {
                    AcademicYearId = existingEvent.AcademicYearId,
                    Title = existingEvent.Title + " (Copy)",
                    Description = existingEvent.Description,
                    StartDateTime = existingEvent.StartDateTime,
                    EndDateTime = existingEvent.EndDateTime,
                    IsAllDay = existingEvent.IsAllDay,
                    EventTypeId = existingEvent.EventTypeId,
                    Color = existingEvent.Color,
                    Location = existingEvent.Location,
                    ContactPerson = existingEvent.ContactPerson,
                    ContactEmail = existingEvent.ContactEmail,
                    SchoolId = existingEvent.SchoolId,
                    ProgrammeId = existingEvent.ProgrammeId,
                    ProgrammeLevelId = existingEvent.ProgrammeLevelId,
                    ModeOfStudyId = existingEvent.ModeOfStudyId,
                    StudentYear = existingEvent.StudentYear,
                    Semester = existingEvent.Semester,
                    IsPublished = false, // Set as unpublished by default
                    IsSystemEvent = false, // Never duplicate as system event
                    CreatedBy = user.UserName,
                    CreatedAt = DateTime.Now
                };

                

                _context.Add(newEvent);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = newEvent.Id, message = "Event duplicated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error duplicating event: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<JsonResult> MoveEvent([FromBody] MoveEventModel model)
        {
            try
            {
                // Add debug logging
                //_logger.LogInformation($"MoveEvent called with ID: {model.Id}, Start: {model.Start}, End: {model.End}, AllDay: {model.AllDay}");

                var calendarEvent = await _context.AcademicCalendarEvents.FindAsync(model.Id);
                if (calendarEvent == null)
                {
                    //_logger.LogWarning($"Event not found with ID: {model.Id}");
                    return Json(new { success = false, message = "Event not found" });
                }

                // Don't allow moving system events
                if (calendarEvent.IsSystemEvent)
                {
                    return Json(new { success = false, message = "System events cannot be moved" });
                }

                // Update event dates
                calendarEvent.StartDateTime = model.Start;
                calendarEvent.EndDateTime = model.End;
                calendarEvent.IsAllDay = model.AllDay;

                // Set audit fields
                var user = await _userManager.GetUserAsync(User);
                calendarEvent.UpdatedBy = user.UserName;
                calendarEvent.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Event moved successfully" });
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error moving event");
                return Json(new { success = false, message = $"Error moving event: {ex.Message}" });
            }
        }

        // Model for binding
        public class MoveEventModel
        {
            public int Id { get; set; }
            public DateTime Start { get; set; }
            public DateTime? End { get; set; }
            public bool AllDay { get; set; }
        }

        // GET: Admin/AcademicCalendar/BulkCreate
        public async Task<IActionResult> BulkCreate()
        {
            ViewBag.AcademicYears = await _context.AcademicYears
                .OrderByDescending(y => y.StartDate)
                .Select(y => new SelectListItem
                {
                    Value = y.YearId.ToString(),
                    Text = y.YearValue,
                    Selected = y.IsActive
                })
                .ToListAsync();

            ViewBag.EventTypes = await _context.AcademicEventTypes
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = t.Name })
                .ToListAsync();

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

            return View();
        }

        // POST: Admin/AcademicCalendar/BulkCreate
        [HttpPost]
        public async Task<JsonResult> BulkCreateEvents(BulkEventCreationModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var newEvents = new List<AcademicCalendarEvent>();
                    var user = await _userManager.GetUserAsync(User);
                    var eventType = await _context.AcademicEventTypes.FindAsync(model.EventTypeId);
                    string color = model.Color;

                    // If no color specified, use event type default color
                    if (string.IsNullOrEmpty(color) && eventType != null)
                    {
                        color = eventType.DefaultColor;
                    }

                    // Calculate the date range
                    DateTime currentDate = model.StartDate;
                    DateTime endRange = model.EndDate;

                    // Create events based on repetition pattern
                    while (currentDate <= endRange)
                    {
                        bool createForThisDate = false;

                        switch (model.RepeatPattern)
                        {
                            case "daily":
                                createForThisDate = true;
                                break;
                            case "weekdays":
                                // Monday through Friday
                                createForThisDate = currentDate.DayOfWeek != DayOfWeek.Saturday &&
                                                  currentDate.DayOfWeek != DayOfWeek.Sunday;
                                break;
                            case "weekly":
                                // Same day of week as start date
                                createForThisDate = currentDate.DayOfWeek == model.StartDate.DayOfWeek;
                                break;
                            case "biweekly":
                                // Every other week on the same day of week
                                int weekDiff = (int)((currentDate - model.StartDate).TotalDays / 7);
                                createForThisDate = currentDate.DayOfWeek == model.StartDate.DayOfWeek &&
                                                  weekDiff % 2 == 0;
                                break;
                            case "monthly":
                                // Same day of month
                                createForThisDate = currentDate.Day == model.StartDate.Day;
                                break;
                            default:
                                // No repeat (just one event)
                                createForThisDate = currentDate == model.StartDate;
                                break;
                        }

                        if (createForThisDate)
                        {
                            // Create event for this date
                            var startDateTime = new DateTime(
                                currentDate.Year,
                                currentDate.Month,
                                currentDate.Day,
                                model.StartTime.Hour,
                                model.StartTime.Minute,
                                0
                            );

                            DateTime? endDateTime = null;
                            if (model.EndTime.HasValue)
                            {
                                endDateTime = new DateTime(
                                    currentDate.Year,
                                    currentDate.Month,
                                    currentDate.Day,
                                    model.EndTime.Value.Hour,
                                    model.EndTime.Value.Minute,
                                    0
                                );
                            }

                            var newEvent = new AcademicCalendarEvent
                            {
                                Title = model.Title,
                                Description = model.Description,
                                StartDateTime = startDateTime,
                                EndDateTime = endDateTime,
                                IsAllDay = model.IsAllDay,
                                AcademicYearId = model.AcademicYearId,
                                EventTypeId = model.EventTypeId,
                                Color = color,
                                Location = model.Location,
                                ContactPerson = model.ContactPerson,
                                ContactEmail = model.ContactEmail,
                                SchoolId = model.SchoolId,
                                ProgrammeId = model.ProgrammeId,
                                ProgrammeLevelId = model.ProgrammeLevelId,
                                ModeOfStudyId = model.ModeOfStudyId,
                                StudentYear = model.StudentYear,
                                Semester = model.Semester,
                                IsPublished = model.IsPublished,
                                IsSystemEvent = false,
                                CreatedBy = user.UserName,
                                CreatedAt = DateTime.Now
                            };

                            newEvents.Add(newEvent);
                        }

                        // Move to next day
                        currentDate = currentDate.AddDays(1);
                    }

                    // Add all events to database
                    await _context.AcademicCalendarEvents.AddRangeAsync(newEvents);
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"{newEvents.Count} events created successfully"
                    });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error creating events: {ex.Message}" });
                }
            }

            return Json(new
            {
                success = false,
                message = "Invalid form data",
                errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList()
            });
        }
    }
}

    // Model for bulk event creation
    public class BulkEventCreationModel
    {
        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Required]
        [Display(Name = "Start Time")]
        public DateTime StartTime { get; set; }

        [Display(Name = "End Time")]
        public DateTime? EndTime { get; set; }

        [Display(Name = "All Day Event")]
        public bool IsAllDay { get; set; }

        [Required]
        [Display(Name = "Academic Year")]
        public int AcademicYearId { get; set; }

        [Required]
        [Display(Name = "Event Type")]
        public int EventTypeId { get; set; }

        [Display(Name = "Color")]
        public string Color { get; set; }

        [Display(Name = "Location")]
        public string Location { get; set; }

        [Display(Name = "Contact Person")]
        public string ContactPerson { get; set; }

        [Display(Name = "Contact Email")]
        [EmailAddress]
        public string ContactEmail { get; set; }

        [Display(Name = "School")]
        public int? SchoolId { get; set; }

        [Display(Name = "Programme")]
        public int? ProgrammeId { get; set; }

        [Display(Name = "Programme Level")]
        public int? ProgrammeLevelId { get; set; }

        [Display(Name = "Mode of Study")]
        public int? ModeOfStudyId { get; set; }

        [Display(Name = "Student Year")]
        public int? StudentYear { get; set; }

        [Display(Name = "Semester")]
        public int? Semester { get; set; }

        [Display(Name = "Repeat Pattern")]
        public string RepeatPattern { get; set; } = "none"; 

        [Display(Name = "Published")]
        public bool IsPublished { get; set; } = true;
    }

