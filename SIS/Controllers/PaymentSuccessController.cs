using Microsoft.AspNetCore.Mvc;

namespace SIS.Controllers
{
    public class PaymentSuccessController : Controller
    {
        [HttpGet("/payment-success")]
        public IActionResult Index()
        {
            return View("payment-success");
        }
    }
}
