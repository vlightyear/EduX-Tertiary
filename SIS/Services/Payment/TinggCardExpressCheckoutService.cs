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
using Checkout.Service; // ✅ Ensure this is the correct namespace from Cellulant.CheckoutEncryption NuGet

namespace SIS.Services.Payment
{
    public class TinggCardExpressCheckoutService
    {
        private readonly HttpClient _httpClient;
        private readonly TinggSettings _settings;
        private readonly TinggAuthService _authService;
        private readonly ILogger<TinggCardExpressCheckoutService> _logger;
        private readonly ApplicationDbContext _db;

        public TinggCardExpressCheckoutService(
            HttpClient httpClient,
            IOptions<TinggSettings> options,
            TinggAuthService authService,
            ILogger<TinggCardExpressCheckoutService> logger,
            ApplicationDbContext db)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _authService = authService;
            _logger = logger;
            _db = db;
        }

        public async Task<string> CreateCardCheckoutUrlAsync(string fullName, string phone, string email, decimal amount, CardDetails cardDetails)
        {
            _logger.LogInformation("Starting Tingg Direct Card Checkout...");

            var token = await _authService.GetTokenAsync();
            _logger.LogInformation("Obtained Auth Token: {Token}", token);

            var names = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = names.Length > 0 ? names[0] : "N/A";
            var lastName = names.Length > 1 ? names[1] : firstName;

            var merchantTransactionId = Guid.NewGuid().ToString();
            var encryptedCardData = EncryptCardData(cardDetails);

            var payload = new
            {
                service_code = _settings.ServiceCode,
                country_code = "ZMB",
                threeDs = "true",
                billing_details = new
                {
                    address = new
                    {
                        country_code = "ZMB",
                        city = "Lusaka"
                    },
                    customer = new
                    {
                        first_name = firstName,
                        surname = lastName,
                        email_address = email,
                        mobile_number = phone
                    }
                },
                merchant_transaction_id = merchantTransactionId,
                payment_option_code = "ECO_CARD",
                source_Of_funds = encryptedCardData,
                locale = "en",
                order = new
                {
                    account_number = "DEFAULT_ACC",
                    amount = amount.ToString("F2"),
                    currency_code = "ZMW",
                    description = "Card payment for XYZ service"
                },
                extra_data = new
                {
                    checkout_request_id = Guid.NewGuid().ToString()
                },
                browser_details = new
                {
                    accept_header = "text/html",
                    screen_color_depth = "24",
                    language = "en-US",
                    screen_height = "768",
                    screen_width = "1366",
                    timezone = "-120",
                    java_enabled = "false",
                    javascript_enabled = "true",
                    ip_address = "127.0.0.1",
                    user_agent = "Mozilla/5.0"
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonPayload = JsonSerializer.Serialize(payload, jsonOptions);

            _logger.LogInformation("Sending request to Tingg Direct Card API...");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tingg.africa/v3/checkout-api/direct-card");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("apikey", _settings.ApiKey);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Tingg Response Status: {StatusCode}", response.StatusCode);
                _logger.LogInformation("Tingg Response Body: {ResponseContent}", responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Tingg returned non-200 status: {StatusCode}. Response: {Response}", response.StatusCode, responseContent);
                    throw new Exception($"Tingg API Error. Status: {response.StatusCode}. Body: {responseContent}");
                }

                using var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("status", out var statusElement) &&
                    statusElement.TryGetProperty("status_code", out var statusCodeElement) &&
                    statusCodeElement.GetInt32() != 200)
                {
                    var errorDescription = statusElement.TryGetProperty("status_description", out var desc)
                        ? desc.GetString()
                        : "Unknown error from Tingg";
                    _logger.LogError("Tingg API status error: {StatusCode} - {Description}", statusCodeElement.GetInt32(), errorDescription);
                    throw new Exception($"Tingg API Error: {statusCodeElement.GetInt32()} - {errorDescription}");
                }

                string? redirectUrl = null;
                if (root.TryGetProperty("results", out var results) &&
                    results.TryGetProperty("redirect_url", out var redirectElement))
                {
                    redirectUrl = redirectElement.GetString();
                }

                if (string.IsNullOrEmpty(redirectUrl))
                {
                    _logger.LogError("Tingg response missing redirect_url.");
                    throw new Exception("Missing redirect URL in Tingg response.");
                }

                // ✅ Save NON-sensitive transaction info (no card details)
                var transaction = new OnlinePayments
                {
                    MerchantTransactionId = merchantTransactionId,
                    FullName = fullName,
                    Phone = phone,
                    Msisdn = phone,
                    Email = email,
                    Amount = amount,
                    CurrencyCode = "ZMW",
                    Status = "PENDING",
                    PaymentMethod = "CARD",
                    CheckoutUrl = redirectUrl,
                    CreatedAt = DateTime.Now
                };

                _db.OnlinePayments.Add(transaction);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Card checkout saved successfully with redirect URL.");

                return redirectUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Tingg Card Direct Checkout process.");
                throw;
            }
        }

        private string EncryptCardData(CardDetails cardDetails)
        {
            _logger.LogInformation("Encrypting Card Data using CheckoutEncryption (Cellulant Package)...");

            var jsonCardData = JsonSerializer.Serialize(new
            {
                card = new
                {
                    nameOnCard = cardDetails.NameOnCard,
                    number = cardDetails.Number,
                    storedOnFile = "TO_BE_STORED",
                    cvv = cardDetails.CVV,
                    expiry = new
                    {
                        month = cardDetails.ExpiryMonth,
                        year = cardDetails.ExpiryYear
                    }
                }
            });

            var ivKey = _settings.IvKey;
            var secretKey = _settings.SecretKey;

            // ✅ Full Namespace Reference
            var encryption = new CheckoutEncryption(ivKey, secretKey);
            string encryptedPayload = encryption.encrypt(jsonCardData);

            _logger.LogInformation("Card data encryption complete.");

            return encryptedPayload;
        }
    }

    public class CardDetails
    {
        public string NameOnCard { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string CVV { get; set; } = string.Empty;
        public string ExpiryMonth { get; set; } = string.Empty;
        public string ExpiryYear { get; set; } = string.Empty;
    }
}
