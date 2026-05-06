using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Threading.Tasks;
using SIS.Data;
using SIS.Models.Payments;
using SIS.Services.Accounting;
using SIS.Services.Emails;
using SIS.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using SIS.Services;

[ApiController]
[Route("api/[controller]")]
public class AbsaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AbsaController> _logger;
    private readonly IAccountingService _accountingService;
    private readonly IBackgroundEmailService _backgroundEmailService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly string _absaApiKey;
    private readonly IPaymentAllocationService _allocationService;

    public AbsaController(
        ApplicationDbContext context, 
        ILogger<AbsaController> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IPaymentAllocationService allocationService,
        IAccountingService accountingService = null,
        IBackgroundEmailService backgroundEmailService = null)
    {
        _context = context;
        _logger = logger;
        _accountingService = accountingService;
        _backgroundEmailService = backgroundEmailService;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _allocationService = allocationService;

        // Read API key from configuration
        _absaApiKey = _configuration["Absa:ApiKey"];
        
        if (string.IsNullOrEmpty(_absaApiKey))
        {
            _logger.LogError("Absa API Key not found in configuration. Please add 'Absa:ApiKey' to appsettings.json");
            throw new InvalidOperationException("Absa API Key not configured");
        }
    }

    // POST: api/Absa/ValidateStudent
    [HttpPost("ValidateStudent")]
    public async Task<IActionResult> ValidateStudent([FromBody] StudentValidationRequest request)
    {
        _logger.LogInformation("ValidateStudent called with userProvidedValue: {Value}, scenarioType: {Type}", 
            request.UserProvidedValue, request.ScenarioType);

        if (!Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || apiKey != _absaApiKey)
        {
            _logger.LogWarning("Invalid or missing API Key for student validation");
            return Unauthorized(new { Message = "Invalid API Key" });
        }

        try
        {
            var errors = new List<string>();
            var fields = new Dictionary<string, object>();

            // Validate scenario type if provided
            if (!string.IsNullOrEmpty(request.ScenarioType) && request.ScenarioType != "COUNTER_CODE")
            {
                errors.Add("Invalid scenario type. Only COUNTER_CODE is supported.");
            }

            // Look up student by registration number
            var student = await _context.Students
                .Include(s => s.AcademicYear)
                .Include(s => s.Programme)
                .FirstOrDefaultAsync(s => s.StudentId_Number == request.UserProvidedValue);

            if (student == null)
            {
                // Also check applicants
                var applicant = await _context.Applicants
                    .Include(a => a.Programme)
                    .FirstOrDefaultAsync(a => a.ReferenceNumber == request.UserProvidedValue);

                if (applicant == null)
                {
                    _logger.LogInformation("No student or applicant found with ID: {Value}", request.UserProvidedValue);
                    return Ok(new StudentValidationResponse
                    {
                        Success = false,
                        Message = "Invalid student data",
                        Errors = new[] { "Student or applicant not found in our system" },
                        Fields = new Dictionary<string, object>()
                    });
                }

                // Return applicant data
                fields = new Dictionary<string, object>
                {
                    ["studentId"] = applicant.ReferenceNumber,
                    ["studentName"] = applicant.FullName,
                    ["gradeLevel"] = applicant.Programme?.Name ?? "N/A"
                 
                };

                _logger.LogInformation("Applicant validated successfully: {RefNumber}", applicant.ReferenceNumber);

                return Ok(new StudentValidationResponse
                {
                    Success = true,
                    Message = "Applicant record validated successfully",
                    Errors = new string[0],
                    Fields = fields
                });
            }

            // Validate grade level for students
            var gradeLevel = student.Programme?.Name ?? "Unknown";
            if (string.IsNullOrEmpty(gradeLevel) || gradeLevel == "Unknown")
            {
                errors.Add("Grade level information not available");
            }

            // Return student data
            fields = new Dictionary<string, object>
            {
                ["studentId"] = student.StudentId_Number,
                ["studentName"] = student.FullName,
                ["gradeLevel"] = gradeLevel,
              
            };

            if (errors.Any())
            {
                _logger.LogWarning("Student validation failed for {StudentId} with errors: {Errors}", 
                    student.StudentId_Number, string.Join(", ", errors));

                return Ok(new StudentValidationResponse
                {
                    Success = false,
                    Message = "Invalid student data",
                    Errors = errors.ToArray(),
                    Fields = new Dictionary<string, object>()
                });
            }

            _logger.LogInformation("Student validated successfully: {StudentId}", student.StudentId_Number);

            return Ok(new StudentValidationResponse
            {
                Success = true,
                Message = "Student record validated successfully",
                Errors = new string[0],
                Fields = fields
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating student with ID: {Value}", request.UserProvidedValue);
            return StatusCode(500, new StudentValidationResponse
            {
                Success = false,
                Message = "Internal server error during validation",
                Errors = new[] { "System error occurred" },
                Fields = new Dictionary<string, object>()
            });
        }
    }

    // POST: api/Absa/PaymentCallback
    [HttpPost("PaymentCallback")]
    public async Task<IActionResult> PaymentCallback([FromBody] PaymentCallbackRequest request)
    {
        _logger.LogInformation("PaymentCallback received - MerchantTransactionID: {MerchantTxnId}, Amount: {Amount}, Status: {Status}", 
            request.MerchantTransactionID, request.Amount, request.StatusDescription);

        if (!Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || apiKey != _absaApiKey)
        {
            _logger.LogWarning("Invalid or missing API Key for payment callback");
            return Unauthorized(new { Message = "Invalid API Key" });
        }

        try
        {
            // Check if payment was successful
            if (request.StatusCode != 140 && request.StatusCode != 188) // 140 and 188 are success codes
            {
                _logger.LogWarning("Payment callback received with non-success status: {Status} - {Description}", 
                    request.StatusCode, request.StatusDescription);

                return Ok(new PaymentCallbackResponse
                {
                    StatusCode = "199", // Error status
                    MerchantTransactionID = request.MerchantTransactionID,
                    StatusDescription = "Payment not successful",
                    CallBackResponseID = Guid.NewGuid().ToString("N")[..8]
                });
            }

            // Check for duplicate payment using multiple reference fields
            var existingPayment = await _context.OnlinePayments
                .AsNoTracking()
                .FirstOrDefaultAsync(op => 
                    op.MerchantTransactionId == request.MerchantTransactionID ||
                    op.MerchantTransactionId == request.BeepTransactionID ||
                    op.MerchantTransactionId == request.PayerTransactionID ||
                    op.ReferenceNumber == request.Reference);

            if (existingPayment != null)
            {
                _logger.LogWarning("Duplicate payment callback detected with MerchantTransactionID: {MerchantTxnId}", 
                    request.MerchantTransactionID);

                return Ok(new PaymentCallbackResponse
                {
                    StatusCode = "188", // Success but duplicate
                    MerchantTransactionID = request.MerchantTransactionID,
                    StatusDescription = "transaction already processed",
                    CallBackResponseID = Guid.NewGuid().ToString("N")[..8]
                });
            }

            // Extract student/applicant ID from reference
            string studentRef = ExtractStudentReference(request);
            
            if (string.IsNullOrEmpty(studentRef))
            {
                _logger.LogError("Could not extract student reference from payment callback");
                return Ok(new PaymentCallbackResponse
                {
                    StatusCode = "199",
                    MerchantTransactionID = request.MerchantTransactionID,
                    StatusDescription = "Invalid student reference",
                    CallBackResponseID = Guid.NewGuid().ToString("N")[..8]
                });
            }

            // Look up student and applicant
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId_Number == studentRef);
            var applicant = await _context.Applicants.FirstOrDefaultAsync(a => a.ReferenceNumber == studentRef);

            if (student == null && applicant == null)
            {
                _logger.LogWarning("Payment callback: Neither student nor applicant found with ref: {Ref}", studentRef);
                return Ok(new PaymentCallbackResponse
                {
                    StatusCode = "199",
                    MerchantTransactionID = request.MerchantTransactionID,
                    StatusDescription = "Student/Applicant not found",
                    CallBackResponseID = Guid.NewGuid().ToString("N")[..8]
                });
            }

            // Create payment record
            var payment = new OnlinePayments
            {
                MerchantTransactionId = request.MerchantTransactionID,
                FullName = student?.FullName ?? applicant?.FullName ?? "Unknown",
                CustomerFirstName = ExtractFirstName(student?.FullName ?? applicant?.FullName ?? ""),
                CustomerLastName = ExtractLastName(student?.FullName ?? applicant?.FullName ?? ""),
                Msisdn = studentRef,
                Phone = studentRef,
                AccountNumber = studentRef,
                Amount = request.Amount,
                CurrencyCode = request.CurrencyCode ?? "ZMW",
                PaymentMethod = $"Absa",
                RequestPayload = JsonSerializer.Serialize(request),
                ResponsePayload = "",
                Status = "Paid",
                CreatedAt = DateTime.TryParse(request.PaymentDate, out var payDate) ? payDate : DateTime.Now,
                CallbackPayload = JsonSerializer.Serialize(request),
                ReferenceNumber = request.Reference
            };

            // Set appropriate IDs
            if (student != null)
            {
                payment.StudentId = student.Id;
                _logger.LogInformation("Payment linked to student: {StudentId}", student.Id);
            }
            
            if (applicant != null)
            {
                payment.ApplicantId = applicant.ApplicantId;
                _logger.LogInformation("Payment linked to applicant: {ApplicantId}", applicant.ApplicantId);
            }

            // Save payment
            _context.OnlinePayments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment callback saved successfully. OnlinePayment ID: {Id}", payment.Id);

            var currentUser = User.Identity?.Name ?? "System";
            var result = await _allocationService.RebuildStudentAllocationsAsync(payment.StudentId, currentUser);

            // Queue background processing
            if (payment.ApplicantId.HasValue || payment.StudentId > 0)
            {
                _logger.LogInformation("Queueing background processing for payment callback");
                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var services = scope.ServiceProvider;

                    try
                    {
                        var processor = new AbsaPaymentProcessor(
                            services.GetRequiredService<ApplicationDbContext>(),
                            services.GetRequiredService<ILogger<AbsaPaymentProcessor>>(),
                            services.GetService<IAccountingService>(),
                            services.GetService<IBackgroundEmailService>());

                        if (payment.ApplicantId.HasValue)
                        {
                            await processor.ProcessApplicantPayment(payment);
                        }

                        if (payment.StudentId > 0)
                        {
                            await processor.ProcessStudentPayment(payment);
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = services.GetRequiredService<ILogger<AbsaController>>();
                        logger.LogError(ex, "Background processing failed for payment callback {PaymentId}", payment.Id);
                    }
                });
            }

            var callbackResponseId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("Payment callback processed successfully with response ID: {ResponseId}", callbackResponseId);

            return Ok(new PaymentCallbackResponse
            {
                StatusCode = "188", // Success
                MerchantTransactionID = request.MerchantTransactionID,
                StatusDescription = "transaction acknowledged successfully",
                CallBackResponseID = callbackResponseId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment callback");
            return Ok(new PaymentCallbackResponse
            {
                StatusCode = "199", // Error
                MerchantTransactionID = request.MerchantTransactionID ?? "unknown",
                StatusDescription = "internal server error",
                CallBackResponseID = Guid.NewGuid().ToString("N")[..8]
            });
        }
    }

    // POST: api/SchoolPay/VerifyStudent (Existing endpoint - kept for backward compatibility)
    [HttpPost("VerifyStudent")]
    public async Task<IActionResult> VerifyStudent([FromBody] VerifyStudentRequest request)
    {
        _logger.LogInformation("VerifyStudent called with Reg: {Reg}", request.Reg);

        if (!Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || apiKey !=  _absaApiKey)
        {
            _logger.LogWarning("Invalid or missing API Key");
            return Unauthorized(new { Message = "Invalid API Key" });
        }

        var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId_Number == request.Reg);

        if (student == null)
        {
            _logger.LogInformation("Student not found with Reg: {Reg}", request.Reg);
            return Ok(new { Message = "Invalid" });
        }

        _logger.LogInformation("Student found with Reg: {Reg}", request.Reg);

        var data = new[]
        {
            new
            {
                regno = student.StudentId_Number,
                studnames = student.FullName,
                stud_campus = "Eden University",
                studphone = student.Phone,
                email = student.Email,
                curBalance = student.OutstandingFees.ToString("F2")
            }
        };

        return Ok(new { Message = "Valid", Data = data });
    }

    // POST: api/SchoolPay/PostPayment (Existing endpoint - kept for backward compatibility)
    [HttpPost("PostPayment")]
    public async Task<IActionResult> PostPayment([FromBody] PaymentRequest model)
    {
        _logger.LogInformation("PostPayment called with Reg: {Reg}, RecNo: {RecNo}, Amount: {Amount}", 
            model.Reg, model.RecNo, model.Amount);

        if (!Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || apiKey !=  _absaApiKey)
        {
            _logger.LogWarning("Invalid or missing API Key");
            return Unauthorized(new { Message = "Invalid API Key" });
        }

        // Check for duplicate payment using RecNo as reference
        if (!string.IsNullOrEmpty(model.RecNo))
        {
            var existingPayment = await _context.OnlinePayments
                .AsNoTracking()
                .FirstOrDefaultAsync(op => op.MerchantTransactionId == model.RecNo || op.ReferenceNumber == model.RecNo);

            if (existingPayment != null)
            {
                _logger.LogWarning("Duplicate payment detected with RecNo: {RecNo}", model.RecNo);
                return Ok(new { 
                    Message = "Payment already processed", 
                    isDuplicate = true,
                    TransactionId = existingPayment.MerchantTransactionId ?? existingPayment.Id.ToString()
                });
            }
        }

        // Look up student and applicant
        var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId_Number == model.Reg);
        var applicant = await _context.Applicants.FirstOrDefaultAsync(a => a.ReferenceNumber == model.Reg);

        if (student == null && applicant == null)
        {
            _logger.LogWarning("Payment rejected: Neither student nor applicant found with Reg: {Reg}", model.Reg);
            return BadRequest(new { Message = "Student/Applicant not found in our system." });
        }

        var names = (model.StudName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var payment = new OnlinePayments
        {
            MerchantTransactionId = model.RecNo,
            FullName = model.StudName,
            CustomerFirstName = names.Length > 0 ? names[0] : "",
            CustomerLastName = names.Length > 1 ? names[^1] : "",
            Msisdn = model.Msisdn,
            Phone = model.Phone,
            AccountNumber = model.Reg,
            Amount = model.Amount,
            CurrencyCode = "ZMW",
            PaymentMethod = "Absa",
            RequestPayload = JsonSerializer.Serialize(model),
            ResponsePayload = JsonSerializer.Serialize(new { Message = "Data Capture Complete" }),
            Status = "Paid",
            CreatedAt = DateTime.Now,
            CallbackPayload = JsonSerializer.Serialize(model),
            ReferenceNumber = model.RecNo
        };

        // Set appropriate IDs
        if (student != null)
        {
            payment.StudentId = student.Id;
            _logger.LogInformation("Found student record: {StudentId}", student.Id);
        }
        
        if (applicant != null)
        {
            payment.ApplicantId = applicant.ApplicantId;
            _logger.LogInformation("Found applicant record: {ApplicantId}", applicant.ApplicantId);
        }

        try
        {
            _logger.LogInformation("Saving payment to OnlinePayments");
            _context.OnlinePayments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment saved successfully. OnlinePayment ID: {Id}", payment.Id);

            var currentUser = User.Identity?.Name ?? "System";
            var result = await _allocationService.RebuildStudentAllocationsAsync(payment.StudentId, currentUser);

            // Queue background processing for both applicants and students
            if (payment.ApplicantId.HasValue || payment.StudentId > 0)
            {
                _logger.LogInformation("Queueing background processing for Absa payment");
                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var services = scope.ServiceProvider;

                    try
                    {
                        var processor = new AbsaPaymentProcessor(
                            services.GetRequiredService<ApplicationDbContext>(),
                            services.GetRequiredService<ILogger<AbsaPaymentProcessor>>(),
                            services.GetService<IAccountingService>(),
                            services.GetService<IBackgroundEmailService>());

                        if (payment.ApplicantId.HasValue)
                        {
                            _logger.LogInformation("Processing applicant payment via Absa");
                            await processor.ProcessApplicantPayment(payment);
                        }

                        if (payment.StudentId > 0)
                        {
                            _logger.LogInformation("Processing student payment via Absa");
                            await processor.ProcessStudentPayment(payment);
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = services.GetRequiredService<ILogger<AbsaController>>();
                        logger.LogError(ex, "Background processing failed for Absa payment {PaymentId}", payment.Id);
                    }
                });
            }

            return Ok(new { 
                Message = "Data Capture Complete",
                TransactionId = payment.MerchantTransactionId ?? payment.Id.ToString(),
                isDuplicate = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Absa payment confirmation");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    // Helper methods
    private string ExtractStudentReference(PaymentCallbackRequest request)
    {
        // First priority: Check extraInformation.reference (this is where the student number is)
        if (request.ExtraInformation?.Reference != null && !string.IsNullOrEmpty(request.ExtraInformation.Reference))
            return request.ExtraInformation.Reference;
            
        // Fallback: Try to extract student reference from other fields
        if (!string.IsNullOrEmpty(request.Reference))
            return request.Reference;
            
        // You might need to extract from other fields based on your implementation
        // For example, if reference is embedded in narration
        if (!string.IsNullOrEmpty(request.Narration))
        {
            // Add logic to extract student ID from narration if needed
        }
        
        return string.Empty;
    }

    private string ExtractFirstName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "";
        var names = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return names.Length > 0 ? names[0] : "";
    }

    private string ExtractLastName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "";
        var names = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return names.Length > 1 ? names[^1] : "";
    }

    // DTOs for new endpoints
    public class StudentValidationRequest
    {
        public string UserProvidedValue { get; set; }
        public string CountryCode { get; set; }
        public int ClientId { get; set; }
        public Dictionary<string, object> ExtraData { get; set; }
        public string ScenarioType { get; set; }
        public string ScenarioValue { get; set; }
    }

    public class StudentValidationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string[] Errors { get; set; }
        public Dictionary<string, object> Fields { get; set; }
    }

    public class ExtraInformation
    {
        public string Reference { get; set; }
        public string ProductCode { get; set; }
        public string CounterCode { get; set; }
        public string StoreCode { get; set; }
    }

    public class PaymentCallbackRequest
    {
        public string MerchantName { get; set; }
        public int ? StoreCode { get; set; }
        public string ?  StoreName { get; set; }
        public int ? CounterCode { get; set; }
        public string? CounterName { get; set; }
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; }
        public string Reference { get; set; }
        public string BeepTransactionID { get; set; }
        public string PayerTransactionID { get; set; }
        public int StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public string Msisdn { get; set; }
        public string Narration { get; set; }
        public int MerchantNotificationID { get; set; }
        public string MerchantTransactionID { get; set; }
        public string PaymentDate { get; set; }
        public ExtraInformation ? ExtraInformation { get; set; }
    }

    public class PaymentCallbackResponse
    {
        public string StatusCode { get; set; }
        public string MerchantTransactionID { get; set; }
        public string StatusDescription { get; set; }
        public string CallBackResponseID { get; set; }
    }

    // Existing DTOs (kept for backward compatibility)
    public class VerifyStudentRequest
    {
        public string Reg { get; set; }
    }

    public class PaymentRequest
    {
        public string Reg { get; set; }  // StudentId_Number or ApplicationReferenceNumber
        public string RecNo { get; set; }
        public DateTime PayDate { get; set; }
        public decimal Amount { get; set; }
        public string ChannelDetail { get; set; }
        public string PaySource { get; set; }
        public string StudName { get; set; }
        public string PayCode { get; set; }
        public string TransCompStatus { get; set; }
        public string Msisdn { get; set; }
        public string Phone { get; set; }
    }
}

// Payment processor specifically for Absa transactions
public class AbsaPaymentProcessor
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AbsaPaymentProcessor> _logger;
    private readonly IAccountingService _accountingService;
    private readonly IBackgroundEmailService _backgroundEmailService;

    public AbsaPaymentProcessor(
        ApplicationDbContext context,
        ILogger<AbsaPaymentProcessor> logger,
        IAccountingService accountingService,
        IBackgroundEmailService backgroundEmailService)
    {
        _context = context;
        _logger = logger;
        _accountingService = accountingService;
        _backgroundEmailService = backgroundEmailService;
    }

    public async Task ProcessApplicantPayment(OnlinePayments transaction)
    {
        _logger.LogInformation("Starting Absa applicant payment processing for transaction {TransactionId}",
            transaction.MerchantTransactionId);

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transactionScope = await _context.Database.BeginTransactionAsync();
            try
            {
                var application = await _context.Applicants
                    .Include(a => a.Programme)
                    .Include(a => a.School)
                    .FirstOrDefaultAsync(a => a.ApplicantId == transaction.ApplicantId);

                if (application == null)
                {
                    _logger.LogError("Application not found for ApplicantId: {ApplicantId}", transaction.ApplicantId);
                    return;
                }

                _logger.LogInformation("Processing Absa application {ApplicationId} for applicant {ApplicantName}",
                    application.ApplicantId, application.FullName);

                // Post to accounting system
                if (_accountingService != null)
                {
                    try
                    {
                        _logger.LogInformation("Posting Absa registration fee to accounting system for application {ReferenceNumber}",
                            application.ReferenceNumber);

                        var accountingResult = await _accountingService.PostRegistrationFeeAsync(
                            transaction.Amount ?? 0,
                            application.ReferenceNumber);

                        if (!accountingResult.Success)
                        {
                            _logger.LogWarning("Accounting system returned error for Absa application {ReferenceNumber}: {Message}",
                                application.ReferenceNumber, accountingResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Accounting system error for Absa application {ReferenceNumber}",
                            application.ReferenceNumber);
                    }
                }

                // Check if payment record already exists
                var existingAppPayment = await _context.ApplicationPayments
                    .FirstOrDefaultAsync(ap => ap.TransactionReference == transaction.MerchantTransactionId);

                if (existingAppPayment == null)
                {
                    _logger.LogInformation("Creating new Absa application payment record");

                    var payment = new ApplicationPayment
                    {
                        ApplicationId = application.ApplicantId,
                        Amount = transaction.Amount ?? 0,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = $"Absa ({transaction.PaymentMethod})",
                        TransactionReference = transaction.MerchantTransactionId,
                        Status = (Status)10 // Paid status
                    };

                    application.PaymentStatus = (Status)10;
                    application.IsSubmitted = true;

                    _context.ApplicationPayments.Add(payment);
                    _context.Entry(application).State = EntityState.Modified;

                    var changes = await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved {Changes} database changes for Absa application payment", changes);
                }

                // Send email notification
                if (_backgroundEmailService != null)
                {
                    try
                    {
                        _logger.LogInformation("Queueing Absa application submission email");
                        _backgroundEmailService.QueueApplicationSubmissionEmail(
                            application.FullName,
                            application.Email,
                            application.Programme?.Name ?? "N/A",
                            application.School?.Name ?? "N/A",
                            application.ReferenceNumber,
                            transaction.Amount ?? 0,
                            transaction.MerchantTransactionId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to queue Absa application email");
                    }
                }

                await transactionScope.CommitAsync();
                _logger.LogInformation("Completed Absa applicant payment processing successfully");
            }
            catch (Exception ex)
            {
                await transactionScope.RollbackAsync();
                _logger.LogError(ex, "Error processing Absa applicant payment");
                throw;
            }
        });
    }

    public async Task ProcessStudentPayment(OnlinePayments transaction)
    {
        _logger.LogInformation("Starting Absa student payment processing for transaction {TransactionId}",
            transaction.MerchantTransactionId);

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transactionScope = await _context.Database.BeginTransactionAsync();
            try
            {
                var student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.Id == transaction.StudentId);

                if (student == null)
                {
                    _logger.LogError("Student not found for ID: {StudentId}", transaction.StudentId);
                    return;
                }

                _logger.LogInformation("Found student {StudentId} with outstanding fees: {OutstandingFees}",
                    student.Id, student.OutstandingFees);

                // Check if financial statement already exists
                var existingStatement = await _context.FinancialStatements
                    .FirstOrDefaultAsync(fs => fs.TransactionReference == transaction.MerchantTransactionId);

                if (existingStatement != null)
                {
                    _logger.LogWarning("Financial statement already exists for Absa transaction {TransactionId}",
                        transaction.MerchantTransactionId);
                    return;
                }

                _logger.LogInformation("Creating new Absa financial statement for amount {Amount}",
                    transaction.Amount ?? 0);

                // Update student outstanding fees
                var originalFees = student.OutstandingFees;
                student.OutstandingFees = StudentTools.GetStudentOutstandingBalance(student.Id);

                _logger.LogInformation("Updating student fees from {Original} to {New} via Absa",
                    originalFees, student.OutstandingFees);

                _context.Students.Update(student);

                var changes = await _context.SaveChangesAsync();
                _logger.LogInformation("Saved {Changes} database changes for Absa financial records", changes);

                // Post to accounting system
                if (_accountingService != null)
                {
                    try
                    {
                        _logger.LogInformation("Posting Absa payment to accounting system for student {StudentId}", student.Id);
                        var accountingResult = await _accountingService.PostPaymentAsync(
                            student.StudentId_Number,
                            transaction.Amount ?? 0);

                        if (!accountingResult.Success)
                        {
                            _logger.LogWarning("Accounting system error for Absa: {Error}", accountingResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Accounting system error for Absa payment");
                    }
                }

                await transactionScope.CommitAsync();
                _logger.LogInformation("Completed Absa student payment processing successfully");
            }
            catch (Exception ex)
            {
                await transactionScope.RollbackAsync();
                _logger.LogError(ex, "Error processing Absa student payment");
                throw;
            }
        });
    }
}