using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products/{productId:guid}/variants/{variantId:guid}/sections")]
    public class ProductSectionsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        public ProductSectionsController(IDbContextFactory<KeytietkiemDbContext> dbFactory, IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        private static string NormType(string? t) => ProductSectionEnums.Normalize(t);

        // ===== LIST: search + filter + sort + paging =====
        [HttpGet]
        public async Task<ActionResult<PagedResult<ProductSectionListItemDto>>> List(
            Guid productId,
            Guid variantId,
            [FromQuery] ProductSectionListQuery query)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Bảo đảm product/variant tồn tại và đúng quan hệ
            var variantExists = await db.ProductVariants
                .AnyAsync(v => v.ProductId == productId && v.VariantId == variantId);
            if (!variantExists) return NotFound();

            var q = db.ProductSections.AsNoTracking()
                                      .Where(s => s.VariantId == variantId);

            // Search
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var s = query.Q.Trim();
                q = q.Where(x =>
                    EF.Functions.Like(x.Title, $"%{s}%") ||
                    EF.Functions.Like(x.Content ?? "", $"%{s}%"));
            }

            // Filter type
            if (!string.IsNullOrWhiteSpace(query.Type))
            {
                var type = NormType(query.Type);
                q = q.Where(x => x.SectionType.ToUpper() == type);
            }

            // Filter active
            if (query.Active.HasValue)
            {
                q = q.Where(x => x.IsActive == query.Active.Value);
            }

            // Sort
            var sort = (query.Sort ?? "sort").Trim().ToLowerInvariant();
            var desc = string.Equals(query.Dir, "desc", StringComparison.OrdinalIgnoreCase);

            q = sort switch
            {
                "title" => desc ? q.OrderByDescending(x => x.Title) : q.OrderBy(x => x.Title),
                "type" => desc ? q.OrderByDescending(x => x.SectionType) : q.OrderBy(x => x.SectionType),
                "active" => desc ? q.OrderByDescending(x => x.IsActive) : q.OrderBy(x => x.IsActive),
                "created" => desc ? q.OrderByDescending(x => x.CreatedAt) : q.OrderBy(x => x.CreatedAt),
                "updated" => desc ? q.OrderByDescending(x => x.UpdatedAt) : q.OrderBy(x => x.UpdatedAt),
                _ => desc ? q.OrderByDescending(x => x.SortOrder) : q.OrderBy(x => x.SortOrder),
            };

            // Paging
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);

            var total = await q.CountAsync();

            var items = await q.Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .Select(s => new ProductSectionListItemDto(
                                   s.SectionId, s.VariantId, s.SectionType, s.Title, s.Content ?? "",
                                   s.SortOrder, s.IsActive, s.CreatedAt, s.UpdatedAt))
                               .ToListAsync();

            return Ok(new PagedResult<ProductSectionListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                Items = items
            });
        }

        // ===== GET DETAIL =====
        [HttpGet("{sectionId:guid}")]
        public async Task<ActionResult<ProductSectionDetailDto>> Get(Guid productId, Guid variantId, Guid sectionId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var s = await db.ProductSections
                .FirstOrDefaultAsync(x => x.SectionId == sectionId && x.VariantId == variantId);
            if (s is null) return NotFound();

            return Ok(new ProductSectionDetailDto(
                s.SectionId, s.VariantId, s.SectionType, s.Title, s.Content ?? "",
                s.SortOrder, s.IsActive, s.CreatedAt, s.UpdatedAt));
        }

        // ===== CREATE =====
        [HttpPost]
        public async Task<ActionResult<ProductSectionDetailDto>> Create(Guid productId, Guid variantId, ProductSectionCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var v = await db.ProductVariants.FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            var type = NormType(dto.SectionType);
            if (!ProductSectionEnums.IsValid(type))
                return BadRequest(new { message = "Invalid SectionType. Allowed: WARRANTY | NOTE | DETAIL" });

            var nextSort = await db.ProductSections.Where(x => x.VariantId == variantId)
                                                   .Select(x => (int?)x.SortOrder).MaxAsync() ?? -1;

            var s = new ProductSection
            {
                SectionId = Guid.NewGuid(),
                VariantId = variantId,
                SectionType = type,
                Title = dto.Title.Trim(),
                Content = dto.Content?.Trim(),
                SortOrder = dto.SortOrder ?? (nextSort + 1),
                IsActive = dto.IsActive,
                CreatedAt = _clock.UtcNow
            };

            db.ProductSections.Add(s);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get),
                new { productId, variantId, sectionId = s.SectionId },
                new ProductSectionDetailDto(
                    s.SectionId, s.VariantId, s.SectionType, s.Title, s.Content ?? "",
                    s.SortOrder, s.IsActive, s.CreatedAt, s.UpdatedAt));
        }

        // ===== UPDATE =====
        [HttpPut("{sectionId:guid}")]
        public async Task<IActionResult> Update(Guid productId, Guid variantId, Guid sectionId, ProductSectionUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var s = await db.ProductSections
                .FirstOrDefaultAsync(x => x.SectionId == sectionId && x.VariantId == variantId);
            if (s is null) return NotFound();

            var type = NormType(dto.SectionType);
            if (!ProductSectionEnums.IsValid(type))
                return BadRequest(new { message = "Invalid SectionType. Allowed: WARRANTY | NOTE | DETAIL" });

            s.SectionType = type;
            s.Title = dto.Title.Trim();
            s.Content = dto.Content?.Trim();
            s.SortOrder = dto.SortOrder;
            s.IsActive = dto.IsActive;
            s.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            return NoContent();
        }

        // ===== DELETE =====
        [HttpDelete("{sectionId:guid}")]
        public async Task<IActionResult> Delete(Guid productId, Guid variantId, Guid sectionId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var s = await db.ProductSections
                .FirstOrDefaultAsync(x => x.SectionId == sectionId && x.VariantId == variantId);
            if (s is null) return NotFound();

            db.ProductSections.Remove(s);
            await db.SaveChangesAsync();
            return NoContent();
        }

        // ===== TOGGLE ACTIVE =====
        [HttpPatch("{sectionId:guid}/toggle")]
        public async Task<IActionResult> Toggle(Guid productId, Guid variantId, Guid sectionId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var s = await db.ProductSections
                .FirstOrDefaultAsync(x => x.SectionId == sectionId && x.VariantId == variantId);
            if (s is null) return NotFound();

            s.IsActive = !s.IsActive;
            s.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            return Ok(new { s.SectionId, s.IsActive });
        }

        // ===== REORDER =====
        [HttpPost("reorder")]
        public async Task<IActionResult> Reorder(Guid productId, Guid variantId, SectionReorderDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var list = await db.ProductSections.Where(x => x.VariantId == variantId).ToListAsync();
            var pos = 0;
            foreach (var id in dto.SectionIdsInOrder)
            {
                var found = list.FirstOrDefault(x => x.SectionId == id);
                if (found != null) found.SortOrder = pos++;
            }
            await db.SaveChangesAsync();
            return NoContent();
        }
    }
}
