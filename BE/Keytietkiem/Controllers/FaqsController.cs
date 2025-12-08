using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using static Keytietkiem.Constants.ModuleCodes;
using static Keytietkiem.Constants.PermissionCodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/faqs")]
    public class FaqsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        private const int QuestionMinLength = 10;
        private const int QuestionMaxLength = 500;
        private const int AnswerMinLength = 10;

        public FaqsController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        // GET: /api/faqs
        [HttpGet]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.VIEW_LIST)]
        public async Task<IActionResult> List(
            [FromQuery] string? keyword,
            [FromQuery] bool? active,
            [FromQuery] string? sort = "sortOrder",
            [FromQuery] string? direction = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.Faqs
                      .AsNoTracking()
                      .AsQueryable();

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
            {
                q = q.Where(f => f.IsActive == active);
            }

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
        f.Question,
        f.Answer ?? string.Empty,
        f.SortOrder,
        f.IsActive,
        f.Categories.Count,   // CategoryCount
        f.Products.Count,     // ProductCount
        f.CreatedAt,
        f.UpdatedAt
    ))
    .ToListAsync();


            return Ok(new { items, total, page, pageSize });
        }

        // GET: /api/faqs/{faqId}
        [HttpGet("{faqId:int}")]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.VIEW_DETAIL)]
        public async Task<ActionResult<ProductFaqDetailDto>> GetById(int faqId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var dto = await db.Faqs
      .AsNoTracking()
      .Where(f => f.FaqId == faqId)
      .Select(f => new ProductFaqDetailDto(
          f.FaqId,
          f.Question,
          f.Answer ?? string.Empty,
          f.SortOrder,
          f.IsActive,
          f.Categories
              .Select(c => c.CategoryId)
              .ToList(),
          f.Products
              .Select(p => p.ProductId)
              .ToList(),
          f.CreatedAt,
          f.UpdatedAt
      ))
      .FirstOrDefaultAsync();


            return dto is null ? NotFound() : Ok(dto);
        }

        // POST: /api/faqs
        [HttpPost]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.CREATE)]
        public async Task<ActionResult<ProductFaqDetailDto>> Create(ProductFaqCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Validate Question & Answer
            var question = (dto.Question ?? string.Empty).Trim();
            var answer = (dto.Answer ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(question))
                return BadRequest(new { message = "Question is required" });

            if (question.Length < QuestionMinLength || question.Length > QuestionMaxLength)
                return BadRequest(new
                {
                    message = $"Question length must be between {QuestionMinLength} and {QuestionMaxLength} characters."
                });

            if (string.IsNullOrWhiteSpace(answer))
                return BadRequest(new { message = "Answer is required" });

            if (answer.Length < AnswerMinLength)
                return BadRequest(new
                {
                    message = $"Answer length must be at least {AnswerMinLength} characters."
                });

            var sortOrder = dto.SortOrder < 0 ? 0 : dto.SortOrder;

            var faq = new Faq
            {
                Question = question,
                Answer = answer,
                SortOrder = sortOrder,
                IsActive = dto.IsActive,
                CreatedAt = _clock.UtcNow,
                UpdatedAt = null
            };

            // Gắn Category
            if (dto.CategoryIds is { Count: > 0 })
            {
                var catIds = dto.CategoryIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (catIds.Count > 0)
                {
                    var categories = await db.Categories
                        .Where(c => catIds.Contains(c.CategoryId))
                        .ToListAsync();

                    foreach (var c in categories)
                        faq.Categories.Add(c);
                }
            }

            // Gắn Product
            if (dto.ProductIds is { Count: > 0 })
            {
                var prodIds = dto.ProductIds
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList();

                if (prodIds.Count > 0)
                {
                    var products = await db.Products
                        .Where(p => prodIds.Contains(p.ProductId))
                        .ToListAsync();

                    foreach (var p in products)
                        faq.Products.Add(p);
                }
            }

            db.Faqs.Add(faq);
            await db.SaveChangesAsync();

            var result = new ProductFaqDetailDto(
                faq.FaqId,
                faq.Question,
                faq.Answer ?? string.Empty,
                faq.SortOrder,
                faq.IsActive,
                CategoryIds: faq.Categories.Select(c => c.CategoryId).ToList(),
                ProductIds: faq.Products.Select(p => p.ProductId).ToList(),
                faq.CreatedAt,
                faq.UpdatedAt
            );

            return CreatedAtAction(nameof(GetById), new { faqId = result.FaqId }, result);
        }

        // PUT: /api/faqs/{faqId}
        [HttpPut("{faqId:int}")]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.EDIT)]
        public async Task<IActionResult> Update(int faqId, ProductFaqUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var faq = await db.Faqs
                .Include(f => f.Categories)
                .Include(f => f.Products)
                .FirstOrDefaultAsync(f => f.FaqId == faqId);

            if (faq is null) return NotFound();

            // Validate Question & Answer
            var question = (dto.Question ?? string.Empty).Trim();
            var answer = (dto.Answer ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(question))
                return BadRequest(new { message = "Question is required" });

            if (question.Length < QuestionMinLength || question.Length > QuestionMaxLength)
                return BadRequest(new
                {
                    message = $"Question length must be between {QuestionMinLength} and {QuestionMaxLength} characters."
                });

            if (string.IsNullOrWhiteSpace(answer))
                return BadRequest(new { message = "Answer is required" });

            if (answer.Length < AnswerMinLength)
                return BadRequest(new
                {
                    message = $"Answer length must be at least {AnswerMinLength} characters."
                });

            if (dto.SortOrder < 0)
                return BadRequest(new { message = "SortOrder must be greater than or equal to 0." });

            // Update main fields
            faq.Question = question;
            faq.Answer = answer;
            faq.SortOrder = dto.SortOrder;
            faq.IsActive = dto.IsActive;
            faq.UpdatedAt = _clock.UtcNow;

            // Replace categories
            faq.Categories.Clear();
            if (dto.CategoryIds is { Count: > 0 })
            {
                var catIds = dto.CategoryIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (catIds.Count > 0)
                {
                    var categories = await db.Categories
                        .Where(c => catIds.Contains(c.CategoryId))
                        .ToListAsync();

                    foreach (var c in categories)
                        faq.Categories.Add(c);
                }
            }

            // Replace products
            faq.Products.Clear();
            if (dto.ProductIds is { Count: > 0 })
            {
                var prodIds = dto.ProductIds
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList();

                if (prodIds.Count > 0)
                {
                    var products = await db.Products
                        .Where(p => prodIds.Contains(p.ProductId))
                        .ToListAsync();

                    foreach (var p in products)
                        faq.Products.Add(p);
                }
            }

            await db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: /api/faqs/{faqId}
        [HttpDelete("{faqId:int}")]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.DELETE)]
        public async Task<IActionResult> Delete(int faqId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var faq = await db.Faqs.FirstOrDefaultAsync(f => f.FaqId == faqId);
            if (faq is null) return NotFound();

            db.Faqs.Remove(faq);
            await db.SaveChangesAsync();
            return NoContent();
        }

        // PATCH: /api/faqs/{faqId}/toggle
        [HttpPatch("{faqId:int}/toggle")]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.EDIT)]
        public async Task<IActionResult> Toggle(int faqId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var faq = await db.Faqs.FirstOrDefaultAsync(f => f.FaqId == faqId);
            if (faq is null) return NotFound();

            faq.IsActive = !faq.IsActive;
            faq.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            return Ok(new { faq.FaqId, faq.IsActive });
        }
    }
}
