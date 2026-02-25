namespace SIS.Services.Transcripts
{
    public interface ITranscriptGenerationService
    {
        /// <summary>
        /// Generates a PDF transcript for a specific semester
        /// </summary>
        Task<byte[]> GenerateSemesterTranscriptAsync(int studentId, int academicYearId, int semester);

        /// <summary>
        /// Generates a full PDF transcript containing all completed semesters
        /// </summary>
        Task<byte[]> GenerateFullTranscriptAsync(int studentId);

        /// <summary>
        /// Generates QR code data for transcript verification
        /// </summary>
        string GenerateTranscriptQRData(string studentNumber, DateTime generatedDate);
    }
}