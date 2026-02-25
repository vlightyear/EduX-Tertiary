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


[ApiController]
[Route("api/[controller]")]
public class SchoolPayController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SchoolPayController> _logger;
    private readonly IAccountingService _accountingService;
    private readonly IBackgroundEmailService _backgroundEmailService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly string _schoolPayApiKey;

    public SchoolPayController(
        ApplicationDbContext context, 
        ILogger<SchoolPayController> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IAccountingService accountingService = null,
        IBackgroundEmailService backgroundEmailService = null)
    {
        _context = context;
        _logger = logger;
        _accountingService = accountingService;
        _backgroundEmailService = backgroundEmailService;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        
        // Read API key from configuration
        _schoolPayApiKey = _configuration["SchoolPay:ApiKey"];
        
        if (string.IsNullOrEmpty(_schoolPayApiKey))
        {
            _logger.LogError("SchoolPay API Key not found in configuration. Please add 'SchoolPay:ApiKey' to appsettings.json");
            throw new InvalidOperationException("SchoolPay API Key not configured");
        }
    }

    // POST: api/SchoolPay/VerifyStudent
    [HttpPost("VerifyStudent")]
    public async Task<IActionResult> VerifyStudent([FromBody] VerifyStudentRequest request)
    {
        _logger.LogInformation("VerifyStudent called with Reg: {Reg}", request.Reg);

        if (!Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || apiKey != _schoolPayApiKey)
        {
            _logger.LogWarning("Invalid or missing API Key");
            return Unauthorized(new { Message = "Invalid API Key" });
        }

        var student = await _context.Students
            .Include(s => s.Programme)
            .FirstOrDefaultAsync(s => s.StudentId_Number == request.Reg);

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
                programme = student.Programme?.Name ?? "N/A",
                stud_campus = "Eden University", // Replace with actual campus if available
                studphone = student.Phone,
                email = student.Email,
                curBalance = student.OutstandingFees.ToString("F2") // Show actual balance
            }
        };

        return Ok(new { Message = "Valid", Data = data });
    }

    // POST: api/SchoolPay/PostPayment
    [HttpPost("PostPayment")]
    public async Task<IActionResult> PostPayment([FromBody] PaymentRequest model)
    {
        _logger.LogInformation("PostPayment called with Reg: {Reg}, RecNo: {RecNo}, Amount: {Amount}", 
            model.Reg, model.RecNo, model.Amount);

        if (!Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || apiKey != _schoolPayApiKey)
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
            PaymentMethod = "School Pay",
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

            // Queue background processing for both applicants and students
            if (payment.ApplicantId.HasValue || payment.StudentId > 0)
            {
                _logger.LogInformation("Queueing background processing for School Pay payment");
                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var services = scope.ServiceProvider;

                    try
                    {
                        var processor = new SchoolPayPaymentProcessor(
                            services.GetRequiredService<ApplicationDbContext>(),
                            services.GetRequiredService<ILogger<SchoolPayPaymentProcessor>>(),
                            services.GetService<IAccountingService>(),
                            services.GetService<IBackgroundEmailService>());

                        if (payment.ApplicantId.HasValue)
                        {
                            _logger.LogInformation("Processing applicant payment via School Pay");
                            await processor.ProcessApplicantPayment(payment);
                        }

                        if (payment.StudentId > 0)
                        {
                            _logger.LogInformation("Processing student payment via School Pay");
                            await processor.ProcessStudentPayment(payment);
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = services.GetRequiredService<ILogger<SchoolPayController>>();
                        logger.LogError(ex, "Background processing failed for School Pay payment {PaymentId}", payment.Id);
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
            _logger.LogError(ex, "Failed to save School Pay payment confirmation");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    // DTOs for incoming requests
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

// Payment processor specifically for School Pay transactions
public class SchoolPayPaymentProcessor
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SchoolPayPaymentProcessor> _logger;
    private readonly IAccountingService _accountingService;
    private readonly IBackgroundEmailService _backgroundEmailService;

    public SchoolPayPaymentProcessor(
        ApplicationDbContext context,
        ILogger<SchoolPayPaymentProcessor> logger,
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
        _logger.LogInformation("Starting School Pay applicant payment processing for transaction {TransactionId}",
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

                _logger.LogInformation("Processing School Pay application {ApplicationId} for applicant {ApplicantName}",
                    application.ApplicantId, application.FullName);

                // Post to accounting system
                if (_accountingService != null)
                {
                    try
                    {
                        _logger.LogInformation("Posting School Pay registration fee to accounting system for application {ReferenceNumber}",
                            application.ReferenceNumber);

                        var accountingResult = await _accountingService.PostRegistrationFeeAsync(
                            transaction.Amount ?? 0,
                            application.ReferenceNumber);

                        if (!accountingResult.Success)
                        {
                            _logger.LogWarning("Accounting system returned error for School Pay application {ReferenceNumber}: {Message}",
                                application.ReferenceNumber, accountingResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Accounting system error for School Pay application {ReferenceNumber}",
                            application.ReferenceNumber);
                    }
                }

                // Check if payment record already exists
                var existingAppPayment = await _context.ApplicationPayments
                    .FirstOrDefaultAsync(ap => ap.TransactionReference == transaction.MerchantTransactionId);

                if (existingAppPayment == null)
                {
                    _logger.LogInformation("Creating new School Pay application payment record");

                    var payment = new ApplicationPayment
                    {
                        ApplicationId = application.ApplicantId,
                        Amount = transaction.Amount ?? 0,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = $"School Pay ({transaction.PaymentMethod})",
                        TransactionReference = transaction.MerchantTransactionId,
                        Status = (Status)10 // Paid status
                    };

                    application.PaymentStatus = (Status)10;
                    application.IsSubmitted = true;

                    _context.ApplicationPayments.Add(payment);
                    _context.Entry(application).State = EntityState.Modified;

                    var changes = await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved {Changes} database changes for School Pay application payment", changes);
                }

                // Send email notification
                if (_backgroundEmailService != null)
                {
                    try
                    {
                        _logger.LogInformation("Queueing School Pay application submission email");
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
                        _logger.LogError(ex, "Failed to queue School Pay application email");
                    }
                }

                await transactionScope.CommitAsync();
                _logger.LogInformation("Completed School Pay applicant payment processing successfully");
            }
            catch (Exception ex)
            {
                await transactionScope.RollbackAsync();
                _logger.LogError(ex, "Error processing School Pay applicant payment");
                throw;
            }
        });
    }

    public async Task ProcessStudentPayment(OnlinePayments transaction)
    {
        _logger.LogInformation("Starting School Pay student payment processing for transaction {TransactionId}",
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
                    _logger.LogWarning("Financial statement already exists for School Pay transaction {TransactionId}",
                        transaction.MerchantTransactionId);
                    return;
                }

                _logger.LogInformation("Creating new School Pay financial statement for amount {Amount}",
                    transaction.Amount ?? 0);

                // Create financial statement
                var financialStatement = new FinancialStatement
                {
                    StudentId = student.Id,
                    AmountPaid = transaction.Amount ?? 0,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = $"School Pay ({transaction.PaymentMethod})",
                    TransactionReference = transaction.MerchantTransactionId,
                    AcademicYearId = student.AcademicYearId,
                    OutstandingAmount = student.OutstandingFees - (transaction.Amount ?? 0),
                    Semester = student.CurrentSemester ?? 1
                };

                // Update student outstanding fees
                var originalFees = student.OutstandingFees;
                student.OutstandingFees = financialStatement.OutstandingAmount;

                _logger.LogInformation("Updating student fees from {Original} to {New} via School Pay",
                    originalFees, student.OutstandingFees);

                _context.FinancialStatements.Add(financialStatement);
                _context.Students.Update(student);

                var changes = await _context.SaveChangesAsync();
                _logger.LogInformation("Saved {Changes} database changes for School Pay financial records", changes);

                // Post to accounting system
                if (_accountingService != null)
                {
                    try
                    {
                        _logger.LogInformation("Posting School Pay payment to accounting system for student {StudentId}", student.Id);
                        var accountingResult = await _accountingService.PostPaymentAsync(
                            student.StudentId_Number,
                            transaction.Amount ?? 0);

                        if (!accountingResult.Success)
                        {
                            _logger.LogWarning("Accounting system error for School Pay: {Error}", accountingResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Accounting system error for School Pay payment");
                    }
                }

                await transactionScope.CommitAsync();
                _logger.LogInformation("Completed School Pay student payment processing successfully");
            }
            catch (Exception ex)
            {
                await transactionScope.RollbackAsync();
                _logger.LogError(ex, "Error processing School Pay student payment");
                throw;
            }
        });
    }
}