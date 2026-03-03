using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SIS.Models.Payments;

namespace SIS.Services
{
    public interface IPayBossService
    {
        Task<string> GetBearerTokenAsync();
        Task<PayBossMobileResponse> CollectMobileAsync(string phone, decimal amount, string narration, string txnId);
        Task<PayBossCardResponse> CollectCardAsync(StudentCardPaymentViewModel model, string narration, string txnId, string redirectUrl);
        Task<PayBossStatusResponse> GetStatusAsync(string txnId);
    }

    public class PayBossService : IPayBossService
    {
        private string BASE_URL = "https://services-prod.bgspayboss.com/api/v1";
        private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly ILogger<PayBossService> _logger;

        private string? _cachedToken;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        public PayBossService(IHttpClientFactory http, IConfiguration config, ILogger<PayBossService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;

            BASE_URL = _config["PayBoss:URL"];
        }

        // ── Step 1: Authentication ──────────────────────────────────────
        public async Task<string> GetBearerTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry.AddSeconds(-10))
                return _cachedToken;

            await _tokenLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry.AddSeconds(-10))
                    return _cachedToken;

                var username = _config["PayBoss:Username"]
                    ?? throw new InvalidOperationException("PayBoss:Username not configured.");
                var apiKey = _config["PayBoss:ApiKey"]
                    ?? throw new InvalidOperationException("PayBoss:ApiKey not configured.");

                var payload = JsonSerializer.Serialize(new { username, apikey = apiKey });

                using var client = _http.CreateClient("PayBoss");
                var req = new HttpRequestMessage(HttpMethod.Post, $"{BASE_URL}/transaction/collection/auth")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                var res = await client.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogError("PayBoss auth failed {Status}: {Body}", res.StatusCode, body);
                    throw new HttpRequestException($"PayBoss auth failed ({res.StatusCode}): {body}");
                }

                var auth = JsonSerializer.Deserialize<PayBossAuthResponse>(body, _jsonOpts)
                    ?? throw new InvalidOperationException("Null PayBoss auth response.");

                _cachedToken = auth.Token;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(auth.ExpiresIn);
                _logger.LogInformation("PayBoss token obtained. Expires in {Sec}s.", auth.ExpiresIn);
                return _cachedToken;
            }
            finally { _tokenLock.Release(); }
        }

        // ── Step 2A: Mobile Money ───────────────────────────────────────
        public async Task<PayBossMobileResponse> CollectMobileAsync(
            string phone, decimal amount, string narration, string txnId)
        {
            var token = await GetBearerTokenAsync();
            var payload = JsonSerializer.Serialize(new
            {
                phoneNumber = phone,
                amount = amount.ToString("F2"),
                narration,
                transactionID = txnId
            });

            using var client = _http.CreateClient("PayBoss");
            var req = new HttpRequestMessage(HttpMethod.Post, $"{BASE_URL}/transaction/collection")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            _logger.LogInformation("PayBoss mobile [{Id}] {Status}: {Body}", txnId, res.StatusCode, body);

            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"PayBoss mobile failed ({res.StatusCode}): {body}");

            return JsonSerializer.Deserialize<PayBossMobileResponse>(body, _jsonOpts)
                ?? throw new InvalidOperationException("Null PayBoss mobile response.");
        }

        // ── Step 2B: Card ───────────────────────────────────────────────
        public async Task<PayBossCardResponse> CollectCardAsync(
            StudentCardPaymentViewModel model, string narration, string txnId, string redirectUrl)
        {
            var token = await GetBearerTokenAsync();
            var payload = JsonSerializer.Serialize(new
            {
                phoneNumber = model.PhoneNumber,
                amount = model.Amount.ToString("F2"),
                narration,
                transactionID = txnId,
                firstname = model.FirstName,
                lastname = model.LastName,
                email = model.Email,
                address = model.Address,
                city = model.City,
                country = model.Country,
                postalCode = model.PostalCode,
                province = model.Province,
                redirectUrl,
                currency = "zmw"
            });

            using var client = _http.CreateClient("PayBoss");
            var req = new HttpRequestMessage(HttpMethod.Post, $"{BASE_URL}/transaction/collection/card")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            _logger.LogInformation("PayBoss card [{Id}] {Status}: {Body}", txnId, res.StatusCode, body);

            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"PayBoss card failed ({res.StatusCode}): {body}");

            return JsonSerializer.Deserialize<PayBossCardResponse>(body, _jsonOpts)
                ?? throw new InvalidOperationException("Null PayBoss card response.");
        }

        // ── Step 3: Status Query ────────────────────────────────────────
        public async Task<PayBossStatusResponse> GetStatusAsync(string txnId)
        {
            var token = await GetBearerTokenAsync();

            using var client = _http.CreateClient("PayBoss");
            var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"{BASE_URL}/transaction/collection/status/{Uri.EscapeDataString(txnId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"PayBoss status failed ({res.StatusCode}): {body}");

            return JsonSerializer.Deserialize<PayBossStatusResponse>(body, _jsonOpts)
                ?? throw new InvalidOperationException("Null PayBoss status response.");
        }
    }
}
