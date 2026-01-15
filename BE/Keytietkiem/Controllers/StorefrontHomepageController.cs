// File: Controllers/StorefrontHomepageController.cs
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.DTOs.ProductClient;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    /// <summary>
    /// API cho trang homepage phía người mua (storefront)
    /// Trả về 5 khối sản phẩm:
    /// 1) Ưu đãi hôm nay (Deals / On sale)
    /// 2) Bán chạy nhất (Best sellers)
    /// 3) Mới ra mắt (New arrivals)
    /// 4) Đang thịnh hành (Trending)
    /// 5) Sắp hết hàng (Low stock)
    /// </summary>
    [ApiController]
    [Route("api/storefront/homepage")]
    public class StorefrontHomepageController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        // ✅ giống StorefrontProductsController: ẩn các item bị admin set ẩn
        private static readonly string[] HiddenStatuses = new[] { "INACTIVE" };

        public StorefrontHomepageController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        private static bool IsKeyType(string? pt)
        {
            var t = (pt ?? "").Trim();
            return t.Equals(ProductEnums.PERSONAL_KEY, StringComparison.OrdinalIgnoreCase)
                || t.Equals(ProductEnums.SHARED_KEY, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPersonalAccountType(string? pt)
        {
            var t = (pt ?? "").Trim();
            return t.Equals(ProductEnums.PERSONAL_ACCOUNT, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSharedAccountType(string? pt)
        {
            var t = (pt ?? "").Trim();
            return t.Equals(ProductEnums.SHARED_ACCOUNT, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Stock "thật" cho storefront:
        /// - KEY: count ProductKey Available, chưa assigned, chưa expiry
        /// - PERSONAL_ACCOUNT: account Active, MaxUsers=1, chưa có customer active, chưa expiry
        /// - SHARED_ACCOUNT: tổng slot trống (MaxUsers - active customers), chưa expiry
        /// - Trừ OrderInventoryReservation (ReservedUntilUtc > nowUtc, Status="Reserved")
        /// - Type khác: fallback StockQty - reserved (>=0)
        /// </summary>
        private static async Task<Dictionary<Guid, int>> ComputeAvailableStockByVariantIdAsync(
            KeytietkiemDbContext db,
            List<VariantStockSeed> seeds,
            DateTime nowUtc,
            CancellationToken ct)
        {
            var variantIds = seeds.Select(s => s.VariantId).Distinct().ToList();
            if (variantIds.Count == 0) return new Dictionary<Guid, int>();

            var productTypeByVariantId = seeds
                .GroupBy(s => s.VariantId)
                .ToDictionary(g => g.Key, g => g.First().ProductType);

            var fallbackStockByVariantId = seeds
                .GroupBy(s => s.VariantId)
                .ToDictionary(g => g.Key, g => g.First().StockQtyFromDb);

            var reservedByVariantId = await db.Set<OrderInventoryReservation>()
                .AsNoTracking()
                .Where(r => variantIds.Contains(r.VariantId)
                            && r.ReservedUntilUtc > nowUtc
                            && r.Status == "Reserved")
                .GroupBy(r => r.VariantId)
                .Select(g => new { VariantId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.VariantId, x => x.Qty, ct);

            var keyCountByVariantId = await db.Set<ProductKey>()
                .AsNoTracking()
                .Where(k => variantIds.Contains(k.VariantId)
                            && k.Status == nameof(ProductKeyStatus.Available)
                            && k.AssignedToOrderId == null
                            && (!k.ExpiryDate.HasValue || k.ExpiryDate.Value >= nowUtc))
                .GroupBy(k => k.VariantId)
                .Select(g => new { VariantId = g.Key, Qty = g.Count() })
                .ToDictionaryAsync(x => x.VariantId, x => x.Qty, ct);

            var personalAccountCountByVariantId = await db.Set<ProductAccount>()
                .AsNoTracking()
                .Where(pa => variantIds.Contains(pa.VariantId)
                             && pa.Status == nameof(ProductAccountStatus.Active)
                             && pa.MaxUsers == 1
                             && (!pa.ExpiryDate.HasValue || pa.ExpiryDate.Value >= nowUtc)
                             && !pa.ProductAccountCustomers.Any(pac => pac.IsActive))
                .GroupBy(pa => pa.VariantId)
                .Select(g => new { VariantId = g.Key, Qty = g.Count() })
                .ToDictionaryAsync(x => x.VariantId, x => x.Qty, ct);

            var sharedAccountSlotsByVariantId = await db.Set<ProductAccount>()
                .AsNoTracking()
                .Where(pa => variantIds.Contains(pa.VariantId)
                             && pa.Status == nameof(ProductAccountStatus.Active)
                             && pa.MaxUsers > 1
                             && (!pa.ExpiryDate.HasValue || pa.ExpiryDate.Value >= nowUtc))
                .Select(pa => new
                {
                    pa.VariantId,
                    Available = pa.MaxUsers - pa.ProductAccountCustomers.Count(pac => pac.IsActive)
                })
                .Where(x => x.Available > 0)
                .GroupBy(x => x.VariantId)
                .Select(g => new { VariantId = g.Key, Qty = g.Sum(x => x.Available) })
                .ToDictionaryAsync(x => x.VariantId, x => x.Qty, ct);

            var result = new Dictionary<Guid, int>(variantIds.Count);

            foreach (var id in variantIds)
            {
                productTypeByVariantId.TryGetValue(id, out var ptRaw);
                var pt = (ptRaw ?? "").Trim();

                int raw;
                if (IsKeyType(pt))
                    raw = keyCountByVariantId.TryGetValue(id, out var kq) ? kq : 0;
                else if (IsPersonalAccountType(pt))
                    raw = personalAccountCountByVariantId.TryGetValue(id, out var aq) ? aq : 0;
                else if (IsSharedAccountType(pt))
                    raw = sharedAccountSlotsByVariantId.TryGetValue(id, out var sq) ? sq : 0;
                else
                    raw = fallbackStockByVariantId.TryGetValue(id, out var fb) ? fb : 0;

                var reserved = reservedByVariantId.TryGetValue(id, out var rq) ? rq : 0;
                var available = raw - reserved;
                if (available < 0) available = 0;

                result[id] = available;
            }

            return result;
        }

        [HttpGet("products")]
        [AllowAnonymous]
        public async Task<ActionResult<StorefrontHomepageProductsDto>> GetHomepageProducts()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            // ===== Base query: ẩn INACTIVE giống list variants =====
            var baseQuery = db.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                    .ThenInclude(p => p.ProductBadges)
                .Where(v =>
                    (v.Product.Status == null || !HiddenStatuses.Contains(v.Product.Status.ToUpper())) &&
                    (v.Status == null || !HiddenStatuses.Contains(v.Status.ToUpper())));

            var rawItems = await baseQuery
                .Select(v => new HomepageVariantRawItem(
                    v.VariantId,
                    v.ProductId,
                    v.Product.ProductCode,
                    v.Product.ProductName,
                    v.Product.ProductType,
                    v.Title,
                    v.Thumbnail,
                    v.Status ?? "ACTIVE",
                    v.ViewCount,
                    v.CreatedAt,
                    v.UpdatedAt,
                    v.SellPrice,
                    v.ListPrice,
                    0m,   // DiscountPercent (compute below)
                    0,    // Sold30d (compute below)
                    0,    // SoldAllTime (optional fallback; compute only when needed)
                    v.Product.ProductBadges.Select(pb => pb.Badge).ToList(),
                    v.StockQty // StockQtyFromDb (sẽ replace = stock thật ngay bên dưới)
                ))
                .ToListAsync(ct);

            if (rawItems.Count == 0)
            {
                var empty = new StorefrontHomepageProductsDto(
                    TodayBestDeals: Array.Empty<StorefrontVariantListItemDto>(),
                    BestSellers: Array.Empty<StorefrontVariantListItemDto>(),
                    WeeklyTrends: Array.Empty<StorefrontVariantListItemDto>(),
                    NewlyUpdated: Array.Empty<StorefrontVariantListItemDto>(),
                    LowStock: Array.Empty<StorefrontVariantListItemDto>()
                );
                return Ok(empty);
            }

            // ===== Replace StockQty = stock thật =====
            var seeds = rawItems
                .Select(i => new VariantStockSeed(i.VariantId, i.ProductType, i.StockQty))
                .ToList();

            var stockLookup = await ComputeAvailableStockByVariantIdAsync(db, seeds, nowUtc, ct);

            rawItems = rawItems
                .Select(i =>
                {
                    var avail = stockLookup.TryGetValue(i.VariantId, out var st) ? st : i.StockQty;
                    return i with { StockQty = avail };
                })
                .ToList();

            // ===== DiscountPercent =====
            rawItems = rawItems
                .Select(i => i with { DiscountPercent = ComputeDiscountPercent(i.SellPrice, i.ListPrice) })
                .ToList();

            // ===== Sold30d / SoldAllTime (từ đơn thành công Status == "Paid") =====
            var thirtyDaysAgo = nowUtc.AddDays(-30);

            var variantIds = rawItems.Select(i => i.VariantId).Distinct().ToList();
            var sold30dLookup = await LoadSoldLookupAsync(db, variantIds, thirtyDaysAgo);

            var hasAnySold30d = sold30dLookup.Values.Any(x => x > 0);
            var soldAllTimeLookup = hasAnySold30d
                ? new Dictionary<Guid, int>()
                : await LoadSoldLookupAsync(db, variantIds, createdFromUtc: null);

            rawItems = rawItems
                .Select(i =>
                {
                    sold30dLookup.TryGetValue(i.VariantId, out var s30);
                    soldAllTimeLookup.TryGetValue(i.VariantId, out var sall);
                    return i with
                    {
                        Sold30d = s30,
                        SoldAllTime = sall
                    };
                })
                .ToList();

            // ===== Items visible (đã ẩn INACTIVE) =====
            var visibleItems = rawItems.ToList();

            // =========================
            // 1) Ưu đãi hôm nay (Deals / On sale)
            // ✅ ưu tiên còn hàng trước, rồi mới out_of_stock
            // =========================
            var todayDealsRaw = visibleItems
                .Where(i => i.DiscountPercent > 0)
                .GroupBy(i => i.ProductId)
                .Select(g => g
                    .OrderByDescending(x => x.StockQty > 0)     // ✅ in-stock first (per product)
                    .ThenByDescending(x => x.DiscountPercent)
                    .ThenByDescending(x => x.Sold30d)
                    .ThenByDescending(x => x.ViewCount)
                    .ThenByDescending(x => x.CreatedAt)
                    .First())
                .OrderByDescending(i => i.StockQty > 0)         // ✅ in-stock first (overall)
                .ThenByDescending(i => i.DiscountPercent)
                .ThenByDescending(i => i.Sold30d)
                .ThenByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.CreatedAt)
                .Take(4)
                .ToList();

            // =========================
            // 2) Bán chạy nhất (Best sellers)
            // ✅ ưu tiên còn hàng trước, rồi mới out_of_stock
            // =========================
            var bestSellersRaw = visibleItems
                .GroupBy(i => i.ProductId)
                .Select(g => g
                    .OrderByDescending(x => x.StockQty > 0)     // ✅ in-stock first (per product)
                    .ThenByDescending(x => x.Sold30d)
                    .ThenByDescending(x => x.SoldAllTime)
                    .ThenByDescending(x => x.ViewCount)
                    .ThenByDescending(x => x.CreatedAt)
                    .First())
                .OrderByDescending(i => i.StockQty > 0)         // ✅ in-stock first (overall)
                .ThenByDescending(i => i.Sold30d)
                .ThenByDescending(i => i.SoldAllTime)
                .ThenByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.CreatedAt)
                .Take(4)
                .ToList();

            // =========================
            // 3) Mới ra mắt (New arrivals)
            // ✅ ưu tiên còn hàng trước, rồi mới out_of_stock
            // =========================
            var newArrivalsRaw = visibleItems
                .GroupBy(i => i.ProductId)
                .Select(g => g
                    .OrderByDescending(x => x.StockQty > 0)     // ✅ in-stock first (per product)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.ViewCount)
                    .First())
                .OrderByDescending(i => i.StockQty > 0)         // ✅ in-stock first (overall)
                .ThenByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.ViewCount)
                .Take(4)
                .ToList();

            // =========================
            // 4) Đang thịnh hành (Trending)
            // ✅ ưu tiên còn hàng trước, rồi mới out_of_stock
            // =========================
            var trendingRaw = visibleItems
                .GroupBy(i => i.ProductId)
                .Select(g => g
                    .OrderByDescending(x => x.StockQty > 0)     // ✅ in-stock first (per product)
                    .ThenByDescending(x => x.ViewCount)
                    .ThenByDescending(x => x.CreatedAt)
                    .First())
                .OrderByDescending(i => i.StockQty > 0)         // ✅ in-stock first (overall)
                .ThenByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.CreatedAt)
                .Take(4)
                .ToList();

            // =========================
            // 5) Sắp hết hàng (Low stock)
            // (giữ đúng nghĩa: chỉ lấy những cái còn hàng nhưng ít)
            // =========================
            const int LowStockThreshold = 5;

            var lowStockRaw = visibleItems
                .Where(i => i.StockQty > 0 && i.StockQty <= LowStockThreshold)
                .GroupBy(i => i.ProductId)
                .Select(g => g
                    .OrderBy(x => x.StockQty)
                    .ThenByDescending(x => x.Sold30d)
                    .ThenByDescending(x => x.ViewCount)
                    .ThenByDescending(x => x.CreatedAt)
                    .First())
                .OrderBy(i => i.StockQty)
                .ThenByDescending(i => i.Sold30d)
                .ThenByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.CreatedAt)
                .Take(4)
                .ToList();

            // ===== Badge lookup (gộp tất cả section) =====
            var allBadgeCodes = todayDealsRaw
                .Concat(bestSellersRaw)
                .Concat(trendingRaw)
                .Concat(newArrivalsRaw)
                .Concat(lowStockRaw)
                .SelectMany(i => i.BadgeCodes)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var badgeLookup = allBadgeCodes.Count == 0
                ? new Dictionary<string, Badge>(StringComparer.OrdinalIgnoreCase)
                : await db.Badges.AsNoTracking()
                    .Where(b => allBadgeCodes.Contains(b.BadgeCode))
                    .ToDictionaryAsync(b => b.BadgeCode, StringComparer.OrdinalIgnoreCase, ct);

            StorefrontVariantListItemDto MapToDto(HomepageVariantRawItem i)
            {
                var badges = i.BadgeCodes
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Select(code =>
                    {
                        if (badgeLookup.TryGetValue(code, out var b))
                        {
                            return new StorefrontBadgeMiniDto(
                                b.BadgeCode,
                                b.DisplayName,
                                b.ColorHex,
                                b.Icon
                            );
                        }

                        return new StorefrontBadgeMiniDto(
                            code,
                            code,
                            null,
                            null
                        );
                    })
                    .ToList();

                // ✅ status derive theo stock thật (INACTIVE đã bị filter từ baseQuery)
                var status = i.StockQty <= 0 ? "OUT_OF_STOCK" : "ACTIVE";

                return new StorefrontVariantListItemDto(
                    VariantId: i.VariantId,
                    ProductId: i.ProductId,
                    ProductCode: i.ProductCode,
                    ProductName: i.ProductName,
                    ProductType: i.ProductType,
                    VariantTitle: i.Title,
                    Thumbnail: i.Thumbnail,
                    Status: status,
                    SellPrice: i.SellPrice,
                    ListPrice: i.ListPrice,
                    Badges: badges
                );
            }

            var dto = new StorefrontHomepageProductsDto(
                TodayBestDeals: todayDealsRaw.Select(MapToDto).ToList(),
                BestSellers: bestSellersRaw.Select(MapToDto).ToList(),
                WeeklyTrends: trendingRaw.Select(MapToDto).ToList(),
                NewlyUpdated: newArrivalsRaw.Select(MapToDto).ToList(),
                LowStock: lowStockRaw.Select(MapToDto).ToList()
            );

            return Ok(dto);
        }

        /// <summary>
        /// SoldQuantity từ các đơn thành công (Orders.Status == "Paid")
        /// Nếu createdFromUtc != null thì chỉ tính trong khoảng thời gian đó (ví dụ 30 ngày gần nhất)
        /// </summary>
        private static async Task<Dictionary<Guid, int>> LoadSoldLookupAsync(
            KeytietkiemDbContext db,
            List<Guid> variantIds,
            DateTime? createdFromUtc)
        {
            if (variantIds == null || variantIds.Count == 0)
                return new Dictionary<Guid, int>();

            var q =
                from od in db.OrderDetails.AsNoTracking()
                join o in db.Orders.AsNoTracking() on od.OrderId equals o.OrderId
                where o.Status == "Paid" && variantIds.Contains(od.VariantId)
                select new
                {
                    od.VariantId,
                    od.Quantity,
                    o.CreatedAt
                };

            if (createdFromUtc.HasValue)
            {
                var fromUtc = createdFromUtc.Value;
                q = q.Where(x => x.CreatedAt >= fromUtc);
            }

            var rows = await q
                .GroupBy(x => x.VariantId)
                .Select(g => new
                {
                    VariantId = g.Key,
                    SoldQty = g.Sum(x => x.Quantity)
                })
                .ToListAsync();

            return rows.ToDictionary(x => x.VariantId, x => x.SoldQty);
        }

        private static decimal ComputeDiscountPercent(decimal sellPrice, decimal listPrice)
        {
            if (sellPrice <= 0 || listPrice <= 0 || sellPrice >= listPrice)
                return 0m;

            var percent = (listPrice - sellPrice) / listPrice * 100m;
            return Math.Round(percent, 2);
        }

        private sealed record VariantStockSeed(
            Guid VariantId,
            string ProductType,
            int StockQtyFromDb
        );

        private sealed record HomepageVariantRawItem(
            Guid VariantId,
            Guid ProductId,
            string ProductCode,
            string ProductName,
            string ProductType,
            string Title,
            string? Thumbnail,
            string Status,
            int ViewCount,
            DateTime CreatedAt,
            DateTime? UpdatedAt,
            decimal SellPrice,
            decimal ListPrice,
            decimal DiscountPercent,
            int Sold30d,
            int SoldAllTime,
            List<string> BadgeCodes,
            int StockQty // ✅ đã được replace = stock thật
        );
    }
}
