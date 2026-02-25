using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using System.Text.Json;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin, Registrar, Academic Officer")]
    public class AcademicEventTypeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AcademicEventTypeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/AcademicEventType
        public async Task<IActionResult> Index()
        {
            var eventTypes = await _context.AcademicEventTypes
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(eventTypes);
        }

        // GET: Admin/AcademicEventType/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/AcademicEventType/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] Dictionary<string, JsonElement> data)
        {
            try
            {
                // Extract values
                string name = data.ContainsKey("Name") ? data["Name"].GetString() : null;
                string color = data.ContainsKey("DefaultColor") ? data["DefaultColor"].GetString() : null;
                string icon = data.ContainsKey("IconName") ? data["IconName"].GetString() : null;

                // Log the extracted values
                Console.WriteLine($"Extracted values: Name={name}, Color={color}, Icon={icon}");

                // Validate
                if (string.IsNullOrEmpty(name))
                {
                    return Json(new { success = false, message = "Name cannot be empty." });
                }

                // Create entity
                var eventType = new AcademicEventType
                {
                    Name = name,
                    DefaultColor = color,
                    IconName = icon
                };

                _context.Add(eventType);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Create: {ex}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: AcademicEventType/GetEventType/{id}
        [HttpGet]
        [Route("AcademicEventType/GetEventType/{id}")]
        public async Task<IActionResult> GetEventType(int id)
        {
            Console.WriteLine($"GetEventType called with id: {id}");
            try
            {
                var eventType = await _context.AcademicEventTypes.FindAsync(id);

                if (eventType == null)
                {
                    Console.WriteLine($"Event type with id {id} not found");
                    return Json(new { success = false, message = "Event type not found." });
                }

                Console.WriteLine($"Found event type: Id={eventType.Id}, Name={eventType.Name}");
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetEventType: {ex}");
                return Json(new { success = false, message = "Error loading event type." });
            }
        }

        // POST: AcademicEventType/UpdateEventType
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEventType([FromBody] Dictionary<string, JsonElement> data)
        {
            try
            {
                // Extract values - Parse Id safely from string
                int id;
                if (data.ContainsKey("Id"))
                {
                    // Handle Id as either string or number
                    if (data["Id"].ValueKind == JsonValueKind.String)
                    {
                        if (!int.TryParse(data["Id"].GetString(), out id))
                        {
                            return Json(new { success = false, message = "Invalid Id format." });
                        }
                    }
                    else
                    {
                        id = data["Id"].GetInt32();
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Id is required." });
                }

                string name = data.ContainsKey("Name") ? data["Name"].GetString() : null;
                string color = data.ContainsKey("DefaultColor") ? data["DefaultColor"].GetString() : null;
                string icon = data.ContainsKey("IconName") ? data["IconName"].GetString() : null;

                Console.WriteLine($"UpdateEventType called with Id={id}, Name={name}, Color={color}, Icon={icon}");

                // Find the event type
                var eventType = await _context.AcademicEventTypes.FindAsync(id);
                if (eventType == null)
                {
                    return Json(new { success = false, message = "Event type not found." });
                }

                // Validate
                if (string.IsNullOrEmpty(name))
                {
                    return Json(new { success = false, message = "Name cannot be empty." });
                }

                // Update the properties
                eventType.Name = name;
                eventType.DefaultColor = color;
                eventType.IconName = icon;

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateEventType: {ex}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: AcademicEventType/DeleteEventType
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEventType([FromBody] Dictionary<string, JsonElement> data)
        {
            try
            {
                // Extract and parse Id safely
                int id;
                if (data.ContainsKey("id"))
                {
                    // Handle id as either string or number
                    if (data["id"].ValueKind == JsonValueKind.String)
                    {
                        if (!int.TryParse(data["id"].GetString(), out id))
                        {
                            return Json(new { success = false, message = "Invalid Id format." });
                        }
                    }
                    else
                    {
                        id = data["id"].GetInt32();
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Id is required." });
                }

                Console.WriteLine($"DeleteEventType called with Id={id}");

                // Check if this event type is in use
                bool inUse = await _context.AcademicCalendarEvents
                    .AnyAsync(e => e.EventTypeId == id);

                if (inUse)
                {
                    return Json(new { success = false, message = "Cannot delete this event type because it is being used by one or more events." });
                }

                var typeToDelete = await _context.AcademicEventTypes.FindAsync(id);
                if (typeToDelete == null)
                {
                    return Json(new { success = false, message = "Event type not found." });
                }

                _context.AcademicEventTypes.Remove(typeToDelete);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteEventType: {ex}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: Admin/AcademicEventType/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var eventType = await _context.AcademicEventTypes.FindAsync(id);
            if (eventType == null)
            {
                return NotFound();
            }

            // Get events using this type
            var events = await _context.AcademicCalendarEvents
                .Include(e => e.AcademicYear)
                .Where(e => e.EventTypeId == id)
                .OrderByDescending(e => e.StartDateTime)
                .Take(10) // Limit to recent 10 events
                .ToListAsync();

            ViewBag.Events = events;
            ViewBag.EventCount = await _context.AcademicCalendarEvents
                .CountAsync(e => e.EventTypeId == id);

            return View(eventType);
        }

        // GET: Admin/AcademicEventType/GetIcons
        public JsonResult GetIcons()
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

        private bool EventTypeExists(int id)
        {
            return _context.AcademicEventTypes.Any(e => e.Id == id);
        }
    }
}
