namespace SIS.Services.Payment
{
    public interface IFinancialManagementService
    {
        Task<string> GetFinancialStatusAsync(int studentId);
    }
}