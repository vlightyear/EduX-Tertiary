using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Registration;

namespace SIS.Repository
{
    public class AcademicRequestRepository : IAcademicRequestRepository
    {
        private readonly ApplicationDbContext _context;

        public AcademicRequestRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(AcademicRequest academicRequest)
        {
            await _context.AcademicRequests.AddAsync(academicRequest);
            await _context.SaveChangesAsync(); // Save changes after adding the request
        }

        public async Task<List<AcademicRequest>> GetAllAsync()
        {
            return await _context.AcademicRequests.ToListAsync();
        }

        public async Task<AcademicRequest> GetByIdAsync(int requestId)
        {
            return await _context.AcademicRequests
                .FirstOrDefaultAsync(r => r.Id == requestId); // Adjust according to your model
        }

        public async Task UpdateAsync(AcademicRequest academicRequest)
        {
            _context.AcademicRequests.Update(academicRequest);
            await _context.SaveChangesAsync();
        }
    }

}
