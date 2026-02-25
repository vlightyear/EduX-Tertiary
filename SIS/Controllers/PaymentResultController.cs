using Microsoft.AspNetCore.Mvc;

namespace SIS.Controllers
{
    public class PaymentResultController : Controller
    {
        [HttpGet("/payment-success")]
        [HttpPost("/payment-success")]
        public IActionResult Success()
        {
            return View("~/Views/Shared/payment-success.cshtml");
        }

        [HttpGet("/payment-fail")]
        [HttpPost("/payment-fail")]
        public IActionResult Fail()
        {
            return View("~/Views/Shared/payment-fail.cshtml");
        }
    }
}
