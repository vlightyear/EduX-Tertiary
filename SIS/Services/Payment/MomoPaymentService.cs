using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SIS.Data;
using SIS.Models.Payments;

namespace SIS.Services.Payment
{
    public class MomoPaymentService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MomoPaymentService> _logger;

        public MomoPaymentService(IServiceScopeFactory serviceScopeFactory, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MomoPaymentService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<MomoPayment?> SavePaymentRequestAsync(MomoPayment payment)
        {
            _logger.LogInformation("Initiating payment request for ExternalId: {ExternalId}", payment.ExternalId);

            var apiBaseUrl = _configuration["Lipila:ApiBaseUrl"];
            var apiKey = _configuration["Lipila:ApiKey"];

            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existing = await context.MomoPayments
                .FirstOrDefaultAsync(p => p.ExternalId == payment.ExternalId);

            if (existing != null)
            {
                _logger.LogWarning("Duplicate payment for ExternalId: {ExternalId}", payment.ExternalId);
                return existing;
            }

            var payload = new
            {
                currency = payment.Currency,
                amount = payment.Amount,
                accountNumber = payment.AccountNumber,
                fullName = payment.FullName,
                phoneNumber = payment.PhoneNumber,
                email = payment.Email,
                externalId = payment.ExternalId,
                narration = payment.Narration
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBaseUrl}/transactions/mobile-money")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            HttpResponseMessage response;
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                response = await httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call Lipila API for payment request.");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Lipila API returned failure status: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<InitialPaymentResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null || string.IsNullOrWhiteSpace(result.TransactionId))
            {
                _logger.LogError("Invalid response from Lipila: {Content}", content);
                return null;
            }

            payment.TransactionId = result.TransactionId;
            payment.Status = result.Status;
            payment.CreatedAt = DateTime.Now;

            context.MomoPayments.Add(payment);
            await context.SaveChangesAsync();

            _logger.LogInformation("Payment request saved with TransactionId: {TransactionId}", payment.TransactionId);

            return payment;
        }

        public async Task CheckAndUpdateStatusAsync(string transactionId)
        {
            _logger.LogInformation("Starting verification for TransactionId: {TransactionId}", transactionId);

            var apiBaseUrl = _configuration["Lipila:ApiBaseUrl"];
            var apiKey = _configuration["Lipila:ApiKey"];

            var startTime = DateTime.Now;
            TimeSpan timeout = TimeSpan.FromMinutes(3);
            TimeSpan pollingInterval = TimeSpan.FromSeconds(15);

            while (DateTime.Now - startTime < timeout)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var payment = await context.MomoPayments.FirstOrDefaultAsync(p => p.TransactionId == transactionId);
                if (payment == null)
                {
                    _logger.LogWarning("TransactionId not found in database: {TransactionId}", transactionId);
                    return;
                }

                var url = $"{apiBaseUrl}/transactions/status?transactionId={transactionId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var response = await httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var statusResponse = JsonSerializer.Deserialize<MomoStatusResponse>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (statusResponse == null)
                    {
                        _logger.LogWarning("Invalid status response for TransactionId: {TransactionId}", transactionId);
                        return;
                    }

                    if (!string.Equals(payment.Status, statusResponse.Status, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Status changed for TransactionId {TransactionId}: {OldStatus} -> {NewStatus}",
                            transactionId, payment.Status, statusResponse.Status);

                        payment.Status = statusResponse.Status;
                        payment.PaymentMethod = statusResponse.PaymentType;
                        payment.UpdatedAt = DateTime.Now;

                        context.MomoPayments.Update(payment);
                        await context.SaveChangesAsync();

                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling status API for TransactionId: {TransactionId}", transactionId);
                }

                await Task.Delay(pollingInterval);
            }

            _logger.LogInformation("Status did not change for TransactionId {TransactionId} after 3 minutes.", transactionId);
        }

        public async Task<MomoPayment?> GetPaymentByTransactionIdAsync(string transactionId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.MomoPayments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TransactionId == transactionId);
        }

        private class InitialPaymentResponse
        {
            public decimal Amount { get; set; }
            public string TransactionId { get; set; } = string.Empty;
            public string ExternalId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Message { get; set; }
        }

        private class MomoStatusResponse
        {
            public string Status { get; set; } = string.Empty;
            public string PaymentType { get; set; } = string.Empty;
        }
    }
}