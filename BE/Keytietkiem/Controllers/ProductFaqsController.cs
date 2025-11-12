using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products/{productId:guid}/faqs")]
    public class ProductFaqsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        public ProductFaqsController(IDbContextFactory<KeytietkiemDbContext> dbFactory, IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        // GET: /api/products/{productId}/faqs
        // Query:
        //   - keyword (optional): search Question/Answer
        //   - active  (optional): true/false
        //   - sort    (optional): one of [question|sortOrder|active|created|updated], default "sortOrder"
        //   - direction(optional): asc|desc, default "asc"
        //   - page    (optional): default 1
        //   - pageSize(optional): default 10 (1..200)
        [HttpGet]
        public async Task<IActionResult> List(
            Guid productId,
            [FromQuery] string? keyword,
            [FromQuery] bool? active,
            [FromQuery] string? sort = "sortOrder",
            [FromQuery] string? direction = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Bảo đảm product tồn tại
            var productExists = await db.Products.AsNoTracking().AnyAsync(p => p.ProductId == productId);
            if (!productExists) return NotFound(new { message = "Product not found" });

            var q = db.ProductFaqs
                      .AsNoTracking()
                      .Where(f => f.ProductId == productId);

            // Search
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLowerInvariant();
                q = q.Where(f =>
                    f.Question.ToLower().Contains(kw) ||
                    (f.Answer != null && f.Answer.ToLower().Contains(kw)));
            }

            // Filter active
            if (active is not null)
                q = q.Where(f => f.IsActive == active);

            // Sort
            sort = sort?.Trim().ToLowerInvariant();
            direction = direction?.Trim().ToLowerInvariant();

            q = (sort, direction) switch
            {
                ("question", "asc") => q.OrderBy(f => f.Question),
                ("question", "desc") => q.OrderByDescending(f => f.Question),
                ("active", "asc") => q.OrderBy(f => f.IsActive).ThenBy(f => f.SortOrder),
                ("active", "desc") => q.OrderByDescending(f => f.IsActive).ThenBy(f => f.SortOrder),
                ("created", "asc") => q.OrderBy(f => f.CreatedAt),
                ("created", "desc") => q.OrderByDescending(f => f.CreatedAt),
                ("updated", "asc") => q.OrderBy(f => f.UpdatedAt),
                ("updated", "desc") => q.OrderByDescending(f => f.UpdatedAt),
                ("sortorder", "desc") => q.OrderByDescending(f => f.SortOrder).ThenBy(f => f.CreatedAt),
                _ => q.OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt) // default
            };

            // Pagination
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var total = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new ProductFaqListItemDto(
                    f.FaqId,
                    f.ProductId,
                    f.Question,
                    f.Answer ?? string.Empty,
                    f.SortOrder,
                    f.IsActive,
                    f.CreatedAt,
                    f.UpdatedAt
                ))
                .ToListAsync();

            return Ok(new { items, total, page, pageSize });
        }

        // GET: /api/products/{productId}/faqs/{faqId}
        [HttpGet("{faqId:guid}")]
        public async Task<ActionResult<ProductFaqDetailDto>> GetById(Guid productId, Guid faqId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var dto = await db.ProductFaqs
                .AsNoTracking()
                .Where(f => f.ProductId == productId && f.FaqId == faqId)
                .Select(f => new ProductFaqDetailDto(
                    f.FaqId,
                    f.ProductId,
                    f.Question,
                    f.Answer ?? string.Empty,
                    f.SortOrder,
                    f.IsActive,
                    f.CreatedAt,
                    f.UpdatedAt
                ))
                .FirstOrDefaultAsync();

            return dto is null ? NotFound() : Ok(dto);
        }

        // POST: /api/products/{productId}/faqs
        [HttpPost]
        public async Task<ActionResult<ProductFaqDetailDto>> Create(Guid productId, ProductFaqCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var productExists = await db.Products.AsNoTracking().AnyAsync(p => p.ProductId == productId);
            if (!productExists) return NotFound(new { message = "Product not found" });

            var faq = new ProductFaq
            {
                FaqId = Guid.NewGuid(),
                ProductId = productId,
                Question = dto.Question?.Trim() ?? string.Empty,
                Answer = dto.Answer?.Trim() ?? string.Empty,
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                CreatedAt = _clock.UtcNow
            };

            db.ProductFaqs.Add(faq);
            await db.SaveChangesAsync();

            var result = new ProductFaqDetailDto(
                faq.FaqId, faq.ProductId, faq.Question, faq.Answer ?? string.Empty,
                faq.SortOrder, faq.IsActive, faq.CreatedAt, faq.UpdatedAt);

            return CreatedAtAction(nameof(GetById), new { productId, faqId = faq.FaqId }, result);
        }

        // PUT: /api/products/{productId}/faqs/{faqId}
        [HttpPut("{faqId:guid}")]
        public async Task<IActionResult> Update(Guid productId, Guid faqId, ProductFaqUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var faq = await db.ProductFaqs.FirstOrDefaultAsync(f => f.ProductId == productId && f.FaqId == faqId);
            if (faq is null) return NotFound();

            faq.Question = dto.Question?.Trim() ?? string.Empty;
            faq.Answer = dto.Answer?.Trim() ?? string.Empty;
            faq.SortOrder = dto.SortOrder;
            faq.IsActive = dto.IsActive;
            faq.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: /api/products/{productId}/faqs/{faqId}
        [HttpDelete("{faqId:guid}")]
        public async Task<IActionResult> Delete(Guid productId, Guid faqId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var faq = await db.ProductFaqs.FirstOrDefaultAsync(f => f.ProductId == productId && f.FaqId == faqId);
            if (faq is null) return NotFound();

            db.ProductFaqs.Remove(faq);
            await db.SaveChangesAsync();
            return NoContent();
        }

        // PATCH: /api/products/{productId}/faqs/{faqId}/toggle
        [HttpPatch("{faqId:guid}/toggle")]
        public async Task<IActionResult> Toggle(Guid productId, Guid faqId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var faq = await db.ProductFaqs.FirstOrDefaultAsync(f => f.ProductId == productId && f.FaqId == faqId);
            if (faq is null) return NotFound();

            faq.IsActive = !faq.IsActive;
            faq.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            return Ok(new { faq.FaqId, faq.IsActive });
        }
    }
}
