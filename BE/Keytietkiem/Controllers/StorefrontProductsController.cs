using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
 
    [ApiController]
    [Route("api/storefront/products")]
    public class StorefrontProductsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

        public StorefrontProductsController(IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        [HttpGet("filters")]
        public async Task<ActionResult<StorefrontFiltersDto>> GetFilters()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var categories = await db.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .Select(c => new StorefrontCategoryFilterItemDto(
                    c.CategoryId,
                    c.CategoryCode,
                    c.CategoryName
                ))
                .ToListAsync();

          
            var productTypes = ProductEnums.Types.ToArray();

            var dto = new StorefrontFiltersDto(categories, productTypes);
            return Ok(dto);
        }

        [HttpGet("variants")]
        public async Task<ActionResult<PagedResult<StorefrontVariantListItemDto>>> ListVariants(
       [FromQuery] StorefrontVariantListQuery query)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // ===== Base query: filter trạng thái & điều kiện lọc =====
            var q = db.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                    .ThenInclude(p => p.Categories)
                .Include(v => v.Product)
                    .ThenInclude(p => p.ProductBadges)
                .Where(v =>
                    v.Product.Status != null &&
                    (v.Product.Status == "ACTIVE" || v.Product.Status == "OUT_OF_STOCK") &&
                    v.Status != null &&
                    (v.Status == "ACTIVE" || v.Status == "OUT_OF_STOCK"));

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var s = query.Q.Trim();
                q = q.Where(v =>
                    EF.Functions.Like(v.Title, $"%{s}%") ||
                    EF.Functions.Like(v.Product.ProductName, $"%{s}%") ||
                    EF.Functions.Like(v.Product.ProductCode, $"%{s}%"));
            }

            if (query.CategoryId is not null)
            {
                var cid = query.CategoryId.Value;
                q = q.Where(v => v.Product.Categories.Any(c => c.CategoryId == cid));
            }

            if (!string.IsNullOrWhiteSpace(query.ProductType))
            {
                var type = query.ProductType.Trim();
                q = q.Where(v => v.Product.ProductType == type);
            }

            // Phân trang info
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 8);
            var total = await q.CountAsync();

            if (total == 0)
            {
                var emptyResult = new PagedResult<StorefrontVariantListItemDto>(
                    new List<StorefrontVariantListItemDto>(),
                    0,
                    page,
                    pageSize
                );
                return Ok(emptyResult);
            }

            var sortKey = (query.Sort ?? "default").Trim().ToLowerInvariant();

            // ===== Lấy data thô (CHƯA phân trang) =====
            var rawItems = await q
                .Select(v => new VariantRawItem(
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
                    v.Product.ProductBadges
                        .Select(pb => pb.Badge)
                        .ToList()
                ))
                .ToListAsync();

            IEnumerable<VariantRawItem> orderedItems;

            // ===== Logic ưu tiên: rank trong từng product =====
            if (sortKey is "default" or "sold" or "bestseller")
            {
                // 1. Gán Rank = 1,2,3,... trong từng ProductId theo ViewCount giảm dần
                var ranked = rawItems
                    .GroupBy(i => i.ProductId)
                    .SelectMany(g =>
                        g.OrderByDescending(x => x.ViewCount)
                         .ThenByDescending(x => x.VariantId)    // tie-break
                         .Select((item, index) => new
                         {
                             Item = item,
                             Rank = index + 1
                         })
                    )
                    .ToList();

                // 2. Toàn bộ list: Rank ↑, rồi ViewCount ↓
                orderedItems = ranked
                    .OrderBy(x => x.Rank)
                    .ThenByDescending(x => x.Item.ViewCount)
                    .Select(x => x.Item)
                    .ToList();
            }
            else
            {
                // Các sort khác giữ behaviour cũ
                orderedItems = sortKey switch
                {
                    "updated" => rawItems
                        .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt),

                    // tạm dùng ViewCount làm proxy cho giá, sau này có Price thì đổi
                    "price-asc" => rawItems
                        .OrderBy(i => i.ViewCount)
                        .ThenByDescending(i => i.CreatedAt),

                    "price-desc" => rawItems
                        .OrderByDescending(i => i.ViewCount)
                        .ThenByDescending(i => i.CreatedAt),

                    "name-asc" => rawItems
                        .OrderBy(i => i.Title),

                    "name-desc" => rawItems
                        .OrderByDescending(i => i.Title),

                    _ => rawItems
                        .OrderByDescending(i => i.ViewCount)
                        .ThenByDescending(i => i.CreatedAt)
                };
            }

            // ===== Phân trang sau khi order =====
            var pageItems = orderedItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ===== Badge lookup chỉ cho badge đang dùng trên page =====
            var pageBadgeCodes = pageItems
                .SelectMany(i => i.BadgeCodes)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var badgeLookup = pageBadgeCodes.Count == 0
                ? new Dictionary<string, Badge>(StringComparer.OrdinalIgnoreCase)
                : await db.Badges.AsNoTracking()
                    .Where(b => pageBadgeCodes.Contains(b.BadgeCode))
                    .ToDictionaryAsync(b => b.BadgeCode, StringComparer.OrdinalIgnoreCase);

            var items = pageItems
                .Select(i =>
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

                            // Badge code còn trên product nhưng badge đã xoá
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
                        i.Title,          // VariantTitle
                        i.Thumbnail,
                        i.Status,
                        badges
                    );
                })
                .ToList();

            var result = new PagedResult<StorefrontVariantListItemDto>(items, total, page, pageSize);
            return Ok(result);
        }


private sealed record VariantRawItem(
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
    List<string> BadgeCodes
);
    }
}
