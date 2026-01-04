using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace Keytietkiem.Utils
{
    public static class GuestCartIdentityHelper
    {
        public const string HeaderName = "X-Guest-Cart-Id";
        public const string CookieName = "ktk_anon_id";
        public const string LegacyCookieName = "ktk_guest_cart_id";

        /// <summary>
        /// Ưu tiên HEADER trước để khớp với FE (localStorage).
        /// Fallback cookie nếu không có header.
        /// </summary>
        public static string? TryGet(HttpContext ctx)
        {
            if (ctx == null) return null;

            var fromHeader = ctx.Request.Headers[HeaderName].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fromHeader)) return fromHeader.Trim();

            var fromCookie = ctx.Request.Cookies[CookieName];
            if (!string.IsNullOrWhiteSpace(fromCookie)) return fromCookie.Trim();

            var fromLegacy = ctx.Request.Cookies[LegacyCookieName];
            if (!string.IsNullOrWhiteSpace(fromLegacy)) return fromLegacy.Trim();

            return null;
        }

        public static string GetOrInit(HttpContext ctx)
        {
            var id = TryGet(ctx);
            if (string.IsNullOrWhiteSpace(id))
                id = Guid.NewGuid().ToString();

            EnsureCookie(ctx, id);
            return id;
        }

        /// <summary>
        /// Quan trọng: Path="/" để cookie được gửi cho cả /api/orders/checkout (không chỉ /api/storefront/*)
        /// </summary>
        public static void EnsureCookie(HttpContext ctx, string id)
        {
            var cur = ctx.Request.Cookies[CookieName];
            if (string.Equals(cur, id, StringComparison.OrdinalIgnoreCase)) return;

            ctx.Response.Cookies.Append(CookieName, id, new CookieOptions
            {
                HttpOnly = true,
                Secure = ctx.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",                    // 🔥 FIX CHÍNH
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
        }
    }
}
