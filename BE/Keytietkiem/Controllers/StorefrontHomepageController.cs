using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    /// <summary>
    /// API cho trang homepage phía ng??i dùng (storefront)
    /// Hi?n t?i ch? t?p trung vào ph?n danh sách s?n ph?m.
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

        /// <summary>
        /// L?y danh sách s?n ph?m cho homepage:
        /// - ?u ?ãi hôm nay: T?M TH?I l?y 4 s?n ph?m có ViewCount cao nh?t
        ///   (sau này s? ??i l?i: % gi?m giá cao nh?t, tie thì ViewCount nhi?u h?n x?p tr??c)
        /// - S?n ph?m bán ch?y: sort gi?ng "sold/bestseller" ? trang list products
        /// - Xu h??ng tu?n này: nhi?u ViewCount nh?t (?u tiên trong 7 ngày g?n nh?t)
        /// - M?i c?p nh?t: sort theo UpdatedAt m?i nh?t
        /// </summary>
        [HttpGet("products")]
        public async Task<ActionResult<StorefrontHomepageProductsDto>> GetHomepageProducts()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // ====== Base query: ch? l?y variant có tr?ng thái hi?n th? ======
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
                    v.Status ?? "INACTIVE",
                    v.ViewCount,
                    v.CreatedAt,
                    v.UpdatedAt,
                    0m, // DiscountPercent: T?M TH?I không dùng discount, ?? 0m
                    v.Product.ProductBadges
                        .Select(pb => pb.Badge)
                        .ToList()
                ))
                .ToListAsync();

            // N?u không có s?n ph?m nào thì tr? v? r?ng cho an toàn
            if (rawItems.Count == 0)
            {
                var empty = new StorefrontHomepageProductsDto(
                    TodayBestDeals: Array.Empty<StorefrontVariantListItemDto>(),
                    BestSellers: Array.Empty<StorefrontVariantListItemDto>(),
                    WeeklyTrends: Array.Empty<StorefrontVariantListItemDto>(),
                    NewlyUpdated: Array.Empty<StorefrontVariantListItemDto>()
                );
                return Ok(empty);
            }

            // ====== 1. ?U ?ÃI HÔM NAY ======
            // TR??C ?ÂY (theo thi?t k?):
            // var todayDealsRaw = rawItems
            //     .OrderByDescending(i => i.DiscountPercent)
            //     .ThenByDescending(i => i.ViewCount)
            //     .ThenByDescending(i => i.CreatedAt)
            //     .Take(4)
            //     .ToList();
            //
            // HI?N T?I: T?m th?i sort theo ViewCount DESC (top 4 s?n ph?m nhi?u view nh?t)
            var todayDealsRaw = rawItems
                .OrderByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.CreatedAt)
                .Take(4)
                .ToList();

            // ====== 2. S?N PH?M BÁN CH?Y: sort gi?ng "sold/bestseller" ? ListVariants ======
            var bestSellerOrdered = OrderAsBestSeller(rawItems);
            var bestSellersRaw = bestSellerOrdered
                .Take(4)
                .ToList();

            // ====== 3. XU H??NG TU?N NÀY: ViewCount cao trong 7 ngày g?n nh?t ======
            var nowUtc = DateTime.UtcNow;
            var sevenDaysAgo = nowUtc.AddDays(-7);

            var weeklyCandidates = rawItems
                .Where(i => i.CreatedAt >= sevenDaysAgo) // ?u tiên s?n ph?m m?i 7 ngày
                .ToList();

            if (weeklyCandidates.Count == 0)
            {
                // n?u 7 ngày g?n ?ây không có s?n ph?m nào, fallback dùng toàn b?
                weeklyCandidates = rawItems;
            }

            var weeklyTrendsRaw = weeklyCandidates
                .OrderByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.CreatedAt)
                .Take(4)
                .ToList();

            // ====== 4. M?I C?P NH?T: UpdatedAt (ho?c CreatedAt) DESC ======
            var newlyUpdatedRaw = rawItems
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
                .ThenByDescending(i => i.ViewCount)
                .Take(4)
                .ToList();

            // ====== Chu?n b? badge meta cho t?t c? section ======
            var allBadgeCodes = todayDealsRaw
                .Concat(bestSellersRaw)
                .Concat(weeklyTrendsRaw)
                .Concat(newlyUpdatedRaw)
                .SelectMany(i => i.BadgeCodes)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var badgeLookup = allBadgeCodes.Count == 0
                ? new Dictionary<string, Badge>(StringComparer.OrdinalIgnoreCase)
                : await db.Badges.AsNoTracking()
                    .Where(b => allBadgeCodes.Contains(b.BadgeCode))
                    .ToDictionaryAsync(b => b.BadgeCode, StringComparer.OrdinalIgnoreCase);

            // Helper map raw -> StorefrontVariantListItemDto
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

                        // Badge code còn trên product nh?ng badge ?ã xoá
                        return new StorefrontBadgeMiniDto(
                            code,
                            code,
                            null,
                            null
                        );
                    })
                    .ToList();

                return new StorefrontVariantListItemDto(
                    i.VariantId,
                    i.ProductId,
                    i.ProductCode,
                    i.ProductName,
                    i.ProductType,
                    i.Title,
                    i.Thumbnail,
                    i.Status,
                    badges
                );
            }

            var dto = new StorefrontHomepageProductsDto(
                TodayBestDeals: todayDealsRaw.Select(MapToDto).ToList(),
                BestSellers: bestSellersRaw.Select(MapToDto).ToList(),
                WeeklyTrends: weeklyTrendsRaw.Select(MapToDto).ToList(),
                NewlyUpdated: newlyUpdatedRaw.Select(MapToDto).ToList()
            );

            return Ok(dto);
        }

        /// <summary>
        /// Logic sort "bán ch?y" gi?ng v?i ListVariants (default/sold/bestseller):
        /// - Gán Rank = 1,2,3,... trong t?ng ProductId theo ViewCount DESC
        /// - Toàn b? list: Rank ?, r?i ViewCount ?
        /// </summary>
        private static List<HomepageVariantRawItem> OrderAsBestSeller(
            List<HomepageVariantRawItem> rawItems)
        {
            var ranked = rawItems
                .GroupBy(i => i.ProductId)
                .SelectMany(g =>
                    g.OrderByDescending(x => x.ViewCount)
                     .ThenByDescending(x => x.VariantId)
                     .Select((item, index) => new
                     {
                         Item = item,
                         Rank = index + 1
                     })
                )
                .ToList();

            var ordered = ranked
                .OrderBy(x => x.Rank)
                .ThenByDescending(x => x.Item.ViewCount)
                .Select(x => x.Item)
                .ToList();

            return ordered;
        }

        /// <summary>
        /// Raw item dùng n?i b? cho homepage (m? r?ng t? VariantRawItem).
        /// </summary>
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
            decimal DiscountPercent,
            List<string> BadgeCodes
        );
    }
}
