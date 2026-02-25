using Microsoft.AspNetCore.Http;
using SIS.Services.StudentImport;

namespace SIS.Services.StudentImport
{
    public interface IStudentImportService
    {
        Task<ImportPreviewResult> PreviewImportDataAsync(IFormFile file, CancellationToken cancellationToken = default);

        Task<ImportProcessResult> ProcessImportAsync(List<StudentImportDto> validStudents, string importedBy, string progressKey, CancellationToken cancellationToken = default);

        Task<List<StudentValidationResult>> ValidateStudentDataAsync(List<StudentImportDto> students, CancellationToken cancellationToken = default);

        Task<byte[]> GenerateImportTemplateAsync(CancellationToken cancellationToken = default);

        Task<byte[]> GenerateErrorReportAsync(ImportPreviewResult previewResult, CancellationToken cancellationToken = default);

        Task<ImportProgress> GetImportProgressAsync(string progressKey, CancellationToken cancellationToken = default);

        Task CleanupProgressAsync(string progressKey);
    }
}