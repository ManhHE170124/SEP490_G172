// File: Controllers/PaymentsDashboardAdminController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Attributes;
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
    /// - KPI cards: created/success/timeout/cancel/refund, success rate, pending overdue, median+p95 time-to-pay
    /// - Charts: daily status stacked, success rate daily, amount daily, attempts distribution, heatmap, top failure reasons
    ///
    /// Data source:
    /// - Payments table (CreatedAt/Status/Amount/TargetType/TargetId/Provider...)
    /// - AuditLogs (EntityType=Payment, Action=PaymentStatusChanged) để:
    ///     + tính PaidAt (OccurredAt) => time-to-pay distribution + percentiles
    ///     + lấy meta.reason => Top Failure Reasons
    ///
    /// Lưu ý:
    /// - PaymentTimeoutService hiện chưa log Audit => "Timeout reason" chủ yếu suy ra từ Payments.Status.
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
        private const string StatusPending = "Pending";
        private const string StatusCancelled = "Cancelled";
        private const string StatusTimeout = "Timeout";
        private const string StatusFailed = "Failed";
        private const string StatusRefunded = "Refunded"; // nếu DB có

        // AuditLogs pattern
        private const string AuditEntityTypePayment = "Payment";
        private const string AuditActionPaymentStatusChanged = "PaymentStatusChanged";

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
            [FromQuery] string? provider = "PayOS",
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
                p => SuccessStatuses.Contains(p.Status!), cancellationToken);

            var pendingCount = await paymentsBase.CountAsync(
                p => p.Status == StatusPending, cancellationToken);

            var timeoutCount = await paymentsBase.CountAsync(
                p => p.Status == StatusTimeout, cancellationToken);

            var cancelledCount = await paymentsBase.CountAsync(
                p => p.Status == StatusCancelled, cancellationToken);

            var failedCount = await paymentsBase.CountAsync(
                p => p.Status == StatusFailed, cancellationToken);

            var refundedCount = await paymentsBase.CountAsync(
                p => p.Status == StatusRefunded, cancellationToken);

            var successRate = createdCount > 0 ? (decimal)successCount / createdCount : (decimal?)null;
            var timeoutRate = createdCount > 0 ? (decimal)timeoutCount / createdCount : (decimal?)null;
            var cancelRate = createdCount > 0 ? (decimal)cancelledCount / createdCount : (decimal?)null;
            var refundRate = createdCount > 0 ? (decimal)refundedCount / createdCount : (decimal?)null;

            // ===== Amounts (best-effort) =====
            // amountCollected: sum amount của payments success trong cohort (CreatedAt range)
            var amountCollected = await paymentsBase
                .Where(p => SuccessStatuses.Contains(p.Status!))
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var refundAmount = await paymentsBase
                .Where(p => p.Status == StatusRefunded)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            // ===== Pending overdue right now (still pending and older than X minutes) =====
            var overdueCutoff = nowUtc.AddMinutes(-pendingOverdueMinutes);
            var pendingOverdueCount = await paymentsBase.CountAsync(
                p => p.Status == StatusPending && p.CreatedAt < overdueCutoff,
                cancellationToken);

            // ===== Time-to-pay metrics (use AuditLogs) =====
            // We compute for "payments that got Paid within range" (PaidAt in [from,to)).
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
            var prevSuccess = await prevBase.CountAsync(p => SuccessStatuses.Contains(p.Status!), cancellationToken);
            var prevSuccessRate = prevCreated > 0 ? (decimal)prevSuccess / prevCreated : (decimal?)null;

            var alerts = BuildAlerts(
                nowUtc,
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
        // 2) DAILY TRENDS (stacked by status + success rate + amount)
        // ============================================================
        // GET: /api/payments-dashboard-admin/trends/daily?days=30&timezoneOffsetMinutes=420
        [HttpGet("trends/daily")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<List<PaymentDailyTrendPointDto>>> GetDailyTrends(
            [FromQuery] int days = 30,
            [FromQuery] string? provider = "PayOS",
            [FromQuery] string? targetType = null,
            [FromQuery] int timezoneOffsetMinutes = 420,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;
            if (days > 366) days = 366;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var from = nowUtc.Date.AddDays(1 - days); // start at 00:00 UTC
            var to = nowUtc;

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
         AmountSuccess = g.Where(x => SuccessStatuses.Contains(x.Status!))
                          .Sum(x => (decimal?)x.Amount) ?? 0m
     })
     .ToListAsync(cancellationToken);


            // Build full day series
            var localFromDay = from.AddMinutes(timezoneOffsetMinutes).Date;
            var localToDay = to.AddMinutes(timezoneOffsetMinutes).Date;

            var allDays = Enumerable.Range(0, (localToDay - localFromDay).Days + 1)
                .Select(i => localFromDay.AddDays(i))
                .ToList();

            var byDay = rows.GroupBy(x => x.Day).ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<PaymentDailyTrendPointDto>(allDays.Count);

            foreach (var d in allDays)
            {
                byDay.TryGetValue(d, out var dayRows);
                dayRows ??= new List<DailyStatusAggRow>();


                int total = dayRows.Sum(x => (int)x.Count);
                int success = dayRows.Where(x => SuccessStatuses.Contains((string?)x.Status ?? "")).Sum(x => (int)x.Count);

                var point = new PaymentDailyTrendPointDto
                {
                    LocalDate = d,
                    TotalCreated = total,
                    SuccessCount = success,
                    SuccessRate = total > 0 ? (decimal)success / total : (decimal?)null,

                    PendingCount = SumStatus(dayRows, StatusPending),
                    TimeoutCount = SumStatus(dayRows, StatusTimeout),
                    CancelledCount = SumStatus(dayRows, StatusCancelled),
                    FailedCount = SumStatus(dayRows, StatusFailed),

                    OtherCount = total
                        - SumStatus(dayRows, StatusPending)
                        - SumStatus(dayRows, StatusTimeout)
                        - SumStatus(dayRows, StatusCancelled)
                        - SumStatus(dayRows, StatusFailed)
                        - success,

                    AmountCollected = dayRows.Sum(x => x.AmountSuccess)

                };

                result.Add(point);
            }

            return Ok(result);
        }

        // ============================================================
        // 3) TIME-TO-PAY (histogram + percentiles) - via AuditLogs
        // ============================================================
        // GET: /api/payments-dashboard-admin/time-to-pay?fromUtc=...&toUtc=...
        [HttpGet("time-to-pay")]
        [RequireRole(RoleCodes.ADMIN)] 
        public async Task<ActionResult<PaymentTimeToPayDto>> GetTimeToPay(
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] string? provider = "PayOS",
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
        // 4) ATTEMPTS DISTRIBUTION (attempt count per Target)
        // ============================================================
        // GET: /api/payments-dashboard-admin/attempts?days=30
        [HttpGet("attempts")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<PaymentAttemptsDto>> GetAttempts(
            [FromQuery] int days = 30,
            [FromQuery] string? provider = "PayOS",
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;
            if (days > 366) days = 366;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var from = nowUtc.AddDays(-days);

            var q = db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= from && p.CreatedAt < nowUtc);

            if (!string.IsNullOrWhiteSpace(provider))
                q = q.Where(p => p.Provider == provider!.Trim());

            // Attempt per (TargetType|TargetId)
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
        // 5) HEATMAP (hour x day-of-week)
        // ============================================================
        // GET: /api/payments-dashboard-admin/heatmap?days=30&metric=success|created
        [HttpGet("heatmap")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<PaymentHeatmapDto>> GetHeatmap(
            [FromQuery] int days = 30,
            [FromQuery] string? provider = "PayOS",
            [FromQuery] string metric = "success",          // "success" | "created"
            [FromQuery] int timezoneOffsetMinutes = 420,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;
            if (days > 366) days = 366;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var from = nowUtc.AddDays(-days);

            var q = db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= from && p.CreatedAt < nowUtc);

            if (!string.IsNullOrWhiteSpace(provider))
                q = q.Where(p => p.Provider == provider!.Trim());

            if (string.Equals(metric, "success", StringComparison.OrdinalIgnoreCase))
                q = q.Where(p => SuccessStatuses.Contains(p.Status!));

            // Pull minimal createdAt
            var createdTimes = await q
                .Select(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            // Matrix: 7 (Mon..Sun) x 24
            var matrix = new int[7, 24];

            foreach (var t in createdTimes)
            {
                var local = t.AddMinutes(timezoneOffsetMinutes);
                var dow = NormalizeDowMonFirst(local.DayOfWeek); // 0=Mon..6=Sun
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
        // 6) TOP FAILURE REASONS (from AuditLogs meta.reason)
        // ============================================================
        // GET: /api/payments-dashboard-admin/failure-reasons?days=30
        [HttpGet("failure-reasons")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<List<ReasonCountDto>>> GetFailureReasons(
            [FromQuery] int days = 30,
            [FromQuery] string? provider = "PayOS",
            [FromQuery] string? targetType = null,
            [FromQuery] int top = 10,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;
            if (days > 366) days = 366;
            if (top <= 0) top = 10;
            if (top > 50) top = 50;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var from = nowUtc.AddDays(-days);

            // Query audit logs in range
            var logs = await db.AuditLogs.AsNoTracking()
                .Where(a =>
                    a.OccurredAt >= from && a.OccurredAt < nowUtc
                    && a.EntityType == AuditEntityTypePayment
                    && a.Action == AuditActionPaymentStatusChanged
                    && a.AfterDataJson != null)
                .Select(a => new { a.EntityId, a.AfterDataJson })
                .ToListAsync(cancellationToken);

            // Optional: filter only PayOS/targetType by checking afterJson fields
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var l in logs)
            {
                if (!TryParseAfterJson(l.AfterDataJson!, out var after)) continue;

                // provider filter
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    if (!TryGetString(after, "provider", out var prov) || !string.Equals(prov, provider.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // targetType filter
                if (!string.IsNullOrWhiteSpace(targetType))
                {
                    if (!TryGetString(after, "targetType", out var tt) || !string.Equals(tt, targetType.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // reason
                var reason = ExtractReason(after) ?? "Unknown";
                if (!counts.ContainsKey(reason)) counts[reason] = 0;
                counts[reason]++;
            }

            // Add "Timeout" bucket from Payments.Status (vì TimeoutService chưa audit)
            var timeoutFromPayments = await db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= from && p.CreatedAt < nowUtc
                            && p.Status == StatusTimeout
                            && (string.IsNullOrWhiteSpace(provider) || p.Provider == provider!.Trim())
                            && (string.IsNullOrWhiteSpace(targetType) || p.TargetType == targetType!.Trim()))
                .CountAsync(cancellationToken);

            if (timeoutFromPayments > 0)
            {
                if (!counts.ContainsKey("Timeout")) counts["Timeout"] = 0;
                counts["Timeout"] += timeoutFromPayments;
            }

            var result = counts
                .OrderByDescending(x => x.Value)
                .Take(top)
                .Select(x => new ReasonCountDto { Reason = x.Key, Count = x.Value })
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

            // swap if inverted
            if (from > to)
            {
                var tmp = from;
                from = to;
                to = tmp;
            }

            // cap overly large ranges (avoid huge dashboard reads by accident)
            if ((to - from).TotalDays > 180)
                from = to.AddDays(-180);

            return (from, to);
        }

        private static DateTime EnsureUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc) return dt;
            if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();
            // Unspecified => treat as UTC to avoid timezone bugs
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        private static int NormalizeDowMonFirst(DayOfWeek dow)
        {
            // DayOfWeek: Sunday=0..Saturday=6
            // want Mon=0..Sun=6
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

        private static int SumStatus(IEnumerable<DailyStatusAggRow> rows, string status)
        {
            return rows.Where(x => string.Equals(x.Status ?? "", status, StringComparison.OrdinalIgnoreCase))
                       .Sum(x => x.Count);
        }


        private static List<DashboardAlertDto> BuildAlerts(
            DateTime nowUtc,
            int createdCount,
            decimal? successRate,
            decimal? timeoutRate,
            decimal? cancelRate,
            int pendingOverdueCount,
            decimal? prevSuccessRate,
            int pendingOverdueMinutes)
        {
            var alerts = new List<DashboardAlertDto>();

            // Success rate drop
            if (successRate.HasValue && prevSuccessRate.HasValue)
            {
                var delta = successRate.Value - prevSuccessRate.Value;
                if (delta <= -0.10m) // drop >= 10%
                {
                    alerts.Add(new DashboardAlertDto
                    {
                        Severity = "Warning",
                        Code = "SUCCESS_RATE_DROP",
                        Message = $"Success rate giảm {(Math.Abs(delta) * 100m):0.#}% so với kỳ trước."
                    });
                }
            }

            // Timeout spike
            if (timeoutRate.HasValue && timeoutRate.Value >= 0.20m && createdCount >= 20)
            {
                alerts.Add(new DashboardAlertDto
                {
                    Severity = "Warning",
                    Code = "TIMEOUT_HIGH",
                    Message = $"Timeout rate cao ({(timeoutRate.Value * 100m):0.#}%). Kiểm tra timeout window/webhook/luồng checkout."
                });
            }

            // Cancel spike
            if (cancelRate.HasValue && cancelRate.Value >= 0.25m && createdCount >= 20)
            {
                alerts.Add(new DashboardAlertDto
                {
                    Severity = "Info",
                    Code = "CANCEL_HIGH",
                    Message = $"Cancel rate cao ({(cancelRate.Value * 100m):0.#}%). Có thể do UX/giá/điều hướng."
                });
            }

            // Pending overdue
            if (pendingOverdueCount >= 5)
            {
                alerts.Add(new DashboardAlertDto
                {
                    Severity = "Error",
                    Code = "PENDING_OVERDUE",
                    Message = $"Có {pendingOverdueCount} payment Pending quá {pendingOverdueMinutes} phút. Nghi webhook không về / mapping lỗi / user thoát."
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
            // 1) load paid events from AuditLogs in range
            var paidLogs = await db.AuditLogs.AsNoTracking()
                .Where(a =>
                    a.OccurredAt >= fromUtc && a.OccurredAt < toUtc
                    && a.EntityType == AuditEntityTypePayment
                    && a.Action == AuditActionPaymentStatusChanged
                    && a.AfterDataJson != null
                    && a.AfterDataJson.Contains("\"status\":\"Paid\"")) // quick filter
                .Select(a => new { a.EntityId, a.OccurredAt, a.AfterDataJson })
                .ToListAsync(ct);

            if (paidLogs.Count == 0)
            {
                return null;
            }

            // 2) build PaidAt dict (min OccurredAt per payment)
            var paidAt = new Dictionary<Guid, DateTime>();

            foreach (var l in paidLogs)
            {
                if (!TryParseAfterJson(l.AfterDataJson!, out var after)) continue;

                // strict status check
                if (!TryGetString(after, "status", out var st) || !string.Equals(st, "Paid", StringComparison.OrdinalIgnoreCase))
                    continue;

                // provider filter (inside afterJson)
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    if (!TryGetString(after, "provider", out var prov) || !string.Equals(prov, provider.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // targetType filter (inside afterJson)
                if (!string.IsNullOrWhiteSpace(targetType))
                {
                    if (!TryGetString(after, "targetType", out var tt) || !string.Equals(tt, targetType.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // paymentId from audit EntityId OR from afterJson
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

            // 3) load createdAt for those payments (avoid IN 2100-limit by loading via CreatedAt range widen)
            // We load Payments created in [from-7d, to] to cover “created earlier, paid now” typical cases.
            var createdLookupFrom = fromUtc.AddDays(-7);
            var payments = await db.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= createdLookupFrom && p.CreatedAt < toUtc)
                .Select(p => new { p.PaymentId, p.CreatedAt })
                .ToListAsync(ct);

            var createdDict = payments.ToDictionary(x => x.PaymentId, x => x.CreatedAt);

            // 4) compute time-to-pay seconds
            var seconds = new List<int>(paidAt.Count);

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

            // nearest-rank with simple interpolation for smoother result
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

        private static bool TryGetString(JsonElement root, string prop, out string? value)
        {
            value = null;
            try
            {
                if (!root.TryGetProperty(prop, out var el)) return false;
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
                if (!root.TryGetProperty(prop, out var el)) return false;
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
                if (!afterRoot.TryGetProperty("meta", out var meta)) return null;
                if (meta.ValueKind != JsonValueKind.Object) return null;
                if (!meta.TryGetProperty("reason", out var r)) return null;
                return r.ValueKind == JsonValueKind.String ? r.GetString() : r.ToString();
            }
            catch
            {
                return null;
            }
        }
    }

    // ============================================================
    // DTOs (keep in same file for quick copy/paste)
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
        public DateTime LocalDate { get; set; } // date-only in local time (stored as DateTime.Date)
        public int TotalCreated { get; set; }

        public int SuccessCount { get; set; }
        public decimal? SuccessRate { get; set; }

        public int PendingCount { get; set; }
        public int TimeoutCount { get; set; }
        public int CancelledCount { get; set; }
        public int FailedCount { get; set; }
        public int OtherCount { get; set; }

        public decimal AmountCollected { get; set; } // sum amount of success cohort for that day
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
        public int? ToSeconds { get; set; } // null => infinity
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

        // 0=Mon..6=Sun, each has 24 values
        public List<HeatmapRowDto> Rows { get; set; } = new();
    }

    public class HeatmapRowDto
    {
        public int DayIndexMonFirst { get; set; }
        public int[] ValuesByHour { get; set; } = Array.Empty<int>();
    }

    public class ReasonCountDto
    {
        public string Reason { get; set; } = "";
        public int Count { get; set; }
    }
}
