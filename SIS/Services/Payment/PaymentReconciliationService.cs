using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIS.Data;
using SIS.Models.Fees;
using SIS.Models.Payments;
using SIS.Services.Accounting;
using System.Diagnostics;

namespace SIS.Services.Payment
{
    public class PaymentReconciliationService : IPaymentReconciliationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PaymentReconciliationService> _logger;
        private readonly IAccountingService _accountingService;

        public PaymentReconciliationService(
            ApplicationDbContext context,
            ILogger<PaymentReconciliationService> logger,
            IAccountingService accountingService = null)
        {
            _context = context;
            _logger = logger;
            _accountingService = accountingService;
        }

        public async Task<PaymentReconciliationResult> ReconcileUnprocessedPaymentsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new PaymentReconciliationResult();

            try
            {
                _logger.LogInformation("Starting payment reconciliation process");

                // Get all unreconciled payments (StudentId = 0 and ApplicantId is null)
                var unreconciledPayments = await _context.OnlinePayments
                    .Where(op => op.StudentId == 0 && op.ApplicantId == null)
                    .ToListAsync();

                result.TotalUnreconciledPayments = unreconciledPayments.Count;

                if (result.TotalUnreconciledPayments == 0)
                {
                    _logger.LogInformation("No unreconciled payments found");
                    stopwatch.Stop();
                    result.ProcessingTime = stopwatch.Elapsed;
                    return result;
                }

                _logger.LogInformation("Found {Count} unreconciled payments to process", result.TotalUnreconciledPayments);

                // Process payments in batches to avoid memory issues
                const int batchSize = 100;
                var batches = unreconciledPayments
                    .Select((payment, index) => new { payment, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.payment).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    await ProcessPaymentBatch(batch, result);
                }

                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;

                _logger.LogInformation("Payment reconciliation completed. Success: {Success}, Failed: {Failed}, Time: {Time}ms",
                    result.SuccessfulReconciliations, result.FailedReconciliations, result.ProcessingTime.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
                result.ErrorMessages.Add($"Critical error during reconciliation: {ex.Message}");
                _logger.LogError(ex, "Critical error during payment reconciliation");
                return result;
            }
        }

        private async Task ProcessPaymentBatch(List<OnlinePayments> batch, PaymentReconciliationResult result)
        {
            // Use the execution strategy to handle transactions with retry logic
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var payment in batch)
                    {
                        try
                        {
                            var reconciled = await ReconcileIndividualPayment(payment);
                            if (reconciled)
                            {
                                result.SuccessfulReconciliations++;
                            }
                            else
                            {
                                result.FailedReconciliations++;
                                result.ErrorMessages.Add($"Payment {payment.MerchantTransactionId}: No matching student found");
                            }
                        }
                        catch (Exception ex)
                        {
                            result.FailedReconciliations++;
                            result.ErrorMessages.Add($"Payment {payment.MerchantTransactionId}: {ex.Message}");
                            _logger.LogError(ex, "Error reconciling payment {PaymentId}", payment.Id);
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Processed batch of {Count} payments", batch.Count);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing payment batch, rolling back");

                    // Mark all payments in this batch as failed
                    foreach (var payment in batch)
                    {
                        if (!result.ErrorMessages.Any(e => e.Contains(payment.MerchantTransactionId)))
                        {
                            result.FailedReconciliations++;
                            result.ErrorMessages.Add($"Payment {payment.MerchantTransactionId}: Batch processing failed");
                        }
                    }
                    throw;
                }
            });
        }

        private async Task<bool> ReconcileIndividualPayment(OnlinePayments payment)
        {
            // Try to find student by multiple strategies
            var student = await FindMatchingStudent(payment);

            if (student == null)
            {
                _logger.LogWarning("No matching student found for payment {PaymentId} with consumer number {ConsumerNo}",
                    payment.Id, payment.Msisdn);
                return false;
            }

            // Check if this payment has already been processed
            var existingStatement = await _context.FinancialStatements
                .FirstOrDefaultAsync(fs => fs.TransactionReference == payment.MerchantTransactionId);

            if (existingStatement != null)
            {
                _logger.LogWarning("Payment {PaymentId} already has a financial statement", payment.Id);
                // Update the payment record with student info but don't create duplicate statement
                await UpdatePaymentStudentInfo(payment, student);
                return true;
            }

            // Determine if this is an applicant or student payment
            if (student.ApplicationReferenceNumber == payment.Msisdn)
            {
                // This is an application payment
                payment.ApplicantId = student.Id;
                await ProcessApplicantReconciliation(payment, student);
            }
            else if (student.StudentId_Number == payment.Msisdn)
            {
                // This is a student fee payment
                payment.StudentId = student.Id;
                await ProcessStudentReconciliation(payment, student);
            }

            _logger.LogInformation("Successfully reconciled payment {PaymentId} for student {StudentId}",
                payment.Id, student.Id);

            return true;
        }

        private async Task<SIS.Models.StudentApplication.Student> FindMatchingStudent(OnlinePayments payment)
        {
            // Strategy 1: Exact match by StudentId_Number
            var student = await _context.Students
                .Include(s => s.AcademicYear)
                .FirstOrDefaultAsync(s => s.StudentId_Number == payment.Msisdn);

            if (student != null)
            {
                return student;
            }

            // Strategy 2: Exact match by ApplicationReferenceNumber
            student = await _context.Students
                .Include(s => s.AcademicYear)
                .FirstOrDefaultAsync(s => s.ApplicationReferenceNumber == payment.Msisdn);

            if (student != null)
            {
                return student;
            }

            // Strategy 3: Try phone number matching (if different from Msisdn)
            if (!string.IsNullOrEmpty(payment.Phone) && payment.Phone != payment.Msisdn)
            {
                student = await _context.Students
                    .Include(s => s.AcademicYear)
                    .FirstOrDefaultAsync(s => s.StudentId_Number == payment.Phone ||
                                            s.ApplicationReferenceNumber == payment.Phone);
            }

            return student;
        }

        private async Task UpdatePaymentStudentInfo(OnlinePayments payment, SIS.Models.StudentApplication.Student student)
        {
            if (student.ApplicationReferenceNumber == payment.Msisdn)
            {
                payment.ApplicantId = student.Id;
            }
            else
            {
                payment.StudentId = student.Id;
            }

            _context.Entry(payment).State = EntityState.Modified;
        }

        private async Task ProcessApplicantReconciliation(OnlinePayments payment, SIS.Models.StudentApplication.Student student)
        {
            // Find the corresponding applicant record
            var applicant = await _context.Applicants
                .Include(a => a.Programme)
                .Include(a => a.School)
                .FirstOrDefaultAsync(a => a.ApplicantId == student.Id);

            if (applicant == null)
            {
                _logger.LogWarning("No applicant record found for student {StudentId}", student.Id);
                return;
            }

            // Check if application payment already exists
            var existingAppPayment = await _context.ApplicationPayments
                .FirstOrDefaultAsync(ap => ap.TransactionReference == payment.MerchantTransactionId);

            if (existingAppPayment == null)
            {
                var applicationPayment = new ApplicationPayment
                {
                    ApplicationId = applicant.ApplicantId,
                    Amount = payment.Amount ?? 0,
                    PaymentDate = payment.CreatedAt,
                    PaymentMethod = payment.PaymentMethod,
                    TransactionReference = payment.MerchantTransactionId,
                    Status = (SIS.Enums.Status)10 // Assuming 10 is the paid status
                };

                _context.ApplicationPayments.Add(applicationPayment);

                // Update applicant status
                applicant.PaymentStatus = (SIS.Enums.Status)10;
                applicant.IsSubmitted = true;
                _context.Entry(applicant).State = EntityState.Modified;
            }

            // Post to accounting system if available
            if (_accountingService != null)
            {
                try
                {
                    await _accountingService.PostRegistrationFeeAsync(
                        payment.Amount ?? 0,
                        applicant.ReferenceNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to post application payment to accounting system");
                }
            }
        }

        private async Task ProcessStudentReconciliation(OnlinePayments payment, SIS.Models.StudentApplication.Student student)
        {

            // Update student's outstanding balance
            student.OutstandingFees = Math.Max(0, student.OutstandingFees - (payment.Amount ?? 0));

            _context.Entry(student).State = EntityState.Modified;

            // Post to accounting system if available
            if (_accountingService != null)
            {
                try
                {
                    await _accountingService.PostPaymentAsync(
                        student.StudentId_Number,
                        payment.Amount ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to post student payment to accounting system");
                }
            }
        }
    }
}