using Microsoft.AspNetCore.StaticFiles;

namespace SIS.Services.FilePreview
{
    public class FileService : IFileService
    {

        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;
        private readonly string[] _allowedExtensions = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
        private readonly string[] _allowedDirectories;

        public FileService(
            IWebHostEnvironment environment,
            ILogger<FileService> logger)
        {
            _environment = environment;
            _logger = logger;

            // Define allowed directories relative to web root
            _allowedDirectories = new[]
            {
                Path.Combine(_environment.WebRootPath, "Uploads", "Results"),
                Path.Combine(_environment.WebRootPath, "Uploads", "NRCs"),
                Path.Combine(_environment.WebRootPath, "Uploads", "StudyPermits")
            };
        }

        public async Task<(byte[] FileContents, string ContentType)?> GetFileAsync(string filePath)
        {
            try
            {
                if (!IsFilePathSafe(filePath))
                {
                    _logger.LogWarning($"Attempted to access potentially unsafe file path: {filePath}");
                    return null;
                }

                var fullPath = Path.GetFullPath(filePath);

                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning($"File not found: {fullPath}");
                    return null;
                }

                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(fullPath, out string contentType))
                {
                    contentType = "application/octet-stream";
                }

                var fileContents = await File.ReadAllBytesAsync(fullPath);
                return (fileContents, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading file: {filePath}");
                return null;
            }
        }

        public bool IsFilePathSafe(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                // Normalize path
                filePath = filePath.Replace('\\', '/');
                var fullPath = Path.GetFullPath(filePath);

                // Check file extension
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                if (!_allowedExtensions.Contains(extension))
                {
                    _logger.LogWarning($"Invalid file extension attempted: {extension}");
                    return false;
                }

                // Check if path is within allowed directories
                bool isInAllowedDirectory = _allowedDirectories.Any(dir =>
                    fullPath.StartsWith(dir, StringComparison.OrdinalIgnoreCase));

                if (!isInAllowedDirectory)
                {
                    _logger.LogWarning($"File path outside allowed directories: {fullPath}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating file path: {filePath}");
                return false;
            }
        }
    }
}
