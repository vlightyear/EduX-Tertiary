using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Utilities;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Payments;
using SIS.Models.StudentApplication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SIS.Services
{
    public interface IPaymentAllocationService
    {
        Task<PaymentAllocationResult> AllocatePaymentAsync(int paymentId, string allocatedBy);
        Task<List<PaymentAllocation>> GetPaymentAllocationsAsync(int paymentId);
        Task<List<PaymentAllocation>> GetInvoiceAllocationsAsync(int invoiceId);
        Task<decimal> GetInvoiceRemainingBalanceAsync(int invoiceId);
        Task<bool> ReversePaymentAllocationAsync(int paymentId, string reversedBy, string reason);
        Task<PaymentAllocationRebuildResult> RebuildStudentAllocationsAsync(int studentId, string rebuiltBy);
    }

    public class PaymentAllocationService : IPaymentAllocationService
    {
        private readonly ApplicationDbContext _context;

        public PaymentAllocationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentAllocationResult> AllocatePaymentAsync(int paymentId, string allocatedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get the payment
                var payment = await _context.OnlinePayments
                    .Include(p => p.PaymentAllocations)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null)
                {
                    return new PaymentAllocationResult
                    {
                        Success = false,
                        Message = "Payment not found"
                    };
                }

                // Check if payment is already allocated
                if (payment.PaymentAllocations.Any())
                {
                    return new PaymentAllocationResult
                    {
                        Success = false,
                        Message = "Payment has already been allocated. Please reverse the allocation first if you need to reallocate."
                    };
                }

                // Validate payment status
                if (payment.Status != "Paid")
                {
                    return new PaymentAllocationResult
                    {
                        Success = false,
                        Message = $"Payment status is '{payment.Status}'. Only 'Paid' payments can be allocated."
                    };
                }

                if (!payment.Amount.HasValue || payment.Amount.Value <= 0)
                {
                    return new PaymentAllocationResult
                    {
                        Success = false,
                        Message = "Payment amount is invalid"
                    };
                }

                decimal remainingPaymentAmount = payment.Amount.Value;
                var allocations = new List<PaymentAllocation>();
                int allocationSequence = 1;

                // Get all pending and partially paid invoices for the student, ordered by CreatedDate ASC
                var invoices = await _context.StudentInvoices
                    .Include(i => i.PaymentAllocations)
                    .Where(i => i.StudentId == payment.StudentId &&
                               i.Status != Status.Paid &&
                               i.DeletedAt == null)
                    .OrderBy(i => i.CreatedDate)
                    .ToListAsync();

                if (!invoices.Any())
                {
                    return new PaymentAllocationResult
                    {
                        Success = false,
                        Message = "No outstanding invoices found for this student"
                    };
                }

                foreach (var invoice in invoices)
                {
                    if (remainingPaymentAmount <= 0)
                        break;

                    // Calculate current invoice balance
                    decimal invoiceBalance = invoice.TotalAmount - invoice.PaymentAllocations.Sum(pa => pa.AllocatedAmount);

                    if (invoiceBalance <= 0)
                        continue; // Invoice is already fully paid

                    // Determine allocation amount
                    decimal allocationAmount = Math.Min(remainingPaymentAmount, invoiceBalance);

                    // Create allocation record
                    var allocation = new PaymentAllocation
                    {
                        OnlinePaymentId = payment.Id,
                        StudentInvoiceId = invoice.Id,
                        AllocatedAmount = allocationAmount,
                        InvoiceBalanceBeforeAllocation = invoiceBalance,
                        InvoiceBalanceAfterAllocation = invoiceBalance - allocationAmount,
                        AllocationSequence = allocationSequence++,
                        AllocatedAt = DateTime.Now,
                        AllocatedBy = allocatedBy,
                        Notes = $"Auto-allocated from payment {payment.MerchantTransactionId}"
                    };

                    _context.PaymentAllocations.Add(allocation);
                    allocations.Add(allocation);

                    // Update invoice status
                    decimal newBalance = invoiceBalance - allocationAmount;
                    if (newBalance <= 0)
                    {
                        invoice.Status = Status.Paid;
                        invoice.UpdatedAt = DateTime.Now;
                        invoice.UpdatedBy = allocatedBy;
                    }
                    else if (newBalance < invoice.TotalAmount)
                    {
                        invoice.Status = Status.PartiallyPaid;
                        invoice.UpdatedAt = DateTime.Now;
                        invoice.UpdatedBy = allocatedBy;
                    }

                    remainingPaymentAmount -= allocationAmount;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = new PaymentAllocationResult
                {
                    Success = true,
                    Message = $"Payment allocated successfully to {allocations.Count} invoice(s)",
                    Allocations = allocations,
                    TotalAllocated = payment.Amount.Value - remainingPaymentAmount,
                    UnallocatedAmount = remainingPaymentAmount
                };

                if (remainingPaymentAmount > 0)
                {
                    result.Message += $". Unallocated amount: K {remainingPaymentAmount:N2}";
                }

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new PaymentAllocationResult
                {
                    Success = false,
                    Message = $"Error allocating payment: {ex.Message}"
                };
            }
        }

        public async Task<List<PaymentAllocation>> GetPaymentAllocationsAsync(int paymentId)
        {
            return await _context.PaymentAllocations
                .Include(pa => pa.StudentInvoice)
                .Where(pa => pa.OnlinePaymentId == paymentId)
                .OrderBy(pa => pa.AllocationSequence)
                .ToListAsync();
        }

        public async Task<List<PaymentAllocation>> GetInvoiceAllocationsAsync(int invoiceId)
        {
            return await _context.PaymentAllocations
                .Include(pa => pa.OnlinePayment)
                .Where(pa => pa.StudentInvoiceId == invoiceId)
                .OrderBy(pa => pa.AllocatedAt)
                .ToListAsync();
        }

        public async Task<decimal> GetInvoiceRemainingBalanceAsync(int invoiceId)
        {
            var invoice = await _context.StudentInvoices
                .Include(i => i.PaymentAllocations)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                return 0;

            return invoice.TotalAmount - invoice.PaymentAllocations.Sum(pa => pa.AllocatedAmount);
        }

        public async Task<bool> ReversePaymentAllocationAsync(int paymentId, string reversedBy, string reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var allocations = await _context.PaymentAllocations
                    .Include(pa => pa.StudentInvoice)
                    .Where(pa => pa.OnlinePaymentId == paymentId)
                    .OrderByDescending(pa => pa.AllocationSequence)
                    .ToListAsync();

                if (!allocations.Any())
                    return false;

                // Reverse allocations in reverse order
                foreach (var allocation in allocations)
                {
                    var invoice = allocation.StudentInvoice;

                    // Recalculate invoice status
                    decimal currentBalance = invoice.TotalAmount -
                        invoice.PaymentAllocations
                            .Where(pa => pa.Id != allocation.Id)
                            .Sum(pa => pa.AllocatedAmount);

                    if (currentBalance >= invoice.TotalAmount)
                    {
                        invoice.Status = Status.Pending;
                    }
                    else if (currentBalance > 0)
                    {
                        invoice.Status = Status.PartiallyPaid;
                    }
                    else
                    {
                        invoice.Status = Status.Paid;
                    }

                    invoice.UpdatedAt = DateTime.Now;
                    invoice.UpdatedBy = reversedBy;

                    _context.PaymentAllocations.Remove(allocation);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<PaymentAllocationRebuildResult> RebuildStudentAllocationsAsync(int studentId, string rebuiltBy)
        {
            var result = new PaymentAllocationRebuildResult
            {
                StudentId = studentId,
                Success = false
            };

            try
            {
                // Use the execution strategy to handle the transaction
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        result.ProcessingLog.Add($"Starting allocation rebuild for student ID: {studentId}");
                        result.ProcessingLog.Add("=".PadRight(80, '='));

                        // Step 1: Remove all existing allocations for this student
                        var existingAllocations = await _context.PaymentAllocations
                            .Include(pa => pa.StudentInvoice)
                            .Where(pa => pa.StudentInvoice.StudentId == studentId)
                            .ToListAsync();

                        result.AllocationsRemoved = existingAllocations.Count;
                        _context.PaymentAllocations.RemoveRange(existingAllocations);
                        await _context.SaveChangesAsync();

                        result.ProcessingLog.Add($"Removed {result.AllocationsRemoved} existing allocation(s)");
                        result.ProcessingLog.Add("");

                        // Step 2: Load all invoices for the student
                        var allInvoices = await _context.StudentInvoices
                            .Include(i => i.OnlinePayments)
                            .Where(i => i.StudentId == studentId && i.DeletedAt == null)
                            .OrderBy(i => i.CreatedDate)
                            .ToListAsync();

                        result.ProcessingLog.Add($"Loaded {allInvoices.Count} invoice(s)");

                        // Step 3: Load all payments for the student
                        var allPayments = await _context.OnlinePayments
                            .Where(p => p.StudentId == studentId &&
                                       (p.Status == "Completed" || p.Status == "Paid"))
                            .OrderBy(p => p.TransactionDate ?? p.CreatedAt)
                            .ToListAsync();

                        result.ProcessingLog.Add($"Loaded {allPayments.Count} payment(s)");
                        result.ProcessingLog.Add("");

                        // Step 4: Separate credit notes, debit notes, regular invoices, and regular payments
                        var creditNotes = allPayments
                            .Where(p => p.PaymentMethod?.Equals("Credit Note", StringComparison.OrdinalIgnoreCase) == true
                                       && p.StudentInvoiceId.HasValue)
                            .OrderBy(p => p.TransactionDate ?? p.CreatedAt)
                            .ToList();

                        var debitNoteInvoices = allInvoices
                             .Where(i => i.TransactionType?.Equals("DN", StringComparison.OrdinalIgnoreCase) == true
                               && i.OnlinePayments != null
                               && i.OnlinePayments.Any())
                            .OrderBy(i => i.CreatedDate)
                            .ToList();

                        var regularInvoices = allInvoices
                            .Where(i => i.TransactionType?.Equals("DN", StringComparison.OrdinalIgnoreCase) != true
                               && (i.OnlinePayments == null
                               || !i.OnlinePayments.Any()))
                            .ToList();

                        var regularPayments = allPayments
                            .Where(p => p.PaymentMethod?.Equals("Credit Note", StringComparison.OrdinalIgnoreCase) != true || !p.StudentInvoiceId.HasValue)
                            .ToList();

                        result.ProcessingLog.Add($"Identified:");
                        result.ProcessingLog.Add($"  - {creditNotes.Count} credit note(s)");
                        result.ProcessingLog.Add($"  - {debitNoteInvoices.Count} debit note(s)");
                        result.ProcessingLog.Add($"  - {regularInvoices.Count} regular invoice(s)");
                        result.ProcessingLog.Add($"  - {regularPayments.Count} regular payment(s)");
                        result.ProcessingLog.Add("");

                        // Step 5: Prepare processable invoices with initial balances
                        var processableInvoices = regularInvoices
                            .Select(i => new ProcessableInvoice
                            {
                                Id = i.Id,
                                InvoiceReference = i.InvoiceReference,
                                OriginalAmount = i.TotalAmount,
                                CreditNotesApplied = 0,
                                EffectiveAmount = i.TotalAmount,
                                RemainingBalance = i.TotalAmount,
                                CreatedDate = i.CreatedDate,
                                IsDebitNote = false,
                                Invoice = i
                            })
                            .OrderBy(pi => pi.CreatedDate)
                            .ToList();

                        // Step 6: Prepare processable payments with initial balances
                        var processablePayments = regularPayments
                            .Select(p => new ProcessablePayment
                            {
                                Id = p.Id,
                                MerchantTransactionId = p.MerchantTransactionId,
                                OriginalAmount = p.Amount ?? 0,
                                DebitNotesApplied = 0,
                                EffectiveAmount = p.Amount ?? 0,
                                RemainingAmount = p.Amount ?? 0,
                                TransactionDate = p.TransactionDate ?? p.CreatedAt,
                                IsCreditNote = false,
                                Payment = p
                            })
                            .OrderBy(pp => pp.TransactionDate)
                            .ToList();

                        int allocationSequence = 1;
                        var newAllocations = new List<PaymentAllocation>();

                        // ============================================================================
                        // PHASE 1: Process Credit Notes First (Specific Invoice Adjustments)
                        // ============================================================================
                        result.ProcessingLog.Add("PHASE 1: PROCESSING CREDIT NOTES");
                        result.ProcessingLog.Add("-".PadRight(80, '-'));

                        foreach (var creditNote in creditNotes)
                        {
                            var targetInvoice = processableInvoices.FirstOrDefault(i => i.Id == creditNote.StudentInvoiceId.Value);

                            if (targetInvoice == null)
                            {
                                result.ProcessingLog.Add($"⚠ Credit Note {creditNote.MerchantTransactionId} (K{creditNote.Amount:N2}) - Target invoice #{creditNote.StudentInvoiceId} not found or is a debit note");
                                continue;
                            }

                            if (targetInvoice.RemainingBalance <= 0)
                            {
                                result.ProcessingLog.Add($"⚠ Credit Note {creditNote.MerchantTransactionId} (K{creditNote.Amount:N2}) - Invoice {targetInvoice.InvoiceReference} already fully paid");
                                continue;
                            }

                            decimal creditAmount = creditNote.Amount ?? 0;
                            decimal allocationAmount = Math.Min(creditAmount, targetInvoice.RemainingBalance);

                            // Create allocation for credit note
                            var allocation = new PaymentAllocation
                            {
                                OnlinePaymentId = creditNote.Id,
                                StudentInvoiceId = targetInvoice.Id,
                                AllocatedAmount = allocationAmount,
                                InvoiceBalanceBeforeAllocation = targetInvoice.RemainingBalance,
                                InvoiceBalanceAfterAllocation = targetInvoice.RemainingBalance - allocationAmount,
                                AllocationSequence = allocationSequence++,
                                AllocatedAt = DateTime.Now,
                                AllocatedBy = rebuiltBy,
                                Notes = $"Credit Note allocation to specific invoice"
                            };

                            _context.PaymentAllocations.Add(allocation);
                            newAllocations.Add(allocation);

                            // Update invoice tracking
                            targetInvoice.CreditNotesApplied += allocationAmount;
                            targetInvoice.RemainingBalance -= allocationAmount;
                            result.TotalCreditNotesApplied += allocationAmount;

                            result.ProcessingLog.Add($"✓ Credit Note {creditNote.MerchantTransactionId} → Invoice {targetInvoice.InvoiceReference}: K{allocationAmount:N2} (Invoice balance: K{targetInvoice.RemainingBalance:N2})");
                        }

                        result.ProcessingLog.Add($"Total Credit Notes Applied: K{result.TotalCreditNotesApplied:N2}");
                        result.ProcessingLog.Add("");

                        // ============================================================================
                        // PHASE 2: Process Debit Notes First (Specific Payment Adjustments)
                        // ============================================================================
                        result.ProcessingLog.Add("PHASE 2: PROCESSING DEBIT NOTES");
                        result.ProcessingLog.Add("-".PadRight(80, '-'));

                        foreach (var debitNote in debitNoteInvoices)
                        {
                            // Find payments that reference this debit note
                            var affectedPayments = processablePayments
                                .Where(p => p.Payment.StudentInvoiceId == debitNote.Id)
                                .ToList();

                            if (!affectedPayments.Any())
                            {
                                result.ProcessingLog.Add($"⚠ Debit Note {debitNote.InvoiceReference} (K{debitNote.TotalAmount:N2}) - No payments reference this debit note");
                                continue;
                            }

                            decimal debitAmount = debitNote.TotalAmount;
                            decimal remainingDebitAmount = debitAmount;

                            result.ProcessingLog.Add($"Processing Debit Note {debitNote.InvoiceReference} (K{debitAmount:N2}):");

                            foreach (var payment in affectedPayments.OrderBy(p => p.TransactionDate))
                            {
                                if (remainingDebitAmount <= 0)
                                    break;

                                if (payment.RemainingAmount <= 0)
                                {
                                    result.ProcessingLog.Add($"  ⚠ Payment {payment.MerchantTransactionId} already fully allocated");
                                    continue;
                                }

                                decimal allocationAmount = Math.Min(remainingDebitAmount, payment.RemainingAmount);

                                // Create allocation for debit note (negative allocation to payment)
                                var allocation = new PaymentAllocation
                                {
                                    OnlinePaymentId = payment.Id,
                                    StudentInvoiceId = debitNote.Id,
                                    AllocatedAmount = allocationAmount,
                                    InvoiceBalanceBeforeAllocation = debitNote.TotalAmount,
                                    InvoiceBalanceAfterAllocation = 0, // Debit notes don't have remaining balance
                                    AllocationSequence = allocationSequence++,
                                    AllocatedAt = DateTime.Now,
                                    AllocatedBy = rebuiltBy,
                                    Notes = $"Debit Note reducing payment availability"
                                };

                                _context.PaymentAllocations.Add(allocation);
                                newAllocations.Add(allocation);

                                // Update payment tracking
                                payment.DebitNotesApplied += allocationAmount;
                                payment.RemainingAmount -= allocationAmount;
                                remainingDebitAmount -= allocationAmount;
                                result.TotalDebitNotesApplied += allocationAmount;

                                result.ProcessingLog.Add($"  ✓ Payment {payment.MerchantTransactionId}: K{allocationAmount:N2} (Payment remaining: K{payment.RemainingAmount:N2})");
                            }

                            if (remainingDebitAmount > 0)
                            {
                                result.ProcessingLog.Add($"  ⚠ Debit Note has unallocated amount: K{remainingDebitAmount:N2}");
                            }
                        }

                        result.ProcessingLog.Add($"Total Debit Notes Applied: K{result.TotalDebitNotesApplied:N2}");
                        result.ProcessingLog.Add("");

                        // ============================================================================
                        // PHASE 3: Chronological Allocation of Remaining Payments to Remaining Invoices
                        // ============================================================================
                        result.ProcessingLog.Add("PHASE 3: CHRONOLOGICAL ALLOCATION OF REMAINING BALANCES");
                        result.ProcessingLog.Add("-".PadRight(80, '-'));

                        // Filter to only invoices and payments with remaining balances
                        var availableInvoices = processableInvoices
                            .Where(i => i.RemainingBalance > 0)
                            .OrderBy(i => i.CreatedDate)
                            .ToList();

                        var availablePayments = processablePayments
                            .Where(p => p.RemainingAmount > 0)
                            .OrderBy(p => p.TransactionDate)
                            .ToList();

                        result.ProcessingLog.Add($"Available for allocation:");
                        result.ProcessingLog.Add($"  - {availableInvoices.Count} invoice(s) with outstanding balance: K{availableInvoices.Sum(i => i.RemainingBalance):N2}");
                        result.ProcessingLog.Add($"  - {availablePayments.Count} payment(s) with available amount: K{availablePayments.Sum(p => p.RemainingAmount):N2}");
                        result.ProcessingLog.Add("");

                        foreach (var payment in availablePayments)
                        {
                            if (payment.RemainingAmount <= 0)
                                continue;

                            result.ProcessingLog.Add($"Allocating Payment {payment.MerchantTransactionId} (Available: K{payment.RemainingAmount:N2}):");
                            bool paymentAllocated = false;

                            foreach (var invoice in availableInvoices)
                            {
                                if (payment.RemainingAmount <= 0)
                                    break;

                                if (invoice.RemainingBalance <= 0)
                                    continue;

                                // Calculate allocation amount
                                decimal allocationAmount = Math.Min(payment.RemainingAmount, invoice.RemainingBalance);

                                // Create allocation
                                var allocation = new PaymentAllocation
                                {
                                    OnlinePaymentId = payment.Id,
                                    StudentInvoiceId = invoice.Id,
                                    AllocatedAmount = allocationAmount,
                                    InvoiceBalanceBeforeAllocation = invoice.RemainingBalance,
                                    InvoiceBalanceAfterAllocation = invoice.RemainingBalance - allocationAmount,
                                    AllocationSequence = allocationSequence++,
                                    AllocatedAt = DateTime.Now,
                                    AllocatedBy = rebuiltBy,
                                    Notes = $"Chronological allocation. Invoice: K{invoice.OriginalAmount:N2} (Credits: K{invoice.CreditNotesApplied:N2}). Payment: K{payment.OriginalAmount:N2} (Debits: K{payment.DebitNotesApplied:N2})"
                                };

                                _context.PaymentAllocations.Add(allocation);
                                newAllocations.Add(allocation);

                                // Update remaining amounts
                                payment.RemainingAmount -= allocationAmount;
                                invoice.RemainingBalance -= allocationAmount;
                                paymentAllocated = true;

                                result.ProcessingLog.Add($"  ✓ → Invoice {invoice.InvoiceReference}: K{allocationAmount:N2} (Invoice: K{invoice.RemainingBalance:N2}, Payment: K{payment.RemainingAmount:N2})");
                            }

                            if (!paymentAllocated)
                            {
                                result.ProcessingLog.Add($"  ⚠ No invoices available for allocation");
                            }
                            else if (payment.RemainingAmount > 0)
                            {
                                result.ProcessingLog.Add($"  ℹ Unallocated amount remaining: K{payment.RemainingAmount:N2}");
                            }
                        }

                        result.ProcessingLog.Add("");
                        result.AllocationsCreated = newAllocations.Count;

                        // ============================================================================
                        // PHASE 4: Update Invoice Statuses
                        // ============================================================================
                        result.ProcessingLog.Add("PHASE 4: UPDATING INVOICE STATUSES");
                        result.ProcessingLog.Add("-".PadRight(80, '-'));

                        foreach (var invoice in processableInvoices)
                        {
                            var oldStatus = invoice.Invoice.Status;

                            if (invoice.RemainingBalance <= 0)
                            {
                                invoice.Invoice.Status = Status.Paid;
                            }
                            else if (invoice.RemainingBalance < invoice.EffectiveAmount)
                            {
                                invoice.Invoice.Status = Status.PartiallyPaid;
                            }
                            else
                            {
                                invoice.Invoice.Status = Status.Pending;
                            }

                            invoice.Invoice.UpdatedAt = DateTime.Now;
                            invoice.Invoice.UpdatedBy = rebuiltBy;

                            if (oldStatus != invoice.Invoice.Status)
                            {
                                result.ProcessingLog.Add($"Invoice {invoice.InvoiceReference}: {oldStatus} → {invoice.Invoice.Status} (Balance: K{invoice.RemainingBalance:N2})");
                            }
                        }

                        result.ProcessingLog.Add("");
                        result.InvoicesProcessed = processableInvoices.Count;
                        result.PaymentsProcessed = processablePayments.Count;

                        // Save all changes
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // ============================================================================
                        // Final Summary
                        // ============================================================================
                        result.ProcessingLog.Add("=".PadRight(80, '='));
                        result.ProcessingLog.Add("REBUILD SUMMARY");
                        result.ProcessingLog.Add("=".PadRight(80, '='));
                        result.ProcessingLog.Add($"Allocations removed: {result.AllocationsRemoved}");
                        result.ProcessingLog.Add($"Allocations created: {result.AllocationsCreated}");
                        result.ProcessingLog.Add($"Invoices processed: {result.InvoicesProcessed}");
                        result.ProcessingLog.Add($"Payments processed: {result.PaymentsProcessed}");
                        result.ProcessingLog.Add($"Credit notes applied: K{result.TotalCreditNotesApplied:N2}");
                        result.ProcessingLog.Add($"Debit notes applied: K{result.TotalDebitNotesApplied:N2}");
                        result.ProcessingLog.Add("");
                        result.ProcessingLog.Add($"Total invoice amount: K{processableInvoices.Sum(i => i.OriginalAmount):N2}");
                        result.ProcessingLog.Add($"Total allocated: K{processableInvoices.Sum(i => i.OriginalAmount - i.RemainingBalance):N2}");
                        result.ProcessingLog.Add($"Total outstanding: K{processableInvoices.Sum(i => i.RemainingBalance):N2}");

                        result.Success = true;
                        result.Message = $"Successfully rebuilt allocations for student {studentId}. Created {result.AllocationsCreated} allocation(s).";
                        result.NewAllocations = newAllocations;
                        result.ProcessingLog.Add("Rebuild completed successfully ✓");
                    }
                    catch (Exception innerEx)
                    {
                        await transaction.RollbackAsync();
                        throw; // Re-throw to be caught by outer catch
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error rebuilding allocations: {ex.Message}";
                result.ProcessingLog.Add("");
                result.ProcessingLog.Add("=".PadRight(80, '='));
                result.ProcessingLog.Add("ERROR OCCURRED");
                result.ProcessingLog.Add("=".PadRight(80, '='));
                result.ProcessingLog.Add($"Error: {ex.Message}");
                result.ProcessingLog.Add($"Stack trace: {ex.StackTrace}");
                return result;
            }
        }
    }

    public class PaymentAllocationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<PaymentAllocation> Allocations { get; set; } = new();
        public decimal TotalAllocated { get; set; }
        public decimal UnallocatedAmount { get; set; }
    }

    public class PaymentAllocationRebuildResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int StudentId { get; set; }
        public int AllocationsRemoved { get; set; }
        public int AllocationsCreated { get; set; }
        public int InvoicesProcessed { get; set; }
        public int PaymentsProcessed { get; set; }
        public decimal TotalCreditNotesApplied { get; set; }
        public decimal TotalDebitNotesApplied { get; set; }
        public List<string> ProcessingLog { get; set; } = new();
        public List<PaymentAllocation> NewAllocations { get; set; } = new();
    }
}