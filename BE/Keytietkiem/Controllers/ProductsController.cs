/**
 * File: ProductsController.cs
 * Author: ManhLDHE170124
 * Created: 24/10/2025
 * Last Updated: 28/10/2025
 * Version: 1.0.0
 * Purpose: Manage products, including listing with filters/pagination, detail view,
 *          create/update (JSON and multipart with images), delete, toggle/status updates,
 *          image CRUD (upload/delete/reorder/set primary/thumbnail), CSV price export/import,
 *          and bulk percentage price adjustments.
 * Endpoints:
 *   - GET    /api/products/list                         : List products (filters, sort, paging)
 *   - GET    /api/products/{id}                         : Get product detail by id (Guid)
 *   - POST   /api/products                              : Create product (JSON)
 *   - POST   /api/products/with-images                  : Create product (multipart + images)
 *   - PUT    /api/products/{id}                         : Update product (JSON)
 *   - PUT    /api/products/{id}/with-images             : Update product (multipart + images)
 *   - DELETE /api/products/{id}                         : Delete product
 *   - PATCH  /api/products/{id}/toggle                  : Toggle visibility (ACTIVE <-> INACTIVE; OUT_OF_STOCK if none)
 *   - PATCH  /api/products/{id}/status                  : Set status explicitly (validated)
 *   - POST   /api/products/{id}/images/upload           : Upload single image
 *   - POST   /api/products/{id}/thumbnail               : Set thumbnail by URL
 *   - DELETE /api/products/{id}/images/{imageId}        : Delete single image
 *   - POST   /api/products/{id}/images/reorder          : Reorder images
 *   - POST   /api/products/{id}/images/{imageId}/primary: Mark image as primary
 *   - GET    /api/products/export-csv                   : Export prices CSV (sku,new_price)
 *   - POST   /api/products/import-price-csv             : Import prices CSV (sku,new_price)
 *   - POST   /api/products/bulk-price                   : Bulk % price change (filters)
 */
// File: Controllers/ProductsController.cs
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        public ProductsController(IDbContextFactory<KeytietkiemDbContext> dbFactory, IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        private static string ResolveStatusFromTotalStock(int totalStock, string? desired)
        {
            if (totalStock <= 0) return "OUT_OF_STOCK";
            if (!string.IsNullOrWhiteSpace(desired) && ProductEnums.Statuses.Contains(desired))
                return desired.ToUpperInvariant();
            return "ACTIVE";
        }

        private static string ToggleVisibility(string current, int totalStock)
        {
            if (totalStock <= 0) return "OUT_OF_STOCK";
            return string.Equals(current, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                ? "INACTIVE" : "ACTIVE";
        }

        // ===== LIST (không giá) =====
        [HttpGet("list")]
        public async Task<ActionResult<PagedResult<ProductListItemDto>>> List(
            [FromQuery] string? keyword,
            [FromQuery] int? categoryId,
            [FromQuery(Name = "type")] string? productType,
            [FromQuery] string? status,
            // NEW: filter theo badge
            [FromQuery] string? badge,
            // OPTIONAL: nhiều badge, phân tách bằng dấu phẩy "HOT,SALE"
            [FromQuery] string? badges,
            [FromQuery] string[]? productTypes,
            [FromQuery] string? sort = "createdAt",
            [FromQuery] string? direction = "desc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.Products.AsNoTracking()
                .Include(p => p.Categories)
                .Include(p => p.ProductBadges)
                .Include(p => p.ProductVariants)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                q = q.Where(p => p.ProductName.Contains(keyword) || p.ProductCode.Contains(keyword));
            if (!string.IsNullOrWhiteSpace(productType))
                q = q.Where(p => p.ProductType == productType);
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(p => p.Status == status);
            if (productTypes != null && productTypes.Length > 0)
            {
                q = q.Where(p => productTypes.Contains(p.ProductType));
            }
            if (categoryId is not null)
                q = q.Where(p => p.Categories.Any(c => c.CategoryId == categoryId));

            // NEW: lọc theo badge
            if (!string.IsNullOrWhiteSpace(badges))
            {
                var set = badges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (set.Count > 0)
                    q = q.Where(p => p.ProductBadges.Any(b => set.Contains(b.Badge)));
            }
            else if (!string.IsNullOrWhiteSpace(badge))
            {
                var code = badge.Trim();
                q = q.Where(p => p.ProductBadges.Any(b => b.Badge == code));
            }

            sort = sort?.Trim().ToLowerInvariant();
            direction = direction?.Trim().ToLowerInvariant();

            // Sort theo tổng stock (biến thể)
            q = (sort, direction) switch
            {
                ("name", "asc") => q.OrderBy(p => p.ProductName),
                ("name", "desc") => q.OrderByDescending(p => p.ProductName),
                ("stock", "asc") => q.OrderBy(p => p.ProductVariants.Sum(v => (int?)v.StockQty) ?? 0),
                ("stock", "desc") => q.OrderByDescending(p => p.ProductVariants.Sum(v => (int?)v.StockQty) ?? 0),
                ("type", "asc") => q.OrderBy(p => p.ProductType),
                ("type", "desc") => q.OrderByDescending(p => p.ProductType),
                ("status", "asc") => q.OrderBy(p => p.Status),
                ("status", "desc") => q.OrderByDescending(p => p.Status),
                ("createdat", "asc") => q.OrderBy(p => p.CreatedAt),
                _ => q.OrderByDescending(p => p.CreatedAt)
            };

            var total = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new ProductListItemDto(
                    p.ProductId,
                    p.ProductCode,
                    p.ProductName,
                    p.ProductType,
                    (p.ProductVariants.Sum(v => (int?)v.StockQty) ?? 0),
                    p.Status,
                    p.ThumbnailUrl,
                    p.Categories.Select(c => c.CategoryId),
                    p.ProductBadges.Select(b => b.Badge)
                ))
                .ToListAsync();

            return Ok(new PagedResult<ProductListItemDto>(items, total, page, pageSize));
        }

        // ===== DETAIL (Images + FAQs + Variants) =====
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProductDetailDto>> GetById(Guid id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var p = await db.Products.AsNoTracking()
                .Include(x => x.Categories)
                .Include(x => x.ProductBadges)
                .Include(x => x.ProductImages)
                .Include(x => x.ProductFaqs)
                .Include(x => x.ProductVariants)
                .FirstOrDefaultAsync(x => x.ProductId == id);

            if (p is null) return NotFound();

            var dto = new ProductDetailDto(
                p.ProductId,
                p.ProductCode,
                p.ProductName,
                p.ProductType,
                p.AutoDelivery,
                p.Status,
                p.ThumbnailUrl,
                p.Categories.Select(c => c.CategoryId),
                p.ProductBadges.Select(b => b.Badge),
                p.ProductImages
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new ProductImageDto(i.ImageId, i.Url, i.SortOrder, i.IsPrimary, i.AltText)),
                p.ProductFaqs
                    .OrderBy(f => f.SortOrder)
                    .Select(f => new ProductFaqDto(f.FaqId, f.Question, f.Answer, f.SortOrder, f.IsActive)),
                p.ProductVariants
                    .OrderBy(v => v.SortOrder)
                    .Select(v => new ProductVariantMiniDto(
                        v.VariantId, v.VariantCode ?? "", v.Title, v.DurationDays,
                        v.OriginalPrice, v.Price, v.StockQty, v.Status, v.SortOrder
                    ))
            );

            return Ok(dto);
        }

        // ===== CREATE (không giá) =====
        [HttpPost]
        public async Task<ActionResult<ProductDetailDto>> Create(ProductCreateDto dto)
        {
            if (!ProductEnums.Types.Contains(dto.ProductType))
                return BadRequest(new { message = "Invalid ProductType" });

            await using var db = await _dbFactory.CreateDbContextAsync();

            if (await db.Products.AnyAsync(x => x.ProductCode == dto.ProductCode))
                return Conflict(new { message = "ProductCode already exists" });

            var entity = new Product
            {
                ProductId = Guid.NewGuid(),
                ProductCode = dto.ProductCode.Trim(),
                ProductName = dto.ProductName.Trim(),
                ProductType = dto.ProductType.Trim(),
                AutoDelivery = dto.AutoDelivery,
                Status = "INACTIVE", // sẽ set lại theo stock sau, mặc định INACTIVE
                ThumbnailUrl = dto.ThumbnailUrl,
                Slug = dto.Slug ?? dto.ProductCode.Trim(),
                CreatedAt = _clock.UtcNow
            };

            if (dto.CategoryIds is { } cids && cids.Any())
            {
                var cats = await db.Categories.Where(c => cids.Contains(c.CategoryId)).ToListAsync();
                foreach (var c in cats) entity.Categories.Add(c);
            }

            if (dto.BadgeCodes is { } bcs && bcs.Any())
            {
                var codes = bcs.Select(x => x.Trim()).Where(x => x != "").ToHashSet(StringComparer.OrdinalIgnoreCase);
                var valid = await db.Badges.Where(b => b.IsActive && codes.Contains(b.BadgeCode))
                                           .Select(b => b.BadgeCode).ToListAsync();
                foreach (var code in valid)
                    entity.ProductBadges.Add(new ProductBadge { ProductId = entity.ProductId, Badge = code, CreatedAt = _clock.UtcNow });
            }

            db.Products.Add(entity);
            await db.SaveChangesAsync();

            // tính total stock (chưa có biến thể -> 0)
            var totalStock = 0;
            entity.Status = ResolveStatusFromTotalStock(totalStock, dto.Status);
            await db.SaveChangesAsync();

            return await GetById(entity.ProductId);
        }

        // ===== UPDATE (không giá) =====
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, ProductUpdateDto dto)
        {
            if (!ProductEnums.Types.Contains(dto.ProductType))
                return BadRequest(new { message = "Invalid ProductType" });

            await using var db = await _dbFactory.CreateDbContextAsync();

            var e = await db.Products
                .Include(p => p.Categories)
                .Include(p => p.ProductBadges)
                .Include(p => p.ProductVariants)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (e is null) return NotFound();

            e.ProductName = dto.ProductName.Trim();
            e.ProductType = dto.ProductType.Trim();
            e.AutoDelivery = dto.AutoDelivery;
            e.ThumbnailUrl = dto.ThumbnailUrl;
            e.Slug = dto.Slug ?? e.Slug;
            e.UpdatedAt = _clock.UtcNow;

            // Categories
            e.Categories.Clear();
            if (dto.CategoryIds is { } cids && cids.Any())
            {
                var cats = await db.Categories.Where(c => cids.Contains(c.CategoryId)).ToListAsync();
                foreach (var c in cats) e.Categories.Add(c);
            }

            // Badges
            e.ProductBadges.Clear();
            if (dto.BadgeCodes is { } bcs && bcs.Any())
            {
                var codes = bcs.Select(x => x.Trim()).Where(x => x != "").ToHashSet(StringComparer.OrdinalIgnoreCase);
                var valid = await db.Badges.Where(b => b.IsActive && codes.Contains(b.BadgeCode))
                                           .Select(b => b.BadgeCode).ToListAsync();
                foreach (var code in valid)
                    e.ProductBadges.Add(new ProductBadge { ProductId = e.ProductId, Badge = code, CreatedAt = _clock.UtcNow });
            }

            // Tính lại status theo tổng stock biến thể
            var totalStock = await db.ProductVariants.Where(v => v.ProductId == e.ProductId).SumAsync(v => (int?)v.StockQty) ?? 0;
            e.Status = ResolveStatusFromTotalStock(totalStock, dto.Status);

            await db.SaveChangesAsync();
            return NoContent();
        }

        // ===== TOGGLE PRODUCT VISIBILITY =====
        [HttpPatch("{id:guid}/toggle")]
        public async Task<IActionResult> Toggle(Guid id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var e = await db.Products.Include(p => p.ProductVariants)
                                     .FirstOrDefaultAsync(p => p.ProductId == id);
            if (e is null) return NotFound();

            var totalStock = e.ProductVariants.Sum(v => v.StockQty);
            e.Status = ToggleVisibility(e.Status, totalStock);
            e.UpdatedAt = _clock.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { e.ProductId, e.Status });
        }

        // ===== DELETE (giữ nguyên) =====
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var e = await db.Products.FindAsync(id);
            if (e is null) return NotFound();
            db.Products.Remove(e);
            await db.SaveChangesAsync();
            return NoContent();
        }
    }
}
