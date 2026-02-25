using Microsoft.AspNetCore.Mvc;
using SIS.Services.Payment;

namespace SIS.Controllers
{
    public class FinancialManagementController : Controller
    {
        private readonly FinancialManagementService _financialManagementService;

        public FinancialManagementController(FinancialManagementService financialManagementService)
        {
            _financialManagementService = financialManagementService;
        }

        public async Task<IActionResult> ViewFinancialStatus(int id)
        {
            var studentId = id;
            var status = await _financialManagementService.GetFinancialStatusAsync(studentId);
            ViewData["FinancialStatus"] = status;
            return View();
        }
    }
}
