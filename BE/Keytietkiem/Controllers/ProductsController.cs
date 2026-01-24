// File: Controllers/ProductsController.cs
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Keytietkiem.Utils;
using Keytietkiem.DTOs.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Utils.Constants;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products")]
    [Authorize]
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

            // 1. Bỏ dấu (normalize Unicode, bỏ NonSpacingMark)
            s = s.Normalize(NormalizationForm.FormD);
            var chars = s.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
            s = new string(chars.ToArray());

            // 2. Xoá toàn bộ khoảng trắng bên trong (join các "word code" lại)
            s = Regex.Replace(s, @"\s+", string.Empty);

            // 3. Các ký tự không phải chữ/số -> "_"
            s = Regex.Replace(s, "[^A-Za-z0-9]+", "_");

            // 4. Gộp "_" liên tiếp và trim "_" ở đầu/cuối
            s = Regex.Replace(s, "_+", "_").Trim('_');

            return s.ToUpperInvariant();
        }

        private static bool IsKeyType(string? pt)
        {
            var t = (pt ?? "").Trim();
            return t.Equals(ProductEnums.PERSONAL_KEY, StringComparison.OrdinalIgnoreCase)
                || t.Equals(ProductEnums.SHARED_KEY, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAccountType(string? pt)
        {
            var t = (pt ?? "").Trim();
            return t.Equals(ProductEnums.PERSONAL_ACCOUNT, StringComparison.OrdinalIgnoreCase)
                || t.Equals(ProductEnums.SHARED_ACCOUNT, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Quy ước status cho Product:
        /// - ACTIVE      : đang hiển thị và còn hàng.
        /// - OUT_OF_STOCK: hết hàng nhưng vẫn hiển thị.
        /// - INACTIVE    : ẩn hoàn toàn, chỉ khi admin set explicit.
        /// </summary>
        private static string ResolveStatusFromTotalStock(int totalStock, string? desired)
        {
            var d = (desired ?? string.Empty).Trim().ToUpperInvariant();

            // Admin cố tình set INACTIVE => giữ INACTIVE
            if (d == "INACTIVE")
                return "INACTIVE";

            // Hết hàng => OUT_OF_STOCK
            if (totalStock <= 0)
                return "OUT_OF_STOCK";

            // Còn hàng:
            if (!string.IsNullOrWhiteSpace(d) && ProductEnums.Statuses.Contains(d) && d != "OUT_OF_STOCK")
                return d;

            // Mặc định: ACTIVE
            return "ACTIVE";
        }

        private static string ToggleVisibility(string current, int totalStock)
        {
            // Hết hàng: vẫn hiển thị nhưng trạng thái là OUT_OF_STOCK
            if (totalStock <= 0) return "OUT_OF_STOCK";

            // Khi còn hàng: toggle giữa ACTIVE <-> INACTIVE
            return string.Equals(current, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                ? "INACTIVE"
                : "ACTIVE";
        }

        // ✅ Stock thật cho variant (key/account còn hạn + chưa gắn đơn + trừ reservation)
        private async Task<Dictionary<Guid, int>> ComputeAvailableStockByVariantIdAsync(
            KeytietkiemDbContext db,
            Dictionary<Guid, string?> productTypeByVariantId,
            DateTime nowUtc,
            CancellationToken ct)
        {
            var variantIds = productTypeByVariantId?.Keys?.Distinct().ToList() ?? new List<Guid>();
            if (variantIds.Count == 0) return new Dictionary<Guid, int>();

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

            var result = new Dictionary<Guid, int>();
            foreach (var id in variantIds)
            {
                if (!productTypeByVariantId.TryGetValue(id, out var ptRaw))
                    continue;

                var pt = (ptRaw ?? "").Trim();

                var raw = 0;
                if (IsKeyType(pt))
                    raw = keyCountByVariantId.TryGetValue(id, out var kq) ? kq : 0;
                else if (pt.Equals(ProductEnums.PERSONAL_ACCOUNT, StringComparison.OrdinalIgnoreCase))
                    raw = personalAccountCountByVariantId.TryGetValue(id, out var aq) ? aq : 0;
                else if (pt.Equals(ProductEnums.SHARED_ACCOUNT, StringComparison.OrdinalIgnoreCase))
                    raw = sharedAccountSlotsByVariantId.TryGetValue(id, out var sq) ? sq : 0;
                else
                    continue; // unknown type -> caller fallback to v.StockQty

                var reserved = reservedByVariantId.TryGetValue(id, out var rq) ? rq : 0;
                var available = raw - reserved;
                if (available < 0) available = 0;

                result[id] = available;
            }

            return result;
        }

        private async Task<int> ComputeTotalAvailableStockForProductAsync(
            KeytietkiemDbContext db,
            Product product,
            DateTime nowUtc,
            CancellationToken ct)
        {
            var map = product.ProductVariants
                .GroupBy(v => v.VariantId)
                .ToDictionary(g => g.Key, g => product.ProductType);

            var stockByVariantId = await ComputeAvailableStockByVariantIdAsync(db, map, nowUtc, ct);

            var total = product.ProductVariants.Sum(v =>
                stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty);

            return total;
        }

        // ===== LIST (không giá) =====
        [HttpGet("list")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<PagedResult<ProductListItemDto>>> List(
            [FromQuery] string? keyword,
            [FromQuery] int? categoryId,
            [FromQuery(Name = "type")] string? productType,
            [FromQuery] string? status,
            [FromQuery] string? badge,
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
                q = q.Where(p => productTypes.Contains(p.ProductType));
            if (categoryId is not null)
                q = q.Where(p => p.Categories.Any(c => c.CategoryId == categoryId));

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

            // NOTE: sort stock ở DB vẫn dựa theo StockQty (cache). Nếu muốn sort theo stock thật,
            // cần chiến lược precompute/cached hoặc materialize toàn bộ -> nặng.
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

            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            var total = await q.CountAsync(ct);

            var pageProducts = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var productTypeByVariantId = pageProducts
                .SelectMany(p => p.ProductVariants.Select(v => new { v.VariantId, p.ProductType }))
                .GroupBy(x => x.VariantId)
                .ToDictionary(g => g.Key, g => g.First().ProductType);

            var stockByVariantId = await ComputeAvailableStockByVariantIdAsync(
                db,
                productTypeByVariantId,
                nowUtc,
                ct);

            var items = pageProducts
                .Select(p =>
                {
                    var totalStock = p.ProductVariants.Sum(v =>
                        stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty);

                    // ✅ status hiển thị theo stock thật, nhưng vẫn tôn trọng INACTIVE (admin set explicit)
                    var effectiveStatus = ResolveStatusFromTotalStock(totalStock, p.Status);

                    return new ProductListItemDto(
                        p.ProductId,
                        p.ProductCode,
                        p.ProductName,
                        p.ProductType,
                        totalStock,
                        effectiveStatus,
                        p.Categories.Select(c => c.CategoryId),
                        p.ProductBadges.Select(b => b.Badge)
                    );
                })
                .ToList();

            return Ok(new PagedResult<ProductListItemDto>(items, total, page, pageSize));
        }

        // ===== DETAIL (Images + FAQs + Variants) =====
        [HttpGet("{id:guid}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<ProductDetailDto>> GetById(Guid id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var p = await db.Products.AsNoTracking()
                .Include(x => x.Categories)
                .Include(x => x.ProductBadges)
                .Include(x => x.ProductVariants)
                .FirstOrDefaultAsync(x => x.ProductId == id);

            if (p is null) return NotFound();

            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            var productTypeByVariantId = p.ProductVariants
                .GroupBy(v => v.VariantId)
                .ToDictionary(g => g.Key, g => p.ProductType);

            var stockByVariantId = await ComputeAvailableStockByVariantIdAsync(
                db,
                productTypeByVariantId,
                nowUtc,
                ct);

            var totalStock = p.ProductVariants.Sum(v =>
                stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty);

            var effectiveProductStatus = ResolveStatusFromTotalStock(totalStock, p.Status);

            var dto = new ProductDetailDto(
                p.ProductId,
                p.ProductCode,
                p.ProductName,
                p.ProductType,
                effectiveProductStatus,
                p.Categories.Select(c => c.CategoryId),
                p.ProductBadges.Select(b => b.Badge),
                p.ProductVariants
                    .Select(v =>
                    {
                        var avail = stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty;
                        var effectiveVariantStatus = ResolveStatusFromTotalStock(avail, v.Status);

                        return new ProductVariantMiniDto(
                            v.VariantId,
                            v.VariantCode ?? "",
                            v.Title,
                            v.DurationDays,
                            avail,
                            effectiveVariantStatus
                        );
                    })
            );

            return Ok(dto);
        }

        // ===== CREATE (không giá) =====
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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
                Status = "INACTIVE",
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

            // Lúc tạo mới chưa có variant => totalStock = 0
            var totalStock = 0;

            // Nếu admin truyền INACTIVE thì giữ, còn lại theo stock => OUT_OF_STOCK
            entity.Status = ResolveStatusFromTotalStock(totalStock, dto.Status);
            await db.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "CreateProduct",
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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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
                if (!string.Equals(newName, e.ProductName, StringComparison.Ordinal))
                {
                    var dupName = await db.Products
                        .AnyAsync(p => p.ProductId != e.ProductId && p.ProductName == newName);
                    if (dupName)
                        return Conflict(new { message = "ProductName already exists" });
                }

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

            e.ProductType = dto.ProductType.Trim();
            e.Slug = dto.Slug ?? e.Slug;
            e.UpdatedAt = _clock.UtcNow;

            e.Categories.Clear();
            if (dto.CategoryIds is { } cids && cids.Any())
            {
                var cats = await db.Categories.Where(c => cids.Contains(c.CategoryId)).ToListAsync();
                foreach (var c in cats) e.Categories.Add(c);
            }

            e.ProductBadges.Clear();
            if (dto.BadgeCodes is { } bcs && bcs.Any())
            {
                var codes = bcs.Select(x => x.Trim()).Where(x => x != "").ToHashSet(StringComparer.OrdinalIgnoreCase);
                var valid = await db.Badges.Where(b => b.IsActive && codes.Contains(b.BadgeCode))
                                           .Select(b => b.BadgeCode).ToListAsync();
                foreach (var code in valid)
                    e.ProductBadges.Add(new ProductBadge { ProductId = e.ProductId, Badge = code, CreatedAt = _clock.UtcNow });
            }

            // ✅ totalStock theo stock thật (key/account - reservation)
            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;
            var totalStock = await ComputeTotalAvailableStockForProductAsync(db, e, nowUtc, ct);

            var desiredStatus = string.IsNullOrWhiteSpace(dto.Status) ? null : dto.Status;

            // ✅ Nếu đang INACTIVE và admin không truyền Status => giữ INACTIVE
            if (string.Equals(e.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(desiredStatus))
            {
                // giữ nguyên
            }
            else
            {
                // Nếu admin truyền desired => ưu tiên desired (INACTIVE vẫn được giữ),
                // nếu không truyền => dùng status hiện tại làm "desired" để auto OUT_OF_STOCK/ACTIVE theo stock
                e.Status = ResolveStatusFromTotalStock(totalStock, desiredStatus ?? e.Status);
            }

            await db.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdateProduct",
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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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

            // ✅ totalStock theo stock thật
            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;
            var totalStock = await ComputeTotalAvailableStockForProductAsync(db, e, nowUtc, ct);

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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var p = await db.Products
                .Include(x => x.ProductVariants)
                .FirstOrDefaultAsync(x => x.ProductId == id);

            if (p is null)
                return NotFound();

            var variantCount = p.ProductVariants.Count;
            var hasVariants = variantCount > 0;
            var hasOrders = false;

            if (hasVariants || hasOrders)
            {
                var reasons = new List<string>();
                if (hasVariants) reasons.Add($"{variantCount} biến thể / key");
                if (hasOrders) reasons.Add("các đơn hàng đã phát sinh từ sản phẩm này");

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

            db.Products.Remove(p);
            await db.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "DeleteProduct",
                entityType: "Product",
                entityId: p.ProductId.ToString(),
                before: beforeSnapshot,
                after: null
            );

            return NoContent();
        }

        // ===== LOW STOCK REPORT (stock thật) =====
        [HttpGet("low-stock")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> GetLowStockProducts(
            [FromQuery] string? type = null, // "KEYS" | "ACCOUNTS" | null (all)
            [FromQuery] int threshold = 20)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var query = db.Products.AsNoTracking()
                .Include(p => p.ProductVariants)
                .Where(p => p.Status != "INACTIVE")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
            {
                var t = type.Trim().ToUpperInvariant();
                if (t == "KEYS")
                {
                    query = query.Where(p => p.ProductType == ProductEnums.PERSONAL_KEY || p.ProductType == ProductEnums.SHARED_KEY);
                }
                else if (t == "ACCOUNTS")
                {
                    query = query.Where(p => p.ProductType == ProductEnums.PERSONAL_ACCOUNT || p.ProductType == ProductEnums.SHARED_ACCOUNT);
                }
            }

            var products = await query.ToListAsync();

            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            // build variantId -> productType map for all variants in the result set
            var productTypeByVariantId = products
                .SelectMany(p => p.ProductVariants.Select(v => new { v.VariantId, p.ProductType }))
                .GroupBy(x => x.VariantId)
                .ToDictionary(g => g.Key, g => g.First().ProductType);

            var stockByVariantId = await ComputeAvailableStockByVariantIdAsync(db, productTypeByVariantId, nowUtc, ct);

            var finalResult = new List<object>();
            foreach (var p in products)
            {
                var available = p.ProductVariants.Sum(v =>
                    stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty);

                if (available < threshold)
                {
                    finalResult.Add(new
                    {
                        p.ProductId,
                        p.ProductCode,
                        p.ProductName,
                        p.ProductType,
                        AvailableCount = available,
                        Threshold = threshold
                    });
                }
            }

            return Ok(finalResult);
        }
    }
}
