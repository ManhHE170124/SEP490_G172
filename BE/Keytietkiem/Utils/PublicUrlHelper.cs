using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Net;

namespace Keytietkiem.Utils
{
    public static class PublicUrlHelper
    {
        /// <summary>
        /// Lấy "public origin" của request hiện tại (vd: https://keytietkiem.com).
        /// Ưu tiên X-Forwarded-* (khi chạy sau Nginx/Reverse Proxy).
        /// Fallback về config nếu không lấy được từ request.
        ///
        /// NOTE (Dev):
        /// - Khi chạy localhost, BE chỉ nhìn thấy host của chính BE (vd https://localhost:7292).
        /// - Nếu muốn link trả về FE (vd http://localhost:3000) thì phải cấu hình ClientConfig:ClientUrl,
        ///   và helper sẽ ưu tiên giá trị này khi host là localhost/127.0.0.1.
        /// </summary>
        public static string GetPublicOrigin(HttpContext? httpContext, IConfiguration? config = null)
        {
            // 1) Fallback config (dùng cho background job hoặc trường hợp không có HttpContext)
            var fallback =
                (config?["ClientConfig:ClientUrl"] ?? config?["PayOS:FrontendBaseUrl"] ?? "https://keytietkiem.com")
                .Trim()
                .TrimEnd('/');

            if (httpContext == null) return fallback;

            // 2) Read forwarded headers (Nginx/Proxy)
            var fProto = httpContext.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
            var fHost = httpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            var fPort = httpContext.Request.Headers["X-Forwarded-Port"].FirstOrDefault();

            var scheme = FirstTokenOrNull(fProto) ?? httpContext.Request.Scheme;
            var host = FirstTokenOrNull(fHost) ?? httpContext.Request.Host.Value;

            if (string.IsNullOrWhiteSpace(host))
                return fallback;

            // ✅ Dev fix: nếu host là localhost/127.0.0.1 thì ưu tiên ClientConfig:ClientUrl (vd http://localhost:3000)
            // Vì BE khi dev chỉ "thấy" https://localhost:7292, không biết FE chạy port nào.
            var hostWithoutPortForLocalCheck = host.Split(':')[0].Trim();
            if (IsLocalHost(hostWithoutPortForLocalCheck))
            {
                var clientUrl = (config?["ClientConfig:ClientUrl"] ?? "").Trim().TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(clientUrl))
                    return clientUrl;
            }

            // 3) Nếu host không có port, thử gắn X-Forwarded-Port (khi cần)
            if (!string.IsNullOrWhiteSpace(fPort) && !host.Contains(":", StringComparison.OrdinalIgnoreCase))
            {
                var isDefaultPort =
                    (string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase) && fPort == "443")
                    || (string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) && fPort == "80");

                if (!isDefaultPort)
                    host = $"{host}:{fPort}";
            }

            var origin = $"{scheme}://{host}".TrimEnd('/');

            // 4) Nếu request host là IP (vd 68.183.xxx.xxx) nhưng bạn muốn ưu tiên domain -> dùng fallback config
            // (Giúp tránh trường hợp FE/Client gọi API bằng IP)
            var hostWithoutPort = host.Split(':')[0];
            if (IPAddress.TryParse(hostWithoutPort, out _))
                return fallback;

            return origin;

            static string? FirstTokenOrNull(string? v)
            {
                if (string.IsNullOrWhiteSpace(v)) return null;
                // trường hợp header có nhiều giá trị: "https, http"
                return v.Split(',').Select(x => x.Trim()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            }

            static bool IsLocalHost(string host)
            {
                if (string.IsNullOrWhiteSpace(host)) return false;
                host = host.Trim().ToLowerInvariant();
                return host == "localhost" || host == "127.0.0.1" || host == "::1";
            }
        }
    }
}