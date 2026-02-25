using SIS.Models.Admin;
using SIS.Models.Reports;

namespace SIS.Services.Reports
{
    public interface ISenateReportService
    {
        Task<SenateReportViewModel> GenerateReportAsync(SenateReportFilters filters);
        Task<SenateReportViewModel> GetFilterOptionsAsync();
        Task<List<Department>> GetDepartmentsBySchoolAsync(int schoolId);
        Task<List<Programme>> GetProgrammesByDepartmentAsync(int departmentId);
        Task<List<StudentProgressionData>> GetEntityStudentDetailsAsync(int entityId, string entityType, SenateReportFilters filters, string? studentNumber = null);
        Task<StudentProgressionDetailViewModel> GetStudentProgressionDetailAsync(int studentId, SenateReportFilters filters);
        Task<List<ResultSubmissionBatchSummary>> GetPendingBatchesForProgrammeAsync(
        int programmeId,
        int academicYearId,
        int semester);

        Task<PublishBatchesResult> PublishBatchesAsync(
            List<int> batchIds,
            string approvedById);

        Task<PublishBatchesResult> PublishAllProgrammeBatchesAsync(
            int programmeId,
            int academicYearId,
            int semester,
            string approvedById);

        Task<ProgrammeGradingOverview> GetProgrammeGradingOverviewAsync(
            int programmeId,
            int academicYearId,
            int semester,
            int yearOfStudy);

        Task<PerformanceSummaryDto> GetPerformanceSummaryAsync(
            int programmeId,
            int academicYearId,
            int semester,
            int yearOfStudy);
    }
}
