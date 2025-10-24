using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Keytietkiem.Controllers;

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

    // LIST + FILTER + PAGING
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
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.ProductId,
                p.ProductCode,
                p.ProductName,
                p.ProductType,
                p.SalePrice,
                p.StockQty,
                p.WarrantyDays,
                p.Status,
                p.Categories.Select(c => c.CategoryId)))
            .ToListAsync();

        return Ok(new PagedResult<ProductListItemDto>(items, total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailDto>> GetById(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var p = await db.Products.Include(x => x.Categories)
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(x => x.ProductId == id);
        if (p is null) return NotFound();

        return Ok(new ProductDetailDto(
            p.ProductId, p.ProductCode, p.ProductName, p.SupplierId, p.ProductType,
            p.CostPrice, p.SalePrice, p.StockQty, p.WarrantyDays, p.ExpiryDate,
            p.AutoDelivery, p.Status, p.Description, p.Categories.Select(c => c.CategoryId)));
    }

    // CREATE
    [HttpPost]
    public async Task<ActionResult<ProductDetailDto>> Create(ProductCreateDto dto)
    {
        if (!ProductEnums.Types.Contains(dto.ProductType)) return BadRequest(new { message = "Invalid ProductType" });
        if (!ProductEnums.Statuses.Contains(dto.Status)) return BadRequest(new { message = "Invalid Status" });
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
            Status = dto.Status,
            Description = dto.Description,
            CreatedAt = _clock.UtcNow
        };

        if (dto.CategoryIds is not null && dto.CategoryIds.Any())
        {
            var cats = await db.Categories.Where(c => dto.CategoryIds.Contains(c.CategoryId)).ToListAsync();
            foreach (var c in cats) e.Categories.Add(c);
        }

        db.Products.Add(e);
        await db.SaveChangesAsync();

        e = await db.Products.Include(x => x.Categories).FirstAsync(x => x.ProductId == e.ProductId);

        return CreatedAtAction(nameof(GetById), new { id = e.ProductId }, new ProductDetailDto(
            e.ProductId, e.ProductCode, e.ProductName, e.SupplierId, e.ProductType,
            e.CostPrice, e.SalePrice, e.StockQty, e.WarrantyDays, e.ExpiryDate,
            e.AutoDelivery, e.Status, e.Description, e.Categories.Select(c => c.CategoryId)));
    }

    // UPDATE
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ProductUpdateDto dto)
    {
        if (!ProductEnums.Types.Contains(dto.ProductType)) return BadRequest(new { message = "Invalid ProductType" });
        if (!ProductEnums.Statuses.Contains(dto.Status)) return BadRequest(new { message = "Invalid Status" });
        if (dto.SalePrice <= 0) return BadRequest(new { message = "SalePrice must be > 0" });
        if (dto.StockQty < 0) return BadRequest(new { message = "StockQty must be >= 0" });
        if (dto.WarrantyDays < 0) return BadRequest(new { message = "WarrantyDays must be >= 0" });

        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Products.Include(p => p.Categories).FirstOrDefaultAsync(p => p.ProductId == id);
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
        e.Status = dto.Status;
        e.Description = dto.Description;
        e.UpdatedAt = _clock.UtcNow;

        e.Categories.Clear();
        if (dto.CategoryIds is not null && dto.CategoryIds.Any())
        {
            var cats = await db.Categories.Where(c => dto.CategoryIds.Contains(c.CategoryId)).ToListAsync();
            foreach (var c in cats) e.Categories.Add(c);
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE
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

    // PATCH status (đổi trạng thái nhanh trong bảng)
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] string status)
    {
        if (!ProductEnums.Statuses.Contains(status)) return BadRequest(new { message = "Invalid Status" });

        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Products.FindAsync(id);
        if (e is null) return NotFound();

        e.Status = status;
        e.UpdatedAt = _clock.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { e.ProductId, e.Status });
    }

    // Tăng/giảm giá theo %
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

    // ===== CSV EXPORT / IMPORT (cho khu "Cập nhật giá hàng loạt") =====

    // GET: api/products/export-csv
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

    // POST: api/products/import-price-csv  (form-data: file)
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
}
