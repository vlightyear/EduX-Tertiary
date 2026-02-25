using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Payments;

namespace SIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// Authenticates student for Digital Library
        /// GET: api/auth/authenticate?studentId=STU001&password=Student@2025
        /// </summary>
        [HttpGet("authenticate")]
        [AllowAnonymous]
        public async Task<IActionResult> Authenticate([FromQuery] string studentId, [FromQuery] string password)
        {
            try
            {
                // Validate required parameters
                if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(password))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Student ID and password are required"
                    });
                }

                // Find student by StudentId_Number
                var student = await _context.Students
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.Programme)
                    .Include(s => s.ProgrammeLevel)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == studentId);

                if (student == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Student not found"
                    });
                }

                // Check if student is registered
                if (!student.IsRegistered)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Student is not registered"
                    });
                }

                // Check if student is admitted
                if (!student.IsAdmitted)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Student is not admitted"
                    });
                }

                // Find user account
                var user = await _userManager.FindByEmailAsync(student.Email);

                if (user == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Student account not found in the system"
                    });
                }

                // Verify the user is a student
                if (!await _userManager.IsInRoleAsync(user, "Student"))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Account is not a student account"
                    });
                }

                // Check password - either default password or actual password
                string defaultPassword = _configuration["StudentAuth:DefaultPassword"] ?? "Student@2025";
                bool isAuthenticated = false;

                if (password == defaultPassword)
                {
                    isAuthenticated = true;
                }
                else
                {
                    var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, password, false);
                    isAuthenticated = passwordCheck.Succeeded;
                }

                if (!isAuthenticated)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Invalid password"
                    });
                }

                // Authentication successful - return student information with additional fields
                return Ok(new
                {
                    success = true,
                    message = "Authentication successful",
                    student = new
                    {
                        studentId = student.StudentId_Number,
                        studentName = student.FullName,
                        studentEmail = student.Email,
                        studentPhone = student.Phone,
                        institution = "Eden University",
                        modeOfStudy = student.ModeOfStudy?.ModeName ?? "N/A",
                        modeOfStudyCode = student.ModeOfStudy?.Code ?? "N/A",
                        currentSemester = student.CurrentSemester ?? 0,
                        currentYear = student.StudentCurrentYear ?? 0,
                        registrationStatus = student.RegistrationStatus.ToString(),
                        programme = student.Programme?.Name ?? "N/A",
                        programmeLevel = student.ProgrammeLevel?.Name ?? "N/A"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred during authentication",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Search students by name, email, or student ID (for autocomplete/search functionality)
        /// GET: api/auth/search?query=john&limit=10
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchStudents([FromQuery] string query, [FromQuery] int limit = 10)
        {
            try
            {
                // Validate query parameter
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Query must be at least 2 characters",
                        students = Array.Empty<object>()
                    });
                }

                // Limit the results
                if (limit <= 0) limit = 10;
                if (limit > 50) limit = 50;

                var searchQuery = query.ToLower().Trim();

                // Search students by name, email, or student ID
                var students = await _context.Students
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.Programme)
                    .Include(s => s.ProgrammeLevel)
                    .Where(s => s.IsAdmitted && s.IsRegistered &&
                           (s.FullName.ToLower().Contains(searchQuery) ||
                            s.Email.ToLower().Contains(searchQuery) ||
                            s.StudentId_Number.ToLower().Contains(searchQuery)))
                    .Take(limit)
                    .Select(s => new
                    {
                        studentId = s.StudentId_Number,
                        studentName = s.FullName,
                        profilePictureUrl = $"https://ecampus.edenuniversity.edu.zm/uploads/student-photos/{s.StudentId_Number}.png",
                        studentEmail = s.Email,
                        studentPhone = s.Phone,
                        institution = "Eden University",
                        modeOfStudy = s.ModeOfStudy != null ? s.ModeOfStudy.ModeName : "N/A",
                        modeOfStudyCode = s.ModeOfStudy != null ? s.ModeOfStudy.Code : "N/A",
                        currentSemester = s.CurrentSemester ?? 0,
                        currentYear = s.StudentCurrentYear ?? 0,
                        registrationStatus = s.RegistrationStatus.ToString(),
                        programme = s.Programme != null ? s.Programme.Name : "N/A",
                        programmeLevel = s.ProgrammeLevel != null ? s.ProgrammeLevel.Name : "N/A"
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Found {students.Count} student(s)",
                    count = students.Count,
                    students = students
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while searching students",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get student details by student ID (without password - for lookup purposes)
        /// GET: api/auth/student/STU001
        /// </summary>
        [HttpGet("student/{studentId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStudentById(string studentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(studentId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Student ID is required"
                    });
                }

                var student = await _context.Students
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.Programme)
                    .Include(s => s.ProgrammeLevel)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == studentId);

                if (student == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Student not found"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Student found",
                    student = new
                    {
                        studentId = student.StudentId_Number,
                        studentName = student.FullName,
                        profilePictureUrl = $"https://ecampus.edenuniversity.edu.zm/uploads/student-photos/{student.StudentId_Number}.png",
                        studentEmail = student.Email,
                        studentPhone = student.Phone,
                        institution = "Eden University",
                        modeOfStudy = student.ModeOfStudy?.ModeName ?? "N/A",
                        modeOfStudyCode = student.ModeOfStudy?.Code ?? "N/A",
                        currentSemester = student.CurrentSemester ?? 0,
                        currentYear = student.StudentCurrentYear ?? 0,
                        registrationStatus = student.RegistrationStatus.ToString(),
                        isRegistered = student.IsRegistered,
                        isAdmitted = student.IsAdmitted,
                        programme = student.Programme?.Name ?? "N/A",
                        programmeLevel = student.ProgrammeLevel?.Name ?? "N/A"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while fetching student details",
                    error = ex.Message
                });
            }
        }



        /// <summary>
        /// Creates an invoice for a student
        /// POST: api/auth/invoice
        /// </summary>
        [HttpPost("invoice")]
        [AllowAnonymous]
        public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request)
        {
            try
            {
                // Validate request
                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request body is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.StudentId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Student ID is required"
                    });
                }

                if (request.Items == null || !request.Items.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "At least one invoice item is required"
                    });
                }

                // Find student by StudentId_Number
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.StudentId_Number == request.StudentId);

                if (student == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Student not found"
                    });
                }

                // Get academic year if provided, otherwise use student's current academic year
                int? academicYearId = request.AcademicYearId ?? student.AcademicYearId;

                // Calculate total amount
                decimal totalAmount = request.Items.Sum(i => i.Amount);

                // Generate invoice reference
                string invoiceRef = $"INV-{request.InvoiceType ?? "GEN"}-{student.StudentId_Number}-{DateTime.Now:yyyyMMddHHmmss}";

                // Create invoice
                var invoice = new StudentInvoice
                {
                    StudentId = student.Id,
                    InvoiceReference = invoiceRef,
                    TotalAmount = totalAmount,
                    CreatedDate = DateTime.Now.AddHours(2),
                    AcademicYearId = academicYearId??0,
                    Status = Enums.Status.Pending,
                    AccountingSystemPostStatus = "Pending"
                };

                // Create invoice items
                foreach (var item in request.Items)
                {
                    var invoiceItem = new StudentInvoiceItem
                    {
                        FeeTypeName = item.FeeTypeName,
                        Description = item.Description,
                        Amount = item.Amount,
                        FeeConfigurationId = item.FeeConfigurationId,
                        StudentInvoice = invoice
                    };
                    invoice.InvoiceItems.Add(invoiceItem);
                }

                _context.StudentInvoices.Add(invoice);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Invoice created successfully",
                    invoice = new
                    {
                        invoiceId = invoice.Id,
                        invoiceReference = invoice.InvoiceReference,
                        studentId = student.StudentId_Number,
                        studentName = student.FullName,
                        totalAmount = invoice.TotalAmount,
                        status = invoice.Status.ToString(),
                        createdDate = invoice.CreatedDate,
                        items = invoice.InvoiceItems.Select(i => new
                        {
                            feeTypeName = i.FeeTypeName,
                            description = i.Description,
                            amount = i.Amount
                        })
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while creating the invoice",
                    error = ex.Message
                });
            }
        }

        // Request models for invoice creation
        public class CreateInvoiceRequest
        {
            public string StudentId { get; set; }
            public int? AcademicYearId { get; set; }
            public string InvoiceType { get; set; } // e.g., "ACCOM", "TUITION", "GEN"
            public List<InvoiceItemRequest> Items { get; set; }
        }

        public class InvoiceItemRequest
        {
            public string FeeTypeName { get; set; }
            public string Description { get; set; }
            public decimal Amount { get; set; }
            public int? FeeConfigurationId { get; set; }
        }


    }
}