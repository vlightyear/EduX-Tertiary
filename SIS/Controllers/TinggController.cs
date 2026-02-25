using Microsoft.AspNetCore.Mvc;
using SIS.Services.Payment;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    public class ExpressCheckoutRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string MerchantTransactionId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? StudentId { get; set; }      // nullable
        public int? ApplicantId { get; set; }   // added
    }

    [ApiController]
    [Route("api/tingg")]
    public class TinggController : ControllerBase
    {
        private readonly TinggExpressCheckoutService _mobileCheckoutService;

        public TinggController(TinggExpressCheckoutService mobileCheckoutService)
        {
            _mobileCheckoutService = mobileCheckoutService;
        }

        [HttpPost("express-checkout")]
        public async Task<IActionResult> StartExpressCheckout([FromBody] ExpressCheckoutRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Phone) ||
                string.IsNullOrWhiteSpace(request.MerchantTransactionId) ||
                request.Amount <= 0 ||
                (request.StudentId is null && request.ApplicantId is null))
            {
                return BadRequest("Invalid input data. Either StudentId or ApplicantId must be provided.");
            }

            string accountNumber = "ACC-001"; // Placeholder for account number logic

            var checkoutUrl = await _mobileCheckoutService.CreateExpressCheckoutUrlAsync(
                fullName: request.Name,
                phone: request.Phone,
                merchantTransactionId: request.MerchantTransactionId,
                amount: request.Amount,
                studentID: request.StudentId,
                applicantID: request.ApplicantId,
                accountNumber: accountNumber
            );

            return Ok(new { checkoutUrl });
        }
    }
}
