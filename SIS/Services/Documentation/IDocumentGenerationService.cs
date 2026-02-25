namespace SIS.Services.Documentation
{
    public interface IDocumentGenerationService
    {
        void GenerateClassPass(int studentId, string studentName);
        void GenerateDocket(int studentId, string studentName);
    }
}
