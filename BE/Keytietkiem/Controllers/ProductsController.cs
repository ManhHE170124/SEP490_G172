using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        public ProductsController(IDbContextFactory<KeytietkiemDbContext> dbFactory, IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        private static string ResolveStatus(int stockQty, string? desired)
        {
            if (stockQty <= 0) return "OUT_OF_STOCK"; // hết hàng thì luôn OUT_OF_STOCK
            if (!string.IsNullOrWhiteSpace(desired) && ProductEnums.Statuses.Contains(desired))
                return desired.ToUpperInvariant();
            return "ACTIVE"; // còn hàng mặc định ACTIVE
        }

        private static string ToggleVisibility(string current, int stockQty)
        {
            if (stockQty <= 0) return "OUT_OF_STOCK";
            return string.Equals(current, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                ? "INACTIVE" : "ACTIVE";
        }

        // ===== LIST =====
        [HttpGet("list")]
        public async Task<ActionResult<PagedResult<ProductListItemDto>>> List(
            [FromQuery] string? keyword,
            [FromQuery] int? categoryId,
            [FromQuery(Name = "type")] string? productType,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.Products.AsNoTracking()
                .Include(p => p.Categories)
                .Include(p => p.ProductBadges)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                q = q.Where(p => p.ProductName.Contains(keyword) || p.ProductCode.Contains(keyword));
            if (!string.IsNullOrWhiteSpace(productType))
                q = q.Where(p => p.ProductType == productType);
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(p => p.Status == status);
            if (categoryId is not null)
                q = q.Where(p => p.Categories.Any(c => c.CategoryId == categoryId));

            var total = await q.CountAsync();

            var items = await q.OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new ProductListItemDto(
                    p.ProductId,
                    p.ProductCode,
                    p.ProductName,
                    p.ProductType,
                    p.SalePrice,
                    p.StockQty,
                    p.WarrantyDays,
                    p.Status,
                    p.ThumbnailUrl,
                    p.Categories.Select(c => c.CategoryId),
                    p.ProductBadges.Select(b => b.Badge)
                ))
                .ToListAsync();

            return Ok(new PagedResult<ProductListItemDto>(items, total, page, pageSize));
        }

        // ===== DETAIL =====
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProductDetailDto>> GetById(Guid id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var p = await db.Products.AsNoTracking()
                .Include(x => x.Categories)
                .Include(x => x.ProductBadges)
                .Include(x => x.ProductImages)
                .FirstOrDefaultAsync(x => x.ProductId == id);

            if (p is null) return NotFound();

            return Ok(new ProductDetailDto(
                p.ProductId, p.ProductCode, p.ProductName, p.SupplierId, p.ProductType,
                p.CostPrice, p.SalePrice, p.StockQty, p.WarrantyDays, p.ExpiryDate,
                p.AutoDelivery, p.Status, p.Description, p.ThumbnailUrl,
                p.Categories.Select(c => c.CategoryId),
                p.ProductBadges.Select(b => b.Badge),
                p.ProductImages
                  .OrderBy(i => i.SortOrder)
                  .Select(i => new ProductImageDto(i.ImageId, i.Url, i.SortOrder, i.IsPrimary))
            ));
        }

        // ===== CREATE (JSON) =====
        [HttpPost]
        public async Task<ActionResult<ProductDetailDto>> Create(ProductCreateDto dto)
        {
            if (!ProductEnums.Types.Contains(dto.ProductType)) return BadRequest(new { message = "Invalid ProductType" });
            if (dto.SalePrice <= 0) return BadRequest(new { message = "SalePrice must be > 0" });
            if (dto.StockQty < 0) return BadRequest(new { message = "StockQty must be >= 0" });
            if (dto.WarrantyDays < 0) return BadRequest(new { message = "WarrantyDays must be >= 0" });

            await using var db = await _dbFactory.CreateDbContextAsync();

            if (await db.Products.AnyAsync(x => x.ProductCode == dto.ProductCode))
                return Conflict(new { message = "ProductCode already exists" });

            if (!await db.Suppliers.AnyAsync(s => s.SupplierId == dto.SupplierId))
                return BadRequest(new { message = "Supplier not found" });

            var e = new Product
            {
                ProductId = Guid.NewGuid(),
                ProductCode = dto.ProductCode.Trim(),
                ProductName = dto.ProductName.Trim(),
                SupplierId = dto.SupplierId,
                ProductType = dto.ProductType,
                CostPrice = dto.CostPrice,
                SalePrice = dto.SalePrice,
                StockQty = dto.StockQty,
                WarrantyDays = dto.WarrantyDays,
                ExpiryDate = dto.ExpiryDate,
                AutoDelivery = dto.AutoDelivery,
                Status = ResolveStatus(dto.StockQty, dto.Status),
                Description = dto.Description,
                ThumbnailUrl = dto.ThumbnailUrl,
                CreatedAt = _clock.UtcNow
            };

            // categories
            if (dto.CategoryIds is not null && dto.CategoryIds.Any())
            {
                var cats = await db.Categories.Where(c => dto.CategoryIds.Contains(c.CategoryId)).ToListAsync();
                foreach (var c in cats) e.Categories.Add(c);
            }

            // badges (chỉ nhận badge active tồn tại)
            if (dto.BadgeCodes is not null && dto.BadgeCodes.Any())
            {
                var codes = dto.BadgeCodes.Select(x => x.Trim()).Where(x => x != "").ToHashSet(StringComparer.OrdinalIgnoreCase);
                var valid = await db.Badges.Where(b => b.IsActive && codes.Contains(b.BadgeCode))
                                           .Select(b => b.BadgeCode).ToListAsync();
                foreach (var code in valid)
                    e.ProductBadges.Add(new ProductBadge { ProductId = e.ProductId, Badge = code, CreatedAt = _clock.UtcNow });
            }

            db.Products.Add(e);
            await db.SaveChangesAsync();

            return await GetById(e.ProductId);
        }

        // ===== CREATE (multipart + upload nhiều ảnh & chọn ảnh đại diện) =====
        public class ProductCreateWithImagesForm
        {
            // các field từ form-data (FromForm)
            public string ProductCode { get; set; } = null!;
            public string ProductName { get; set; } = null!;
            public int SupplierId { get; set; }
            public string ProductType { get; set; } = null!;
            public decimal? CostPrice { get; set; }
            public decimal SalePrice { get; set; }
            public int StockQty { get; set; }
            public int WarrantyDays { get; set; }
            public DateOnly? ExpiryDate { get; set; }
            public bool AutoDelivery { get; set; }
            public string? Status { get; set; }
            public string? Description { get; set; }
            public List<int>? CategoryIds { get; set; }
            public List<string>? BadgeCodes { get; set; }

            // files
            public List<IFormFile>? Images { get; set; }
            public int? PrimaryIndex { get; set; } // chỉ số ảnh làm đại diện (0-based)
        }

       [HttpPost("with-images")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> CreateWithImages([FromForm] ProductCreateWithImagesForm form)
{
    // Gọi create JSON
    var result = await Create(new ProductCreateDto(
        form.ProductCode, form.ProductName, form.SupplierId, form.ProductType,
        form.CostPrice, form.SalePrice, form.StockQty, form.WarrantyDays,
        form.ExpiryDate, form.AutoDelivery, form.Status, form.Description,
        null, form.CategoryIds ?? new List<int>(), form.BadgeCodes ?? new List<string>()
    ));

    // Nếu Create trả lỗi, “unwrap” status & payload rồi trả về đúng IActionResult
    if (result.Result is ObjectResult orr && orr.StatusCode is >= 400)
        return StatusCode(orr.StatusCode!.Value, orr.Value);

    var detail = (result.Value ?? (result.Result as ObjectResult)?.Value) as ProductDetailDto;
    if (detail is null) return Problem("Create failed");

    // Upload ảnh, set thumbnail như cũ...
    if (form.Images is { Count: > 0 })
    {
        var urls = new List<string>();
        foreach (var file in form.Images)
        {
            var imgDto = await UploadImageInternal(detail.ProductId, file);
            urls.Add(imgDto.Url);
        }
        var idx = Math.Clamp(form.PrimaryIndex ?? 0, 0, urls.Count - 1);
        await SetThumbnail(detail.ProductId, urls[idx]);
    }

    // Trả về chi tiết cuối cùng
    var finalDetail = await GetById(detail.ProductId);
    return Ok(finalDetail.Value ?? (finalDetail.Result as ObjectResult)?.Value);
}

        // ===== UPDATE (JSON) =====
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, ProductUpdateDto dto)
        {
            if (!ProductEnums.Types.Contains(dto.ProductType)) return BadRequest(new { message = "Invalid ProductType" });
            if (dto.SalePrice <= 0) return BadRequest(new { message = "SalePrice must be > 0" });
            if (dto.StockQty < 0) return BadRequest(new { message = "StockQty must be >= 0" });
            if (dto.WarrantyDays < 0) return BadRequest(new { message = "WarrantyDays must be >= 0" });

            await using var db = await _dbFactory.CreateDbContextAsync();
            var e = await db.Products
                .Include(p => p.Categories)
                .Include(p => p.ProductBadges)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (e is null) return NotFound();
            if (!await db.Suppliers.AnyAsync(s => s.SupplierId == dto.SupplierId))
                return BadRequest(new { message = "Supplier not found" });

            e.ProductName = dto.ProductName.Trim();
            e.SupplierId = dto.SupplierId;
            e.ProductType = dto.ProductType;
            e.CostPrice = dto.CostPrice;
            e.SalePrice = dto.SalePrice;
            e.StockQty = dto.StockQty;
            e.WarrantyDays = dto.WarrantyDays;
            e.ExpiryDate = dto.ExpiryDate;
            e.AutoDelivery = dto.AutoDelivery;
            e.Status = ResolveStatus(dto.StockQty, dto.Status);
            e.Description = dto.Description;
            e.ThumbnailUrl = dto.ThumbnailUrl;
            e.UpdatedAt = _clock.UtcNow;

            // sync categories
            e.Categories.Clear();
            if (dto.CategoryIds is not null && dto.CategoryIds.Any())
            {
                var cats = await db.Categories.Where(c => dto.CategoryIds.Contains(c.CategoryId)).ToListAsync();
                foreach (var c in cats) e.Categories.Add(c);
            }

            // sync badges
            e.ProductBadges.Clear();
            if (dto.BadgeCodes is not null && dto.BadgeCodes.Any())
            {
                var codes = dto.BadgeCodes.Select(x => x.Trim()).Where(x => x != "").ToHashSet(StringComparer.OrdinalIgnoreCase);
                var valid = await db.Badges.Where(b => b.IsActive && codes.Contains(b.BadgeCode))
                                           .Select(b => b.BadgeCode).ToListAsync();
                foreach (var code in valid)
                    e.ProductBadges.Add(new ProductBadge { ProductId = e.ProductId, Badge = code, CreatedAt = _clock.UtcNow });
            }

            await db.SaveChangesAsync();
            return NoContent();
        }

        // ===== UPDATE with images (multipart) =====
        public class ProductUpdateWithImagesForm
        {
            public string ProductName { get; set; } = null!;
            public int SupplierId { get; set; }
            public string ProductType { get; set; } = null!;
            public decimal? CostPrice { get; set; }
            public decimal SalePrice { get; set; }
            public int StockQty { get; set; }
            public int WarrantyDays { get; set; }
            public DateOnly? ExpiryDate { get; set; }
            public bool AutoDelivery { get; set; }
            public string? Status { get; set; }
            public string? Description { get; set; }
            public string? ThumbnailUrl { get; set; }
            public List<int>? CategoryIds { get; set; }
            public List<string>? BadgeCodes { get; set; }

            // ảnh thêm mới
            public List<IFormFile>? NewImages { get; set; }
            public int? PrimaryIndex { get; set; } // nếu set thì dùng ảnh (mới + cũ) theo thứ tự sau cập nhật để chọn
            // ảnh xóa
            public List<int>? DeleteImageIds { get; set; }
        }

        [HttpPut("{id:guid}/with-images")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateWithImages(Guid id, [FromForm] ProductUpdateWithImagesForm form)
        {
            // 1) update core fields
            var dto = new ProductUpdateDto(
                form.ProductName, form.SupplierId, form.ProductType, form.CostPrice, form.SalePrice,
                form.StockQty, form.WarrantyDays, form.ExpiryDate, form.AutoDelivery,
                form.Status, form.Description, form.ThumbnailUrl,
                form.CategoryIds ?? new List<int>(), form.BadgeCodes ?? new List<string>()
            );
            var res = await Update(id, dto);
            if (res is ObjectResult r && r.StatusCode is >= 400) return res;

            await using var db = await _dbFactory.CreateDbContextAsync();

            // 2) delete images if requested
            if (form.DeleteImageIds is not null && form.DeleteImageIds.Any())
            {
                var dels = await db.ProductImages.Where(i => i.ProductId == id && form.DeleteImageIds.Contains(i.ImageId)).ToListAsync();
                if (dels.Count > 0)
                {
                    db.ProductImages.RemoveRange(dels);
                    await db.SaveChangesAsync();

                    // nếu xóa mất thumbnail, gán lại ảnh đầu tiên (nếu có)
                    var p = await db.Products.FindAsync(id);
                    if (p is not null && !string.IsNullOrWhiteSpace(p.ThumbnailUrl))
                    {
                        if (dels.Any(d => string.Equals(d.Url, p.ThumbnailUrl, StringComparison.OrdinalIgnoreCase)))
                        {
                            var first = await db.ProductImages
                                .Where(i => i.ProductId == id)
                                .OrderBy(i => i.SortOrder)
                                .Select(i => i.Url)
                                .FirstOrDefaultAsync();
                            p.ThumbnailUrl = first;
                            await db.SaveChangesAsync();
                        }
                    }
                }
            }

            // 3) add new images
            var appendedUrls = new List<string>();
            if (form.NewImages is not null && form.NewImages.Count > 0)
            {
                foreach (var file in form.NewImages)
                {
                    var img = await UploadImageInternal(id, file);
                    appendedUrls.Add(img.Url);
                }
            }

            // 4) primary selection (optional)
            if (form.PrimaryIndex is not null)
            {
                // danh sách ảnh sau khi thêm mới
                var all = await db.ProductImages.Where(i => i.ProductId == id).OrderBy(i => i.SortOrder).ToListAsync();
                var primaryIdx = Math.Clamp(form.PrimaryIndex.Value, 0, Math.Max(all.Count - 1, 0));
                var selected = all.ElementAtOrDefault(primaryIdx);
                if (selected is not null)
                {
                    foreach (var i in all) i.IsPrimary = (i.ImageId == selected.ImageId);
                    var p = await db.Products.FindAsync(id);
                    if (p is not null) p.ThumbnailUrl = selected.Url;
                    await db.SaveChangesAsync();
                }
            }

            return NoContent();
        }

        // ===== DELETE =====
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var e = await db.Products.FindAsync(id);
            if (e is null) return NotFound();
            db.Products.Remove(e);
            await db.SaveChangesAsync();
            return NoContent();
        }

        // ===== TOGGLE VISIBILITY (ẩn/hiện) =====
        [HttpPatch("{id:guid}/toggle")]
        public async Task<IActionResult> Toggle(Guid id)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var e = await db.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (e is null) return NotFound();

            e.Status = ToggleVisibility(e.Status, e.StockQty);
            e.UpdatedAt = _clock.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { e.ProductId, e.Status });
        }

        // ===== PATCH status trực tiếp (giữ cho UI cũ) =====
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] string status)
        {
            if (!ProductEnums.Statuses.Contains(status)) return BadRequest(new { message = "Invalid Status" });
            await using var db = await _dbFactory.CreateDbContextAsync();
            var e = await db.Products.FindAsync(id);
            if (e is null) return NotFound();
            e.Status = ResolveStatus(e.StockQty, status);
            e.UpdatedAt = _clock.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { e.ProductId, e.Status });
        }

        // ===== ẢNH SẢN PHẨM =====

        // dùng nội bộ cho create-with-images / update-with-images
        private async Task<ProductImageDto> UploadImageInternal(Guid id, IFormFile file)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Products.FindAsync(id) ?? throw new InvalidOperationException("Product not found");

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products", id.ToString("N"));
            Directory.CreateDirectory(folder);

            var fileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Path.GetFileName(file.FileName)}";
            var fullPath = Path.Combine(folder, fileName);
            await using (var fs = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            var url = $"/uploads/products/{id:N}/{Uri.EscapeDataString(fileName)}";
            var sort = await db.ProductImages.Where(i => i.ProductId == id).Select(i => (int?)i.SortOrder).MaxAsync() ?? -1;

            var img = new ProductImage { ProductId = id, Url = url, SortOrder = sort + 1, IsPrimary = false, CreatedAt = _clock.UtcNow };
            db.ProductImages.Add(img);
            await db.SaveChangesAsync();

            // nếu chưa có thumbnail thì set luôn
            if (string.IsNullOrWhiteSpace(p.ThumbnailUrl))
            {
                p.ThumbnailUrl = url;
                await db.SaveChangesAsync();
            }

            return new ProductImageDto(img.ImageId, img.Url, img.SortOrder, img.IsPrimary);
        }

        // Upload 1 ảnh (API lẻ)
        [HttpPost("{id:guid}/images/upload")]
        public async Task<ActionResult<ProductImageDto>> UploadImage(Guid id, IFormFile file)
            => Ok(await UploadImageInternal(id, file));

        // Đặt thumbnail từ URL (hoặc từ ảnh đã có)
        [HttpPost("{id:guid}/thumbnail")]
        public async Task<IActionResult> SetThumbnail(Guid id, [FromBody] string urlOrExistingImageUrl)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Products.FindAsync(id);
            if (p is null) return NotFound();

            p.ThumbnailUrl = urlOrExistingImageUrl?.Trim();
            p.UpdatedAt = _clock.UtcNow;
            await db.SaveChangesAsync();
            return NoContent();
        }

        // Xoá 1 ảnh
        [HttpDelete("{id:guid}/images/{imageId:int}")]
        public async Task<IActionResult> DeleteImage(Guid id, int imageId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var img = await db.ProductImages.FirstOrDefaultAsync(i => i.ImageId == imageId && i.ProductId == id);
            if (img is null) return NotFound();

            db.ProductImages.Remove(img);
            await db.SaveChangesAsync();

            var p = await db.Products.FindAsync(id);
            if (p is not null && string.Equals(p.ThumbnailUrl, img.Url, StringComparison.OrdinalIgnoreCase))
            {
                var first = await db.ProductImages.Where(i => i.ProductId == id)
                                                  .OrderBy(i => i.SortOrder)
                                                  .Select(i => i.Url).FirstOrDefaultAsync();
                p.ThumbnailUrl = first;
                await db.SaveChangesAsync();
            }

            return NoContent();
        }

        public record ReorderImagesDto(IReadOnlyList<int> ImageIds);

        // Sắp xếp lại thứ tự ảnh
        [HttpPost("{id:guid}/images/reorder")]
        public async Task<IActionResult> ReorderImages(Guid id, ReorderImagesDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var imgs = await db.ProductImages.Where(i => i.ProductId == id).ToListAsync();
            var pos = 0;
            foreach (var imgId in dto.ImageIds)
            {
                var found = imgs.FirstOrDefault(x => x.ImageId == imgId);
                if (found != null) found.SortOrder = pos++;
            }
            await db.SaveChangesAsync();
            return NoContent();
        }

        // Đánh dấu ảnh primary và cập nhật thumbnail
        [HttpPost("{id:guid}/images/{imageId:int}/primary")]
        public async Task<IActionResult> SetPrimary(Guid id, int imageId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var imgs = await db.ProductImages.Where(i => i.ProductId == id).ToListAsync();
            foreach (var i in imgs) i.IsPrimary = (i.ImageId == imageId);

            var primaryUrl = imgs.FirstOrDefault(x => x.IsPrimary)?.Url;
            var p = await db.Products.FindAsync(id);
            if (p is not null && !string.IsNullOrWhiteSpace(primaryUrl))
                p.ThumbnailUrl = primaryUrl;

            await db.SaveChangesAsync();
            return NoContent();
        }

        // ===== CSV EXPORT / IMPORT (cập nhật giá) =====
        [HttpGet("export-csv")]
        public async Task<IActionResult> ExportCsv()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var rows = await db.Products
                .Select(p => new { sku = p.ProductCode, new_price = p.SalePrice })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("sku,new_price");
            foreach (var r in rows)
                sb.AppendLine($"{r.sku},{r.new_price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "products_price.csv");
        }

        [HttpPost("import-price-csv")]
        public async Task<ActionResult<PriceImportResult>> ImportPriceCsv(IFormFile file)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            await using var db = await _dbFactory.CreateDbContextAsync();

            int total = 0, updated = 0, notFound = 0, invalid = 0;

            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
            string? line = await reader.ReadLineAsync(); // header
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                total++;
                var parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2) { invalid++; continue; }

                var sku = parts[0];
                if (!decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var newPrice) || newPrice <= 0)
                {
                    invalid++; continue;
                }

                var prod = await db.Products.FirstOrDefaultAsync(p => p.ProductCode == sku);
                if (prod is null) { notFound++; continue; }

                prod.SalePrice = Math.Round(newPrice, 2);
                prod.UpdatedAt = _clock.UtcNow;
                updated++;
            }

            await db.SaveChangesAsync();
            return Ok(new PriceImportResult(total, updated, notFound, invalid));
        }

        // ===== Bulk tăng/giảm % =====
        [HttpPost("bulk-price")]
        public async Task<ActionResult<object>> BulkPrice(BulkPriceUpdateDto dto)
        {
            if (dto.Percent == 0) return BadRequest(new { message = "Percent must be non-zero" });

            await using var db = await _dbFactory.CreateDbContextAsync();
            var q = db.Products.Include(p => p.Categories).AsQueryable();

            if (!string.IsNullOrWhiteSpace(dto.ProductType))
            {
                if (!ProductEnums.Types.Contains(dto.ProductType))
                    return BadRequest(new { message = "Invalid ProductType" });
                q = q.Where(p => p.ProductType == dto.ProductType);
            }

            if (dto.CategoryIds is not null && dto.CategoryIds.Any())
                q = q.Where(p => p.Categories.Any(c => dto.CategoryIds.Contains(c.CategoryId)));

            var list = await q.ToListAsync();
            foreach (var p in list)
            {
                var newPrice = p.SalePrice * (1 + dto.Percent / 100m);
                p.SalePrice = Math.Round(newPrice ?? 0, 2);
                p.UpdatedAt = _clock.UtcNow;
            }

            var affected = await db.SaveChangesAsync();
            return Ok(new { items = list.Count, updated = affected });
        }
    }
}
