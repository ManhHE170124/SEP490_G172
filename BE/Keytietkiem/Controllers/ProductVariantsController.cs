// File: Controllers/ProductVariantsController.cs
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

        private static async Task RecalcProductStatus(KeytietkiemDbContext db, Guid productId, string? desiredStatus = null)
        {
            var p = await db.Products.Include(x => x.ProductVariants).FirstAsync(x => x.ProductId == productId);
            var totalStock = p.ProductVariants.Sum(v => v.StockQty);
            if (totalStock <= 0) p.Status = "OUT_OF_STOCK";
            else if (!string.IsNullOrWhiteSpace(desiredStatus) && ProductEnums.Statuses.Contains(desiredStatus))
                p.Status = desiredStatus.ToUpperInvariant();
            else p.Status = "ACTIVE";
            p.UpdatedAt = DateTime.UtcNow;
        }

        // LIST
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductVariantListItemDto>>> List(Guid productId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var exists = await db.Products.AnyAsync(p => p.ProductId == productId);
            if (!exists) return NotFound();

            var items = await db.ProductVariants
                .Where(v => v.ProductId == productId)
                .OrderBy(v => v.SortOrder)
                .Select(v => new ProductVariantListItemDto(
                    v.VariantId, v.VariantCode ?? "", v.Title, v.DurationDays,
                    v.OriginalPrice, v.Price, v.StockQty, v.Status, v.SortOrder))
                .ToListAsync();

            return Ok(items);
        }

        // DETAIL
        [HttpGet("{variantId:guid}")]
        public async Task<ActionResult<ProductVariantDetailDto>> Get(Guid productId, Guid variantId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var v = await db.ProductVariants
                .FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            return Ok(new ProductVariantDetailDto(
                v.VariantId, v.ProductId, v.VariantCode ?? "", v.Title, v.DurationDays,
                v.OriginalPrice, v.Price, v.StockQty, v.WarrantyDays, v.Status, v.SortOrder));
        }

        // CREATE
        [HttpPost]
        public async Task<ActionResult<ProductVariantDetailDto>> Create(Guid productId, ProductVariantCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Products.FirstOrDefaultAsync(x => x.ProductId == productId);
            if (p is null) return NotFound();

            var nextSort = await db.ProductVariants.Where(x => x.ProductId == productId)
                                                   .Select(x => (int?)x.SortOrder).MaxAsync() ?? -1;

            var v = new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = productId,
                VariantCode = dto.VariantCode?.Trim(),
                Title = dto.Title.Trim(),
                DurationDays = dto.DurationDays,
                OriginalPrice = dto.OriginalPrice is null ? null : Math.Round(dto.OriginalPrice.Value, 2),
                Price = Math.Round(dto.Price, 2),
                StockQty = dto.StockQty,
                WarrantyDays = dto.WarrantyDays,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "ACTIVE" : dto.Status!.Trim().ToUpperInvariant(),
                SortOrder = dto.SortOrder ?? (nextSort + 1),
                CreatedAt = _clock.UtcNow
            };

            db.ProductVariants.Add(v);
            await db.SaveChangesAsync();

            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { productId, variantId = v.VariantId },
                new ProductVariantDetailDto(v.VariantId, v.ProductId, v.VariantCode ?? "", v.Title, v.DurationDays,
                    v.OriginalPrice, v.Price, v.StockQty, v.WarrantyDays, v.Status, v.SortOrder));
        }

        // UPDATE
        [HttpPut("{variantId:guid}")]
        public async Task<IActionResult> Update(Guid productId, Guid variantId, ProductVariantUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var v = await db.ProductVariants.FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            v.Title = dto.Title.Trim();
            v.DurationDays = dto.DurationDays;
            v.OriginalPrice = dto.OriginalPrice is null ? null : Math.Round(dto.OriginalPrice.Value, 2);
            v.Price = Math.Round(dto.Price, 2);
            v.StockQty = dto.StockQty;
            v.WarrantyDays = dto.WarrantyDays;
            if (!string.IsNullOrWhiteSpace(dto.Status)) v.Status = dto.Status!.Trim().ToUpperInvariant();
            if (dto.SortOrder.HasValue) v.SortOrder = dto.SortOrder.Value;
            v.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            await RecalcProductStatus(db, productId);
            await db.SaveChangesAsync();

            return NoContent();
        }

        // DELETE
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

        // REORDER
        [HttpPost("reorder")]
        public async Task<IActionResult> Reorder(Guid productId, VariantReorderDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var list = await db.ProductVariants.Where(x => x.ProductId == productId).ToListAsync();

            var pos = 0;
            foreach (var id in dto.VariantIdsInOrder)
            {
                var found = list.FirstOrDefault(x => x.VariantId == id);
                if (found != null) found.SortOrder = pos++;
            }

            await db.SaveChangesAsync();
            return NoContent();
        }
    }
}
