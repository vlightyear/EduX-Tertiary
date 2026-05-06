using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SIS.Data;
using SIS.Models.StudentApplication;
using SIS.Models.ViewModels;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using SIS.Models.Admin;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using iTextSharp.text.pdf.qrcode;


namespace SIS.Services.PDF
{
    public class PdfInvoiceService : IPdfInvoiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PdfInvoiceService> _logger;
        private readonly IInstitutionConfigService _institutionConfig;

        public PdfInvoiceService(ApplicationDbContext context, ILogger<PdfInvoiceService> logger, IInstitutionConfigService institutionConfig)
        {
            _context = context;
            _logger = logger;
            _institutionConfig = institutionConfig;
        }

        public async Task<byte[]> GenerateProgrammeFeesInvoiceAsync(int programmeId, int yearOfStudy, string applicantName = null)
        {
            try
            {
                // Get programme details
                var programme = await _context.Programmes
                    .Include(p => p.Department)
                        .ThenInclude(d => d.School)
                    .Include(p => p.ProgrammeLevel)
                    .Include(p => p.ModeOfStudy)
                    .FirstOrDefaultAsync(p => p.Id == programmeId);

                if (programme == null)
                    throw new ArgumentException($"Programme with ID {programmeId} not found");

                // Get fees for the specific year
                var fees = await GetFeeBreakdownForProgramme(programmeId, yearOfStudy);

                // Create invoice view model
                var invoiceViewModel = new ProgrammeInvoiceViewModel
                {
                    ProgrammeId = programmeId,
                    ProgrammeName = programme.Name,
                    SchoolName = programme.Department?.School?.Name ?? "N/A",
                    DepartmentName = programme.Department?.Name ?? "N/A",
                    YearOfStudy = yearOfStudy,
                    ApplicantName = applicantName ?? "Prospective Student",
                    GeneratedDate = DateTime.Now,
                    FeeDetails = fees,
                    TotalAmount = fees.Sum(f => f.Amount),
                    InvoiceNumber = GenerateInvoiceNumber(programmeId, yearOfStudy)
                };

                // Generate PDF
                return GenerateProgrammeInvoicePdf(invoiceViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating programme fees invoice for Programme ID: {ProgrammeId}", programmeId);
                throw;
            }
        }

        public async Task<byte[]> GenerateApplicationFeesInvoiceAsync(string referenceNumber)
        {
            try
            {
                // Get application details
                var application = await _context.Applicants
                    .Include(a => a.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Include(a => a.ProgrammeLevel)
                    .FirstOrDefaultAsync(a => a.ReferenceNumber == referenceNumber);

                if (application == null)
                    throw new ArgumentException($"Application with reference {referenceNumber} not found");

                // Get applicable fees for candidate
                var fees = await GetCandidateApplicableFees(application);

                // Generate PDF for application fees
                return GenerateApplicationInvoicePdf(application, fees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating application fees invoice for reference: {ReferenceNumber}", referenceNumber);
                throw;
            }
        }

        // Helper methods
        private async Task<List<ProgrammeFeeDetailViewModel>> GetFeeBreakdownForProgramme(int programmeId, int yearOfStudy)
        {
            // Get current academic year
            var currentAcademicYear = await _context.AcademicYears
                .Where(a => a.IsActive)
                .FirstOrDefaultAsync();

            if (currentAcademicYear == null)
                return new List<ProgrammeFeeDetailViewModel>();

            // Get programme details
            var programme = await _context.Programmes
                .Include(p => p.Department)
                .Include(p => p.ProgrammeLevel)
                .Include(p => p.ModeOfStudy)
                .FirstOrDefaultAsync(p => p.Id == programmeId);

            if (programme == null)
                return new List<ProgrammeFeeDetailViewModel>();

            // Get applicable fees
            var fees = await _context.FeeConfigurations
                .Include(f => f.FeeType)
                .Where(f => f.AcademicYearId == currentAcademicYear.YearId &&
                           f.FeeType.ApplicableFor == "Student" &&
                           f.FeeType.IsActive &&
                           (f.YearOfStudy == null || f.YearOfStudy == yearOfStudy) &&
                           (f.AppliesUniversally ||
                            f.ProgrammeId == programmeId ||
                            f.SchoolId == programme.Department.SchoolId ||
                            f.ProgramLevelId == programme.ProgrammeLevelId ||
                            f.ModeOfStudyId == programme.ModeOfStudyId))
                .ToListAsync();

            return fees.Select(f => new ProgrammeFeeDetailViewModel
            {
                FeeName = f.FeeType.Name,
                Description = f.FeeType.Description ?? f.FeeType.Name,
                Amount = f.Amount,
                YearApplicable = f.YearOfStudy ?? yearOfStudy
            }).ToList();
        }

        private async Task<List<ProgrammeFeeDetailViewModel>> GetCandidateApplicableFees(SIS.Models.StudentApplication.Applicant application)
        {
            var result = new List<ProgrammeFeeDetailViewModel>();

            var applicationFees = await _context.FeeConfigurations
                .Include(f => f.FeeType)
                .Where(f =>
                    f.FeeType.ApplicableFor == "Candidate" &&
                    f.FeeType.IsActive &&
                    f.AcademicYearId == application.AcademicYearId &&
                    (f.AppliesUniversally ||
                     f.ProgrammeId == application.ProgrammeId ||
                     f.SchoolId == application.SchoolId ||
                     f.ProgramLevelId == application.ProgrammeLevelId ||
                     f.ModeOfStudyId == application.ModeOfStudyId))
                .ToListAsync();

            foreach (var fee in applicationFees)
            {
                result.Add(new ProgrammeFeeDetailViewModel
                {
                    FeeName = fee.FeeType.Name,
                    Description = fee.FeeType.Description ?? fee.FeeType.Name,
                    Amount = fee.Amount,
                    YearApplicable = 1
                });
            }

            // If no fees found, add default application fee
            if (!result.Any())
            {
                result.Add(new ProgrammeFeeDetailViewModel
                {
                    FeeName = "Application Fee",
                    Description = "Standard application processing fee",
                    Amount = 250.00m,
                    YearApplicable = 1
                });
            }

            return result;
        }

        private string GenerateInvoiceNumber(int programmeId, int yearOfStudy)
        {
            return $"INV-PROG-{programmeId}-Y{yearOfStudy}-{DateTime.Now:yyyyMMddHHmm}";
        }

        private byte[] GenerateMultiPageProgrammeInvoice(ProgrammeInvoiceViewModel invoice)
        {
            var document = new PdfDocument();

            // Set up fonts
            var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
            var subHeaderFont = new XFont("Arial", 11, XFontStyle.Bold);
            var regularFont = new XFont("Arial", 9);
            var smallFont = new XFont("Arial", 8);
            var tableHeaderFont = new XFont("Arial", 9, XFontStyle.Bold);

            // Colors
            var primaryColor = XColor.FromArgb(8, 116, 144);
            var secondaryColor = XColor.FromArgb(71, 85, 105);
            var accentColor = XColor.FromArgb(16, 185, 129);
            var lightGray = XColor.FromArgb(248, 250, 252);
            var darkGray = XColor.FromArgb(51, 65, 85);
            var borderGray = XColor.FromArgb(226, 232, 240);

            // Layout measurements
            const double margin = 30;
            const double contentWidth = 535;
            const double lineHeight = 16;
            const double smallLineHeight = 14;

            // First page with header info
            var page1 = document.AddPage();
            page1.Size = PdfSharpCore.PageSize.A4;
            var graphics1 = XGraphics.FromPdfPage(page1);

            double yPosition = margin;

            // Compact Header
            var headerRect = new XRect(0, 0, page1.Width, 60);
            var headerBrush = new XLinearGradientBrush(
                new XPoint(0, 0),
                new XPoint(page1.Width, 60),
                primaryColor,
                XColor.FromArgb(59, 130, 246)
            );
            graphics1.DrawRectangle(headerBrush, headerRect);

            // Logo
            graphics1.DrawEllipse(new XPen(XColors.White, 2), XBrushes.White,
                new XRect(margin, 10, 40, 40));
            graphics1.DrawString("UNIV", new XFont("Arial", 10, XFontStyle.Bold),
                new XSolidBrush(primaryColor), new XPoint(margin + 10, 33));

            // Header text
            graphics1.DrawString("UNIVERSITY FINANCIAL SERVICES",
                new XFont("Arial", 12, XFontStyle.Bold), XBrushes.White,
                new XPoint(margin + 60, 25));
            graphics1.DrawString("PROGRAMME FEE INVOICE", titleFont, XBrushes.White,
                new XPoint(margin + 60, 43));

            yPosition = 80;

            // Invoice details
            var cardRect = new XRect(margin, yPosition, contentWidth, 70);
            graphics1.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), cardRect);

            yPosition += 10;
            graphics1.DrawString("Invoice Details", subHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 10, yPosition));

            yPosition += lineHeight + 5;
            graphics1.DrawString($"Invoice #: {invoice.InvoiceNumber}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));
            graphics1.DrawString($"Year: {GetYearDisplayName(invoice.YearOfStudy)}", regularFont, XBrushes.Black,
                new XPoint(margin + 140, yPosition));
            graphics1.DrawString($"Academic Year: {DateTime.Now.Year}/{DateTime.Now.Year + 1}", regularFont, XBrushes.Black,
                new XPoint(margin + 280, yPosition));

            yPosition += smallLineHeight;
            graphics1.DrawString($"Generated: {invoice.GeneratedDate:dd MMM yyyy}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));

            yPosition = 170;

            // Student Information
            var studentRect = new XRect(margin, yPosition, contentWidth, 50);
            graphics1.DrawRectangle(new XPen(borderGray), XBrushes.White, studentRect);

            yPosition += 10;
            graphics1.DrawString("STUDENT INFORMATION", subHeaderFont, new XSolidBrush(primaryColor),
                new XPoint(margin + 10, yPosition));

            yPosition += lineHeight + 3;
            graphics1.DrawString($"Name: {invoice.ApplicantName}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));

            yPosition = 240;

            // Programme Information
            var programmeRect = new XRect(margin, yPosition, contentWidth, 70);
            graphics1.DrawRectangle(new XPen(borderGray), XBrushes.White, programmeRect);

            yPosition += 10;
            graphics1.DrawString("PROGRAMME DETAILS", subHeaderFont, new XSolidBrush(primaryColor),
                new XPoint(margin + 10, yPosition));

            yPosition += lineHeight + 3;
            graphics1.DrawString($"Programme: {invoice.ProgrammeName}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));

            yPosition += smallLineHeight;
            graphics1.DrawString($"School: {invoice.SchoolName}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));
            graphics1.DrawString($"Department: {invoice.DepartmentName}", regularFont, XBrushes.Black,
                new XPoint(margin + 280, yPosition));

            yPosition = 330;

            // Fee Breakdown Section
            graphics1.DrawString("FEE BREAKDOWN", headerFont, new XSolidBrush(primaryColor),
                new XPoint(margin, yPosition));

            yPosition += lineHeight + 8;

            // Table header
            var tableHeaderRect = new XRect(margin, yPosition, contentWidth, 25);
            graphics1.DrawRectangle(new XPen(borderGray), new XSolidBrush(XColor.FromArgb(241, 245, 249)), tableHeaderRect);

            yPosition += 5;
            graphics1.DrawString("Description", tableHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 10, yPosition + 12));
            graphics1.DrawString("Details", tableHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 220, yPosition + 12));
            graphics1.DrawString("Amount (ZMW)", tableHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 420, yPosition + 12));

            yPosition += 25;

            // Calculate how many fees fit on first page
            double availableSpaceFirstPage = page1.Height - yPosition - 80; // 80 for footer
            int feesPerPage = (int)(availableSpaceFirstPage / 22); // 22 pixels per fee row
            int feesOnFirstPage = Math.Min(feesPerPage, invoice.FeeDetails.Count);

            // Draw fees on first page
            bool isEven = false;
            for (int i = 0; i < feesOnFirstPage; i++)
            {
                var fee = invoice.FeeDetails[i];

                if (isEven)
                {
                    var rowRect = new XRect(margin, yPosition, contentWidth, 22);
                    graphics1.DrawRectangle(new XSolidBrush(XColor.FromArgb(248, 250, 252)), rowRect);
                }

                yPosition += 3;
                graphics1.DrawString(fee.FeeName, regularFont, XBrushes.Black,
                    new XPoint(margin + 10, yPosition + 10));

                var descriptionRect = new XRect(margin + 220, yPosition + 4, 190, 20);
                graphics1.DrawString(fee.Description, smallFont, new XSolidBrush(secondaryColor),
                    descriptionRect, XStringFormats.TopLeft);

                graphics1.DrawString($"{fee.Amount:N2}", regularFont, XBrushes.Black,
                    new XPoint(margin + 450, yPosition + 10));

                yPosition += 19;
                graphics1.DrawLine(new XPen(borderGray), margin, yPosition, margin + contentWidth, yPosition);
                isEven = !isEven;
            }

            // Add "Continued on next page" if there are more fees
            if (feesOnFirstPage < invoice.FeeDetails.Count)
            {
                yPosition += 10;
                graphics1.DrawString("Continued on next page...",
                    new XFont("Arial", 9, XFontStyle.Italic), new XSolidBrush(secondaryColor),
                    new XPoint(margin + 200, yPosition));
            }

            // Add page number
            graphics1.DrawString("Page 1", smallFont, new XSolidBrush(secondaryColor),
                new XPoint(margin + 480, page1.Height - 20));

            // Second page for remaining fees and totals
            if (feesOnFirstPage < invoice.FeeDetails.Count)
            {
                var page2 = document.AddPage();
                page2.Size = PdfSharpCore.PageSize.A4;
                var graphics2 = XGraphics.FromPdfPage(page2);

                yPosition = margin;

                // Simple header for second page
                graphics2.DrawString("FEE BREAKDOWN (CONTINUED)", headerFont, new XSolidBrush(primaryColor),
                    new XPoint(margin, yPosition));

                yPosition += lineHeight + 8;

                // Table header for second page
                var tableHeaderRect2 = new XRect(margin, yPosition, contentWidth, 25);
                graphics2.DrawRectangle(new XPen(borderGray), new XSolidBrush(XColor.FromArgb(241, 245, 249)), tableHeaderRect2);

                yPosition += 5;
                graphics2.DrawString("Description", tableHeaderFont, new XSolidBrush(darkGray),
                    new XPoint(margin + 10, yPosition + 12));
                graphics2.DrawString("Details", tableHeaderFont, new XSolidBrush(darkGray),
                    new XPoint(margin + 220, yPosition + 12));
                graphics2.DrawString("Amount (ZMW)", tableHeaderFont, new XSolidBrush(darkGray),
                    new XPoint(margin + 420, yPosition + 12));

                yPosition += 25;

                // Draw remaining fees on second page
                isEven = false;
                for (int i = feesOnFirstPage; i < invoice.FeeDetails.Count; i++)
                {
                    var fee = invoice.FeeDetails[i];

                    if (isEven)
                    {
                        var rowRect = new XRect(margin, yPosition, contentWidth, 22);
                        graphics2.DrawRectangle(new XSolidBrush(XColor.FromArgb(248, 250, 252)), rowRect);
                    }

                    yPosition += 3;
                    graphics2.DrawString(fee.FeeName, regularFont, XBrushes.Black,
                        new XPoint(margin + 10, yPosition + 10));

                    var descriptionRect = new XRect(margin + 220, yPosition + 4, 190, 20);
                    graphics2.DrawString(fee.Description, smallFont, new XSolidBrush(secondaryColor),
                        descriptionRect, XStringFormats.TopLeft);

                    graphics2.DrawString($"{fee.Amount:N2}", regularFont, XBrushes.Black,
                        new XPoint(margin + 450, yPosition + 10));

                    yPosition += 19;
                    graphics2.DrawLine(new XPen(borderGray), margin, yPosition, margin + contentWidth, yPosition);
                    isEven = !isEven;
                }

                // Total Section
                yPosition += 10;
                var totalRect = new XRect(margin, yPosition, contentWidth, 35);
                graphics2.DrawRectangle(new XPen(accentColor), new XSolidBrush(XColor.FromArgb(240, 253, 244)), totalRect);

                yPosition += 5;
                graphics2.DrawString("TOTAL AMOUNT", headerFont, new XSolidBrush(accentColor),
                    new XPoint(margin + 220, yPosition + 18));
                graphics2.DrawString($"ZMW {invoice.TotalAmount:N2}", headerFont, new XSolidBrush(accentColor),
                    new XPoint(margin + 420, yPosition + 18));

                yPosition += 50;

                // Important Notes Section
                var notesRect = new XRect(margin, yPosition, contentWidth, 80);
                graphics2.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), notesRect);

                yPosition += 8;
                graphics2.DrawString("IMPORTANT NOTES", subHeaderFont, new XSolidBrush(darkGray),
                    new XPoint(margin + 10, yPosition));

                yPosition += lineHeight;

                var notes = new[]
                {
            "• Fees estimate based on current academic year rates  • Subject to change per university policy",
            "• Additional fees may apply for specific courses  • Payment plans available - contact finance office",
            "• All amounts in Zambian Kwacha (ZMW)  • Invoice valid for current academic year"
        };

                foreach (var note in notes)
                {
                    graphics2.DrawString(note, smallFont, new XSolidBrush(secondaryColor),
                        new XPoint(margin + 15, yPosition));
                    yPosition += 12;
                }

                // Footer for second page
                yPosition = page2.Height - 40;
                graphics2.DrawLine(new XPen(borderGray), margin, yPosition, margin + contentWidth, yPosition);

                yPosition += 8;
                graphics2.DrawString("Computer-generated invoice - no signature required",
                    smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin, yPosition));

                graphics2.DrawString($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}",
                    smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin + 350, yPosition));

                // Page number
                graphics2.DrawString("Page 2", smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin + 480, page2.Height - 20));
            }
            else
            {
                // If all fees fit on first page, add total and notes there
                // Total Section on first page
                yPosition += 10;
                var totalRect = new XRect(margin, yPosition, contentWidth, 35);
                graphics1.DrawRectangle(new XPen(accentColor), new XSolidBrush(XColor.FromArgb(240, 253, 244)), totalRect);

                yPosition += 5;
                graphics1.DrawString("TOTAL AMOUNT", headerFont, new XSolidBrush(accentColor),
                    new XPoint(margin + 220, yPosition + 18));
                graphics1.DrawString($"ZMW {invoice.TotalAmount:N2}", headerFont, new XSolidBrush(accentColor),
                    new XPoint(margin + 420, yPosition + 18));

                yPosition += 50;

                // Important Notes Section on first page
                var notesRect = new XRect(margin, yPosition, contentWidth, 80);
                graphics1.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), notesRect);

                yPosition += 8;
                graphics1.DrawString("IMPORTANT NOTES", subHeaderFont, new XSolidBrush(darkGray),
                    new XPoint(margin + 10, yPosition));

                yPosition += lineHeight;

                var notes = new[]
                {
            "• Fees estimate based on current academic year rates  • Subject to change per university policy",
            "• Additional fees may apply for specific courses  • Payment plans available - contact finance office",
            "• All amounts in Zambian Kwacha (ZMW)  • Invoice valid for current academic year"
        };

                foreach (var note in notes)
                {
                    graphics1.DrawString(note, smallFont, new XSolidBrush(secondaryColor),
                        new XPoint(margin + 15, yPosition));
                    yPosition += 12;
                }

                // Footer for first page
                yPosition = page1.Height - 40;
                graphics1.DrawLine(new XPen(borderGray), margin, yPosition, margin + contentWidth, yPosition);

                yPosition += 8;
                graphics1.DrawString("Computer-generated invoice - no signature required",
                    smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin, yPosition));

                graphics1.DrawString($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}",
                    smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin + 350, yPosition));
            }

            using var stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }
        private byte[] GenerateProgrammeInvoicePdf(ProgrammeInvoiceViewModel invoice)
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            var graphics = XGraphics.FromPdfPage(page);

            // Set up fonts
            var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
            var subHeaderFont = new XFont("Arial", 11, XFontStyle.Bold);
            var regularFont = new XFont("Arial", 9);
            var smallFont = new XFont("Arial", 8);
            var tableHeaderFont = new XFont("Arial", 9, XFontStyle.Bold);

            // Colors
            var primaryColor = XColor.FromArgb(8, 116, 144);
            var secondaryColor = XColor.FromArgb(71, 85, 105);
            var accentColor = XColor.FromArgb(16, 185, 129);
            var lightGray = XColor.FromArgb(248, 250, 252);
            var darkGray = XColor.FromArgb(51, 65, 85);
            var borderGray = XColor.FromArgb(226, 232, 240);

            // Layout measurements
            const double margin = 30;
            const double contentWidth = 535;
            const double lineHeight = 16;
            const double smallLineHeight = 14;
            double yPosition = margin;

            // Compact Header - reduced height from 100 to 60
            var headerRect = new XRect(0, 0, page.Width, 60);
            var headerBrush = new XLinearGradientBrush(
                new XPoint(0, 0),
                new XPoint(page.Width, 60),
                primaryColor,
                XColor.FromArgb(59, 130, 246)
            );
            graphics.DrawRectangle(headerBrush, headerRect);

            // Smaller logo/icon
            //graphics.DrawEllipse(new XPen(XColors.White, 2), XBrushes.White,
            //    new XRect(margin, 10, 40, 40));
            //graphics.DrawString("UNIV", new XFont("Arial", 10, XFontStyle.Bold),
            //    new XSolidBrush(primaryColor), new XPoint(margin + 10, 33));

            // Compact header text
            graphics.DrawString("UNIVERSITY FINANCIAL SERVICES: PROGRAMME FEE INVOICE",
                new XFont("Arial", 12, XFontStyle.Bold), XBrushes.White,
                new XPoint(margin + 60, 25));
            //graphics.DrawString("PROGRAMME FEE INVOICE", titleFont, XBrushes.White,
            //    new XPoint(margin + 60, 43));

            yPosition = 80;

            // Invoice details - more compact
            var cardRect = new XRect(margin, yPosition, contentWidth, 70);
            graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), cardRect);

            yPosition += 10;
            graphics.DrawString("Invoice Details", subHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 10, yPosition));

            yPosition += lineHeight + 5;

            // Four columns layout for more efficient space use
            graphics.DrawString($"Invoice #: {invoice.InvoiceNumber}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));
            //graphics.DrawString($"Year: {GetYearDisplayName(invoice.YearOfStudy)}", regularFont, XBrushes.Black,
            //    new XPoint(margin + 140, yPosition));
            graphics.DrawString($"Year: {GetYearDisplayName(invoice.YearOfStudy)}", regularFont, XBrushes.Black,
                new XPoint(margin + 280, yPosition));

            yPosition += smallLineHeight;
            graphics.DrawString($"Generated: {invoice.GeneratedDate:dd MMM yyyy}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));

            yPosition = 170;

            // Student Information - compact
            var studentRect = new XRect(margin, yPosition, contentWidth, 50);
            graphics.DrawRectangle(new XPen(borderGray), XBrushes.White, studentRect);

            yPosition += 10;
            graphics.DrawString("STUDENT INFORMATION", subHeaderFont, new XSolidBrush(primaryColor),
                new XPoint(margin + 10, yPosition));

            yPosition += lineHeight + 3;
            graphics.DrawString($"Name: {invoice.ApplicantName}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));

            yPosition = 240;

            // Programme Information - compact
            var programmeRect = new XRect(margin, yPosition, contentWidth, 70);
            graphics.DrawRectangle(new XPen(borderGray), XBrushes.White, programmeRect);

            yPosition += 10;
            graphics.DrawString("PROGRAMME DETAILS", subHeaderFont, new XSolidBrush(primaryColor),
                new XPoint(margin + 10, yPosition));

            yPosition += lineHeight + 3;
            graphics.DrawString($"Programme: {invoice.ProgrammeName}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));

            yPosition += smallLineHeight;
            graphics.DrawString($"School: {invoice.SchoolName}", regularFont, XBrushes.Black,
                new XPoint(margin + 10, yPosition));
            graphics.DrawString($"Department: {invoice.DepartmentName}", regularFont, XBrushes.Black,
                new XPoint(margin + 280, yPosition));

            yPosition = 330;

            // Fee Breakdown Section
            graphics.DrawString("FEE BREAKDOWN", headerFont, new XSolidBrush(primaryColor),
                new XPoint(margin, yPosition));

            yPosition += lineHeight + 8;

            // Table header with background
            var tableHeaderRect = new XRect(margin, yPosition, contentWidth, 25);
            graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(XColor.FromArgb(241, 245, 249)), tableHeaderRect);

            yPosition += 5;

            // Table headers
            graphics.DrawString("Description", tableHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 10, yPosition + 12));
            graphics.DrawString("Details", tableHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 220, yPosition + 12));
            graphics.DrawString("Amount (ZMW)", tableHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 420, yPosition + 12));

            yPosition += 25;

            // Calculate if we need multiple pages
            double estimatedTableHeight = invoice.FeeDetails.Count * 22 + 60; // 60 for total section
            double remainingSpace = page.Height - yPosition - 120; // 120 for footer and notes

            if (estimatedTableHeight > remainingSpace && invoice.FeeDetails.Count > 5)
            {
                // Multi-page approach
                return GenerateMultiPageProgrammeInvoice(invoice);
            }

            // Single page approach - compact table
            bool isEven = false;
            foreach (var fee in invoice.FeeDetails)
            {
                if (isEven)
                {
                    var rowRect = new XRect(margin, yPosition, contentWidth, 22);
                    graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(248, 250, 252)), rowRect);
                }

                yPosition += 3;
                graphics.DrawString(fee.FeeName, regularFont, XBrushes.Black,
                    new XPoint(margin + 10, yPosition + 10));

                // Wrap long descriptions
                var descriptionRect = new XRect(margin + 220, yPosition + 4, 190, 20);
                graphics.DrawString(fee.Description, smallFont, new XSolidBrush(secondaryColor),
                    descriptionRect, XStringFormats.TopLeft);

                graphics.DrawString($"{fee.Amount:N2}", regularFont, XBrushes.Black,
                    new XPoint(margin + 450, yPosition + 10));

                yPosition += 19;
                graphics.DrawLine(new XPen(XColor.FromArgb(226, 232, 240)), margin, yPosition, margin + contentWidth, yPosition);

                isEven = !isEven;
            }

            // Total Section with colored background
            yPosition += 5;
            var totalRect = new XRect(margin, yPosition, contentWidth, 35);
            graphics.DrawRectangle(new XPen(accentColor), new XSolidBrush(XColor.FromArgb(240, 253, 244)), totalRect);

            yPosition += 5;
            graphics.DrawString("TOTAL AMOUNT", headerFont, new XSolidBrush(accentColor),
                new XPoint(margin + 220, yPosition + 18));
            graphics.DrawString($"ZMW {invoice.TotalAmount:N2}", headerFont, new XSolidBrush(accentColor),
                new XPoint(margin + 420, yPosition + 18));

            yPosition += 50;

            // Compact Important Notes Section
            var notesRect = new XRect(margin, yPosition, contentWidth, 80);
            graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), notesRect);

            yPosition += 8;
            graphics.DrawString("IMPORTANT NOTES", subHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 10, yPosition));

            yPosition += lineHeight;

            var notes = new[]
            {
        "• Fees estimate based on current academic year rates  • Subject to change per university policy",
        "• Additional fees may apply for specific courses  • Payment plans available - contact finance office",
        "• All amounts in Zambian Kwacha (ZMW)  • Invoice valid for current academic year"
    };

            foreach (var note in notes)
            {
                graphics.DrawString(note, smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin + 15, yPosition));
                yPosition += 12;
            }

            // Compact Footer
            yPosition = page.Height - 40;
            graphics.DrawLine(new XPen(borderGray), margin, yPosition, margin + contentWidth, yPosition);

            yPosition += 8;
            graphics.DrawString("Computer-generated invoice - no signature required",
                smallFont, new XSolidBrush(secondaryColor),
                new XPoint(margin, yPosition));

            graphics.DrawString($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}",
                smallFont, new XSolidBrush(secondaryColor),
                new XPoint(margin + 350, yPosition));

            using var stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }


        private byte[] GenerateApplicationInvoicePdf(SIS.Models.StudentApplication.Applicant application, List<ProgrammeFeeDetailViewModel> fees)
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            var graphics = XGraphics.FromPdfPage(page);

            // Fonts
            var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
            var subHeaderFont = new XFont("Arial", 11, XFontStyle.Bold);
            var regularFont = new XFont("Arial", 10);
            var smallFont = new XFont("Arial", 9);

            // Colors - matching programme invoice
            var primaryColor = XColor.FromArgb(8, 116, 144);
            var secondaryColor = XColor.FromArgb(71, 85, 105);
            var accentColor = XColor.FromArgb(16, 185, 129);
            var lightGray = XColor.FromArgb(248, 250, 252);
            var darkGray = XColor.FromArgb(51, 65, 85);
            var borderGray = XColor.FromArgb(226, 232, 240);

            const double margin = 40;
            const double contentWidth = 515;
            const double lineHeight = 18;
            double yPosition = margin;

            // Header with gradient
            var headerRect = new XRect(0, 0, page.Width, 80);
            var headerBrush = new XLinearGradientBrush(
                new XPoint(0, 0),
                new XPoint(page.Width, 80),
                primaryColor,
                XColor.FromArgb(59, 130, 246)
            );
            graphics.DrawRectangle(headerBrush, headerRect);

            // Logo placeholder
            graphics.DrawEllipse(new XPen(XColors.White, 2), XBrushes.White,
                new XRect(margin, 15, 50, 50));
            graphics.DrawString("UNIV", new XFont("Arial", 12, XFontStyle.Bold),
                new XSolidBrush(primaryColor), new XPoint(margin + 15, 45));

            // Header text
            graphics.DrawString("UNIVERSITY ADMISSIONS",
                new XFont("Arial", 14, XFontStyle.Bold), XBrushes.White,
                new XPoint(margin + 80, 30));
            graphics.DrawString("APPLICATION FEES INVOICE", titleFont, XBrushes.White,
                new XPoint(margin + 80, 55));

            yPosition = 100;

            // Application details in a clean card
            var cardRect = new XRect(margin, yPosition, contentWidth, 90);
            graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), cardRect);

            yPosition += 15;
            graphics.DrawString("Application Details", subHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 15, yPosition));

            yPosition += lineHeight + 5;
            graphics.DrawString($"Reference Number: {application.ReferenceNumber}", regularFont, XBrushes.Black,
                new XPoint(margin + 15, yPosition));

            yPosition += lineHeight;
            graphics.DrawString($"Applicant Name: {application.FullName}", regularFont, XBrushes.Black,
                new XPoint(margin + 15, yPosition));

            yPosition += lineHeight;
            graphics.DrawString($"Programme: {application.Programme?.Name}", regularFont, XBrushes.Black,
                new XPoint(margin + 15, yPosition));

            yPosition = 220;

            // Simple fee breakdown
            graphics.DrawString("APPLICATION FEES", headerFont, new XSolidBrush(primaryColor),
                new XPoint(margin, yPosition));

            yPosition += lineHeight + 10;

            // Fee items in a clean table
            var tableHeaderRect = new XRect(margin, yPosition, contentWidth, 30);
            graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(XColor.FromArgb(241, 245, 249)), tableHeaderRect);

            yPosition += 8;
            graphics.DrawString("Fee Type", subHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 15, yPosition + 15));
            graphics.DrawString("Amount (ZMW)", subHeaderFont, new XSolidBrush(darkGray),
                new XPoint(margin + 400, yPosition + 15));

            yPosition += 30;

            // Draw fees
            bool isEven = false;
            foreach (var fee in fees)
            {
                if (isEven)
                {
                    var rowRect = new XRect(margin, yPosition, contentWidth, 25);
                    graphics.DrawRectangle(new XSolidBrush(lightGray), rowRect);
                }

                yPosition += 5;
                graphics.DrawString(fee.FeeName, regularFont, XBrushes.Black,
                    new XPoint(margin + 15, yPosition + 12));
                graphics.DrawString($"{fee.Amount:N2}", regularFont, XBrushes.Black,
                    new XPoint(margin + 430, yPosition + 12));

                yPosition += 20;
                graphics.DrawLine(new XPen(borderGray), margin, yPosition, margin + contentWidth, yPosition);
                isEven = !isEven;
            }

            // Total amount
            yPosition += 10;
            var totalRect = new XRect(margin, yPosition, contentWidth, 40);
            graphics.DrawRectangle(new XPen(accentColor, 2), new XSolidBrush(XColor.FromArgb(240, 253, 244)), totalRect);

            yPosition += 10;
            graphics.DrawString("TOTAL AMOUNT", headerFont, new XSolidBrush(accentColor),
                new XPoint(margin + 250, yPosition + 18));
            graphics.DrawString($"ZMW {fees.Sum(f => f.Amount):N2}", headerFont, new XSolidBrush(accentColor),
                new XPoint(margin + 400, yPosition + 18));

            yPosition += 70;

            // Simple payment note
            var noteRect = new XRect(margin, yPosition, contentWidth, 40);
            graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), noteRect);

            yPosition += 10;
            graphics.DrawString("Payment required to proceed with application review", regularFont, new XSolidBrush(secondaryColor),
                new XPoint(margin + 15, yPosition + 15));

            // Footer
            yPosition = page.Height - 50;
            graphics.DrawLine(new XPen(borderGray), margin, yPosition, margin + contentWidth, yPosition);

            yPosition += 10;
            graphics.DrawString("This is a computer-generated invoice", smallFont, new XSolidBrush(secondaryColor),
                new XPoint(margin, yPosition));

            graphics.DrawString($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}",
                smallFont, new XSolidBrush(secondaryColor),
                new XPoint(margin + 350, yPosition));

            using var stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }


        private string GetYearDisplayName(int year)
        {
            return year switch
            {
                1 => "First Year",
                2 => "Second Year",
                3 => "Third Year",
                4 => "Fourth Year",
                5 => "Fifth Year",
                _ => $"Year {year}"
            };
        }





        // Add this method to your PdfInvoiceService class

        public async Task<byte[]> GenerateAdmissionLetterAsync(Student student)
        {
            try
            {
                var document = new PdfDocument();
                var page = document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                var graphics = XGraphics.FromPdfPage(page);

                // Set up fonts
                var titleFont = new XFont("Arial", 20, XFontStyle.Bold);
                var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
                var subHeaderFont = new XFont("Arial", 12, XFontStyle.Bold);
                var regularFont = new XFont("Arial", 10);
                var smallFont = new XFont("Arial", 9);
                var largeFont = new XFont("Arial", 16, XFontStyle.Bold);

                // Colors matching University theme
                var primaryColor = XColor.FromArgb(8, 116, 144);
                var secondaryColor = XColor.FromArgb(71, 85, 105);
                var accentColor = XColor.FromArgb(16, 185, 129);
                var lightGray = XColor.FromArgb(248, 250, 252);
                var darkGray = XColor.FromArgb(51, 65, 85);
                var borderGray = XColor.FromArgb(226, 232, 240);

                // Layout measurements
                const double margin = 40;
                const double contentWidth = 515;
                const double lineHeight = 16;
                double yPosition = margin;

                // Header with University Branding
                var headerRect = new XRect(0, 0, page.Width, 120);
                var headerBrush = new XLinearGradientBrush(
                    new XPoint(0, 0),
                    new XPoint(page.Width, 120),
                    primaryColor,
                    XColor.FromArgb(59, 130, 246)
                );
                graphics.DrawRectangle(headerBrush, headerRect);

                // University Logo (placeholder - you can replace with actual logo)
                graphics.DrawEllipse(new XPen(XColors.White, 3), XBrushes.White,
                    new XRect(margin, 20, 80, 80));
                var institution = _institutionConfig.GetCurrentInstitution();
                var logoText = institution.ShortName ?? "UNIV";
                graphics.DrawString(logoText.Substring(0, Math.Min(4, logoText.Length)).ToUpper(),
                    new XFont("Arial", 16, XFontStyle.Bold),
                    new XSolidBrush(primaryColor), new XPoint(margin + 20, 55));

                // University Header Text
                graphics.DrawString(institution.Name.ToUpper(), titleFont, XBrushes.White,
                    new XPoint(margin + 100, 45));
                graphics.DrawString("Certified by the Higher Education Authority",
                    new XFont("Arial", 11), XBrushes.White,
                    new XPoint(margin + 100, 70));

                yPosition = 140;

                // Date
                graphics.DrawString($"Date: {DateTime.Now:dd}{GetOrdinalSuffix(DateTime.Now.Day)} {DateTime.Now:MMMM yyyy}",
                    regularFont, XBrushes.Black, new XPoint(margin, yPosition));

                yPosition += 30;

                // Recipient
                graphics.DrawString($"Dear {student.FullName},", regularFont, XBrushes.Black,
                    new XPoint(margin, yPosition));

                yPosition += 25;

                // Subject line with styling
                var subjectRect = new XRect(margin, yPosition, contentWidth, 25);
                graphics.DrawRectangle(new XPen(primaryColor), new XSolidBrush(lightGray), subjectRect);

                yPosition += 5;
                graphics.DrawString($"RE: ADMISSION IN {student.Programme?.Name?.ToUpper() ?? "PROGRAMME"} ON FULL-TIME BASIS",
                    subHeaderFont, new XSolidBrush(primaryColor),
                    new XPoint(margin + 10, yPosition + 12));

                yPosition += 35;

                // Main content paragraph
                var mainText = $"I write to offer you admission at {institution.Name} for the academic year {student.AcademicYear?.YearValue} to study leading to the award of {student.Programme?.Name ?? "your chosen programme"}. The registration will run from 2nd June to 20th June {DateTime.Now.Year}. Classes will commence on 7th July {DateTime.Now.Year} and only registered students are eligible to attend. Late registration will attract a penalty fee.";

                // Wrap text properly
                var textRect = new XRect(margin, yPosition, contentWidth, 60);
                graphics.DrawString(mainText, regularFont, XBrushes.Black, textRect, XStringFormats.TopLeft);

                yPosition += 80;

                // "This offer is valid upon:" section
                graphics.DrawString("This offer is valid upon:", subHeaderFont, new XSolidBrush(darkGray),
                    new XPoint(margin, yPosition));

                yPosition += 20;

                // Conditions list with better formatting
                var conditions = new[]
                {
            "Production of your original certificates or statements of results in support of your qualifications as stated in your application.",
            "Production of your National Registration Card or other nationally recognizable document.",
            "Payment of at least 75% of the tuition and other fees during registration.",
            "Once fees are paid, refund of tuition fees will attract a 10% administrative charge and processing a refund will take at least 30 days before payment is ready.",
            "You are required to familiarize yourself with the student's general rules and regulations handbook from Dean of Students Affairs and adhere to them.",
            "The university does not offer any scholarships, kindly confirm all payment requests with the university using our official contact details."
        };

                for (int i = 0; i < conditions.Length; i++)
                {
                    var conditionRect = new XRect(margin + 20, yPosition, contentWidth - 20, 30);
                    graphics.DrawString($"{i + 1}. {conditions[i]}", regularFont, XBrushes.Black,
                        conditionRect, XStringFormats.TopLeft);

                    yPosition += (i == 3 || i == 4) ? 35 : 25; // More space for longer conditions
                }

                yPosition += 15;

                // Important notice
                var noticeRect = new XRect(margin, yPosition, contentWidth, 20);
                graphics.DrawRectangle(new XPen(XColor.FromArgb(239, 68, 68)), new XSolidBrush(XColor.FromArgb(254, 242, 242)), noticeRect);

                graphics.DrawString("This offer will lapse if not taken in the 2025 academic year.",
                    new XFont("Arial", 10, XFontStyle.Bold), new XSolidBrush(XColor.FromArgb(239, 68, 68)),
                    new XPoint(margin + 10, yPosition + 13));

                yPosition += 35;

                // Congratulations section
                var congratsRect = new XRect(margin, yPosition, contentWidth, 40);
                graphics.DrawRectangle(new XPen(accentColor), new XSolidBrush(XColor.FromArgb(240, 253, 244)), congratsRect);

                yPosition += 10;
                graphics.DrawString($"Congratulations on your admission at {institution.Name} and on behalf of the university;",
      regularFont, new XSolidBrush(accentColor), new XPoint(margin + 10, yPosition + 8));
                graphics.DrawString("wishing you a warm welcome and success in your studies here at your dream university.",
                    regularFont, new XSolidBrush(accentColor), new XPoint(margin + 10, yPosition + 23));

                yPosition += 60;

                // Signature section
                graphics.DrawString("Yours sincerely,", regularFont, XBrushes.Black,
                    new XPoint(margin, yPosition));

                yPosition += 40;

                // Signature placeholder
                graphics.DrawLine(new XPen(XColors.Black), margin, yPosition, margin + 200, yPosition);
                yPosition += 15;

                graphics.DrawString("Registrars Office", subHeaderFont, XBrushes.Black,
                    new XPoint(margin, yPosition));
                yPosition += 15;
                graphics.DrawString("REGISTRAR", regularFont, XBrushes.Black,
                    new XPoint(margin, yPosition));

                // Footer with contact information
                yPosition = page.Height - 80;
                var footerRect = new XRect(0, yPosition - 10, page.Width, 90);
                graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(249, 250, 251)), footerRect);

                yPosition += 5;
                graphics.DrawString(institution.Name.ToUpper(), subHeaderFont, new XSolidBrush(primaryColor),
                    new XPoint(margin, yPosition));

                yPosition += 18;
                graphics.DrawString(institution.ContactInfo?.Address ?? "Address", smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin, yPosition));

                yPosition += 12;
                graphics.DrawString($"Mobile No. {institution.ContactInfo?.Phone ?? "+260-XXX-XXXX"}", smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin, yPosition));

                yPosition += 12;
                graphics.DrawString($"E-mail: {institution.EmailSettings.SenderEmail}", smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin, yPosition));

                // Student ID watermark
                if (!string.IsNullOrEmpty(student.StudentId_Number))
                {
                    graphics.DrawString($"Student ID: {student.StudentId_Number}",
                        new XFont("Arial", 8, XFontStyle.Italic),
                        new XSolidBrush(XColor.FromArgb(100, secondaryColor.R, secondaryColor.G, secondaryColor.B)),
                        new XPoint(page.Width - 150, page.Height - 30));
                }

                using var stream = new MemoryStream();
                document.Save(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating admission letter for Student ID: {StudentId}", student.StudentId_Number);
                throw;
            }
        }

        // Helper method for ordinal suffixes
        private string GetOrdinalSuffix(int day)
        {
            if (day >= 11 && day <= 13)
                return "th";

            return (day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        }























        // Add this method to your existing PdfInvoiceService class

        public async Task<byte[]> GenerateStudentListPdfAsync(List<FilteredStudentViewModel> students, StudentListExportOptions exportOptions)
        {
            try
            {
                var document = new PdfDocument();

                // Set up fonts
                var titleFont = new XFont("Arial", 16, XFontStyle.Bold);
                var headerFont = new XFont("Arial", 12, XFontStyle.Bold);
                var subHeaderFont = new XFont("Arial", 10, XFontStyle.Bold);
                var regularFont = new XFont("Arial", 8);
                var smallFont = new XFont("Arial", 7);
                var tableHeaderFont = new XFont("Arial", 8, XFontStyle.Bold);

                // Colors (reusing existing theme)
                var primaryColor = XColor.FromArgb(8, 116, 144);
                var secondaryColor = XColor.FromArgb(71, 85, 105);
                var accentColor = XColor.FromArgb(16, 185, 129);
                var lightGray = XColor.FromArgb(248, 250, 252);
                var darkGray = XColor.FromArgb(51, 65, 85);
                var borderGray = XColor.FromArgb(226, 232, 240);

                // Layout measurements
                const double margin = 30;
                const double contentWidth = 535;
                const double headerHeight = 80;
                const double footerHeight = 40;
                const double rowHeight = 16;

                // Column configurations
                var columnConfigs = GetColumnConfigurations(exportOptions.SelectedColumns);
                var totalTableWidth = columnConfigs.Sum(c => c.Width);
                var scaleRatio = contentWidth / totalTableWidth;

                // Adjust column widths to fit page
                foreach (var config in columnConfigs)
                {
                    config.Width = (int)(config.Width * scaleRatio);
                }

                // Calculate rows per page
                var availableHeight = 841.89 - headerHeight - footerHeight - 120; // A4 height minus headers/footers
                var rowsPerPage = (int)(availableHeight / rowHeight) - 3; // Reserve space for table header

                var totalPages = (int)Math.Ceiling((double)students.Count / rowsPerPage);
                var currentRow = 0;

                for (int pageNum = 1; pageNum <= totalPages; pageNum++)
                {
                    var page = document.AddPage();
                    page.Size = PdfSharpCore.PageSize.A4;
                    var graphics = XGraphics.FromPdfPage(page);

                    double yPosition = margin;

                    // Header
                    DrawPageHeader(graphics, exportOptions, pageNum, totalPages, yPosition, contentWidth, titleFont, headerFont, regularFont, primaryColor, lightGray, borderGray);
                    yPosition += headerHeight + 20;

                    // Filter summary (only on first page)
                    if (pageNum == 1 && exportOptions.FilterSummary.Any())
                    {
                        yPosition = DrawFilterSummary(graphics, exportOptions.FilterSummary, yPosition, contentWidth, subHeaderFont, regularFont, lightGray, borderGray, darkGray);
                        yPosition += 15;
                    }

                    // Table header
                    yPosition = DrawTableHeader(graphics, columnConfigs, yPosition, tableHeaderFont, primaryColor, lightGray, borderGray);
                    yPosition += 20;

                    // Table rows for this page
                    var rowsOnThisPage = Math.Min(rowsPerPage, students.Count - currentRow);

                    for (int i = 0; i < rowsOnThisPage; i++)
                    {
                        var student = students[currentRow + i];
                        yPosition = DrawStudentRow(graphics, student, columnConfigs, yPosition, regularFont, i % 2 == 0 ? XColors.White : lightGray, borderGray);
                    }

                    currentRow += rowsOnThisPage;

                    // Footer
                    DrawPageFooter(graphics, exportOptions, pageNum, totalPages, page.Height - 30, contentWidth, smallFont, secondaryColor);
                }

                using var stream = new MemoryStream();
                document.Save(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating student list PDF");
                throw;
            }
        }

        private List<ExportColumnOption> GetColumnConfigurations(List<string> selectedColumns)
        {
            var allColumns = new Dictionary<string, ExportColumnOption>
            {
                ["StudentNumber"] = new ExportColumnOption { Key = "StudentNumber", DisplayName = "Student #", Width = 80 },
                ["FullName"] = new ExportColumnOption { Key = "FullName", DisplayName = "Full Name", Width = 120 },
                ["Email"] = new ExportColumnOption { Key = "Email", DisplayName = "Email", Width = 100 },
                ["Phone"] = new ExportColumnOption { Key = "Phone", DisplayName = "Phone", Width = 80 },
                ["ProgrammeName"] = new ExportColumnOption { Key = "ProgrammeName", DisplayName = "Programme", Width = 100 },
                ["SchoolName"] = new ExportColumnOption { Key = "SchoolName", DisplayName = "School", Width = 80 },
                ["DepartmentName"] = new ExportColumnOption { Key = "DepartmentName", DisplayName = "Department", Width = 90 },
                ["ModeOfStudyName"] = new ExportColumnOption { Key = "ModeOfStudyName", DisplayName = "Mode", Width = 60 },
                ["ProgrammeLevelName"] = new ExportColumnOption { Key = "ProgrammeLevelName", DisplayName = "Level", Width = 60 },
                ["AcademicYear"] = new ExportColumnOption { Key = "AcademicYear", DisplayName = "Academic Year", Width = 70 },
                ["CurrentYear"] = new ExportColumnOption { Key = "CurrentYear", DisplayName = "Year", Width = 40 },
                ["CurrentSemester"] = new ExportColumnOption { Key = "CurrentSemester", DisplayName = "Sem", Width = 35 },
                ["RegistrationStatus"] = new ExportColumnOption { Key = "RegistrationStatus", DisplayName = "Status", Width = 70 },
                ["OutstandingFees"] = new ExportColumnOption { Key = "OutstandingFees", DisplayName = "Outstanding", Width = 70 },
                ["RegistrationDate"] = new ExportColumnOption { Key = "RegistrationDate", DisplayName = "Reg. Date", Width = 70 },
                ["NrcOrPassportNumber"] = new ExportColumnOption { Key = "NrcOrPassportNumber", DisplayName = "NRC/Passport", Width = 85 },
                ["Gender"] = new ExportColumnOption { Key = "Gender", DisplayName = "Gender", Width = 50 },
                ["Nationality"] = new ExportColumnOption { Key = "Nationality", DisplayName = "Nationality", Width = 70 },
            };

            return selectedColumns.Where(col => allColumns.ContainsKey(col))
                                  .Select(col => allColumns[col])
                                  .ToList();
        }

        private void DrawPageHeader(XGraphics graphics, StudentListExportOptions options, int pageNum, int totalPages,
     double yPosition, double contentWidth, XFont titleFont, XFont headerFont, XFont regularFont,
     XColor primaryColor, XColor lightGray, XColor borderGray)
        {
            // Get institution configuration
            var institution = _institutionConfig.GetCurrentInstitution();

            // Header background
            var headerRect = new XRect(0, 0, graphics.PageSize.Width, 60);
            var headerBrush = new XLinearGradientBrush(
                new XPoint(0, 0),
                new XPoint(graphics.PageSize.Width, 60),
                primaryColor,
                XColor.FromArgb(59, 130, 246)
            );
            graphics.DrawRectangle(headerBrush, headerRect);

            // University info - using institution name and proper fonts
            graphics.DrawString(institution.Name.ToUpper(), titleFont, XBrushes.White, new XPoint(30, 35));
            graphics.DrawString("STUDENT INFORMATION SYSTEM", headerFont, XBrushes.White, new XPoint(30, 50));

            // Title and page info
            graphics.DrawString(options.Title, titleFont, new XSolidBrush(XColor.FromArgb(51, 65, 85)),
                new XPoint(30, yPosition + 20));

            graphics.DrawString($"Page {pageNum} of {totalPages}", regularFont, new XSolidBrush(XColor.FromArgb(71, 85, 105)),
                new XPoint(contentWidth - 50, yPosition + 20));

            graphics.DrawString($"Total Records: {options.TotalRecords}", regularFont, new XSolidBrush(XColor.FromArgb(71, 85, 105)),
                new XPoint(30, yPosition + 35));

            graphics.DrawString($"Generated: {options.GeneratedDate:dd/MM/yyyy HH:mm}", regularFont, new XSolidBrush(XColor.FromArgb(71, 85, 105)),
                new XPoint(contentWidth - 120, yPosition + 35));
        }

        private double DrawFilterSummary(XGraphics graphics, Dictionary<string, string> filterSummary, double yPosition,
            double contentWidth, XFont subHeaderFont, XFont regularFont, XColor lightGray, XColor borderGray, XColor darkGray)
        {
            if (!filterSummary.Any()) return yPosition;

            graphics.DrawString("Applied Filters:", subHeaderFont, new XSolidBrush(darkGray), new XPoint(30, yPosition));
            yPosition += 15;

            var filterHeight = Math.Max(20, filterSummary.Count * 12 + 10);
            var filterRect = new XRect(30, yPosition, contentWidth, filterHeight);
            graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), filterRect);

            yPosition += 8;
            foreach (var filter in filterSummary.Take(6)) // Limit to 6 filters to avoid overflow
            {
                graphics.DrawString($"• {filter.Key}: {filter.Value}", regularFont, new XSolidBrush(darkGray),
                    new XPoint(40, yPosition));
                yPosition += 12;
            }

            return yPosition + 5;
        }

        private double DrawTableHeader(XGraphics graphics, List<ExportColumnOption> columns, double yPosition,
            XFont tableHeaderFont, XColor primaryColor, XColor lightGray, XColor borderGray)
        {
            var currentX = 30.0;

            foreach (var column in columns)
            {
                var headerRect = new XRect(currentX, yPosition, column.Width, 18);
                graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(XColor.FromArgb(241, 245, 249)), headerRect);

                graphics.DrawString(column.DisplayName, tableHeaderFont, new XSolidBrush(primaryColor),
                    new XPoint(currentX + 3, yPosition + 12));

                currentX += column.Width;
            }

            return yPosition;
        }

        private double DrawStudentRow(XGraphics graphics, FilteredStudentViewModel student, List<ExportColumnOption> columns,
            double yPosition, XFont regularFont, XColor backgroundColor, XColor borderGray)
        {
            var currentX = 30.0;

            // Background
            if (backgroundColor != XColors.White)
            {
                var rowRect = new XRect(30, yPosition, columns.Sum(c => c.Width), 15);
                graphics.DrawRectangle(new XSolidBrush(backgroundColor), rowRect);
            }

            foreach (var column in columns)
            {
                var cellRect = new XRect(currentX, yPosition, column.Width, 15);
                graphics.DrawRectangle(new XPen(borderGray, 0.5), cellRect);

                var value = GetStudentColumnValue(student, column.Key);
                graphics.DrawString(value, regularFont, XBrushes.Black, new XPoint(currentX + 2, yPosition + 10));

                currentX += column.Width;
            }

            return yPosition + 15;
        }

        private string GetStudentColumnValue(FilteredStudentViewModel student, string columnKey)
        {
            return columnKey switch
            {
                "StudentNumber" => student.StudentNumber,
                "FullName" => TruncateText(student.FullName, 15),
                "Email" => TruncateText(student.Email, 18),
                "Phone" => student.Phone,
                "ProgrammeName" => TruncateText(student.ProgrammeName, 15),
                "SchoolName" => TruncateText(student.SchoolName, 12),
                "DepartmentName" => TruncateText(student.DepartmentName, 12),
                "ModeOfStudyName" => TruncateText(student.ModeOfStudyName, 8),
                "ProgrammeLevelName" => TruncateText(student.ProgrammeLevelName, 8),
                "AcademicYear" => student.AcademicYear,
                "CurrentYear" => student.CurrentYear.ToString(),
                "CurrentSemester" => student.CurrentPeriodLabel.ToString(),
                "RegistrationStatus" => TruncateText(student.RegistrationStatus, 10),
                "OutstandingFees" => $"K{student.OutstandingFees:N0}",
                "RegistrationDate" => student.RegistrationDate?.ToString("dd/MM/yy") ?? "N/A",
                "NrcOrPassportNumber" => TruncateText(student.NrcOrPassportNumber, 12),
                "Gender" => TruncateText(student.Gender, 8),
                "Nationality" => TruncateText(student.Nationality, 10),
                _ => ""
            };
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 2) + "..";
        }

        private void DrawPageFooter(XGraphics graphics, StudentListExportOptions options, int pageNum, int totalPages,
            double yPosition, double contentWidth, XFont smallFont, XColor secondaryColor)
        {

            var institution = _institutionConfig.GetCurrentInstitution();

            graphics.DrawLine(new XPen(XColor.FromArgb(226, 232, 240)), 30, yPosition, 30 + contentWidth, yPosition);

            graphics.DrawString($"Generated by: {options.GeneratedBy}", smallFont, new XSolidBrush(secondaryColor),
            new XPoint(30, yPosition + 15));

            graphics.DrawString($"{institution.Name.ToUpper()} - Student Information System", smallFont, new XSolidBrush(secondaryColor),
         new XPoint(200, yPosition + 15));

            graphics.DrawString($"{DateTime.Now:dd/MM/yyyy HH:mm}", smallFont, new XSolidBrush(secondaryColor),
                new XPoint(contentWidth - 50, yPosition + 15));
        }









        // Excel Methods 
        public async Task<byte[]> GenerateStudentListExcelAsync(List<FilteredStudentViewModel> students, StudentListExportOptions exportOptions)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Student List");

                // Column configurations
                var columnConfigs = GetExcelColumnConfigurations(exportOptions.SelectedColumns);

                var currentRow = 1;
                var currentCol = 1;

                // Title Section
                var institutionName = _institutionConfig.GetInstitutionName();
                worksheet.Cell(currentRow, 1).Value = institutionName.ToUpper();
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.FromArgb(8, 116, 144);
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "STUDENT INFORMATION SYSTEM";
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.FromArgb(71, 85, 105);
                currentRow++;

                currentRow++; // Empty row

                // Report Title
                worksheet.Cell(currentRow, 1).Value = exportOptions.Title;
                worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                currentRow++;

                // Report Info
                worksheet.Cell(currentRow, 1).Value = $"Generated: {exportOptions.GeneratedDate:dd/MM/yyyy HH:mm}";
                worksheet.Cell(currentRow, 3).Value = $"Generated By: {exportOptions.GeneratedBy}";
                worksheet.Cell(currentRow, 5).Value = $"Total Records: {exportOptions.TotalRecords}";
                currentRow++;

                currentRow++; // Empty row

                // Filter Summary
                if (exportOptions.FilterSummary.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "Applied Filters:";
                    worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                    worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.FromArgb(51, 65, 85);
                    currentRow++;

                    foreach (var filter in exportOptions.FilterSummary)
                    {
                        worksheet.Cell(currentRow, 1).Value = $"• {filter.Key}:";
                        worksheet.Cell(currentRow, 2).Value = filter.Value;
                        currentRow++;
                    }
                    currentRow++; // Empty row
                }

                // Table Headers
                var headerRow = currentRow;
                var dataStartRow = currentRow + 1; // Track where data starts
                currentCol = 1;

                foreach (var column in columnConfigs)
                {
                    var headerCell = worksheet.Cell(headerRow, currentCol);
                    headerCell.Value = column.DisplayName;
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
                    headerCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    headerCell.Style.Font.FontColor = XLColor.Black;
                    headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    currentCol++;
                }

                currentRow++;

                // Data Rows
                foreach (var student in students)
                {
                    currentCol = 1;

                    foreach (var column in columnConfigs)
                    {
                        var cellValue = GetStudentExcelColumnValue(student, column.Key);
                        var cell = worksheet.Cell(currentRow, currentCol);

                        // Set value based on type
                        if (column.Key == "OutstandingFees" && decimal.TryParse(cellValue.Replace("K", "").Replace(",", ""), out decimal feeValue))
                        {
                            cell.Value = feeValue;
                            cell.Style.NumberFormat.Format = "#,##0.00";
                        }
                        else if (column.Key == "RegistrationDate" && DateTime.TryParse(cellValue, out DateTime dateValue))
                        {
                            cell.Value = dateValue;
                            cell.Style.DateFormat.Format = "dd/MM/yyyy";
                        }
                        else if (column.Key == "CurrentYear" || column.Key == "CurrentSemester")
                        {
                            if (int.TryParse(cellValue, out int intValue))
                                cell.Value = intValue;
                            else
                                cell.Value = cellValue;
                        }
                        else
                        {
                            cell.Value = cellValue;
                        }

                        // Apply borders
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        cell.Style.Border.OutsideBorderColor = XLColor.LightGray;

                        // Alternate row coloring
                        if ((currentRow - dataStartRow) % 2 == 0)
                        {
                            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
                        }

                        currentCol++;
                    }
                    currentRow++;
                }

                var dataEndRow = currentRow - 1; // Track where data ends

                // Auto-fit columns
                worksheet.ColumnsUsed().AdjustToContents();

                // Set column width limits
                foreach (var column in worksheet.ColumnsUsed())
                {
                    if (column.Width < 10)
                        column.Width = 10;
                    else if (column.Width > 50)
                        column.Width = 50;
                }

                // Add freeze panes for header (only if we have data)
                if (students.Any())
                {
                    worksheet.SheetView.FreezeRows(headerRow);
                }

                // Create a table for the data (FIXED: Use correct range and avoid conflicts)
                if (students.Any() && columnConfigs.Any())
                {
                    try
                    {
                        var tableRange = worksheet.Range(headerRow, 1, dataEndRow, columnConfigs.Count);
                        var table = tableRange.CreateTable("StudentListTable");
                        table.Theme = XLTableTheme.TableStyleMedium2;

                        // Table automatically includes filtering, so we don't need SetAutoFilter
                    }
                    catch (Exception tableEx)
                    {
                        // If table creation fails, just apply auto filter instead
                        _logger.LogWarning(tableEx, "Could not create table, applying auto filter instead");
                        var filterRange = worksheet.Range(headerRow, 1, dataEndRow, columnConfigs.Count);
                        filterRange.SetAutoFilter();
                    }
                }

                // Add summary section (after the main data table)
                currentRow += 2;
                worksheet.Cell(currentRow, 1).Value = "Summary:";
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.FromArgb(8, 116, 144);
                currentRow++;

                var registeredCount = students.Count(s => s.IsRegistered);
                var unregisteredCount = students.Count - registeredCount;
                var outstandingFeesCount = students.Count(s => s.OutstandingFees > 0);

                worksheet.Cell(currentRow, 1).Value = "Registered Students:";
                worksheet.Cell(currentRow, 2).Value = registeredCount;
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Unregistered Students:";
                worksheet.Cell(currentRow, 2).Value = unregisteredCount;
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Students with Outstanding Fees:";
                worksheet.Cell(currentRow, 2).Value = outstandingFeesCount;
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                currentRow++;

                if (exportOptions.SelectedColumns.Contains("OutstandingFees"))
                {
                    var totalOutstanding = students.Sum(s => s.OutstandingFees);
                    worksheet.Cell(currentRow, 1).Value = "Total Outstanding Fees (ZMW):";
                    worksheet.Cell(currentRow, 2).Value = totalOutstanding;
                    worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                    worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating student list Excel with ClosedXML");
                throw;
            }
        }

        public async Task<byte[]> GenerateExamDocketPdfAsync(Student student, AcademicCalendarEvent examEvent, List<dynamic> courses)
        {
            try
            {
                var document = new PdfDocument();
                var page = document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                var graphics = XGraphics.FromPdfPage(page);

                // Fonts
                var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
                var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
                var subHeaderFont = new XFont("Arial", 11, XFontStyle.Bold);
                var regularFont = new XFont("Arial", 9);
                var smallFont = new XFont("Arial", 8);

                // Colors - Professional navy blue and slate gray theme
                var primaryColor = XColor.FromArgb(30, 64, 175); // Navy blue (#1e40af)
                var secondaryColor = XColor.FromArgb(100, 116, 139); // Slate gray (#64748b)
                var accentColor = XColor.FromArgb(5, 150, 105); // Emerald green (#059669)
                var lightGray = XColor.FromArgb(248, 250, 252);
                var darkGray = XColor.FromArgb(51, 65, 85);
                var borderGray = XColor.FromArgb(226, 232, 240);

                const double margin = 30;
                const double contentWidth = 535;
                double yPosition = margin;

                // Header with WHITE background and BLACK text (print-friendly)
                var headerRect = new XRect(0, 0, page.Width, 80);
                graphics.DrawRectangle(XBrushes.White, headerRect);

                // Add a simple border at the bottom of header
                graphics.DrawLine(new XPen(primaryColor, 2), 0, 80, page.Width, 80);

                // Institution logo
                var institution = _institutionConfig.GetCurrentInstitution();
                var logoPath = institution.LogoPath;

                // Convert relative path to absolute if needed
                if (!string.IsNullOrEmpty(logoPath) && logoPath.StartsWith("/"))
                {
                    logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", logoPath.TrimStart('/'));
                }

                // Draw logo
                var logoRect = new XRect(margin, 15, 50, 50);
                DrawImageSafely(graphics, logoPath, logoRect, "LOGO");

                // Institution name and title - NOW IN BLACK
                graphics.DrawString(institution.Name.ToUpper(), titleFont, XBrushes.Black,
                    new XPoint(margin + 70, 30));
                graphics.DrawString("EXAMINATION ADMISSION SLIP", headerFont, XBrushes.Black,
                    new XPoint(margin + 70, 50));

                var examType = examEvent.EventType?.Name?.Split(' ')[0]?.ToUpper() ?? "EXAM";
                graphics.DrawString($"{examType} EXAMINATIONS", subHeaderFont, XBrushes.Black,
                    new XPoint(margin + 70, 65));

                yPosition = 100;

                // Academic period info
                graphics.DrawString($"{examEvent.AcademicYear?.YearValue ?? DateTime.Now.Year.ToString()} - Semester {examEvent.Semester ?? 1}",
                    subHeaderFont, new XSolidBrush(darkGray), new XPoint(page.Width / 2, yPosition), XStringFormats.TopCenter);

                yPosition += 25;

                // Student section with photo
                var studentRect = new XRect(margin, yPosition, contentWidth, 90);
                graphics.DrawRectangle(new XPen(borderGray), XBrushes.White, studentRect);

                // Student photo
                var photoPath = student.PassportPhotoPath;
                if (!string.IsNullOrEmpty(photoPath) && photoPath.StartsWith("C:"))
                {
                    // Use the full path as provided
                }
                else if (!string.IsNullOrEmpty(photoPath) && photoPath.StartsWith("/"))
                {
                    // Convert relative path to absolute
                    photoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photoPath.TrimStart('/'));
                }

                var photoRect = new XRect(margin + 15, yPosition + 10, 60, 70);
                DrawImageSafely(graphics, photoPath, photoRect, "STUDENT\nPHOTO");

                // Student details
                var detailsX = margin + 90;
                var detailsY = yPosition + 15;

                graphics.DrawString("STUDENT DETAILS", subHeaderFont, new XSolidBrush(primaryColor),
                    new XPoint(detailsX, detailsY));
                detailsY += 20;

                var studentDetails = new[]
                {
            $"Name: {student.FullName ?? "N/A"}",
            $"Student ID: {student.StudentId_Number ?? "N/A"}",
            $"NRC/Passport: {student.NrcOrPassportNumber ?? "N/A"}",
            $"Programme: {student.Programme?.Name ?? "N/A"}",
            $"Year: {student.StudentCurrentYear ?? 1}"
        };

                foreach (var detail in studentDetails)
                {
                    graphics.DrawString(detail, regularFont, XBrushes.Black, new XPoint(detailsX, detailsY));
                    detailsY += 12;
                }

                yPosition += 110;

                // Exam details section
                var examRect = new XRect(margin, yPosition, contentWidth, 50);
                graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), examRect);

                yPosition += 10;
                graphics.DrawString("EXAMINATION DETAILS", subHeaderFont, new XSolidBrush(primaryColor),
                    new XPoint(margin + 10, yPosition));

                yPosition += 20;
                var examDate = DateTime.Parse(examEvent.StartDateTime.ToString());
                var examEndDate = examEvent.EndDateTime.HasValue ? DateTime.Parse(examEvent.EndDateTime.ToString()) : examDate;

                var examPeriod = examEndDate.Date != examDate.Date
                    ? $"{examDate:dd MMM yyyy} - {examEndDate:dd MMM yyyy}"
                    : examDate.ToString("dd MMM yyyy");

                graphics.DrawString($"Period: {examPeriod}", regularFont, XBrushes.Black,
                    new XPoint(margin + 10, yPosition));
                graphics.DrawString("Status: ELIGIBLE", regularFont, new XSolidBrush(accentColor),
                    new XPoint(margin + 250, yPosition));

                if (!string.IsNullOrEmpty(examEvent.Location))
                {
                    yPosition += 15;
                    graphics.DrawString($"Venue: {examEvent.Location}", regularFont, XBrushes.Black,
                        new XPoint(margin + 10, yPosition));
                }

                yPosition += 40;

                // Courses table
                graphics.DrawString("REGISTERED COURSES", headerFont, new XSolidBrush(primaryColor),
                    new XPoint(margin, yPosition));

                yPosition += 20;

                // Table header - Updated columns (white background with border for print)
                var tableHeaderRect = new XRect(margin, yPosition, contentWidth, 25);
                graphics.DrawRectangle(new XPen(primaryColor, 1), XBrushes.White, tableHeaderRect);

                graphics.DrawString("CODE", subHeaderFont, new XSolidBrush(primaryColor), new XPoint(margin + 10, yPosition + 15));
                graphics.DrawString("COURSE NAME", subHeaderFont, new XSolidBrush(primaryColor), new XPoint(margin + 100, yPosition + 15));
                graphics.DrawString("DATE / TIME", subHeaderFont, new XSolidBrush(primaryColor), new XPoint(margin + 350, yPosition + 15));
                graphics.DrawString("VENUE", subHeaderFont, new XSolidBrush(primaryColor), new XPoint(margin + 450, yPosition + 15));

                yPosition += 25;

                // Course rows - Limit courses to ensure footer fits
                int maxCourses = 10; // Reduced from 15 to ensure footer visibility
                if (courses?.Any() == true)
                {
                    bool isEven = false;
                    foreach (var course in courses.Take(maxCourses))
                    {
                        if (isEven)
                        {
                            var rowRect = new XRect(margin, yPosition, contentWidth, 20);
                            graphics.DrawRectangle(new XSolidBrush(lightGray), rowRect);
                        }

                        var courseCode = course.GetType().GetProperty("Code")?.GetValue(course)?.ToString() ?? "";
                        var courseName = course.GetType().GetProperty("Name")?.GetValue(course)?.ToString() ?? "";

                        graphics.DrawString(courseCode, regularFont, XBrushes.Black, new XPoint(margin + 10, yPosition + 12));

                        // Truncate long course names
                        var maxNameLength = 35;
                        var displayName = courseName.Length > maxNameLength
                            ? courseName.Substring(0, maxNameLength) + "..."
                            : courseName;
                        graphics.DrawString(displayName, regularFont, XBrushes.Black, new XPoint(margin + 100, yPosition + 12));

                        // Leave Date/Time column empty for manual filling
                        // Leave Venue column empty for manual filling

                        yPosition += 20;
                        isEven = !isEven;
                    }
                }
                else
                {
                    graphics.DrawString("No courses registered for this examination period",
                        regularFont, new XSolidBrush(secondaryColor),
                        new XPoint(page.Width / 2, yPosition + 20), XStringFormats.TopCenter);
                    yPosition += 40;
                }

                // Important notes
                yPosition += 20;
                var notesRect = new XRect(margin, yPosition, contentWidth, 60);
                graphics.DrawRectangle(new XPen(secondaryColor), new XSolidBrush(XColor.FromArgb(241, 245, 249)), notesRect);

                yPosition += 10;
                graphics.DrawString("IMPORTANT NOTES:", subHeaderFont, new XSolidBrush(primaryColor),
                    new XPoint(margin + 10, yPosition));

                yPosition += 15;
                var notes = new[]
                {
            "• Present this docket and valid student ID for examination entry",
            "• Arrive at least 30 minutes before examination time",
            "• Mobile phones and unauthorized materials are prohibited"
        };

                foreach (var note in notes)
                {
                    graphics.DrawString(note, smallFont, new XSolidBrush(darkGray),
                        new XPoint(margin + 15, yPosition));
                    yPosition += 12;
                }

                // Footer - FIXED POSITION from bottom
                // A4 page height is approximately 842 points, leaving 100 points for footer
                var footerStartY = page.Height - 80; // Fixed position 80 points from bottom
                graphics.DrawLine(new XPen(borderGray, 1), margin, footerStartY, margin + contentWidth, footerStartY);

                var footerContentY = footerStartY + 15;

                // Generate QR Code
                var qrCodeData = $"EXAM_DOCKET_{student.StudentId_Number}_{DateTime.Now:yyyyMMddHHmm}";
                var qrCodeImage = GenerateQRCode(qrCodeData);

                // Draw QR Code at bottom left
                var qrCodeRect = new XRect(margin, footerContentY, 40, 40);
                graphics.DrawImage(qrCodeImage, qrCodeRect);

                // Footer text (moved right to accommodate QR code)
                graphics.DrawString($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}", smallFont,
                    new XSolidBrush(secondaryColor), new XPoint(margin + 50, footerContentY + 5));

                var docketNumber = $"DOCKET-{student.StudentId_Number}-{DateTime.Now:yyyyMMddHHmm}";
                graphics.DrawString($"Docket No: {docketNumber}", smallFont,
                    new XSolidBrush(secondaryColor), new XPoint(margin + 50, footerContentY + 20));

                using var stream = new MemoryStream();
                document.Save(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating exam docket PDF for student: {StudentId}", student.StudentId_Number);
                throw;
            }
        }

        // Simple QR Code generation method
        private XImage GenerateQRCode(string data)
        {
            try
            {
                var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                var pngByteQRCode = new PngByteQRCode(qrCodeData);
                var qrCodeBytes = pngByteQRCode.GetGraphic(20);

                return XImage.FromStream(() => new MemoryStream(qrCodeBytes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code");

                // Simple fallback - create a basic placeholder
                var placeholder = new byte[] {
            137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 50, 0, 0, 0, 50, 8, 6, 0, 0, 0, 21, 175, 31, 169
        }; // Minimal PNG header for 50x50 transparent image

                return XImage.FromStream(() => new MemoryStream(placeholder));
            }
        }

        // Helper method - add this to your PdfInvoiceService class
        private void DrawImageSafely(XGraphics graphics, string imagePath, XRect rect, string fallbackText = "")
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                {
                    DrawProfessionalPlaceholder(graphics, rect, fallbackText);
                    return;
                }

                string fullPath = imagePath;
                if (imagePath.StartsWith("/"))
                {
                    fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/'));
                }

                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Image file not found: {ImagePath}", fullPath);
                    DrawProfessionalPlaceholder(graphics, rect, fallbackText);
                    return;
                }

                var extension = Path.GetExtension(fullPath).ToLowerInvariant();

                if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp")
                {
                    using var image = XImage.FromFile(fullPath);

                    // Calculate aspect ratio
                    var aspectRatio = (double)image.PixelWidth / image.PixelHeight;
                    var targetAspectRatio = rect.Width / rect.Height;

                    XRect drawRect = rect;
                    if (aspectRatio > targetAspectRatio)
                    {
                        var newHeight = rect.Width / aspectRatio;
                        drawRect = new XRect(rect.X, rect.Y + (rect.Height - newHeight) / 2, rect.Width, newHeight);
                    }
                    else
                    {
                        var newWidth = rect.Height * aspectRatio;
                        drawRect = new XRect(rect.X + (rect.Width - newWidth) / 2, rect.Y, newWidth, rect.Height);
                    }

                    graphics.DrawImage(image, drawRect);
                    return;
                }

                DrawProfessionalPlaceholder(graphics, rect, fallbackText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading image: {ImagePath}", imagePath);
                DrawProfessionalPlaceholder(graphics, rect, fallbackText);
            }
        }

        private void DrawProfessionalPlaceholder(XGraphics graphics, XRect rect, string placeholderType)
        {
            if (placeholderType.Contains("LOGO"))
            {
                // Draw institution logo placeholder
                var primaryColor = XColor.FromArgb(30, 64, 175);

                // Draw circular background
                graphics.DrawEllipse(new XPen(primaryColor, 2), new XSolidBrush(XColor.FromArgb(241, 245, 249)), rect);

                // Draw institution initials
                var institution = _institutionConfig.GetCurrentInstitution();
                var initials = GetInstitutionInitials(institution.Name);

                var font = new XFont("Arial", 12, XFontStyle.Bold);
                graphics.DrawString(initials, font, new XSolidBrush(primaryColor),
                    new XRect(rect.X, rect.Y, rect.Width, rect.Height), XStringFormats.Center);
            }
            else if (placeholderType.Contains("STUDENT"))
            {
                // Draw student photo placeholder
                var borderColor = XColor.FromArgb(203, 213, 225);
                var backgroundColor = XColor.FromArgb(248, 250, 252);

                // Draw rectangle with border
                graphics.DrawRectangle(new XPen(borderColor, 1), new XSolidBrush(backgroundColor), rect);

                // Draw person icon representation
                var centerX = rect.X + rect.Width / 2;
                var centerY = rect.Y + rect.Height / 2;

                // Head circle
                var headRadius = Math.Min(rect.Width, rect.Height) * 0.15;
                graphics.DrawEllipse(new XPen(borderColor, 1),
                    new XRect(centerX - headRadius, centerY - rect.Height * 0.2, headRadius * 2, headRadius * 2));

                // Body representation
                var bodyWidth = rect.Width * 0.4;
                var bodyHeight = rect.Height * 0.3;
                graphics.DrawRectangle(new XPen(borderColor, 1),
                    new XRect(centerX - bodyWidth / 2, centerY + headRadius, bodyWidth, bodyHeight));

                // Add "STUDENT PHOTO" text
                var font = new XFont("Arial", 7);
                graphics.DrawString("STUDENT", font, new XSolidBrush(XColor.FromArgb(156, 163, 175)),
                    new XPoint(centerX, rect.Y + rect.Height - 20), XStringFormats.TopCenter);
                graphics.DrawString("PHOTO", font, new XSolidBrush(XColor.FromArgb(156, 163, 175)),
                    new XPoint(centerX, rect.Y + rect.Height - 10), XStringFormats.TopCenter);
            }
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
            foreach (var word in words.Take(3)) // Max 3 initials
            {
                if (!string.IsNullOrEmpty(word))
                {
                    initials += word[0];
                }
            }

            return initials.ToUpper();
        }

        private void DrawPlaceholder(XGraphics graphics, XRect rect, string fallbackText)
        {
            // Draw placeholder border and background
            graphics.DrawRectangle(new XPen(XColor.FromArgb(203, 213, 225)),
                new XSolidBrush(XColor.FromArgb(248, 250, 252)), rect);

            if (!string.IsNullOrEmpty(fallbackText))
            {
                var font = new XFont("Arial", 8);
                var lines = fallbackText.Split('\n');
                var lineHeight = 12;
                var totalTextHeight = lines.Length * lineHeight;
                var startY = rect.Y + (rect.Height - totalTextHeight) / 2;

                for (int i = 0; i < lines.Length; i++)
                {
                    var textRect = new XRect(rect.X, startY + (i * lineHeight), rect.Width, lineHeight);
                    graphics.DrawString(lines[i], font,
                        new XSolidBrush(XColor.FromArgb(156, 163, 175)),
                        textRect, XStringFormats.Center);
                }
            }
        }

        private List<ExportColumnOption> GetExcelColumnConfigurations(List<string> selectedColumns)
        {
            var allColumns = new Dictionary<string, ExportColumnOption>
            {
                ["StudentNumber"] = new ExportColumnOption { Key = "StudentNumber", DisplayName = "Student Number" },
                ["FullName"] = new ExportColumnOption { Key = "FullName", DisplayName = "Full Name" },
                ["Email"] = new ExportColumnOption { Key = "Email", DisplayName = "Email Address" },
                ["Phone"] = new ExportColumnOption { Key = "Phone", DisplayName = "Phone Number" },
                ["ProgrammeName"] = new ExportColumnOption { Key = "ProgrammeName", DisplayName = "Programme" },
                ["SchoolName"] = new ExportColumnOption { Key = "SchoolName", DisplayName = "School" },
                ["DepartmentName"] = new ExportColumnOption { Key = "DepartmentName", DisplayName = "Department" },
                ["ModeOfStudyName"] = new ExportColumnOption { Key = "ModeOfStudyName", DisplayName = "Mode of Study" },
                ["ProgrammeLevelName"] = new ExportColumnOption { Key = "ProgrammeLevelName", DisplayName = "Programme Level" },
                ["AcademicYear"] = new ExportColumnOption { Key = "AcademicYear", DisplayName = "Academic Year" },
                ["CurrentYear"] = new ExportColumnOption { Key = "CurrentYear", DisplayName = "Year of Study" },
                ["CurrentSemester"] = new ExportColumnOption { Key = "CurrentSemester", DisplayName = "Semester" },
                ["RegistrationStatus"] = new ExportColumnOption { Key = "RegistrationStatus", DisplayName = "Registration Status" },
                ["OutstandingFees"] = new ExportColumnOption { Key = "OutstandingFees", DisplayName = "Outstanding Fees (ZMW)" },
                ["RegistrationDate"] = new ExportColumnOption { Key = "RegistrationDate", DisplayName = "Registration Date" },
                ["NrcOrPassportNumber"] = new ExportColumnOption { Key = "NrcOrPassportNumber", DisplayName = "NRC/Passport Number" },
                ["Gender"] = new ExportColumnOption { Key = "Gender", DisplayName = "Gender" },
                ["Nationality"] = new ExportColumnOption { Key = "Nationality", DisplayName = "Nationality" },
            };

            return selectedColumns.Where(col => allColumns.ContainsKey(col))
                                  .Select(col => allColumns[col])
                                  .ToList();
        }

        private string GetStudentExcelColumnValue(FilteredStudentViewModel student, string columnKey)
        {
            return columnKey switch
            {
                "StudentNumber" => student.StudentNumber,
                "FullName" => student.FullName,
                "Email" => student.Email,
                "Phone" => student.Phone,
                "ProgrammeName" => student.ProgrammeName,
                "SchoolName" => student.SchoolName,
                "DepartmentName" => student.DepartmentName,
                "ModeOfStudyName" => student.ModeOfStudyName,
                "ProgrammeLevelName" => student.ProgrammeLevelName,
                "AcademicYear" => student.AcademicYear,
                "CurrentYear" => student.CurrentYear.ToString(),
                "CurrentSemester" => student.CurrentPeriodLabel.ToString(),
                "RegistrationStatus" => student.RegistrationStatus,
                "OutstandingFees" => student.OutstandingFees.ToString("N2"),
                "RegistrationDate" => student.RegistrationDate?.ToString("dd/MM/yyyy") ?? "N/A",
                "NrcOrPassportNumber" => student.NrcOrPassportNumber,
                "Gender" => student.Gender,
                "Nationality" => student.Nationality,
                _ => ""
            };
        }
    }
}