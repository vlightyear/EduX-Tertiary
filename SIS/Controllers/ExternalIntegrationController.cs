using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Fees;
using SIS.Models.Payments;

namespace SIS.Controllers
{
    [ApiController]
    [Route("api/library")]
    [Authorize] // Add authentication if needed
    public class ExternalIntegrationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExternalIntegrationController> _logger;

        public ExternalIntegrationController(
            ApplicationDbContext context,
            ILogger<ExternalIntegrationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Posts a library penalty fee and creates an invoice for the student
        /// </summary>
        /// <param name="request">Penalty fee request details</param>
        /// <returns>Response with reference number</returns>
        [HttpPost("penaltyfee")]
        [ProducesResponseType(typeof(PenaltyFeeResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<IActionResult> PostPenaltyFee([FromBody] PenaltyFeeRequest request)
        {
            try
            {
                // Validate request
                if (request == null)
                {
                    return BadRequest(new ErrorResponse
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.StudentId))
                {
                    return BadRequest(new ErrorResponse
                    {
                        Success = false,
                        Message = "Student ID is required"
                    });
                }

                if (request.Amount <= 0)
                {
                    return BadRequest(new ErrorResponse
                    {
                        Success = false,
                        Message = "Amount must be greater than zero"
                    });
                }

                // Find student by student ID number
                var student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == request.StudentId);

                if (student == null)
                {
                    _logger.LogWarning("Student not found with ID: {StudentId}", request.StudentId);
                    return NotFound(new ErrorResponse
                    {
                        Success = false,
                        Message = $"Student with ID {request.StudentId} not found"
                    });
                }

                // Check if invoice with this reference already exists
                var existingInvoice = await _context.StudentInvoices
                    .FirstOrDefaultAsync(i => i.InvoiceReference == request.Reference);

                if (existingInvoice != null)
                {
                    _logger.LogWarning("Invoice with reference {Reference} already exists", request.Reference);
                    return BadRequest(new ErrorResponse
                    {
                        Success = false,
                        Message = $"Invoice with reference {request.Reference} already exists",
                        Reference = existingInvoice.InvoiceReference
                    });
                }

                // Get or create Library Penalty fee type
                var feeType = await _context.FeeTypes
                    .FirstOrDefaultAsync(ft => ft.Name == "Library Penalty Fee");

                if (feeType == null)
                {
                    feeType = new FeeType
                    {
                        Name = "Library Penalty Fee",
                        Description = "Late return penalty from Digital Library system",
                        CreatedAt = DateTime.Now.AddHours(2),
                        CreatedBy = "System"
                    };
                    _context.FeeTypes.Add(feeType);
                    await _context.SaveChangesAsync();
                }

                // Create invoice reference if not provided
                string invoiceReference = string.IsNullOrWhiteSpace(request.Reference)
                    ? $"ELIB-PENALTY-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}"
                    : request.Reference;

               

                // Create student invoice
                var invoice = new StudentInvoice
                {
                    StudentId = student.Id,
                    InvoiceReference = invoiceReference,
                    TotalAmount = request.Amount,
                    CreatedDate = DateTime.Now,
                    AcademicYearId = student.AcademicYearId,
                    Status = Status.Pending,
                    AccountingSystemPostStatus = "Pending",
                    CreatedAt = DateTime.Now.AddHours(2),
                    CreatedBy = "DigitalLibrary_System"
                };

                // Create invoice item
                var invoiceItem = new StudentInvoiceItem
                {
                    FeeTypeName = feeType.Name,
                    Description = request.Description ?? "Library late return penalty",
                    Amount = request.Amount,
                    FeeConfigurationId = null,
                    StudentInvoice = invoice
                };

                invoice.InvoiceItems.Add(invoiceItem);

                // Add invoice to context
                _context.StudentInvoices.Add(invoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Library penalty fee invoice created successfully. Student: {StudentId}, Reference: {Reference}, Amount: {Amount}",
                    request.StudentId, invoiceReference, request.Amount);

                // Return success response
                return Ok(new PenaltyFeeResponse
                {
                    Success = true,
                    Reference = invoiceReference,
                    Message = $"Penalty fee invoice created successfully for student {request.StudentId}",
                    InvoiceId = invoice.Id,
                    Amount = request.Amount,
                    Status = "Pending",
                    CreatedDate = invoice.CreatedDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating library penalty fee invoice for student {StudentId}", request?.StudentId);
                return StatusCode(500, new ErrorResponse
                {
                    Success = false,
                    Message = "An error occurred while processing the penalty fee",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets the status of a penalty fee by reference number
        /// </summary>
        /// <param name="reference">Invoice reference number</param>
        /// <returns>Penalty fee information including payment status</returns>
        [HttpGet("penaltyfee/{reference}")]
        [ProducesResponseType(typeof(PenaltyFeeInfo), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<IActionResult> GetPenaltyFee(string reference)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reference))
                {
                    return BadRequest(new ErrorResponse
                    {
                        Success = false,
                        Message = "Reference number is required"
                    });
                }

                // Find invoice by reference
                var invoice = await _context.StudentInvoices
                    .Include(i => i.Student)
                    .Include(i => i.InvoiceItems)
                    .FirstOrDefaultAsync(i => i.InvoiceReference == reference);

                if (invoice == null)
                {
                    _logger.LogWarning("Invoice not found with reference: {Reference}", reference);
                    return NotFound(new ErrorResponse
                    {
                        Success = false,
                        Message = $"Invoice with reference {reference} not found"
                    });
                }

                // Check payment status by calculating student balance
                var payments = await _context.OnlinePayments
                    .Where(p => p.StudentId == invoice.StudentId &&
                               p.Status == "Paid" &&
                               p.CreatedAt >= invoice.CreatedDate)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                decimal studentBalance = GetStudentAvailableBalance(invoice.StudentId);

                string status = "Pending";
                DateTime? paidDate = null;

                // If student has sufficient balance (positive or zero), consider invoice as paid
                if (invoice.Status == Status.Paid || studentBalance >= 0)
                {
                    status = "Paid";
                    paidDate = payments.FirstOrDefault()?.CreatedAt ?? invoice.UpdatedAt;
                }
                else
                {
                    status = invoice.Status.ToString();
                }

                var feeInfo = new PenaltyFeeInfo
                {
                    Reference = invoice.InvoiceReference,
                    StudentId = invoice.Student?.StudentId_Number ?? "",
                    Amount = invoice.TotalAmount,
                    Description = invoice.InvoiceItems.FirstOrDefault()?.Description ?? "",
                    Status = status,
                    PaidDate = paidDate,
                    Note = $"Invoice created on {invoice.CreatedDate:yyyy-MM-dd}. Academic Year ID: {invoice.AcademicYearId}"
                };

                return Ok(feeInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving penalty fee information for reference {Reference}", reference);
                return StatusCode(500, new ErrorResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving penalty fee information",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Information endpoint about invoice cancellation policy
        /// </summary>
        /// <param name="reference">Invoice reference number</param>
        /// <returns>Information about how to handle invoice cancellation</returns>
        [HttpPost("penaltyfee/{reference}/cancel")]
        [ProducesResponseType(typeof(CancellationPolicyResponse), 200)]
        public IActionResult GetCancellationPolicy(string reference)
        {
            return Ok(new CancellationPolicyResponse
            {
                Success = false,
                Message = "Invoices cannot be cancelled directly in the system. To reverse a penalty fee, a Credit Note must be created by the Finance Department.",
                Instructions = new List<string>
                {
                    "Contact the Finance Department with the invoice reference number",
                    $"Invoice Reference: {reference}",
                    "Request a Credit Note to be issued for this penalty fee",
                    "Provide justification for the credit note request",
                    "The Finance Department will process the credit note and adjust the student's account accordingly"
                },
                ContactInfo = "Please contact the Finance Department at finance@university.edu or visit the Finance Office for assistance."
            });
        }

        /// <summary>
        /// Gets student balance information
        /// </summary>
        /// <param name="studentId">Student ID number</param>
        /// <returns>Student balance details</returns>
        [HttpGet("student/{studentId}/balance")]
        [ProducesResponseType(typeof(StudentBalanceInfo), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public async Task<IActionResult> GetStudentBalance(string studentId)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == studentId);

                if (student == null)
                {
                    return NotFound(new ErrorResponse
                    {
                        Success = false,
                        Message = $"Student with ID {studentId} not found"
                    });
                }

                decimal availableBalance = GetStudentAvailableBalance(student.Id);

                var totalInvoices = await _context.StudentInvoices
                    .Where(i => i.StudentId == student.Id && i.DeletedAt == null)
                    .SumAsync(i => i.TotalAmount);

                var totalPayments = await _context.OnlinePayments
                    .Where(p => p.StudentId == student.Id && p.Status == "Paid")
                    .SumAsync(p => p.Amount ?? 0);

                var outstandingBalance = totalInvoices - totalPayments;

                var balanceInfo = new StudentBalanceInfo
                {
                    StudentId = studentId,
                    StudentName = student.FullName,
                    TotalInvoiced = totalInvoices,
                    TotalPaid = totalPayments,
                    AvailableBalance = availableBalance,
                    OutstandingBalance = outstandingBalance < 0 ? 0 : outstandingBalance,
                    CanBorrowBooks = availableBalance >= 0 // Student can borrow if balance is not negative
                };

                return Ok(balanceInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student balance for {StudentId}", studentId);
                return StatusCode(500, new ErrorResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving student balance",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Bulk check if students can borrow books (based on balance)
        /// </summary>
        /// <param name="studentIds">List of student IDs to check</param>
        /// <returns>List of students with their borrowing eligibility</returns>
        [HttpPost("students/check-eligibility")]
        [ProducesResponseType(typeof(List<StudentEligibility>), 200)]
        public async Task<IActionResult> CheckBorrowingEligibility([FromBody] List<string> studentIds)
        {
            try
            {
                var results = new List<StudentEligibility>();

                foreach (var studentId in studentIds)
                {
                    var student = await _context.Students
                        .FirstOrDefaultAsync(s => s.StudentId_Number == studentId);

                    if (student == null)
                    {
                        results.Add(new StudentEligibility
                        {
                            StudentId = studentId,
                            CanBorrow = false,
                            Reason = "Student not found"
                        });
                        continue;
                    }

                    decimal balance = GetStudentAvailableBalance(student.Id);
                    bool canBorrow = balance >= 0;

                    results.Add(new StudentEligibility
                    {
                        StudentId = studentId,
                        StudentName = student.FullName,
                        CanBorrow = canBorrow,
                        AvailableBalance = balance,
                        Reason = canBorrow ? "Eligible" : $"Outstanding balance: K{Math.Abs(balance):N2}"
                    });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking borrowing eligibility");
                return StatusCode(500, new ErrorResponse
                {
                    Success = false,
                    Message = "An error occurred while checking eligibility",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Helper method to calculate student's available balance
        /// </summary>
        private decimal GetStudentAvailableBalance(int studentId)
        {
            // Get total payments
            decimal totalPaid = _context.OnlinePayments
                .Where(op => op.StudentId == studentId && op.Status == "Paid")
                .Sum(p => p.Amount ?? 0);

            // Get total invoices (excluding soft-deleted)
            decimal totalInvoiced = _context.StudentInvoices
                .Where(si => si.StudentId == studentId && si.DeletedAt == null)
                .Sum(i => i.TotalAmount);

            return totalPaid - totalInvoiced;
        }
    }

    // ==================== DTO MODELS ====================

    public class PenaltyFeeRequest
    {
        public string StudentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? FeeType { get; set; }
        public string? DueDate { get; set; }
    }

    public class PenaltyFeeResponse
    {
        public bool Success { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class PenaltyFeeInfo
    {
        public string Reference { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? PaidDate { get; set; }
        public string? Note { get; set; }
    }

    public class ErrorResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Error { get; set; }
    }

    public class CancellationPolicyResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Instructions { get; set; } = new();
        public string ContactInfo { get; set; } = string.Empty;
    }

    public class StudentBalanceInfo
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public decimal TotalInvoiced { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal OutstandingBalance { get; set; }
        public bool CanBorrowBooks { get; set; }
    }

    public class StudentEligibility
    {
        public string StudentId { get; set; } = string.Empty;
        public string? StudentName { get; set; }
        public bool CanBorrow { get; set; }
        public decimal? AvailableBalance { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}