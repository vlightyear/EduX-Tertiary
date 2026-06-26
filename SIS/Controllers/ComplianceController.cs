using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Compliance;
using SIS.Models.Payments;
using SIS.Models.Registration;
using SIS.Models.Reports;
using SIS.Models.StudentApplication;
using SIS.Models.ViewModels;
using SIS.Services.PDF;
using SIS.Services.Reports;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Barcode;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using System.Linq;

namespace SIS.Controllers
{
    public class ComplianceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StudentLookupController> _logger;
        private readonly IPdfInvoiceService _pdfService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ISenateReportService _senateReportService;

        public ComplianceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<StudentLookupController> logger,
            IPdfInvoiceService pdfService,
            IWebHostEnvironment webHostEnvironment,
            ISenateReportService senateReportService)


        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _pdfService = pdfService;
            _webHostEnvironment = webHostEnvironment;
            _senateReportService = senateReportService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Get user's jurisdiction info for display
                var jurisdictionInfo = await GetUserJurisdictionInfo(user, primaryRole);

                var viewModel = new DocketLookupIndexViewModel
                {
                    UserRole = primaryRole,
                    UserName = user.FullName,
                    JurisdictionInfo = jurisdictionInfo,
                    SearchTypes = new List<string> { "StudentNumber", "Name", "NrcPassport", "Email" }
                };

                ViewData["programmes"] = await _context.Programmes.OrderBy(p => p.Name).ToListAsync();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student lookup dashboard");
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DocketLookup(string currentYearPeriod, string year, string programme, string docket, string? studentNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentYearPeriod) || string.IsNullOrWhiteSpace(year) || string.IsNullOrWhiteSpace(programme))
                {
                    return Json(new { success = false, message = "Search terms are required" });
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault();

                // Get students based on user's jurisdiction
                var studentsQuery = await BuildStudentsQuery(user, primaryRole);
                decimal threshold = 0;
                string DocketType = string.Empty;

                Student student = null;
                if (string.IsNullOrEmpty(studentNumber))
                {
                    student = await _context.Students.Where(s => s.CurrentYearPeriodId == Int32.Parse(currentYearPeriod)
                                                            && s.StudentCurrentYear == Int32.Parse(year)
                                                            && s.ProgrammeId == Int32.Parse(programme))
                                                            .FirstOrDefaultAsync();
                }
                else
                {
                    student = await _context.Students.Where(s => s.StudentId_Number == studentNumber)
                                                            .FirstOrDefaultAsync();
                }


                AcademicYear academicYear = null;
                if (student != null)
                {
                    academicYear = await _context.AcademicYears.Where(ay => ay.YearId == student.AcademicYearId).FirstOrDefaultAsync();
                }

                switch (docket)
                {
                    case "1":
                        threshold = 50;
                        DocketType = "CA-1";
                        break;
                    case "2":
                        threshold = academicYear?.MinRegistrationPaymentPercentage ?? 75;
                        DocketType = "CA-2";
                        break;
                    case "3":
                        threshold = academicYear?.MinExamPaymentPercentage ?? 100;
                        DocketType = "EXAMINATION";
                        break;
                    case "4":
                        threshold = academicYear?.MinExamPaymentPercentage ?? 100;
                        DocketType = "SUP/DEFFERED EXAMINATION";
                        break;
                }

                // Apply search filter
                List<StudentProgressionData> stds = new();
                if(docket == "4")
                {
                    SenateReportFilters filters = new();
                    filters.ReportLevel = "Programme";
                    filters.AcademicYearId = academicYear.YearId;
                    filters.AcademicPeriod = student.CurrentYearPeriod.AcademicPeriod.Id;
                    filters.YearOfStudy = student.StudentCurrentYear;
                    var stdsTmp = await _senateReportService.GetEntityStudentDetailsAsync(
                        student.ProgrammeId,
                        filters.ReportLevel,
                        filters,
                        studentNumber
                    );
                    stds = stdsTmp.Where(s => s.ProgressionStatus == "DEF" || s.ProgressionStatus == "Sup").ToList();

                    if(stds != null && stds.Any() && docket == "4")
                    {
                        var invoice = await _context.StudentInvoices.AsNoTracking().FirstOrDefaultAsync(si => (si.TransactionType == "DEF" || si.TransactionType == "Sup") && si.StudentId == stds.FirstOrDefault().StudentId);

                        if (invoice == null && stds != null && stds.Any())
                        {
                            StudentInvoice studentInvoice = new StudentInvoice();
                            studentInvoice.Status = Enums.Status.Active;
                            studentInvoice.StudentId = stds.FirstOrDefault().StudentId;
                            studentInvoice.CreatedAt = DateTime.Now;
                            studentInvoice.CreatedDate = DateTime.Now;
                            studentInvoice.AcademicYearId = academicYear.YearId;
                            studentInvoice.InvoiceReference = $"INV-{stds.FirstOrDefault().ProgressionStatus}-2026-1-{stds.FirstOrDefault().StudentNumber}";
                            studentInvoice.TransactionType = stds.FirstOrDefault().ProgressionStatus;

                            if (stds.FirstOrDefault().ProgressionStatus == "DEF")
                            {
                                studentInvoice.TotalAmount = 500;
                            }
                            else
                            {
                                studentInvoice.TotalAmount = 250;
                            }
                            _context.Add(studentInvoice);
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                if (string.IsNullOrEmpty(studentNumber))
                {
                    studentsQuery = studentsQuery.Where(s => s.CurrentYearPeriodId == Int32.Parse(currentYearPeriod) && s.StudentCurrentYear == Int32.Parse(year)
                                                    && s.ProgrammeId == Int32.Parse(programme) && s.PercentPaid >= threshold
                                                    && s.RegistrationStatus == true && s.PermitValid == 1);
                }
                else if (!string.IsNullOrEmpty(studentNumber))
                {
                    studentsQuery = studentsQuery.Where(s => s.StudentIdNumber == studentNumber && s.PercentPaid >= threshold
                                                    && s.RegistrationStatus == true && s.PermitValid == 1);
                }

                List<StudentData> students = new();

                if (stds != null && stds.Any() && docket == "4")
                {
                    var studentNumbers = stds.Select(spd => spd.StudentNumber).ToList(); // Bring StudentNumbers into memory
                    students = await studentsQuery
                        .Where(s => studentNumbers.Contains(s.StudentIdNumber))
                        .ToListAsync();

                    // Map courses from StudentProgressionData to each student
                    foreach (var stud in students)
                    {
                        var studentProgression = stds.FirstOrDefault(spd => spd.StudentNumber == stud.StudentIdNumber);

                        if (studentProgression?.StudentProgression?.Courses != null && studentProgression.StudentProgression.Courses.Any(c => !c.IsPassed))
                        {
                            stud.CoursesRegistered = string.Join(
                                Environment.NewLine,
                                studentProgression.StudentProgression.Courses
                                    .Where(c => !c.IsPassed)
                                    .Select(fc => $"{fc.CourseCode} - {fc.CourseName}")
                            );
                        }
                        else
                        {
                            stud.CoursesRegistered = "No failed courses";
                        }
                    }
                }
                else
                {
                    if(docket == "4")
                    {
                        students = new();
                    }
                    else
                    {
                        students = await studentsQuery
                            .ToListAsync();
                    }
                }

                string downloadUrl = string.Empty;
                if(students != null && students.Any())
                {
                    await PopulateCurrentPeriodLabels(students);
                    byte[] pdfBytes = GeneratePdf(students, DocketType);

                    // Save temporarily to disk or memory cache
                    DocketType = DocketType.Replace("/", "_");
                    var pdfFileName = $"{DocketType}_{students.FirstOrDefault().CurrentYearPeriodId}_{students.FirstOrDefault().Programme}.pdf";
                    var folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "Dockets");
                    var filePath = Path.Combine(folderPath, pdfFileName);

                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    System.IO.File.WriteAllBytes(filePath, pdfBytes);

                    downloadUrl = Url.Action("DownloadPdf", "Compliance", new { fileName = pdfFileName });
                    downloadUrl = $"{Request.Scheme}://{Request.Host}{downloadUrl}";
                }
                
                return Json(new { success = true, students = students, downloadUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching students with given terms");
                return Json(new { success = false, message = "An error occurred while searching students" });
            }
        }
        public IActionResult DownloadPdf(string fileName)
        {
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "Dockets", fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/pdf", fileName);
        }

        private async Task<string> GetUserJurisdictionInfo(ApplicationUser user, string role)
        {
            // All roles now have university-wide access
            return "All Students (University-wide access)";
        }

        private async Task<IQueryable<StudentData>> BuildStudentsQuery(ApplicationUser user, string role)
        {
            var query = _context.StudentDockets.AsQueryable();

            // Give all roles access to all students
            return query;
        }

        private string GetVerificationUrl(string studentNumber, string docketType)
        {
            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/Compliance/VerifyQr/{studentNumber}/{docketType}";
        }

        [HttpGet("Compliance/VerifyQr/{studentNumber}/{docketType}")]
        public async Task<IActionResult> VerifyQr(string studentNumber, string docketType)
        {
            if (string.IsNullOrWhiteSpace(studentNumber))
                return BadRequest("Invalid QR data.");

            try
            {
                var student = await _context.StudentDockets
                    .FirstOrDefaultAsync(s => s.StudentIdNumber == studentNumber);

                if (student == null)
                {
                    ViewBag.Message = "No record found for the provided student number.";
                    return View("VerifyQrNotFound");
                }

                await PopulateCurrentPeriodLabels(new List<StudentData> { student });
                student.DocketType = docketType;

                return View("VerifyQr", student);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying QR for student {studentNumber}", studentNumber);
                ViewBag.Message = "An error occurred while verifying this QR code.";
                return View("VerifyQrError");
            }
        }

        private async Task PopulateCurrentPeriodLabels(List<StudentData> students)
        {
            var periodIds = students
                .Select(s => s.CurrentYearPeriodId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (!periodIds.Any()) return;

            var labels = await _context.AcademicYearPeriods
                .Include(yp => yp.AcademicYear)
                .Include(yp => yp.AcademicPeriod)
                .Where(yp => periodIds.Contains(yp.Id))
                .ToDictionaryAsync(
                    yp => yp.Id,
                    yp => yp.AcademicYear.YearValue + " - " + yp.AcademicPeriod.PeriodName);

            foreach (var student in students)
            {
                student.CurrentPeriodLabel = labels.TryGetValue(student.CurrentYearPeriodId, out var label)
                    ? label
                    : student.CurrentYearPeriodId.ToString();
            }
        }


        private byte[] GeneratePdf(List<StudentData> students, string docket)
        {
            decimal threshold = 100;
            switch (docket)
            {
                case "1":
                    docket = "CA-1";
                    threshold = 50;
                    break;
                case "2":
                    docket = "CA-2";
                    threshold = 75;
                    break;
                case "3":
                    docket = "FINAL EXAMINATION";
                    threshold = 100;
                    break;
                case "4":
                    docket = "SUP/DEFFERED EXAMINATION";
                    threshold = 100;
                    break;
            }
            // Create a new PDF document
            using (var document = new PdfDocument())
            {
                foreach (var student in students)
                {
                    bool hasMetThreshold = false;

                    if(student.PercentPaid >= threshold)
                    {
                        hasMetThreshold = true;
                    }
                    // Add a page to the document
                    var page = document.Pages.Add();

                    // Create PDF graphics for the page
                    var graphics = page.Graphics;

                    // Set the standard font
                    var font = new PdfStandardFont(PdfFontFamily.TimesRoman, 12);

                    // Page dimensions
                    float pageWidth = page.Graphics.ClientSize.Width;
                    float pageHeight = page.Graphics.ClientSize.Height;

                    // Define fonts
                    var titleFont = new PdfStandardFont(PdfFontFamily.TimesRoman, 16, PdfFontStyle.Bold);
                    var headerFont = new PdfStandardFont(PdfFontFamily.TimesRoman, 10, PdfFontStyle.Bold);
                    var bodyFont = new PdfStandardFont(PdfFontFamily.TimesRoman, 9);
                    var smallFont = new PdfStandardFont(PdfFontFamily.TimesRoman, 6);
                    PdfFont boldFont = new PdfStandardFont(PdfFontFamily.TimesRoman, 9, PdfFontStyle.Bold);
                    PdfGridCellStyle boldStyle = new PdfGridCellStyle();
                    boldStyle.Font = new PdfStandardFont(PdfFontFamily.TimesRoman, 9, PdfFontStyle.Bold);
                    PdfGridCellStyle ordStyle = new PdfGridCellStyle();
                    ordStyle.Font = new PdfStandardFont(PdfFontFamily.TimesRoman, 9, PdfFontStyle.Regular);
                    boldStyle.StringFormat = new PdfStringFormat(PdfTextAlignment.Left, PdfVerticalAlignment.Middle);

                    float leftMargin = 40;
                    float topMargin = 40;
                    float currentY = 0;

                    // Create a text element with the text and font
                    var element = new PdfTextElement($"Eden University\n{docket} DOCKET\n{student.CurrentPeriodLabel}");
                    element.Font = new PdfStandardFont(PdfFontFamily.TimesRoman, 12, PdfFontStyle.Bold);
                    //element.Brush = new PdfSolidBrush(new PdfColor(89, 89, 93));
                    element.Brush = PdfBrushes.Black;
                    var result = element.Draw(page, new RectangleF(80, 0, page.Graphics.ClientSize.Width / 2, 200));

                    // Draw the logo image (if file exists)
                    try
                    {
                        string logoPath = Path.Combine("wwwroot", "images", "institution-logo.png");
                        if (System.IO.File.Exists(logoPath))
                        {
                            using (FileStream imageStream = new FileStream(logoPath, FileMode.Open, FileAccess.Read))
                            {
                                PdfImage img = PdfImage.FromStream(imageStream);
                                // img.Width
                                graphics.DrawImage(img, new RectangleF(0, 0, 50, 50));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Logo not found, continue without it
                        Console.WriteLine($"Logo image not found: {ex.Message}");
                    }

                    // Draw student photo (if file exists)
                    // Student Photo on the left
                    float photoX = 0;
                    float photoY = 60;
                    float photoWidth = 100;
                    float photoHeight = 120;

                    try
                    {
                        string photoPath = Path.Combine("wwwroot", "uploads", "student-photos", $"{student.StudentIdNumber}.png");
                        if (System.IO.File.Exists(photoPath))
                        {
                            using (FileStream photoStream = new FileStream(photoPath, FileMode.Open, FileAccess.Read))
                            {
                                PdfImage photo = PdfImage.FromStream(photoStream);
                                graphics.DrawImage(photo, new RectangleF(photoX, photoY, photoWidth, photoHeight));
                            }
                        }
                        else
                        {
                            // Draw placeholder rectangle for photo
                            graphics.DrawRectangle(new PdfPen(PdfBrushes.LightGray, 1), new RectangleF(photoX, photoY, photoWidth, photoHeight));
                        }
                    }
                    catch
                    {
                        // Draw placeholder rectangle for photo
                        graphics.DrawRectangle(new PdfPen(PdfBrushes.LightGray, 1), new RectangleF(photoX, photoY, photoWidth, photoHeight));
                    }

                    currentY = 60;

                    // Draw student information
                    PdfGrid gridInfor = new PdfGrid();
                    font = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
                    gridInfor.Style.Font = font;
                    gridInfor.Columns.Add(2);
                    gridInfor.Columns[0].Width = 80;
                    gridInfor.Columns[1].Width = 200;

                    PdfGridRow rowInfor = gridInfor.Rows.Add();
                    rowInfor.Cells[0].Value = "Exam Slip for:";
                    rowInfor.Cells[0].StringFormat.LineAlignment = PdfVerticalAlignment.Middle;
                    rowInfor.Cells[0].Style = ordStyle;
                    rowInfor.Cells[1].Value = student.FullName;
                    rowInfor.Cells[1].Style = boldStyle;

                    rowInfor = gridInfor.Rows.Add();
                    rowInfor.Cells[0].Value = "Student Number:";
                    rowInfor.Cells[0].StringFormat.LineAlignment = PdfVerticalAlignment.Middle;
                    rowInfor.Cells[0].Style = ordStyle;
                    rowInfor.Cells[1].Value = student.StudentIdNumber;
                    rowInfor.Cells[1].Style = boldStyle;

                    rowInfor = gridInfor.Rows.Add();
                    rowInfor.Cells[0].Value = "NRC No:";
                    rowInfor.Cells[0].StringFormat.LineAlignment = PdfVerticalAlignment.Middle;
                    rowInfor.Cells[0].Style = ordStyle;
                    rowInfor.Cells[1].Value = student.NRCNo;
                    rowInfor.Cells[1].Style = boldStyle;

                    rowInfor = gridInfor.Rows.Add();
                    rowInfor.Cells[0].Value = "Printed:";
                    rowInfor.Cells[0].StringFormat.LineAlignment = PdfVerticalAlignment.Middle;
                    rowInfor.Cells[0].Style = ordStyle;
                    rowInfor.Cells[1].Value = DateTime.Today.ToString("dd-MM-yyyy");
                    rowInfor.Cells[1].Style = boldStyle;

                    rowInfor = gridInfor.Rows.Add();
                    rowInfor.Cells[0].Value = "Status:";
                    rowInfor.Cells[0].StringFormat.LineAlignment = PdfVerticalAlignment.Middle;
                    rowInfor.Cells[0].Style = ordStyle;
                    rowInfor.Cells[1].Value = student.PercentPaid?.ToString("N02") + " % THRESHOLD " + (hasMetThreshold ? " MET " : " MET ") + "(" + student.OutstandingFees?.ToString("N02") + ")";
                    rowInfor.Cells[1].Style = boldStyle;

                    rowInfor = gridInfor.Rows.Add();
                    rowInfor.Cells[0].Value = "Delivery:";
                    rowInfor.Cells[0].Style = ordStyle;
                    rowInfor.Cells[1].Value = student.Delivery;
                    rowInfor.Cells[1].Style = boldStyle;

                    gridInfor.ApplyBuiltinStyle(PdfGridBuiltinStyle.ListTable1LightAccent6);
                    PdfGridStyle gridStyle1 = new PdfGridStyle();
                    gridStyle1.CellPadding = new PdfPaddings(5, 5, 1, 1);
                    gridInfor.Style = gridStyle1;

                    PdfGridLayoutFormat layoutFormat1 = new PdfGridLayoutFormat();
                    layoutFormat1.Layout = PdfLayoutType.Paginate;
                    result = gridInfor.Draw(page, photoX + photoWidth, photoY, pageWidth - 35, layoutFormat1);

                    // Draw QR code at top right of details section
                    try
                    {
                        // Generate verification URL with student info
                        string verificationUrl = GetVerificationUrl(student.StudentIdNumber, docket);

                        // Create QR code using Syncfusion
                        PdfQRBarcode qrCode = new PdfQRBarcode();
                        qrCode.Text = verificationUrl;
                        qrCode.XDimension = 3; // Size of individual modules
                        qrCode.Size = new SizeF(100, 100);

                        // Draw the QR code
                        qrCode.Draw(page, new PointF(pageWidth - 130, photoY));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error generating QR code: {ex.Message}");
                    }

                    currentY = photoY + photoHeight + 25;

                    var status = hasMetThreshold ? "" : "NOT";

                    // Registration message
                    string regString = "Student is Registered under: ";
                    string registrationMsg = $"{student.Programme} Year {student.StudentCurrentYear} - {student.CurrentPeriodLabel}";
                    string regString3 = $"Candidate has been {status} authorized to write {docket} in the following courses:\n";

                    PdfTextElement regElement = new PdfTextElement(regString, bodyFont);
                    result = regElement.Draw(page, new RectangleF(photoX, currentY, 120, 30));

                    PdfTextElement regElementMsg = new PdfTextElement(registrationMsg, boldFont);
                    result = regElementMsg.Draw(page, new RectangleF(result.Bounds.Right, currentY, pageWidth - 120, 30));
                    currentY = result.Bounds.Bottom;

                    PdfTextElement regElementMsg2 = new PdfTextElement(regString3, boldFont);
                    result = regElementMsg2.Draw(page, new RectangleF(photoX, currentY, pageWidth - 100, 30));
                    currentY = result.Bounds.Bottom;

                    PdfTextElement regElementBlank = new PdfTextElement("-", boldFont);
                    result = regElementBlank.Draw(page, new RectangleF(photoX, currentY, pageWidth - 100, 30));
                    currentY = result.Bounds.Bottom;

                    // Course list
                    string[] courses = [];
                    if (student.CoursesRegistered != null)
                    {
                        courses = student.CoursesRegistered.Split("\r\n");
                    }

                    // foreach (var course in courses)
                    // {
                    //     graphics.DrawString(course.Trim(), bodyFont, PdfBrushes.Black, new PointF(leftMargin + 10, currentY));
                    //     currentY += 20;
                    //     graphics.DrawLine(new PdfPen(PdfBrushes.Black, 0.5f), new PointF(leftMargin, currentY), new PointF(pageWidth - 40, currentY));
                    //     currentY += 15;
                    // }

                    var tableWidth = pageWidth - 35;

                    PdfGrid grid = new PdfGrid();
                    font = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
                    grid.Style.Font = font;
                    grid.Columns.Add(3);
                    grid.Columns[0].Width = tableWidth / 2;
                    grid.Columns[1].Width = tableWidth / 4;
                    grid.Columns[2].Width = tableWidth / 4;

                    grid.Headers.Add(1);
                    PdfStringFormat stringFormat = new PdfStringFormat(PdfTextAlignment.Center, PdfVerticalAlignment.Middle);
                    PdfGridRow header = grid.Headers[0];

                    header.Cells[0].Value = "COURSE";
                    header.Cells[0].StringFormat.LineAlignment = PdfVerticalAlignment.Middle;
                    header.Cells[0].Style = boldStyle;

                    header.Cells[1].Value = "DATE/TIME";
                    header.Cells[1].StringFormat = stringFormat;
                    header.Cells[1].Style = boldStyle;

                    header.Cells[2].Value = "SIGNATURE INVIGILATOR";
                    header.Cells[2].StringFormat = stringFormat;
                    header.Cells[2].Style = boldStyle;

                    foreach (var course in courses)
                    {

                        PdfGridRow row = grid.Rows.Add();
                        row.Cells[0].Value = course.Trim();
                        row.Cells[0].StringFormat.LineAlignment = PdfVerticalAlignment.Middle;
                        row.Cells[0].Style = boldStyle;

                    }

                    grid.ApplyBuiltinStyle(PdfGridBuiltinStyle.GridTable1Light);
                    PdfGridStyle gridStyle = new PdfGridStyle();
                    gridStyle.CellPadding = new PdfPaddings(5, 5, 5, 5);
                    grid.Style = gridStyle;

                    PdfGridLayoutFormat layoutFormat = new PdfGridLayoutFormat();
                    layoutFormat.Layout = PdfLayoutType.Paginate;
                    result = grid.Draw(page, photoX, currentY, pageWidth - 35, layoutFormat);

                    // Calculate position for footer
                    currentY = result.Bounds.Bottom + 20;

                    // Footer warning text
                    string footerText = "Kindly cross check your courses on this slip against the separate examination timetable for the EXACT date and time of the examination.\n" +
                                       "VERY IMPORTANT - Admission into the Examination Hall will be STRICTLY by STUDENT IDENTITY CARD, NRC OR PASSPORT, this " +
                                       "EXAMINATION CONFIRMATION SLIP and clearance of all OUTSTANDING TUITION FEES.";

                    PdfTextElement footerElement = new PdfTextElement(footerText, smallFont);
                    footerElement.Brush = PdfBrushes.Black;
                    footerElement.Draw(page, new RectangleF(photoX, currentY, pageWidth - 100, 50));

                }

                // Save the document to memory stream
                using (MemoryStream stream = new MemoryStream())
                {
                    document.Save(stream);
                    return stream.ToArray();
                }
            }
        }
    }
}
