using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIS.Controllers;
using SIS.Data;
using SIS.Models.Payments;
using SIS.Models.StudentApplication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class StudentTools
{
    private static IServiceScopeFactory? _scopeFactory;

    public static void Configure(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Gets the currently logged-in student by email address
    /// Call this from a controller: var student = await StudentTools.GetCurrentLoggedInStudentAsync(User.Identity.Name);
    /// </summary>
    /// <param name="userEmail">Email of the logged-in user (from User.Identity.Name or User claims)</param>
    /// <returns>Student if found and user has Student role, null otherwise</returns>
    public static async Task<Student> GetCurrentLoggedInStudentAsync(string userEmail)
    {
        if (_scopeFactory == null)
            throw new InvalidOperationException("StudentTools not configured with a valid IServiceScopeFactory.");

        if (string.IsNullOrWhiteSpace(userEmail))
            return null;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Find student by email
        var student = await context.Students
            .Include(s => s.Programme)
                .ThenInclude(p => p.Department)
                    .ThenInclude(d => d.School)
            .Include(s => s.AcademicYear)
            .Include(s => s.ModeOfStudy)
            .Include(s => s.ProgrammeLevel)
            .FirstOrDefaultAsync(s => s.Email == userEmail);

        if (student == null)
            return null;

        // Verify the user has the Student role
        var user = await userManager.FindByEmailAsync(userEmail);
        if (user == null || !await userManager.IsInRoleAsync(user, "Student"))
        {
            return null;
        }

        return student;
    }

    /// <summary>
    /// Gets a student by student ID number
    /// </summary>
    /// <param name="studentIdNumber">Student ID number (e.g., "STD001")</param>
    /// <returns>Student if found, null otherwise</returns>
    public static async Task<Student> GetStudentByIdNumberAsync(string studentIdNumber)
    {
        if (_scopeFactory == null)
            throw new InvalidOperationException("StudentTools not configured with a valid IServiceScopeFactory.");

        if (string.IsNullOrWhiteSpace(studentIdNumber))
            return null;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var student = await context.Students
            .Include(s => s.Programme)
                .ThenInclude(p => p.Department)
                    .ThenInclude(d => d.School)
            .Include(s => s.AcademicYear)
            .Include(s => s.ModeOfStudy)
            .Include(s => s.ProgrammeLevel)
            .FirstOrDefaultAsync(s => s.StudentId_Number == studentIdNumber);

        return student;
    }

    /// <summary>
    /// Gets a student by database ID
    /// </summary>
    /// <param name="studentId">Student database ID</param>
    /// <returns>Student if found, null otherwise</returns>
    public static async Task<Student> GetStudentByIdAsync(int studentId)
    {
        if (_scopeFactory == null)
            throw new InvalidOperationException("StudentTools not configured with a valid IServiceScopeFactory.");

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var student = await context.Students
            .Include(s => s.Programme)
                .ThenInclude(p => p.Department)
                    .ThenInclude(d => d.School)
            .Include(s => s.AcademicYear)
            .Include(s => s.ModeOfStudy)
            .Include(s => s.ProgrammeLevel)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        return student;
    }

    /// <summary>
    /// Gets the student's outstanding balance (Total Invoices - Total Payments)
    /// Positive = student owes money, Negative = student has credit
    /// </summary>
    public static decimal GetStudentOutstandingBalance(int studentId)
    {
        if (_scopeFactory == null)
            throw new InvalidOperationException("StudentTools not configured with a valid IServiceScopeFactory.");

        using var scope = _scopeFactory.CreateScope();
        var _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get student payments (credits)
        var paymentsQuery = _context.OnlinePayments
            .Where(op => op.StudentId == studentId && op.Status == "Paid")
            .Select(p => new UnifiedTransactionDto
            {
                Id = p.Id,
                StudentId = p.StudentId,
                Amount = p.Amount,
                Credit = true,
                Reference = p.ReferenceNumber,
                AccountingSystemPostStatus = p.AccountingSystemPostStatus,
                CreatedAt = p.CreatedAt
            });

        // Get student invoices (debits)
        var invoicesQuery = _context.StudentInvoices
            .Where(si => si.StudentId == studentId && si.DeletedAt == null)
            .Select(i => new UnifiedTransactionDto
            {
                Id = i.Id,
                StudentId = i.StudentId,
                Amount = i.TotalAmount,
                Credit = false,
                Reference = i.InvoiceReference,
                AccountingSystemPostStatus = i.AccountingSystemPostStatus,
                CreatedAt = i.CreatedDate
            });

        // Combine and sort
        var unified = paymentsQuery
            .Union(invoicesQuery)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        decimal outstandingFees = 0;

        foreach (var u in unified)
        {
            if (u.Credit)
                outstandingFees -= u.Amount ?? 0;
            else
                outstandingFees += u.Amount ?? 0;
        }

        return outstandingFees;
    }

    public static decimal GetCurrentInvoicesBalance(int studentId)
    {
        if (_scopeFactory == null)
            throw new InvalidOperationException("StudentTools not configured with a valid IServiceScopeFactory.");

        using var scope = _scopeFactory.CreateScope();
        var _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var student = _context.Students.AsNoTracking().FirstOrDefault(s => s.Id == studentId);
        // Get student invoices (debits)
        var invoices = _context.StudentInvoices
            .Where(si => si.StudentId == studentId && si.DeletedAt == null && si.AcademicYearId == student.AcademicYearId && si.YearPeriodId == student.CurrentYearPeriodId)
            .ToList();

        decimal balance = 0;

        foreach (var u in invoices)
        {
            balance += u.TotalAmount;
        }

        return balance;
    }

    /// <summary>
    /// Gets the student's available balance (Total Payments - Total Invoices)
    /// Positive = student has credit available, Negative = student owes money
    /// </summary>
    public static decimal GetStudentAvailableBalance(int studentId)
    {
        // Available balance is the inverse of outstanding balance
        return -GetStudentOutstandingBalance(studentId);
    }

    /// <summary>
    /// Gets the total fees invoiced to the student
    /// </summary>
    public static decimal GetStudentTotalFees(int studentId)
    {
        if (_scopeFactory == null)
            throw new InvalidOperationException("StudentTools not configured with a valid IServiceScopeFactory.");

        using var scope = _scopeFactory.CreateScope();
        var _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get student payments (credits)
        var paymentsQuery = _context.OnlinePayments
            .Where(op => op.StudentId == studentId && op.Status == "Paid")
            .Select(p => new UnifiedTransactionDto
            {
                Id = p.Id,
                StudentId = p.StudentId,
                Amount = p.Amount,
                Credit = true,
                Reference = p.ReferenceNumber,
                AccountingSystemPostStatus = p.AccountingSystemPostStatus,
                CreatedAt = p.CreatedAt
            });

        // Get student invoices (debits)
        var invoicesQuery = _context.StudentInvoices
            .Where(si => si.StudentId == studentId && si.DeletedAt == null)
            .Select(i => new UnifiedTransactionDto
            {
                Id = i.Id,
                StudentId = i.StudentId,
                Amount = i.TotalAmount,
                Credit = false,
                Reference = i.InvoiceReference,
                AccountingSystemPostStatus = i.AccountingSystemPostStatus,
                CreatedAt = i.CreatedDate
            });

        // Combine and sort
        var unified = paymentsQuery
            .Union(invoicesQuery)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        decimal totalFees = 0;

        foreach (var u in unified)
        {
            if (!u.Credit)
                totalFees += u.Amount ?? 0;
        }

        return totalFees;
    }

    /// <summary>
    /// Gets the total amount paid by the student
    /// </summary>
    public static decimal GetStudentTotalPaid(int studentId)
    {
        if (_scopeFactory == null)
            throw new InvalidOperationException("StudentTools not configured with a valid IServiceScopeFactory.");

        using var scope = _scopeFactory.CreateScope();
        var _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get student payments (credits)
        var paymentsQuery = _context.OnlinePayments
            .Where(op => op.StudentId == studentId && op.Status == "Paid")
            .Select(p => new UnifiedTransactionDto
            {
                Id = p.Id,
                StudentId = p.StudentId,
                Amount = p.Amount,
                Credit = true,
                Reference = p.ReferenceNumber,
                AccountingSystemPostStatus = p.AccountingSystemPostStatus,
                CreatedAt = p.CreatedAt
            });

        // Get student invoices (debits)
        var invoicesQuery = _context.StudentInvoices
            .Where(si => si.StudentId == studentId && si.DeletedAt == null)
            .Select(i => new UnifiedTransactionDto
            {
                Id = i.Id,
                StudentId = i.StudentId,
                Amount = i.TotalAmount,
                Credit = false,
                Reference = i.InvoiceReference,
                AccountingSystemPostStatus = i.AccountingSystemPostStatus,
                CreatedAt = i.CreatedDate
            });

        // Combine and sort
        var unified = paymentsQuery
            .Union(invoicesQuery)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        decimal totalPaid = 0;

        foreach (var u in unified)
        {
            if (u.Credit)
                totalPaid += u.Amount ?? 0;
        }

        return totalPaid;
    }

    /// <summary>
    /// Gets the complete financial statement for a student
    /// </summary>
    public static List<UnifiedTransactionDto> GetStudentFinancialStatement(int studentId)
    {
        if (_scopeFactory == null)
            throw new InvalidOperationException("StudentTools not configured with a valid IServiceScopeFactory.");

        using var scope = _scopeFactory.CreateScope();
        var _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get student payments
        var paymentsQuery = _context.OnlinePayments
            .Where(op => op.StudentId == studentId && op.Status == "Paid")
            .Select(p => new UnifiedTransactionDto
            {
                Id = p.Id,
                StudentId = p.StudentId,
                Amount = p.Amount,
                Credit = true,
                Reference = p.ReferenceNumber,
                AccountingSystemPostStatus = p.AccountingSystemPostStatus,
                CreatedAt = p.TransactionDate ?? p.CreatedAt
            });

        // Get student invoices
        var invoicesQuery = _context.StudentInvoices
            .Where(si => si.StudentId == studentId && si.DeletedAt == null)
            .Select(i => new UnifiedTransactionDto
            {
                Id = i.Id,
                StudentId = i.StudentId,
                Amount = i.TotalAmount,
                Credit = false,
                Reference = i.InvoiceReference,
                AccountingSystemPostStatus = i.AccountingSystemPostStatus,
                CreatedAt = i.CreatedDate
            });

        // Combine and sort chronologically
        var unified = paymentsQuery
            .Union(invoicesQuery)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        return unified;
    }


    public static List<StudentInvoice> GetStudentInvoice(int studentId)
    {
        if (_scopeFactory == null)throw new InvalidOperationException("StudentTools not configured with a valid IServiceScopeFactory.");

        using var scope = _scopeFactory.CreateScope();
        var _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get student invoices
        return _context.StudentInvoices.Where(si => si.StudentId == studentId).ToList();
    }



}