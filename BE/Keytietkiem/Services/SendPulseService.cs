using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Keytietkiem.Options;
using Keytietkiem.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Keytietkiem.Services;

public class SendPulseService : ISendPulseService
{
    private readonly HttpClient _httpClient;
    private readonly SendPulseConfig _config;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SendPulseService> _logger;

    private const string TokenCacheKey = "SendPulse_AccessToken";

    public SendPulseService(
        HttpClient httpClient,
        IOptions<SendPulseConfig> config,
        IMemoryCache memoryCache,
        ILogger<SendPulseService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string combinedHtmlBody)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Failed to retrieve Access Token from SendPulse.");
                return false;
            }

            // Reference: https://sendpulse.com/integrations/api/bulk-email
            // Endpoint: POST /smtp/emails
            var requestUrl = $"{_config.BaseUrl.TrimEnd('/')}/smtp/emails";

            // Encode HTML content to Base64
            var htmlBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(combinedHtmlBody));

            var emailData = new
            {
                email = new
                {
                    html = htmlBase64,
                    text = string.Empty,
                    subject = subject,
                    from = new
                    {
                        name = "Keytietkiem",
                        email = "no-reply@keytietkiem.com" // You might want to make this configurable
                    },
                    to = new[]
                    {
                        new
                        {
                            name = toEmail, // Or parse name if available
                            email = toEmail
                        }
                    }
                }
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(emailData), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error sending email via SendPulse. Status: {StatusCode}, Details: {Details}", response.StatusCode, errorContent);
                return false;
            }

            _logger.LogInformation("Email sent successfully to {Email} via SendPulse.", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending email to {Email} via SendPulse.", toEmail);
            return false;
        }
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        if (_memoryCache.TryGetValue(TokenCacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken;
        }

        try
        {
            // Endpoint: POST /oauth/access_token
            var requestUrl = $"{_config.BaseUrl.TrimEnd('/')}/oauth/access_token";

            var payload = new
            {
                grant_type = _config.GrantType,
                client_id = _config.ApiKey,
                client_secret = _config.ApiSecret
            };

            var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to authenticate with SendPulse. Status: {StatusCode}, Details: {Details}", response.StatusCode, errorContent);
                return null;
            }

            var authResponse = await response.Content.ReadFromJsonAsync<SendPulseAuthResponse>();
            if (authResponse == null || string.IsNullOrEmpty(authResponse.AccessToken))
            {
                _logger.LogError("Invalid authentication response from SendPulse.");
                return null;
            }

            // Cache the token. Usually expires in 1 hour (3600s).
            // We subtract a small buffer (e.g., 60s) to be safe.
            var expiresInSeconds = authResponse.ExpiresIn > 60 ? authResponse.ExpiresIn - 60 : authResponse.ExpiresIn;
            
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(expiresInSeconds));

            _memoryCache.Set(TokenCacheKey, authResponse.AccessToken, cacheOptions);

            return authResponse.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while retrieving SendPulse access token.");
            return null;
        }
    }

    private class SendPulseAuthResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }
    }
}
