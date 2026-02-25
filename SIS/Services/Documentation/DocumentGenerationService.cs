using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Reflection.Metadata;
using Document = iTextSharp.text.Document;

namespace SIS.Services.Documentation
{
    public class DocumentGenerationService : IDocumentGenerationService
    {
        public void GenerateClassPass(int studentId, string studentName)
        {
            var filePath = Path.Combine("Documents", $"{studentName}_ClassPass.pdf");

            using (var doc = new Document())
            {
                PdfWriter.GetInstance(doc, new FileStream(filePath, FileMode.Create));
                doc.Open();
                doc.Add(new Paragraph($"Class Pass for {studentName} (ID: {studentId})"));
                doc.Add(new Paragraph($"Date of Issue: {DateTime.Now:MM/dd/yyyy}"));
                doc.Add(new Paragraph($"Student ID: {studentId}"));
                doc.Close();
            }
        }

        public void GenerateDocket(int studentId, string studentName)
        {
            var filePath = Path.Combine("Documents", $"{studentName}_Docket.pdf");

            using (var doc = new Document())
            {
                PdfWriter.GetInstance(doc, new FileStream(filePath, FileMode.Create));
                doc.Open();
                doc.Add(new Paragraph($"Docket for {studentName} (ID: {studentId})"));
                doc.Add(new Paragraph($"Date of Issue: {DateTime.Now:MM/dd/yyyy}"));
                doc.Add(new Paragraph($"Student ID: {studentId}"));
                doc.Close();
            }
        }
    }

}
