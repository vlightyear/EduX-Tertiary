namespace SIS.Services.Registration
{
    public interface IGradeService
    {
        Task<string> GetGradesAsync(int studentId);
    }
}