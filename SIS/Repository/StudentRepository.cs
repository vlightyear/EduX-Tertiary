using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.StudentApplication;

namespace SIS.Repository
{
    // StudentRepository.cs (or whatever class implements IStudentRepository)
    public class StudentRepository : IStudentRepository
    {
        private readonly ApplicationDbContext _context;

        public StudentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Student> GetByIdAsync(int studentId)
        {
            // Fetch the student by Id, including any necessary related data
            return await _context.Students
                .Include(s => s.FinancialStatements)  // If needed, include related entities like financial statements
                .FirstOrDefaultAsync(s => s.Id == studentId); // Adjust the query to match your database schema
        }

        public async Task<Student> GetByEmailAsync(string email) => await _context.Students
            .Include(s => s.FinancialStatements)
            .Include(s => s.Programme.ProgrammeCourses)
            .FirstOrDefaultAsync(s => s.Email == email); // Adjust the query to match your database schema>
    }

}
