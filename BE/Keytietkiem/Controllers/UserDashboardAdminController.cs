// File: Controllers/UserDashboardAdminController.cs
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Users;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/user-dashboard-admin")]
    public class UserDashboardAdminController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        public UserDashboardAdminController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        // =========================
        // Growth Overview (SIMPLE)
        // =========================
        // GET /api/user-dashboard-admin/overview-growth?month=yyyy-MM&groupBy=day|week&asOf=yyyy-MM-dd(optional)
        [HttpGet("overview-growth")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<UserGrowthDashboardDto>> GetGrowthOverview(
            [FromQuery] string? month = null,           // yyyy-MM
            [FromQuery] string groupBy = "day",         // day|week
            [FromQuery] string? asOf = null,            // yyyy-MM-dd (optional)
            CancellationToken cancellationToken = default)
        {
            groupBy = NormalizeGroupBy(groupBy);

            var tz = GetBangkokTimeZone();
            var nowUtc = _clock.UtcNow;
            var nowLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz).Date);

            // month default = current month
            var (monthStart, nextMonthStart) = ResolveMonth(month, nowLocalDate);

            // asOf default:
            // - if selected month is current month => today
            // - else => end of selected month (nextMonthStart - 1 day)
            var defaultAsOf = (monthStart.Year == nowLocalDate.Year && monthStart.Month == nowLocalDate.Month)
                ? nowLocalDate
                : nextMonthStart.AddDays(-1);

            var asOfDate = TryParseDateOnly(asOf, out var tmpAsOf) ? tmpAsOf : defaultAsOf;

            // clamp asOf into selected month range
            if (asOfDate < monthStart) asOfDate = monthStart;
            if (asOfDate >= nextMonthStart) asOfDate = nextMonthStart.AddDays(-1);

            // Local date boundaries => UTC
            var fromUtc = LocalDateStartToUtc(monthStart, tz);
            var toUtcExclusive = LocalDateStartToUtc(asOfDate.AddDays(1), tz);
            var monthStartUtc = fromUtc;

            // End of previous month = monthStart (exclusive)
            var prevMonthEndUtcExclusive = monthStartUtc;

            // NOTE:
            // CreatedAt trong DB thường lưu UTC. Để group theo "ngày VN", ta shift +7h rồi lấy Date.
            // Bangkok không DST nên offset cố định 7h.
            const int BKK_OFFSET_HOURS = 7;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            // ========== KPI ==========
            // New users in selected month (up to as-of), isTemp=false
            var newUsersInMonth = await db.Users.AsNoTracking()
                .Where(u => !u.IsTemp && u.CreatedAt >= fromUtc && u.CreatedAt < toUtcExclusive)
                .LongCountAsync(cancellationToken);

            // Total users as-of (cumulative), isTemp=false
            var totalUsersAsOf = await db.Users.AsNoTracking()
                .Where(u => !u.IsTemp && u.CreatedAt < toUtcExclusive)
                .LongCountAsync(cancellationToken);

            // Total users at end of previous month (cumulative), isTemp=false
            var totalUsersEndPrevMonth = await db.Users.AsNoTracking()
                .Where(u => !u.IsTemp && u.CreatedAt < prevMonthEndUtcExclusive)
                .LongCountAsync(cancellationToken);

            var changeVsPrevMonth = totalUsersAsOf - totalUsersEndPrevMonth;

            // ========== SERIES (daily then rebucket week if needed) ==========
            var dailyRaw = await db.Users.AsNoTracking()
                .Where(u => !u.IsTemp && u.CreatedAt >= fromUtc && u.CreatedAt < toUtcExclusive)
                .GroupBy(u => u.CreatedAt.AddHours(BKK_OFFSET_HOURS).Date) // local date bucket
                .Select(g => new DailyRow
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync(cancellationToken);

            var dailySeries = BuildFullDailySeries(monthStart, asOfDate, dailyRaw);

            var finalSeries = groupBy == "week"
                ? RebucketWeek(dailySeries)
                : dailySeries;

            var dto = new UserGrowthDashboardDto
            {
                Filter = new UserGrowthFilterEchoDto
                {
                    Month = $"{monthStart:yyyy-MM}",
                    AsOfDate = asOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    GroupBy = groupBy,
                    TimeZone = "Asia/Bangkok"
                },
                Kpis = new UserGrowthKpisDto
                {
                    NewUsersInMonth = newUsersInMonth,
                    TotalUsersAsOf = totalUsersAsOf,
                    TotalUsersChangeVsPrevMonth = changeVsPrevMonth
                },
                Series = finalSeries
            };

            return Ok(dto);
        }

        // =========================
        // Helpers (private)
        // =========================

        private sealed class DailyRow
        {
            public DateTime Date { get; set; } // date part
            public int Count { get; set; }
        }

        private static string NormalizeGroupBy(string? g)
        {
            var x = (g ?? "").Trim().ToLowerInvariant();
            return x == "week" ? "week" : "day";
        }

        private static (DateOnly MonthStart, DateOnly NextMonthStart) ResolveMonth(string? month, DateOnly fallbackNowLocal)
        {
            // month format: yyyy-MM
            if (!string.IsNullOrWhiteSpace(month))
            {
                var m = month.Trim();
                if (m.Length == 7
                    && int.TryParse(m.Substring(0, 4), out var y)
                    && int.TryParse(m.Substring(5, 2), out var mm)
                    && mm >= 1 && mm <= 12)
                {
                    var start = new DateOnly(y, mm, 1);
                    return (start, start.AddMonths(1));
                }
            }

            var fb = new DateOnly(fallbackNowLocal.Year, fallbackNowLocal.Month, 1);
            return (fb, fb.AddMonths(1));
        }

        private static bool TryParseDateOnly(string? ymd, out DateOnly d)
        {
            d = default;
            if (string.IsNullOrWhiteSpace(ymd)) return false;

            return DateOnly.TryParseExact(
                ymd.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out d);
        }

        private static TimeZoneInfo GetBangkokTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok"); } catch { }
            try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); } catch { }
            return TimeZoneInfo.Utc;
        }

        private static DateTime LocalDateStartToUtc(DateOnly localDate, TimeZoneInfo tz)
        {
            var local = new DateTime(localDate.Year, localDate.Month, localDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(local, tz);
        }

        private static List<UserGrowthSeriesPointDto> BuildFullDailySeries(
            DateOnly fromD,
            DateOnly toD,
            List<DailyRow> raw)
        {
            var dict = raw.ToDictionary(
                x => DateOnly.FromDateTime(x.Date),
                x => x.Count
            );

            var res = new List<UserGrowthSeriesPointDto>();
            for (var d = fromD; d <= toD; d = d.AddDays(1))
            {
                dict.TryGetValue(d, out var cnt);
                res.Add(new UserGrowthSeriesPointDto
                {
                    BucketDate = d,
                    NewUsers = cnt
                });
            }
            return res;
        }

        private static List<UserGrowthSeriesPointDto> RebucketWeek(List<UserGrowthSeriesPointDto> daily)
        {
            static DateOnly WeekStart(DateOnly d)
            {
                var diff = (int)d.DayOfWeek - (int)DayOfWeek.Monday;
                if (diff < 0) diff += 7;
                return d.AddDays(-diff);
            }

            return daily
                .GroupBy(x => WeekStart(x.BucketDate))
                .OrderBy(g => g.Key)
                .Select(g => new UserGrowthSeriesPointDto
                {
                    BucketDate = g.Key,
                    NewUsers = g.Sum(x => x.NewUsers)
                })
                .ToList();
        }
    }
}
