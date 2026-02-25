using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIS.Models;

namespace SIS.Services.Payment
{
    public class TinggAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly TinggSettings _settings;
        private readonly ILogger<TinggAuthService> _logger;

        public TinggAuthService(
            HttpClient httpClient,
            IOptions<TinggSettings> options,
            ILogger<TinggAuthService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<string> GetTokenAsync()
        {
          var authUrl = $"https://api.tingg.africa/v1/oauth/token/request";

            var payload = new
            {
                client_id = _settings.ClientId,
                client_secret = _settings.ClientSecret,
                grant_type = "client_credentials"
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, authUrl);
            request.Headers.Add("apiKey", _settings.ApiKey);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                _logger.LogInformation("Sending Tingg token request to: {Url}", authUrl);
                _logger.LogInformation("Token request payload: {Payload}", jsonPayload);

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Tingg Token API Response Status: {StatusCode}", response.StatusCode);
                _logger.LogInformation("Tingg Token API Response Content: {Content}", responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Non-success response from Tingg Auth API: {StatusCode} - {Response}", response.StatusCode, responseContent);
                    throw new Exception($"Tingg Auth API returned error: {response.StatusCode}");
                }

                // Validate Content-Type
                if (!response.Content.Headers.ContentType?.MediaType?.Contains("json") ?? true)
                {
                    throw new Exception("Tingg Auth API returned non-JSON response (possible HTML error page).");
                }

                using var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("access_token", out var tokenElement))
                {
                    string? token = tokenElement.GetString();
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        _logger.LogInformation("Successfully retrieved Tingg access token.");
                        return token;
                    }
                }

                _logger.LogError("access_token not found in Tingg Auth API response: {Response}", responseContent);
                throw new Exception("access_token not found in response.");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse Tingg Auth API JSON response.");
                throw new Exception("Failed to parse JSON from Tingg Auth API.", jsonEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Tingg token request.");
                throw;
            }
        }
    }
}
