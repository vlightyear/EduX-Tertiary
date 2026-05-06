using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Data;
using SIS.Models.Payments;
using SIS.Services.Accounting;
using SIS.Services.Emails;
using SIS.Enums;

namespace SIS.Controllers
{
    [ApiController]
    [Route("callback")]
    public class TingCallBackController : ControllerBase
    {
        private readonly ILogger<TingCallBackController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IAccountingService _accountingService;
        private readonly IBackgroundEmailService _backgroundEmailService;

        public TingCallBackController(
            ILogger<TingCallBackController> logger,
            ApplicationDbContext context,
            IAccountingService accountingService,
            IBackgroundEmailService backgroundEmailService)
        {
            _logger = logger;
            _context = context;
            _accountingService = accountingService;
            _backgroundEmailService = backgroundEmailService;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessCallback([FromBody] JsonElement payload)
        {
            var rawPayload = payload.GetRawText();
            _logger.LogInformation("=== START OF CALLBACK ===");
            _logger.LogInformation("Raw callback payload received: {RawPayload}", rawPayload);
            
            string merchantTransactionId = string.Empty;
            
            try
            {
                if (!payload.TryGetProperty("merchant_transaction_id", out var transactionIdProp) ||
                    !payload.TryGetProperty("request_status_code", out var statusCodeProp))
                {
                    _logger.LogWarning("Missing required fields in callback payload");
                    _logger.LogInformation("=== END OF CALLBACK (ERROR) ===");
                    return BadRequest(new 
                    {
                        status_code = "400",
                        status_description = "Missing required fields (merchant_transaction_id or request_status_code)"
                    });
                }

                merchantTransactionId = transactionIdProp.GetString();
                var statusCode = statusCodeProp.GetInt32();
                var status = MapStatusCodeToStatus(statusCode);

                _logger.LogInformation("Processing callback for transaction {TransactionId} with status {StatusCode} ({Status})", 
                    merchantTransactionId, statusCode, status);

                var transaction = await _context.OnlinePayments
                    .FirstOrDefaultAsync(t => t.MerchantTransactionId == merchantTransactionId);

                if (transaction == null)
                {
                    _logger.LogWarning("Transaction not found in database: {TransactionId}", merchantTransactionId);
                    _logger.LogInformation("=== END OF CALLBACK (ERROR) ===");
                    return NotFound(new 
                    {
                        status_code = "404",
                        merchant_transaction_id = merchantTransactionId,
                        status_description = "Transaction not found"
                    });
                }

                transaction.Status = status;
                transaction.CallbackPayload = rawPayload;
                transaction.UpdatedAt = DateTime.Now;

                if (payload.TryGetProperty("amount_paid", out var amountPaidProp) &&
                    amountPaidProp.TryGetDecimal(out var amountPaid))
                {
                    transaction.Amount = amountPaid;
                    _logger.LogInformation("Amount paid: {Amount}", amountPaid);
                }

                if (status == "Paid")
                {
                    await ProcessSuccessfulPayment(transaction);
                    
                    if (transaction.ApplicantId.HasValue)
                    {
                        await ProcessApplicantPayment(transaction);
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully processed callback for transaction {TransactionId}", merchantTransactionId);

                var response = new 
                {
                    status_code = "183",
                    merchant_transaction_id = merchantTransactionId,
                    status_description = "Callback processed successfully",
                    processed_at = DateTime.Now.ToString("o")
                };

                _logger.LogInformation("Callback response: {@Response}", response);
                _logger.LogInformation("=== END OF CALLBACK (SUCCESS) ===");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment callback for {TransactionId}", merchantTransactionId);
                _logger.LogError("Raw callback payload that caused error: {RawPayload}", rawPayload);
                _logger.LogInformation("=== END OF CALLBACK (ERROR) ===");
                
                return StatusCode(500, new 
                {
                    status_code = "500",
                    merchant_transaction_id = merchantTransactionId,
                    status_description = $"Internal server error: {ex.Message}"
                });
            }
        }

        private async Task ProcessSuccessfulPayment(OnlinePayments transaction)
        {
            _logger.LogInformation("Starting payment processing for student transaction {TransactionId}", transaction.MerchantTransactionId);
            
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            
            await executionStrategy.ExecuteAsync(async () =>
            {
                using var transactionScope = await _context.Database.BeginTransactionAsync();
                try
                {
                    var student = await _context.Students
                        .Include(s => s.AcademicYear)
                        .FirstOrDefaultAsync(s => s.Id == transaction.StudentId);

                    if (student == null)
                    {
                        _logger.LogError("Student not found for StudentId: {StudentId}", transaction.StudentId);
                        return;
                    }

                    bool exists = await _context.FinancialStatements
                        .AnyAsync(fs => fs.TransactionReference == transaction.MerchantTransactionId);

                    if (exists)
                    {
                        _logger.LogWarning("Duplicate financial entry detected for TransactionReference: {TransactionReference}", 
                            transaction.MerchantTransactionId);
                        return;
                    }

                    try
                    {
                        _logger.LogInformation("Posting payment to accounting system for student {StudentNumber}", student.StudentId_Number);
                        var accountingResult = await _accountingService.PostPaymentAsync(
                            student.StudentId_Number,
                            transaction.Amount ?? 0);

                        if (!accountingResult.Success)
                        {
                            _logger.LogWarning("Accounting system returned error: {Error}", accountingResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error posting to accounting system");
                    }

                    student.OutstandingFees -= transaction.Amount ?? 0;
                    _context.Students.Update(student);
                    
                    await _context.SaveChangesAsync();
                    await transactionScope.CommitAsync();

                    _logger.LogInformation("Successfully processed payment for student {StudentId}", student.Id);
                }
                catch (Exception ex)
                {
                    await transactionScope.RollbackAsync();
                    _logger.LogError(ex, "Error processing payment for transaction {TransactionId}", transaction.MerchantTransactionId);
                    throw;
                }
            });
        }

        private async Task ProcessApplicantPayment(OnlinePayments transaction)
        {
            _logger.LogInformation("Starting applicant payment processing for transaction {TransactionId}", transaction.MerchantTransactionId);
            
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            
            await executionStrategy.ExecuteAsync(async () =>
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

                    bool exists = await _context.ApplicationPayments
                        .AnyAsync(p => p.TransactionReference == transaction.MerchantTransactionId);

                    if (exists)
                    {
                        _logger.LogWarning("Duplicate application payment detected for TransactionReference: {TransactionReference}", 
                            transaction.MerchantTransactionId);
                        return;
                    }

                    try
                    {
                        _logger.LogInformation("Posting registration fee to accounting system for application {ReferenceNumber}", 
                            application.ReferenceNumber);
                        var accountingResult = await _accountingService.PostRegistrationFeeAsync(
                            transaction.Amount ?? 0,
                            application.ReferenceNumber);

                        if (!accountingResult.Success)
                        {
                            _logger.LogWarning("Accounting system returned error: {Error}", accountingResult.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error posting to accounting system");
                    }

                    var payment = new ApplicationPayment
                    {
                        ApplicationId = application.ApplicantId,
                        Amount = transaction.Amount ?? 0,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = $"Mobile Money ({transaction.PaymentMethod})",
                        TransactionReference = transaction.MerchantTransactionId,
                        Status = Status.Paid
                    };

                    application.PaymentStatus = Status.Paid;
                    application.IsSubmitted = true;

                    _context.ApplicationPayments.Add(payment);

                    try
                    {
                        _logger.LogInformation("Queueing application submission email for {Email}", application.Email);
                        _backgroundEmailService.QueueApplicationSubmissionEmail(
                            application.FullName,
                            application.Email,
                            application.Programme?.Name ?? "N/A",
                            application.School?.Name ?? "N/A",
                            application.ReferenceNumber,
                            transaction.Amount ?? 0,
                            transaction.MerchantTransactionId
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error queueing email notification");
                    }

                    await _context.SaveChangesAsync();
                    await transactionScope.CommitAsync();
                    _logger.LogInformation("Successfully processed applicant payment for application {ApplicantId}", application.ApplicantId);
                }
                catch (Exception ex)
                {
                    await transactionScope.RollbackAsync();
                    _logger.LogError(ex, "Error processing applicant payment for transaction {TransactionId}", 
                        transaction.MerchantTransactionId);
                    throw;
                }
            });
        }

        private string MapStatusCodeToStatus(int statusCode)
        {
            var status = statusCode switch
            {
                177 => "PartiallyPaid",
                178 => "Paid",
                179 => "PartiallyPaidExpired",
                129 => "Expired",
                102 => "InsufficientFunds",
                101 => "Cancelled",
                99 => "Failed",
                160 => "Pending",
                400 => "Failed",
                _ => "Unknown"
            };

            _logger.LogDebug("Mapped status code {StatusCode} to status {Status}", statusCode, status);
            return status;
        }
    }
}