// File: Controllers/ProductFaqsController.cs
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductFaqDto>>> List(Guid productId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var exists = await db.Products.AnyAsync(p => p.ProductId == productId);
            if (!exists) return NotFound();

            var items = await db.ProductFaqs.Where(f => f.ProductId == productId)
                .OrderBy(f => f.SortOrder)
                .Select(f => new ProductFaqDto(f.FaqId, f.Question, f.Answer, f.SortOrder, f.IsActive))
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        public async Task<ActionResult<ProductFaqDto>> Create(Guid productId, ProductFaqCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var exists = await db.Products.AnyAsync(p => p.ProductId == productId);
            if (!exists) return NotFound();

            var faq = new ProductFaq
            {
                FaqId = Guid.NewGuid(),
                ProductId = productId,
                Question = dto.Question.Trim(),
                Answer = dto.Answer.Trim(),
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                CreatedAt = _clock.UtcNow
            };

            db.ProductFaqs.Add(faq);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(List), new { productId }, new ProductFaqDto(faq.FaqId, faq.Question, faq.Answer, faq.SortOrder, faq.IsActive));
        }

        [HttpPut("{faqId:guid}")]
        public async Task<IActionResult> Update(Guid productId, Guid faqId, ProductFaqUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var faq = await db.ProductFaqs.FirstOrDefaultAsync(f => f.ProductId == productId && f.FaqId == faqId);
            if (faq is null) return NotFound();

            faq.Question = dto.Question.Trim();
            faq.Answer = dto.Answer.Trim();
            faq.SortOrder = dto.SortOrder;
            faq.IsActive = dto.IsActive;
            faq.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            return NoContent();
        }

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
    }
}
