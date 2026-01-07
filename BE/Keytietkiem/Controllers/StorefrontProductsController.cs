using Keytietkiem.DTOs;
using Keytietkiem.DTOs.ProductClient;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
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
        [AllowAnonymous]
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
        [AllowAnonymous]
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

            // ===== Filter theo giá bán (SellPrice) =====
            if (query.MinPrice.HasValue)
            {
                var min = query.MinPrice.Value;
                q = q.Where(v => v.SellPrice >= min);
            }

            if (query.MaxPrice.HasValue)
            {
                var max = query.MaxPrice.Value;
                q = q.Where(v => v.SellPrice <= max);
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
                    v.Status ?? "ACTIVE",
                    v.ViewCount,
                    v.CreatedAt,
                    v.UpdatedAt,
                    v.SellPrice,
                    v.ListPrice,
                    v.Product.ProductBadges
                        .Select(pb => pb.Badge)
                        .ToList(),
                    v.StockQty
                ))
                .ToListAsync();

            IEnumerable<VariantRawItem> orderedItems;

            // ===== Logic ưu tiên: rank trong từng product =====
            if (sortKey is "default" or "sold" or "bestseller")
            {
                var ranked = rawItems
                    .GroupBy(i => i.ProductId)
                    .SelectMany(g =>
                        g.OrderByDescending(x => x.StockQty > 0) // ✅ ưu tiên còn hàng trong product
                         .ThenByDescending(x => x.ViewCount)
                         .ThenByDescending(x => x.VariantId)
                         .Select((item, index) => new
                         {
                             Item = item,
                             Rank = index + 1
                         })
                    )
                    .ToList();

                orderedItems = ranked
                    .OrderBy(x => x.Rank)
                    .ThenByDescending(x => x.Item.ViewCount)
                    .Select(x => x.Item)
                    .ToList();
            }
            else
            {
                orderedItems = sortKey switch
                {
                    // ✅ NEW: Ưu đãi hôm nay (Deals)
                    // Ưu tiên còn hàng -> có giảm giá -> % giảm desc -> view desc -> createdAt desc
                    "deals" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenByDescending(i => ComputeDiscountPercent(i.SellPrice, i.ListPrice) > 0m)
                        .ThenByDescending(i => ComputeDiscountPercent(i.SellPrice, i.ListPrice))
                        .ThenByDescending(i => i.ViewCount)
                        .ThenByDescending(i => i.CreatedAt),

                    // ✅ NEW: Sắp hết hàng (Low stock)
                    // Ưu tiên còn hàng -> stock asc -> view desc -> createdAt desc
                    "low-stock" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenBy(i => i.StockQty <= 0 ? int.MaxValue : i.StockQty)
                        .ThenByDescending(i => i.ViewCount)
                        .ThenByDescending(i => i.CreatedAt),

                    "updated" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenByDescending(i => i.UpdatedAt ?? i.CreatedAt),

                    // sort đúng theo SellPrice
                    "price-asc" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenBy(i => i.SellPrice)
                        .ThenByDescending(i => i.ViewCount),

                    "price-desc" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenByDescending(i => i.SellPrice)
                        .ThenByDescending(i => i.ViewCount),

                    "name-asc" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenBy(i => i.Title),

                    "name-desc" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenByDescending(i => i.Title),

                    _ => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenByDescending(i => i.ViewCount)
                        .ThenByDescending(i => i.CreatedAt)
                };
            }

            // ✅ Ép "còn hàng trước" theo kiểu STABLE cho mọi sort (hết hàng dồn xuống cuối)
            var orderedList = orderedItems.ToList();
            orderedItems = orderedList
                .Select((item, index) => new { item, index })
                .OrderByDescending(x => x.item.StockQty > 0)
                .ThenBy(x => x.index)
                .Select(x => x.item)
                .ToList();

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
                        i.VariantId,
                        i.ProductId,
                        i.ProductCode,
                        i.ProductName,
                        i.ProductType,
                        i.Title,
                        i.Thumbnail,
                        status,
                        i.SellPrice,
                        i.ListPrice,
                        badges
                    );
                })
                .ToList();

            var result = new PagedResult<StorefrontVariantListItemDto>(items, total, page, pageSize);
            return Ok(result);
        }

        [HttpGet("{productId:guid}/variants/{variantId:guid}/detail")]
        [AllowAnonymous]
        public async Task<ActionResult<StorefrontVariantDetailDto>> GetVariantDetail(
            Guid productId,
            Guid variantId,
            [FromQuery] bool countView = true)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // ===== Load biến thể + product + categories (CÓ TRACKING để tăng ViewCount) =====
            var v = await db.ProductVariants
                .Include(x => x.Product)
                    .ThenInclude(p => p.Categories)
                .FirstOrDefaultAsync(x =>
                    x.ProductId == productId &&
                    x.VariantId == variantId &&
                    x.Product.Status != null &&
                    (x.Product.Status == "ACTIVE" || x.Product.Status == "OUT_OF_STOCK") &&
                    x.Status != null &&
                    (x.Status == "ACTIVE" || x.Status == "OUT_OF_STOCK"));

            if (v is null) return NotFound();

            // ===== Tăng ViewCount nếu countView=true =====
            if (countView)
            {
                v.ViewCount += 1;
                await db.SaveChangesAsync();
            }

            var detailOutOfStock = v.StockQty <= 0;
            var detailStatus = detailOutOfStock ? "OUT_OF_STOCK" : "ACTIVE";

            var p = v.Product;

            // ===== Danh mục của sản phẩm =====
            var categories = p.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .Select(c => new StorefrontCategoryMiniDto(
                    c.CategoryId,
                    c.CategoryCode,
                    c.CategoryName
                ))
                .ToList();

            // ===== Các biến thể khác cùng sản phẩm (sort theo ViewCount) =====
            var siblingVariants = await db.ProductVariants
                .AsNoTracking()
                .Where(x =>
                    x.ProductId == productId &&
                    x.Status != null &&
                    (x.Status == "ACTIVE" || x.Status == "OUT_OF_STOCK"))
                .OrderByDescending(x => x.ViewCount)
                .ThenBy(x => x.Title)
                .Select(x => new StorefrontSiblingVariantDto(
                    x.VariantId,
                    x.Title,
                    x.StockQty <= 0 ? "OUT_OF_STOCK" : "ACTIVE"

                ))
                .ToListAsync();

            // ===== Sections của biến thể (đang active) =====
            var sections = await db.ProductSections
                .AsNoTracking()
                .Where(s => s.VariantId == variantId && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.CreatedAt)
                .Select(s => new StorefrontSectionDto(
                    s.SectionId,
                    s.SectionType,
                    s.Title,
                    s.Content ?? string.Empty
                ))
                .ToListAsync();

            // ===== FAQ: theo Category trước, rồi trực tiếp Product =====
            var categoryIds = categories.Select(c => c.CategoryId).ToList();

            var categoryFaqs = await db.Faqs
                .AsNoTracking()
                .Where(f =>
                    f.IsActive &&
                    f.Categories.Any(c => categoryIds.Contains(c.CategoryId)))
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.FaqId)
                .Select(f => new StorefrontFaqItemDto(
                    f.FaqId,
                    f.Question,
                    f.Answer ?? string.Empty,
                    "CATEGORY"
                ))
                .ToListAsync();

            var productFaqs = await db.Faqs
                .AsNoTracking()
                .Where(f =>
                    f.IsActive &&
                    f.Products.Any(pr => pr.ProductId == productId))
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.FaqId)
                .Select(f => new StorefrontFaqItemDto(
                    f.FaqId,
                    f.Question,
                    f.Answer ?? string.Empty,
                    "PRODUCT"
                ))
                .ToListAsync();

            var faqs = categoryFaqs
                .Concat(productFaqs)
                .GroupBy(x => x.FaqId)
                .Select(g => g.First())
                .ToList();

            var dto = new StorefrontVariantDetailDto(
                VariantId: v.VariantId,
                ProductId: p.ProductId,
                ProductCode: p.ProductCode,
                ProductName: p.ProductName,
                ProductType: p.ProductType,
                VariantTitle: v.Title,
                Status: detailStatus,
                StockQty: v.StockQty,
                Thumbnail: v.Thumbnail,
                Categories: categories,
                SiblingVariants: siblingVariants,
                SellPrice: v.SellPrice,
                ListPrice: v.ListPrice,
                Sections: sections,
                Faqs: faqs
            );

            return Ok(dto);
        }

        [HttpGet("{productId:guid}/variants/{variantId:guid}/related")]
        [AllowAnonymous]
        public async Task<ActionResult<IReadOnlyCollection<StorefrontVariantListItemDto>>> GetRelatedVariants(
            Guid productId,
            Guid variantId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // 1) Load variant gốc + Product + Categories + Badges
            var baseVariant = await db.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                    .ThenInclude(p => p.Categories)
                .Include(v => v.Product)
                    .ThenInclude(p => p.ProductBadges)
                .FirstOrDefaultAsync(v =>
                    v.ProductId == productId &&
                    v.VariantId == variantId &&
                    v.Product.Status != null &&
                    (v.Product.Status == "ACTIVE" || v.Product.Status == "OUT_OF_STOCK") &&
                    v.Status != null &&
                    (v.Status == "ACTIVE" || v.Status == "OUT_OF_STOCK"));

            if (baseVariant is null) return NotFound();

            var baseProduct = baseVariant.Product;

            var baseCategoryIds = baseProduct.Categories
                .Where(c => c.IsActive)
                .Select(c => c.CategoryId)
                .ToList();

            var baseBadgeCodes = baseProduct.ProductBadges
                .Select(pb => pb.Badge)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var baseType = baseProduct.ProductType;

            var baseCategorySet = new HashSet<int>(baseCategoryIds);
            var baseBadgeSet = new HashSet<string>(baseBadgeCodes, StringComparer.OrdinalIgnoreCase);

            // 2) Lấy các biến thể ứng viên
            var candidates = await db.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                    .ThenInclude(p => p.Categories)
                .Include(v => v.Product)
                    .ThenInclude(p => p.ProductBadges)
                .Where(v =>
                    v.ProductId != productId &&
                    v.Product.Status != null &&
                    (v.Product.Status == "ACTIVE" || v.Product.Status == "OUT_OF_STOCK") &&
                    v.Status != null &&
                    (v.Status == "ACTIVE" || v.Status == "OUT_OF_STOCK") &&

                    (
                        (baseCategoryIds.Count == 0
                            ? false
                            : v.Product.Categories.Any(c => baseCategoryIds.Contains(c.CategoryId)))
                        ||
                        (baseBadgeCodes.Count == 0
                            ? false
                            : v.Product.ProductBadges.Any(pb => baseBadgeCodes.Contains(pb.Badge)))
                        ||
                        v.Product.ProductType == baseType
                    )
                )
                .Select(v => new RelatedVariantRawItem(
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
                    v.SellPrice,
                    v.ListPrice,
                    v.Product.Categories
                        .Select(c => c.CategoryId)
                        .ToList(),
                    v.Product.ProductBadges
                        .Select(pb => pb.Badge)
                        .ToList(),
                    v.StockQty
                ))
                .ToListAsync();

            if (candidates.Count == 0)
                return Ok(Array.Empty<StorefrontVariantListItemDto>());

            // 3) Mỗi product chỉ giữ lại 1 biến thể (view cao nhất)
            var bestPerProduct = candidates
                .GroupBy(x => x.ProductId)
                .Select(g => g
                    .OrderByDescending(v => v.ViewCount)
                    .ThenByDescending(v => v.CreatedAt)
                    .First())
                .ToList();

            // 4) Tính độ tương đồng & sort
            var ranked = bestPerProduct
                .Select(item =>
                {
                    var catMatches = item.CategoryIds.Count(id => baseCategorySet.Contains(id));
                    var badgeMatches = item.BadgeCodes.Count(code => baseBadgeSet.Contains(code));
                    var typeMatch = string.Equals(item.ProductType, baseType, StringComparison.OrdinalIgnoreCase) ? 1 : 0;

                    return new
                    {
                        Item = item,
                        CatMatches = catMatches,
                        BadgeMatches = badgeMatches,
                        TypeMatch = typeMatch
                    };
                })
                .OrderByDescending(x => x.CatMatches)
                .ThenByDescending(x => x.BadgeMatches)
                .ThenByDescending(x => x.TypeMatch)
                .ThenByDescending(x => x.Item.ViewCount)
                .Take(8)
                .ToList();

            var relatedRawItems = ranked.Select(x => x.Item).ToList();

            // 5) Badge meta
            var relatedBadgeCodes = relatedRawItems
                .SelectMany(i => i.BadgeCodes)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var badgeLookup = relatedBadgeCodes.Count == 0
                ? new Dictionary<string, Badge>(StringComparer.OrdinalIgnoreCase)
                : await db.Badges.AsNoTracking()
                    .Where(b => relatedBadgeCodes.Contains(b.BadgeCode))
                    .ToDictionaryAsync(b => b.BadgeCode, StringComparer.OrdinalIgnoreCase);

            // 6) Map DTO
            var items = relatedRawItems
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
                        i.VariantId,
                        i.ProductId,
                        i.ProductCode,
                        i.ProductName,
                        i.ProductType,
                        i.Title,
                        i.Thumbnail,
                        status,
                        i.SellPrice,
                        i.ListPrice,
                        badges
                    );
                })
                .ToList();

            return Ok(items);
        }


        private static decimal ComputeDiscountPercent(decimal sellPrice, decimal listPrice)
        {
            if (sellPrice <= 0 || listPrice <= 0 || sellPrice >= listPrice) return 0m;
            var percent = (listPrice - sellPrice) / listPrice * 100m;
            return Math.Round(percent, 2);
        }


        // ===== Raw record types =====

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
            decimal SellPrice,
            decimal ListPrice,
            List<string> BadgeCodes,
            int StockQty
        );

        private sealed record RelatedVariantRawItem(
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
            decimal SellPrice,
            decimal ListPrice,
            List<int> CategoryIds,
            List<string> BadgeCodes,
            int StockQty
        );
    }
}
