using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIS.Data;
using SIS.Services;

namespace SIS.BackgroundServices
{
    public class PaymentStatusPollingService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PaymentStatusPollingService> _logger;

        private readonly TimeSpan _interval = TimeSpan.FromMinutes(2);

        public PaymentStatusPollingService(
            IServiceScopeFactory scopeFactory,
            ILogger<PaymentStatusPollingService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PaymentStatusPollingService started.");

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollPendingPaymentsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Unhandled error in PaymentStatusPollingService.");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("PaymentStatusPollingService stopped.");
        }

        private async Task PollPendingPaymentsAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payBoss = scope.ServiceProvider.GetRequiredService<IPayBossService>();
            var logger = _logger;

            var pendingPayments = await context.OnlinePayments
                .Where(p => p.Status == "Pending" && (p.PaymentMethod == "PayBoss Mobile" || p.PaymentMethod == "PayBoss Card"))
                .ToListAsync(stoppingToken);

            if (!pendingPayments.Any())
                return;

            logger.LogInformation(
                "PaymentStatusPoller: checking {Count} pending payment(s).", pendingPayments.Count);

            foreach (var payment in pendingPayments)
            {
                if (stoppingToken.IsCancellationRequested) break;

                if (string.IsNullOrWhiteSpace(payment.ReferenceNumber))
                    continue;

                if (payment.CreatedAt < DateTime.Now.AddHours(-24))
                {
                    payment.Status = "Expired";
                    logger.LogWarning(
                        "Payment {Ref} marked Expired (older than 24 h).", payment.ReferenceNumber);
                    continue;
                }

                try
                {
                    var status = await payBoss.GetStatusAsync(payment.ReferenceNumber);

                    if (string.Equals(status.Status, "successful", StringComparison.OrdinalIgnoreCase) || string.Equals(status.Status, "success", StringComparison.OrdinalIgnoreCase))
                    {
                        payment.Status = "Paid";
                        logger.LogInformation(
                            "Payment {Ref} → Paid (providerRef: {Ref2}).",
                            payment.ReferenceNumber, status.ServiceProviderRef);
                    }
                    else if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
                    {
                        payment.Status = "Failed";
                        logger.LogInformation(
                            "Payment {Ref} → Failed.", payment.ReferenceNumber);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error polling status for payment {Ref}.", payment.ReferenceNumber);
                }
            }

            await context.SaveChangesAsync(stoppingToken);
        }
    }
}