// File: Controllers/StorefrontProductsController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Constants;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.DTOs.ProductClient;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Utils;
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
        private readonly IClock _clock;

        public StorefrontProductsController(
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
            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            // ===== Base query: chỉ ẩn INACTIVE, status ACTIVE/OUT sẽ derive theo stock thật =====
            var q = db.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                    .ThenInclude(p => p.Categories)
                .Include(v => v.Product)
                    .ThenInclude(p => p.ProductBadges)
                .Where(v =>
                    (v.Product.Status == null || v.Product.Status != "INACTIVE") &&
                    (v.Status == null || v.Status != "INACTIVE"));

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

            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 8);
            var total = await q.CountAsync(ct);

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
                    v.Product.ProductBadges.Select(pb => pb.Badge).ToList(),
                    v.StockQty // from DB (sẽ replace = stock thật)
                ))
                .ToListAsync(ct);

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

            IEnumerable<VariantRawItem> orderedItems;

            if (sortKey is "default" or "sold" or "bestseller")
            {
                var ranked = rawItems
                    .GroupBy(i => i.ProductId)
                    .SelectMany(g =>
                        g.OrderByDescending(x => x.StockQty > 0) // ✅ còn hàng theo stock thật
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
                    "deals" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenByDescending(i => ComputeDiscountPercent(i.SellPrice, i.ListPrice) > 0m)
                        .ThenByDescending(i => ComputeDiscountPercent(i.SellPrice, i.ListPrice))
                        .ThenByDescending(i => i.ViewCount)
                        .ThenByDescending(i => i.CreatedAt),

                    "low-stock" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenBy(i => i.StockQty <= 0 ? int.MaxValue : i.StockQty)
                        .ThenByDescending(i => i.ViewCount)
                        .ThenByDescending(i => i.CreatedAt),

                    "updated" => rawItems
                        .OrderByDescending(i => i.StockQty > 0)
                        .ThenByDescending(i => i.UpdatedAt ?? i.CreatedAt),

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

            // ✅ Ép "còn hàng trước" STABLE theo stock thật
            var orderedList = orderedItems.ToList();
            orderedItems = orderedList
                .Select((item, index) => new { item, index })
                .OrderByDescending(x => x.item.StockQty > 0)
                .ThenBy(x => x.index)
                .Select(x => x.item)
                .ToList();

            var pageItems = orderedItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pageBadgeCodes = pageItems
                .SelectMany(i => i.BadgeCodes)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var badgeLookup = pageBadgeCodes.Count == 0
                ? new Dictionary<string, Badge>(StringComparer.OrdinalIgnoreCase)
                : await db.Badges.AsNoTracking()
                    .Where(b => pageBadgeCodes.Contains(b.BadgeCode))
                    .ToDictionaryAsync(b => b.BadgeCode, StringComparer.OrdinalIgnoreCase, ct);

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

                    // ✅ status derive theo stock thật (INACTIVE đã bị filter)
                    var status = i.StockQty <= 0 ? "OUT_OF_STOCK" : "ACTIVE";

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
            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            // ✅ chỉ ẩn INACTIVE
            var v = await db.ProductVariants
                .Include(x => x.Product)
                    .ThenInclude(p => p.Categories)
                .FirstOrDefaultAsync(x =>
                    x.ProductId == productId &&
                    x.VariantId == variantId &&
                    (x.Product.Status == null || x.Product.Status != "INACTIVE") &&
                    (x.Status == null || x.Status != "INACTIVE"), ct);

            if (v is null) return NotFound();

            if (countView)
            {
                v.ViewCount += 1;
                await db.SaveChangesAsync(ct);
            }

            var p = v.Product;

            // ===== stock thật cho variant detail =====
            var seed = new List<VariantStockSeed>
            {
                new VariantStockSeed(v.VariantId, p.ProductType, v.StockQty)
            };
            var stockLookup = await ComputeAvailableStockByVariantIdAsync(db, seed, nowUtc, ct);
            var avail = stockLookup.TryGetValue(v.VariantId, out var st) ? st : v.StockQty;

            var detailStatus = avail <= 0 ? "OUT_OF_STOCK" : "ACTIVE";

            var categories = p.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .Select(c => new StorefrontCategoryMiniDto(
                    c.CategoryId,
                    c.CategoryCode,
                    c.CategoryName
                ))
                .ToList();

            // ===== sibling variants (status theo stock thật) =====
            var siblingRaw = await db.ProductVariants
                .AsNoTracking()
                .Where(x =>
                    x.ProductId == productId &&
                    (x.Status == null || x.Status != "INACTIVE"))
                .OrderByDescending(x => x.ViewCount)
                .ThenBy(x => x.Title)
                .Select(x => new { x.VariantId, x.Title, x.StockQty })
                .ToListAsync(ct);

            var siblingSeeds = siblingRaw
                .Select(x => new VariantStockSeed(x.VariantId, p.ProductType, x.StockQty))
                .ToList();

            var siblingStock = await ComputeAvailableStockByVariantIdAsync(db, siblingSeeds, nowUtc, ct);

            var siblingVariants = siblingRaw
                .Select(x =>
                {
                    var sAvail = siblingStock.TryGetValue(x.VariantId, out var sx) ? sx : x.StockQty;
                    return new StorefrontSiblingVariantDto(
                        x.VariantId,
                        x.Title,
                        sAvail <= 0 ? "OUT_OF_STOCK" : "ACTIVE"
                    );
                })
                .ToList();

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
                .ToListAsync(ct);

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
                .ToListAsync(ct);

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
                .ToListAsync(ct);

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
                StockQty: avail, // ✅ stock thật
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
            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            var baseVariant = await db.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                    .ThenInclude(p => p.Categories)
                .Include(v => v.Product)
                    .ThenInclude(p => p.ProductBadges)
                .FirstOrDefaultAsync(v =>
                    v.ProductId == productId &&
                    v.VariantId == variantId &&
                    (v.Product.Status == null || v.Product.Status != "INACTIVE") &&
                    (v.Status == null || v.Status != "INACTIVE"), ct);

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

            var candidates = await db.ProductVariants
                .AsNoTracking()
                .Include(v => v.Product)
                    .ThenInclude(p => p.Categories)
                .Include(v => v.Product)
                    .ThenInclude(p => p.ProductBadges)
                .Where(v =>
                    v.ProductId != productId &&
                    (v.Product.Status == null || v.Product.Status != "INACTIVE") &&
                    (v.Status == null || v.Status != "INACTIVE") &&

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
                    v.Product.Categories.Select(c => c.CategoryId).ToList(),
                    v.Product.ProductBadges.Select(pb => pb.Badge).ToList(),
                    v.StockQty // from DB (replace = stock thật)
                ))
                .ToListAsync(ct);

            if (candidates.Count == 0)
                return Ok(Array.Empty<StorefrontVariantListItemDto>());

            // ===== replace stock thật cho candidates =====
            var seeds = candidates
                .Select(x => new VariantStockSeed(x.VariantId, x.ProductType, x.StockQty))
                .ToList();

            var stockLookup = await ComputeAvailableStockByVariantIdAsync(db, seeds, nowUtc, ct);

            candidates = candidates
                .Select(x =>
                {
                    var avail = stockLookup.TryGetValue(x.VariantId, out var st) ? st : x.StockQty;
                    return x with { StockQty = avail };
                })
                .ToList();

            // 3) Mỗi product chỉ giữ lại 1 biến thể (ưu tiên còn hàng rồi view cao)
            var bestPerProduct = candidates
                .GroupBy(x => x.ProductId)
                .Select(g => g
                    .OrderByDescending(v => v.StockQty > 0)
                    .ThenByDescending(v => v.ViewCount)
                    .ThenByDescending(v => v.CreatedAt)
                    .First())
                .ToList();

            // 4) Tính độ tương đồng & sort (ưu tiên similarity, rồi còn hàng, rồi view)
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
                .ThenByDescending(x => x.Item.StockQty > 0)
                .ThenByDescending(x => x.Item.ViewCount)
                .Take(8)
                .ToList();

            var relatedRawItems = ranked.Select(x => x.Item).ToList();

            var relatedBadgeCodes = relatedRawItems
                .SelectMany(i => i.BadgeCodes)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var badgeLookup = relatedBadgeCodes.Count == 0
                ? new Dictionary<string, Badge>(StringComparer.OrdinalIgnoreCase)
                : await db.Badges.AsNoTracking()
                    .Where(b => relatedBadgeCodes.Contains(b.BadgeCode))
                    .ToDictionaryAsync(b => b.BadgeCode, StringComparer.OrdinalIgnoreCase, ct);

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

                    var status = i.StockQty <= 0 ? "OUT_OF_STOCK" : "ACTIVE";

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

        private sealed record VariantStockSeed(
            Guid VariantId,
            string ProductType,
            int StockQtyFromDb
        );

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
            int StockQty // ✅ đã được replace = stock thật
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
            int StockQty // ✅ đã được replace = stock thật
        );
    }
}
