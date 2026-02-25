namespace SIS.Services.FilePreview
{
    public interface IFileService
    {
        Task<(byte[] FileContents, string ContentType)?> GetFileAsync(string filePath);
        bool IsFilePathSafe(string filePath);
    }
}
