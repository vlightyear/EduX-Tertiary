using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    [Authorize]
    public class DocumentViewerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public DocumentViewerController(ApplicationDbContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }

        public async Task<IActionResult> ViewPdf(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return BadRequest("File path is required");
                }

                // Remove any leading slashes or "~/" from the path
                filePath = filePath.TrimStart('~', '/');

                // Security check: Ensure the file path is a PDF
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension != ".pdf")
                {
                    return BadRequest("Only PDF files are supported");
                }

                // Get the web root path (wwwroot)
                string webRootPath = _hostingEnvironment.WebRootPath;

                // Try different potential file locations
                string[] possiblePaths = new[]
                {
            // Option 1: File is in wwwroot directory
            Path.Combine(webRootPath, filePath),
            
            // Option 2: File is in a directory outside wwwroot
            Path.Combine(Directory.GetCurrentDirectory(), filePath),
            
            // Option 3: File is in a content directory
            Path.Combine(Directory.GetCurrentDirectory(), "Content", filePath),
            
            // Option 4: File is directly at the path specified
            filePath
        };

                // Find the first path that exists
                string fullPath = possiblePaths.FirstOrDefault(p => System.IO.File.Exists(p));

                if (fullPath == null)
                {
                    // Log all paths that were checked
                    Console.WriteLine($"[ERROR] {DateTime.Now} - PDF file not found at any of these locations:");
                    foreach (var path in possiblePaths)
                    {
                        Console.WriteLine($"- {path}");
                    }

                    return NotFound("File not found. Please check the file path.");
                }

                // Pass the file path to the view
                ViewBag.FilePath = filePath;

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {DateTime.Now} - Error viewing PDF file: {filePath}");
                Console.WriteLine($"[ERROR] {DateTime.Now} - Exception: {ex.Message}");
                return RedirectToAction("Error", "Home");
            }
        }

        // Alternative approach: stream the PDF directly if needed
        public async Task<IActionResult> GetPdfContent(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return BadRequest("File path is required");
                }

                // Normalize and validate path as above
                string normalizedPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), filePath));
                if (!normalizedPath.StartsWith(Directory.GetCurrentDirectory()) ||
                    Path.GetExtension(normalizedPath).ToLowerInvariant() != ".pdf")
                {
                    return BadRequest("Invalid file path");
                }

                if (!System.IO.File.Exists(normalizedPath))
                {
                    return NotFound("File not found");
                }

                // Return the file as a stream
                var fileStream = new FileStream(normalizedPath, FileMode.Open, FileAccess.Read);
                return File(fileStream, "application/pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {DateTime.Now} - Error streaming PDF file: {filePath}");
                Console.WriteLine($"[ERROR] {DateTime.Now} - Exception: {ex.Message}");
                return StatusCode(500, "Error retrieving PDF file");
            }
        }
    }
}