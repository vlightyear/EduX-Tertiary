using SIS.DTOs.StudentApplication;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SIS.Services.StudentApplication
{
    public interface IApplicantService
    {
        Task<List<DropdownOptionDto>> GetSubjectsAsync();
        Task<List<DropdownOptionDto>> GetGradesAsync();
        Task<List<DropdownOptionDto>> GetSchoolsAsync();
        Task<List<DropdownOptionDto>> GetProgrammesAsync(int schoolId);
        Task<List<DropdownOptionDto>> GetModesOfStudyAsync();
        Task<List<DropdownOptionDto>> GetAcademicYearsAsync();
        Task<List<DropdownOptionDto>> GetProgrammeLevels();
        Task<Applicant> GetApplicantByNrcOrPassportAsync(string nrcOrPassport);
        string GenerateReferenceNumber();
        Task<string> GenerateStudentIdAsync(int academicYear);
        Task<List<Applicant>> GetApplicationsByUserIdAsync(string userId);
        Task<List<DropdownOptionDto>> GetProgrammesAsync(int schoolId, int programmeLevelId);
    }
}