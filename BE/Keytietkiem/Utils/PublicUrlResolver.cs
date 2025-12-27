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
        }
    }
}
