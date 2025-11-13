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
            var p = await db.Products.Include(x => x.ProductVariants)
                                     .FirstAsync(x => x.ProductId == productId);

            var totalStock = p.ProductVariants.Sum(v => (int?)v.StockQty) ?? 0;
            if (totalStock <= 0) p.Status = "OUT_OF_STOCK";
            else if (!string.IsNullOrWhiteSpace(desiredStatus) && ProductEnums.Statuses.Contains(desiredStatus.Trim().ToUpperInvariant()))
                p.Status = desiredStatus!.Trim().ToUpperInvariant();
            else p.Status = "ACTIVE";

            p.UpdatedAt = _clock.UtcNow;
        }

        private static string ToggleVisibility(string? current, int stock)
        {
            if (stock <= 0) return "OUT_OF_STOCK";
            var cur = NormalizeStatus(current);
            return cur == "ACTIVE" ? "INACTIVE" : "ACTIVE";
        }

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
                    case "<=30": q = q.Where(v => (v.DurationDays ?? 0) <= 30); break;
                    case "31-180": q = q.Where(v => (v.DurationDays ?? 0) >= 31 && (v.DurationDays ?? 0) <= 180); break;
                    case ">180": q = q.Where(v => (v.DurationDays ?? 0) > 180); break;
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

            return Ok(new ProductVariantDetailDto(
                v.VariantId, v.ProductId, v.VariantCode ?? "", v.Title, v.DurationDays,
                v.StockQty, v.WarrantyDays, v.Thumbnail, v.MetaTitle, v.MetaDescription,
                v.ViewCount, v.Status));
        }

        // ===== CREATE =====
        [HttpPost]
        public async Task<ActionResult<ProductVariantDetailDto>> Create(Guid productId, ProductVariantCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Products.FirstOrDefaultAsync(x => x.ProductId == productId);
            if (p is null) return NotFound();

            var stock = dto.StockQty;
            var status = ResolveStatusFromStock(stock, dto.Status);

            var v = new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = productId,
                VariantCode = dto.VariantCode?.Trim(),
                Title = dto.Title.Trim(),
                DurationDays = dto.DurationDays,
                StockQty = stock,
                WarrantyDays = dto.WarrantyDays,
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
                new ProductVariantDetailDto(v.VariantId, v.ProductId, v.VariantCode ?? "", v.Title, v.DurationDays,
                    v.StockQty, v.WarrantyDays, v.Thumbnail, v.MetaTitle, v.MetaDescription, v.ViewCount, v.Status));
        }

        // ===== UPDATE =====
        [HttpPut("{variantId:guid}")]
        public async Task<IActionResult> Update(Guid productId, Guid variantId, ProductVariantUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var v = await db.ProductVariants.FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            v.Title = dto.Title.Trim();
            v.DurationDays = dto.DurationDays;
            v.StockQty = dto.StockQty;
            v.WarrantyDays = dto.WarrantyDays;
            v.Thumbnail = string.IsNullOrWhiteSpace(dto.Thumbnail) ? null : dto.Thumbnail!.Trim();
            v.MetaTitle = string.IsNullOrWhiteSpace(dto.MetaTitle) ? null : dto.MetaTitle!.Trim();
            v.MetaDescription = string.IsNullOrWhiteSpace(dto.MetaDescription) ? null : dto.MetaDescription!.Trim();

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
            var v = await db.ProductVariants.FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

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
