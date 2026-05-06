using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIS.Data;
using SIS.Models.ViewModels;

namespace SIS.Controllers
{
    [Authorize(Roles = "VC,DVC,Registrar,Admin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(ApplicationDbContext context, ILogger<ReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                await LoadReportsData(fromDate, toDate);
                return View("~/Views/FinanceReports/Index.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports data");
                TempData["Error"] = "An error occurred while loading reports data.";
                return RedirectToAction("Index", "Home");
            }
        }

        public async Task<IActionResult> StudentStats(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                await LoadReportsData(fromDate, toDate);
                return View("~/Views/FinanceReports/StudentStats.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports data");
                TempData["Error"] = "An error occurred while loading reports data.";
                return RedirectToAction("Index", "Home");
            }
        }

        private async Task LoadReportsData(DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Initialize defaults
            ViewBag.TotalRevenue = 0;
            ViewBag.TotalOutstanding = 0;
            ViewBag.StudentsWithOutstanding = 0;
            ViewBag.AverageOutstanding = 0;
            ViewBag.CollectionRate = 100;
            ViewBag.Transactions = "[]";
            ViewBag.OutstandingStudents = "[]";

            try
            {
                // Build date filter query
                var transactionQuery = _context.OnlinePayments
                    .Include(fs => fs.Student)
                        .ThenInclude(s => s.Programme)
                    .AsQueryable();
                transactionQuery = transactionQuery.Where(fs => fs.Status == "Paid");
                if (fromDate.HasValue)
                    transactionQuery = transactionQuery.Where(fs => fs.TransactionDate >= fromDate.Value);
                if (toDate.HasValue)
                    transactionQuery = transactionQuery.Where(fs => fs.TransactionDate <= toDate.Value);

                // Get transactions data
                var transactions = await transactionQuery
                    .OrderByDescending(fs => fs.TransactionDate)
                    //.Take(50)
                    .Select(fs => new
                    {
                        StudentName = fs.Student.FullName ?? "Unknown",
                        StudentId = fs.Student.StudentId_Number,
                        Programme = fs.Student.Programme != null ? fs.Student.Programme.Name : "Unknown",
                        Amount = fs.Amount,
                        PaymentDate = fs.TransactionDate,
                        PaymentMethod = fs.PaymentMethod ?? "Unknown",
                        TransactionRef = fs.ReferenceNumber
                    })
                    .ToListAsync();

                // Get outstanding students
                var outstandingStudents = await _context.Students
                    .Include(s => s.Programme)
                    .Include(s => s.School)
                    .Where(s => s.OutstandingFees > 0)
                    .OrderByDescending(s => s.OutstandingFees)
                    .Take(50)
                    .Select(s => new
                    {
                        StudentName = s.FullName ?? "Unknown",
                        StudentId = s.StudentId_Number,
                        Programme = s.Programme != null ? s.Programme.Name : "Unknown",
                        School = s.School != null ? s.School.Name : "Unknown",
                        OutstandingAmount = s.OutstandingFees,
                        LastPaymentDate = s.FinancialStatements
                            .OrderByDescending(fs => fs.PaymentDate)
                            .Select(fs => fs.PaymentDate)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                // Calculate summary stats
                var totalRevenue = await transactionQuery.SumAsync(fs => (decimal?)fs.Amount) ?? 0;
                var totalOutstanding = await _context.Students.Where(s => s.OutstandingFees > 0).SumAsync(s => (decimal?)s.OutstandingFees) ?? 0;
                var studentsWithOutstanding = await _context.Students.Where(s => s.OutstandingFees > 0).CountAsync();
                var totalStudents = await _context.Students.CountAsync();
                var averageOutstanding = studentsWithOutstanding > 0 ? totalOutstanding / studentsWithOutstanding : 0;
                var collectionRate = totalStudents > 0 ? (int)((totalStudents - studentsWithOutstanding) * 100.0 / totalStudents) : 100;

                // Set ViewBag values
                ViewBag.Transactions = JsonConvert.SerializeObject(transactions);
                ViewBag.OutstandingStudents = JsonConvert.SerializeObject(outstandingStudents);
                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.TotalOutstanding = totalOutstanding;
                ViewBag.StudentsWithOutstanding = studentsWithOutstanding;
                ViewBag.AverageOutstanding = averageOutstanding;
                ViewBag.CollectionRate = collectionRate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LoadReportsData");
            }
        }

        [HttpGet]
        public async Task<IActionResult> SchoolBillingMonthly(
            string? selectedSchool = null,
            string? monthFrom = null,
            string? monthTo = null)
        {
            // ── 1. Pull raw data from the view ────────────────────────────────────
            var query = _context.VwBiSchoolBillingMonthly.AsQueryable();  // DbSet for VW_BI_SchoolBilling_Monthly

            if (!string.IsNullOrEmpty(selectedSchool))
                query = query.Where(r => r.School == selectedSchool);

            if (!string.IsNullOrEmpty(monthFrom))
                query = query.Where(r => string.Compare(r.MonthKey, monthFrom) >= 0);

            if (!string.IsNullOrEmpty(monthTo))
                query = query.Where(r => string.Compare(r.MonthKey, monthTo) <= 0);

            var raw = await query.OrderBy(r => r.MonthKey).ThenBy(r => r.School).ToListAsync();

            // ── 2. Build ViewModel ────────────────────────────────────────────────
            var vm = new SchoolBillingMonthly
            {
                SelectedSchool = selectedSchool,
                MonthFrom = monthFrom,
                MonthTo = monthTo,

                // Dropdown options (always unfiltered)
                Schools = await _context.VwBiSchoolBillingMonthly
                               .Select(r => r.School).Distinct().OrderBy(s => s).ToListAsync(),
                AllMonths = await _context.VwBiSchoolBillingMonthly
                               .Select(r => r.MonthKey).Distinct().OrderBy(m => m).ToListAsync(),

                // Rows
                Rows = raw.Select(r => new SchoolBillingRow
                {
                    School = r.School,
                    MonthKey = r.MonthKey,
                    MonthlyInvoices = r.MonthlyInvoices,
                    MonthlyPayments = r.MonthlyPayments,
                    MonthlyBalance = r.MonthlyBalance,
                }).ToList(),
            };

            // ── 3. Aggregates ──────────────────────────────────────────────────────
            vm.TotalInvoices = vm.Rows.Sum(r => r.MonthlyInvoices);
            vm.TotalPayments = vm.Rows.Sum(r => r.MonthlyPayments);
            vm.TotalBalance = vm.Rows.Sum(r => r.MonthlyBalance);
            vm.ActiveSchools = vm.Rows.Select(r => r.School).Distinct().Count();
            vm.PeriodMonths = vm.Rows.Select(r => r.MonthKey).Distinct().Count();

            // ── 4. Monthly trend (line chart) ─────────────────────────────────────
            vm.MonthlyTrend = vm.Rows
                .GroupBy(r => r.MonthKey)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => (Invoices: g.Sum(r => r.MonthlyInvoices),
                          Payments: g.Sum(r => r.MonthlyPayments)));

            // ── 5. Per-school totals (bar chart) ──────────────────────────────────
            vm.SchoolTotals = vm.Rows
                .GroupBy(r => r.School)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => (Invoices: g.Sum(r => r.MonthlyInvoices),
                          Payments: g.Sum(r => r.MonthlyPayments)));

            return View("~/Views/FinanceReports/SchoolBillingMonthly.cshtml", vm);
        }

        // ── Optional CSV export ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> ExportBillingReport(
            string? selectedSchool = null,
            string? monthFrom = null,
            string? monthTo = null)
        {
            var query = _context.VwBiSchoolBillingMonthly.AsQueryable();
            if (!string.IsNullOrEmpty(selectedSchool)) query = query.Where(r => r.School == selectedSchool);
            if (!string.IsNullOrEmpty(monthFrom)) query = query.Where(r => string.Compare(r.MonthKey, monthFrom) >= 0);
            if (!string.IsNullOrEmpty(monthTo)) query = query.Where(r => string.Compare(r.MonthKey, monthTo) <= 0);

            var rows = await query.OrderBy(r => r.MonthKey).ThenBy(r => r.School).ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("School,MonthKey,MonthlyInvoices,MonthlyPayments,MonthlyBalance");
            foreach (var r in rows)
                sb.AppendLine($"\"{r.School}\",{r.MonthKey},{r.MonthlyInvoices},{r.MonthlyPayments},{r.MonthlyBalance}");

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"SchoolBilling_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}