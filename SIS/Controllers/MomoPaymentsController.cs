
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIS.Models.Payments;
using SIS.Services.Payment;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MomoPaymentsController : ControllerBase
    {
        private readonly MomoPaymentService _paymentService;
        private readonly ILogger<MomoPaymentsController> _logger;

        public MomoPaymentsController(MomoPaymentService paymentService, ILogger<MomoPaymentsController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        // POST: /api/momopayments
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MomoPayment payment)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var savedPayment = await _paymentService.SavePaymentRequestAsync(payment);
            if (savedPayment == null)
                return StatusCode(500, "Failed to create payment request.");

            // Start background status checking without blocking the response
            _ = Task.Run(async () =>
            {
                try
                {
                    await _paymentService.CheckAndUpdateStatusAsync(savedPayment.TransactionId!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background status check failed for TransactionId: {TransactionId}", savedPayment.TransactionId);
                }
            });

            return Ok(savedPayment);
        }

        // GET: /api/momopayments/status/{transactionId}
        [HttpGet("status/{transactionId}")]
        public async Task<IActionResult> CheckStatus(string transactionId)
        {
            _logger.LogInformation("Manual status check requested for TransactionId: {TransactionId}", transactionId);

            await _paymentService.CheckAndUpdateStatusAsync(transactionId);

            var updatedPayment = await _paymentService.GetPaymentByTransactionIdAsync(transactionId);
            if (updatedPayment == null)
                return NotFound("Payment not found.");

            return Ok(updatedPayment);
        }
    }
}


