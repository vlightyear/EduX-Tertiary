using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SIS.Services.Zoom
{
    public interface IZoomWebSdkService
    {
         (string SdkKey, string Signature) GenerateSignature(string meetingNumber, int role);
        string GenerateZoomWebConfig(string meetingNumber, string userName, int role, string password = null);
    }

    public class ZoomWebSdkService : IZoomWebSdkService
    {
        private readonly IConfiguration _configuration;
        private readonly string _meetingWebSdkClientId;
        private readonly string _meetingWebSdkClientSecret;

        public ZoomWebSdkService(IConfiguration configuration)
        {
            _configuration = configuration;
            _meetingWebSdkClientId = _configuration["ZoomMeetingWebSdk:ClientId"] ?? "";
            _meetingWebSdkClientSecret = _configuration["ZoomMeetingWebSdk:ClientSecret"] ?? "";
        }

        public (string SdkKey,string Signature )GenerateSignature(string meetingNumber, int role)
        {
            //// The SDK Key from your Zoom JWT App
            //string sdkKey = _sdkKey;
            //// The SDK Secret from your Zoom JWT App
            //string sdkSecret = _sdkSecret;
            //// The meeting number        


            var iat = DateTimeOffset.UtcNow.AddSeconds(-30).ToUnixTimeSeconds();
            var exp = iat + 60 * 60 * 48;
           

            // Define payload
            var payload = new Dictionary<string, object>
            {
                { "sdkKey", _meetingWebSdkClientId },
                { "mn", meetingNumber },
                { "role", role},
                { "iat", iat },
                { "exp", exp },
                { "tokenExp", exp }
                // { "video_webrtc_mode", 0 }
            };

            string webSignature = CreateJwtToken(payload, _meetingWebSdkClientSecret);
            Console.WriteLine("WebSignature:"+ webSignature);

            return (_meetingWebSdkClientId, webSignature);

        }

        public string GenerateZoomWebConfig(string meetingNumber, string userName, int role, string password = null)
        {
            var config = new
            {
                sdkKey = _meetingWebSdkClientId,
                meetingNumber = meetingNumber,
                userName = userName,
                passWord = password,
                signature = GenerateSignature(meetingNumber, role),
                role = role
            };

            return JsonSerializer.Serialize(config);
        }


        string CreateJwtToken(Dictionary<string, object> payload, string secret)
        {
            // Create JWT header
            var header = new Dictionary<string, object>
                {
                    { "alg", "HS256" },
                    { "typ", "JWT" }
                };

            // Convert header and payload to Base64Url
            string encodedHeader = StringToBase64Url(JsonSerializer.Serialize(header));
            string encodedPayload = StringToBase64Url(JsonSerializer.Serialize(payload));

            // Create signature
            string dataToSign = $"{encodedHeader}.{encodedPayload}";
            string signature = ComputeHS256Signature(dataToSign, secret);

            // Combine to form JWT
            return $"{encodedHeader}.{encodedPayload}.{signature}";
        }

        string StringToBase64Url(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            return BytesToBase64Url(bytes);
        }

        string BytesToBase64Url(byte[] input)
        {
            string base64 = Convert.ToBase64String(input);
            // Make Base64 URL-safe
            base64 = base64.Replace('+', '-')
                           .Replace('/', '_')
                           .TrimEnd('=');
            return base64;
        }

        string ComputeHS256Signature(string input, string secret)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BytesToBase64Url(hash);
            }
        }



    }
}
