using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Services
{
    public class AuditLogger : IAuditLogger
    {
        private readonly KeytietkiemDbContext _db;
        private readonly ILogger<AuditLogger> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public AuditLogger(KeytietkiemDbContext db, ILogger<AuditLogger> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task LogAsync(
     HttpContext httpContext,
     string action,
     string? entityType = null,
     string? entityId = null,
     object? before = null,
     object? after = null)
        {
            var path = httpContext?.Request?.Path.Value ?? string.Empty;

            try
            {
                if (!string.IsNullOrEmpty(path) &&
                    path.StartsWith("/api/auditlogs", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var user = httpContext.User;

                // ====== ĐỌC OVERRIDE TỪ HttpContext.Items (nếu có) ======
                Guid? actorIdOverride = null;
                string? actorEmailOverride = null;
                string? actorRoleOverride = null;

                if (httpContext?.Items != null)
                {
                    if (httpContext.Items.TryGetValue("Audit:ActorId", out var idObj))
                    {
                        switch (idObj)
                        {
                            case Guid g:
                                actorIdOverride = g;
                                break;
                            case string s when Guid.TryParse(s, out var g2):
                                actorIdOverride = g2;
                                break;
                        }
                    }

                    if (httpContext.Items.TryGetValue("Audit:ActorEmail", out var emailObj)
                        && emailObj is string emailStr && !string.IsNullOrWhiteSpace(emailStr))
                    {
                        actorEmailOverride = emailStr;
                    }

                    if (httpContext.Items.TryGetValue("Audit:ActorRole", out var roleObj)
                        && roleObj is string roleStr && !string.IsNullOrWhiteSpace(roleStr))
                    {
                        actorRoleOverride = roleStr;
                    }
                }

                // ==== Actor info (ưu tiên override, fallback sang claims) ====
                var actorId = actorIdOverride ?? TryGetUserId(user);
                var actorEmail = actorEmailOverride ?? TryGetEmail(user);
                var actorRole = actorRoleOverride ?? TryGetRole(user);

                // ==== Request context ====
                var sessionId = GetSessionId(httpContext);
                var ipAddress = GetClientIp(httpContext);
                var userAgent = httpContext.Request.Headers["User-Agent"].ToString();

                string? beforeJson = SerializeObjectSafe(before);
                string? afterJson = SerializeObjectSafe(after);

                var log = new AuditLog
                {
                    OccurredAt = DateTime.UtcNow,

                    ActorId = actorId,
                    ActorEmail = actorEmail,
                    ActorRole = actorRole,

                    SessionId = sessionId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,

                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,

                    BeforeDataJson = beforeJson,
                    AfterDataJson = afterJson
                };

                _db.AuditLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to write audit log for Action={Action}, Path={Path}",
                    action,
                    path
                );
            }
        }


        // ==================== Helper methods ====================

        private static Guid? TryGetUserId(ClaimsPrincipal user)
        {
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
                return null;

            string? idValue =
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                user.FindFirst("sub")?.Value ??
                user.FindFirst("userId")?.Value;

            if (Guid.TryParse(idValue, out var id))
            {
                return id;
            }

            return null;
        }

        private static string? TryGetEmail(ClaimsPrincipal user)
        {
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
                return null;

            return user.FindFirst(ClaimTypes.Email)?.Value
                   ?? user.FindFirst("email")?.Value;
        }

        private static string? TryGetRole(ClaimsPrincipal user)
        {
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
                return null;

            // Lấy role đầu tiên (nếu có nhiều)
            var roleClaim = user.FindAll(ClaimTypes.Role).FirstOrDefault()
                            ?? user.FindAll("role").FirstOrDefault();

            return roleClaim?.Value;
        }

        private static string? GetSessionId(HttpContext httpContext)
        {
            // Ưu tiên header X-Client-Id (do FE gửi lên)
            var fromHeader = httpContext.Request.Headers["X-Client-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(fromHeader))
            {
                return fromHeader;
            }

            // Có thể fallback sang cookie nếu muốn
            var fromCookie = httpContext.Request.Cookies["ktk_client_id"];
            if (!string.IsNullOrWhiteSpace(fromCookie))
            {
                return fromCookie;
            }

            // Cuối cùng fallback TraceIdentifier
            return httpContext.TraceIdentifier;
        }

        private static string? GetClientIp(HttpContext httpContext)
        {
            // Nếu về sau có reverse proxy (Nginx/Cloudflare...) thì đọc X-Forwarded-For trước
            var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                // X-Forwarded-For có thể chứa nhiều IP, lấy IP đầu.
                var first = forwarded.Split(',').FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first.Trim();
                }
            }

            return httpContext.Connection.RemoteIpAddress?.ToString();
        }

        private static string? SerializeObjectSafe(object? value)
        {
            if (value == null) return null;

            try
            {
                return JsonSerializer.Serialize(value, JsonOptions);
            }
            catch
            {
                // Nếu serialize fail (vòng tham chiếu...), tránh throw
                return null;
            }
        }
    }
}
