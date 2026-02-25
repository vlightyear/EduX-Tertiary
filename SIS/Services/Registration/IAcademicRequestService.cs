namespace SIS.Services.Registration
{
    public interface IAcademicRequestService
    {
        Task<string> SubmitRequestAsync(int studentId, string requestType, string description, int programmeId, int schoolId);
        Task<string> GetRequestStatusAsync(int requestId);
    }
}