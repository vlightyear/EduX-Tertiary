using Microsoft.AspNetCore.Mvc;

namespace SIS.Controllers
{
    public class PaymentFailController : Controller
    {
        [HttpGet("/payment-fail")]
        public IActionResult Index()
        {
            return View("payment-fail");
        }
    }
}
