// PayOSService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
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
        public string? PaymentLinkId { get; set; }
    }

    public class PayOSCreatePaymentResult
    {
        public string CheckoutUrl { get; set; } = "";
        public string PaymentLinkId { get; set; } = "";
    }

    public class PayOSService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PayOSService> _logger;
        private readonly IConfiguration _config;
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

        // ✅ serialize ổn định cho signature (arrays/objects)
        private static readonly JsonSerializerOptions SignatureJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

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

        /// <summary>
        /// DB-first: lấy PayOS keys từ PaymentGateway(Name="PayOS", IsActive=true),
        /// fallback appsettings nếu DB chưa có.
        /// </summary>
        private async Task<(string clientId, string apiKey, string checksumKey, string endpoint)> GetSettingsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var gw = await db.PaymentGateways
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Name == "PayOS" &&
                    (x.IsActive == null || x.IsActive == true));

            var clientId = gw?.ClientId?.Trim();
            var apiKey = gw?.ApiKey?.Trim();
            var checksumKey = gw?.ChecksumKey?.Trim();

            if (string.IsNullOrWhiteSpace(clientId)) clientId = _config["PayOS:ClientId"]?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey)) apiKey = _config["PayOS:ApiKey"]?.Trim();
            if (string.IsNullOrWhiteSpace(checksumKey)) checksumKey = _config["PayOS:ChecksumKey"]?.Trim();
            var endpoint = _config["PayOS:Endpoint"]?.Trim() ?? "";

            return (clientId ?? "", apiKey ?? "", checksumKey ?? "", endpoint);
        }

        private static string ComputeHmacSha256Hex(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        // =========================
        // 1) CREATE PAYMENT (V2)
        // =========================
        public virtual async Task<PayOSCreatePaymentResult> CreatePaymentV2(
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
                throw new InvalidOperationException("Thiếu cấu hình PayOS (ClientId/ApiKey/ChecksumKey/Endpoint).");
            }

            var rawSignature =
                $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

            var signature = ComputeHmacSha256Hex(rawSignature, checksumKey);

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

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var jsonString = JsonSerializer.Serialize(requestBody, jsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("x-client-id", clientId);
            request.Headers.Add("x-api-key", apiKey);
            request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("PayOS CreatePayment response: {response}", responseContent);

            if (!response.IsSuccessStatusCode)
                throw new Exception("Lỗi HTTP từ PayOS: " + responseContent);

            var payOSResponse = JsonSerializer.Deserialize<PayOSResponse>(responseContent, jsonOptions);
            if (payOSResponse == null || payOSResponse.Code != "00")
                throw new Exception("Lỗi từ PayOS: " + responseContent);
            var checkoutUrl = payOSResponse.Data?.CheckoutUrl;
            if (string.IsNullOrWhiteSpace(checkoutUrl))
                throw new Exception("Không tìm thấy checkoutUrl trong phản hồi PayOS.");

            var paymentLinkId = payOSResponse.Data?.PaymentLinkId ?? "";

            return new PayOSCreatePaymentResult
            {
                CheckoutUrl = checkoutUrl,
                PaymentLinkId = paymentLinkId
            };
        }

        // =========================
        // 2) CANCEL PAYMENT LINK
        // =========================
        public virtual async Task<bool> CancelPaymentLink(string paymentLinkId, string? cancellationReason = null)
        {
            if (string.IsNullOrWhiteSpace(paymentLinkId)) return false;

            var (clientId, apiKey, _, endpoint) = await GetSettingsAsync();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
                return false;

            var baseUrl = endpoint.TrimEnd('/');
            var url = $"{baseUrl}/{paymentLinkId}/cancel";

            var reason = string.IsNullOrWhiteSpace(cancellationReason) ? "Cancelled by system" : cancellationReason.Trim();
            var body = new { cancellationReason = reason };
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var jsonString = JsonSerializer.Serialize(body, jsonOptions);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("x-client-id", clientId);
            req.Headers.Add("x-api-key", apiKey);
            req.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            var resp = await _httpClient.SendAsync(req);
            var content = await resp.Content.ReadAsStringAsync();

            _logger.LogInformation("PayOS CancelPaymentLink response: {response}", content);

            try
            {
                var payOSResponse = JsonSerializer.Deserialize<PayOSResponse>(content, jsonOptions);
                return payOSResponse != null && payOSResponse.Code == "00";
            }
            catch
            {
                return resp.IsSuccessStatusCode;
            }
        }

        // =========================
        // 3) VERIFY WEBHOOK SIGNATURE
        // =========================

        /// <summary>
        /// ✅ Khuyến nghị dùng bản async (vì checksumKey nằm DB)
        /// </summary>
        public virtual async Task<bool> VerifyWebhookSignatureAsync(object data, string? signature)
        {
            if (data == null) return false;
            if (string.IsNullOrWhiteSpace(signature)) return false;

            var (_, _, checksumKey, _) = await GetSettingsAsync();
            if (string.IsNullOrWhiteSpace(checksumKey)) return false;

            JsonElement el;
            try
            {
                el = JsonSerializer.SerializeToElement(data, SignatureJsonOptions);
            }
            catch
            {
                return false;
            }

            return VerifyWebhookSignatureCore(el, signature, checksumKey);
        }

        /// <summary>
        /// Giữ lại hàm sync để khỏi sửa nhiều chỗ gọi.
        /// (Nếu bạn có thể sửa caller, hãy chuyển sang VerifyWebhookSignatureAsync)
        /// </summary>
        public virtual bool VerifyWebhookSignature(object data, string? signature)
        {
            return VerifyWebhookSignatureAsync(data, signature).GetAwaiter().GetResult();
        }

        public virtual async Task<bool> VerifyWebhookSignatureAsync(JsonElement data, string? signature)
        {
            var (_, _, checksumKey, _) = await GetSettingsAsync();
            if (string.IsNullOrWhiteSpace(checksumKey)) return false;

            return VerifyWebhookSignatureCore(data, signature, checksumKey);
        }

        public virtual bool VerifyWebhookSignature(JsonElement data, string? signature)
        {
            return VerifyWebhookSignatureAsync(data, signature).GetAwaiter().GetResult();
        }

        private static bool VerifyWebhookSignatureCore(JsonElement data, string? signature, string checksumKey)
        {
            if (data.ValueKind != JsonValueKind.Object) return false;
            if (string.IsNullOrWhiteSpace(signature)) return false;

            var queryStr = BuildQueryStringSorted(data);
            var computed = ComputeHmacSha256Hex(queryStr, checksumKey);

            return FixedTimeEqualsHex(computed, signature.Trim());
        }

        private static string BuildQueryStringSorted(JsonElement obj)
        {
            var props = obj.EnumerateObject()
                           .OrderBy(p => p.Name, StringComparer.Ordinal);

            var parts = new List<string>();

            foreach (var p in props)
            {
                var key = p.Name;
                var value = ToSignatureValueString(p.Value);
                parts.Add($"{key}={value}");
            }

            return string.Join("&", parts);
        }

        private static string ToSignatureValueString(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
                return "";

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    var s = value.GetString();
                    if (string.IsNullOrEmpty(s)) return "";
                    if (string.Equals(s, "null", StringComparison.OrdinalIgnoreCase)) return "";
                    if (string.Equals(s, "undefined", StringComparison.OrdinalIgnoreCase)) return "";
                    return s;

                case JsonValueKind.Number:
                    return value.GetRawText();

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return value.GetRawText();

                case JsonValueKind.Array:
                case JsonValueKind.Object:
                    var normalized = NormalizeJsonForStableJson(value);
                    return JsonSerializer.Serialize(normalized, SignatureJsonOptions);

                default:
                    return value.GetRawText();
            }
        }

        private static object? NormalizeJsonForStableJson(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var p in el.EnumerateObject())
                        dict[p.Name] = NormalizeJsonForStableJson(p.Value);
                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var x in el.EnumerateArray())
                        list.Add(NormalizeJsonForStableJson(x));
                    return list;

                case JsonValueKind.String:
                    var s = el.GetString();
                    if (string.IsNullOrEmpty(s)) return "";
                    if (string.Equals(s, "null", StringComparison.OrdinalIgnoreCase)) return "";
                    if (string.Equals(s, "undefined", StringComparison.OrdinalIgnoreCase)) return "";
                    return s;

                case JsonValueKind.Number:
                    if (el.TryGetInt64(out var l)) return l;
                    if (el.TryGetDecimal(out var d)) return d;
                    return el.GetRawText();

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return el.GetBoolean();

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return "";

                default:
                    return el.GetRawText();
            }
        }

        private static bool FixedTimeEqualsHex(string aHex, string bHex)
        {
            var a = aHex.Trim().ToLowerInvariant();
            var b = bHex.Trim().ToLowerInvariant();

            if (a.Length != b.Length) return false;

            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }

        // =========================
        // 4) GET CHECKOUT URL BY PAYMENT LINK ID
        // =========================
        public virtual async Task<string?> GetCheckoutUrlByPaymentLinkId(string paymentLinkId)
        {
            if (string.IsNullOrWhiteSpace(paymentLinkId)) return null;

            var (clientId, apiKey, _, endpoint) = await GetSettingsAsync();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
                return null;
            var baseUrl = endpoint.TrimEnd('/');
            var getUrl = $"{baseUrl}/{paymentLinkId}";

            using var req = new HttpRequestMessage(HttpMethod.Get, getUrl);
            req.Headers.Add("x-client-id", clientId);
            req.Headers.Add("x-api-key", apiKey);

            var resp = await _httpClient.SendAsync(req);
            var content = await resp.Content.ReadAsStringAsync();

            _logger.LogInformation("PayOS GetPaymentLink response: {response}", content);

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var payOSResponse = JsonSerializer.Deserialize<PayOSResponse>(content, jsonOptions);

            if (payOSResponse == null || payOSResponse.Code != "00")
                return null;

            return payOSResponse.Data?.CheckoutUrl;
        }
    }
}