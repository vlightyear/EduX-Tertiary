using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Payments;
using System.IO;
using System.Text.Json;

namespace SIS.Controllers
{
    [Authorize(Roles = "Admin,Finance")]
    public class SageTransactionSyncController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public SageTransactionSyncController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            _context = context;
            _configuration = configuration;
            _env = env;
        }

        public async Task<IActionResult> Index(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string searchTerm = null,
            int page = 1,
            int pageSize = 20)
        {
            // Get statistics
            var stats = await GetStatistics();
            ViewBag.Statistics = stats;

            // Build query for transactions - ONLY FromSage transactions
            var transactions = await GetTransactions(dateFrom, dateTo, searchTerm, page, pageSize);
            
            ViewBag.Transactions = transactions.Items;
            ViewBag.TotalItems = transactions.TotalItems;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)transactions.TotalItems / pageSize);
            
            // Pass filter values back to view
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.SearchTerm = searchTerm;

            // Explicitly return view from Admin folder
            return View("~/Views/Admin/SageTransactionSync.cshtml");
        }

        private async Task<SageSyncStatistics> GetStatistics()
        {
            var stats = new SageSyncStatistics
            {
                // Invoice statistics - Only FromSage
                TotalInvoices = await _context.StudentInvoices
                    .Where(i => i.AccountingSystemPostStatus == "FromSage")
                    .CountAsync(),

                // Payment statistics - Only FromSage
                TotalPayments = await _context.OnlinePayments
                    .Where(p => p.AccountingSystemPostStatus == "FromSage")
                    .CountAsync(),

                // Service configuration
                IsEnabled = _configuration.GetValue<bool>("Sage:TransactionSync:Enabled", true),
                IntervalMinutes = 5,
                BatchSize = _configuration.GetValue<int>("Sage:BatchSize", 10),
                BaseUrl = _configuration["Sage:BaseUrl"] ?? "http://41.63.0.222:4433",
                PaymentGLAccount = _configuration["Sage:PaymentGLAccount"] ?? "1200",
                DefaultGLAccount = _configuration["Sage:DefaultGLAccount"] ?? "4000"
            };

            return stats;
        }

        private async Task<PaginatedResult<TransactionViewModel>> GetTransactions(
            DateTime? dateFrom,
            DateTime? dateTo,
            string searchTerm,
            int page,
            int pageSize)
        {
            // Build invoices query
            var invoiceQuery = _context.StudentInvoices
                .Include(i => i.Student)
                .Where(i => i.AccountingSystemPostStatus == "FromSage");

            if (dateFrom.HasValue)
                invoiceQuery = invoiceQuery.Where(i => i.CreatedDate >= dateFrom.Value);
            if (dateTo.HasValue)
                invoiceQuery = invoiceQuery.Where(i => i.CreatedDate <= dateTo.Value.AddDays(1).AddSeconds(-1));

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                invoiceQuery = invoiceQuery.Where(i =>
                    i.InvoiceReference.Contains(searchTerm) ||
                    i.Student.FullName.Contains(searchTerm) ||
                    i.Student.StudentId_Number.Contains(searchTerm));
            }

            // Build payments query
            var paymentQuery = _context.OnlinePayments
                .Where(p => p.AccountingSystemPostStatus == "FromSage");

            if (dateFrom.HasValue)
                paymentQuery = paymentQuery.Where(p => p.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue)
                paymentQuery = paymentQuery.Where(p => p.CreatedAt <= dateTo.Value.AddDays(1).AddSeconds(-1));

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                paymentQuery = paymentQuery.Where(p =>
                    p.MerchantTransactionId.Contains(searchTerm) ||
                    p.ReferenceNumber.Contains(searchTerm) ||
                    p.FullName.Contains(searchTerm) ||
                    p.AccountNumber.Contains(searchTerm));
            }

            // Execute queries with projection at database level
            var invoiceViewModels = await invoiceQuery
                .Select(i => new TransactionViewModel
                {
                    Id = i.Id,
                    Type = "Invoice",
                    Reference = i.InvoiceReference ?? string.Empty,
                    CustomerName = i.Student.FullName ?? string.Empty,
                    CustomerCode = i.Student.StudentId_Number ?? string.Empty,
                    Amount = i.TotalAmount,
                    Status = "FromSage",
                    CreatedDate = i.CreatedDate,
                    TransactionDate = null
                })
                .ToListAsync();

            var paymentViewModels = await paymentQuery
                .Select(p => new TransactionViewModel
                {
                    Id = p.Id,
                    Type = "Payment",
                    Reference = p.ReferenceNumber ?? p.MerchantTransactionId ?? string.Empty,
                    CustomerName = p.FullName ?? string.Empty,
                    CustomerCode = p.AccountNumber ?? string.Empty,
                    Amount = p.Amount ?? 0m,  // Handle nullable decimal
                    Status = "FromSage",
                    CreatedDate = p.CreatedAt,
                    TransactionDate = p.TransactionDate
                })
                .ToListAsync();

            // Combine and sort
            var transactions = invoiceViewModels
                .Concat(paymentViewModels)
                .OrderByDescending(t => t.CreatedDate)
                .ToList();

            // Calculate pagination
            var totalItems = transactions.Count;
            var items = transactions
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedResult<TransactionViewModel>
            {
                Items = items,
                TotalItems = totalItems,
                CurrentPage = page,
                PageSize = pageSize
            };
        }

        [HttpPost]
        public async Task<IActionResult> ToggleService(bool enable)
        {
            try
            {
                var appsettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
                var json = await System.IO.File.ReadAllTextAsync(appsettingsPath);
                
                using var doc = JsonDocument.Parse(json);
                var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                
                if (settings == null)
                {
                    return Json(new { success = false, message = "Failed to read settings file" });
                }

                Dictionary<string, object> sageSettings;
                if (settings.ContainsKey("Sage"))
                {
                    sageSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        settings["Sage"].GetRawText()) ?? new Dictionary<string, object>();
                }
                else
                {
                    sageSettings = new Dictionary<string, object>();
                }

                var transactionSync = new Dictionary<string, object>
                {
                    ["Enabled"] = enable
                };
                
                sageSettings["TransactionSync"] = transactionSync;
                settings["Sage"] = JsonSerializer.SerializeToElement(sageSettings);

                var options = new JsonSerializerOptions { WriteIndented = true };
                await System.IO.File.WriteAllTextAsync(
                    appsettingsPath,
                    JsonSerializer.Serialize(settings, options)
                );

                return Json(new
                {
                    success = true,
                    message = $"Service {(enable ? "enabled" : "disabled")} successfully. Application restart required for changes to take effect.",
                    requiresRestart = true
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error updating service status: {ex.Message}"
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSettings(
            int intervalMinutes,
            int batchSize,
            string baseUrl,
            string paymentGLAccount,
            string defaultGLAccount)
        {
            try
            {
                if (intervalMinutes < 1 || intervalMinutes > 60)
                    return Json(new { success = false, message = "Interval must be between 1 and 60 minutes" });

                if (batchSize < 10 || batchSize > 100)
                    return Json(new { success = false, message = "Batch size must be between 10 and 100" });

                if (string.IsNullOrWhiteSpace(baseUrl))
                    return Json(new { success = false, message = "Base URL is required" });

                var appsettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
                var json = await System.IO.File.ReadAllTextAsync(appsettingsPath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (settings == null)
                {
                    return Json(new { success = false, message = "Failed to read settings file" });
                }

                Dictionary<string, object> sageSettings;
                if (settings.ContainsKey("Sage"))
                {
                    sageSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        settings["Sage"].GetRawText()) ?? new Dictionary<string, object>();
                }
                else
                {
                    sageSettings = new Dictionary<string, object>();
                }

                sageSettings["BaseUrl"] = baseUrl;
                sageSettings["BatchSize"] = batchSize;
                sageSettings["PaymentGLAccount"] = paymentGLAccount;
                sageSettings["DefaultGLAccount"] = defaultGLAccount;

                settings["Sage"] = JsonSerializer.SerializeToElement(sageSettings);

                var options = new JsonSerializerOptions { WriteIndented = true };
                await System.IO.File.WriteAllTextAsync(
                    appsettingsPath,
                    JsonSerializer.Serialize(settings, options)
                );

                return Json(new
                {
                    success = true,
                    message = "Settings updated successfully. Application restart required for changes to take effect."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error updating settings: {ex.Message}"
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RetryTransaction(int id, string type)
        {
            try
            {
                if (type == "Invoice")
                {
                    var invoice = await _context.StudentInvoices.FindAsync(id);
                    if (invoice == null)
                        return Json(new { success = false, message = "Invoice not found" });

                    invoice.AccountingSystemPostStatus = "Pending";
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Invoice marked for retry. It will be processed in the next sync cycle."
                    });
                }
                else if (type == "Payment")
                {
                    var payment = await _context.OnlinePayments.FindAsync(id);
                    if (payment == null)
                        return Json(new { success = false, message = "Payment not found" });

                    payment.AccountingSystemPostStatus = "Pending";
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Payment marked for retry. It will be processed in the next sync cycle."
                    });
                }

                return Json(new { success = false, message = "Invalid transaction type" });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error retrying transaction: {ex.Message}"
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RetryAllFailed(string type)
        {
            try
            {
                int count = 0;

                if (type == "All" || type == "Invoice")
                {
                    var failedInvoices = await _context.StudentInvoices
                        .Where(i => i.AccountingSystemPostStatus == "Failed")
                        .Take(20)
                        .ToListAsync();

                    foreach (var invoice in failedInvoices)
                    {
                        invoice.AccountingSystemPostStatus = "Pending";
                        count++;
                    }
                }

                if (type == "All" || type == "Payment")
                {
                    var failedPayments = await _context.OnlinePayments
                        .Where(p => p.AccountingSystemPostStatus == "Failed")
                        .Take(20)
                        .ToListAsync();

                    foreach (var payment in failedPayments)
                    {
                        payment.AccountingSystemPostStatus = "Pending";
                        count++;
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"{count} transaction(s) marked for retry. They will be processed in the next sync cycle."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error retrying transactions: {ex.Message}"
                });
            }
        }
    }

    #region View Models

    public class SageSyncStatistics
    {
        public int TotalInvoices { get; set; }
        public int TotalPayments { get; set; }
        public bool IsEnabled { get; set; }
        public int IntervalMinutes { get; set; }
        public int BatchSize { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string PaymentGLAccount { get; set; } = string.Empty;
        public string DefaultGLAccount { get; set; } = string.Empty;
    }

    public class TransactionViewModel
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? TransactionDate { get; set; }
    }

    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalItems { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }

    #endregion
}