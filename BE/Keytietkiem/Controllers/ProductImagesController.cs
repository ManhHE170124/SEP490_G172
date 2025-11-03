// File: Controllers/ProductImagesController.cs
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products/{productId:guid}/images")]
    public class ProductImagesController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        public ProductImagesController(IDbContextFactory<KeytietkiemDbContext> dbFactory, IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        // LIST
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductImageDto>>> List(Guid productId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var exists = await db.Products.AnyAsync(p => p.ProductId == productId);
            if (!exists) return NotFound();

            var items = await db.ProductImages
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageDto(i.ImageId, i.Url, i.SortOrder, i.IsPrimary, i.AltText))
                .ToListAsync();
            return Ok(items);
        }

        // UPLOAD (file)
        [HttpPost("upload")]
        public async Task<ActionResult<ProductImageDto>> Upload(Guid productId, IFormFile file)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Products.FindAsync(productId);
            if (p is null) return NotFound();

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products", productId.ToString("N"));
            Directory.CreateDirectory(folder);

            var fileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Path.GetFileName(file.FileName)}";
            var fullPath = Path.Combine(folder, fileName);
            await using (var fs = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            var url = $"/uploads/products/{productId:N}/{Uri.EscapeDataString(fileName)}";
            var sort = await db.ProductImages.Where(i => i.ProductId == productId).Select(i => (int?)i.SortOrder).MaxAsync() ?? -1;

            var img = new ProductImage
            {
                ProductId = productId,
                Url = url,
                SortOrder = sort + 1,
                IsPrimary = false,
                CreatedAt = _clock.UtcNow
            };
            db.ProductImages.Add(img);
            await db.SaveChangesAsync();

            if (string.IsNullOrWhiteSpace(p.ThumbnailUrl))
            {
                p.ThumbnailUrl = url;
                await db.SaveChangesAsync();
            }

            return Ok(new ProductImageDto(img.ImageId, img.Url, img.SortOrder, img.IsPrimary, img.AltText));
        }

        // ADD BY URL
        [HttpPost("by-url")]
        public async Task<IActionResult> AddByUrl(Guid productId, ProductImageCreateByUrlDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Products.FindAsync(productId);
            if (p is null) return NotFound();

            var nextSort = await db.ProductImages.Where(i => i.ProductId == productId).Select(i => (int?)i.SortOrder).MaxAsync() ?? -1;

            var img = new ProductImage
            {
                ProductId = productId,
                Url = dto.Url.Trim(),
                AltText = dto.AltText,
                SortOrder = dto.SortOrder ?? (nextSort + 1),
                IsPrimary = dto.IsPrimary ?? false,
                CreatedAt = _clock.UtcNow
            };
            db.ProductImages.Add(img);
            await db.SaveChangesAsync();

            if (img.IsPrimary || string.IsNullOrWhiteSpace(p.ThumbnailUrl))
            {
                p.ThumbnailUrl = img.Url;
                await db.SaveChangesAsync();
            }

            return NoContent();
        }

        // SET THUMBNAIL
        [HttpPost("thumbnail")]
        public async Task<IActionResult> SetThumbnail(Guid productId, [FromBody] string url)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Products.FindAsync(productId);
            if (p is null) return NotFound();
            p.ThumbnailUrl = url?.Trim();
            p.UpdatedAt = _clock.UtcNow;
            await db.SaveChangesAsync();
            return NoContent();
        }

        // REORDER
        [HttpPost("reorder")]
        public async Task<IActionResult> Reorder(Guid productId, ProductImageReorderDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var imgs = await db.ProductImages.Where(i => i.ProductId == productId).ToListAsync();
            var pos = 0;
            foreach (var id in dto.ImageIdsInOrder)
            {
                var found = imgs.FirstOrDefault(x => x.ImageId == id);
                if (found != null) found.SortOrder = pos++;
            }
            await db.SaveChangesAsync();
            return NoContent();
        }

        // SET PRIMARY
        [HttpPost("{imageId:int}/primary")]
        public async Task<IActionResult> SetPrimary(Guid productId, int imageId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var imgs = await db.ProductImages.Where(i => i.ProductId == productId).ToListAsync();
            foreach (var i in imgs) i.IsPrimary = (i.ImageId == imageId);

            var primaryUrl = imgs.FirstOrDefault(x => x.IsPrimary)?.Url;
            var p = await db.Products.FindAsync(productId);
            if (p is not null && !string.IsNullOrWhiteSpace(primaryUrl))
                p.ThumbnailUrl = primaryUrl;

            await db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE
        [HttpDelete("{imageId:int}")]
        public async Task<IActionResult> Delete(Guid productId, int imageId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var img = await db.ProductImages.FirstOrDefaultAsync(i => i.ProductId == productId && i.ImageId == imageId);
            if (img is null) return NotFound();

            db.ProductImages.Remove(img);
            await db.SaveChangesAsync();

            var p = await db.Products.FindAsync(productId);
            if (p is not null && string.Equals(p.ThumbnailUrl, img.Url, StringComparison.OrdinalIgnoreCase))
            {
                var first = await db.ProductImages.Where(i => i.ProductId == productId)
                                                  .OrderBy(i => i.SortOrder)
                                                  .Select(i => i.Url).FirstOrDefaultAsync();
                p.ThumbnailUrl = first;
                await db.SaveChangesAsync();
            }

            return NoContent();
        }
    }
}
