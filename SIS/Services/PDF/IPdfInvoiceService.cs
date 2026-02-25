using SIS.Models.Admin;
using SIS.Models.StudentApplication;
using SIS.Models.ViewModels;

namespace SIS.Services.PDF
{
    public interface IPdfInvoiceService
    {
        Task<byte[]> GenerateProgrammeFeesInvoiceAsync(int programmeId, int yearOfStudy, string applicantName = null);
        Task<byte[]> GenerateApplicationFeesInvoiceAsync(string referenceNumber);

        Task<byte[]> GenerateAdmissionLetterAsync(Student student);

        Task<byte[]> GenerateStudentListPdfAsync(List<FilteredStudentViewModel> students, StudentListExportOptions exportOptions);
        Task<byte[]> GenerateStudentListExcelAsync(List<FilteredStudentViewModel> students, StudentListExportOptions exportOptions);
        Task<byte[]> GenerateExamDocketPdfAsync(Student student, AcademicCalendarEvent examEvent, List<dynamic> courses);
    }
}