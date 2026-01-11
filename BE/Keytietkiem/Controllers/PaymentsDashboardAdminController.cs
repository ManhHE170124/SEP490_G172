// File: Controllers/PaymentsDashboardAdminController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Controllers
{
    /// <summary>
    /// Payments / PayOS Dashboard (Admin).
    /// - KPI cards: tổng giao dịch/tỷ lệ thành công/hết hạn/hủy/hoàn tiền, chờ quá hạn, trung vị+p95 thời gian thanh toán
    /// - Charts: xu hướng theo ngày theo trạng thái, xu hướng tỷ lệ thành công, xu hướng tiền thu, phân bố attempts, heatmap, top lý do thất bại
    ///
    /// Data source:
    /// - Payments table (CreatedAt/Status/Amount/TargetType/TargetId/Provider...)
    /// - AuditLogs (EntityType=Payment, Action=PaymentStatusChanged) để:
    ///     + lấy PaidAt (OccurredAt) => time-to-pay distribution + percentiles
    ///     + lấy meta.reason => Top Failure Reasons
    /// </summary>
    [ApiController]
    [Route("api/payments-dashboard-admin")]
    [Authorize]
    public class PaymentsDashboardAdminController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;
        private readonly ILogger<PaymentsDashboardAdminController> _logger;

        // ===== Status groups (sync with PaymentsController) =====
        private static readonly string[] SuccessStatuses = { "Paid", "Success", "Completed" };

        // ✅ FE hiện tại có mode "ALL" => provider query param có thể bị omit (undefined)
        // => provider mặc định phải là null (không filter), FE tự gửi "PayOS" khi chọn PayOS.
        private static readonly string[] PendingStatuses = { "Pending", "PendingPayment" };
        private static readonly string[] TimeoutStatuses = { "Timeout", "Expired", "CancelledByTimeout" };
        private static readonly string[] CancelledStatuses = { "Cancelled", "Canceled", "CancelledByUser" };
        private static readonly string[] FailedStatuses = { "Failed", "Error" };
        private static readonly string[] RefundedStatuses = { "Refunded", "Refund" };

        // AuditLogs pattern
        private const string AuditEntityTypePayment = "Payment";
        private const string AuditActionPaymentStatusChanged = "PaymentStatusChanged";

        // ✅ cap range theo FE: FE có thể chọn theo năm và clamp days tới 3660
        private const int MaxRangeDays = 3660;

        // ===== Vietnamese mapping =====
        private static readonly Dictionary<string, string> StatusViMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Pending"] = "Đang chờ",
            ["PendingPayment"] = "Đang chờ",
            ["Paid"] = "Đã thanh toán",
            ["Success"] = "Thành công",
            ["Completed"] = "Hoàn tất",
            ["Cancelled"] = "Đã hủy",
            ["Canceled"] = "Đã hủy",
            ["CancelledByUser"] = "Đã hủy",
            ["Timeout"] = "Hết hạn thanh toán",
            ["Expired"] = "Hết hạn thanh toán",
            ["CancelledByTimeout"] = "Hết hạn thanh toán",
            ["Failed"] = "Giao dịch lỗi",
            ["Error"] = "Giao dịch lỗi",
            ["Refunded"] = "Hoàn tiền",
            ["Refund"] = "Hoàn tiền",
            ["Unknown"] = "Không rõ"
        };

        public PaymentsDashboardAdminController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock,
            ILogger<PaymentsDashboardAdminController> logger)
        {
            _dbFactory = dbFactory;
            _clock = clock;
            _logger = logger;
        }

        // ============================================================
        // 1) SUMMARY (KPI cards + Alerts)
        // ============================================================
        // GET: /api/payments-dashboard-admin/summary?fromUtc=...&toUtc=...&provider=PayOS&targetType=Order
        [HttpGet("summary")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<PaymentDashboardSummaryDto>> GetSummary(
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] string? provider = null,
            [FromQuery] string? targetType = null,          // Order | SupportPlan | null=all
            [FromQuery] int pendingOverdueMinutes = 5,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var (from, to) = NormalizeRangeUtc(nowUtc, fromUtc, toUtc);

            if (pendingOverdueMinutes <= 0) pendingOverdueMinutes = 5;

            var paymentsBase = db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= from && p.CreatedAt < to);

            if (!string.IsNullOrWhiteSpace(provider))
                paymentsBase = paymentsBase.Where(p => p.Provider == provider!.Trim());

            if (!string.IsNullOrWhiteSpace(targetType))
                paymentsBase = paymentsBase.Where(p => p.TargetType == targetType!.Trim());

            // ===== Cohort counts (by CreatedAt in range) =====
            var createdCount = await paymentsBase.CountAsync(cancellationToken);

            var successCount = await paymentsBase.CountAsync(
                p => SuccessStatuses.Contains(p.Status ?? ""), cancellationToken);

            var pendingCount = await paymentsBase.CountAsync(
                p => PendingStatuses.Contains(p.Status ?? ""), cancellationToken);

            var timeoutCount = await paymentsBase.CountAsync(
                p => TimeoutStatuses.Contains(p.Status ?? ""), cancellationToken);

            var cancelledCount = await paymentsBase.CountAsync(
                p => CancelledStatuses.Contains(p.Status ?? ""), cancellationToken);

            var failedCount = await paymentsBase.CountAsync(
                p => FailedStatuses.Contains(p.Status ?? ""), cancellationToken);

            var refundedCount = await paymentsBase.CountAsync(
                p => RefundedStatuses.Contains(p.Status ?? ""), cancellationToken);

            var successRate = createdCount > 0 ? (decimal)successCount / createdCount : (decimal?)null;
            var timeoutRate = createdCount > 0 ? (decimal)timeoutCount / createdCount : (decimal?)null;
            var cancelRate = createdCount > 0 ? (decimal)cancelledCount / createdCount : (decimal?)null;
            var refundRate = createdCount > 0 ? (decimal)refundedCount / createdCount : (decimal?)null;

            // ===== Amounts (best-effort) =====
            var amountCollected = await paymentsBase
                .Where(p => SuccessStatuses.Contains(p.Status ?? ""))
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var refundAmount = await paymentsBase
                .Where(p => RefundedStatuses.Contains(p.Status ?? ""))
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            // ===== Pending overdue right now =====
            var overdueCutoff = nowUtc.AddMinutes(-pendingOverdueMinutes);
            var pendingOverdueCount = await paymentsBase.CountAsync(
                p => PendingStatuses.Contains(p.Status ?? "") && p.CreatedAt < overdueCutoff,
                cancellationToken);

            // ===== Time-to-pay metrics (use AuditLogs) =====
            var timeToPay = await ComputeTimeToPayStatsAsync(
                db, from, to,
                provider, targetType,
                cancellationToken);

            // ===== Alerts: compare successRate vs previous same-duration window =====
            var duration = to - from;
            var prevFrom = from - duration;
            var prevTo = from;

            var prevBase = db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= prevFrom && p.CreatedAt < prevTo);

            if (!string.IsNullOrWhiteSpace(provider))
                prevBase = prevBase.Where(p => p.Provider == provider!.Trim());

            if (!string.IsNullOrWhiteSpace(targetType))
                prevBase = prevBase.Where(p => p.TargetType == targetType!.Trim());

            var prevCreated = await prevBase.CountAsync(cancellationToken);
            var prevSuccess = await prevBase.CountAsync(p => SuccessStatuses.Contains(p.Status ?? ""), cancellationToken);
            var prevSuccessRate = prevCreated > 0 ? (decimal)prevSuccess / prevCreated : (decimal?)null;

            var alerts = BuildAlerts(
                createdCount, successRate, timeoutRate, cancelRate, pendingOverdueCount,
                prevSuccessRate,
                pendingOverdueMinutes);

            return Ok(new PaymentDashboardSummaryDto
            {
                RangeFromUtc = from,
                RangeToUtc = to,
                Provider = provider,
                TargetType = targetType,

                TotalPaymentsCreated = createdCount,
                TotalSuccessful = successCount,
                SuccessRate = successRate,

                TotalAmountCollected = amountCollected,

                TimeoutCount = timeoutCount,
                TimeoutRate = timeoutRate,

                CancelledCount = cancelledCount,
                CancelRate = cancelRate,

                FailedCount = failedCount,

                RefundedCount = refundedCount,
                RefundRate = refundRate,
                RefundAmount = refundAmount,

                PendingCount = pendingCount,
                PendingOverdueCount = pendingOverdueCount,
                PendingOverdueMinutes = pendingOverdueMinutes,

                MedianTimeToPaySeconds = timeToPay?.P50Seconds,
                P95TimeToPaySeconds = timeToPay?.P95Seconds,

                Alerts = alerts
            });
        }

        // ============================================================
        // 2) DAILY TRENDS
        // ============================================================
        [HttpGet("trends/daily")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<List<PaymentDailyTrendPointDto>>> GetDailyTrends(
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] int days = 30,
            [FromQuery] string? provider = null,
            [FromQuery] string? targetType = null,
            [FromQuery] int timezoneOffsetMinutes = 420,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;

            DateTime from;
            DateTime to;

            // ✅ Nếu FE gửi from/to => dùng đúng kỳ lọc (và không bị cap 180d nữa)
            if (fromUtc.HasValue || toUtc.HasValue)
            {
                var rng = NormalizeRangeUtc(nowUtc, fromUtc, toUtc);
                from = rng.from;
                to = rng.to;
            }
            else
            {
                if (days <= 0) days = 30;
                if (days > MaxRangeDays) days = MaxRangeDays;
                from = nowUtc.Date.AddDays(1 - days);
                to = nowUtc;
            }

            var paymentsBase = db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= from && p.CreatedAt < to);

            if (!string.IsNullOrWhiteSpace(provider))
                paymentsBase = paymentsBase.Where(p => p.Provider == provider!.Trim());

            if (!string.IsNullOrWhiteSpace(targetType))
                paymentsBase = paymentsBase.Where(p => p.TargetType == targetType!.Trim());

            var rows = await paymentsBase
                .GroupBy(p => new
                {
                    Day = p.CreatedAt.AddMinutes(timezoneOffsetMinutes).Date,
                    Status = p.Status
                })
                .Select(g => new DailyStatusAggRow
                {
                    Day = g.Key.Day,
                    Status = g.Key.Status,
                    Count = g.Count(),
                    AmountSuccess = g.Where(x => SuccessStatuses.Contains(x.Status ?? ""))
                                     .Sum(x => (decimal?)x.Amount) ?? 0m
                })
                .ToListAsync(cancellationToken);

            // ✅ tính dải ngày local đúng “to exclusive”
            var localFromDay = from.AddMinutes(timezoneOffsetMinutes).Date;
            var localTo = to.AddMinutes(timezoneOffsetMinutes);
            var localToInclusive = localTo.TimeOfDay == TimeSpan.Zero ? localTo.Date.AddDays(-1) : localTo.Date;

            if (localToInclusive < localFromDay) localToInclusive = localFromDay;

            var allDays = Enumerable.Range(0, (localToInclusive - localFromDay).Days + 1)
                .Select(i => localFromDay.AddDays(i))
                .ToList();

            var byDay = rows.GroupBy(x => x.Day).ToDictionary(g => g.Key, g => g.ToList());
            var result = new List<PaymentDailyTrendPointDto>(allDays.Count);

            foreach (var d in allDays)
            {
                byDay.TryGetValue(d, out var dayRows);
                dayRows ??= new List<DailyStatusAggRow>();

                int total = dayRows.Sum(x => x.Count);
                int success = dayRows.Where(x => SuccessStatuses.Contains((x.Status ?? "").Trim())).Sum(x => x.Count);

                var pending = SumStatuses(dayRows, PendingStatuses);
                var timeout = SumStatuses(dayRows, TimeoutStatuses);
                var cancelled = SumStatuses(dayRows, CancelledStatuses);
                var failed = SumStatuses(dayRows, FailedStatuses);

                var point = new PaymentDailyTrendPointDto
                {
                    LocalDate = d,
                    TotalCreated = total,
                    SuccessCount = success,
                    SuccessRate = total > 0 ? (decimal)success / total : (decimal?)null,

                    PendingCount = pending,
                    TimeoutCount = timeout,
                    CancelledCount = cancelled,
                    FailedCount = failed,

                    OtherCount = Math.Max(0, total - pending - timeout - cancelled - failed - success),

                    AmountCollected = dayRows.Sum(x => x.AmountSuccess)
                };

                result.Add(point);
            }

            return Ok(result);
        }

        // ============================================================
        // 3) TIME-TO-PAY
        // ============================================================
        [HttpGet("time-to-pay")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<PaymentTimeToPayDto>> GetTimeToPay(
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] string? provider = null,
            [FromQuery] string? targetType = null,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var (from, to) = NormalizeRangeUtc(nowUtc, fromUtc, toUtc);

            var stats = await ComputeTimeToPayStatsAsync(db, from, to, provider, targetType, cancellationToken);

            return Ok(stats ?? new PaymentTimeToPayDto
            {
                RangeFromUtc = from,
                RangeToUtc = to,
                TotalPaidEvents = 0,
                P50Seconds = null,
                P90Seconds = null,
                P95Seconds = null,
                Histogram = BuildDefaultHistogram()
            });
        }

        // ============================================================
        // 4) ATTEMPTS
        // ============================================================
        [HttpGet("attempts")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<PaymentAttemptsDto>> GetAttempts(
            [FromQuery] int days = 30,
            [FromQuery] string? provider = null,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;
            if (days > MaxRangeDays) days = MaxRangeDays;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var from = nowUtc.AddDays(-days);

            var q = db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= from && p.CreatedAt < nowUtc);

            if (!string.IsNullOrWhiteSpace(provider))
                q = q.Where(p => p.Provider == provider!.Trim());

            var perTarget = await q
                .Where(p => p.TargetType != null && p.TargetId != null)
                .GroupBy(p => (p.TargetType ?? "") + "|" + (p.TargetId ?? ""))
                .Select(g => new { Key = g.Key, AttemptCount = g.Count() })
                .ToListAsync(cancellationToken);

            int c1 = perTarget.Count(x => x.AttemptCount == 1);
            int c2 = perTarget.Count(x => x.AttemptCount == 2);
            int c3 = perTarget.Count(x => x.AttemptCount == 3);
            int c4p = perTarget.Count(x => x.AttemptCount >= 4);

            var dto = new PaymentAttemptsDto
            {
                RangeFromUtc = from,
                RangeToUtc = nowUtc,
                Provider = provider,
                TotalTargets = perTarget.Count,
                AttemptBuckets = new List<AttemptBucketDto>
                {
                    new AttemptBucketDto { Label = "1", Count = c1 },
                    new AttemptBucketDto { Label = "2", Count = c2 },
                    new AttemptBucketDto { Label = "3", Count = c3 },
                    new AttemptBucketDto { Label = "4+", Count = c4p },
                },
                TargetsWithAttemptGte3 = perTarget.Count(x => x.AttemptCount >= 3)
            };

            return Ok(dto);
        }

        // ============================================================
        // 5) HEATMAP
        // ============================================================
        [HttpGet("heatmap")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<PaymentHeatmapDto>> GetHeatmap(
            [FromQuery] int days = 30,
            [FromQuery] string? provider = null,
            [FromQuery] string metric = "success",          // "success" | "created"
            [FromQuery] int timezoneOffsetMinutes = 420,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;
            if (days > MaxRangeDays) days = MaxRangeDays;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var from = nowUtc.AddDays(-days);

            var q = db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= from && p.CreatedAt < nowUtc);

            if (!string.IsNullOrWhiteSpace(provider))
                q = q.Where(p => p.Provider == provider!.Trim());

            if (string.Equals(metric, "success", StringComparison.OrdinalIgnoreCase))
                q = q.Where(p => SuccessStatuses.Contains(p.Status ?? ""));

            var createdTimes = await q
                .Select(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            var matrix = new int[7, 24];

            foreach (var t in createdTimes)
            {
                var local = t.AddMinutes(timezoneOffsetMinutes);
                var dow = NormalizeDowMonFirst(local.DayOfWeek);
                var hour = local.Hour;
                matrix[dow, hour]++;
            }

            var rows = new List<HeatmapRowDto>();
            for (int d = 0; d < 7; d++)
            {
                var arr = new int[24];
                for (int h = 0; h < 24; h++) arr[h] = matrix[d, h];

                rows.Add(new HeatmapRowDto
                {
                    DayIndexMonFirst = d,
                    ValuesByHour = arr
                });
            }

            return Ok(new PaymentHeatmapDto
            {
                RangeFromUtc = from,
                RangeToUtc = nowUtc,
                Provider = provider,
                Metric = metric,
                TimezoneOffsetMinutes = timezoneOffsetMinutes,
                Rows = rows
            });
        }

        // ============================================================
        // 6) TOP FAILURE REASONS (Vietnamese mapping)
        // ============================================================
        [HttpGet("failure-reasons")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<List<ReasonCountDto>>> GetFailureReasons(
            [FromQuery] int days = 30,
            [FromQuery] string? provider = null,
            [FromQuery] string? targetType = null,
            [FromQuery] int top = 10,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;
            if (days > MaxRangeDays) days = MaxRangeDays;
            if (top <= 0) top = 10;
            if (top > 50) top = 50;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var from = nowUtc.AddDays(-days);

            var logs = await db.AuditLogs.AsNoTracking()
                .Where(a =>
                    a.OccurredAt >= from && a.OccurredAt < nowUtc
                    && a.EntityType == AuditEntityTypePayment
                    && a.Action == AuditActionPaymentStatusChanged
                    && a.AfterDataJson != null)
                .Select(a => new { a.EntityId, a.AfterDataJson })
                .ToListAsync(cancellationToken);

            // key: display label (Vietnamese), value: count + store a representative code
            var counts = new Dictionary<string, (int Count, string Code)>(StringComparer.OrdinalIgnoreCase);

            foreach (var l in logs)
            {
                if (!TryParseAfterJson(l.AfterDataJson!, out var after)) continue;

                // ✅ case-insensitive keys (provider/targetType/meta/reason)
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    if (!TryGetString(after, "provider", out var prov) || !string.Equals(prov, provider.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                if (!string.IsNullOrWhiteSpace(targetType))
                {
                    if (!TryGetString(after, "targetType", out var tt) || !string.Equals(tt, targetType.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var raw = ExtractReason(after) ?? "Unknown";

                // ✅ Nếu raw là status (Timeout/Cancelled/Failed/...), đổi sang tiếng Việt để hiển thị
                var label = ToVietnameseFailureReason(raw, out var code);

                if (!counts.TryGetValue(label, out var cur))
                {
                    counts[label] = (1, code);
                }
                else
                {
                    counts[label] = (cur.Count + 1, cur.Code);
                }
            }

            // Add "Timeout" bucket from Payments.Status (vì TimeoutService chưa audit)
            var timeoutFromPayments = await db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= from && p.CreatedAt < nowUtc
                            && TimeoutStatuses.Contains(p.Status ?? "")
                            && (string.IsNullOrWhiteSpace(provider) || p.Provider == provider!.Trim())
                            && (string.IsNullOrWhiteSpace(targetType) || p.TargetType == targetType!.Trim()))
                .CountAsync(cancellationToken);

            if (timeoutFromPayments > 0)
            {
                var label = ToVietnameseFailureReason("Timeout", out var code);
                if (!counts.TryGetValue(label, out var cur))
                    counts[label] = (timeoutFromPayments, code);
                else
                    counts[label] = (cur.Count + timeoutFromPayments, cur.Code);
            }

            var result = counts
                .OrderByDescending(x => x.Value.Count)
                .Take(top)
                .Select(x => new ReasonCountDto
                {
                    Code = x.Value.Code,
                    Reason = x.Key,
                    Count = x.Value.Count
                })
                .ToList();

            return Ok(result);
        }

        // ============================================================
        // Helpers
        // ============================================================
        private sealed class DailyStatusAggRow
        {
            public DateTime Day { get; set; }
            public string? Status { get; set; }
            public int Count { get; set; }
            public decimal AmountSuccess { get; set; }
        }

        private static (DateTime from, DateTime to) NormalizeRangeUtc(DateTime nowUtc, DateTime? fromUtc, DateTime? toUtc)
        {
            var to = toUtc.HasValue ? EnsureUtc(toUtc.Value) : nowUtc;
            var from = fromUtc.HasValue ? EnsureUtc(fromUtc.Value) : to.AddDays(-7);

            if (from > to)
            {
                var tmp = from;
                from = to;
                to = tmp;
            }

            // ✅ cap theo FE (thay vì 180 ngày)
            if ((to - from).TotalDays > MaxRangeDays)
                from = to.AddDays(-MaxRangeDays);

            return (from, to);
        }

        private static DateTime EnsureUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc) return dt;
            if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        private static int NormalizeDowMonFirst(DayOfWeek dow)
        {
            return dow switch
            {
                DayOfWeek.Monday => 0,
                DayOfWeek.Tuesday => 1,
                DayOfWeek.Wednesday => 2,
                DayOfWeek.Thursday => 3,
                DayOfWeek.Friday => 4,
                DayOfWeek.Saturday => 5,
                _ => 6, // Sunday
            };
        }

        private static int SumStatuses(IEnumerable<DailyStatusAggRow> rows, IEnumerable<string> statuses)
        {
            var set = new HashSet<string>(statuses.Select(s => (s ?? "").Trim()), StringComparer.OrdinalIgnoreCase);
            return rows.Where(x => set.Contains((x.Status ?? "").Trim()))
                       .Sum(x => x.Count);
        }

        private static string ToViStatusOrKeep(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return StatusViMap["Unknown"];
            return StatusViMap.TryGetValue(status.Trim(), out var vi) ? vi : status.Trim();
        }

        // ✅ Map “reason” hiển thị: nếu reason là status -> tiếng Việt; nếu không thì giữ nguyên (nhưng chuẩn hóa Unknown)
        private static string ToVietnameseFailureReason(string raw, out string code)
        {
            code = (raw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                code = "Unknown";
                return StatusViMap["Unknown"];
            }

            // nếu raw đúng là status
            if (StatusViMap.TryGetValue(code, out var vi))
                return vi;

            // vài trường hợp hay gặp: "Cancel" / "Canceled" / "Timeout" viết thường...
            var norm = code.Replace("_", " ").Trim();

            if (string.Equals(norm, "Canceled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(norm, "Canceled by user", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(norm, "Cancel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(norm, "CancelFromReturn", StringComparison.OrdinalIgnoreCase))
            {
                code = "Cancelled";
                return StatusViMap["Cancelled"];
            }

            if (string.Equals(norm, "Time out", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(norm, "Timed out", StringComparison.OrdinalIgnoreCase))
            {
                code = "Timeout";
                return StatusViMap["Timeout"];
            }

            if (string.Equals(norm, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                code = "Unknown";
                return StatusViMap["Unknown"];
            }

            // không map được => giữ nguyên (nếu meta.reason đã là tiếng Việt thì OK)
            return code;
        }

        private static List<DashboardAlertDto> BuildAlerts(
            int createdCount,
            decimal? successRate,
            decimal? timeoutRate,
            decimal? cancelRate,
            int pendingOverdueCount,
            decimal? prevSuccessRate,
            int pendingOverdueMinutes)
        {
            var alerts = new List<DashboardAlertDto>();

            // Giảm tỷ lệ thành công
            if (successRate.HasValue && prevSuccessRate.HasValue)
            {
                var delta = successRate.Value - prevSuccessRate.Value;
                if (delta <= -0.10m)
                {
                    alerts.Add(new DashboardAlertDto
                    {
                        Severity = "Warning",
                        Code = "SUCCESS_RATE_DROP",
                        Message = $"Tỷ lệ thanh toán thành công giảm {(Math.Abs(delta) * 100m):0.#}% so với kỳ trước."
                    });
                }
            }

            // Tỷ lệ hết hạn cao
            if (timeoutRate.HasValue && timeoutRate.Value >= 0.20m && createdCount >= 20)
            {
                alerts.Add(new DashboardAlertDto
                {
                    Severity = "Warning",
                    Code = "TIMEOUT_HIGH",
                    Message = $"Tỷ lệ giao dịch hết hạn cao ({(timeoutRate.Value * 100m):0.#}%). Hãy kiểm tra thời gian timeout, webhook PayOS và luồng thanh toán."
                });
            }

            // Tỷ lệ hủy cao
            if (cancelRate.HasValue && cancelRate.Value >= 0.25m && createdCount >= 20)
            {
                alerts.Add(new DashboardAlertDto
                {
                    Severity = "Info",
                    Code = "CANCEL_HIGH",
                    Message = $"Tỷ lệ hủy cao ({(cancelRate.Value * 100m):0.#}%). Có thể liên quan trải nghiệm người dùng, giá hoặc điều hướng."
                });
            }

            // Chờ quá hạn
            if (pendingOverdueCount >= 5)
            {
                alerts.Add(new DashboardAlertDto
                {
                    Severity = "Error",
                    Code = "PENDING_OVERDUE",
                    Message = $"Có {pendingOverdueCount} giao dịch đang chờ quá {pendingOverdueMinutes} phút. Có thể do webhook chưa về, lỗi đồng bộ trạng thái hoặc người dùng thoát giữa chừng."
                });
            }

            return alerts;
        }

        private static List<TimeBucketDto> BuildDefaultHistogram()
        {
            return new List<TimeBucketDto>
            {
                new TimeBucketDto { Label = "0–1m", FromSeconds = 0, ToSeconds = 60, Count = 0 },
                new TimeBucketDto { Label = "1–3m", FromSeconds = 60, ToSeconds = 180, Count = 0 },
                new TimeBucketDto { Label = "3–5m", FromSeconds = 180, ToSeconds = 300, Count = 0 },
                new TimeBucketDto { Label = "5–10m", FromSeconds = 300, ToSeconds = 600, Count = 0 },
                new TimeBucketDto { Label = ">10m", FromSeconds = 600, ToSeconds = null, Count = 0 },
            };
        }

        private async Task<PaymentTimeToPayDto?> ComputeTimeToPayStatsAsync(
            KeytietkiemDbContext db,
            DateTime fromUtc,
            DateTime toUtc,
            string? provider,
            string? targetType,
            CancellationToken ct)
        {
            // ✅ FE dùng provider có thể null ("ALL") => không filter
            // ✅ AuditLogs JSON key có thể PascalCase => parse + get property case-insensitive
            // ✅ Lấy earliest "success" event cho mỗi paymentId trong kỳ

            var logs = await db.AuditLogs.AsNoTracking()
                .Where(a =>
                    a.OccurredAt >= fromUtc && a.OccurredAt < toUtc
                    && a.EntityType == AuditEntityTypePayment
                    && a.Action == AuditActionPaymentStatusChanged
                    && a.AfterDataJson != null
                    && (
                        a.AfterDataJson.Contains("\"status\":\"Paid\"") ||
                        a.AfterDataJson.Contains("\"Status\":\"Paid\"") ||
                        a.AfterDataJson.Contains("\"status\":\"Success\"") ||
                        a.AfterDataJson.Contains("\"Status\":\"Success\"") ||
                        a.AfterDataJson.Contains("\"status\":\"Completed\"") ||
                        a.AfterDataJson.Contains("\"Status\":\"Completed\"")
                    ))
                .Select(a => new { a.EntityId, a.OccurredAt, a.AfterDataJson })
                .ToListAsync(ct);

            if (logs.Count == 0) return null;

            var paidAt = new Dictionary<Guid, DateTime>();

            foreach (var l in logs)
            {
                if (!TryParseAfterJson(l.AfterDataJson!, out var after)) continue;

                if (!TryGetString(after, "status", out var st)) continue;
                var stNorm = (st ?? "").Trim();
                if (!SuccessStatuses.Contains(stNorm, StringComparer.OrdinalIgnoreCase)) continue;

                Guid pid;
                if (!Guid.TryParse(l.EntityId, out pid))
                {
                    if (!TryGetGuid(after, "paymentId", out pid))
                        continue;
                }

                if (paidAt.TryGetValue(pid, out var existed))
                {
                    if (l.OccurredAt < existed) paidAt[pid] = l.OccurredAt;
                }
                else
                {
                    paidAt[pid] = l.OccurredAt;
                }
            }

            if (paidAt.Count == 0) return null;

            // ✅ Query Payments theo danh sách PaymentId để lấy CreatedAt + filter theo provider/targetType (không phụ thuộc audit json)
            var ids = paidAt.Keys.ToList();
            var createdDict = new Dictionary<Guid, DateTime>(ids.Count);

            // chunk để tránh IN quá lớn
            const int ChunkSize = 2000;
            for (int i = 0; i < ids.Count; i += ChunkSize)
            {
                var chunk = ids.Skip(i).Take(ChunkSize).ToList();

                var q = db.Payments.AsNoTracking()
                    .Where(p => chunk.Contains(p.PaymentId));

                if (!string.IsNullOrWhiteSpace(provider))
                    q = q.Where(p => p.Provider == provider!.Trim());

                if (!string.IsNullOrWhiteSpace(targetType))
                    q = q.Where(p => p.TargetType == targetType!.Trim());

                var rows = await q
                    .Select(p => new { p.PaymentId, p.CreatedAt })
                    .ToListAsync(ct);

                foreach (var r in rows)
                {
                    createdDict[r.PaymentId] = r.CreatedAt;
                }
            }

            var seconds = new List<int>(createdDict.Count);

            foreach (var kv in paidAt)
            {
                if (!createdDict.TryGetValue(kv.Key, out var createdAtUtc)) continue;
                var s = (int)Math.Max(0, (kv.Value - createdAtUtc).TotalSeconds);
                seconds.Add(s);
            }

            if (seconds.Count == 0) return null;

            seconds.Sort();

            var p50 = PercentileSeconds(seconds, 0.50);
            var p90 = PercentileSeconds(seconds, 0.90);
            var p95 = PercentileSeconds(seconds, 0.95);

            var histogram = BuildDefaultHistogram();
            foreach (var s in seconds)
            {
                var b = histogram.FirstOrDefault(x =>
                    x.ToSeconds.HasValue
                        ? (s >= x.FromSeconds && s < x.ToSeconds.Value)
                        : (s >= x.FromSeconds));

                if (b != null) b.Count++;
            }

            return new PaymentTimeToPayDto
            {
                RangeFromUtc = fromUtc,
                RangeToUtc = toUtc,
                TotalPaidEvents = seconds.Count,
                P50Seconds = p50,
                P90Seconds = p90,
                P95Seconds = p95,
                Histogram = histogram
            };
        }

        private static int PercentileSeconds(List<int> sortedSeconds, double p)
        {
            if (sortedSeconds == null || sortedSeconds.Count == 0) return 0;
            if (p <= 0) return sortedSeconds[0];
            if (p >= 1) return sortedSeconds[^1];

            var n = sortedSeconds.Count;
            var idx = (p * (n - 1));
            var lo = (int)Math.Floor(idx);
            var hi = (int)Math.Ceiling(idx);
            if (lo == hi) return sortedSeconds[lo];

            var w = idx - lo;
            return (int)Math.Round(sortedSeconds[lo] * (1 - w) + sortedSeconds[hi] * w);
        }

        private static bool TryParseAfterJson(string json, out JsonElement root)
        {
            root = default;
            try
            {
                using var doc = JsonDocument.Parse(json);
                root = doc.RootElement.Clone();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ✅ Case-insensitive property getter (để khớp audit json PascalCase/camelCase)
        private static bool TryGetPropertyCaseInsensitive(JsonElement root, string prop, out JsonElement value)
        {
            value = default;
            try
            {
                if (root.ValueKind != JsonValueKind.Object) return false;

                // fast path (exact)
                if (root.TryGetProperty(prop, out value)) return true;

                // case-insensitive scan
                foreach (var p in root.EnumerateObject())
                {
                    if (string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase))
                    {
                        value = p.Value;
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetString(JsonElement root, string prop, out string? value)
        {
            value = null;
            try
            {
                if (!TryGetPropertyCaseInsensitive(root, prop, out var el)) return false;
                if (el.ValueKind == JsonValueKind.String)
                {
                    value = el.GetString();
                    return true;
                }
                value = el.ToString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetGuid(JsonElement root, string prop, out Guid value)
        {
            value = Guid.Empty;
            try
            {
                if (!TryGetPropertyCaseInsensitive(root, prop, out var el)) return false;
                var s = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
                return Guid.TryParse(s, out value);
            }
            catch
            {
                return false;
            }
        }

        private static string? ExtractReason(JsonElement afterRoot)
        {
            try
            {
                if (!TryGetPropertyCaseInsensitive(afterRoot, "meta", out var meta)) return null;
                if (meta.ValueKind != JsonValueKind.Object) return null;
                if (!TryGetPropertyCaseInsensitive(meta, "reason", out var r)) return null;
                return r.ValueKind == JsonValueKind.String ? r.GetString() : r.ToString();
            }
            catch
            {
                return null;
            }
        }
    }

    // ============================================================
    // DTOs
    // ============================================================
    public class PaymentDashboardSummaryDto
    {
        public DateTime RangeFromUtc { get; set; }
        public DateTime RangeToUtc { get; set; }
        public string? Provider { get; set; }
        public string? TargetType { get; set; }

        public int TotalPaymentsCreated { get; set; }

        public int TotalSuccessful { get; set; }
        public decimal? SuccessRate { get; set; }

        public decimal TotalAmountCollected { get; set; }

        public int TimeoutCount { get; set; }
        public decimal? TimeoutRate { get; set; }

        public int CancelledCount { get; set; }
        public decimal? CancelRate { get; set; }

        public int FailedCount { get; set; }

        public int RefundedCount { get; set; }
        public decimal? RefundRate { get; set; }
        public decimal RefundAmount { get; set; }

        public int PendingCount { get; set; }
        public int PendingOverdueCount { get; set; }
        public int PendingOverdueMinutes { get; set; }

        public int? MedianTimeToPaySeconds { get; set; }
        public int? P95TimeToPaySeconds { get; set; }

        public List<DashboardAlertDto> Alerts { get; set; } = new();
    }

    public class DashboardAlertDto
    {
        public string Severity { get; set; } = "Info"; // Info/Warning/Error
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public class PaymentDailyTrendPointDto
    {
        public DateTime LocalDate { get; set; }
        public int TotalCreated { get; set; }

        public int SuccessCount { get; set; }
        public decimal? SuccessRate { get; set; }

        public int PendingCount { get; set; }
        public int TimeoutCount { get; set; }
        public int CancelledCount { get; set; }
        public int FailedCount { get; set; }
        public int OtherCount { get; set; }

        public decimal AmountCollected { get; set; }
    }

    public class PaymentTimeToPayDto
    {
        public DateTime RangeFromUtc { get; set; }
        public DateTime RangeToUtc { get; set; }

        public int TotalPaidEvents { get; set; }

        public int? P50Seconds { get; set; }
        public int? P90Seconds { get; set; }
        public int? P95Seconds { get; set; }

        public List<TimeBucketDto> Histogram { get; set; } = new();
    }

    public class TimeBucketDto
    {
        public string Label { get; set; } = "";
        public int FromSeconds { get; set; }
        public int? ToSeconds { get; set; }
        public int Count { get; set; }
    }

    public class PaymentAttemptsDto
    {
        public DateTime RangeFromUtc { get; set; }
        public DateTime RangeToUtc { get; set; }
        public string? Provider { get; set; }

        public int TotalTargets { get; set; }
        public int TargetsWithAttemptGte3 { get; set; }

        public List<AttemptBucketDto> AttemptBuckets { get; set; } = new();
    }

    public class AttemptBucketDto
    {
        public string Label { get; set; } = "";
        public int Count { get; set; }
    }

    public class PaymentHeatmapDto
    {
        public DateTime RangeFromUtc { get; set; }
        public DateTime RangeToUtc { get; set; }
        public string? Provider { get; set; }
        public string Metric { get; set; } = "success";
        public int TimezoneOffsetMinutes { get; set; } = 420;

        public List<HeatmapRowDto> Rows { get; set; } = new();
    }

    public class HeatmapRowDto
    {
        public int DayIndexMonFirst { get; set; }
        public int[] ValuesByHour { get; set; } = Array.Empty<int>();
    }

    public class ReasonCountDto
    {
        // ✅ Code gốc (Timeout/Cancelled/...) để debug/filter nếu cần
        public string Code { get; set; } = "";

        // ✅ Nhãn hiển thị thuần Việt (Hết hạn thanh toán/Đã hủy/...)
        public string Reason { get; set; } = "";

        public int Count { get; set; }
    }
}
