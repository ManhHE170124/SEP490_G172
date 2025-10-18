using Keytietkiem.DTOs;
using Keytietkiem.Models;

namespace Keytietkiem.Mapping
{
    public static class AuditMapping
    {
        public static AuditLogDto ToDto(this AuditLog s, bool mask)
        {
            return new AuditLogDto
            {
                AuditId = s.AuditId,
                OccurredAt = s.OccurredAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                ActorEmail = mask ? MaskEmail(s.ActorEmail) : s.ActorEmail,
                Action = s.Action,
                Resource = s.Resource,
                EntityId = s.EntityId,
                IpAddress = mask ? MaskIp(s.IpAddress) : s.IpAddress,
                UserAgent = s.UserAgent,
                DetailJson = s.DetailJson,
                CorrelationId = s.CorrelationId?.ToString(),
                IntegrityAlert = s.IntegrityAlert
            };
        }

        public static string? MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return email;
            var at = email.IndexOf('@'); if (at <= 0) return "***";
            var name = email[..at]; var domain = email[at..];
            var visible = Math.Min(1, name.Length);
            return $"{name[..visible]}***{domain}";
        }

        public static string? MaskIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return ip;
            if (ip.Contains('.'))
            {
                var p = ip.Split('.');
                if (p.Length == 4) return $"{p[0]}.{p[1]}.{p[2]}.x";
            }
            if (ip.Contains(':')) return ip.Split(':')[0] + ":****";
            return "x.x.x.x";
        }
    }
}
