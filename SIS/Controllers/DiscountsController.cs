using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;

namespace SIS.Controllers
{
    public class DiscountsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CreditNoteController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public DiscountsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<CreditNoteController> logger,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }
        public async Task<IActionResult> IndexAsync()
        {
            var today = DateTime.Now.Date;
            var todaysCreditNotes = await _context.OnlinePayments
                .Where(p => p.TransactionType == "CRN"
                         && p.CreatedAt >= today
                         && p.CreatedAt < today.AddDays(1))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(todaysCreditNotes);
        }
    }
}
