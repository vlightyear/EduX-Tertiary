using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using Newtonsoft.Json;

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
                var transactionQuery = _context.FinancialStatements
                    .Include(fs => fs.Student)
                    .ThenInclude(s => s.Programme)
                    .AsQueryable();

                if (fromDate.HasValue)
                    transactionQuery = transactionQuery.Where(fs => fs.PaymentDate >= fromDate.Value);
                if (toDate.HasValue)
                    transactionQuery = transactionQuery.Where(fs => fs.PaymentDate <= toDate.Value);

                // Get transactions data
                var transactions = await transactionQuery
                    .OrderByDescending(fs => fs.PaymentDate)
                    //.Take(50)
                    .Select(fs => new
                    {
                        StudentName = fs.Student.FullName ?? "Unknown",
                        StudentId = fs.Student.StudentId_Number,
                        Programme = fs.Student.Programme != null ? fs.Student.Programme.Name : "Unknown",
                        Amount = fs.AmountPaid,
                        PaymentDate = fs.PaymentDate,
                        PaymentMethod = fs.PaymentMethod ?? "Unknown",
                        TransactionRef = fs.TransactionReference
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
                var totalRevenue = await transactionQuery.SumAsync(fs => (decimal?)fs.AmountPaid) ?? 0;
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
    }
}