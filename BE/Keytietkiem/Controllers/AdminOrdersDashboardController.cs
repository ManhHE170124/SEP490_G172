// File: Controllers/AdminOrdersDashboardController.cs
using Keytietkiem.Constants;
using Keytietkiem.Models;
using Keytietkiem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    /// <summary>
    /// Dashboard đơn hàng (Admin)
    /// - bucket=auto để tránh chart quá dài khi chọn range lớn (vd cả năm).
    /// - Trả dữ liệu tối giản: KPI + Trend + Breakdown theo trạng thái
    /// - Bestsellers: TopProducts + TopVariants (Paid) theo QuantitySold.
    /// </summary>
    [ApiController]
    [Route("api/admin/orders-dashboard")]
    [Authorize]
    public class AdminOrdersDashboardController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly ILogger<AdminOrdersDashboardController> _logger;

        // Order status phổ biến trong hệ thống hiện tại
        private const string OrderPaid = "Paid";
        private const string OrderPendingPayment = "PendingPayment";
        private const string OrderCancelled = "Cancelled";
        private const string OrderCancelledByTimeout = "CancelledByTimeout";
        private const string OrderNeedsManualAction = "NeedsManualAction";

        public AdminOrdersDashboardController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            ILogger<AdminOrdersDashboardController> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/admin/orders-dashboard?fromUtc=...&toUtc=...&bucket=auto
        /// bucket: auto|day|week|month|quarter|year
        /// </summary>
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<AdminOrdersDashboardResponse>> Get(
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] string? bucket = "auto",
            CancellationToken ct = default)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;

                var from = (fromUtc ?? nowUtc.AddDays(-30));
                var to = (toUtc ?? nowUtc);

                // normalize kind
                if (from.Kind != DateTimeKind.Utc) from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
                if (to.Kind != DateTimeKind.Utc) to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

                if (to <= from)
                {
                    // tự sửa range lỗi: tối thiểu 1 ngày
                    to = from.AddDays(1);
                }

                var bucketNorm = NormalizeBucket(bucket);
                var bucketUsed = ResolveBucket(bucketNorm, from, to);

                await using var db = await _dbFactory.CreateDbContextAsync(ct);

                var q = db.Orders
                    .AsNoTracking()
                    .Where(o => o.CreatedAt >= from && o.CreatedAt < to);

                // ✅ FIX: không chạy nhiều query song song trên cùng DbContext
                var agg = await q
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        Paid = g.Sum(x => x.Status == OrderPaid ? 1 : 0),
                        Pending = g.Sum(x => x.Status == OrderPendingPayment ? 1 : 0),
                        Manual = g.Sum(x => x.Status == OrderNeedsManualAction ? 1 : 0),
                        Cancelled = g.Sum(x => (x.Status == OrderCancelled || x.Status == OrderCancelledByTimeout) ? 1 : 0),
                        Revenue = g.Where(x => x.Status == OrderPaid)
                                   .Sum(x => (decimal?)((x.TotalAmount) - (x.DiscountAmount))) ?? 0m
                    })
                    .FirstOrDefaultAsync(ct);

                var totalOrders = agg?.Total ?? 0;
                var paidOrders = agg?.Paid ?? 0;
                var pendingOrders = agg?.Pending ?? 0;
                var manualOrders = agg?.Manual ?? 0;
                var cancelledOrders = agg?.Cancelled ?? 0;
                var revenue = agg?.Revenue ?? 0m;

                var avgPaidOrderValue = paidOrders > 0 ? (revenue / paidOrders) : 0m;
                var conversionRate = totalOrders > 0 ? (double)paidOrders / totalOrders : 0d;

                // Breakdown theo trạng thái
                var statusBreakdown = await q
                    .GroupBy(o => o.Status ?? "Unknown")
                    .Select(g => new StatusCountDto
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync(ct);

                // Trend theo bucket
                var trend = await BuildTrendAsync(db, from, to, bucketUsed, ct);

                // ✅ Bestsellers (Paid): TopProducts + TopVariants
                var (topProducts, topVariants) = await BuildBestsellersAsync(db, from, to, ct);

                // Alerts tối giản
                var alerts = new List<DashboardAlertDto>();
                if (totalOrders == 0)
                {
                    alerts.Add(new DashboardAlertDto
                    {
                        Code = "NO_DATA",
                        Severity = "warn",
                        Message = "Không có đơn hàng nào trong khoảng thời gian đã chọn."
                    });
                }
                else if (pendingOrders > 0)
                {
                    alerts.Add(new DashboardAlertDto
                    {
                        Code = "PENDING_EXISTS",
                        Severity = "info",
                        Message = $"Có {pendingOrders} đơn đang chờ thanh toán."
                    });
                }

                var resp = new AdminOrdersDashboardResponse
                {
                    FromUtc = from,
                    ToUtc = to,
                    Bucket = bucketUsed,
                    Kpi = new OrdersKpiDto
                    {
                        TotalOrders = totalOrders,
                        PaidOrders = paidOrders,
                        PendingOrders = pendingOrders,
                        CancelledOrders = cancelledOrders,
                        NeedsManualActionOrders = manualOrders,
                        Revenue = revenue,
                        AvgPaidOrderValue = avgPaidOrderValue,
                        ConversionRate = conversionRate
                    },
                    Trend = trend,
                    StatusBreakdown = statusBreakdown,
                    TopProducts = topProducts,
                    TopVariants = topVariants,
                    Alerts = alerts
                };

                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orders dashboard error");
                return StatusCode(500, new { message = "Lỗi khi tải dashboard đơn hàng. Vui lòng thử lại." });
            }
        }

        private static string NormalizeBucket(string? bucket)
        {
            var b = (bucket ?? "auto").Trim().ToLowerInvariant();
            return b switch
            {
                "auto" => "auto",
                "day" => "day",
                "week" => "week",
                "month" => "month",
                "quarter" => "quarter",
                "year" => "year",
                _ => "auto"
            };
        }

        /// <summary>
        /// AUTO bucket theo độ dài range để tránh chart dài/lag:
        /// - <= 14 ngày  => day
        /// - <= 120 ngày => week
        /// - <= 550 ngày => month
        /// - <= 1100 ngày => quarter
        /// - còn lại => year
        /// </summary>
        private static string ResolveBucket(string bucketNorm, DateTime fromUtc, DateTime toUtc)
        {
            if (bucketNorm != "auto") return bucketNorm;

            var days = Math.Ceiling((toUtc - fromUtc).TotalDays);
            if (days <= 14) return "day";
            if (days <= 120) return "week";
            if (days <= 550) return "month";
            if (days <= 1100) return "quarter";
            return "year";
        }

        private static async Task<List<OrderTrendPointDto>> BuildTrendAsync(
            KeytietkiemDbContext db,
            DateTime fromUtc,
            DateTime toUtc,
            string bucketUsed,
            CancellationToken ct)
        {
            var q = db.Orders.AsNoTracking()
                .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc);

            if (bucketUsed == "day")
            {
                var raw = await q
                    .GroupBy(o => o.CreatedAt.Date)
                    .Select(g => new
                    {
                        Start = g.Key,
                        Orders = g.Count(),
                        Paid = g.Count(x => x.Status == OrderPaid),
                        Revenue = g.Where(x => x.Status == OrderPaid)
                            .Sum(x => (decimal?)((x.TotalAmount) - (x.DiscountAmount))) ?? 0m
                    })
                    .ToListAsync(ct);

                var dict = raw.ToDictionary(x => x.Start, x => x);

                var points = new List<OrderTrendPointDto>();
                var cur = fromUtc.Date;
                while (cur < toUtc)
                {
                    dict.TryGetValue(cur, out var v);
                    points.Add(new OrderTrendPointDto
                    {
                        StartUtc = DateTime.SpecifyKind(cur, DateTimeKind.Utc),
                        EndUtc = DateTime.SpecifyKind(cur.AddDays(1), DateTimeKind.Utc),
                        Orders = v?.Orders ?? 0,
                        PaidOrders = v?.Paid ?? 0,
                        Revenue = v?.Revenue ?? 0m
                    });
                    cur = cur.AddDays(1);
                }
                return points;
            }

            if (bucketUsed == "week")
            {
                var anchor = fromUtc.Date;
                var raw = await q
                    .GroupBy(o => EF.Functions.DateDiffWeek(anchor, o.CreatedAt))
                    .Select(g => new
                    {
                        WeekIndex = g.Key,
                        Orders = g.Count(),
                        Paid = g.Count(x => x.Status == OrderPaid),
                        Revenue = g.Where(x => x.Status == OrderPaid)
                            .Sum(x => (decimal?)((x.TotalAmount) - (x.DiscountAmount))) ?? 0m
                    })
                    .ToListAsync(ct);

                var dict = raw.ToDictionary(x => x.WeekIndex, x => x);

                var points = new List<OrderTrendPointDto>();
                var totalWeeks = (int)Math.Ceiling((toUtc - anchor).TotalDays / 7d);
                for (int i = 0; i < totalWeeks; i++)
                {
                    var start = anchor.AddDays(i * 7);
                    var end = start.AddDays(7);
                    dict.TryGetValue(i, out var v);

                    points.Add(new OrderTrendPointDto
                    {
                        StartUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc),
                        EndUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc),
                        Orders = v?.Orders ?? 0,
                        PaidOrders = v?.Paid ?? 0,
                        Revenue = v?.Revenue ?? 0m
                    });
                }
                return points;
            }

            if (bucketUsed == "month")
            {
                var raw = await q
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Orders = g.Count(),
                        Paid = g.Count(x => x.Status == OrderPaid),
                        Revenue = g.Where(x => x.Status == OrderPaid)
                            .Sum(x => (decimal?)((x.TotalAmount) - (x.DiscountAmount))) ?? 0m
                    })
                    .ToListAsync(ct);

                var dict = raw.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x);

                var points = new List<OrderTrendPointDto>();
                var cur = new DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                while (cur < toUtc)
                {
                    var key = $"{cur.Year:D4}-{cur.Month:D2}";
                    dict.TryGetValue(key, out var v);

                    var end = cur.AddMonths(1);
                    points.Add(new OrderTrendPointDto
                    {
                        StartUtc = cur,
                        EndUtc = end,
                        Orders = v?.Orders ?? 0,
                        PaidOrders = v?.Paid ?? 0,
                        Revenue = v?.Revenue ?? 0m
                    });

                    cur = end;
                }

                return points;
            }

            if (bucketUsed == "quarter")
            {
                var raw = await q
                    .GroupBy(o => new
                    {
                        o.CreatedAt.Year,
                        Quarter = ((o.CreatedAt.Month - 1) / 3) + 1
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Quarter,
                        Orders = g.Count(),
                        Paid = g.Count(x => x.Status == OrderPaid),
                        Revenue = g.Where(x => x.Status == OrderPaid)
                            .Sum(x => (decimal?)((x.TotalAmount) - (x.DiscountAmount))) ?? 0m
                    })
                    .ToListAsync(ct);

                var dict = raw.ToDictionary(x => $"{x.Year:D4}-Q{x.Quarter}", x => x);

                var points = new List<OrderTrendPointDto>();

                var startQuarter = ((fromUtc.Month - 1) / 3) * 3 + 1;
                var cur = new DateTime(fromUtc.Year, startQuarter, 1, 0, 0, 0, DateTimeKind.Utc);

                while (cur < toUtc)
                {
                    var qNum = ((cur.Month - 1) / 3) + 1;
                    var key = $"{cur.Year:D4}-Q{qNum}";
                    dict.TryGetValue(key, out var v);

                    var end = cur.AddMonths(3);
                    points.Add(new OrderTrendPointDto
                    {
                        StartUtc = cur,
                        EndUtc = end,
                        Orders = v?.Orders ?? 0,
                        PaidOrders = v?.Paid ?? 0,
                        Revenue = v?.Revenue ?? 0m
                    });

                    cur = end;
                }

                return points;
            }

            // year
            {
                var raw = await q
                    .GroupBy(o => o.CreatedAt.Year)
                    .Select(g => new
                    {
                        Year = g.Key,
                        Orders = g.Count(),
                        Paid = g.Count(x => x.Status == OrderPaid),
                        Revenue = g.Where(x => x.Status == OrderPaid)
                            .Sum(x => (decimal?)((x.TotalAmount) - (x.DiscountAmount))) ?? 0m
                    })
                    .ToListAsync(ct);

                var dict = raw.ToDictionary(x => x.Year, x => x);

                var points = new List<OrderTrendPointDto>();
                var curYear = fromUtc.Year;

                while (new DateTime(curYear, 1, 1, 0, 0, 0, DateTimeKind.Utc) < toUtc)
                {
                    var start = new DateTime(curYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var end = start.AddYears(1);

                    dict.TryGetValue(curYear, out var v);

                    points.Add(new OrderTrendPointDto
                    {
                        StartUtc = start,
                        EndUtc = end,
                        Orders = v?.Orders ?? 0,
                        PaidOrders = v?.Paid ?? 0,
                        Revenue = v?.Revenue ?? 0m
                    });

                    curYear++;
                }

                return points;
            }
        }

        /// <summary>
        /// Bestsellers (Paid) theo QuantitySold:
        /// - TopProducts: group theo ProductId
        /// - TopVariants: group theo VariantId
        /// </summary>
        private static async Task<(List<TopProductDto> topProducts, List<TopVariantDto> topVariants)> BuildBestsellersAsync(
            KeytietkiemDbContext db,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var odQ = db.OrderDetails.AsNoTracking();
            var oQ = db.Orders.AsNoTracking();
            var pvQ = db.Set<ProductVariant>().AsNoTracking();
            var pQ = db.Set<Product>().AsNoTracking();

            // Base rows: OrderDetails thuộc các Order Paid trong range
            var baseRows =
                from od in odQ
                join o in oQ on od.OrderId equals o.OrderId
                join v in pvQ on od.VariantId equals v.VariantId into vj
                from v in vj.DefaultIfEmpty()
                join p in pQ on (v != null ? v.ProductId : Guid.Empty) equals p.ProductId into pj
                from p in pj.DefaultIfEmpty()
                where o.CreatedAt >= fromUtc
                      && o.CreatedAt < toUtc
                      && o.Status == OrderPaid
                select new
                {
                    od.OrderId,
                    od.VariantId,
                    VariantTitle = v != null ? v.Title : null,
                    ProductId = v != null ? v.ProductId : Guid.Empty,
                    ProductName = p != null ? p.ProductName : null,
                    od.Quantity,
                    od.UnitPrice
                };

            // ✅ bỏ các dòng lỗi join (ProductId empty)
            var baseRowsValid = baseRows.Where(x => x.ProductId != Guid.Empty);

            // ✅ Top 5
            var topProducts = await baseRowsValid
                .GroupBy(x => new { x.ProductId, x.ProductName })
                .Select(g => new TopProductDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName ?? "",
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.UnitPrice * x.Quantity),
                    OrdersCount = g.Select(x => x.OrderId).Distinct().Count()
                })
                .OrderByDescending(x => x.QuantitySold)
                .ThenByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync(ct);

            // ✅ Top 5
            var topVariants = await baseRowsValid
                .GroupBy(x => new { x.VariantId, x.VariantTitle, x.ProductId, x.ProductName })
                .Select(g => new TopVariantDto
                {
                    VariantId = g.Key.VariantId,
                    VariantTitle = g.Key.VariantTitle ?? "",
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName ?? "",
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.UnitPrice * x.Quantity),
                    OrdersCount = g.Select(x => x.OrderId).Distinct().Count()
                })
                .OrderByDescending(x => x.QuantitySold)
                .ThenByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync(ct);

            return (topProducts, topVariants);
        }

        // ===== DTOs =====

        public class AdminOrdersDashboardResponse
        {
            public DateTime FromUtc { get; set; }
            public DateTime ToUtc { get; set; } // exclusive
            public string Bucket { get; set; } = "auto";

            public OrdersKpiDto Kpi { get; set; } = new OrdersKpiDto();
            public List<OrderTrendPointDto> Trend { get; set; } = new List<OrderTrendPointDto>();
            public List<StatusCountDto> StatusBreakdown { get; set; } = new List<StatusCountDto>();

            // ✅ Bestsellers (Paid)
            public List<TopProductDto> TopProducts { get; set; } = new List<TopProductDto>();
            public List<TopVariantDto> TopVariants { get; set; } = new List<TopVariantDto>();

            public List<DashboardAlertDto> Alerts { get; set; } = new List<DashboardAlertDto>();
        }

        public class OrdersKpiDto
        {
            public int TotalOrders { get; set; }
            public int PaidOrders { get; set; }
            public int PendingOrders { get; set; }
            public int CancelledOrders { get; set; }
            public int NeedsManualActionOrders { get; set; }

            public decimal Revenue { get; set; }
            public decimal AvgPaidOrderValue { get; set; }
            public double ConversionRate { get; set; } // 0..1
        }

        public class OrderTrendPointDto
        {
            public DateTime StartUtc { get; set; }
            public DateTime EndUtc { get; set; } // exclusive
            public int Orders { get; set; }
            public int PaidOrders { get; set; }
            public decimal Revenue { get; set; }
        }

        public class StatusCountDto
        {
            public string Status { get; set; } = "Unknown";
            public int Count { get; set; }
        }

        public class TopProductDto
        {
            public Guid ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public int QuantitySold { get; set; }
            public decimal Revenue { get; set; }
            public int OrdersCount { get; set; }
        }

        public class TopVariantDto
        {
            public Guid VariantId { get; set; }
            public string VariantTitle { get; set; } = "";
            public Guid ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public int QuantitySold { get; set; }
            public decimal Revenue { get; set; }
            public int OrdersCount { get; set; }
        }

        
    }
}
