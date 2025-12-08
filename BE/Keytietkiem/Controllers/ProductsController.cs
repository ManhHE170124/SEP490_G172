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
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;
        private readonly IAuditLogger _auditLogger;

        private const int MaxProductNameLength = 100; // Đổi đúng với DB
        private const int MaxProductCodeLength = 50;

        public ProductsController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock,
            IAuditLogger auditLogger)
        {
            _dbFactory = dbFactory;
            _clock = clock;
            _auditLogger = auditLogger;
        }

        private static string NormalizeProductCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            var s = code.Trim();

            // Bỏ dấu
            s = s.Normalize(NormalizationForm.FormD);
            var chars = s.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
            s = new string(chars.ToArray());

            // Ký tự không phải chữ/số -> "_"
            s = Regex.Replace(s, "[^A-Za-z0-9]+", "_");
            s = Regex.Replace(s, "_+", "_").Trim('_');

            return s.ToUpperInvariant();
        }

        private static string ResolveStatusFromTotalStock(int totalStock, string? desired)
        {
            var d = (desired ?? string.Empty).Trim().ToUpperInvariant();

            if (totalStock <= 0)
            {
                // Nếu explicit INACTIVE -> coi là nháp/ẩn, không phải hết hàng
                if (d == "INACTIVE")
                    return "INACTIVE";

                // Mặc định hết hàng
                return "OUT_OF_STOCK";
            }

            if (!string.IsNullOrWhiteSpace(d) && ProductEnums.Statuses.Contains(d))
                return d;

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
                .Include(x => x.ProductVariants)
                .FirstOrDefaultAsync(x => x.ProductId == id);

            if (p is null) return NotFound();

            var dto = new ProductDetailDto(
                p.ProductId,
                p.ProductCode,
                p.ProductName,
                p.ProductType,
                p.Status,
                p.Categories.Select(c => c.CategoryId),
                p.ProductBadges.Select(b => b.Badge),
                p.ProductVariants
                    .Select(v => new ProductVariantMiniDto(
                        v.VariantId, v.VariantCode ?? "", v.Title, v.DurationDays,
                        v.StockQty, v.Status
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

            var normalizedCode = NormalizeProductCode(dto.ProductCode);
            if (string.IsNullOrWhiteSpace(normalizedCode))
                return BadRequest(new { message = "ProductCode is required" });
            if (normalizedCode.Length > MaxProductCodeLength)
                return BadRequest(new { message = $"ProductCode must not exceed {MaxProductCodeLength} characters." });

            var name = (dto.ProductName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { message = "ProductName is required" });
            if (name.Length > MaxProductNameLength)
                return BadRequest(new { message = $"ProductName must not exceed {MaxProductNameLength} characters." });

            if (await db.Products.AnyAsync(x => x.ProductCode == normalizedCode))
                return Conflict(new { message = "ProductCode already exists" });

            if (await db.Products.AnyAsync(x => x.ProductName == name))
                return Conflict(new { message = "ProductName already exists" });

            var entity = new Product
            {
                ProductId = Guid.NewGuid(),
                ProductCode = normalizedCode,
                ProductName = name,
                ProductType = dto.ProductType.Trim(),
                Status = "INACTIVE", // sẽ set lại theo stock sau
                Slug = dto.Slug ?? normalizedCode,
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

            var totalStock = 0;
            entity.Status = ResolveStatusFromTotalStock(totalStock, dto.Status);
            await db.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "Create",
                entityType: "Product",
                entityId: entity.ProductId.ToString(),
                before: null,
                after: new
                {
                    entity.ProductId,
                    entity.ProductCode,
                    entity.ProductName,
                    entity.ProductType,
                    entity.Status
                }
);

            return await GetById(entity.ProductId);
        }

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

            var beforeSnapshot = new
            {
                e.ProductId,
                e.ProductCode,
                e.ProductName,
                e.ProductType,
                e.Status,
                Categories = e.Categories.Select(c => c.CategoryId).ToList(),
                Badges = e.ProductBadges.Select(b => b.Badge).ToList()
            };

            var newName = dto.ProductName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newName))
                return BadRequest(new { message = "ProductName is required" });
            if (newName.Length > MaxProductNameLength)
                return BadRequest(new { message = $"ProductName must not exceed {MaxProductNameLength} characters." });

            var rawCode = dto.ProductCode;
            var normalizedCode = string.IsNullOrWhiteSpace(rawCode)
                ? e.ProductCode
                : NormalizeProductCode(rawCode);

            if (string.IsNullOrWhiteSpace(normalizedCode))
                return BadRequest(new { message = "ProductCode is required" });
            if (normalizedCode.Length > MaxProductCodeLength)
                return BadRequest(new { message = $"ProductCode must not exceed {MaxProductCodeLength} characters." });

            var hasVariants = e.ProductVariants.Any();
            var locked = hasVariants;

            if (locked)
            {
                // Không cho đổi tên hoặc mã nếu đã có biến thể hoặc FAQ
                if (!string.Equals(newName, e.ProductName, StringComparison.Ordinal) ||
                    !string.Equals(normalizedCode, e.ProductCode, StringComparison.Ordinal))
                {
                    return BadRequest(new
                    {
                        message = "Không thể sửa tên hoặc mã sản phẩm khi đã có biến thể thời gian hoặc FAQ."
                    });
                }
            }
            else
            {
                // Check trùng tên
                if (!string.Equals(newName, e.ProductName, StringComparison.Ordinal))
                {
                    var dupName = await db.Products
                        .AnyAsync(p => p.ProductId != e.ProductId && p.ProductName == newName);
                    if (dupName)
                        return Conflict(new { message = "ProductName already exists" });
                }

                // Check trùng mã
                if (!string.Equals(normalizedCode, e.ProductCode, StringComparison.Ordinal))
                {
                    var dupCode = await db.Products
                        .AnyAsync(p => p.ProductId != e.ProductId && p.ProductCode == normalizedCode);
                    if (dupCode)
                        return Conflict(new { message = "ProductCode already exists" });
                }

                e.ProductName = newName;
                e.ProductCode = normalizedCode;
            }

            // Phần còn lại giữ nguyên
            e.ProductType = dto.ProductType.Trim();
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

            await _auditLogger.LogAsync(
                HttpContext,
                action: "Update",
                entityType: "Product",
                entityId: e.ProductId.ToString(),
                before: beforeSnapshot,
                after: new
                {
                    e.ProductId,
                    e.ProductCode,
                    e.ProductName,
                    e.ProductType,
                    e.Status,
                    Categories = e.Categories.Select(c => c.CategoryId).ToList(),
                    Badges = e.ProductBadges.Select(b => b.Badge).ToList()
                }
 );

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

            var beforeSnapshot = new
            {
                e.ProductId,
                e.Status
            };

            var totalStock = e.ProductVariants.Sum(v => v.StockQty);
            e.Status = ToggleVisibility(e.Status, totalStock);
            e.UpdatedAt = _clock.UtcNow;
            await db.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "ToggleVisibility",
                entityType: "Product",
                entityId: e.ProductId.ToString(),
                before: beforeSnapshot,
                after: new { e.ProductId, e.Status }
);

            return Ok(new { e.ProductId, e.Status });
        }

        // ===== DELETE (chặn nếu còn Variant / FAQ / (tuỳ chọn) đơn hàng) =====
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Load product kèm các collection cần check
            var p = await db.Products
                .Include(x => x.ProductVariants)
                .FirstOrDefaultAsync(x => x.ProductId == id);

            if (p is null)
            {
                return NotFound();
            }

            // Đếm số Variant / FAQ đang gắn với product này
            var variantCount = p.ProductVariants.Count;

            // (Tuỳ bạn có bảng OrderItem / OrderLines thì thêm check đơn hàng ở đây)
            // Ví dụ (đổi tên DbSet và field cho đúng với project):
            // var orderCount = await db.OrderItems.CountAsync(o => o.ProductId == id);

            var hasVariants = variantCount > 0;
            var hasOrders = false; // set lại nếu bạn có check đơn hàng

            if (hasVariants || hasOrders)
            {
                var reasons = new List<string>();

                if (hasVariants)
                    reasons.Add($"{variantCount} biến thể / key");
                if (hasOrders)
                    reasons.Add("các đơn hàng đã phát sinh từ sản phẩm này");

                var reasonText = string.Join(", ", reasons);

                return Conflict(new
                {
                    message =
                        $"Không thể xoá sản phẩm \"{p.ProductName}\" vì đã có {reasonText}. " +
                        "Vui lòng ẩn sản phẩm (tắt hiển thị) hoặc xoá các dữ liệu liên quan trước khi xoá vĩnh viễn.",
                    variantCount,
                    hasVariants,
                    hasOrders
                });
            }

            var beforeSnapshot = new
            {
                p.ProductId,
                p.ProductCode,
                p.ProductName,
                p.Status
            };

            // Không còn gì phụ thuộc -> cho xoá
            db.Products.Remove(p);
            await db.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "Delete",
                entityType: "Product",
                entityId: p.ProductId.ToString(),
                before: beforeSnapshot,
                after: null
);

            return NoContent();
        }
    }
}
