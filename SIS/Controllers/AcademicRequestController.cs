using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;

namespace SIS.Controllers
{
    public class AcademicRequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AcademicRequestController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        // STUDENT ACTIONS


        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyRequests(int page = 1, int pageSize = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Email == user.Email);

            if (student == null)
            {
                TempData["Error"] = "Student profile not found.";
                return RedirectToAction("Index", "Home");
            }

            var query = _context.AcademicRequests
                .Include(r => r.School)
                .Include(r => r.Programme)
                .Include(r => r.Documents)
                .Where(r => r.StudentId == student.Id)
                .OrderByDescending(r => r.RequestDate);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var requests = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            return View(requests);
        }


        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Submit()
        {
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Email == user.Email);

            if (student == null)
            {
                TempData["Error"] = "Student profile not found.";
                return RedirectToAction("Index", "Home");
            }

            return View(student);
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(string requestType, string description,
            int schoolId = 0, int programmeId = 0, List<IFormFile> documents = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email == user.Email);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found." });
                }

                // Validation
                if (string.IsNullOrEmpty(requestType) || string.IsNullOrEmpty(description))
                {
                    return Json(new { success = false, message = "Request type and description are required." });
                }

                if (requestType == "Programme Change" && (schoolId <= 0 || programmeId <= 0))
                {
                    return Json(new { success = false, message = "Please select both school and programme for programme change requests." });
                }

                if (requestType == "Quota Request" && (schoolId <= 0 || programmeId <= 0))
                {
                    return Json(new { success = false, message = "Please select both school and programme for quota requests." });
                }

                // Create request
                var request = new AcademicRequest
                {
                    StudentId = student.Id,
                    RequestType = requestType,
                    Description = description,
                    SchoolId = (requestType == "Programme Change" || requestType == "Quota Request") ? schoolId : student.SchoolId,
                    ProgrammeId = (requestType == "Programme Change" || requestType == "Quota Request") ? programmeId : student.ProgrammeId,
                    Status = Status.Pending,
                    RequestDate = DateTime.Now,
                    CreatedBy = student.FullName,
                    CreatedAt = DateTime.Now
                };

                _context.AcademicRequests.Add(request);
                await _context.SaveChangesAsync();

                // Handle file uploads
                if (documents != null && documents.Any())
                {
                    await SaveDocuments(request.Id, documents);
                }

                return Json(new { success = true, message = $"{requestType} request submitted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while submitting your request." });
            }
        }

        // ADMIN ACTIONS

        [Authorize(Roles = "Admin,Registrar,Dean,Assistant Registrar")]
        public async Task<IActionResult> Manage(int page = 1, int pageSize = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user.Id;

            // Single query with OR condition
            var query = _context.AcademicRequests
                .AsNoTracking()
                .Include(r => r.Student)
                .Include(r => r.School)
                .Include(r => r.Programme)
                .Include(r => r.Documents)
                .Where(r => r.RequestType != "Quota Request" ||
                           (r.RequestType == "Quota Request" && r.School.AssistantRegistrarId == userId))
                .OrderByDescending(r => r.RequestDate);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var requests = await query
                //.Skip((page - 1) * pageSize)
                //.Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            return View(requests);
        }

        [Authorize(Roles = "Admin,Registrar,Dean,Assistant Registrar")]
        public async Task<IActionResult> QuotaRequests(int page = 1, int pageSize = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user.Id;

            // Single query with OR condition
            var query = _context.AcademicRequests
                .AsNoTracking()
                .Include(r => r.Student)
                    .ThenInclude(s => s.Programme)
                .Include(r => r.Student)
                    .ThenInclude(s => s.School)
                .Include(r => r.School)
                .Include(r => r.Programme)
                .Include(r => r.Documents)
                .Where(r => (r.RequestType == "Quota Request" && r.School.AssistantRegistrarId == userId))
                .OrderByDescending(r => r.RequestDate);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var requests = await query
                //.Skip((page - 1) * pageSize)
                //.Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            return View("~/Views/AcademicRequest/Manage.cshtml", requests);
        }
        /*public async Task<IActionResult> Manage(int page = 1, int pageSize = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            var query = _context.AcademicRequests
                .Include(r => r.Student)
                .Include(r => r.School)
                .Include(r => r.Programme)
                .Include(r => r.Documents)
                .OrderByDescending(r => r.RequestDate);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var requests = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            return View(requests);
        }*/

        [HttpPost]
        [Authorize(Roles = "Admin,Registrar,Dean")]
        public async Task<IActionResult> UpdateStatus(int requestId, Status status, string adminNotes = "")
        {
            try
            {
                var request = await _context.AcademicRequests
                    .Include(r => r.Student)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    return Json(new { success = false, message = "Request not found." });
                }

                request.Status = status;
                request.AdminNotes = adminNotes;
                request.UpdatedBy = User.Identity.Name;
                request.UpdatedAt = DateTime.Now;

                // If approving a programme change, update student record
                if (status == Status.Approved && request.RequestType == "Programme Change")
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.Student.Id);
                    student.SchoolId = request.SchoolId;
                    student.ProgrammeId = request.ProgrammeId;
                    student.UpdatedBy = User.Identity.Name;
                    student.UpdatedAt = DateTime.Now;
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }

                // If approving a quota request, update student record
                if (status == Status.Approved && request.RequestType == "Quota Request")
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.Student.Id);
                    student.SchoolId = request.SchoolId;
                    student.ProgrammeId = request.ProgrammeId;
                    student.UpdatedBy = User.Identity.Name;
                    student.UpdatedAt = DateTime.Now;
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Request {status.ToDisplayString().ToLower()} successfully.",
                    newStatus = status.ToDisplayString()
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while updating the request." });
            }
        }

        // SHARED ACTIONS

        [HttpGet]
        public async Task<IActionResult> GetSchools()
        {
            var schools = await _context.Schools
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return Json(new { success = true, schools });
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammes(int schoolId)
        {
            if (schoolId <= 0)
            {
                return Json(new { success = false, message = "Invalid school ID." });
            }

            var programmes = await _context.Programmes
                .AsNoTracking()
                .Where(p => p.Department.SchoolId == schoolId)
                .Select(p => new { id = p.Id, name = p.Name })
                .ToListAsync();

            return Json(new { success = true, programmes });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int documentId)
        {
            var document = await _context.Set<AcademicRequestDocument>()
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null || !System.IO.File.Exists(document.FilePath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(document.FilePath);
            return File(fileBytes, document.ContentType, document.FileName);
        }

        [HttpGet]
        public async Task<IActionResult> ViewDocument(int documentId)
        {
            var document = await _context.Set<AcademicRequestDocument>()
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null || !System.IO.File.Exists(document.FilePath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(document.FilePath);
            return File(fileBytes, document.ContentType);
        }

        // PRIVATE METHODS

        private async Task SaveDocuments(int requestId, List<IFormFile> files)
        {
            var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "academic-requests");
            Directory.CreateDirectory(uploadsPath);

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var document = new AcademicRequestDocument
                    {
                        AcademicRequestId = requestId,
                        FileName = file.FileName,
                        FilePath = filePath,
                        FileSize = file.Length,
                        ContentType = file.ContentType,
                        UploadedAt = DateTime.Now
                    };

                    _context.Set<AcademicRequestDocument>().Add(document);
                }
            }

            await _context.SaveChangesAsync();
        }


        [HttpGet]
        public async Task<IActionResult> GetDocumentContent(int documentId)
        {
            var document = await _context.Set<AcademicRequestDocument>()
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null || !System.IO.File.Exists(document.FilePath))
            {
                return Json(new { success = false, message = "Document not found." });
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(document.FilePath);
            var base64String = Convert.ToBase64String(fileBytes);

            return Json(new
            {
                success = true,
                fileName = document.FileName,
                contentType = document.ContentType,
                content = base64String
            });
        }
    }
}