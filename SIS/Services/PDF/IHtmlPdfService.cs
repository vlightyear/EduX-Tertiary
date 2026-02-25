using PuppeteerSharp;
using Microsoft.Extensions.Logging;
using SIS.Models.StudentApplication;

namespace SIS.Services.PDF
{
    public interface IHtmlPdfService
    {
        Task<byte[]> GenerateAdmissionLetterAsync(Student student);
        Task<byte[]> GenerateMultiPageDocumentAsync(string htmlContent, PdfOptions options = null);
    }

    public class HtmlPdfService : IHtmlPdfService
    {
        private readonly ILogger<HtmlPdfService> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IInstitutionConfigService _institutionConfig;

        public HtmlPdfService(ILogger<HtmlPdfService> logger, IWebHostEnvironment webHostEnvironment, IInstitutionConfigService institutionConfig)
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _institutionConfig = institutionConfig;
        }

        public async Task<byte[]> GenerateAdmissionLetterAsync(Student student)
        {
            try
            {
                var htmlContent = GenerateAdmissionLetterHtml(student);

                var options = new PdfOptions
                {
                    Format = PuppeteerSharp.Media.PaperFormat.A4,
                    PrintBackground = true,
                    MarginOptions = new PuppeteerSharp.Media.MarginOptions
                    {
                        Top = "0.5in",
                        Right = "0.5in",
                        Bottom = "0.5in",
                        Left = "0.5in"
                    },
                    HeaderTemplate = "",
                    FooterTemplate = ""
                };

                return await GenerateMultiPageDocumentAsync(htmlContent, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating admission letter for Student ID: {StudentId}", student.StudentId_Number);
                throw;
            }
        }

        public async Task<byte[]> GenerateMultiPageDocumentAsync(string htmlContent, PdfOptions options = null)
        {
            try
            {
                // Download Chromium if not already present
                await new BrowserFetcher().DownloadAsync();

                // Launch browser
                await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                });

                // Create new page
                await using var page = await browser.NewPageAsync();

                // Set content
                await page.SetContentAsync(htmlContent, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
                });

                // Generate PDF
                var pdfOptions = options ?? new PdfOptions
                {
                    Format = PuppeteerSharp.Media.PaperFormat.A4,
                    PrintBackground = true
                };

                var pdfBytes = await page.PdfDataAsync(pdfOptions);
                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF from HTML content");
                throw;
            }
        }

        private string GenerateAdmissionLetterHtml(Student student)
        {
            // Get logo URL - you can store this in configuration or use a local file
            var institution = _institutionConfig.GetCurrentInstitution();
            var logoUrl = GetLogoUrl();

            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Admission Letter - {student.FullName}</title>
    <style>
        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap');
        
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            line-height: 1.6;
            color: #1f2937;
            background: #ffffff;
        }}

        .document {{
            max-width: 8.5in;
            margin: 0 auto;
            background: white;
            min-height: 11in;
        }}

        .header {{
            background: linear-gradient(135deg, #0891b2 0%, #0284c7 100%);
            color: white;
            padding: 2rem;
            margin-bottom: 2rem;
            position: relative;
            overflow: hidden;
        }}

        .header::before {{
            content: '';
            position: absolute;
            top: -50%;
            right: -20%;
            width: 40%;
            height: 200%;
            background: rgba(255, 255, 255, 0.1);
            transform: rotate(15deg);
        }}

        .header-content {{
            display: flex;
            align-items: center;
            gap: 1.5rem;
            position: relative;
            z-index: 1;
        }}

        .logo {{
            width: 80px;
            height: 80px;
            background: white;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        }}

        .logo img {{
            width: 60px;
            height: 60px;
            object-fit: contain;
        }}

        .university-info h1 {{
            font-size: 2.5rem;
            font-weight: 700;
            margin-bottom: 0.5rem;
            letter-spacing: -0.025em;
        }}

        .university-info p {{
            font-size: 1.1rem;
            opacity: 0.95;
            font-weight: 300;
        }}

        .content {{
            padding: 0 2rem 2rem;
        }}

        .date {{
            text-align: right;
            font-size: 1rem;
            color: #6b7280;
            margin-bottom: 2rem;
        }}

        .recipient {{
            font-size: 1.1rem;
            margin-bottom: 1.5rem;
        }}

        .subject {{
            background: linear-gradient(135deg, #f0f9ff 0%, #e0f2fe 100%);
            border-left: 4px solid #0891b2;
            padding: 1rem 1.5rem;
            margin: 2rem 0;
            border-radius: 0 8px 8px 0;
        }}

        .subject h2 {{
            color: #0891b2;
            font-size: 1.2rem;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.025em;
        }}

        .main-text {{
            font-size: 1rem;
            line-height: 1.7;
            margin-bottom: 2rem;
            text-align: justify;
        }}

        .conditions {{
            margin: 2rem 0;
        }}

        .conditions h3 {{
            font-size: 1.2rem;
            font-weight: 600;
            color: #374151;
            margin-bottom: 1rem;
        }}

        .condition-item {{
            display: flex;
            gap: 1rem;
            margin-bottom: 1rem;
            padding: 0.75rem;
            background: #f9fafb;
            border-radius: 8px;
            border-left: 3px solid #e5e7eb;
        }}

        .condition-number {{
            background: #0891b2;
            color: white;
            width: 24px;
            height: 24px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 0.875rem;
            font-weight: 600;
            flex-shrink: 0;
            margin-top: 0.125rem;
        }}

        .condition-text {{
            flex: 1;
            font-size: 0.95rem;
            line-height: 1.6;
        }}

        .notice-box {{
            background: linear-gradient(135deg, #fef2f2 0%, #fee2e2 100%);
            border: 1px solid #fca5a5;
            border-radius: 12px;
            padding: 1.5rem;
            margin: 2rem 0;
            position: relative;
        }}

        .notice-box::before {{
            content: '⚠️';
            position: absolute;
            top: -12px;
            left: 20px;
            background: white;
            padding: 0 8px;
            font-size: 1.2rem;
        }}

        .notice-text {{
            color: #dc2626;
            font-weight: 600;
            text-align: center;
            font-size: 1rem;
        }}

        .congratulations {{
            background: linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%);
            border: 1px solid #86efac;
            border-radius: 12px;
            padding: 2rem;
            margin: 2rem 0;
            text-align: center;
            position: relative;
            overflow: hidden;
        }}

        .congratulations::before {{
            content: '';
            position: absolute;
            top: -50%;
            left: -50%;
            width: 200%;
            height: 200%;
            background: url('data:image/svg+xml,<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><circle cx=""50"" cy=""50"" r=""2"" fill=""%2316a34a"" opacity=""0.1""/></svg>') repeat;
            animation: float 20s linear infinite;
        }}

        @keyframes float {{
            0% {{ transform: translateX(-50%) translateY(-50%) rotate(0deg); }}
            100% {{ transform: translateX(-50%) translateY(-50%) rotate(360deg); }}
        }}

        .congratulations-content {{
            position: relative;
            z-index: 1;
        }}

        .congratulations h3 {{
            color: #16a34a;
            font-size: 1.3rem;
            font-weight: 700;
            margin-bottom: 0.5rem;
        }}

        .congratulations p {{
            color: #15803d;
            font-size: 1rem;
            line-height: 1.6;
        }}

        .signature-section {{
            margin-top: 3rem;
            margin-bottom: 2rem;
        }}

        .signature-line {{
            width: 250px;
            height: 2px;
            background: #374151;
            margin: 2rem 0 1rem 0;
        }}

        .signature-name {{
            font-weight: 600;
            font-size: 1.1rem;
            margin-bottom: 0.25rem;
        }}

        .signature-title {{
            color: #6b7280;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            font-size: 0.9rem;
        }}

        .footer {{
            background: #f8fafc;
            border-top: 1px solid #e2e8f0;
            padding: 2rem;
            margin-top: 3rem;
        }}

        .footer-content {{
            display: grid;
            grid-template-columns: 1fr auto;
            gap: 2rem;
            align-items: center;
        }}

        .contact-info h4 {{
            color: #0891b2;
            font-size: 1.2rem;
            font-weight: 700;
            margin-bottom: 1rem;
        }}

        .contact-item {{
            display: flex;
            align-items: center;
            gap: 0.5rem;
            margin-bottom: 0.5rem;
            font-size: 0.9rem;
            color: #4b5563;
        }}

        .student-id {{
            background: #0891b2;
            color: white;
            padding: 0.5rem 1rem;
            border-radius: 6px;
            font-family: 'Courier New', monospace;
            font-weight: 600;
            font-size: 0.9rem;
        }}

        /* Print styles */
        @media print {{
            .document {{
                margin: 0;
                max-width: none;
                width: 100%;
            }}
            
            .header {{
                -webkit-print-color-adjust: exact;
                color-adjust: exact;
            }}
            
            .page-break {{
                page-break-before: always;
            }}
        }}

        /* Responsive */
        @media (max-width: 768px) {{
            .header-content {{
                flex-direction: column;
                text-align: center;
            }}
            
            .university-info h1 {{
                font-size: 2rem;
            }}
        }}
    </style>
</head>
<body>
    <div class='document'>
        <header class='header'>
            <div class='header-content'>
                <div class='logo'>
                    <img src='{logoUrl}' alt='{institution.Name} Logo' />
                </div>
                <div class='university-info'>
                    <h1>{institution.Name.ToUpper()}</h1>
                    <p>Certified by the Higher Education Authority</p>
                </div>
            </div>
        </header>

        <main class='content'>
            <div class='date'>
                Date: {DateTime.Now:dd}{GetOrdinalSuffix(DateTime.Now.Day)} {DateTime.Now:MMMM yyyy}
            </div>

            <div class='recipient'>
                Dear <strong>{student.FullName}</strong>,
            </div>

            <div class='subject'>
                <h2>RE: ADMISSION IN {student.Programme?.Name?.ToUpper()}  BASIS</h2>
            </div>

            <div class='main-text'>
                I write to offer you admission at {institution.Name} for the academic year <strong>{student.AcademicYear?.YearValue}</strong> for a 4-year course of study leading to the award of <strong>{student.Programme?.Name}</strong>. The registration will run from <strong>2nd June to 20th June {DateTime.Now.Year}</strong>. Classes will commence on <strong>7th July {DateTime.Now.Year}</strong> and only registered students are eligible to attend. Late registration will attract a penalty fee.
            </div>

            <div class='conditions'>
                <h3>This offer is valid upon:</h3>
                
                <div class='condition-item'>
                    <div class='condition-number'>1</div>
                    <div class='condition-text'>
                        Production of your original certificates or statements of results in support of your qualifications as stated in your application.
                    </div>
                </div>

                <div class='condition-item'>
                    <div class='condition-number'>2</div>
                    <div class='condition-text'>
                        Production of your National Registration Card or other nationally recognizable document.
                    </div>
                </div>

                <div class='condition-item'>
                    <div class='condition-number'>3</div>
                    <div class='condition-text'>
                        Payment of at least 75% of the tuition and other fees during registration.
                    </div>
                </div>

                <div class='condition-item'>
                    <div class='condition-number'>4</div>
                    <div class='condition-text'>
                        Once fees are paid, refund of tuition fees will attract a 10% administrative charge and processing a refund will take at least 30 days before payment is ready.
                    </div>
                </div>

                <div class='condition-item'>
                    <div class='condition-number'>5</div>
                    <div class='condition-text'>
                        You are required to familiarize yourself with the student's general rules and regulations handbook from Dean of Students Affairs and adhere to them.
                    </div>
                </div>

                <div class='condition-item'>
                    <div class='condition-number'>6</div>
                    <div class='condition-text'>
                        The university does not offer any scholarships, kindly confirm all payment requests with the university using our official contact details.
                    </div>
                </div>
            </div>

            <div class='notice-box'>
                <div class='notice-text'>
                    This offer will lapse if not taken in the {DateTime.Now.Year} academic year.
                </div>
            </div>

            <div class='congratulations'>
                <div class='congratulations-content'>
                    <h3>🎉 Congratulations on your admission at {institution.Name}!</h3>
                    <p>On behalf of the university, we wish you a warm welcome and success in your studies here at your dream university.</p>
                </div>
            </div>

            <div class='signature-section'>
                <div>Yours sincerely,</div>
                <div class='signature-line'></div>
                <div class='signature-name'>Registrars Office</div>
                <div class='signature-title'>REGISTRAR</div>
            </div>
        </main>

        <footer class='footer'>
            <div class='footer-content'>
                <div class='contact-info'>
                    <h4>{institution.Name.ToUpper()}</h4>
                    <div class='contact-item'>
                        📍 {institution.ContactInfo?.Address ?? "Address not configured"}
                    </div>
                    <div class='contact-item'>
                        📞 Mobile No. {institution.ContactInfo?.Phone ?? "+260-XXX-XXXX"}
                    </div>
                    <div class='contact-item'>
                        ✉️ E-mail: {institution.EmailSettings.SenderEmail}
                    </div>
                </div>
                <div class='student-id'>
                    Student ID: {student.StudentId_Number}
                </div>
            </div>
        </footer>
    </div>
</body>
</html>";
        }

        private string GetLogoUrl()
        {
            var institution = _institutionConfig.GetCurrentInstitution();
            var logoUrl = $"{institution.ContactInfo?.Website?.TrimEnd('/')}{institution.LogoPath}";

            // Fallback if logo URL is not available
            return !string.IsNullOrEmpty(logoUrl) ? logoUrl : "https://via.placeholder.com/80x80/0891b2/ffffff?text=UNIV";
        }
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
    }
}