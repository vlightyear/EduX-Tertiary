using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Registration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SIS.Services.PDF
{
    public class CalendarPdfService : ICalendarPdfService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CalendarPdfService> _logger;

        public CalendarPdfService(ApplicationDbContext context, ILogger<CalendarPdfService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<byte[]> GenerateCalendarPdfAsync(int academicYearId)
        {
            try
            {
                // Get the academic year (removed ModeOfStudy include since it no longer exists)
                var academicYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(a => a.YearId == academicYearId);

                if (academicYear == null)
                    throw new ArgumentException($"Academic year with ID {academicYearId} not found");

                // Get all events for this academic year
                var events = await _context.AcademicCalendarEvents
                    .Include(e => e.EventType)
                    .Include(e => e.School)
                    .Include(e => e.Programme)
                    .Include(e => e.ProgrammeLevel)
                    .Include(e => e.ModeOfStudy)
                    .Where(e => e.AcademicYearId == academicYearId && e.IsPublished)
                    .OrderBy(e => e.StartDateTime)
                    .ToListAsync();

                // Generate PDF document
                return GenerateCalendarPdf(academicYear, events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating academic calendar PDF for Year ID: {AcademicYearId}", academicYearId);
                throw;
            }
        }

        private byte[] GenerateCalendarPdf(AcademicYear academicYear, List<AcademicCalendarEvent> events)
        {
            // Create new PDF document
            var document = new PdfDocument();

            // Set up fonts
            var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
            var subHeaderFont = new XFont("Arial", 12, XFontStyle.Bold);
            var regularFont = new XFont("Arial", 10);
            var smallFont = new XFont("Arial", 8);
            var boldFont = new XFont("Arial", 10, XFontStyle.Bold);

            // Colors
            var primaryColor = XColor.FromArgb(8, 116, 144);   // Cyan-like primary color
            var secondaryColor = XColor.FromArgb(71, 85, 105); // Slate secondary color
            var lightGray = XColor.FromArgb(248, 250, 252);
            var borderGray = XColor.FromArgb(226, 232, 240);

            // Layout measurements
            const double margin = 40;
            const double contentWidth = 515;

            // Add first page
            var firstPage = document.AddPage();
            firstPage.Size = PdfSharpCore.PageSize.A4;
            var graphics = XGraphics.FromPdfPage(firstPage);

            double yPosition = margin;

            // Header with gradient
            var headerRect = new XRect(0, 0, firstPage.Width, 80);
            var headerBrush = new XLinearGradientBrush(
                new XPoint(0, 0),
                new XPoint(firstPage.Width, 80),
                primaryColor,
                XColor.FromArgb(59, 130, 246)  // Lighter blue for gradient
            );
            graphics.DrawRectangle(headerBrush, headerRect);

            // Logo placeholder
            graphics.DrawEllipse(new XPen(XColors.White, 2), XBrushes.White,
                new XRect(margin, 15, 50, 50));
            graphics.DrawString("UNIV", new XFont("Arial", 12, XFontStyle.Bold),
                new XSolidBrush(primaryColor), new XPoint(margin + 15, 45));

            // Header text
            graphics.DrawString("ACADEMIC CALENDAR",
                titleFont, XBrushes.White,
                new XPoint(margin + 80, 45));
            graphics.DrawString(academicYear.YearValue,
                headerFont, XBrushes.White,
                new XPoint(margin + 80, 70));

            yPosition = 100;

            // Academic Year details
            var detailsRect = new XRect(margin, yPosition, contentWidth, 120); // Increased height for new fields
            graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), detailsRect);

            yPosition += 15;
            graphics.DrawString("ACADEMIC YEAR DETAILS", subHeaderFont, new XSolidBrush(secondaryColor),
                new XPoint(margin + 15, yPosition));

            yPosition += 20;
            graphics.DrawString("Year:", boldFont, XBrushes.Black,
                new XPoint(margin + 15, yPosition));
            graphics.DrawString(academicYear.YearValue, regularFont, XBrushes.Black,
                new XPoint(margin + 100, yPosition));

            // Display Academic Type instead of Mode of Study
            graphics.DrawString("Type:", boldFont, XBrushes.Black,
                new XPoint(margin + 250, yPosition));
            graphics.DrawString(academicYear.AcademicType.ToString(), regularFont, XBrushes.Black,
                new XPoint(margin + 350, yPosition));

            yPosition += 20;
            graphics.DrawString("Period:", boldFont, XBrushes.Black,
                new XPoint(margin + 15, yPosition));
            graphics.DrawString($"{academicYear.StartDate:MMMM d, yyyy} - {academicYear.EndDate:MMMM d, yyyy}",
                regularFont, XBrushes.Black,
                new XPoint(margin + 100, yPosition));

            yPosition += 20;

            // Add semester information if it's a semester-based academic year
            if (academicYear.AcademicType == AcademicType.Semester)
            {
                if (academicYear.Semester1StartDate.HasValue && academicYear.Semester1EndDate.HasValue)
                {
                    graphics.DrawString("Semester 1:", boldFont, XBrushes.Black,
                        new XPoint(margin + 15, yPosition));
                    graphics.DrawString($"{academicYear.Semester1StartDate.Value:MMMM d, yyyy} - {academicYear.Semester1EndDate.Value:MMMM d, yyyy}",
                        regularFont, XBrushes.Black,
                        new XPoint(margin + 100, yPosition));
                    yPosition += 20;
                }

                if (academicYear.Semester2StartDate.HasValue && academicYear.Semester2EndDate.HasValue)
                {
                    graphics.DrawString("Semester 2:", boldFont, XBrushes.Black,
                        new XPoint(margin + 15, yPosition));
                    graphics.DrawString($"{academicYear.Semester2StartDate.Value:MMMM d, yyyy} - {academicYear.Semester2EndDate.Value:MMMM d, yyyy}",
                        regularFont, XBrushes.Black,
                        new XPoint(margin + 100, yPosition));
                    yPosition += 20;
                }
            }

            // Add registration period if available
            if (academicYear.RegistrationStartDate.HasValue && academicYear.RegistrationEndDate.HasValue)
            {
                graphics.DrawString("Registration:", boldFont, XBrushes.Black,
                    new XPoint(margin + 15, yPosition));
                graphics.DrawString($"{academicYear.RegistrationStartDate.Value:MMMM d, yyyy} - {academicYear.RegistrationEndDate.Value:MMMM d, yyyy}",
                    regularFont, XBrushes.Black,
                    new XPoint(margin + 100, yPosition));
                yPosition += 20;
            }

            // Add exam period if available
            if (academicYear.FinalExamStartDate.HasValue && academicYear.FinalExamEndDate.HasValue)
            {
                graphics.DrawString("Final Exams:", boldFont, XBrushes.Black,
                    new XPoint(margin + 15, yPosition));
                graphics.DrawString($"{academicYear.FinalExamStartDate.Value:MMMM d, yyyy} - {academicYear.FinalExamEndDate.Value:MMMM d, yyyy}",
                    regularFont, XBrushes.Black,
                    new XPoint(margin + 100, yPosition));
            }

            yPosition = 240; // Adjusted for the larger details section

            // Event Type Legend
            graphics.DrawString("EVENT TYPES", subHeaderFont, new XSolidBrush(primaryColor),
                new XPoint(margin, yPosition));

            yPosition += 20;

            // Group events by type
            var eventTypes = events
                .GroupBy(e => e.EventType?.Name)
                .Select(g => new {
                    TypeName = g.Key ?? "Other",
                    Color = g.First().Color,
                    Icon = g.First().EventType?.IconName ?? "event",
                    Count = g.Count()
                })
                .OrderBy(t => t.TypeName)
                .ToList();

            // Create a grid of event types (3 columns)
            const int columns = 3;
            const double columnWidth = contentWidth / columns;
            const double iconSize = 15;
            const double legendItemHeight = 20;

            for (int i = 0; i < eventTypes.Count; i++)
            {
                var eventType = eventTypes[i];
                int column = i % columns;
                int row = i / columns;

                double x = margin + (column * columnWidth);
                double y = yPosition + (row * legendItemHeight);

                // Draw a color sample
                graphics.DrawRectangle(
                    new XSolidBrush(HexToXColor(eventType.Color)),
                    new XRect(x, y, iconSize, iconSize));

                // Draw the event type name
                graphics.DrawString($"{eventType.TypeName} ({eventType.Count})", regularFont, XBrushes.Black,
                    new XPoint(x + iconSize + 5, y + 12));
            }

            // Calculate how much vertical space the legend takes
            int legendRows = (eventTypes.Count + columns - 1) / columns; // ceiling division
            yPosition += (legendRows * legendItemHeight) + 30;

            // Calendar Overview
            graphics.DrawString("CALENDAR OVERVIEW", subHeaderFont, new XSolidBrush(primaryColor),
                new XPoint(margin, yPosition));

            yPosition += 20;

            // Group events by month
            var eventsByMonth = events
                .GroupBy(e => new { e.StartDateTime.Year, e.StartDateTime.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .ToList();

            // Display month overview
            foreach (var monthGroup in eventsByMonth)
            {
                var month = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1);
                var monthName = month.ToString("MMMM yyyy");
                var eventCount = monthGroup.Count();

                // Check if we need a new page
                if (yPosition > firstPage.Height - 100)
                {
                    // Add a new page
                    var newPage = document.AddPage();
                    newPage.Size = PdfSharpCore.PageSize.A4;
                    graphics = XGraphics.FromPdfPage(newPage);
                    yPosition = margin;

                    // Add a simple header
                    graphics.DrawLine(new XPen(primaryColor, 2), margin, yPosition, margin + contentWidth, yPosition);
                    yPosition += 10;
                    graphics.DrawString("ACADEMIC CALENDAR - CONTINUED", headerFont, new XSolidBrush(primaryColor),
                        new XPoint(margin, yPosition));
                    yPosition += 30;
                }

                // Month header
                var monthRect = new XRect(margin, yPosition, contentWidth, 30);
                graphics.DrawRectangle(new XPen(borderGray), new XSolidBrush(lightGray), monthRect);

                graphics.DrawString(monthName, boldFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin + 15, yPosition + 20));
                graphics.DrawString($"{eventCount} events", regularFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin + 200, yPosition + 20));

                yPosition += 40;

                // Events in this month
                foreach (var calendarEvent in monthGroup.OrderBy(e => e.StartDateTime))
                {
                    // Check if we need a new page
                    if (yPosition > firstPage.Height - 60)
                    {
                        // Add a new page
                        var newPage = document.AddPage();
                        newPage.Size = PdfSharpCore.PageSize.A4;
                        graphics = XGraphics.FromPdfPage(newPage);
                        yPosition = margin;

                        // Add a simple header
                        graphics.DrawLine(new XPen(primaryColor, 2), margin, yPosition, margin + contentWidth, yPosition);
                        yPosition += 10;
                        graphics.DrawString("ACADEMIC CALENDAR - CONTINUED", headerFont, new XSolidBrush(primaryColor),
                            new XPoint(margin, yPosition));
                        yPosition += 30;
                    }

                    // Event date
                    string dateStr;
                    if (calendarEvent.IsAllDay)
                    {
                        if (calendarEvent.EndDateTime.HasValue && calendarEvent.EndDateTime.Value.Date > calendarEvent.StartDateTime.Date)
                        {
                            dateStr = $"{calendarEvent.StartDateTime:MMM d} - {calendarEvent.EndDateTime.Value:MMM d}";
                        }
                        else
                        {
                            dateStr = $"{calendarEvent.StartDateTime:MMM d}";
                        }

                        dateStr += " (All day)";
                    }
                    else
                    {
                        dateStr = $"{calendarEvent.StartDateTime:MMM d, HH:mm}";
                        if (calendarEvent.EndDateTime.HasValue)
                        {
                            if (calendarEvent.EndDateTime.Value.Date == calendarEvent.StartDateTime.Date)
                            {
                                dateStr += $" - {calendarEvent.EndDateTime.Value:HH:mm}";
                            }
                            else
                            {
                                dateStr += $" - {calendarEvent.EndDateTime.Value:MMM d, HH:mm}";
                            }
                        }
                    }

                    // Event background
                    var eventRect = new XRect(margin, yPosition, contentWidth, 40);
                    var eventColor = HexToXColor(calendarEvent.Color);
                    graphics.DrawRectangle(new XPen(eventColor, 1),
                        new XSolidBrush(XColor.FromArgb(20, eventColor.R, eventColor.G, eventColor.B)),
                        eventRect);

                    // Event icon (placeholder)
                    var iconRect = new XRect(margin + 5, yPosition + 10, 20, 20);
                    graphics.DrawRectangle(new XSolidBrush(HexToXColor(calendarEvent.Color)), iconRect);

                    // Event title and date
                    graphics.DrawString(calendarEvent.Title, boldFont, XBrushes.Black,
                        new XPoint(margin + 35, yPosition + 15));
                    graphics.DrawString(dateStr, regularFont, XBrushes.Black,
                        new XPoint(margin + 35, yPosition + 32));

                    // Event type
                    graphics.DrawString(calendarEvent.EventType?.Name ?? "Event", regularFont, new XSolidBrush(secondaryColor),
                        new XPoint(margin + 350, yPosition + 15));

                    yPosition += 50;
                }
            }

            // Add footer to every page
            for (int i = 0; i < document.PageCount; i++)
            {
                var page = document.Pages[i];
                var pageGraphics = XGraphics.FromPdfPage(page);

                var footerY = page.Height - 30;
                pageGraphics.DrawLine(new XPen(borderGray), margin, footerY, margin + contentWidth, footerY);

                footerY += 15;
                pageGraphics.DrawString("Generated on " + DateTime.Now.ToString("MMMM d, yyyy"), smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin, footerY));
                pageGraphics.DrawString("Page " + (i + 1) + " of " + document.PageCount, smallFont, new XSolidBrush(secondaryColor),
                    new XPoint(margin + contentWidth - 80, footerY));
            }

            // Save to memory stream and return bytes
            using var stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }

        // Helper method to convert hex color string to XColor
        private XColor HexToXColor(string hexColor)
        {
            try
            {
                // Remove # if present
                if (hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }

                // Make sure the string is 6 characters
                if (hexColor.Length != 6)
                {
                    return XColor.FromArgb(128, 128, 128); // Default gray
                }

                // Parse the hex values
                int r = int.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                int g = int.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                int b = int.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

                // Create and return XColor
                return XColor.FromArgb(r, g, b);
            }
            catch
            {
                // Return a default color if parsing fails
                return XColor.FromArgb(128, 128, 128); // Gray as fallback
            }
        }
    }

    public interface ICalendarPdfService
    {
        Task<byte[]> GenerateCalendarPdfAsync(int academicYearId);
    }
}