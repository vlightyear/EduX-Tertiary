using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using SIS.Data;
using SIS.Models.StudentAccommodation;

namespace SIS.Controllers.AccomodationControllers
{
    [Authorize]
    public class AccommodationConfigurationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccommodationConfigurationController> _logger;

        public AccommodationConfigurationController(
            ApplicationDbContext context,
            ILogger<AccommodationConfigurationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: AccommodationConfiguration/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();

                // If no configuration exists, create a default one
                if (config == null)
                {
                    config = new AccomodationConfiguration
                    {
                        CreditCode = "ACC-CR-001",
                        DebitCode = "ACC-DR-001",
                        ReservationHoursValidity = 48,
                        AccommodationFee = 2500.00m,
                        LocationToTakeAccommodationPaymentReceipt = "Accommodation Office",
                        DeAllocateBedSpaceUponCheckOut = false
                    };
                }

                return View("~/Views/Accommodation/AccommodationConfiguration_Index.cshtml", config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accommodation configuration");
                TempData["Error"] = "An error occurred while loading the configuration.";
                return View("~/Views/Accommodation/AccommodationConfiguration_Index.cshtml", new AccomodationConfiguration());
            }
        }

        // POST: AccommodationConfiguration/SaveConfiguration
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveConfiguration(AccomodationConfiguration model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Invalid data provided." });
                }

                // Validate business rules
                if (string.IsNullOrWhiteSpace(model.CreditCode))
                {
                    return Json(new { success = false, message = "Credit Code is required." });
                }

                if (string.IsNullOrWhiteSpace(model.DebitCode))
                {
                    return Json(new { success = false, message = "Debit Code is required." });
                }

                if (model.ReservationHoursValidity < 1)
                {
                    return Json(new { success = false, message = "Reservation hours must be at least 1 hour." });
                }

                if (model.ReservationHoursValidity > 168)
                {
                    return Json(new { success = false, message = "Reservation hours cannot exceed 168 hours (1 week)." });
                }

                if (model.AccommodationFee < 0)
                {
                    return Json(new { success = false, message = "Accommodation fee cannot be negative." });
                }

                if (string.IsNullOrWhiteSpace(model.LocationToTakeAccommodationPaymentReceipt))
                {
                    return Json(new { success = false, message = "Payment receipt location is required." });
                }

                // Check if configuration exists
                var existingConfig = await _context.AccomodationConfigurations.FirstOrDefaultAsync();

                if (existingConfig == null)
                {
                    // Create new configuration
                    _context.AccomodationConfigurations.Add(model);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Accommodation configuration created by {User.Identity?.Name ?? "System"}. " +
                                         $"Credit Code: {model.CreditCode}, " +
                                         $"Debit Code: {model.DebitCode}, " +
                                         $"Fee: K{model.AccommodationFee}, " +
                                         $"DeAllocate on CheckOut: {model.DeAllocateBedSpaceUponCheckOut}");

                    return Json(new
                    {
                        success = true,
                        message = "Configuration created successfully!",
                        data = new
                        {
                            id = model.Id,
                            creditCode = model.CreditCode,
                            debitCode = model.DebitCode,
                            reservationHours = model.ReservationHoursValidity,
                            accommodationFee = model.AccommodationFee,
                            paymentLocation = model.LocationToTakeAccommodationPaymentReceipt,
                            deAllocateOnCheckOut = model.DeAllocateBedSpaceUponCheckOut
                        }
                    });
                }
                else
                {
                    // Update existing configuration
                    existingConfig.CreditCode = model.CreditCode;
                    existingConfig.DebitCode = model.DebitCode;
                    existingConfig.ReservationHoursValidity = model.ReservationHoursValidity;
                    existingConfig.AccommodationFee = model.AccommodationFee;
                    existingConfig.LocationToTakeAccommodationPaymentReceipt = model.LocationToTakeAccommodationPaymentReceipt;
                    existingConfig.DeAllocateBedSpaceUponCheckOut = model.DeAllocateBedSpaceUponCheckOut;

                    _context.AccomodationConfigurations.Update(existingConfig);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Accommodation configuration updated by {User.Identity?.Name ?? "System"}. " +
                                         $"Credit Code: {existingConfig.CreditCode}, " +
                                         $"Debit Code: {existingConfig.DebitCode}, " +
                                         $"Fee: K{existingConfig.AccommodationFee}, " +
                                         $"Reservation Hours: {existingConfig.ReservationHoursValidity}, " +
                                         $"DeAllocate on CheckOut: {existingConfig.DeAllocateBedSpaceUponCheckOut}");

                    return Json(new
                    {
                        success = true,
                        message = "Configuration updated successfully!",
                        data = new
                        {
                            id = existingConfig.Id,
                            creditCode = existingConfig.CreditCode,
                            debitCode = existingConfig.DebitCode,
                            reservationHours = existingConfig.ReservationHoursValidity,
                            accommodationFee = existingConfig.AccommodationFee,
                            paymentLocation = existingConfig.LocationToTakeAccommodationPaymentReceipt,
                            deAllocateOnCheckOut = existingConfig.DeAllocateBedSpaceUponCheckOut
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving accommodation configuration");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: AccommodationConfiguration/GetConfiguration
        [HttpGet]
        public async Task<IActionResult> GetConfiguration()
        {
            try
            {
                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();

                if (config == null)
                {
                    return Json(new { success = false, message = "Configuration not found." });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = config.Id,
                        creditCode = config.CreditCode,
                        debitCode = config.DebitCode,
                        reservationHours = config.ReservationHoursValidity,
                        accommodationFee = config.AccommodationFee,
                        paymentLocation = config.LocationToTakeAccommodationPaymentReceipt,
                        deAllocateOnCheckOut = config.DeAllocateBedSpaceUponCheckOut
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accommodation configuration");
                return Json(new { success = false, message = "An error occurred while fetching configuration." });
            }
        }

        // POST: AccommodationConfiguration/ResetToDefaults
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetToDefaults()
        {
            try
            {
                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();

                if (config == null)
                {
                    return Json(new { success = false, message = "Configuration not found." });
                }

                // Reset to default values
                config.CreditCode = "ACC-CR-001";
                config.DebitCode = "ACC-DR-001";
                config.ReservationHoursValidity = 48;
                config.AccommodationFee = 5500.00m;
                config.LocationToTakeAccommodationPaymentReceipt = "Ecampus Payment Page";
                config.DeAllocateBedSpaceUponCheckOut = false;

                _context.AccomodationConfigurations.Update(config);
                await _context.SaveChangesAsync();

                _logger.LogWarning($"Accommodation configuration reset to defaults by {User.Identity?.Name ?? "System"}");

                return Json(new
                {
                    success = true,
                    message = "Configuration reset to default values successfully!",
                    data = new
                    {
                        id = config.Id,
                        creditCode = config.CreditCode,
                        debitCode = config.DebitCode,
                        reservationHours = config.ReservationHoursValidity,
                        accommodationFee = config.AccommodationFee,
                        paymentLocation = config.LocationToTakeAccommodationPaymentReceipt,
                        deAllocateOnCheckOut = config.DeAllocateBedSpaceUponCheckOut
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting accommodation configuration");
                return Json(new { success = false, message = "An error occurred while resetting configuration." });
            }
        }

        // GET: AccommodationConfiguration/GetStatistics
        [HttpGet]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();

                if (config == null)
                {
                    return Json(new { success = false, message = "Configuration not found." });
                }

                // Get statistics
                var totalApplications = await _context.AccommodationApplications.CountAsync();
                var pendingApplications = await _context.AccommodationApplications
                    .CountAsync(a => a.Status == SIS.Enums.Status.Pending);
                var approvedApplications = await _context.AccommodationApplications
                    .CountAsync(a => a.Status == SIS.Enums.Status.Approved);
                var totalAllocations = await _context.Allocations
                    .CountAsync(a => a.Status == SIS.Enums.Status.Active);

                // Calculate total revenue from student invoices with accommodation fees
                var totalRevenue = await _context.StudentInvoices
                    .Where(si => si.InvoiceItems.Any(item => item.FeeTypeName.Contains("Accommodation")))
                    .SumAsync(si => si.TotalAmount);

                // Get total occupied beds
                var totalOccupiedBeds = await _context.BedSpaces
                    .CountAsync(b => b.Status == SIS.Enums.Status.Occupied);

                // Get total available beds
                var totalAvailableBeds = await _context.BedSpaces
                    .CountAsync(b => b.Status == SIS.Enums.Status.Available);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        currentFee = config.AccommodationFee,
                        reservationHours = config.ReservationHoursValidity,
                        totalApplications,
                        pendingApplications,
                        approvedApplications,
                        totalAllocations,
                        totalRevenue,
                        totalOccupiedBeds,
                        totalAvailableBeds,
                        creditCode = config.CreditCode,
                        debitCode = config.DebitCode,
                        paymentLocation = config.LocationToTakeAccommodationPaymentReceipt,
                        deAllocateOnCheckOut = config.DeAllocateBedSpaceUponCheckOut
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accommodation statistics");
                return Json(new { success = false, message = "An error occurred while fetching statistics." });
            }
        }
    }
}