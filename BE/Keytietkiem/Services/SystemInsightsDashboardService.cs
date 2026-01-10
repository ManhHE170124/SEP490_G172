// File: Services/SystemInsightsDashboardService.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.DTOs.SystemInsights;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Services
{
    public interface ISystemInsightsDashboardService
    {
        Task<SystemInsightsOverviewResponse> GetOverviewAsync(
            DateTime? fromLocal,
            DateTime? toLocalExclusive,
            string bucket,
            CancellationToken ct);
    }

    public sealed class SystemInsightsDashboardService : ISystemInsightsDashboardService
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

        public SystemInsightsDashboardService(IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<SystemInsightsOverviewResponse> GetOverviewAsync(
            DateTime? fromLocal,
            DateTime? toLocalExclusive,
            string bucket,
            CancellationToken ct)
        {
            bucket = string.Equals(bucket, "hour", StringComparison.OrdinalIgnoreCase) ? "hour" : "day";

            var tz = GetBangkokTimeZone();

            // ✅ FIX: chuẩn hoá lọc thời gian theo style OrdersDashboard
            // - default 30 ngày gần nhất
            // - nếu to <= from -> to = from + 1 day
            // - clamp tối đa 180 ngày
            var (fromL, toLExclusive, fromUtc, toUtc) = NormalizeRange(fromLocal, toLocalExclusive, tz);

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            // ===== Load minimal fields (AuditLogs) =====
            var audits = await LoadAuditRowsAsync(db, fromUtc, toUtc, ct);

            // ===== Load Notifications (minimal + aggregate like NotificationsController) =====
            var notis = await LoadNotificationRowsAsync(db, fromUtc, toUtc, ct);

            // ===== Build response =====
            var res = new SystemInsightsOverviewResponse
            {
                Timezone = "Asia/Bangkok",
                Bucket = bucket,
                FromLocal = fromL,
                ToLocal = toLExclusive // exclusive
            };

            BuildAuditKpisAndCharts(res, audits, tz, bucket, fromL, toLExclusive);
            BuildNotificationKpisAndCharts(res, notis, tz, fromL, toLExclusive);

            return res;
        }

        // -------------------------
        // Audit
        // -------------------------
        private sealed class AuditRow
        {
            public DateTime OccurredAtUtc { get; set; }
            public string? ActorEmail { get; set; }
            public string? ActorRole { get; set; }
            public string? Action { get; set; }
            public string? EntityType { get; set; }
            public string? IpAddress { get; set; }
        }

        private static async Task<List<AuditRow>> LoadAuditRowsAsync(
            KeytietkiemDbContext db,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            // Fallback property name: OccurredAtUtc -> OccurredAt
            async Task<List<AuditRow>> TryLoad(string occurredProp)
            {
                return await db.Set<AuditLog>()
                    .AsNoTracking()
                    .Where(a =>
                        EF.Property<DateTime>(a, occurredProp) >= fromUtc &&
                        EF.Property<DateTime>(a, occurredProp) < toUtc)
                    .Select(a => new AuditRow
                    {
                        OccurredAtUtc = EF.Property<DateTime>(a, occurredProp),
                        ActorEmail = EF.Property<string>(a, "ActorEmail"),
                        ActorRole = EF.Property<string>(a, "ActorRole"),
                        Action = EF.Property<string>(a, "Action"),
                        EntityType = EF.Property<string>(a, "EntityType"),
                        IpAddress = EF.Property<string>(a, "IpAddress"),
                    })
                    .ToListAsync(ct);
            }

            try { return await TryLoad("OccurredAtUtc"); }
            catch { return await TryLoad("OccurredAt"); }
        }

        private static void BuildAuditKpisAndCharts(
            SystemInsightsOverviewResponse res,
            List<AuditRow> audits,
            TimeZoneInfo tz,
            string bucket,
            DateTime fromLocal,
            DateTime toLocalExclusive)
        {
            int total = audits.Count;

            // unique actors: ưu tiên ActorEmail (ổn định nhất cho dashboard)
            int uniqueActors = audits
                .Select(x => (x.ActorEmail ?? "").Trim().ToLowerInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Count();

            int systemActions = audits.Count(x =>
            {
                var role = (x.ActorRole ?? "").Trim();
                return string.IsNullOrWhiteSpace(role) || role.Equals("System", StringComparison.OrdinalIgnoreCase);
            });

            res.SystemActivity.TotalActions = total;
            res.SystemActivity.UniqueActors = uniqueActors;
            res.SystemActivity.SystemActions = systemActions;
            res.SystemActivity.SystemActionRate = total > 0 ? (double)systemActions / total : 0;

            // Convert to local time once
            var localEvents = audits.Select(x =>
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.OccurredAtUtc, DateTimeKind.Utc), tz);
                var role = NormalizeRole(x.ActorRole);
                return new
                {
                    Local = local,
                    Role = role,
                    Action = NormalizeName(x.Action, "(NoAction)"),
                    EntityType = NormalizeName(x.EntityType, "(NoEntityType)"),
                    Ip = (x.IpAddress ?? "").Trim()
                };
            }).ToList();

            // 1) Line series by bucket
            res.AuditActionsSeries = BuildBucketSeries(
                fromLocal, toLocalExclusive, bucket,
                localEvents.GroupBy(e => BucketStart(e.Local, bucket)).ToDictionary(g => g.Key, g => g.Count()),
                dt => new TimePointDto { BucketStartLocal = FormatBucket(dt, bucket), Count = 0 });

            // 2) Stacked by role
            var roleGroups = localEvents
                .GroupBy(e => new { B = BucketStart(e.Local, bucket), e.Role })
                .Select(g => new { g.Key.B, g.Key.Role, C = g.Count() })
                .ToList();

            var bucketStarts = EnumerateBuckets(fromLocal, toLocalExclusive, bucket).ToList();
            foreach (var b in bucketStarts)
            {
                var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var rg in roleGroups.Where(x => x.B == b))
                    dict[rg.Role] = rg.C;

                res.AuditActionsByRoleSeries.Add(new StackedTimePointDto
                {
                    BucketStartLocal = FormatBucket(b, bucket),
                    RoleCounts = dict
                });
            }

            // 3) Top actions
            res.TopAuditActions = localEvents
                .GroupBy(x => x.Action)
                .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .Take(10)
                .ToList();

            // 4) Top entity types
            res.TopAuditEntityTypes = localEvents
                .GroupBy(x => x.EntityType)
                .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .Take(10)
                .ToList();

            // 5) Heatmap day-of-week x hour (Mon..Sun)
            var heat = localEvents
                .GroupBy(e => new { D = DayIndexMon0(e.Local.DayOfWeek), H = e.Local.Hour })
                .ToDictionary(g => (g.Key.D, g.Key.H), g => g.Count());

            for (int d = 0; d < 7; d++)
                for (int h = 0; h < 24; h++)
                    res.AuditHeatmap.Add(new HeatmapCellDto
                    {
                        DayIndex = d,
                        Hour = h,
                        Count = heat.TryGetValue((d, h), out var c) ? c : 0
                    });

            // 6) Top IP
            res.TopAuditIpAddresses = localEvents
                .Where(x => !string.IsNullOrWhiteSpace(x.Ip))
                .GroupBy(x => x.Ip)
                .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .Take(10)
                .ToList();
        }

        // -------------------------
        // Notifications
        // -------------------------
        private sealed class NotiBase
        {
            public int Id { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public bool IsSystemGenerated { get; set; }
            public bool IsGlobal { get; set; }
            public byte Severity { get; set; }
            public string? Type { get; set; }
        }

        private sealed class NotiRow
        {
            public DateTime CreatedAtUtc { get; set; }
            public bool IsSystemGenerated { get; set; }
            public bool IsGlobal { get; set; }
            public byte Severity { get; set; }
            public string? Type { get; set; }

            // ✅ computed like NotificationsController (NOT columns)
            public int TotalTargetUsers { get; set; }
            public int ReadCount { get; set; }
            public int TargetRolesCount { get; set; }
        }

        private sealed class LocalNotiEvent
        {
            public DateTime Date { get; set; }
            public bool IsSystemGenerated { get; set; }
            public bool IsGlobal { get; set; }
            public byte Severity { get; set; }
            public string Type { get; set; } = "(NoType)";
            public int TotalTargetUsers { get; set; }
            public int ReadCount { get; set; }
            public int TargetRolesCount { get; set; }
        }

        private static async Task<List<NotiRow>> LoadNotificationRowsAsync(
            KeytietkiemDbContext db,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            async Task<List<NotiBase>> TryLoadBase(string createdProp)
            {
                return await db.Notifications
                    .AsNoTracking()
                    .Where(n =>
                        EF.Property<DateTime>(n, createdProp) >= fromUtc &&
                        EF.Property<DateTime>(n, createdProp) < toUtc)
                    .Select(n => new NotiBase
                    {
                        Id = n.Id,
                        CreatedAtUtc = EF.Property<DateTime>(n, createdProp),
                        IsSystemGenerated = n.IsSystemGenerated,
                        IsGlobal = n.IsGlobal,
                        Severity = n.Severity,
                        Type = n.Type
                    })
                    .ToListAsync(ct);
            }

            List<NotiBase> baseNotis;
            try { baseNotis = await TryLoadBase("CreatedAtUtc"); }
            catch { baseNotis = await TryLoadBase("CreatedAt"); }

            if (baseNotis.Count == 0) return new List<NotiRow>();

            var ids = baseNotis.Select(x => x.Id).ToList();

            // total active users for IsGlobal notifications (same logic as NotificationsController)
            var totalActiveUsers = await db.Users.AsNoTracking()
                .CountAsync(u => u.Status == "Active", ct);

            // aggregate NotificationUsers: total + read
            var userAgg = await db.NotificationUsers.AsNoTracking()
                .Where(nu => ids.Contains(nu.NotificationId))
                .GroupBy(nu => nu.NotificationId)
                .Select(g => new
                {
                    NotificationId = g.Key,
                    Total = g.Count(),
                    Read = g.Count(x => x.IsRead)
                })
                .ToListAsync(ct);

            var userAggMap = userAgg.ToDictionary(x => x.NotificationId, x => x);

            // aggregate NotificationTargetRoles count
            var roleAgg = await db.NotificationTargetRoles.AsNoTracking()
                .Where(ntr => ids.Contains(ntr.NotificationId))
                .GroupBy(ntr => ntr.NotificationId)
                .Select(g => new
                {
                    NotificationId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync(ct);

            var roleAggMap = roleAgg.ToDictionary(x => x.NotificationId, x => x.Count);

            // build NotiRow
            var rows = new List<NotiRow>(baseNotis.Count);
            foreach (var n in baseNotis)
            {
                userAggMap.TryGetValue(n.Id, out var ua);
                roleAggMap.TryGetValue(n.Id, out var rc);

                var totalTargets = n.IsGlobal ? totalActiveUsers : (ua?.Total ?? 0);
                var readCount = ua?.Read ?? 0;

                rows.Add(new NotiRow
                {
                    CreatedAtUtc = n.CreatedAtUtc,
                    IsSystemGenerated = n.IsSystemGenerated,
                    IsGlobal = n.IsGlobal,
                    Severity = n.Severity,
                    Type = n.Type,
                    TotalTargetUsers = Math.Max(0, totalTargets),
                    ReadCount = Math.Max(0, readCount),
                    TargetRolesCount = Math.Max(0, rc)
                });
            }

            return rows;
        }

        private static void BuildNotificationKpisAndCharts(
            SystemInsightsOverviewResponse res,
            List<NotiRow> notis,
            TimeZoneInfo tz,
            DateTime fromLocal,
            DateTime toLocalExclusive)
        {
            int total = notis.Count;
            int sys = notis.Count(x => x.IsSystemGenerated);
            int manual = total - sys;
            int global = notis.Count(x => x.IsGlobal);
            int targeted = total - global;

            long targetSum = notis.Sum(x => (long)Math.Max(0, x.TotalTargetUsers));
            long readSum = notis.Sum(x => (long)Math.Max(0, x.ReadCount));

            res.NotificationsHealth.TotalNotifications = total;
            res.NotificationsHealth.SystemGeneratedCount = sys;
            res.NotificationsHealth.ManualCount = manual;
            res.NotificationsHealth.SystemGeneratedRate = total > 0 ? (double)sys / total : 0;

            res.NotificationsHealth.GlobalCount = global;
            res.NotificationsHealth.TargetedCount = targeted;
            res.NotificationsHealth.GlobalRate = total > 0 ? (double)global / total : 0;

            res.NotificationsHealth.TotalTargetUsersSum = targetSum;
            res.NotificationsHealth.ReadCountSum = readSum;
            res.NotificationsHealth.OverallReadRate = targetSum > 0 ? (double)readSum / targetSum : 0;

            var localNotis = notis.Select(n =>
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc), tz);
                return new LocalNotiEvent
                {
                    Date = local.Date,
                    IsSystemGenerated = n.IsSystemGenerated,
                    IsGlobal = n.IsGlobal,
                    Severity = n.Severity,
                    Type = NormalizeName(n.Type, "(NoType)"),
                    TotalTargetUsers = Math.Max(0, n.TotalTargetUsers),
                    ReadCount = Math.Max(0, n.ReadCount),
                    TargetRolesCount = Math.Max(0, n.TargetRolesCount)
                };
            }).ToList();

            var dailyAll = localNotis
                .GroupBy(x => x.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ✅ FIX: enumerate ngày theo toLocalExclusive (nếu to có time -> vẫn tính tới hết ngày đó)
            foreach (var d in EnumerateDays(fromLocal, toLocalExclusive))
            {
                if (!dailyAll.TryGetValue(d, out var list))
                    list = new List<LocalNotiEvent>();

                int t = list.Count;
                int s = list.Count(x => x.IsSystemGenerated);

                res.NotificationsDaily.Add(new NotificationsDailyDto
                {
                    DateLocal = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Total = t,
                    System = s,
                    Manual = t - s
                });
            }

            foreach (var d in EnumerateDays(fromLocal, toLocalExclusive))
            {
                if (!dailyAll.TryGetValue(d, out var list))
                    list = new List<LocalNotiEvent>();

                res.NotificationsSeverityDaily.Add(new NotificationsSeverityDailyDto
                {
                    DateLocal = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Info = list.Count(x => x.Severity == 0),
                    Success = list.Count(x => x.Severity == 1),
                    Warning = list.Count(x => x.Severity == 2),
                    Error = list.Count(x => x.Severity == 3),
                });
            }

            // 3) Scope breakdown: Global / RoleTargeted / UserTargeted
            int roleTargeted = localNotis.Count(x => !x.IsGlobal && x.TargetRolesCount > 0);
            int userTargeted = localNotis.Count(x => !x.IsGlobal && x.TargetRolesCount == 0);

            res.NotificationScope = new NotificationScopeBreakdownDto
            {
                Global = global,
                RoleTargeted = roleTargeted,
                UserTargeted = userTargeted
            };

            // 4) Top Notification Type
            res.TopNotificationTypes = localNotis
                .GroupBy(x => x.Type)
                .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .Take(10)
                .ToList();

            // 5) Read rate daily
            foreach (var d in EnumerateDays(fromLocal, toLocalExclusive))
            {
                if (!dailyAll.TryGetValue(d, out var list))
                    list = new List<LocalNotiEvent>();

                long ts = list.Sum(x => (long)x.TotalTargetUsers);
                long rs = list.Sum(x => (long)x.ReadCount);

                res.NotificationReadRateDaily.Add(new ReadRateDailyDto
                {
                    DateLocal = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    TotalTargetUsers = ts,
                    ReadCount = rs,
                    ReadRate = ts > 0 ? (double)rs / ts : 0
                });
            }

            // 6) Histogram recipients
            var bins = new (string label, Func<int, bool> pred)[]
            {
                ("0", x => x == 0),
                ("1-9", x => x >= 1 && x <= 9),
                ("10-49", x => x >= 10 && x <= 49),
                ("50-99", x => x >= 50 && x <= 99),
                ("100-199", x => x >= 100 && x <= 199),
                ("200-499", x => x >= 200 && x <= 499),
                ("500+", x => x >= 500),
            };

            foreach (var b in bins)
            {
                res.NotificationRecipientsHistogram.Add(new HistogramBucketDto
                {
                    Label = b.label,
                    Count = localNotis.Count(x => b.pred(x.TotalTargetUsers))
                });
            }
        }

        // -------------------------
        // Helpers
        // -------------------------
        private static TimeZoneInfo GetBangkokTimeZone()
        {
            // Linux: "Asia/Bangkok"
            // Windows: "SE Asia Standard Time"
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        }

        // ✅ FIX: NormalizeRange theo style OrdersDashboard
        private static (DateTime fromLocal, DateTime toLocalExclusive, DateTime fromUtc, DateTime toUtc)
            NormalizeRange(DateTime? fromLocal, DateTime? toLocalExclusive, TimeZoneInfo tz)
        {
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            var fromL = fromLocal ?? nowLocal.AddDays(-30);
            var toL = toLocalExclusive ?? nowLocal; // exclusive; nếu FE không gửi thì dùng "now"

            // Treat as "local unspecified"
            fromL = DateTime.SpecifyKind(fromL, DateTimeKind.Unspecified);
            toL = DateTime.SpecifyKind(toL, DateTimeKind.Unspecified);

            // giống OrdersDashboard: đảm bảo tối thiểu 1 ngày
            if (toL <= fromL)
            {
                toL = fromL.AddDays(1);
            }

            // clamp tối đa 180 ngày
            if ((toL - fromL).TotalDays > 180)
            {
                toL = fromL.AddDays(180);
            }

            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromL, tz);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(toL, tz);

            return (fromL, toL, fromUtc, toUtc);
        }

        private static string NormalizeRole(string? role)
        {
            var r = (role ?? "").Trim();
            if (string.IsNullOrWhiteSpace(r)) return "System";
            return r;
        }

        private static string NormalizeName(string? s, string fallback)
        {
            var x = (s ?? "").Trim();
            return string.IsNullOrWhiteSpace(x) ? fallback : x;
        }

        private static DateTime BucketStart(DateTime local, string bucket)
        {
            if (bucket == "hour")
                return new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0);
            return local.Date;
        }

        private static string FormatBucket(DateTime bucketStartLocal, string bucket)
        {
            if (bucket == "hour")
                return bucketStartLocal.ToString("yyyy-MM-dd'T'HH':00:00'", CultureInfo.InvariantCulture);
            return bucketStartLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static int DayIndexMon0(DayOfWeek dow)
        {
            // C#: Sunday=0 ... Saturday=6
            // Convert to Mon=0 ... Sun=6
            return dow == DayOfWeek.Sunday ? 6 : ((int)dow - 1);
        }

        // ✅ NEW: floor/ceil theo bucket để không hụt giờ/ngày khi toLocalExclusive có time
        private static DateTime FloorToBucket(DateTime local, string bucket)
        {
            if (bucket == "hour")
                return new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0);
            return local.Date;
        }

        private static DateTime CeilToBucketExclusive(DateTime localExclusive, string bucket)
        {
            if (bucket == "hour")
            {
                var f = new DateTime(localExclusive.Year, localExclusive.Month, localExclusive.Day, localExclusive.Hour, 0, 0);
                var exact = localExclusive.Minute == 0 && localExclusive.Second == 0 && localExclusive.Millisecond == 0;
                return exact ? f : f.AddHours(1);
            }

            var d = localExclusive.Date;
            return localExclusive.TimeOfDay == TimeSpan.Zero ? d : d.AddDays(1);
        }

        private static IEnumerable<DateTime> EnumerateBuckets(DateTime fromLocal, DateTime toLocalExclusive, string bucket)
        {
            var cur = FloorToBucket(fromLocal, bucket);
            var end = CeilToBucketExclusive(toLocalExclusive, bucket);

            if (bucket == "hour")
            {
                while (cur < end)
                {
                    yield return cur;
                    cur = cur.AddHours(1);
                }
                yield break;
            }

            while (cur < end)
            {
                yield return cur;
                cur = cur.AddDays(1);
            }
        }

        private static List<TimePointDto> BuildBucketSeries(
            DateTime fromLocal,
            DateTime toLocalExclusive,
            string bucket,
            Dictionary<DateTime, int> counts,
            Func<DateTime, TimePointDto> factory)
        {
            var list = new List<TimePointDto>();
            foreach (var b in EnumerateBuckets(fromLocal, toLocalExclusive, bucket))
            {
                var dto = factory(b);
                dto.BucketStartLocal = FormatBucket(b, bucket);
                dto.Count = counts.TryGetValue(b, out var c) ? c : 0;
                list.Add(dto);
            }
            return list;
        }

        private static IEnumerable<DateTime> EnumerateDays(DateTime fromLocal, DateTime toLocalExclusive)
        {
            var d = fromLocal.Date;
            var end = CeilToBucketExclusive(toLocalExclusive, "day"); // midnight boundary
            while (d < end)
            {
                yield return d;
                d = d.AddDays(1);
            }
        }
    }
}
