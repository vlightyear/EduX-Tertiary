using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Enums;
using SIS.Models.Payments;
using SIS.Services.Accounting;
using SIS.Services.Emails;
using SIS.Data;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SIS.Services;

namespace SIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BillMasterPaymentConfirmationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BillMasterPaymentConfirmationController> _logger;
        private readonly IAccountingService _accountingService;
        private readonly IBackgroundEmailService _backgroundEmailService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPaymentAllocationService _allocationService;

        public BillMasterPaymentConfirmationController(
            ApplicationDbContext context,
            ILogger<BillMasterPaymentConfirmationController> logger,
            IServiceScopeFactory scopeFactory,
            IPaymentAllocationService allocationService,
            IAccountingService accountingService = null,
            IBackgroundEmailService backgroundEmailService = null)
        {
            _context = context;
            _logger = logger;
            _accountingService = accountingService;
            _backgroundEmailService = backgroundEmailService;
            _scopeFactory = scopeFactory;
            _allocationService = allocationService;
        }

        [HttpPost]
        public async Task<IActionResult> Store()
        {
            string rawBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            bool isJson = Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
            JsonDocument jsonDoc = null;

            _logger.LogInformation("Raw payment confirmation request received: {RawBody}", rawBody);

            if (isJson)
            {
                jsonDoc = TryParseJson(rawBody);

                if (jsonDoc == null)
                {
                    _logger.LogWarning("Malformed JSON detected. Attempting to auto-correct...");
                    string correctedJson = AttemptJsonFix(rawBody);
                    jsonDoc = TryParseJson(correctedJson);

                    if (jsonDoc != null)
                    {
                        _logger.LogInformation("Malformed JSON successfully corrected.");
                        rawBody = correctedJson;
                    }
                    else
                    {
                        _logger.LogError("Failed to correct malformed JSON.");
                    }
                }
            }

            var data = jsonDoc?.RootElement ?? default;

            var consumerNo = data.GetPropertyOrDefault("consumerNo");
            var referenceNo = data.GetPropertyOrDefault("refrenceNo");
            var txnId = data.GetPropertyOrDefault("txnId");
            var amount = data.GetDecimalOrDefault("amount");

            _logger.LogInformation("Processing payment - Consumer: {ConsumerNo}, Ref: {ReferenceNo}, TxnId: {TxnId}, Amount: {Amount}",
                consumerNo, referenceNo, txnId, amount);

            // Look up student first to determine student ID
            _logger.LogInformation("Looking up student for consumerNo: {ConsumerNo}", consumerNo);
            var student = await _context.Students
                .FirstOrDefaultAsync(s =>
                    s.StudentId_Number == consumerNo ||
                    s.ApplicationReferenceNumber == consumerNo);

            int? currentStudentId = null;
            int? currentApplicantId = null;

            if (student != null)
            {
                _logger.LogInformation("Found student record: {StudentId}", student.Id);
                if (student.ApplicationReferenceNumber == consumerNo)
                {
                    currentApplicantId = student.Id;
                    _logger.LogInformation("Matched ApplicationReferenceNumber. ApplicantId: {Id}", student.Id);
                }
                else if (student.StudentId_Number == consumerNo)
                {
                    currentStudentId = student.Id;
                    _logger.LogInformation("Matched StudentId_Number. StudentId: {Id}", student.Id);
                }
            }
            else
            {
                _logger.LogWarning("No student record found for consumerNo: {ConsumerNo}", consumerNo);
            }

            // Check for duplicate reference
            if (!string.IsNullOrEmpty(referenceNo))
            {
                var existingPayment = await _context.OnlinePayments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(op => op.ReferenceNumber == referenceNo);

                if (existingPayment != null)
                {
                    // Check if it's for the same student
                    bool isSameStudent = (currentStudentId.HasValue && existingPayment.StudentId == currentStudentId.Value) ||
                                        (currentApplicantId.HasValue && existingPayment.ApplicantId == currentApplicantId.Value);

                    // Append timestamp to reference for any duplicate
                    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    referenceNo = $"{referenceNo}_{timestamp}_exist";
                    
                    if (isSameStudent)
                    {
                        _logger.LogInformation("Reference exists for same student. Modified reference to: {ReferenceNo}", referenceNo);
                    }
                    else
                    {
                        _logger.LogInformation("Reference exists for different student. Modified reference to: {ReferenceNo}", referenceNo);
                    }
                }
            }

            var payment = new OnlinePayments
            {
                MerchantTransactionId = txnId,
                FullName = data.GetPropertyOrDefault("consumerName"),
                CustomerFirstName = data.GetPropertyOrDefault("consumerName"),
                CustomerLastName = "",
                Msisdn = consumerNo,
                Phone = consumerNo,
                AccountNumber = consumerNo,
                Amount = amount,
                CurrencyCode = "ZMW",
                PaymentMethod = "Zanaco Bill Master",
                Status = "Paid",
                CreatedAt = DateTime.Now,
                CallbackPayload = rawBody,
                Email = data.GetPropertyOrDefault("userName"),
                ReferenceNumber = referenceNo,
                StudentId = currentStudentId ?? 0,
                ApplicantId = currentApplicantId
            };

            try
            {
                _logger.LogInformation("Saving payment to OnlinePayments");
                _context.OnlinePayments.Add(payment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Payment saved successfully. OnlinePayment ID: {Id}", payment.Id);

                var currentUser = User.Identity?.Name ?? "System";
                var result = await _allocationService.RebuildStudentAllocationsAsync(payment.StudentId, currentUser);

                if (payment.ApplicantId.HasValue || payment.StudentId > 0)
                {
                    _logger.LogInformation("Queueing background processing");
                    _ = Task.Run(async () =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var services = scope.ServiceProvider;

                        try
                        {
                            var processor = new PaymentProcessor(
                                services.GetRequiredService<ApplicationDbContext>(),
                                services.GetRequiredService<ILogger<PaymentProcessor>>(),
                                services.GetService<IAccountingService>(),
                                services.GetService<IBackgroundEmailService>());

                            if (payment.ApplicantId.HasValue)
                            {
                                _logger.LogInformation("Processing applicant payment");
                                await processor.ProcessApplicantPayment(payment);
                            }

                            if (payment.StudentId > 0)
                            {
                                _logger.LogInformation("Processing student payment");
                                await processor.ProcessSuccessfulPayment(payment);
                            }
                        }
                        catch (Exception ex)
                        {
                            var logger = services.GetRequiredService<ILogger<BillMasterPaymentConfirmationController>>();
                            logger.LogError(ex, "Background processing failed for payment {PaymentId}", payment.Id);
                        }
                    });
                }

                var response = new
                {
                    status = "SUCCESS",
                    id = payment.MerchantTransactionId ?? payment.Id.ToString(),
                    RESPONSE = "Successfully received and saved",
                    isDuplicate = false
                };

                return isJson ? Ok(response) : BuildXmlResponse(response.id, "SUCCESS", response.RESPONSE);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save payment confirmation");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        private JsonDocument TryParseJson(string json)
        {
            try
            {
                return JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private string AttemptJsonFix(string json)
        {
            var fixedJson = json.Trim();
            if (!fixedJson.StartsWith("{")) fixedJson = "{" + fixedJson;
            if (!fixedJson.EndsWith("}")) fixedJson += "}";
            return fixedJson;
        }

        private ContentResult BuildXmlResponse(string id, string status, string message)
        {
            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<PostTranResponse status=""{status}"" errorMessage=""{(status == "SUCCESS" ? "" : message)}"">
  <Transaction id=""{id}"" status=""{status}"" errorMessage=""{(status == "SUCCESS" ? "" : message)}"" />
</PostTranResponse>";

            return Content(xml, "application/xml");
        }
    }

    public class PaymentProcessor
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PaymentProcessor> _logger;
        private readonly IAccountingService _accountingService;
        private readonly IBackgroundEmailService _backgroundEmailService;

        public PaymentProcessor(
            ApplicationDbContext context,
            ILogger<PaymentProcessor> logger,
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
            _logger.LogInformation("Starting applicant payment processing for transaction {TransactionId}",
                transaction.MerchantTransactionId);

            // Use the execution strategy to handle retries and transactions
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

                    _logger.LogInformation("Processing application {ApplicationId} for applicant {ApplicantName}",
                        application.ApplicantId, application.FullName);

                    if (_accountingService != null)
                    {
                        try
                        {
                            _logger.LogInformation("Posting to accounting system for application {ReferenceNumber}",
                                application.ReferenceNumber);

                            var accountingResult = await _accountingService.PostRegistrationFeeAsync(
                                transaction.Amount ?? 0,
                                application.ReferenceNumber);

                            if (!accountingResult.Success)
                            {
                                _logger.LogWarning("Accounting system returned error for application {ReferenceNumber}: {Message}",
                                    application.ReferenceNumber, accountingResult.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Accounting system error for application {ReferenceNumber}",
                                application.ReferenceNumber);
                        }
                    }

                    var existingAppPayment = await _context.ApplicationPayments
                        .FirstOrDefaultAsync(ap => ap.TransactionReference == transaction.MerchantTransactionId);

                    if (existingAppPayment == null)
                    {
                        _logger.LogInformation("Creating new application payment record");

                        var payment = new ApplicationPayment
                        {
                            ApplicationId = application.ApplicantId,
                            Amount = transaction.Amount ?? 0,
                            PaymentDate = DateTime.Now,
                            PaymentMethod = $"Bill Master ({transaction.PaymentMethod})",
                            TransactionReference = transaction.MerchantTransactionId,
                            Status = (Status)10
                        };

                        application.PaymentStatus = (Status)10;
                        application.IsSubmitted = true;

                        _context.ApplicationPayments.Add(payment);
                        _context.Entry(application).State = EntityState.Modified;

                        var changes = await _context.SaveChangesAsync();
                        _logger.LogInformation("Saved {Changes} database changes for application payment", changes);
                    }

                    if (_backgroundEmailService != null)
                    {
                        try
                        {
                            _logger.LogInformation("Queueing application submission email");
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
                            _logger.LogError(ex, "Failed to queue application email");
                        }
                    }

                    await transactionScope.CommitAsync();
                    _logger.LogInformation("Completed applicant payment processing successfully");
                }
                catch (Exception ex)
                {
                    await transactionScope.RollbackAsync();
                    _logger.LogError(ex, "Error processing applicant payment");
                    throw;
                }
            });
        }

        public async Task ProcessSuccessfulPayment(OnlinePayments transaction)
        {
            _logger.LogInformation("Starting student payment processing for transaction {TransactionId}",
                transaction.MerchantTransactionId);

            // Use the execution strategy to handle retries and transactions
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transactionScope = await _context.Database.BeginTransactionAsync();
                try
                {
                    _logger.LogInformation("Looking up student {StudentId}", transaction.StudentId);
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

                    var existingStatement = await _context.FinancialStatements
                        .FirstOrDefaultAsync(fs => fs.TransactionReference == transaction.MerchantTransactionId);

                    if (existingStatement != null)
                    {
                        _logger.LogWarning("Financial statement already exists for transaction {TransactionId}",
                            transaction.MerchantTransactionId);
                        return;
                    }

                    _logger.LogInformation("Creating new financial statement for amount {Amount}",
                        transaction.Amount ?? 0);

                    var originalFees = student.OutstandingFees;

                    _logger.LogInformation("Updating student fees from {Original} to {New}",
                        originalFees, student.OutstandingFees);

                    _context.Students.Update(student);

                    var changes = await _context.SaveChangesAsync();

                    if (_accountingService != null)
                    {
                        try
                        {
                            _logger.LogInformation("Posting to accounting system for student {StudentId}", student.Id);
                            var accountingResult = await _accountingService.PostPaymentAsync(
                                student.StudentId_Number,
                                transaction.Amount ?? 0);

                            if (!accountingResult.Success)
                            {
                                _logger.LogWarning("Accounting system error: {Error}", accountingResult.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Accounting system error");
                        }
                    }

                    await transactionScope.CommitAsync();
                    _logger.LogInformation("Completed student payment processing successfully");
                }
                catch (Exception ex)
                {
                    await transactionScope.RollbackAsync();
                    _logger.LogError(ex, "Error processing student payment");
                    throw;
                }
            });
        }
    }

    public static class JsonExtensions
    {
        public static string GetPropertyOrDefault(this JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        public static decimal? GetDecimalOrDefault(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number)
                {
                    return value.GetDecimal();
                }
                if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsedDecimal))
                {
                    return parsedDecimal;
                }
            }
            return null;
        }
    }
}