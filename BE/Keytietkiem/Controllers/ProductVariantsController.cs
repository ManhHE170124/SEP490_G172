// Controllers/ProductVariantsController.cs
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products/{productId:guid}/variants")]
    public class ProductVariantsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        private const int TitleMaxLength = 60;
        private const int CodeMaxLength = 50;

        public ProductVariantsController(IDbContextFactory<KeytietkiemDbContext> dbFactory, IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        private static string NormalizeStatus(string? s)
        {
            var u = (s ?? "").Trim().ToUpperInvariant();
            return ProductEnums.Statuses.Contains(u) ? u : "INACTIVE";
        }

        private static string ResolveStatusFromStock(int stockQty, string? desired)
        {
            if (stockQty <= 0) return "OUT_OF_STOCK";
            var d = NormalizeStatus(desired);
            return d == "OUT_OF_STOCK" ? "ACTIVE" : d;
        }

        private async Task RecalcProductStatus(KeytietkiemDbContext db, Guid productId, string? desiredStatus = null)
        {
            var p = await db.Products
                            .Include(x => x.ProductVariants)
                            .FirstAsync(x => x.ProductId == productId);

            var totalStock = p.ProductVariants.Sum(v => (int?)v.StockQty) ?? 0;
            if (totalStock <= 0)
            {
                p.Status = "OUT_OF_STOCK";
            }
            else if (!string.IsNullOrWhiteSpace(desiredStatus) &&
                     ProductEnums.Statuses.Contains(desiredStatus.Trim().ToUpperInvariant()))
            {
                p.Status = desiredStatus!.Trim().ToUpperInvariant();
            }
            else
            {
                p.Status = "ACTIVE";
            }

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
            // Tên biến thể
            if (string.IsNullOrWhiteSpace(title))
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "TITLE_REQUIRED",
                    message = "Tên biến thể là bắt buộc."
                }));
            }

            if (title.Length > TitleMaxLength)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "TITLE_TOO_LONG",
                    message = $"Tên biến thể không được vượt quá {TitleMaxLength} ký tự."
                }));
            }

            // Mã biến thể
            if (string.IsNullOrWhiteSpace(variantCode))
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "CODE_REQUIRED",
                    message = "Mã biến thể là bắt buộc."
                }));
            }

            if (variantCode.Length > CodeMaxLength)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "CODE_TOO_LONG",
                    message = $"Mã biến thể không được vượt quá {CodeMaxLength} ký tự."
                }));
            }

            // Duration / Warranty: số nguyên >= 0, Duration > Warranty
            if (durationDays.HasValue && durationDays.Value < 0)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "DURATION_INVALID",
                    message = "Thời lượng (ngày) phải lớn hơn hoặc bằng 0."
                }));
            }

            if (warrantyDays.HasValue && warrantyDays.Value < 0)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "WARRANTY_INVALID",
                    message = "Bảo hành (ngày) phải lớn hơn hoặc bằng 0."
                }));
            }

            if (durationDays.HasValue &&
                warrantyDays.HasValue &&
                durationDays.Value <= warrantyDays.Value)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "DURATION_LE_WARRANTY",
                    message = "Thời lượng (ngày) phải lớn hơn số ngày bảo hành."
                }));
            }

            return (true, null);
        }

        private static string NormalizeString(string? s)
            => (s ?? string.Empty).Trim();

        // ===== LIST =====
        [HttpGet]
        public async Task<ActionResult<PagedResult<ProductVariantListItemDto>>> List(
            Guid productId,
            [FromQuery] ProductVariantListQuery query)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var exists = await db.Products.AnyAsync(p => p.ProductId == productId);
            if (!exists) return NotFound();

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

            var sort = (query.Sort ?? "created").Trim().ToLowerInvariant();
            var desc = string.Equals(query.Dir, "desc", StringComparison.OrdinalIgnoreCase);

            q = sort switch
            {
                "title" => desc ? q.OrderByDescending(v => v.Title) : q.OrderBy(v => v.Title),
                "duration" => desc ? q.OrderByDescending(v => v.DurationDays) : q.OrderBy(v => v.DurationDays),
                "stock" => desc ? q.OrderByDescending(v => v.StockQty) : q.OrderBy(v => v.StockQty),
                "status" => desc ? q.OrderByDescending(v => v.Status) : q.OrderBy(v => v.Status),
                "views" => desc ? q.OrderByDescending(v => v.ViewCount) : q.OrderBy(v => v.ViewCount),
                _ => desc ? q.OrderByDescending(v => v.CreatedAt) : q.OrderBy(v => v.CreatedAt),
            };

            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);
            var total = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new ProductVariantListItemDto(
                    v.VariantId,
                    v.VariantCode ?? "",
                    v.Title,
                    v.DurationDays,
                    v.StockQty,
                    v.Status,
                    v.Thumbnail,
                    v.ViewCount
                ))
                .ToListAsync();

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
        public async Task<ActionResult<ProductVariantDetailDto>> Get(Guid productId, Guid variantId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var v = await db.ProductVariants
                            .FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            // Kiểm tra xem biến thể đang được dùng trong section hay chưa
            var hasSections = await db.ProductSections
                                      .AnyAsync(s => s.VariantId == variantId);

            // Trả thêm cờ HasSections để FE disable sửa mã biến thể khi đã có section
            return Ok(new
            {
                v.VariantId,
                v.ProductId,
                VariantCode = v.VariantCode ?? "",
                v.Title,
                v.DurationDays,
                v.StockQty,
                v.WarrantyDays,
                v.Thumbnail,
                v.MetaTitle,
                v.MetaDescription,
                v.ViewCount,
                v.Status,
                HasSections = hasSections
            });
        }

        // ===== CREATE =====
        [HttpPost]
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

            // Không cho trùng Title trong cùng một sản phẩm (case-insensitive)
            var normalizedTitle = title.ToLower();
            var titleExists = await db.ProductVariants.AnyAsync(v =>
                v.ProductId == productId &&
                v.Title != null &&
                v.Title.ToLower() == normalizedTitle);

            if (titleExists)
            {
                return Conflict(new
                {
                    code = "VARIANT_TITLE_DUPLICATE",
                    message = "Tên biến thể đã tồn tại trong sản phẩm này."
                });
            }

            // Không cho trùng Mã biến thể trong cùng sản phẩm (giữa các sản phẩm khác có thể trùng)
            var normalizedCode = variantCode.ToLower();
            var codeExists = await db.ProductVariants.AnyAsync(v =>
                v.ProductId == productId &&
                v.VariantCode != null &&
                v.VariantCode.ToLower() == normalizedCode);

            if (codeExists)
            {
                return Conflict(new
                {
                    code = "VARIANT_CODE_DUPLICATE",
                    message = "Mã biến thể đã tồn tại trong sản phẩm này."
                });
            }

            var stock = dto.StockQty;
            // Nếu cần, có thể clamp stock < 0 => 0
            if (stock < 0) stock = 0;

            var status = ResolveStatusFromStock(stock, dto.Status);

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
                CreatedAt = _clock.UtcNow
            };

            db.ProductVariants.Add(v);
            await db.SaveChangesAsync();

            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

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
                    v.Status
                ));
        }

        // ===== UPDATE =====
        [HttpPut("{variantId:guid}")]
        public async Task<IActionResult> Update(Guid productId, Guid variantId, ProductVariantUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var v = await db.ProductVariants.FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            var title = NormalizeString(dto.Title);
            var variantCode = NormalizeString(dto.VariantCode ?? v.VariantCode ?? string.Empty);

            var durationDays = dto.DurationDays;
            var warrantyDays = dto.WarrantyDays;

            var (isValid, errorResult) = ValidateCommonFields(title, variantCode, durationDays, warrantyDays);
            if (!isValid) return errorResult!;

            // Check đang có section không
            var hasSections = await db.ProductSections
                                      .AnyAsync(s => s.VariantId == variantId);

            // Nếu đang có section thì không cho đổi mã biến thể (giữ v.VariantCode cũ)
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

            // Không cho trùng Title trong cùng 1 sản phẩm (trừ chính nó)
            var normalizedTitle = title.ToLower();
            var titleExists = await db.ProductVariants.AnyAsync(x =>
                x.ProductId == productId &&
                x.VariantId != variantId &&
                x.Title != null &&
                x.Title.ToLower() == normalizedTitle);

            if (titleExists)
            {
                return Conflict(new
                {
                    code = "VARIANT_TITLE_DUPLICATE",
                    message = "Tên biến thể đã tồn tại trong sản phẩm này."
                });
            }

            // Không cho trùng Mã biến thể trong cùng sản phẩm (trừ chính nó)
            var normalizedCode = variantCode.ToLower();
            var codeExists = await db.ProductVariants.AnyAsync(x =>
                x.ProductId == productId &&
                x.VariantId != variantId &&
                x.VariantCode != null &&
                x.VariantCode.ToLower() == normalizedCode);

            if (codeExists)
            {
                return Conflict(new
                {
                    code = "VARIANT_CODE_DUPLICATE",
                    message = "Mã biến thể đã tồn tại trong sản phẩm này."
                });
            }

            v.Title = title;
            v.DurationDays = durationDays;
            v.StockQty = dto.StockQty; // FE đang giữ nguyên giá trị, không cho sửa trên UI chi tiết
            v.WarrantyDays = warrantyDays;
            v.Thumbnail = string.IsNullOrWhiteSpace(dto.Thumbnail) ? null : dto.Thumbnail!.Trim();
            v.MetaTitle = string.IsNullOrWhiteSpace(dto.MetaTitle) ? null : dto.MetaTitle!.Trim();
            v.MetaDescription = string.IsNullOrWhiteSpace(dto.MetaDescription) ? null : dto.MetaDescription!.Trim();

            // Nếu không bị khoá mã biến thể bởi section thì cho cập nhật mã
            if (!hasSections)
            {
                v.VariantCode = variantCode;
            }

            var desired = string.IsNullOrWhiteSpace(dto.Status) ? null : dto.Status;
            v.Status = ResolveStatusFromStock(v.StockQty, desired);
            v.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

            return NoContent();
        }

        // ===== DELETE =====
        [HttpDelete("{variantId:guid}")]
        public async Task<IActionResult> Delete(Guid productId, Guid variantId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var v = await db.ProductVariants
                            .FirstOrDefaultAsync(x => x.ProductId == productId &&
                                                      x.VariantId == variantId);
            if (v is null) return NotFound();

            // 1) Check SECTION trước – giống logic bên Product
            var hasSections = await db.ProductSections
                                      .AnyAsync(s => s.VariantId == variantId);
            if (hasSections)
            {
                return Conflict(new
                {
                    code = "VARIANT_IN_USE_SECTION",
                    message = "Không thể xoá biến thể này vì đang được sử dụng trong các section. " +
                              "Vui lòng xoá hoặc cập nhật các section liên quan trước."
                });
            }

            // 2) Sau này muốn bắt Key / Account / Order thì thêm tương tự ở đây:
            // var hasKeys = await db.Keys.AnyAsync(k => k.VariantId == variantId);
            // if (hasKeys) return Conflict(new { code = "VARIANT_IN_USE_KEY", message = "..." });

            db.ProductVariants.Remove(v);
            await db.SaveChangesAsync();

            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

            return NoContent();
        }

        // ===== TOGGLE =====
        [HttpPatch("{variantId:guid}/toggle")]
        public async Task<IActionResult> Toggle(Guid productId, Guid variantId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var v = await db.ProductVariants
                                .FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
                if (v is null) return NotFound();

                v.Status = ToggleVisibility(v.Status, v.StockQty);
                v.UpdatedAt = _clock.UtcNow;

                await db.SaveChangesAsync();
                await RecalcProductStatus(db, productId);
                await db.SaveChangesAsync();

                return Ok(new { VariantId = v.VariantId, Status = v.Status });
            }
            catch (Exception ex)
            {
                return Problem(title: "Toggle variant status failed",
                               detail: ex.Message,
                               statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        // (Tuỳ chọn) tăng view như Posts nếu cần
        // [HttpPost("{variantId:guid}/view")] ...
    }
}
