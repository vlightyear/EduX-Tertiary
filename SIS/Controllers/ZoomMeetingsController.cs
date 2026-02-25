
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Zoom;
using SIS.Services.Zoom;
using System.Security.Claims;
using System.Transactions;

namespace SIS.Controllers
{
    public class ZoomMeetingsController : Controller
    {
        private readonly IZoomService _zoomService;
        private readonly IZoomWebSdkService _zoomWebSdkService;
        private readonly ILogger<ZoomMeetingsController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        // Update the constructor to include UserManager
        public ZoomMeetingsController(
            IZoomService zoomService,
            IZoomWebSdkService zoomWebSdkService,
            ILogger<ZoomMeetingsController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _zoomService = zoomService;
            _zoomWebSdkService = zoomWebSdkService;
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        // The rest of your controller remains the same
        public async Task<IActionResult> Index()
        {
            // Get the current user ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Differentiate between lecturer and student
            if (User.IsInRole("Lecturer"))
            {
                var meetings = await _zoomService.GetMeetingsForLecturerAsync(userId);
                return View(meetings);
            }
            else
            {
                // Get the current user
                var user = await _userManager.GetUserAsync(User);

                // Get the username
                var username = user.UserName;

                var meetings = await _zoomService.GetMeetingsForStudentAsync(username);
                return View("StudentIndex", meetings);
            }
        }

        // GET: ZoomMeetings/Create
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Create()
        {
            // Get the current logged-in lecturer's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // Fetch all courses where this lecturer is assigned
            var lecturerCourses = await _context.Courses
                .Include(c => c.Programme)
                    .ThenInclude(p => p.Department)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                            c.InstructorId == currentUserId)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            ViewBag.Courses = lecturerCourses;

            return View();
        }

        // POST: ZoomMeetings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Create(CreateMeetingViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Get the current user ID, not name
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                    var meeting = await _zoomService.CreateMeetingAsync(
                        model.Topic,
                        model.StartTime,
                        model.Duration,
                        model.Agenda,
                        model.CourseId,
                        userId); // Pass user ID here, not User.Identity.Name

                    return RedirectToAction(nameof(Details), new { id = meeting.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating meeting");
                    ModelState.AddModelError(string.Empty, "An error occurred while creating the meeting.");
                    TempData["Error"] = "An error occurred while creating the meeting.";
                }
            }

            // Repopulate the courses for the dropdown
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var lecturerCourses = await _context.Courses
                .Include(c => c.Programme)
                    .ThenInclude(p => p.Department)
                .Include(c => c.CourseLecturers)
                    .ThenInclude(cl => cl.Lecturer)
                .Where(c => c.CourseLecturers.Any(cl => cl.LecturerId == currentUserId) ||
                            c.InstructorId == currentUserId)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            ViewBag.Courses = lecturerCourses;

            return View(model);
        }

        // GET: ZoomMeetings/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var meeting = await _zoomService.GetMeetingAsync(id);
            if (meeting == null)
            {
                return NotFound();
            }

            return View(meeting);
        }

        // GET: ZoomMeetings/StartMeeting/5
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> StartMeeting(int id)
        {
            var meeting = await _zoomService.GetMeetingAsync(id);
            if (meeting == null)
            {
                return NotFound();
            }

            // Get the current user ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Only allow the creator to start the meeting
            if (meeting.CreatedById != userId)
            {
                return Forbid();
            }

            // Update meeting status
            await _zoomService.UpdateMeetingStatusAsync(id, Enums.Status.Active);

            // Check if StartUrl is available
            if (string.IsNullOrEmpty(meeting.StartUrl))
            {
                _logger.LogWarning($"Invalid Zoom host meeting URL for meeting {id}");
                TempData["Error"] = "Unable to generate host meeting link. Please contact support.";
                return RedirectToAction(nameof(Index));
            }

            // Pass the start URL to a transitional view
            TempData["Success"] = "Meeting started Successfully";
            ViewBag.StartUrl = meeting.StartUrl;
            return View("StartMeetingRedirect", meeting);
        }

        // GET: ZoomMeetings/JoinMeeting/5
        [Authorize]
        public async Task<IActionResult> JoinMeeting(int id)
        {
            var meeting = await _zoomService.GetMeetingAsync(id);
            if (meeting == null)
            {
                return NotFound();
            }

            // Additional error handling and logging
            if (string.IsNullOrEmpty(meeting.JoinUrl))
            {
                _logger.LogWarning($"Invalid Zoom meeting URL for meeting {id}");
                TempData["Error"] = "Unable to generate meeting link. Please contact support.";
                return RedirectToAction(nameof(Index));
            }

           

            // Consider passing additional context to the view
            ViewBag.MeetingUrl = meeting.JoinUrl;
            return View("JoinMeeting", meeting);
        }

        [Authorize(Roles = "Lecturer")]

        public async Task<IActionResult> HostMeeting(int id)
        {
            var meeting = await _zoomService.GetMeetingAsync(id);
            if (meeting == null)
            {
                return NotFound();
            }

            // Additional error handling and logging
            if (string.IsNullOrEmpty(meeting.JoinUrl))
            {
                _logger.LogWarning($"Invalid Zoom meeting URL for meeting {id}");
                TempData["Error"] = "Unable to generate meeting link. Please contact support.";
                return RedirectToAction(nameof(Index));
            }



            // Consider passing additional context to the view
            ViewBag.MeetingUrl = meeting.JoinUrl;
            return View("HostMeeting", meeting);
        }

        // Helper method to check enrollment
        // Helper method to check enrollment
        private async Task<bool> IsStudentEnrolledInCourse(string studentId, int courseId)
        {
            // Parse the studentId to integer (assuming your system uses int for user IDs)
            if (!int.TryParse(studentId, out int studentIdInt))
            {
                _logger.LogWarning($"Could not parse studentId {studentId} to integer");
                return false;
            }

            // Query the database to check if the student is enrolled in the course
            var isEnrolled = await _context.StudentCourseRegistrations
                .AnyAsync(scr => scr.StudentId == studentIdInt &&
                                 scr.CourseId == courseId);

            if (!isEnrolled)
            {
                _logger.LogInformation($"Student {studentId} attempted to access course {courseId} but is not enrolled");
                TempData["Info"] = $"Student {studentId} attempted to access course {courseId} but is not enrolled";
            }


            return isEnrolled;
        }

        // POST: ZoomMeetings/EndMeeting/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> EndMeeting(int id)
        {
            var meeting = await _zoomService.GetMeetingAsync(id);
            if (meeting == null)
            {
                return NotFound();
            }

            // Get the current user ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Only allow the creator to end the meeting
            if (meeting.CreatedById != userId)
            {
                return Forbid();
            }

            // Update meeting status
            await _zoomService.UpdateMeetingStatusAsync(id, Enums.Status.Completed);
            TempData["Success"] = "Meeting has been Updated Successfully";

            return RedirectToAction(nameof(Index));
        }


        // POST: ZoomMeetings/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Delete(int id)
        {
            var meeting = await _zoomService.GetMeetingAsync(id);
            if (meeting == null)
            {
                return NotFound();
            }

            // Get the current user ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Only allow the creator to delete the meeting
            if (meeting.CreatedById != userId)
            {
                return Forbid();
            }

            await _zoomService.DeleteMeetingAsync(id);
            TempData["Success"] = "Meeting Deleted Successfully";
            return RedirectToAction(nameof(Index));
        }


        [Authorize()]
        public async Task<IActionResult> Join(int id)
        {
            var role = 0;//participant
            try
            {
                var meetingResponse = await _zoomService.GetMeetingAsync(id);
                if (meetingResponse == null)
                {
                    return NotFound();
                }

                // If no username is provided, use a default or get from user claims if authenticated
                var userName = "";
                if (string.IsNullOrEmpty(userName))
                {
                    if (User.Identity.IsAuthenticated)
                    {
                        // Try to get the name from claims if user is authenticated
                        userName = User.FindFirst(ClaimTypes.Name)?.Value ??
                                  User.FindFirst(ClaimTypes.Email)?.Value ??
                                  "App User";
                    }
                    else
                    {
                        userName = "Guest User";
                    }
                }


                var (sdkKey,signature) = _zoomWebSdkService.GenerateSignature(meetingResponse.ZoomMeetingId, role);

                var viewModel = new ZoomMeetingJoinViewModel
                {
                    MeetingId = meetingResponse.Id,
                    ZoomMeetingNumber = meetingResponse.ZoomMeetingId,
                    MeetingPassword = meetingResponse.Password,
                    MeetingTopic = meetingResponse.Topic,
                    UserName = userName,
                    Role = role,// 0 for attendee, 1 for host
                    Signature=signature,
                    SdkKey=sdkKey
                };              

                return View(viewModel);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index", "Home", new { error = $"Error joining meeting: {ex.Message}" });
            }
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Host(int id)
        {

            var role = 1;//host

            try
            {
                var meetingResponse = await _zoomService.GetMeetingAsync(id);
                if (meetingResponse == null)
                {
                    return NotFound();
                }

                var (sdkKey, signature) = _zoomWebSdkService.GenerateSignature(meetingResponse.ZoomMeetingId, role);

                // Get the current user ID
                var userName = User.FindFirstValue(ClaimTypes.Name);

                // Get username for the host
                if (string.IsNullOrEmpty(userName))
                {
                    if (User.Identity.IsAuthenticated)
                    {
                        userName = User.FindFirst(ClaimTypes.Name)?.Value ??
                                  User.FindFirst(ClaimTypes.Email)?.Value ??
                                  "Meeting Host";
                    }
                    else
                    {
                        userName = "Meeting Host";
                    }
                }

                var viewModel = new ZoomMeetingJoinViewModel
                {
                    MeetingId = meetingResponse.Id,
                    ZoomMeetingNumber = meetingResponse.ZoomMeetingId,
                    MeetingPassword = meetingResponse.Password,
                    MeetingTopic = meetingResponse.Topic,
                    UserName = userName,
                    Role = role,// 0 for attendee, 1 for host
                    Signature = signature,
                    SdkKey = sdkKey
                };

                return View("Join", viewModel);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index", "Home", new { error = $"Error hosting meeting: {ex.Message}" });
            }
        }


        public async Task<IActionResult> NotifyMeetingEnded(int id)
        {
            return View();
        }


    }





































// ViewModel for creating a meeting
public class CreateMeetingViewModel
{
    public string Topic { get; set; }
    public DateTime StartTime { get; set; }
    public int Duration { get; set; }
    public string Agenda { get; set; }
    public int CourseId { get; set; }
}
}
