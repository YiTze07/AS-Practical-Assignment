using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AS_Practical_Assignment.Services
{
    public class RecaptchaService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public RecaptchaService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<bool> ValidateAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                string secretKey = _configuration["Google:ReCaptcha:SecretKey"];

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", secretKey),
                    new KeyValuePair<string, string>("response", token)
                });

                var response = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
                string result = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(result);
                bool success = doc.RootElement.GetProperty("success").GetBoolean();

                // Optional: Check score for v3 (0.0 to 1.0, higher is better)
                if (success && doc.RootElement.TryGetProperty("score", out var scoreElement))
                {
                    double score = scoreElement.GetDouble();
                    // Reject if score is too low (below 0.5 is likely a bot)
                    return score >= 0.5;
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"reCAPTCHA Error: {ex.Message}");
                // For production: return false to block on error
                // For testing: return true to allow through
                return false;
            }
        }
    }
}