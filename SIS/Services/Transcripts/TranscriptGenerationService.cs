using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using QRCoder;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using SIS.Models.StudentResults;
using System.Drawing;
using System.Drawing.Imaging;

namespace SIS.Services.Transcripts
{
    public class TranscriptGenerationService : ITranscriptGenerationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TranscriptGenerationService> _logger;
        private readonly IInstitutionConfigService _institutionConfig;

        // Page dimensions and margins (A4 Portrait)
        private const double PageWidth = 595; // A4 width in points
        private const double PageHeight = 842; // A4 height in points
        private const double MarginLeft = 40;
        private const double MarginRight = 40;
        private const double MarginTop = 30;
        private const double MarginBottom = 100; // Space for footer

        // Colors matching exam docket
        private static readonly XColor PrimaryColor = XColor.FromArgb(30, 64, 175); // Navy blue
        private static readonly XColor SecondaryColor = XColor.FromArgb(100, 116, 139); // Slate gray
        private static readonly XColor LightGray = XColor.FromArgb(248, 250, 252);
        private static readonly XColor BorderGray = XColor.FromArgb(226, 232, 240);

        public TranscriptGenerationService(
            ApplicationDbContext context,
            ILogger<TranscriptGenerationService> logger,
            IInstitutionConfigService institutionConfig)
        {
            _context = context;
            _logger = logger;
            _institutionConfig = institutionConfig;
        }

        public async Task<byte[]> GenerateSemesterTranscriptAsync(int studentId, int academicYearId, int semester)
        {
            try
            {
                var student = await GetStudentDetailsAsync(studentId);
                if (student == null)
                    throw new ArgumentException($"Student with ID {studentId} not found");

                var semesterData = await GetSemesterResultsAsync(studentId, academicYearId, semester);
                if (!semesterData.Any())
                    throw new ArgumentException($"No results found for the specified semester");

                var document = new PdfDocument();
                var page = document.AddPage();
                page.Width = PageWidth;
                page.Height = PageHeight;

                var graphics = XGraphics.FromPdfPage(page);
                double yPosition = MarginTop;

                // Draw header
                yPosition = DrawHeader(graphics, yPosition);

                // Draw semester details
                var academicYear = await _context.AcademicYears.FindAsync(academicYearId);
                yPosition = DrawSemesterInfo(graphics, student, academicYear, semester, yPosition);

                // Draw results table
                yPosition = DrawResultsTable(graphics, semesterData, yPosition, page);

                // Draw comment
                var comment = GetSemesterComment(semesterData);
                yPosition = DrawComment(graphics, comment, yPosition);

                // Draw signatures (before footer)
                yPosition = DrawSignatures(graphics, student, yPosition);

                // Draw footer with date and QR code only
                DrawFooter(graphics, student, page);

                return SaveDocument(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating semester transcript for Student ID: {StudentId}", studentId);
                throw;
            }
        }

        public async Task<byte[]> GenerateFullTranscriptAsync(int studentId)
        {
            try
            {
                var student = await GetStudentDetailsAsync(studentId);
                if (student == null)
                    throw new ArgumentException($"Student with ID {studentId} not found");

                // Get all academic years with results
                var allResults = await GetAllStudentResultsAsync(studentId);
                if (!allResults.Any())
                    throw new ArgumentException($"No results found for student");

                // Group results by academic year and semester
                var groupedResults = allResults
                    .GroupBy(r => new { r.AcademicYearId, r.YearPeriodId })
                    .OrderBy(g => g.Key.AcademicYearId)
                    .ThenBy(g => g.Key.YearPeriodId)
                    .ToList();

                var document = new PdfDocument();

                // Track pages as we create them
                var pages = new List<PdfPage>();
                PdfPage currentPage = document.AddPage();
                currentPage.Width = PageWidth;
                currentPage.Height = PageHeight;
                pages.Add(currentPage);

                XGraphics graphics = XGraphics.FromPdfPage(currentPage);
                double yPosition = MarginTop;

                // Draw header on first page
                yPosition = DrawHeader(graphics, yPosition);

                foreach (var group in groupedResults)
                {
                    var academicYear = await _context.AcademicYears.FindAsync(group.Key.AcademicYearId);
                    var semesterResults = group.ToList();

                    // Calculate space needed for this semester section
                    double sectionHeight = 90; // Info section
                    sectionHeight += 25 + (semesterResults.Count * 18); // Table
                    sectionHeight += 40; // Comment and spacing

                    // Check if we need a new page
                    if (yPosition + sectionHeight > PageHeight - MarginBottom - 80) // Extra space for signatures
                    {
                        // IMPORTANT: Dispose current graphics before creating new page
                        graphics.Dispose();

                        // Create new page
                        currentPage = document.AddPage();
                        currentPage.Width = PageWidth;
                        currentPage.Height = PageHeight;
                        pages.Add(currentPage);

                        graphics = XGraphics.FromPdfPage(currentPage);
                        yPosition = MarginTop + 20;
                    }

                    // Draw semester section
                    yPosition = DrawSemesterInfo(graphics, student, academicYear, group.Key.YearPeriodId ?? 0, yPosition);
                    yPosition = DrawResultsTable(graphics, semesterResults, yPosition, currentPage);

                    var comment = GetSemesterComment(semesterResults);
                    yPosition = DrawComment(graphics, comment, yPosition);
                    yPosition += 25; // Space between semesters
                }

                // Draw signatures before footer on last page (use current graphics, don't create new)
                DrawSignatures(graphics, student, yPosition);

                // Dispose the last graphics object before creating new ones for page numbers
                graphics.Dispose();

                // Now add page numbers to all pages
                for (int i = 0; i < pages.Count; i++)
                {
                    using (var pageGraphics = XGraphics.FromPdfPage(pages[i]))
                    {
                        DrawPageNumber(pageGraphics, i + 1, pages.Count);
                    }
                }

                // Draw footer (date + QR) on last page only
                using (var lastPageGraphics = XGraphics.FromPdfPage(pages[pages.Count - 1]))
                {
                    DrawFooter(lastPageGraphics, student, pages[pages.Count - 1]);
                }

                return SaveDocument(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating full transcript for Student ID: {StudentId}", studentId);
                throw;
            }
        }

        public string GenerateTranscriptQRData(string studentNumber, DateTime generatedDate)
        {
            return $"Student: {studentNumber} | Generated: {generatedDate:dd.MM.yyyy HH:mm}";
        }

        // Helper Methods

        private async Task<Student> GetStudentDetailsAsync(int studentId)
        {
            return await _context.Students
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Include(s => s.AcademicYear)
                .FirstOrDefaultAsync(s => s.Id == studentId);
        }

        private async Task<List<StudentCourseResult>> GetSemesterResultsAsync(int studentId, int academicYearId, int semester)
        {
            return await _context.StudentCourseResults
                .Include(r => r.Course)
                .Where(r => r.StudentId == studentId &&
                           r.AcademicYearId == academicYearId &&
                           r.YearPeriodId == semester &&
                           r.Status == SIS.Enums.Status.Published)
                .OrderBy(r => r.Course.CourseCode)
                .ToListAsync();
        }

        private async Task<List<StudentCourseResult>> GetAllStudentResultsAsync(int studentId)
        {
            return await _context.StudentCourseResults
                .Include(r => r.Course)
                .Where(r => r.StudentId == studentId &&
                           r.Status == SIS.Enums.Status.Published)
                .OrderBy(r => r.AcademicYearId)
                .ThenBy(r => r.YearPeriodId)
                .ThenBy(r => r.Course.CourseCode)
                .ToListAsync();
        }

        private double DrawHeader(XGraphics graphics, double yPosition)
        {
            var institution = _institutionConfig.GetCurrentInstitution();

            // Draw logo (centered)
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", institution.LogoPath.TrimStart('/'));
            double logoSize = 55;
            double logoCenterX = PageWidth / 2;

            try
            {
                if (File.Exists(logoPath))
                {
                    using (var image = XImage.FromFile(logoPath))
                    {
                        var logoRect = new XRect(logoCenterX - logoSize / 2, yPosition, logoSize, logoSize);
                        graphics.DrawImage(image, logoRect);
                    }
                }
                else
                {
                    DrawLogoPlaceholder(graphics, new XRect(logoCenterX - logoSize / 2, yPosition, logoSize, logoSize));
                }
            }
            catch
            {
                DrawLogoPlaceholder(graphics, new XRect(logoCenterX - logoSize / 2, yPosition, logoSize, logoSize));
            }

            yPosition += logoSize + 8;

            // Institution name (centered, bold)
            var titleFont = new XFont("Arial", 14, XFontStyle.Bold);
            graphics.DrawString(institution.Name.ToUpper(), titleFont, XBrushes.Black,
                new XRect(0, yPosition, PageWidth, 20), XStringFormats.TopCenter);
            yPosition += 18;

            // Contact details (centered, smaller)
            var contactFont = new XFont("Arial", 8, XFontStyle.Regular);
            var contactInfo = $"{institution.ContactInfo.Phone}";
            graphics.DrawString(contactInfo, contactFont, new XSolidBrush(SecondaryColor),
                new XRect(0, yPosition, PageWidth, 12), XStringFormats.TopCenter);
            yPosition += 12;

            graphics.DrawString(institution.ContactInfo.Website, contactFont, new XSolidBrush(SecondaryColor),
                new XRect(0, yPosition, PageWidth, 12), XStringFormats.TopCenter);
            yPosition += 18;

            // "RESULTS TRANSCRIPT" title (centered, bold, NO BORDER)
            var transcriptTitleFont = new XFont("Arial", 12, XFontStyle.Bold);
            graphics.DrawString("RESULTS TRANSCRIPT", transcriptTitleFont, new XSolidBrush(PrimaryColor),
                new XRect(0, yPosition, PageWidth, 20), XStringFormats.TopCenter);
            yPosition += 25;

            // ONLY bottom border line
            graphics.DrawLine(new XPen(PrimaryColor, 2), 0, yPosition, PageWidth, yPosition);
            yPosition += 15;

            return yPosition;
        }

        private double DrawSemesterInfo(XGraphics graphics, Student student, AcademicYear academicYear, int semester, double yPosition)
        {
            var labelFont = new XFont("Arial", 9, XFontStyle.Bold);
            var valueFont = new XFont("Arial", 9, XFontStyle.Regular);

            // NO background box, NO border - just plain text
            double labelX = MarginLeft;
            double valueX = MarginLeft + 130; // Fixed position for all values
            double lineHeight = 15;

            // Year of study text
            string yearOfStudyText = $"{GetOrdinal(student.StudentCurrentYear ?? 1)} YEAR/PERIOD {semester}";

            // Single column layout - plain text, no box
            graphics.DrawString("NAME", labelFont, new XSolidBrush(PrimaryColor), labelX, yPosition);
            string fullName = $"{student.FullName}".ToUpper();
            graphics.DrawString(fullName, valueFont, XBrushes.Black, valueX, yPosition);
            yPosition += lineHeight;

            graphics.DrawString("YEAR OF STUDY", labelFont, new XSolidBrush(PrimaryColor), labelX, yPosition);
            graphics.DrawString(yearOfStudyText, valueFont, XBrushes.Black, valueX, yPosition);
            yPosition += lineHeight;

            graphics.DrawString("PROGRAMME", labelFont, new XSolidBrush(PrimaryColor), labelX, yPosition);
            string programmeName = student.Programme?.Name?.ToUpper() ?? "N/A";
            // Wrap long programme names
            if (programmeName.Length > 50)
            {
                string line1 = programmeName.Substring(0, 50);
                string line2 = programmeName.Substring(50);
                if (line2.Length > 50) line2 = line2.Substring(0, 47) + "...";
                graphics.DrawString(line1, valueFont, XBrushes.Black, valueX, yPosition);
                yPosition += lineHeight;
                graphics.DrawString(line2, valueFont, XBrushes.Black, valueX, yPosition);
            }
            else
            {
                graphics.DrawString(programmeName, valueFont, XBrushes.Black, valueX, yPosition);
            }
            yPosition += lineHeight;

            graphics.DrawString("SCHOOL: ", labelFont, new XSolidBrush(PrimaryColor), labelX, yPosition);
            graphics.DrawString(student.Programme?.Department?.School?.Name?.ToUpper() ?? "N/A", valueFont, XBrushes.Black, valueX, yPosition);
            yPosition += lineHeight;

            graphics.DrawString("STUDENT NUMBER:", labelFont, new XSolidBrush(PrimaryColor), labelX, yPosition);
            graphics.DrawString(student.StudentId_Number ?? "N/A", valueFont, XBrushes.Black, valueX, yPosition);
            yPosition += lineHeight;

            graphics.DrawString("NRC/PASSPORT: ", labelFont, new XSolidBrush(PrimaryColor), labelX, yPosition);
            graphics.DrawString(student.NrcOrPassportNumber ?? "N/A", valueFont, XBrushes.Black, valueX, yPosition);
            yPosition += lineHeight;

            graphics.DrawString("ACADEMIC YEAR:", labelFont, new XSolidBrush(PrimaryColor), labelX, yPosition);
            graphics.DrawString(academicYear?.YearValue ?? "N/A", valueFont, XBrushes.Black, valueX, yPosition);

            yPosition += 25; // Space after student info section

            return yPosition;
        }

        private double DrawResultsTable(XGraphics graphics, List<StudentCourseResult> results, double yPosition, PdfPage page)
        {
            var headerFont = new XFont("Arial", 9, XFontStyle.Bold);
            var dataFont = new XFont("Arial", 8, XFontStyle.Regular);

            // Table header with background
            double tableWidth = PageWidth - MarginLeft - MarginRight;
            var headerRect = new XRect(MarginLeft, yPosition, tableWidth, 20);
            graphics.DrawRectangle(new XPen(PrimaryColor, 1), new XSolidBrush(PrimaryColor), headerRect);

            // Define column positions and widths
            double col1X = MarginLeft + 5;      // Course Code
            double col1Width = 80;
            double col2X = col1X + col1Width;   // Course Title
            double col2Width = 280;
            double col3X = col2X + col2Width;   // Marks
            double col3Width = 50;
            double col4X = col3X + col3Width;   // Grade
            double col4Width = 50;

            // Draw table header text (white on blue background)
            graphics.DrawString("COURSE CODE", headerFont, XBrushes.White, col1X, yPosition + 13);
            graphics.DrawString("COURSE TITLE", headerFont, XBrushes.White, col2X, yPosition + 13);
            graphics.DrawString("MARKS", headerFont, XBrushes.White, col3X + 10, yPosition + 13);
            graphics.DrawString("GRADE", headerFont, XBrushes.White, col4X + 10, yPosition + 13);

            yPosition += 20;

            // Draw each course result with alternating row colors
            bool isEven = false;
            foreach (var result in results)
            {
                // Check if we need a new page
                if (yPosition + 18 > PageHeight - MarginBottom - 80)
                {
                    break; // Stop drawing more courses on this page
                }

                // Alternating row background
                if (isEven)
                {
                    var rowRect = new XRect(MarginLeft, yPosition, tableWidth, 16);
                    graphics.DrawRectangle(new XSolidBrush(LightGray), rowRect);
                }

                graphics.DrawString(result.Course.CourseCode ?? "N/A", dataFont, XBrushes.Black, col1X, yPosition + 11);

                // Wrap course title if too long
                string courseTitle = result.Course.CourseName ?? "N/A";
                if (courseTitle.Length > 42)
                {
                    courseTitle = courseTitle.Substring(0, 39) + "...";
                }
                graphics.DrawString(courseTitle, dataFont, XBrushes.Black, col2X, yPosition + 11);

                // Marks (centered in column)
                string marks = result.NormalizedTotal.ToString("F0") ?? "--";
                var marksWidth = graphics.MeasureString(marks, dataFont).Width;
                graphics.DrawString(marks, dataFont, XBrushes.Black, col3X + (col3Width - marksWidth) / 2, yPosition + 11);

                // Grade (centered in column)
                string grade = result.GradeLetter ?? "--";
                var gradeWidth = graphics.MeasureString(grade, dataFont).Width;
                graphics.DrawString(grade, dataFont, XBrushes.Black, col4X + (col4Width - gradeWidth) / 2, yPosition + 11);

                yPosition += 16;
                isEven = !isEven;
            }

            // Draw bottom border of table
            graphics.DrawLine(new XPen(BorderGray, 1), MarginLeft, yPosition, PageWidth - MarginRight, yPosition);
            yPosition += 15;

            return yPosition;
        }

        private double DrawComment(XGraphics graphics, string comment, double yPosition)
        {
            var labelFont = new XFont("Arial", 9, XFontStyle.Bold);
            var valueFont = new XFont("Arial", 9, XFontStyle.Regular);

            // Comment box
            var commentRect = new XRect(MarginLeft, yPosition, PageWidth - MarginLeft - MarginRight, 25);
            graphics.DrawRectangle(new XPen(BorderGray, 1), XBrushes.White, commentRect);

            graphics.DrawString("COMMENT:", labelFont, new XSolidBrush(PrimaryColor), MarginLeft + 10, yPosition + 16);
            graphics.DrawString(comment, valueFont, XBrushes.Black, MarginLeft + 90, yPosition + 16);

            yPosition += 35;

            return yPosition;
        }

        private double DrawSignatures(XGraphics graphics, Student student, double yPosition)
        {
            var labelFont = new XFont("Arial", 7, XFontStyle.Bold);

            // Add some space before signatures
            yPosition += 20;

            double signatureWidth = 150;

            // Dean signature (left) - JUST "DEAN"
            graphics.DrawLine(new XPen(XBrushes.Black, 0.5), MarginLeft, yPosition, MarginLeft + signatureWidth, yPosition);
            graphics.DrawString("DEAN", labelFont, XBrushes.Black, MarginLeft, yPosition + 8);

            // Registrar signature (right)
            double registrarX = PageWidth - MarginRight - signatureWidth;
            graphics.DrawLine(new XPen(XBrushes.Black, 0.5), registrarX, yPosition, registrarX + signatureWidth, yPosition);
            graphics.DrawString("REGISTRAR", labelFont, XBrushes.Black, registrarX, yPosition + 8);

            yPosition += 30;

            return yPosition;
        }

        private void DrawFooter(XGraphics graphics, Student student, PdfPage page)
        {
            var font = new XFont("Arial", 8, XFontStyle.Regular);
            var labelFont = new XFont("Arial", 7, XFontStyle.Bold);

            double footerY = PageHeight - 70;

            // Top border line for footer
            graphics.DrawLine(new XPen(BorderGray, 1), MarginLeft, footerY, PageWidth - MarginRight, footerY);
            footerY += 15;

            // Date (left side)
            string dateText = DateTime.Now.ToString("dd.MM.yyyy");
            graphics.DrawString(dateText, font, XBrushes.Black, MarginLeft, footerY);
            graphics.DrawString("DATE", labelFont, XBrushes.Black, MarginLeft, footerY + 15);

            // Generate and draw QR code (right side)
            var qrData = GenerateTranscriptQRData(student.StudentId_Number, DateTime.Now);
            var qrCode = GenerateQRCodeImage(qrData);

            if (qrCode != null)
            {
                double qrSize = 45;
                double qrX = PageWidth - MarginRight - qrSize;
                graphics.DrawImage(qrCode, new XRect(qrX, footerY - 5, qrSize, qrSize));

                // QR label centered under QR code
                graphics.DrawString("SCAN TO VERIFY", labelFont, new XSolidBrush(SecondaryColor),
                    new XRect(qrX, footerY + qrSize, qrSize, 10), XStringFormats.TopCenter);
            }
        }

        private void DrawPageNumber(XGraphics graphics, int pageNumber, int totalPages)
        {
            var font = new XFont("Arial", 8, XFontStyle.Regular);
            string pageText = $"Page {pageNumber} of {totalPages}";
            var textWidth = graphics.MeasureString(pageText, font).Width;

            graphics.DrawString(pageText, font, new XSolidBrush(SecondaryColor),
                (PageWidth - textWidth) / 2, PageHeight - 15);
        }

        private void DrawLogoPlaceholder(XGraphics graphics, XRect rect)
        {
            graphics.DrawEllipse(new XPen(PrimaryColor, 2), new XSolidBrush(LightGray), rect);

            var institution = _institutionConfig.GetCurrentInstitution();
            var initials = GetInstitutionInitials(institution.Name);

            var font = new XFont("Arial", 12, XFontStyle.Bold);
            graphics.DrawString(initials, font, new XSolidBrush(PrimaryColor),
                new XRect(rect.X, rect.Y, rect.Width, rect.Height), XStringFormats.Center);
        }

        private string GetInstitutionInitials(string institutionName)
        {
            if (string.IsNullOrEmpty(institutionName)) return "UNIV";

            var words = institutionName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
            {
                return words[0].Length >= 2 ? words[0].Substring(0, 2).ToUpper() : words[0].ToUpper();
            }

            var initials = "";
            foreach (var word in words.Take(3))
            {
                if (!string.IsNullOrEmpty(word))
                {
                    initials += word[0];
                }
            }

            return initials.ToUpper();
        }

        private XImage GenerateQRCodeImage(string data)
        {
            try
            {
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);

                    PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                    byte[] qrCodeBytes = qrCode.GetGraphic(20);

                    var ms = new MemoryStream(qrCodeBytes);
                    return XImage.FromStream(() => ms);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code");
                return null;
            }
        }

        private string GetSemesterComment(List<StudentCourseResult> results)
        {
            int passedCount = results.Count(r => r.IsPassed);
            int totalCount = results.Count;

            if (passedCount == totalCount)
                return "CLEAR PASS";
            else if (passedCount == 0)
                return "FAILED ALL COURSES";
            else
                return $"PASSED {passedCount}/{totalCount} COURSES";
        }

        private string GetOrdinal(int number)
        {
            if (number <= 0) return number.ToString();

            switch (number % 100)
            {
                case 11:
                case 12:
                case 13:
                    return number + "TH";
            }

            switch (number % 10)
            {
                case 1:
                    return number + "ST";
                case 2:
                    return number + "ND";
                case 3:
                    return number + "RD";
                default:
                    return number + "TH";
            }
        }

        private byte[] SaveDocument(PdfDocument document)
        {
            using (var stream = new MemoryStream())
            {
                document.Save(stream, false);
                return stream.ToArray();
            }
        }
    }
}
