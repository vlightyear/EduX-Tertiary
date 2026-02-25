// Services/Accounting/StudentInvoiceService.cs
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Accounting;
using SIS.Models.Fees;
using SIS.Enums;
using SIS.Models.StudentApplication;
using SIS.Models.Payments;

namespace SIS.Services.Accounting
{
    public class StudentInvoiceService : IStudentInvoiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAccountingService _accountingService;
        private readonly ILogger<StudentInvoiceService> _logger;

        public StudentInvoiceService(
            ApplicationDbContext context,
            IAccountingService accountingService,
            ILogger<StudentInvoiceService> logger)
        {
            _context = context;
            _accountingService = accountingService;
            _logger = logger;
        }

        public async Task<AccountingApiResponse> GenerateStudentInvoiceAsync(int studentId)
        {
            try
            {
                // Get student information with all necessary includes
                var student = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.School)
                    .Include(s => s.ModeOfStudy)
                    .Include(s => s.AcademicYear)
                    .Include(s => s.ProgrammeLevel)
                    .Include(s => s.StudentAddress)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    return new AccountingApiResponse
                    {
                        Success = false,
                        Message = "Student not found"
                    };
                }

                return await GenerateStudentInvoiceAsync(student);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student for invoice generation: {StudentId}", studentId);
                return new AccountingApiResponse
                {
                    Success = false,
                    Message = $"An error occurred while retrieving student information: {ex.Message}"
                };
            }
        }

        public async Task<AccountingApiResponse> GenerateStudentInvoiceAsync(Student student)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var dbTransaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Check for existing VALID invoice
                    var existingInvoice = await _context.StudentInvoices
                        .Include(si => si.InvoiceItems)
                        .FirstOrDefaultAsync(si => si.StudentId == student.Id &&
                                                 si.AcademicYearId == student.AcademicYearId &&
                                                 si.DeletedAt == null &&
                                                 (student.Programme.IsSemesterBased == false || si.Semester == student.CurrentSemester));

                    if (existingInvoice != null)
                    {
                        bool isValidInvoice = existingInvoice.TotalAmount > 0 &&
                                             existingInvoice.InvoiceItems.Any() &&
                                             existingInvoice.Status != Status.Canceled;

                        if (isValidInvoice)
                        {
                            await dbTransaction.CommitAsync();
                            _logger.LogInformation($"Valid invoice already exists for student {student.StudentId_Number}");
                            return new AccountingApiResponse
                            {
                                Success = true,
                                Message = "Valid invoice already exists for this student",
                                TransactionId = existingInvoice.InvoiceReference
                            };
                        }
                        else
                        {
                            _logger.LogWarning($"Found incomplete invoice for student {student.StudentId_Number}. Removing and recreating.");

                            if (existingInvoice.InvoiceItems.Any())
                            {
                                _context.StudentInvoiceItems.RemoveRange(existingInvoice.InvoiceItems);
                            }
                            _context.StudentInvoices.Remove(existingInvoice);
                            await _context.SaveChangesAsync();
                        }
                    }

                    // Get all applicable fees
                    var applicableFees = await GetApplicableFees(student);

                    if (!applicableFees.Any())
                    {
                        await dbTransaction.RollbackAsync();
                        _logger.LogWarning($"No applicable fees found for student {student.StudentId_Number}");
                        return new AccountingApiResponse
                        {
                            Success = false,
                            Message = "Could not create invoice because there are no fee configurations that match your criteria"
                        };
                    }

                    var totalAmount = applicableFees.Sum(f => f.Amount);

                    if (totalAmount <= 0)
                    {
                        await dbTransaction.RollbackAsync();
                        _logger.LogWarning($"Total fee amount is zero for student {student.StudentId_Number}");
                        return new AccountingApiResponse
                        {
                            Success = false,
                            Message = "Total fee amount cannot be zero"
                        };
                    }

                    // Generate unique invoice reference
                    var today = DateTime.Now.Date;
                    var semesterSuffix = student.Programme?.IsSemesterBased == true && student.CurrentSemester.HasValue
                        ? $"-S{student.CurrentSemester}"
                        : "";
                    var baseInvoiceReference = $"INV-{today:yyyyMMdd}-{student.StudentId_Number}{semesterSuffix}";

                    var invoiceReference = baseInvoiceReference;
                    int suffix = 1;
                    while (await _context.StudentInvoices.AnyAsync(si => si.InvoiceReference == invoiceReference))
                    {
                        invoiceReference = $"{baseInvoiceReference}-{suffix:D2}";
                        suffix++;
                    }

                    var address = student.StudentAddress != null
                        ? $"{student.StudentAddress.AddressLine1}, {student.StudentAddress.AddressLine2}, {student.StudentAddress.City}, {student.StudentAddress.State}, {student.StudentAddress.Country}".Trim(' ', ',')
                        : "Address not provided";

                    // 🎯 NEW APPROACH: Try to post to accounting, but don't fail if it doesn't work
                    string accountingPostStatus = "Pending";
                    string accountingMessage = string.Empty;

                    try
                    {
                        var accountingResult = await _accountingService.PostStudentInvoiceAsync(
                            studentId: student.StudentId_Number,
                            studentName: student.FullName,
                            address: address,
                            email: student.Email,
                            phone: student.Phone,
                            totalAmount: totalAmount,
                            fees: applicableFees
                        );

                        if (accountingResult.Success)
                        {
                            accountingPostStatus = "Posted";
                            accountingMessage = "Successfully posted to accounting system";
                            _logger.LogInformation($"Successfully posted invoice to accounting system for student {student.StudentId_Number}");
                        }
                        else
                        {
                            accountingPostStatus = "Failed";
                            accountingMessage = $"Accounting post failed: {accountingResult.Message}";
                            _logger.LogWarning($"Failed to post to accounting system for student {student.StudentId_Number}: {accountingResult.Message}");
                        }
                    }
                    catch (Exception accountingEx)
                    {
                        accountingPostStatus = "Failed";
                        accountingMessage = $"Accounting system error: {accountingEx.Message}";
                        _logger.LogError(accountingEx, $"Error calling accounting service for student {student.StudentId_Number}");
                    }

                    // 🎯 CONTINUE WITH INVOICE CREATION REGARDLESS OF ACCOUNTING RESULT
                    try
                    {
                        // Create the main invoice record
                        var studentInvoice = new StudentInvoice
                        {
                            StudentId = student.Id,
                            InvoiceReference = invoiceReference,
                            TotalAmount = totalAmount,
                            CreatedDate = DateTime.Now,
                            AcademicYearId = student.AcademicYearId,
                            Semester = student.Programme?.IsSemesterBased == true ? student.CurrentSemester : null,
                            Status = Status.Pending,
                            AccountingSystemPostStatus = accountingPostStatus // ✅ Track the status
                        };

                        _context.StudentInvoices.Add(studentInvoice);
                        await _context.SaveChangesAsync(); // Save to get the ID

                        // Create invoice items
                        var invoiceItems = new List<StudentInvoiceItem>();
                        foreach (var fee in applicableFees)
                        {
                            if (fee.FeeType == null)
                            {
                                _logger.LogWarning($"Fee configuration {fee.Id} has null FeeType for student {student.StudentId_Number}");
                                continue;
                            }

                            var invoiceItem = new StudentInvoiceItem
                            {
                                StudentInvoiceId = studentInvoice.Id,
                                FeeTypeName = fee.FeeType.Name,
                                Description = fee.FeeType.Description ?? fee.FeeType.Name,
                                Amount = fee.Amount,
                                FeeConfigurationId = fee.Id
                            };

                            invoiceItems.Add(invoiceItem);
                            _context.StudentInvoiceItems.Add(invoiceItem);
                        }

                        await _context.SaveChangesAsync();
                        await dbTransaction.CommitAsync();

                        // Log appropriate message based on accounting status
                        var statusMessage = accountingPostStatus == "Posted"
                            ? "and posted to accounting system"
                            : "but accounting post failed (will retry later)";

                        _logger.LogInformation($"Successfully created invoice {invoiceReference} for student {student.StudentId_Number} with {invoiceItems.Count} items totaling {totalAmount:C2} {statusMessage}");

                        return new AccountingApiResponse
                        {
                            Success = true, // ✅ Invoice was created successfully
                            Message = accountingPostStatus == "Posted"
                                ? $"Invoice created successfully with {invoiceItems.Count} fee items and posted to accounting"
                                : $"Invoice created successfully with {invoiceItems.Count} fee items (accounting post pending: {accountingMessage})",
                            TransactionId = invoiceReference
                        };
                    }
                    catch (Exception dbEx)
                    {
                        await dbTransaction.RollbackAsync();
                        _logger.LogError(dbEx, $"Database error while saving invoice for student {student.StudentId_Number}");
                        return new AccountingApiResponse
                        {
                            Success = false,
                            Message = $"Database error while creating invoice: {dbEx.Message}"
                        };
                    }
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogError(ex, "Error generating student invoice for student ID: {StudentId}", student.Id);
                    return new AccountingApiResponse
                    {
                        Success = false,
                        Message = $"An error occurred: {ex.Message}"
                    };
                }
            });
        }

        // COMPLETELY UPDATED: Enhanced fee calculation logic
        private async Task<List<FeeConfiguration>> GetApplicableFees(Student student)
        {
            // Check if student has active accommodation allocations
            bool hasActiveAllocation = await _context.Allocations
                .AnyAsync(a => a.ApplicationId == student.Id &&
                             a.Status == Status.Active &&
                             a.EndDate >= DateTime.Now);

            // CRITICAL FIX: Include fees where AcademicYearId is NULL (applies to all years)
            // OR where it matches the student's academic year
            var fees = await _context.FeeConfigurations
                .AsNoTracking()
                .Include(f => f.FeeType)
                .Include(f => f.Programme) // Add this include to check IsSemesterBased
                .Where(f => (/*f.AcademicYearId == null ||*/ f.AcademicYearId == student.AcademicYearId) &&
                            f.FeeType.ApplicableFor == "Student")
                .ToListAsync();

            // Filter by semester if the student's programme is semester-based
            if (student.Programme?.IsSemesterBased == true && student.CurrentSemester.HasValue)
            {
                fees = fees.Where(f =>
                    //f.Semester == null ||  // Fee applies to whole year (both semesters)
                    f.Semester == student.CurrentSemester.Value  // Fee applies to current semester
                ).ToList();
            }
            else
            {
                // For yearly programmes, only include fees that are yearly (Semester is null)
                fees = fees.Where(f => f.Semester == null).ToList();
            }

            // Filter fees based on various criteria
            var filteredFees = fees.Where(f =>
                // Check for accommodation status
                (f.AppliesOnlyToAccommodated == false ||
                 student.HasAccommodationClearance == true ||
                 hasActiveAllocation) &&

                // Check for foreign student status
                (((f.AppliesOnlyToForeignStudents == true && student.IsForeigner == true) ||
                  (f.AppliesOnlyToLocalStudents == true && student.IsForeigner == false) ||
                  (f.AppliesOnlyToLocalStudents == false && f.AppliesOnlyToForeignStudents == false))) &&

                // Then check other criteria
                ((
                    (f.Semester == null || f.Semester == student.CurrentSemester) &&
                    (f.ProgrammeId == null || f.ProgrammeId == student.ProgrammeId) &&
                    (f.ModeOfStudyId == null || f.ModeOfStudyId == student.ModeOfStudyId) &&
                    (f.YearOfStudy == null || f.YearOfStudy == student.StudentCurrentYear) &&
                    (f.AcademicYearId == null || f.AcademicYearId == student.AcademicYearId)
                ))
            ).ToList();

            var deduplicatedFees = DeduplicateFees(filteredFees);

            return deduplicatedFees;
        }

        /*private List<FeeConfiguration> DeduplicateFees(List<FeeConfiguration> fees)
        {
            // Group by FeeTypeId and select the most specific fee for each type
            var deduplicatedFees = fees
                .GroupBy(f => f.FeeTypeId)
                .Select(group =>
                {
                    // If there's only one fee of this type, return it
                    if (group.Count() == 1)
                        return group.First();

                    // Otherwise, pick the most specific one based on specificity score
                    return group.OrderByDescending(f => CalculateSpecificityScore(f)).First();
                })
                .ToList();

            return deduplicatedFees;
        }*/
        private List<FeeConfiguration> DeduplicateFees(List<FeeConfiguration> fees)
        {
            return fees
                .GroupBy(f => f.FeeTypeId)
                .Select(group =>
                    group
                        .OrderByDescending(f => f.Amount)                     // primary rule (SQL ORDER BY Amount DESC)
                        .ThenByDescending(f => CalculateSpecificityScore(f))   // tie-breaker
                        .First()
                )
                .ToList();
        }

        private int CalculateSpecificityScore(FeeConfiguration fee)
        {
            int score = 0;

            // More specific criteria get higher scores
            if (fee.ProgrammeId != null) score += 10000;  // Programme-specific is highest priority
            if (fee.Semester != null) score += 1000;      // Semester-specific is high priority
            if (fee.YearOfStudy != null) score += 100;    // Year-specific
            if (fee.SchoolId != null) score += 10;        // School-specific
            if (fee.ModeOfStudyId != null) score += 5;    // Mode of study specific
            if (fee.ProgramLevelId != null) score += 1;   // Program level specific

            return score;
        }
    }
}