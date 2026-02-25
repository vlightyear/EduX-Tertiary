using Microsoft.Extensions.DependencyInjection;
using SIS.Models.StudentApplication;
using SIS.Services.PDF;
using System.Threading.Channels;

namespace SIS.Services.Emails
{
    public class BackgroundEmailService : BackgroundService, IBackgroundEmailService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundEmailService> _logger;
        private readonly Channel<EmailTask> _emailQueue;

        public BackgroundEmailService(IServiceProvider serviceProvider, ILogger<BackgroundEmailService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Create a channel for email tasks
            var options = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _emailQueue = Channel.CreateBounded<EmailTask>(options);
        }

        public void QueueApplicationSubmissionEmail(string applicantName, string applicantEmail, string programmeName, string schoolName, string referenceNumber, decimal paymentAmount, string transactionReference)
        {
            var emailTask = new EmailTask
            {
                Type = EmailType.ApplicationSubmission,
                ApplicantName = applicantName,
                ApplicantEmail = applicantEmail,
                ProgrammeName = programmeName,
                SchoolName = schoolName,
                ReferenceNumber = referenceNumber,
                PaymentAmount = paymentAmount,
                TransactionReference = transactionReference
            };

            if (!_emailQueue.Writer.TryWrite(emailTask))
            {
                _logger.LogWarning("Failed to queue application submission email for {Email}", applicantEmail);
            }
        }

        public void QueueAdmissionEmail(Student student)
        {
            var emailTask = new EmailTask
            {
                Type = EmailType.Admission,
                Student = student
            };

            if (!_emailQueue.Writer.TryWrite(emailTask))
            {
                _logger.LogWarning("Failed to queue admission email for {Email}", student.Email);
            }
        }

        public void QueueRejectionEmail(string applicantName, string applicantEmail, string programmeName, string schoolName, string reason)
        {
            var emailTask = new EmailTask
            {
                Type = EmailType.Rejection,
                ApplicantName = applicantName,
                ApplicantEmail = applicantEmail,
                ProgrammeName = programmeName,
                SchoolName = schoolName,
                RejectionReason = reason
            };

            if (!_emailQueue.Writer.TryWrite(emailTask))
            {
                _logger.LogWarning("Failed to queue rejection email for {Email}", applicantEmail);
            }
        }

        public void QueueWaitlistEmail(string applicantName, string applicantEmail, string programmeName, string schoolName, DateTime academicYearStart)
        {
            var emailTask = new EmailTask
            {
                Type = EmailType.Waitlist,
                ApplicantName = applicantName,
                ApplicantEmail = applicantEmail,
                ProgrammeName = programmeName,
                SchoolName = schoolName,
                AcademicYearStart = academicYearStart
            };

            if (!_emailQueue.Writer.TryWrite(emailTask))
            {
                _logger.LogWarning("Failed to queue waitlist email for {Email}", applicantEmail);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var emailTask in _emailQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessEmailTask(emailTask);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing email task of type {EmailType}", emailTask.Type);
                }
            }
        }

        private async Task ProcessEmailTask(EmailTask emailTask)
        {
            using var scope = _serviceProvider.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var htmlPdfService = scope.ServiceProvider.GetRequiredService<IHtmlPdfService>();

            try
            {
                switch (emailTask.Type)
                {
                    case EmailType.ApplicationSubmission:
                        await emailService.SendApplicationSubmissionEmailAsync(
                            emailTask.ApplicantName,
                            emailTask.ApplicantEmail,
                            emailTask.ProgrammeName,
                            emailTask.SchoolName,
                            emailTask.ReferenceNumber,
                            emailTask.PaymentAmount,
                            emailTask.TransactionReference);
                        _logger.LogInformation("Successfully sent application submission email to {Email}", emailTask.ApplicantEmail);
                        break;

                    case EmailType.Admission:
                        var admissionLetterPdf = await htmlPdfService.GenerateAdmissionLetterAsync(emailTask.Student);
                        await emailService.SendAdmissionEmailAsync(emailTask.Student, admissionLetterPdf);
                        _logger.LogInformation("Successfully sent admission email to {Email}", emailTask.Student.Email);
                        break;

                    case EmailType.Rejection:
                        await emailService.SendRejectionEmailAsync(
                            emailTask.ApplicantName,
                            emailTask.ApplicantEmail,
                            emailTask.ProgrammeName,
                            emailTask.SchoolName,
                            emailTask.RejectionReason);
                        _logger.LogInformation("Successfully sent rejection email to {Email}", emailTask.ApplicantEmail);
                        break;

                    case EmailType.Waitlist:
                        await emailService.SendWaitlistEmailAsync(
                            emailTask.ApplicantName,
                            emailTask.ApplicantEmail,
                            emailTask.ProgrammeName,
                            emailTask.SchoolName,
                            emailTask.AcademicYearStart);
                        _logger.LogInformation("Successfully sent waitlist email to {Email}", emailTask.ApplicantEmail);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send {EmailType} email to {Email}", emailTask.Type, emailTask.ApplicantEmail ?? emailTask.Student?.Email);
            }
        }
    }

    // Supporting classes
    public class EmailTask
    {
        public EmailType Type { get; set; }
        public string ApplicantName { get; set; } = string.Empty;
        public string ApplicantEmail { get; set; } = string.Empty;
        public string ProgrammeName { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public decimal PaymentAmount { get; set; }
        public string TransactionReference { get; set; } = string.Empty;
        public string RejectionReason { get; set; } = string.Empty;
        public DateTime AcademicYearStart { get; set; }
        public Student Student { get; set; }
    }

    public enum EmailType
    {
        ApplicationSubmission,
        Admission,
        Rejection,
        Waitlist
    }
}