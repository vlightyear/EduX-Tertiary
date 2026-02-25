using SIS.Models.StudentApplication;

namespace SIS.Services.StudentApplication
{
    public interface IProgrammeService
    {
        Task<ProgrammeListViewModel> GetProgrammesGroupedBySchoolAsync(string search = "", int? schoolId = null, int? programmeLevelId = null);
        Task<ProgrammeDetailsViewModel> GetProgrammeDetailsWithCoursesAsync(int id);
        Task<List<CoursesGroupedByYearViewModel>> GetCoursesGroupedByYearAsync(int programmeId);
        Task<List<ProgrammeFeeDetailViewModel>> GetFeeBreakdownForProgramme(int programmeId, int? yearOfStudy = null);
    }
}
