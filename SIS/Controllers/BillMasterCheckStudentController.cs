
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration; // ✅ Added for reading settings

namespace SIS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BillMasterCheckStudentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // ✅ Now readonly instead of const — loaded from appsettings
        private readonly string ApiKey;

        public BillMasterCheckStudentController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            ApiKey = configuration["BillMasterAPI:ApiKey"]; // ✅ Read from appsettings.json
        }

        [HttpGet]
        public async Task<IActionResult> GetStudent([FromQuery] string query, [FromQuery] string apikey)
        {
            // ✅ API Key validation
            if (string.IsNullOrWhiteSpace(apikey) || apikey != ApiKey)
            {
                return Unauthorized(new { error = "Invalid or missing API key." });
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { error = "Query parameter is required." });
            }

            // ✅ Fetch exactly one student (first match)
            var student = await _context.Students
                .Where(s =>  s.StudentId_Number == query)
                .Select(s => new
                {
                    IsAppRefQuery = EF.Functions.Like(s.ApplicationReferenceNumber, $"%{query}%"),
                    s.StudentId_Number,
                    s.ApplicationReferenceNumber,
                    s.FullName,
                    s.NrcOrPassportNumber
                })
                .FirstOrDefaultAsync();

            if (student == null)
            {
                return Ok(new
                {
                    query = "Unit",
                    suggestions = new object[0]
                });
            }

            // ✅ Keep value/data consistent with search type
            var result = student.IsAppRefQuery
                ? new
                {
                    value = $"{student.ApplicationReferenceNumber} - {student.FullName}",
                    data = student.ApplicationReferenceNumber,
                    student_ID = student.ApplicationReferenceNumber,
                    student_Name = student.FullName,
                    student_NRC = student.NrcOrPassportNumber,
                    Institution_ID = "6146-EDEN UNIVERSITY"
                }
                : new
                {
                    value = $"{student.StudentId_Number} - {student.FullName}",
                    data = student.StudentId_Number,
                    student_ID = student.StudentId_Number,
                    student_Name = student.FullName,
                    student_NRC = student.NrcOrPassportNumber,
                    Institution_ID = "6146-EDEN UNIVERSITY"
                };

            return Ok(new
            {
                query = "Unit",
                suggestions = new[] { result }
            });
        }
    }
}
