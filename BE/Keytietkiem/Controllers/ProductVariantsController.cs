// File: Controllers/ProductVariantsController.cs
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products/{productId:guid}/variants")]
    [Authorize]
    public class ProductVariantsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;
        private readonly IAuditLogger _auditLogger;

        private const int TitleMaxLength = 60;
        private const int CodeMaxLength = 50;
        private const decimal MaxPriceValue = 9999999999999999.99M; // decimal(18,2)

        public ProductVariantsController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock,
            IAuditLogger auditLogger)
        {
            _dbFactory = dbFactory;
            _clock = clock;
            _auditLogger = auditLogger;
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
                    continue;

                var reserved = reservedByVariantId.TryGetValue(id, out var rq) ? rq : 0;
                var available = raw - reserved;
                if (available < 0) available = 0;

                result[id] = available;
            }

            return result;
        }

        private static string NormalizeStatus(string? s)
        {
            var u = (s ?? "").Trim().ToUpperInvariant();
            return ProductEnums.Statuses.Contains(u) ? u : "INACTIVE";
        }

        /// <summary>
        /// Quy ước status cho Variant:
        /// - INACTIVE chỉ khi admin set explicit.
        /// - OUT_OF_STOCK khi stock<=0 (và không INACTIVE)
        /// - Còn hàng: ACTIVE (hoặc status hợp lệ khác nếu muốn giữ)
        /// </summary>
        private static string ResolveStatusFromStock(int stockQty, string? desiredOrCurrent)
        {
            var d = (desiredOrCurrent ?? string.Empty).Trim().ToUpperInvariant();

            if (d == "INACTIVE")
                return "INACTIVE";

            if (stockQty <= 0)
                return "OUT_OF_STOCK";

            if (!string.IsNullOrWhiteSpace(d) && ProductEnums.Statuses.Contains(d) && d != "OUT_OF_STOCK")
                return d;

            return "ACTIVE";
        }

        private static void TrySetProductStockQty(object productEntity, int totalStock)
        {
            var prop = productEntity.GetType().GetProperty("StockQty");
            if (prop == null || !prop.CanWrite) return;

            var t = prop.PropertyType;
            if (t == typeof(int)) { prop.SetValue(productEntity, totalStock); return; }
            if (t == typeof(int?)) { prop.SetValue(productEntity, (int?)totalStock); return; }
            if (t == typeof(long)) { prop.SetValue(productEntity, (long)totalStock); return; }
            if (t == typeof(long?)) { prop.SetValue(productEntity, (long?)totalStock); return; }

            try
            {
                var target = Nullable.GetUnderlyingType(t) ?? t;
                var converted = Convert.ChangeType(totalStock, target);
                prop.SetValue(productEntity, converted);
            }
            catch { }
        }

        /// <summary>
        /// Recalc status Product dựa theo tổng stock thật của variant:
        /// - Nếu Product đang INACTIVE và không truyền desiredStatus => giữ nguyên.
        /// - Nếu không INACTIVE => OUT_OF_STOCK khi total<=0, còn lại ACTIVE (hoặc desired hợp lệ).
        /// </summary>
        private async Task RecalcProductStatus(KeytietkiemDbContext db, Guid productId, string? desiredStatus = null)
        {
            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            var p = await db.Products
                            .Include(x => x.ProductVariants)
                            .FirstAsync(x => x.ProductId == productId, ct);

            var map = p.ProductVariants
                .GroupBy(v => v.VariantId)
                .ToDictionary(g => g.Key, g => p.ProductType);

            var stockByVariantId = await ComputeAvailableStockByVariantIdAsync(db, map, nowUtc, ct);

            var totalStock = p.ProductVariants.Sum(v =>
                stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty);

            TrySetProductStockQty(p, totalStock);

            if (string.Equals(p.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(desiredStatus))
            {
                p.UpdatedAt = _clock.UtcNow;
                return;
            }

            if (!string.IsNullOrWhiteSpace(desiredStatus))
            {
                var d = desiredStatus.Trim().ToUpperInvariant();
                if (ProductEnums.Statuses.Contains(d))
                {
                    if (d == "INACTIVE")
                        p.Status = "INACTIVE";
                    else
                        p.Status = totalStock <= 0 ? "OUT_OF_STOCK" : d;

                    p.UpdatedAt = _clock.UtcNow;
                    return;
                }
            }

            // Không có desiredStatus: tự suy từ tồn kho
            p.Status = totalStock <= 0 ? "OUT_OF_STOCK" : "ACTIVE";
            p.UpdatedAt = _clock.UtcNow;
        }

        private static string ToggleVisibility(string? current, int stock)
        {
            if (stock <= 0) return "OUT_OF_STOCK";

            var cur = NormalizeStatus(current);
            return cur == "ACTIVE" ? "INACTIVE" : "ACTIVE";
        }

        private static (bool IsValid, ActionResult? ErrorResult) ValidateCommonFields(
            string title,
            string variantCode,
            int? durationDays,
            int? warrantyDays)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return (false, new BadRequestObjectResult(new { code = "TITLE_REQUIRED", message = "Tên biến thể là bắt buộc." }));
            }
            if (title.Length > TitleMaxLength)
            {
                return (false, new BadRequestObjectResult(new { code = "TITLE_TOO_LONG", message = $"Tên biến thể không được vượt quá {TitleMaxLength} ký tự." }));
            }

            if (string.IsNullOrWhiteSpace(variantCode))
            {
                return (false, new BadRequestObjectResult(new { code = "CODE_REQUIRED", message = "Mã biến thể là bắt buộc." }));
            }
            if (variantCode.Length > CodeMaxLength)
            {
                return (false, new BadRequestObjectResult(new { code = "CODE_TOO_LONG", message = $"Mã biến thể không được vượt quá {CodeMaxLength} ký tự." }));
            }

            if (durationDays.HasValue && durationDays.Value < 0)
            {
                return (false, new BadRequestObjectResult(new { code = "DURATION_INVALID", message = "Thời lượng (ngày) phải lớn hơn hoặc bằng 0." }));
            }
            if (warrantyDays.HasValue && warrantyDays.Value < 0)
            {
                return (false, new BadRequestObjectResult(new { code = "WARRANTY_INVALID", message = "Bảo hành (ngày) phải lớn hơn hoặc bằng 0." }));
            }
            if (durationDays.HasValue && warrantyDays.HasValue && durationDays.Value <= warrantyDays.Value)
            {
                return (false, new BadRequestObjectResult(new { code = "DURATION_LE_WARRANTY", message = "Thời lượng (ngày) phải lớn hơn số ngày bảo hành." }));
            }

            return (true, null);
        }

        private (bool IsValid, ActionResult? ErrorResult) ValidatePriceFields(
            decimal sellPrice,
            decimal listPrice,
            decimal? currentCogsPrice = null)
        {
            if (sellPrice < 0)
                return (false, new BadRequestObjectResult(new { code = "SELL_PRICE_INVALID", message = "Giá bán phải lớn hơn hoặc bằng 0." }));

            if (listPrice < 0)
                return (false, new BadRequestObjectResult(new { code = "LIST_PRICE_INVALID", message = "Giá niêm yết phải lớn hơn hoặc bằng 0." }));

            if (sellPrice > MaxPriceValue || listPrice > MaxPriceValue)
                return (false, new BadRequestObjectResult(new { code = "PRICE_TOO_LARGE", message = "Giá không được vượt quá giới hạn cho phép (decimal 18,2)." }));

            if (sellPrice > listPrice)
                return (false, new BadRequestObjectResult(new { code = "SELL_GT_LIST", message = "Giá bán không được lớn hơn giá niêm yết." }));

            if (currentCogsPrice.HasValue && currentCogsPrice.Value > 0 && listPrice < currentCogsPrice.Value)
                return (false, new BadRequestObjectResult(new { code = "LIST_LT_COGS", message = "Giá niêm yết không được nhỏ hơn giá vốn." }));

            return (true, null);
        }

        private static string NormalizeString(string? s) => (s ?? string.Empty).Trim();

        // ===== LIST =====
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<PagedResult<ProductVariantListItemDto>>> List(
            Guid productId,
            [FromQuery] ProductVariantListQuery query)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var productType = await db.Products
                .AsNoTracking()
                .Where(p => p.ProductId == productId)
                .Select(p => p.ProductType)
                .FirstOrDefaultAsync();

            if (productType == null) return NotFound();

            var q = db.ProductVariants.AsNoTracking()
                                      .Where(v => v.ProductId == productId);

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var s = query.Q.Trim();
                q = q.Where(v =>
                    EF.Functions.Like(v.Title, $"%{s}%") ||
                    EF.Functions.Like(v.VariantCode ?? "", $"%{s}%"));
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var st = query.Status.Trim().ToUpperInvariant();
                q = q.Where(v => (v.Status ?? "").ToUpper() == st);
            }

            if (!string.IsNullOrWhiteSpace(query.Dur))
            {
                switch (query.Dur)
                {
                    case "<=30":
                        q = q.Where(v => (v.DurationDays ?? 0) <= 30);
                        break;
                    case "31-180":
                        q = q.Where(v => (v.DurationDays ?? 0) >= 31 && (v.DurationDays ?? 0) <= 180);
                        break;
                    case ">180":
                        q = q.Where(v => (v.DurationDays ?? 0) > 180);
                        break;
                }
            }

            if (query.MinPrice.HasValue)
                q = q.Where(v => v.SellPrice >= query.MinPrice.Value);

            if (query.MaxPrice.HasValue)
                q = q.Where(v => v.SellPrice <= query.MaxPrice.Value);

            var sort = (query.Sort ?? "created").Trim().ToLowerInvariant();
            var desc = string.Equals(query.Dir, "desc", StringComparison.OrdinalIgnoreCase);

            // NOTE: sort theo stock ở DB vẫn dựa StockQty (cache)
            q = sort switch
            {
                "title" => desc ? q.OrderByDescending(v => v.Title) : q.OrderBy(v => v.Title),
                "duration" => desc ? q.OrderByDescending(v => v.DurationDays) : q.OrderBy(v => v.DurationDays),
                "stock" => desc ? q.OrderByDescending(v => v.StockQty) : q.OrderBy(v => v.StockQty),
                "status" => desc ? q.OrderByDescending(v => v.Status) : q.OrderBy(v => v.Status),
                "views" => desc ? q.OrderByDescending(v => v.ViewCount) : q.OrderBy(v => v.ViewCount),
                "price" => desc ? q.OrderByDescending(v => v.SellPrice) : q.OrderBy(v => v.SellPrice),
                _ => desc ? q.OrderByDescending(v => v.CreatedAt) : q.OrderBy(v => v.CreatedAt),
            };

            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);
            var total = await q.CountAsync();

            var pageVariants = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            var map = pageVariants
                .GroupBy(v => v.VariantId)
                .ToDictionary(g => g.Key, g => productType);

            var stockByVariantId = await ComputeAvailableStockByVariantIdAsync(db, map, nowUtc, ct);

            var items = pageVariants.Select(v =>
            {
                var avail = stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty;

                // ✅ status hiển thị theo stock thật, nhưng vẫn tôn trọng INACTIVE
                var effectiveStatus = ResolveStatusFromStock(avail, v.Status);

                return new ProductVariantListItemDto(
                    v.VariantId,
                    v.VariantCode ?? "",
                    v.Title,
                    v.DurationDays,
                    avail,
                    effectiveStatus,
                    v.Thumbnail,
                    v.ViewCount,
                    v.SellPrice,
                    v.ListPrice,
                    v.CogsPrice
                );
            }).ToList();

            return Ok(new PagedResult<ProductVariantListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                Items = items
            });
        }

        // ===== DETAIL =====
        [HttpGet("{variantId:guid}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<ProductVariantDetailDto>> Get(Guid productId, Guid variantId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var productType = await db.Products
                .AsNoTracking()
                .Where(p => p.ProductId == productId)
                .Select(p => p.ProductType)
                .FirstOrDefaultAsync();

            if (productType == null) return NotFound();

            var v = await db.ProductVariants
                            .FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            var map = new Dictionary<Guid, string?> { [v.VariantId] = productType };
            var stockByVariantId = await ComputeAvailableStockByVariantIdAsync(db, map, nowUtc, ct);
            var avail = stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty;

            var effectiveStatus = ResolveStatusFromStock(avail, v.Status);

            var hasSections = await db.ProductSections.AnyAsync(s => s.VariantId == variantId);

            return Ok(new
            {
                v.VariantId,
                v.ProductId,
                VariantCode = v.VariantCode ?? "",
                v.Title,
                v.DurationDays,
                StockQty = avail, // ✅ stock thật
                v.WarrantyDays,
                v.Thumbnail,
                v.MetaTitle,
                v.MetaDescription,
                v.ViewCount,
                Status = effectiveStatus, // ✅ status theo stock thật (trừ INACTIVE)
                v.SellPrice,
                v.ListPrice,
                v.CogsPrice,
                HasSections = hasSections
            });
        }

        // ===== CREATE =====
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<ActionResult<ProductVariantDetailDto>> Create(Guid productId, ProductVariantCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Products.FirstOrDefaultAsync(x => x.ProductId == productId);
            if (p is null) return NotFound();

            var title = NormalizeString(dto.Title);
            var variantCode = NormalizeString(dto.VariantCode);

            var durationDays = dto.DurationDays;
            var warrantyDays = dto.WarrantyDays;

            var (isValid, errorResult) = ValidateCommonFields(title, variantCode, durationDays, warrantyDays);
            if (!isValid) return errorResult!;

            if (!dto.SellPrice.HasValue)
                return BadRequest(new { code = "SELL_PRICE_REQUIRED", message = "Giá bán là bắt buộc." });

            if (!dto.ListPrice.HasValue)
                return BadRequest(new { code = "LIST_PRICE_REQUIRED", message = "Giá niêm yết là bắt buộc." });

            var sellPrice = dto.SellPrice.Value;
            var listPrice = dto.ListPrice.Value;

            var (priceValid, priceError) = ValidatePriceFields(sellPrice, listPrice, currentCogsPrice: null);
            if (!priceValid) return priceError!;

            var normalizedTitle = title.ToLower();
            var titleExists = await db.ProductVariants.AnyAsync(v =>
                v.ProductId == productId &&
                v.Title != null &&
                v.Title.ToLower() == normalizedTitle);
            if (titleExists)
                return Conflict(new { code = "VARIANT_TITLE_DUPLICATE", message = "Tên biến thể đã tồn tại trong sản phẩm này." });

            var normalizedCode = variantCode.ToLower();
            var codeExists = await db.ProductVariants.AnyAsync(v =>
                v.ProductId == productId &&
                v.VariantCode != null &&
                v.VariantCode.ToLower() == normalizedCode);
            if (codeExists)
                return Conflict(new { code = "VARIANT_CODE_DUPLICATE", message = "Mã biến thể đã tồn tại trong sản phẩm này." });

            var stock = dto.StockQty;
            if (stock < 0) stock = 0;

            // ✅ Với KEY/ACCOUNT: stock thật phụ thuộc key/account => lúc tạo mới coi như 0 để set status đúng.
            var effectiveStockForStatus = (IsKeyType(p.ProductType) || IsAccountType(p.ProductType)) ? 0 : stock;
            var status = ResolveStatusFromStock(effectiveStockForStatus, dto.Status);

            var v = new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = productId,
                VariantCode = variantCode,
                Title = title,
                DurationDays = durationDays,
                StockQty = stock,
                WarrantyDays = warrantyDays,
                Thumbnail = string.IsNullOrWhiteSpace(dto.Thumbnail) ? null : dto.Thumbnail!.Trim(),
                MetaTitle = string.IsNullOrWhiteSpace(dto.MetaTitle) ? null : dto.MetaTitle!.Trim(),
                MetaDescription = string.IsNullOrWhiteSpace(dto.MetaDescription) ? null : dto.MetaDescription!.Trim(),
                ViewCount = 0,
                Status = status,
                SellPrice = sellPrice,
                ListPrice = listPrice,
                CreatedAt = _clock.UtcNow
            };

            db.ProductVariants.Add(v);
            await db.SaveChangesAsync();

            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "CreateProductVariant",
                entityType: "ProductVariant",
                entityId: v.VariantId.ToString(),
                before: null,
                after: new
                {
                    v.VariantId,
                    v.ProductId,
                    v.VariantCode,
                    v.Title,
                    v.DurationDays,
                    v.StockQty,
                    v.WarrantyDays,
                    v.Status,
                    v.SellPrice,
                    v.ListPrice,
                    v.CogsPrice
                }
            );

            return CreatedAtAction(nameof(Get), new { productId, variantId = v.VariantId },
                new ProductVariantDetailDto(
                    v.VariantId,
                    v.ProductId,
                    v.VariantCode ?? "",
                    v.Title,
                    v.DurationDays,
                    v.StockQty,
                    v.WarrantyDays,
                    v.Thumbnail,
                    v.MetaTitle,
                    v.MetaDescription,
                    v.ViewCount,
                    v.Status,
                    v.SellPrice,
                    v.ListPrice,
                    v.CogsPrice
                ));
        }

        // ===== UPDATE =====
        [HttpPut("{variantId:guid}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> Update(Guid productId, Guid variantId, ProductVariantUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var productType = await db.Products
                .Where(p => p.ProductId == productId)
                .Select(p => p.ProductType)
                .FirstOrDefaultAsync();

            if (productType == null) return NotFound();

            var v = await db.ProductVariants.FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            var before = new
            {
                v.VariantId,
                v.ProductId,
                v.VariantCode,
                v.Title,
                v.DurationDays,
                v.StockQty,
                v.WarrantyDays,
                v.Status,
                v.SellPrice,
                v.ListPrice,
                v.CogsPrice
            };

            var title = NormalizeString(dto.Title);
            var variantCode = NormalizeString(dto.VariantCode ?? v.VariantCode ?? string.Empty);

            var durationDays = dto.DurationDays;
            var warrantyDays = dto.WarrantyDays;

            var (isValid, errorResult) = ValidateCommonFields(title, variantCode, durationDays, warrantyDays);
            if (!isValid) return errorResult!;

            var newSellPrice = dto.SellPrice ?? v.SellPrice;
            var newListPrice = dto.ListPrice ?? v.ListPrice;

            var (priceValid, priceError) = ValidatePriceFields(newSellPrice, newListPrice, v.CogsPrice);
            if (!priceValid) return priceError!;

            var hasSections = await db.ProductSections.AnyAsync(s => s.VariantId == variantId);

            if (hasSections &&
                !string.IsNullOrWhiteSpace(v.VariantCode) &&
                !string.Equals(v.VariantCode.Trim(), variantCode, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    code = "VARIANT_CODE_IN_USE_SECTION",
                    message = "Không thể thay đổi mã biến thể vì đang được sử dụng trong các section. Vui lòng cập nhật hoặc xoá các section liên quan trước."
                });
            }

            var normalizedTitle = title.ToLower();
            var titleExists = await db.ProductVariants.AnyAsync(x =>
                x.ProductId == productId &&
                x.VariantId != variantId &&
                x.Title != null &&
                x.Title.ToLower() == normalizedTitle);
            if (titleExists)
                return Conflict(new { code = "VARIANT_TITLE_DUPLICATE", message = "Tên biến thể đã tồn tại trong sản phẩm này." });

            var normalizedCode = variantCode.ToLower();
            var codeExists = await db.ProductVariants.AnyAsync(x =>
                x.ProductId == productId &&
                x.VariantId != variantId &&
                x.VariantCode != null &&
                x.VariantCode.ToLower() == normalizedCode);
            if (codeExists)
                return Conflict(new { code = "VARIANT_CODE_DUPLICATE", message = "Mã biến thể đã tồn tại trong sản phẩm này." });

            v.Title = title;
            v.DurationDays = durationDays;
            v.StockQty = dto.StockQty;
            v.WarrantyDays = warrantyDays;
            v.Thumbnail = string.IsNullOrWhiteSpace(dto.Thumbnail) ? null : dto.Thumbnail!.Trim();
            v.MetaTitle = string.IsNullOrWhiteSpace(dto.MetaTitle) ? null : dto.MetaTitle!.Trim();
            v.MetaDescription = string.IsNullOrWhiteSpace(dto.MetaDescription) ? null : dto.MetaDescription!.Trim();
            v.SellPrice = newSellPrice;
            v.ListPrice = newListPrice;

            if (!hasSections)
                v.VariantCode = variantCode;

            var ct = HttpContext.RequestAborted;
            var nowUtc = _clock.UtcNow;

            // ✅ effective stock để tính status: KEY/ACCOUNT dùng stock thật (keys/accounts - reservation), còn lại dùng StockQty
            int effectiveStockForStatus;
            if (IsKeyType(productType) || IsAccountType(productType))
            {
                var map = new Dictionary<Guid, string?> { [v.VariantId] = productType };
                var stockByVariantId = await ComputeAvailableStockByVariantIdAsync(db, map, nowUtc, ct);
                effectiveStockForStatus = stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty;
            }
            else
            {
                effectiveStockForStatus = v.StockQty;
            }

            var desired = string.IsNullOrWhiteSpace(dto.Status) ? null : dto.Status;

            // ✅ Nếu đang INACTIVE và admin không truyền Status => giữ INACTIVE
            if (string.Equals(v.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(desired))
            {
                // giữ nguyên
            }
            else
            {
                // Nếu có desired: ưu tiên desired; nếu không: dùng status hiện tại để auto OUT_OF_STOCK/ACTIVE theo stock
                v.Status = ResolveStatusFromStock(effectiveStockForStatus, desired ?? v.Status);
            }

            v.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

            var after = new
            {
                v.VariantId,
                v.ProductId,
                v.VariantCode,
                v.Title,
                v.DurationDays,
                v.StockQty,
                v.WarrantyDays,
                v.Status,
                v.SellPrice,
                v.ListPrice,
                v.CogsPrice
            };

            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdateProductVariant",
                entityType: "ProductVariant",
                entityId: v.VariantId.ToString(),
                before: before,
                after: after
            );

            return NoContent();
        }

        // ===== DELETE =====
        [HttpDelete("{variantId:guid}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> Delete(Guid productId, Guid variantId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var v = await db.ProductVariants
                            .FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            var before = new
            {
                v.VariantId,
                v.ProductId,
                v.VariantCode,
                v.Title,
                v.DurationDays,
                v.StockQty,
                v.WarrantyDays,
                v.Status,
                v.SellPrice,
                v.ListPrice,
                v.CogsPrice
            };

            var hasSections = await db.ProductSections.AnyAsync(s => s.VariantId == variantId);
            if (hasSections)
            {
                return Conflict(new
                {
                    code = "VARIANT_IN_USE_SECTION",
                    message = "Không thể xoá biến thể này vì đang được sử dụng trong các section. Vui lòng xoá hoặc cập nhật các section liên quan trước."
                });
            }

            db.ProductVariants.Remove(v);
            await db.SaveChangesAsync();

            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "DeleteProductVariant",
                entityType: "ProductVariant",
                entityId: v.VariantId.ToString(),
                before: before,
                after: null
            );

            return NoContent();
        }

        // ===== TOGGLE =====
        [HttpPatch("{variantId:guid}/toggle")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> Toggle(Guid productId, Guid variantId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();

                var productType = await db.Products
                    .Where(p => p.ProductId == productId)
                    .Select(p => p.ProductType)
                    .FirstOrDefaultAsync();

                if (productType == null) return NotFound();

                var v = await db.ProductVariants
                                .FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
                if (v is null) return NotFound();

                var before = new
                {
                    v.VariantId,
                    v.ProductId,
                    v.Status,
                    v.StockQty
                };

                var ct = HttpContext.RequestAborted;
                var nowUtc = _clock.UtcNow;

                int effectiveStock;
                if (IsKeyType(productType) || IsAccountType(productType))
                {
                    var map = new Dictionary<Guid, string?> { [v.VariantId] = productType };
                    var stockByVariantId = await ComputeAvailableStockByVariantIdAsync(db, map, nowUtc, ct);
                    effectiveStock = stockByVariantId.TryGetValue(v.VariantId, out var st) ? st : v.StockQty;
                }
                else
                {
                    effectiveStock = v.StockQty;
                }

                v.Status = ToggleVisibility(v.Status, effectiveStock);
                v.UpdatedAt = _clock.UtcNow;

                await db.SaveChangesAsync();
                await RecalcProductStatus(db, productId);
                await db.SaveChangesAsync();

                var after = new
                {
                    v.VariantId,
                    v.ProductId,
                    v.Status,
                    v.StockQty
                };

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "ToggleProductVariantStatus",
                    entityType: "ProductVariant",
                    entityId: v.VariantId.ToString(),
                    before: before,
                    after: after
                );

                return Ok(new { VariantId = v.VariantId, Status = v.Status });
            }
            catch (Exception ex)
            {
                return Problem(title: "Toggle variant status failed",
                               detail: ex.Message,
                               statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
