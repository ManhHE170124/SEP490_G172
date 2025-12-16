using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Infrastructure
{
    public class PayOSResponse
    {
        public string Code { get; set; } = "";
        public string Desc { get; set; } = "";
        public PayOSData? Data { get; set; }
    }

    public class PayOSData
    {
        public string? CheckoutUrl { get; set; }
    }

    public class PayOSService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PayOSService> _logger;
        private readonly IConfiguration _config;
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

        public PayOSService(
            HttpClient httpClient,
            ILogger<PayOSService> logger,
            IConfiguration config,
            IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config;
            _dbFactory = dbFactory;
        }

        private async Task<(string clientId, string apiKey, string checksumKey, string endpoint)> GetSettingsAsync()
        {
            // DB-first: lấy từ PaymentGateway Name="PayOS"
            await using var db = await _dbFactory.CreateDbContextAsync();

            var gw = await db.PaymentGateways
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Name == "PayOS" &&
                    (x.IsActive == null || x.IsActive == true));

            var clientId = gw?.ClientId?.Trim();
            var apiKey = gw?.ApiKey?.Trim();
            var checksumKey = gw?.ChecksumKey?.Trim();

            // Fallback appsettings
            if (string.IsNullOrWhiteSpace(clientId))
                clientId = _config["PayOS:ClientId"]?.Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = _config["PayOS:ApiKey"]?.Trim();

            if (string.IsNullOrWhiteSpace(checksumKey))
                checksumKey = _config["PayOS:ChecksumKey"]?.Trim();

            var endpoint = _config["PayOS:Endpoint"]?.Trim() ?? "";

            return (clientId ?? "", apiKey ?? "", checksumKey ?? "", endpoint);
        }

        private static string ComputeSignature(string rawSignature, string checksumKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawSignature));
            return BitConverter.ToString(signatureBytes).Replace("-", "").ToLowerInvariant();
        }

        public async Task<string> CreatePayment(
            int orderCode,
            int amount,
            string description,
            string returnUrl,
            string cancelUrl,
            string buyerPhone,
            string buyerName,
            string buyerEmail)
        {
            var (clientId, apiKey, checksumKey, endpoint) = await GetSettingsAsync();

            if (string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(checksumKey) ||
                string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException(
                    "Thiếu cấu hình PayOS (ClientId/ApiKey/ChecksumKey/Endpoint). Hãy cập nhật trong WebConfig hoặc appsettings.");
            }

            // Raw signature theo format bạn đang dùng
            var rawSignature =
                $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
            _logger.LogInformation("PayOS rawSignature: {rawSignature}", rawSignature);

            var signature = ComputeSignature(rawSignature, checksumKey);

            var requestBody = new
            {
                orderCode,
                amount,
                description,
                returnUrl,
                cancelUrl,
                buyerName,
                buyerEmail,
                buyerPhone,
                signature
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonString = JsonSerializer.Serialize(requestBody, jsonOptions);
            _logger.LogInformation("PayOS request json: {json}", jsonString);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("x-client-id", clientId);
            request.Headers.Add("x-api-key", apiKey);
            request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PayOS response: {response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayOS HTTP error: {status} - {body}", (int)response.StatusCode, responseContent);
                throw new Exception("Lỗi HTTP từ PayOS: " + responseContent);
            }

            var payOSResponse = JsonSerializer.Deserialize<PayOSResponse>(responseContent, jsonOptions);

            if (payOSResponse == null || payOSResponse.Code != "00")
            {
                throw new Exception("Lỗi từ PayOS: " + responseContent);
            }

            return payOSResponse.Data?.CheckoutUrl
                   ?? throw new Exception("Không tìm thấy checkoutUrl trong phản hồi.");
        }
    }
}
