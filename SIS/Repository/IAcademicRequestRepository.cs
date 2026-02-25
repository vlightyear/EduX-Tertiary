using SIS.Models.Registration;

namespace SIS.Repository
{
    public interface IAcademicRequestRepository
    {
        Task AddAsync(AcademicRequest academicRequest);
        Task<List<AcademicRequest>> GetAllAsync();
        Task<AcademicRequest> GetByIdAsync(int requestId);
        Task UpdateAsync(AcademicRequest academicRequest);
    }
}
