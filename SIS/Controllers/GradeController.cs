using Microsoft.AspNetCore.Mvc;
using SIS.Services.Registration;

namespace SIS.Controllers
{
    public class GradeController : Controller
    {
        private readonly GradeService _gradeService;

        public GradeController(GradeService gradeService)
        {
            _gradeService = gradeService;
        }

        public async Task<IActionResult> ViewGrades(int studentId)
        {
            var grades = await _gradeService.GetGradesAsync(studentId);
            ViewData["Grades"] = grades;
            return View();
        }
    }
}
