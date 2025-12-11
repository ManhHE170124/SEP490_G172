// Controllers/ProductVariantsController.cs
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using static Keytietkiem.Constants.ModuleCodes;
using static Keytietkiem.Constants.PermissionCodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products/{productId:guid}/variants")]
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

        private static string NormalizeStatus(string? s)
        {
            var u = (s ?? "").Trim().ToUpperInvariant();
            return ProductEnums.Statuses.Contains(u) ? u : "INACTIVE";
        }

        /// <summary>
        /// Quy ước status cho Variant (tương tự Product):
        /// - ACTIVE      : còn hàng & hiển thị.
        /// - OUT_OF_STOCK: hết hàng nhưng vẫn hiển thị.
        /// - INACTIVE    : ẩn hoàn toàn, chỉ khi admin set explicit.
        /// 
        /// Hết hàng KHÔNG bao giờ tự chuyển sang INACTIVE, chỉ OUT_OF_STOCK.
        /// </summary>
        private static string ResolveStatusFromStock(int stockQty, string? desired)
        {
            var d = (desired ?? string.Empty).Trim().ToUpperInvariant();

            // Admin explicit INACTIVE => luôn INACTIVE
            if (d == "INACTIVE")
                return "INACTIVE";

            // Hết hàng => OUT_OF_STOCK (vẫn hiển thị)
            if (stockQty <= 0)
                return "OUT_OF_STOCK";

            // Còn hàng:
            if (!string.IsNullOrWhiteSpace(d) && ProductEnums.Statuses.Contains(d) && d != "OUT_OF_STOCK")
            {
                // Cho phép ACTIVE / các status hợp lệ khác (trừ OUT_OF_STOCK khi còn hàng)
                return d;
            }

            // Mặc định khi có stock mà không truyền status hợp lệ: ACTIVE
            return "ACTIVE";
        }

        /// <summary>
        /// Recalc status của Product dựa trên tổng stock các variant.
        /// Hết hàng -> OUT_OF_STOCK, còn hàng -> ACTIVE.
        /// Không bao giờ tự chuyển sang INACTIVE khi hết hàng.
        /// Nếu product đang INACTIVE và không truyền desiredStatus thì giữ nguyên,
        /// để đảm bảo "ẩn" là do admin quyết định.
        /// </summary>
        private async Task RecalcProductStatus(KeytietkiemDbContext db, Guid productId, string? desiredStatus = null)
        {
            var p = await db.Products
                            .Include(x => x.ProductVariants)
                            .FirstAsync(x => x.ProductId == productId);

            var totalStock = p.ProductVariants.Sum(v => (int?)v.StockQty) ?? 0;

            // Nếu admin đã đặt sản phẩm INACTIVE và không truyền desiredStatus
            // thì không tự động thay đổi nữa.
            if (string.Equals(p.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(desiredStatus))
            {
                p.UpdatedAt = _clock.UtcNow;
                return;
            }

            // Nếu có desiredStatus (ví dụ từ màn cập nhật Product) thì ưu tiên nó,
            // nhưng INACTIVE luôn là quyết định explicit của admin.
            if (!string.IsNullOrWhiteSpace(desiredStatus))
            {
                var d = desiredStatus.Trim().ToUpperInvariant();
                if (ProductEnums.Statuses.Contains(d))
                {
                    if (d == "INACTIVE")
                    {
                        p.Status = "INACTIVE";
                    }
                    else if (totalStock <= 0)
                    {
                        p.Status = "OUT_OF_STOCK";
                    }
                    else
                    {
                        p.Status = d;
                    }

                    p.UpdatedAt = _clock.UtcNow;
                    return;
                }
            }

            // Không có desiredStatus: tự suy từ tồn kho, chỉ ACTIVE / OUT_OF_STOCK
            p.Status = totalStock <= 0 ? "OUT_OF_STOCK" : "ACTIVE";
            p.UpdatedAt = _clock.UtcNow;
        }

        private static string ToggleVisibility(string? current, int stock)
        {
            // Hết hàng => chỉ OUT_OF_STOCK, không ẩn
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

        /// <summary>
        /// Validate 2 giá chỉnh từ màn variant:
        /// - SellPrice >= 0, ListPrice >= 0
        /// - SellPrice <= ListPrice
        /// - Nếu biết CogsPrice (giá vốn) thì ListPrice >= CogsPrice
        /// </summary>
        private (bool IsValid, ActionResult? ErrorResult) ValidatePriceFields(
            decimal sellPrice,
            decimal listPrice,
            decimal? currentCogsPrice = null)
        {
            if (sellPrice < 0)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "SELL_PRICE_INVALID",
                    message = "Giá bán phải lớn hơn hoặc bằng 0."
                }));
            }

            if (listPrice < 0)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "LIST_PRICE_INVALID",
                    message = "Giá niêm yết phải lớn hơn hoặc bằng 0."
                }));
            }

            if (sellPrice > MaxPriceValue || listPrice > MaxPriceValue)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "PRICE_TOO_LARGE",
                    message = "Giá không được vượt quá giới hạn cho phép (decimal 18,2)."
                }));
            }

            // Giá bán không được lớn hơn giá niêm yết
            if (sellPrice > listPrice)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "SELL_GT_LIST",
                    message = "Giá bán không được lớn hơn giá niêm yết."
                }));
            }

            // Nếu đã có giá vốn (CogsPrice) thì ListPrice không được nhỏ hơn giá vốn
            if (currentCogsPrice.HasValue && currentCogsPrice.Value > 0 && listPrice < currentCogsPrice.Value)
            {
                return (false, new BadRequestObjectResult(new
                {
                    code = "LIST_LT_COGS",
                    message = "Giá niêm yết không được nhỏ hơn giá vốn."
                }));
            }

            return (true, null);
        }

        private static string NormalizeString(string? s)
            => (s ?? string.Empty).Trim();

        // ===== LIST =====
        [HttpGet]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.VIEW_LIST)]
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

            if (query.MinPrice.HasValue)
            {
                q = q.Where(v => v.SellPrice >= query.MinPrice.Value);
            }

            if (query.MaxPrice.HasValue)
            {
                q = q.Where(v => v.SellPrice <= query.MaxPrice.Value);
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
                "price" => desc ? q.OrderByDescending(v => v.SellPrice) : q.OrderBy(v => v.SellPrice),
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
                    v.ViewCount,
                    v.SellPrice,
                    v.ListPrice,
                    v.CogsPrice  // show giá vốn để admin thấy nhưng không sửa qua API này
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
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.VIEW_DETAIL)]
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
                v.SellPrice,
                v.ListPrice,
                v.CogsPrice, // hiển thị giá vốn
                HasSections = hasSections
            });
        }

        // ===== CREATE =====
        [HttpPost]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.CREATE)]
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
            {
                return BadRequest(new
                {
                    code = "SELL_PRICE_REQUIRED",
                    message = "Giá bán là bắt buộc."
                });
            }

            if (!dto.ListPrice.HasValue)
            {
                return BadRequest(new
                {
                    code = "LIST_PRICE_REQUIRED",
                    message = "Giá niêm yết là bắt buộc."
                });
            }

            var sellPrice = dto.SellPrice.Value;
            var listPrice = dto.ListPrice.Value;

            // Lúc tạo mới, CogsPrice sẽ để default (0) và sau này được cập nhật từ module nhập key
            var (priceValid, priceError) = ValidatePriceFields(sellPrice, listPrice, currentCogsPrice: null);
            if (!priceValid) return priceError!;

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

            // Không cho trùng Mã biến thể trong cùng sản phẩm
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
                SellPrice = sellPrice,
                ListPrice = listPrice,
                // CogsPrice KHÔNG set ở đây => để default 0, sau này module nhập key/account sẽ cập nhật
                CreatedAt = _clock.UtcNow
            };

            db.ProductVariants.Add(v);
            await db.SaveChangesAsync();

            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

            // === AUDIT LOG: CREATE SUCCESS ===
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Create",
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
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.EDIT)]
        public async Task<IActionResult> Update(Guid productId, Guid variantId, ProductVariantUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var v = await db.ProductVariants.FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            // Snapshot before
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

            // Validate giá: dùng giá vốn hiện tại của biến thể để đảm bảo ListPrice >= CogsPrice
            var (priceValid, priceError) = ValidatePriceFields(newSellPrice, newListPrice, v.CogsPrice);
            if (!priceValid) return priceError!;

            // Check đang có section không
            var hasSections = await db.ProductSections
                                      .AnyAsync(s => s.VariantId == variantId);

            // Nếu đang có section thì không cho đổi mã biến thể
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
            v.StockQty = dto.StockQty;
            v.WarrantyDays = warrantyDays;
            v.Thumbnail = string.IsNullOrWhiteSpace(dto.Thumbnail) ? null : dto.Thumbnail!.Trim();
            v.MetaTitle = string.IsNullOrWhiteSpace(dto.MetaTitle) ? null : dto.MetaTitle!.Trim();
            v.MetaDescription = string.IsNullOrWhiteSpace(dto.MetaDescription) ? null : dto.MetaDescription!.Trim();
            v.SellPrice = newSellPrice;
            v.ListPrice = newListPrice;
            // CogsPrice giữ nguyên, không cho sửa ở API này

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

            // Snapshot after
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

            // === AUDIT LOG: UPDATE SUCCESS ===
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Update",
                entityType: "ProductVariant",
                entityId: v.VariantId.ToString(),
                before: before,
                after: after
            );

            return NoContent();
        }

        // ===== DELETE =====
        [HttpDelete("{variantId:guid}")]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.DELETE)]
        public async Task<IActionResult> Delete(Guid productId, Guid variantId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var v = await db.ProductVariants
                            .FirstOrDefaultAsync(x => x.ProductId == productId &&
                                                      x.VariantId == variantId);
            if (v is null) return NotFound();

            // Snapshot before delete
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

            db.ProductVariants.Remove(v);
            await db.SaveChangesAsync();

            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

            // === AUDIT LOG: DELETE SUCCESS ===
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Delete",
                entityType: "ProductVariant",
                entityId: v.VariantId.ToString(),
                before: before,
                after: null
            );

            return NoContent();
        }

        // ===== TOGGLE =====
        [HttpPatch("{variantId:guid}/toggle")]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.EDIT)]
        public async Task<IActionResult> Toggle(Guid productId, Guid variantId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
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

                v.Status = ToggleVisibility(v.Status, v.StockQty);
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

                // === AUDIT LOG: TOGGLE SUCCESS ===
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "Toggle",
                    entityType: "ProductVariant",
                    entityId: v.VariantId.ToString(),
                    before: before,
                    after: after
                );

                return Ok(new { VariantId = v.VariantId, Status = v.Status });
            }
            catch (Exception ex)
            {
                // Không audit log lỗi toggle để tránh spam, chỉ trả 500
                return Problem(title: "Toggle variant status failed",
                               detail: ex.Message,
                               statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
