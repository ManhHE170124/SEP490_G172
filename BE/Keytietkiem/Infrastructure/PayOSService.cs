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
        private readonly string _clientId;
        private readonly string _apiKey;
        private readonly string _checksumKey;
        private readonly string _endpoint;

        // ✅ serialize ổn định cho signature (arrays/objects)
        private static readonly JsonSerializerOptions SignatureJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        public PayOSService(HttpClient httpClient, ILogger<PayOSService> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _clientId = config["PayOS:ClientId"] ?? "";
            _apiKey = config["PayOS:ApiKey"] ?? "";
            _checksumKey = config["PayOS:ChecksumKey"] ?? "";
            _endpoint = config["PayOS:Endpoint"] ?? "";
        }
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
            var rawSignature =
                $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_checksumKey));
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawSignature));
            var signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLowerInvariant();

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

            var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            request.Headers.Add("x-client-id", _clientId);
            request.Headers.Add("x-api-key", _apiKey);
            request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("PayOS CreatePayment response: {response}", responseContent);

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

        /// <summary>
        /// ✅ Verify webhook signature/HMAC:
        /// - Build query string từ object "data" theo key alphabet: key=value&...
        /// - Value null/undefined/"null"/"undefined" => ""
        /// - Array/Object => stringify JSON ổn định (sort keys)
        /// - HMAC_SHA256(checksumKey) => hex lowercase
        /// </summary>
        public virtual bool VerifyWebhookSignature(object data, string? signature)
        {
            if (data == null) return false;
            if (string.IsNullOrWhiteSpace(signature)) return false;
            if (string.IsNullOrWhiteSpace(_checksumKey)) return false;

            JsonElement el;
            try
            {
                // Quan trọng: camelCase khi serialize để match field names PayOS
                el = JsonSerializer.SerializeToElement(data, SignatureJsonOptions);
            }
            catch
            {
                return false;
            }

            return VerifyWebhookSignature(el, signature);
        }

        /// <summary>
        /// Overload: nếu bạn có sẵn JsonElement data
        /// </summary>
        public virtual bool VerifyWebhookSignature(JsonElement data, string? signature)
        {
            if (data.ValueKind != JsonValueKind.Object) return false;
            if (string.IsNullOrWhiteSpace(signature)) return false;
            if (string.IsNullOrWhiteSpace(_checksumKey)) return false;

            var queryStr = BuildQueryStringSorted(data);
            var computed = ComputeHmacSha256Hex(queryStr, _checksumKey);

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
            // null/undefined => ""
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
                    // dùng raw để tránh format lại
                    return value.GetRawText();

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return value.GetRawText();

                case JsonValueKind.Array:
                case JsonValueKind.Object:
                    // normalize keys để stringify ổn định
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

        private static string ComputeHmacSha256Hex(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
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

        /// <summary>
        /// Lấy lại checkoutUrl theo paymentLinkId (idempotency: DB không lưu checkoutUrl).
        /// Endpoint GET phụ thuộc PayOS, ở đây dùng dạng: {Endpoint}/{paymentLinkId}
        /// (nếu endpoint config đã là .../payment-requests)
        /// </summary>
        public virtual async Task<string?> GetCheckoutUrlByPaymentLinkId(string paymentLinkId)
        {
            if (string.IsNullOrWhiteSpace(paymentLinkId)) return null;
            if (string.IsNullOrWhiteSpace(_endpoint)) return null;

            var baseUrl = _endpoint.TrimEnd('/');
            var getUrl = $"{baseUrl}/{paymentLinkId}";

            var req = new HttpRequestMessage(HttpMethod.Get, getUrl);
            req.Headers.Add("x-client-id", _clientId);
            req.Headers.Add("x-api-key", _apiKey);

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
