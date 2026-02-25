using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIS.Models;
using SIS.Models.Payments;
using SIS.Data;

namespace SIS.Services.Payment
{
    public class TinggExpressCheckoutService
    {
        private readonly HttpClient _httpClient;
        private readonly TinggSettings _settings;
        private readonly TinggAuthService _authService;
        private readonly ILogger<TinggExpressCheckoutService> _logger;
        private readonly ApplicationDbContext _db;

        public TinggExpressCheckoutService(
            HttpClient httpClient,
            IOptions<TinggSettings> options,
            TinggAuthService authService,
            ILogger<TinggExpressCheckoutService> logger,
            ApplicationDbContext db)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _authService = authService;
            _logger = logger;
            _db = db;
        }

        public async Task<string> CreateExpressCheckoutUrlAsync(
            string fullName,
            string phone,
            string merchantTransactionId,
            decimal amount,
            int? studentID,
            int? applicantID,
            string accountNumber)
        {
            _logger.LogInformation("Starting CreateExpressCheckoutUrlAsync...");

            var token = await _authService.GetTokenAsync();
            _logger.LogInformation("Obtained Auth Token: {Token}", token);

            fullName = fullName?.Trim() ?? "";
            var names = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = names.Length > 0 ? names[0] : "N/A";
            var lastName = names.Length > 1 ? names[1] : firstName;

            var payload = new
            {
                customer_first_name = firstName,
                customer_last_name = lastName,
                msisdn = phone,
                account_number = phone,
                request_amount = amount,
                currency_code = "ZMW",
                external_reference = Guid.NewGuid().ToString(),
                merchant_transaction_id = merchantTransactionId,
                callback_url = _settings.CallbackUrl,
                success_redirect_url = _settings.SuccessUrl,
                fail_redirect_url = _settings.FailUrl,
                service_code = _settings.ServiceCode,
                country_code = "ZMB",
                language_code = "en",
                request_description = "Payment for service XYZ"
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var jsonPayload = JsonSerializer.Serialize(payload, jsonOptions);
            _logger.LogInformation("Outgoing request payload: {Payload}", jsonPayload);

            var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.tingg.africa/v3/checkout-api/checkout/request")
            {
                Headers =
                {
                    Authorization = new AuthenticationHeaderValue("Bearer", token)
                },
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            requestMessage.Headers.Add("apiKey", _settings.ApiKey);

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Response Status Code: {StatusCode}", response.StatusCode);
                _logger.LogInformation("Response Content: {ResponseContent}", responseContent);

                using var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;

                // Check for Tingg fault response
                if (root.TryGetProperty("fault", out var fault))
                {
                    var faultString = fault.GetProperty("faultstring").GetString();
                    var errorCode = fault.GetProperty("detail").GetProperty("errorcode").GetString();

                    _logger.LogError("Tingg API Fault: {FaultString} - {ErrorCode}", faultString, errorCode);
                    throw new Exception($"Tingg API Fault: {faultString} - {errorCode}");
                }

                if (root.TryGetProperty("status", out var statusElement) &&
                    statusElement.TryGetProperty("status_code", out var statusCodeElement) &&
                    statusCodeElement.GetInt32() != 200)
                {
                    var errorDescription = statusElement.TryGetProperty("status_description", out var desc)
                        ? desc.GetString()
                        : "Unknown error";

                    _logger.LogError("Tingg API returned an error: {Code} - {Description}", statusCodeElement.GetInt32(), errorDescription);
                    throw new Exception($"Tingg API returned error: {statusCodeElement.GetInt32()} - {errorDescription}");
                }

                string? checkoutUrl = null;

                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("checkoutUrl", out var longUrl) &&
                    !string.IsNullOrEmpty(longUrl.GetString()))
                {
                    checkoutUrl = longUrl.GetString();
                }
                else if (root.TryGetProperty("results", out var results) &&
                         results.TryGetProperty("short_url", out var shortUrl) &&
                         !string.IsNullOrEmpty(shortUrl.GetString()))
                {
                    checkoutUrl = shortUrl.GetString();
                }

                if (checkoutUrl == null)
                {
                    _logger.LogError("Response JSON did not contain expected checkout URL.");
                    throw new Exception("Invalid response format: missing checkout URL.");
                }

                // Save transaction
                var transaction = new OnlinePayments
                {
                    MerchantTransactionId = merchantTransactionId,
                    FullName = fullName,
                    Phone = phone,
                    StudentId = studentID ?? 0,
                    ApplicantId = applicantID,
                    Msisdn = phone,
                    AccountNumber = accountNumber,
                    Amount = amount,
                    CurrencyCode = "ZMW",
                    Status = "PENDING",
                    PaymentMethod = "Mobile Money",
                    RequestPayload = jsonPayload,
                    ResponsePayload = responseContent,
                    CheckoutUrl = checkoutUrl,
                    CreatedAt = DateTime.Now
                };

                _db.OnlinePayments.Add(transaction);
                await _db.SaveChangesAsync();

                return checkoutUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating express checkout URL.");
                throw new Exception("Failed to create checkout URL. Please try again.", ex);
            }
        }
    }
}
