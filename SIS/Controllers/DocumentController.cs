using Microsoft.AspNetCore.Mvc;
using SIS.Services.Documentation;

namespace SIS.Controllers
{
    public class DocumentController : Controller
    {
        private readonly DocumentGenerationService _documentGenerationService;

        public DocumentController(DocumentGenerationService documentGenerationService)
        {
            _documentGenerationService = documentGenerationService;
        }

        public IActionResult DownloadClassPass(int studentId, string studentName)
        {
            _documentGenerationService.GenerateClassPass(studentId, studentName);
            var filePath = Path.Combine("Documents", $"{studentName}_ClassPass.pdf");
            return PhysicalFile(filePath, "application/pdf", $"{studentName}_ClassPass.pdf");
        }

        public IActionResult DownloadDocket(int studentId, string studentName)
        {
            _documentGenerationService.GenerateDocket(studentId, studentName);
            var filePath = Path.Combine("Documents", $"{studentName}_Docket.pdf");
            return PhysicalFile(filePath, "application/pdf", $"{studentName}_Docket.pdf");
        }
    }
}
