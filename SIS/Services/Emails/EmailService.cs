using System.Net;
using System.Net.Mail;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.Extensions.Options;
using SIS.Models;
using SIS.Models.Admin;
using SIS.Models.Configuration;
using SIS.Models.StudentApplication;
using SIS.Services;
using SIS.Services.Emails;

public class EmailService : IEmailService
{
    private readonly SIS.Models.Configuration.EmailSettings _emailSettings;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IInstitutionConfigService _institutionConfig;

    public EmailService(IOptions<SIS.Models.Configuration.EmailSettings> emailSettings, IWebHostEnvironment webHostEnvironment, IInstitutionConfigService institutionConfig)
    {
        _emailSettings = emailSettings.Value;
        _webHostEnvironment = webHostEnvironment;
        _institutionConfig = institutionConfig;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
    {
        try
        {
            using var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port);

            smtpClient.EnableSsl = _emailSettings.EnableSsl;
            smtpClient.UseDefaultCredentials = _emailSettings.UseDefaultCredentials;
            smtpClient.Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);

            using var mailMessage = new MailMessage();

            mailMessage.From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName);
            mailMessage.To.Add(toEmail);
            mailMessage.Subject = subject;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = isHtml;

            await smtpClient.SendMailAsync(mailMessage);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Email sending failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendApplicationSubmissionEmailAsync(string applicantName, string applicantEmail,
        string programmeName, string schoolName, string referenceNumber,
        decimal paymentAmount, string transactionReference)
    {
        try
        {
            string subject = $"Application Submitted Successfully - {referenceNumber}";
            string emailBody = GenerateApplicationSubmissionEmailHtml(applicantName, programmeName,
                schoolName, referenceNumber, paymentAmount, transactionReference);

            return await SendEmailAsync(applicantEmail, subject, emailBody, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Application submission email failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendEmailWithAttachmentAsync(string toEmail, string subject, string body, byte[] attachmentData, string attachmentName, bool isHtml = true)
    {
        try
        {
            using var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port);

            smtpClient.EnableSsl = _emailSettings.EnableSsl;
            smtpClient.UseDefaultCredentials = _emailSettings.UseDefaultCredentials;
            smtpClient.Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);

            using var mailMessage = new MailMessage();

            mailMessage.From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName);
            mailMessage.To.Add(toEmail);
            mailMessage.Subject = subject;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = isHtml;

            // Add PDF attachment
            if (attachmentData != null && attachmentData.Length > 0)
            {
                using var attachmentStream = new MemoryStream(attachmentData);
                var attachment = new Attachment(attachmentStream, attachmentName, "application/pdf");
                mailMessage.Attachments.Add(attachment);

                await smtpClient.SendMailAsync(mailMessage);
            }
            else
            {
                await smtpClient.SendMailAsync(mailMessage);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Email with attachment sending failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendAdmissionEmailAsync(Student student, byte[] admissionLetterPdf)
    {
        try
        {
            var institutionName = _institutionConfig.GetInstitutionName();
            string subject = $"🎉 Congratulations! You've Been Admitted to {institutionName}";
            string emailBody = GenerateAdmissionEmailHtml(student);
            string attachmentName = $"Admission_Letter_{student.StudentId_Number}.pdf";

            return await SendEmailWithAttachmentAsync(student.Email, subject, emailBody, admissionLetterPdf, attachmentName, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Admission email failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendRejectionEmailAsync(string applicantName, string applicantEmail, string programmeName, string schoolName, string reason)
    {
        try
        {

            var institutionName = _institutionConfig.GetInstitutionName();
            string subject = $"Application Status Update - {institutionName}";
            string emailBody = GenerateRejectionEmailHtml(applicantName, programmeName, schoolName, reason);

            return await SendEmailAsync(applicantEmail, subject, emailBody, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Rejection email failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendWaitlistEmailAsync(string applicantName, string applicantEmail, string programmeName, string schoolName, DateTime academicYearStart)
    {
        try
        {
            string subject = "Application Status Update - You've Been Waitlisted";
            string emailBody = GenerateWaitlistEmailHtml(applicantName, programmeName, schoolName, academicYearStart);

            return await SendEmailAsync(applicantEmail, subject, emailBody, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Waitlist email failed: {ex.Message}");
            return false;
        }
    }

    private string GenerateApplicationSubmissionEmailHtml(string applicantName, string programmeName,
        string schoolName, string referenceNumber, decimal paymentAmount, string transactionReference)
    {
        var institution = _institutionConfig.GetCurrentInstitution();
        // Get the logo URL (you can adjust the path as needed)
        string logoUrl = $"{institution.ContactInfo?.Website?.TrimEnd('/')}{institution.LogoPath}";

        return $@"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Application Submission Confirmation</title>
                <style>
                    body {{
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        line-height: 1.6;
                        color: #333;
                        max-width: 600px;
                        margin: 0 auto;
                        background-color: #f8f9fa;
                    }}
                    .email-container {{
                        background-color: #ffffff;
                        border-radius: 10px;
                        box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                        overflow: hidden;
                    }}
                    .header {{
                        background: linear-gradient(135deg, #2c3e50 0%, #3498db 100%);
                        color: white;
                        padding: 30px 20px;
                        text-align: center;
                    }}
                    .logo {{
                        max-width: 150px;
                        height: auto;
                        margin-bottom: 15px;
                    }}
                    .header h1 {{
                        margin: 0;
                        font-size: 28px;
                        font-weight: 300;
                    }}
                    .content {{
                        padding: 40px 30px;
                    }}
                    .welcome-message {{
                        font-size: 18px;
                        color: #2c3e50;
                        margin-bottom: 25px;
                        text-align: center;
                    }}
                    .application-details {{
                        background-color: #f8f9fa;
                        border-left: 5px solid #3498db;
                        padding: 20px;
                        margin: 25px 0;
                        border-radius: 5px;
                    }}
                    .detail-row {{
                        display: flex;
                        justify-content: space-between;
                        margin-bottom: 10px;
                        padding: 8px 0;
                        border-bottom: 1px solid #e9ecef;
                    }}
                    .detail-row:last-child {{
                        border-bottom: none;
                    }}
                    .detail-label {{
                        font-weight: 600;
                        color: #2c3e50;
                        flex: 1;
                    }}
                    .detail-value {{
                        flex: 2;
                        text-align: right;
                        color: #555;
                    }}
                    .payment-info {{
                        background-color: #d4edda;
                        border: 1px solid #c3e6cb;
                        border-radius: 5px;
                        padding: 15px;
                        margin: 20px 0;
                    }}
                    .payment-info h3 {{
                        color: #155724;
                        margin-top: 0;
                    }}
                    .next-steps {{
                        background-color: #fff3cd;
                        border: 1px solid #ffeaa7;
                        border-radius: 5px;
                        padding: 20px;
                        margin: 25px 0;
                    }}
                    .next-steps h3 {{
                        color: #856404;
                        margin-top: 0;
                    }}
                    .footer {{
                        background-color: #2c3e50;
                        color: white;
                        padding: 25px 30px;
                        text-align: center;
                    }}
                    .contact-info {{
                        margin-top: 15px;
                        font-size: 14px;
                    }}
                    .highlight {{
                        color: #3498db;
                        font-weight: 600;
                    }}
                    @media (max-width: 600px) {{
                        .content {{
                            padding: 20px;
                        }}
                        .detail-row {{
                            flex-direction: column;
                        }}
                        .detail-value {{
                            text-align: left;
                            margin-top: 5px;
                        }}
                    }}
                </style>
            </head>
            <body>
                <div class='email-container'>
                    <div class='header'>
                        <img src='{logoUrl}' alt='{institution.Name} Logo' class='logo'>
                        <h1>Application Submitted Successfully!</h1>
                    </div>
        
                    <div class='content'>
                        <div class='welcome-message'>
                            Dear <strong>{applicantName}</strong>,<br>
                            Congratulations! Your application has been successfully submitted and processed.
                        </div>
            
                       <p>Thank you for choosing <strong>{institution.Name}</strong> for your academic journey. We are delighted to have received your application and look forward to welcoming you to our community of learners.</p>
            
                        <div class='application-details'>
                            <h3 style='color: #2c3e50; margin-top: 0;'>📋 Application Details</h3>
                            <div class='detail-row'>
                                <span class='detail-label'>Reference Number:</span>
                                <span class='detail-value highlight'>{referenceNumber}</span>
                            </div>
                            <div class='detail-row'>
                                <span class='detail-label'>Programme:</span>
                                <span class='detail-value'>{programmeName}</span>
                            </div>
                            <div class='detail-row'>
                                <span class='detail-label'>School:</span>
                                <span class='detail-value'>{schoolName}</span>
                            </div>
                            <div class='detail-row'>
                                <span class='detail-label'>Application Date:</span>
                                <span class='detail-value'>{DateTime.Now:MMMM dd, yyyy}</span>
                            </div>
                        </div>
            
                        <div class='payment-info'>
                            <h3>✅ Payment Confirmed</h3>
                            <div class='detail-row'>
                                <span class='detail-label'>Amount Paid:</span>
                                <span class='detail-value'><strong>K{paymentAmount:N2}</strong></span>
                            </div>
                            <div class='detail-row'>
                                <span class='detail-label'>Transaction Reference:</span>
                                <span class='detail-value'>{transactionReference}</span>
                            </div>
                            <p style='margin-bottom: 0; color: #155724;'>
                                <small>Keep this transaction reference for your records.</small>
                            </p>
                        </div>
            
                        <div class='next-steps'>
                            <h3>🚀 What Happens Next?</h3>
                            <ul style='margin: 10px 0;'>
                                <li><strong>Application Review:</strong> Our admissions team will carefully review your application within 2-3 weeks.</li>
                                <li><strong>Status Updates:</strong> You will receive email notifications about any changes to your application status.</li>
                                <li><strong>Additional Requirements:</strong> If we need any additional documents, we'll contact you directly.</li>
                                <li><strong>Admission Decision:</strong> You'll be notified of the final decision via email and can check your application portal.</li>
                            </ul>
                        </div>
            
                        <p style='text-align: center; margin-top: 30px;'>
<strong>Thank you for choosing {institution.Name}!</strong><br>
                            We're excited about the possibility of having you join our academic community.
                        </p>
                    </div>
        
                    <div class='footer'>
                        <h4 style='margin: 0 0 10px 0;'>{institution.Name}</h4>
                        <div class='contact-info'>
                            📧 <strong>Email:</strong> {institution.EmailSettings.SenderEmail}<br>
                            📞 <strong>Phone:</strong> {institution.ContactInfo?.Phone ?? "+260-XXX-XXXX"}<br>
                            🌐 <strong>Website:</strong> {institution.ContactInfo?.Website ?? "www.institution.edu"}<br>
                            📍 <strong>Address:</strong> {institution.ContactInfo?.Address ?? "Campus Address"}
                        </div>
                        <p style='margin-top: 15px; font-size: 12px; opacity: 0.8;'>
                            This is an automated message. Please do not reply to this email.<br>
                            For inquiries, contact us at {institution.EmailSettings.SenderEmail}
                        </p>
                    </div>
                </div>
            </body>
        </html>";
    }

    private string GenerateAdmissionEmailHtml(Student student)
    {

        var institution = _institutionConfig.GetCurrentInstitution();
        string logoUrl = $"{institution.ContactInfo?.Website?.TrimEnd('/')}{institution.LogoPath}";

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Admission Confirmation</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            background-color: #f8f9fa;
        }}
        .email-container {{
            background-color: #ffffff;
            border-radius: 10px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #27ae60 0%, #2ecc71 100%);
            color: white;
            padding: 30px 20px;
            text-align: center;
        }}
        .logo {{
            max-width: 150px;
            height: auto;
            margin-bottom: 15px;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 300;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .congratulations {{
            font-size: 24px;
            color: #27ae60;
            margin-bottom: 25px;
            text-align: center;
            font-weight: 600;
        }}
        .student-details {{
            background-color: #d4edda;
            border: 1px solid #c3e6cb;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .detail-row {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 10px;
            padding: 8px 0;
            border-bottom: 1px solid #a3d5a5;
        }}
        .detail-row:last-child {{
            border-bottom: none;
        }}
        .detail-label {{
            font-weight: 600;
            color: #155724;
            flex: 1;
        }}
        .detail-value {{
            flex: 2;
            text-align: right;
            color: #155724;
        }}
        .attachment-notice {{
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
            text-align: center;
        }}
        .footer {{
            background-color: #27ae60;
            color: white;
            padding: 25px 30px;
            text-align: center;
        }}
        .highlight {{
            color: #27ae60;
            font-weight: 600;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='{logoUrl}' alt='{institution.Name} Logo' class='logo'>
            <h1>🎉 Congratulations!</h1>
        </div>
        
        <div class='content'>
            <div class='congratulations'>
                You've Been Admitted to {institution.Name}!
            </div>
            
            <p>Dear <strong>{student.FullName}</strong>,</p>
            
            <p>We are thrilled to inform you that you have been <strong>officially admitted</strong> to {institution.Name}! This is a significant achievement, and we're excited to welcome you to our academic community.</p>
            
            <div class='student-details'>
                <h3 style='color: #155724; margin-top: 0;'>🎓 Your Admission Details</h3>
                <div class='detail-row'>
                    <span class='detail-label'>Student ID:</span>
                    <span class='detail-value highlight'>{student.StudentId_Number}</span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>Programme:</span>
                    <span class='detail-value'>{student.Programme?.Name ?? "N/A"}</span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>School:</span>
                    <span class='detail-value'>{student.School?.Name ?? "N/A"}</span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>Academic Year:</span>
                    <span class='detail-value'>{student.AcademicYear?.YearValue ?? "N/A"}</span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>Admission Date:</span>
                    <span class='detail-value'>{student.AdmissionDate:MMMM dd, yyyy}</span>
                </div>
            </div>
            
            <div class='attachment-notice'>
                <h3 style='color: #856404; margin-top: 0;'>📄 Important Document Attached</h3>
                <p style='margin-bottom: 0; color: #856404;'>
                    <strong>Please find your official Admission Letter attached to this email.</strong><br>
                    This document contains important details about your admission, including:
                </p>
                <ul style='text-align: left; color: #856404; margin-top: 10px;'>
                    <li>Complete admission terms and conditions</li>
                    <li>Registration process and deadlines</li>
                    <li>Required documents for enrollment</li>
                    <li>Academic calendar and important dates</li>
                    <li>Financial information and payment details</li>
                </ul>
            </div>
            
            <p><strong>Next Steps:</strong></p>
            <ol>
                <li>Review your attached admission letter carefully</li>
                <li>Complete the registration process as outlined in the letter</li>
                <li>Prepare required documents for enrollment</li>
                <li>Watch for further communications regarding orientation</li>
            </ol>
            
            <p style='text-align: center; margin-top: 30px;'>
                <strong>Welcome to {institution.Name}!</strong><br>
                We look forward to supporting you on your academic journey.
            </p>
        </div>
        
        <div class='footer'>
<h4 style='margin: 0 0 10px 0;'>{institution.Name}</h4>
            <div style='margin-top: 15px; font-size: 14px;'>
               📧 <strong>Email:</strong> {institution.EmailSettings.SenderEmail}<br>
                📞 <strong>Phone:</strong> {institution.ContactInfo?.Phone ?? "+260-XXX-XXXX"}<br>
                🌐 <strong>Website:</strong> {institution.ContactInfo?.Website ?? "www.institution.edu"}<br>
                📍 <strong>Address:</strong> {institution.ContactInfo?.Address ?? "Campus Address"}
            </div>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateRejectionEmailHtml(string applicantName, string programmeName, string schoolName, string reason)
    {

        var institution = _institutionConfig.GetCurrentInstitution();
        string logoUrl = $"{institution.ContactInfo?.Website?.TrimEnd('/')}{institution.LogoPath}"; // Add your logo URL here

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Application Status Update</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            background-color: #f8f9fa;
        }}
        .email-container {{
            background-color: #ffffff;
            border-radius: 10px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #2c3e50 0%, #34495e 100%);
            color: white;
            padding: 30px 20px;
            text-align: center;
        }}
        .logo {{
            max-width: 150px;
            height: auto;
            margin-bottom: 15px;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 300;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .status-message {{
            font-size: 18px;
            color: #2c3e50;
            margin-bottom: 25px;
            text-align: center;
        }}
        .reason-box {{
            background-color: #f8f9fa;
            border-left: 5px solid #e74c3c;
            padding: 20px;
            margin: 25px 0;
            border-radius: 5px;
        }}
        .encouragement {{
            background-color: #d1ecf1;
            border: 1px solid #bee5eb;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .footer {{
            background-color: #2c3e50;
            color: white;
            padding: 25px 30px;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='{logoUrl}' alt='{institution.Name} Logo' class='logo'>
            <h1>Application Status Update</h1>
        </div>
        
        <div class='content'>
            <div class='status-message'>
                Dear <strong>{applicantName}</strong>,
            </div>
            
            <p>Thank you for your interest in {institution.Name} and for taking the time to submit your application for the <strong>{programmeName}</strong> programme in the <strong>{schoolName}</strong>.</p>
            
            <p>After careful consideration of your application, we regret to inform you that we are unable to offer you admission at this time.</p>
            
            <div class='reason-box'>
                <h3 style='color: #c0392b; margin-top: 0;'>Reason for Decision</h3>
                <p style='margin-bottom: 0; color: #2c3e50;'>{reason}</p>
            </div>
            
            <div class='encouragement'>
                <h3 style='color: #0c5460; margin-top: 0;'>💪 Don't Give Up!</h3>
                <p style='margin-bottom: 0; color: #0c5460;'>
                    We understand this news may be disappointing, but we encourage you to:
                    <br>• Consider reapplying for future intake periods
                    <br>• Explore other programmes that might align with your interests
                    <br>• Continue pursuing your educational goals
                    <br>• Contact our admissions office if you have any questions
                </p>
            </div>
            
            <p>We appreciate your interest in {institution.Name} and wish you all the best in your future academic endeavors.</p>
            
            <p style='text-align: center; margin-top: 30px;'>
                <strong>Best regards,</strong><br>
                {institution.Name} Admissions Team
            </p>
        </div>
        
        <div class='footer'>
<h4 style='margin: 0 0 10px 0;'>{institution.Name}</h4>
            <div style='margin-top: 15px; font-size: 14px;'>
                📧 <strong>Email:</strong> {institution.EmailSettings.SenderEmail}<br>
📞 <strong>Phone:</strong> {institution.ContactInfo?.Phone ?? "+260-XXX-XXXX"}<br>
🌐 <strong>Website:</strong> {institution.ContactInfo?.Website ?? "www.institution.edu"}<br>
📍 <strong>Address:</strong> {institution.ContactInfo?.Address ?? "Campus Address"}
            </div>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateWaitlistEmailHtml(string applicantName, string programmeName, string schoolName, DateTime academicYearStart)
    {

        var institution = _institutionConfig.GetCurrentInstitution();
        string logoUrl = $"{institution.ContactInfo?.Website?.TrimEnd('/')}{institution.LogoPath}"; // Add your logo URL here
        string waitlistDeadline = academicYearStart.AddDays(-30).ToString("MMMM dd, yyyy"); // 30 days before academic year starts

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Application Waitlist Status</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            background-color: #f8f9fa;
        }}
        .email-container {{
            background-color: #ffffff;
            border-radius: 10px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #f39c12 0%, #e67e22 100%);
            color: white;
            padding: 30px 20px;
            text-align: center;
        }}
        .logo {{
            max-width: 150px;
            height: auto;
            margin-bottom: 15px;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 300;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .status-message {{
            font-size: 18px;
            color: #2c3e50;
            margin-bottom: 25px;
            text-align: center;
        }}
        .waitlist-info {{
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .timeline-box {{
            background-color: #d1ecf1;
            border: 1px solid #bee5eb;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .footer {{
            background-color: #e67e22;
            color: white;
            padding: 25px 30px;
            text-align: center;
        }}
        .highlight {{
            color: #f39c12;
            font-weight: 600;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
<img src='{logoUrl}' alt='{institution.Name} Logo' class='logo'>
            <h1>You've Been Waitlisted!</h1>
        </div>
        
        <div class='content'>
            <div class='status-message'>
                Dear <strong>{applicantName}</strong>,
            </div>
            
            <p>Thank you for your application to the <strong>{programmeName}</strong> programme in the <strong>{schoolName}</strong> at {institution.Name}.</p>
            
            <p>We're pleased to inform you that your application has been <strong class='highlight'>placed on our waitlist</strong>. This means you are a qualified candidate, and we may be able to offer you admission if space becomes available.</p>
            
            <div class='waitlist-info'>
                <h3 style='color: #856404; margin-top: 0;'>📋 What Does This Mean?</h3>
                <ul style='color: #856404; margin-bottom: 0;'>
                    <li><strong>You're qualified:</strong> Your application meets our admission requirements</li>
                    <li><strong>Space dependent:</strong> Admission depends on available spots in the programme</li>
                    <li><strong>Stay informed:</strong> We'll notify you if your status changes</li>
                    <li><strong>No action needed:</strong> Your application remains active on our waitlist</li>
                </ul>
            </div>
            
            <div class='timeline-box'>
                <h3 style='color: #0c5460; margin-top: 0;'>⏰ Important Timeline</h3>
                <p style='color: #0c5460;'>
                    <strong>Waitlist Period:</strong> Until <strong>{waitlistDeadline}</strong><br>
                    <strong>Final Notifications:</strong> You will receive a final decision before the academic year begins on <strong>{academicYearStart:MMMM dd, yyyy}</strong>
                </p>
                <p style='color: #0c5460; margin-bottom: 0;'>
                    <small><em>If space becomes available, we will contact you immediately with further instructions.</em></small>
                </p>
            </div>
            
            <p><strong>What You Should Do:</strong></p>
            <ol>
                <li>Keep your contact information updated with us</li>
                <li>Consider applying to other programmes or institutions as backup options</li>
                <li>Monitor your email regularly for updates</li>
                <li>Feel free to contact us if you have any questions</li>
            </ol>
            
            <p style='text-align: center; margin-top: 30px;'>
                <strong>Thank you for your patience and continued interest in {institution.Name}!</strong>
            </p>
        </div>
        
        <div class='footer'>
<h4 style='margin: 0 0 10px 0;'>{institution.Name}</h4>
            <div style='margin-top: 15px; font-size: 14px;'>
               📧 <strong>Email:</strong> {institution.EmailSettings.SenderEmail}<br>
📞 <strong>Phone:</strong> {institution.ContactInfo?.Phone ?? "+260-XXX-XXXX"}<br>
🌐 <strong>Website:</strong> {institution.ContactInfo?.Website ?? "www.institution.edu"}<br>
📍 <strong>Address:</strong> {institution.ContactInfo?.Address ?? "Campus Address"}
            </div>
        </div>
    </div>
</body>
</html>";
    }

    // Add this method to your EmailService class
    public async Task<bool> SendUserCreationEmailAsync(string fullName, string email, string role, string password, string loginUrl = "https://ecampus.edenuniversity.edu.zm/Account/Login")
    {
        try
        {

            var institution = _institutionConfig.GetCurrentInstitution();
            var institutionName = _institutionConfig.GetInstitutionName();
            loginUrl =  institution.ContactInfo?.Website ?? "Website";
            string subject = $"Welcome to {institutionName} - Your Account Has Been Created";
            string emailBody = GenerateUserCreationEmailHtml(fullName, email, role, password, loginUrl);

            return await SendEmailAsync(email, subject, emailBody, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"User creation email failed: {ex.Message}");
            return false;
        }
    }

    private string GenerateUserCreationEmailHtml(string fullName, string email, string role, string password, string loginUrl)
    {

        var institution = _institutionConfig.GetCurrentInstitution();
        string logoUrl = $"{institution.ContactInfo?.Website?.TrimEnd('/')}{institution.LogoPath}";

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Welcome to {institution.Name}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            background-color: #f8f9fa;
        }}
        .email-container {{
            background-color: #ffffff;
            border-radius: 10px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);
            color: white;
            padding: 30px 20px;
            text-align: center;
        }}
        .logo {{
            max-width: 150px;
            height: auto;
            margin-bottom: 15px;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 300;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .welcome-message {{
            font-size: 18px;
            color: #2c3e50;
            margin-bottom: 25px;
            text-align: center;
        }}
        .account-details {{
            background-color: #e8f4fd;
            border: 1px solid #bee5eb;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .detail-row {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 10px;
            padding: 8px 0;
            border-bottom: 1px solid #b8daff;
        }}
        .detail-row:last-child {{
            border-bottom: none;
        }}
        .detail-label {{
            font-weight: 600;
            color: #0c5460;
            flex: 1;
        }}
        .detail-value {{
            flex: 2;
            text-align: right;
            color: #0c5460;
        }}
        .login-instructions {{
            background-color: #d4edda;
            border: 1px solid #c3e6cb;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .security-notice {{
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .login-button {{
            background-color: #3498db;
            color: white;
            padding: 12px 30px;
            text-decoration: none;
            border-radius: 5px;
            display: inline-block;
            font-weight: 600;
            margin: 15px 0;
        }}
        .footer {{
            background-color: #2c3e50;
            color: white;
            padding: 25px 30px;
            text-align: center;
        }}
        .highlight {{
            color: #3498db;
            font-weight: 600;
        }}
        .password {{
            background-color: #f8f9fa;
            border: 2px dashed #6c757d;
            padding: 10px;
            font-family: 'Courier New', monospace;
            font-size: 16px;
            text-align: center;
            margin: 10px 0;
            border-radius: 5px;
        }}
        @media (max-width: 600px) {{
            .content {{
                padding: 20px;
            }}
            .detail-row {{
                flex-direction: column;
            }}
            .detail-value {{
                text-align: left;
                margin-top: 5px;
            }}
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
<img src='{logoUrl}' alt='{institution.Name} Logo' class='logo'>
            <h1>Welcome to {institution.Name}!</h1>
        </div>
        
        <div class='content'>
            <div class='welcome-message'>
                Dear <strong>{fullName}</strong>,<br>
                Your account has been successfully created in the {institution.Name} system.
            </div>
            
            <p>We're pleased to inform you that an account has been created for you with <strong>{role}</strong> access privileges. You can now access the {institution.Name} portal using the credentials provided below.</p>
            
            <div class='account-details'>
                <h3 style='color: #0c5460; margin-top: 0;'>🔐 Your Account Details</h3>
                <div class='detail-row'>
                    <span class='detail-label'>Full Name:</span>
                    <span class='detail-value'>{fullName}</span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>Email/Username:</span>
                    <span class='detail-value highlight'>{email}</span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>Role:</span>
                    <span class='detail-value'>{role}</span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>Temporary Password:</span>
                    <span class='detail-value'>
                        <div class='password'>{password}</div>
                    </span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>Account Created:</span>
                    <span class='detail-value'>{DateTime.Now:MMMM dd, yyyy}</span>
                </div>
            </div>
            
            <div class='login-instructions'>
                <h3 style='color: #155724; margin-top: 0;'>🚀 How to Access Your Account</h3>
                <ol style='color: #155724; margin-bottom: 15px;'>
                    <li>Click the login button below or visit the {institution.Name} portal</li>
                    <li>Enter your email address as the username: <strong>{email}</strong></li>
                    <li>Use the temporary password provided above</li>
                    <li>You will be prompted to change your password on first login</li>
                </ol>
                <div style='text-align: center;'>
                    <a href='{loginUrl}' class='login-button'>Access Your Account</a>
                </div>
            </div>
            
            <div class='security-notice'>
                <h3 style='color: #856404; margin-top: 0;'>🔒 Important Security Information</h3>
                <ul style='color: #856404; margin-bottom: 0;'>
                    <li><strong>Change your password:</strong> Please change your temporary password immediately after logging in</li>
                    <li><strong>Keep credentials secure:</strong> Never share your login details with anyone</li>
                    <li><strong>Secure connection:</strong> Always ensure you're on the official {institution.Name} website</li>
                    <li><strong>Contact support:</strong> If you experience any issues, contact our IT support team</li>
                </ul>
            </div>
            
            <p><strong>Need Help?</strong></p>
            <p>If you have any questions about your account or need assistance logging in, please don't hesitate to contact our support team.</p>
            
            <p style='text-align: center; margin-top: 30px;'>
                <strong>Welcome to the {institution.Name} family!</strong><br>
                We're excited to have you on board.
            </p>
        </div>
        
        <div class='footer'>
            <h4 style='margin: 0 0 10px 0;'>{institution.Name}</h4>
            <div style='margin-top: 15px; font-size: 14px;'>
               📧 <strong>Email:</strong> {institution.EmailSettings.SenderEmail}<br>
📞 <strong>Phone:</strong> {institution.ContactInfo?.Phone ?? "+260-XXX-XXXX"}<br>
🌐 <strong>Website:</strong> {institution.ContactInfo?.Website ?? "www.institution.edu"}<br>
📍 <strong>Address:</strong> {institution.ContactInfo?.Address ?? "Campus Address"}
                🔗 <strong>Portal:</strong> <a href='{loginUrl}' style='color: #74b9ff;'>Login Here</a>
            </div>
            <p style='margin-top: 15px; font-size: 12px; opacity: 0.8;'>
                This is an automated message. Please do not reply to this email.<br>
                For support, contact us at {institution.EmailSettings.SenderEmail}
            </p>
        </div>
    </div>
</body>
</html>";
    }

    public async Task<bool> SendPasswordResetEmailAsync(string fullName, string email, string newPassword, string loginUrl = "https://ecampus.edenuniversity.edu.zm/Account/Login")
    {
        try
        {

            var institution = _institutionConfig.GetCurrentInstitution();
            var institutionName = _institutionConfig.GetInstitutionName();
            loginUrl = institution.ContactInfo?.Website ?? "Website";
            string subject = $"Password Reset - {institutionName}";
            string emailBody = GeneratePasswordResetEmailHtml(fullName, email, newPassword, loginUrl);

            return await SendEmailAsync(email, subject, emailBody, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Password reset email failed: {ex.Message}");
            return false;
        }
    }

    private string GeneratePasswordResetEmailHtml(string fullName, string email, string newPassword, string loginUrl)
    {

        var institution = _institutionConfig.GetCurrentInstitution();
        string logoUrl = $"{institution.ContactInfo?.Website?.TrimEnd('/')}{institution.LogoPath}";

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Password Reset - {institution.Name}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            background-color: #f8f9fa;
        }}
        .email-container {{
            background-color: #ffffff;
            border-radius: 10px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
            color: white;
            padding: 30px 20px;
            text-align: center;
        }}
        .logo {{
            max-width: 150px;
            height: auto;
            margin-bottom: 15px;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 300;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .alert-message {{
            font-size: 18px;
            color: #2c3e50;
            margin-bottom: 25px;
            text-align: center;
        }}
        .password-details {{
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .detail-row {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 10px;
            padding: 8px 0;
            border-bottom: 1px solid #ffeaa7;
        }}
        .detail-row:last-child {{
            border-bottom: none;
        }}
        .detail-label {{
            font-weight: 600;
            color: #856404;
            flex: 1;
        }}
        .detail-value {{
            flex: 2;
            text-align: right;
            color: #856404;
        }}
        .password-display {{
            background-color: #f8f9fa;
            border: 2px dashed #6c757d;
            padding: 15px;
            font-family: 'Courier New', monospace;
            font-size: 18px;
            text-align: center;
            margin: 15px 0;
            border-radius: 5px;
            letter-spacing: 1px;
            font-weight: bold;
        }}
        .security-notice {{
            background-color: #f8d7da;
            border: 1px solid #f5c6cb;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .login-instructions {{
            background-color: #d1ecf1;
            border: 1px solid #bee5eb;
            border-radius: 5px;
            padding: 20px;
            margin: 25px 0;
        }}
        .login-button {{
            background-color: #e74c3c;
            color: white;
            padding: 12px 30px;
            text-decoration: none;
            border-radius: 5px;
            display: inline-block;
            font-weight: 600;
            margin: 15px 0;
        }}
        .footer {{
            background-color: #2c3e50;
            color: white;
            padding: 25px 30px;
            text-align: center;
        }}
        .highlight {{
            color: #e74c3c;
            font-weight: 600;
        }}
        @media (max-width: 600px) {{
            .content {{
                padding: 20px;
            }}
            .detail-row {{
                flex-direction: column;
            }}
            .detail-value {{
                text-align: left;
                margin-top: 5px;
            }}
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <img src='{logoUrl}' alt='{institution.Name} Logo' class='logo'>
            <h1>🔐 Password Reset</h1>
        </div>
        
        <div class='content'>
            <div class='alert-message'>
                Dear <strong>{fullName}</strong>,<br>
                Your password has been successfully reset as requested.
            </div>
            
            <p>We received a request to reset the password for your {institution.Name} account. Your password has been changed to a new secure password.</p>
            
            <div class='password-details'>
                <h3 style='color: #856404; margin-top: 0;'>🔑 Your New Login Credentials</h3>
                <div class='detail-row'>
                    <span class='detail-label'>Email/Username:</span>
                    <span class='detail-value highlight'>{email}</span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>New Password:</span>
                    <span class='detail-value'>
                        <div class='password-display'>{newPassword}</div>
                    </span>
                </div>
                <div class='detail-row'>
                    <span class='detail-label'>Reset Date:</span>
                    <span class='detail-value'>{DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}</span>
                </div>
            </div>
            
            <div class='security-notice'>
                <h3 style='color: #721c24; margin-top: 0;'>⚠️ Important Security Information</h3>
                <ul style='color: #721c24; margin-bottom: 0;'>
                    <li><strong>Change this password immediately:</strong> This is a temporary password. Please change it after logging in.</li>
                    <li><strong>Account security:</strong> All existing login sessions have been invalidated for security.</li>
                    <li><strong>Keep it secure:</strong> Do not share this password with anyone.</li>
                    <li><strong>Not you?</strong> If you didn't request this reset, contact support immediately.</li>
                </ul>
            </div>
            
            <div class='login-instructions'>
                <h3 style='color: #0c5460; margin-top: 0;'>🚀 How to Access Your Account</h3>
                <ol style='color: #0c5460; margin-bottom: 15px;'>
                    <li>Click the login button below or visit the {institution.Name} portal</li>
                    <li>Enter your email address: <strong>{email}</strong></li>
                    <li>Use the new password provided above</li>
                    <li><strong>Important:</strong> Change your password immediately after logging in</li>
                </ol>
                <div style='text-align: center;'>
                    <a href='{loginUrl}' class='login-button'>Login to Your Account</a>
                </div>
            </div>
            
            <p><strong>Password Requirements for New Password:</strong></p>
            <ul style='font-size: 14px; color: #6c757d;'>
                <li>At least 8 characters long</li>
                <li>Include uppercase and lowercase letters</li>
                <li>Include at least one number</li>
                <li>Include at least one special character</li>
            </ul>
            
            <p style='text-align: center; margin-top: 30px;'>
                <strong>Need Help?</strong><br>
                If you have any questions or need assistance, please contact our support team.
            </p>
        </div>
        
        <div class='footer'>
            <h4 style='margin: 0 0 10px 0;'>{institution.Name}</h4>
            <div style='margin-top: 15px; font-size: 14px;'>
                📧 <strong>Email:</strong> {institution.EmailSettings.SenderEmail}<br>
                📞 <strong>Phone:</strong> {institution.ContactInfo?.Phone ?? "+260-XXX-XXXX"}<br>
                🌐 <strong>Website:</strong> {institution.ContactInfo?.Website ?? "www.institution.edu"}<br>
                📍 <strong>Address:</strong> {institution.ContactInfo?.Address ?? "Campus Address"}
                🔗 <strong>Portal:</strong> <a href='{loginUrl}' style='color: #74b9ff;'>Login Here</a>
            </div>
            <p style='margin-top: 15px; font-size: 12px; opacity: 0.8;'>
                This is an automated message. Please do not reply to this email.<br>
                For support, contact us at {institution.EmailSettings.SenderEmail}
            </p>
        </div>
    </div>
</body>
</html>";
    }

    Task<bool> IEmailService.NotifyApprovalActionAsync(WorkflowInstance wi, WorkflowApproval wa)
    {
        //throw new NotImplementedException();
        return Task.FromResult(true);
    }

    Task<bool> IEmailService.NotifyApprovalRequestAsync(WorkflowInstance wi, WorkflowApproval wa)
    {
        //throw new NotImplementedException();
        return Task.FromResult(true);
    }

    Task<bool> IEmailService.NotifyWorkflowRejectionAsync(WorkflowInstance wi, WorkflowApproval wa)
    {
        //throw new NotImplementedException();
        return Task.FromResult(true);
    }

    Task<bool> IEmailService.NotifyWorkflowCompletionAsync(WorkflowInstance wi)
    {
        //throw new NotImplementedException();
        return Task.FromResult(true);
    }

    Task<bool> IEmailService.NotifyDelegationAsync(int workflowInstanceId, string fromApproverId, string toApproverId, string reason)
    {
        //throw new NotImplementedException();
        return Task.FromResult(true);
    }
}
