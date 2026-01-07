using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keytietkiem.DTOs.ProductClient;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public StorefrontHomepageController(IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        [HttpGet("products")]
        [AllowAnonymous]
        public async Task<ActionResult<StorefrontHomepageProductsDto>> GetHomepageProducts()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // ===== Base query: lấy variant còn "hiển thị" (ACTIVE/OUT_OF_STOCK),
            // nhưng các khối bên dưới sẽ lọc "còn hàng" bằng StockQty > 0 đúng mục đích =====
            var baseQuery = db.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                    .ThenInclude(p => p.ProductBadges)
                .Where(v =>
                    v.Product.Status != null &&
                    (v.Product.Status == "ACTIVE" || v.Product.Status == "OUT_OF_STOCK") &&
                    v.Status != null &&
                    (v.Status == "ACTIVE" || v.Status == "OUT_OF_STOCK"));

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
                    v.StockQty
                ))
                .ToListAsync();

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

            // ===== DiscountPercent =====
            rawItems = rawItems
                .Select(i => i with { DiscountPercent = ComputeDiscountPercent(i.SellPrice, i.ListPrice) })
                .ToList();

            // ===== Sold30d / SoldAllTime (từ đơn thành công Status == "Paid") =====
            var nowUtc = DateTime.UtcNow;
            var thirtyDaysAgo = nowUtc.AddDays(-30);

            var variantIds = rawItems.Select(i => i.VariantId).Distinct().ToList();
            var sold30dLookup = await LoadSoldLookupAsync(db, variantIds, thirtyDaysAgo);

            // Chỉ fallback SoldAllTime nếu 30d toàn 0 (tránh query nặng khi hệ thống đã có data 30d)
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

            // ===== Lọc “còn hàng” dùng StockQty =====
            var inStockItems = rawItems.Where(i => i.StockQty > 0).ToList();

            // Helper: chọn 1 variant tốt nhất per product (tránh homepage bị trùng nhiều variant cùng 1 product)
            static List<HomepageVariantRawItem> PickTopVariantPerProduct(
                IEnumerable<HomepageVariantRawItem> source,
                Func<HomepageVariantRawItem, object?> primaryKey,
                Func<HomepageVariantRawItem, object?>? secondaryKey = null,
                Func<HomepageVariantRawItem, object?>? thirdKey = null,
                Func<HomepageVariantRawItem, object?>? fourthKey = null)
            {
                // Group per ProductId → pick "best" inside group theo thứ tự keys (desc cho numeric/time)
                // (trong LINQ, mình triển khai đúng từng section bên dưới cho rõ ràng)
                return source.ToList();
            }

            // =========================
            // 1) Ưu đãi hôm nay (Deals / On sale)
            // Lấy còn hàng + DiscountPercent > 0
            // Sort: DiscountPercent desc → Sold30d desc → ViewCount desc → CreatedAt desc
            // =========================
            var todayDealsRaw = inStockItems
                .Where(i => i.DiscountPercent > 0)
                .GroupBy(i => i.ProductId)
                .Select(g => g
                    .OrderByDescending(x => x.DiscountPercent)
                    .ThenByDescending(x => x.Sold30d)
                    .ThenByDescending(x => x.ViewCount)
                    .ThenByDescending(x => x.CreatedAt)
                    .First())
                .OrderByDescending(i => i.DiscountPercent)
                .ThenByDescending(i => i.Sold30d)
                .ThenByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.CreatedAt)
                .Take(4)
                .ToList();

            // =========================
            // 2) Bán chạy nhất (Best sellers)
            // Lấy còn hàng
            // Sort theo SoldQuantity từ đơn thành công (ưu tiên 30 ngày gần nhất)
            // =========================
            var bestSellersRaw = inStockItems
                .GroupBy(i => i.ProductId)
                .Select(g => g
                    .OrderByDescending(x => x.Sold30d)
                    .ThenByDescending(x => x.SoldAllTime)
                    .ThenByDescending(x => x.ViewCount)
                    .ThenByDescending(x => x.CreatedAt)
                    .First())
                .OrderByDescending(i => i.Sold30d)
                .ThenByDescending(i => i.SoldAllTime)
                .ThenByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.CreatedAt)
                .Take(4)
                .ToList();

            // =========================
            // 3) Mới ra mắt (New arrivals)
            // Lấy còn hàng
            // Sort: CreatedAt desc
            // =========================
            var newArrivalsRaw = inStockItems
                .GroupBy(i => i.ProductId)
                .Select(g => g
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.ViewCount)
                    .First())
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.ViewCount)
                .Take(4)
                .ToList();

            // =========================
            // 4) Đang thịnh hành (Trending)
            // Lấy còn hàng
            // Sort: ViewCount desc
            // =========================
            var trendingRaw = inStockItems
                .GroupBy(i => i.ProductId)
                .Select(g => g
                    .OrderByDescending(x => x.ViewCount)
                    .ThenByDescending(x => x.CreatedAt)
                    .First())
                .OrderByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.CreatedAt)
                .Take(4)
                .ToList();

            // =========================
            // 5) Sắp hết hàng (Low stock)
            // Lấy còn hàng + StockQty nhỏ (<= threshold)
            // Sort: StockQty asc → Sold30d desc
            // =========================
            const int LowStockThreshold = 5;

            var lowStockRaw = inStockItems
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
                    .ToDictionaryAsync(b => b.BadgeCode, StringComparer.OrdinalIgnoreCase);

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

                var outOfStock = i.StockQty <= 0;
                var status = outOfStock ? "OUT_OF_STOCK" : "ACTIVE";

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

            // NOTE: Giữ tên field DTO để FE cũ không vỡ:
            // - TodayBestDeals => Deals
            // - BestSellers   => Best sellers
            // - WeeklyTrends  => Trending
            // - NewlyUpdated  => New arrivals
            // + LowStock      => Low stock
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
            // listPrice = giá niêm yết, sellPrice = giá bán thực tế
            if (sellPrice <= 0 || listPrice <= 0 || sellPrice >= listPrice)
                return 0m;

            var percent = (listPrice - sellPrice) / listPrice * 100m;
            return Math.Round(percent, 2);
        }

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
            int StockQty
        );
    }
}
