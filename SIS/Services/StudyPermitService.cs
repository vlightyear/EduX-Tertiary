using Microsoft.EntityFrameworkCore;
using SIS.Services.Emails;
using SIS.Data;
using SIS.Models.StudyPermits;

namespace SIS.Services
{
    public interface IStudyPermitService
    {
        Task<List<StudyPermit>> GetExpiringPermitsAsync(int daysBeforeExpiry);
        Task SendExpiryNotificationsAsync();
        Task UpdatePermitStatusesAsync();
        Task<StudyPermit> AddOrUpdatePermitAsync(StudyPermit permit);
        Task<StudyPermitConfig> GetConfigAsync();
        Task UpdateConfigAsync(StudyPermitConfig config);
    }

    public class StudyPermitService : IStudyPermitService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _notificationService;

        public StudyPermitService(ApplicationDbContext context, IEmailService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<List<StudyPermit>> GetExpiringPermitsAsync(int daysBeforeExpiry)
        {
            var cutoffDate = DateTime.Now.AddDays(daysBeforeExpiry);
            return await _context.StudyPermits
                .Include(p => p.Student)
                .Where(p => p.IsActive && p.ExpiryDate <= cutoffDate && p.ExpiryDate >= DateTime.Now)
                .ToListAsync();
        }

        public async Task SendExpiryNotificationsAsync()
        {
            var config = await _context.StudyPermitConfigs.FirstOrDefaultAsync()
                         ?? new StudyPermitConfig();
            var permits = await GetExpiringPermitsAsync(config.DaysBeforeExpiryReminder);

            foreach (var permit in permits)
            {
                if (permit.LastNotificationSent.HasValue &&
                    (DateTime.Now - permit.LastNotificationSent.Value).TotalDays < 7)
                    continue; // avoid duplicate weekly reminders

                string message = config.EmailTemplate ??
                    $"Dear {permit.Student.FullName}, your study permit (No. {permit.PermitNumber}) " +
                    $"will expire on {permit.ExpiryDate:dd MMM yyyy}. Please renew soon.";

                if (config.EnableEmailReminders)
                    await _notificationService.SendEmailAsync(permit.Student.Email, "Study Permit Expiry", message, true);

                if (config.EnableSmsReminders && !string.IsNullOrEmpty(permit.Student.Phone))
                    //await _notificationService.SendSmsAsync(permit.Student.Phone, message);

                permit.LastNotificationSent = DateTime.Now;

                _context.StudyPermitNotificationLogs.Add(new StudyPermitNotificationLog
                {
                    StudyPermitId = permit.Id,
                    NotificationDate = DateTime.Now,
                    NotificationType = "Reminder",
                    Message = message,
                    Success = true,
                    CreatedAt = DateTime.Now,
                    CreatedBy = string.Empty
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdatePermitStatusesAsync()
        {
            var permits = await _context.StudyPermits.ToListAsync();
            foreach (var permit in permits)
            {
                if (permit.ExpiryDate < DateTime.Now)
                    permit.Status = PermitStatus.Expired;
                else if ((permit.ExpiryDate - DateTime.Now).TotalDays <= 30)
                    permit.Status = PermitStatus.ExpiringSoon;
                else
                    permit.Status = PermitStatus.Valid;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<StudyPermit> AddOrUpdatePermitAsync(StudyPermit permit)
        {
            if (permit.Id == 0)
                _context.StudyPermits.Add(permit);
            else
                _context.StudyPermits.Update(permit);

            await _context.SaveChangesAsync();
            return permit;
        }

        public async Task<StudyPermitConfig> GetConfigAsync() =>
            await _context.StudyPermitConfigs.FirstOrDefaultAsync() ?? new StudyPermitConfig();

        public async Task UpdateConfigAsync(StudyPermitConfig config)
        {
            _context.StudyPermitConfigs.Update(config);
            await _context.SaveChangesAsync();
        }
    }

    public class StudyPermitBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public StudyPermitBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var studyPermitService = scope.ServiceProvider.GetRequiredService<IStudyPermitService>();

                await studyPermitService.UpdatePermitStatusesAsync();
                await studyPermitService.SendExpiryNotificationsAsync();

                // Run once a day
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
