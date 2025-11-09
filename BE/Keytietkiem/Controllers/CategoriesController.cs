/**
 * File: CategoriesController.cs
 * Author: ManhLDHE170124
 * Created: 24/10/2025
 * Last Updated: 28/10/2025
 * Version: 1.0.0
 * Purpose: Manage product categories (CRUD, toggle), bulk upsert, and CSV import/export.
 * Endpoints:
 *   - GET    /api/categories                     : List categories (keyword/active filter, sort)
 *   - GET    /api/categories/{id}                : Get category by id
 *   - POST   /api/categories                     : Create a new category
 *   - PUT    /api/categories/{id}                : Update a category
 *   - DELETE /api/categories/{id}                : Delete a category
 *   - PATCH  /api/categories/{id}/toggle         : Toggle IsActive
 *   - POST   /api/categories/bulk-upsert         : Bulk upsert categories
 *   - GET    /api/categories/export.csv          : Export categories to CSV
 *   - POST   /api/categories/import.csv          : Import categories from CSV
 */
using CsvHelper;
using CsvHelper.Configuration;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Formats.Asn1;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
    private readonly IClock _clock;

    public CategoriesController(IDbContextFactory<KeytietkiemDbContext> dbFactory, IClock clock)
    {
        _dbFactory = dbFactory;
        _clock = clock;
    }

    private static string NormalizeSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, @"[^a-z0-9\-]", "");
        s = Regex.Replace(s, @"-+", "-");
        return s;
    }

    [HttpGet]
    /**
     * Summary: Retrieve category list with optional keyword/active filters; supports sorting & pagination.
     * Route: GET /api/categories
     * Params:
     *   - keyword   (query, optional): search across CategoryCode, CategoryName, Description
     *   - active    (query, optional): filter by IsActive (true/false)
     *   - sort      (query, optional): one of [name|code|displayOrder|active], default "displayOrder"
     *   - direction (query, optional): "asc" | "desc", default "asc"
     *   - page      (query, optional): page index starts from 1, default 1
     *   - pageSize  (query, optional): page size (1..200), default 20
     * Returns:
     *   - 200 OK with { items, total, page, pageSize } where items is IEnumerable<CategoryListItemDto>
     */
    public async Task<IActionResult> Get(
     [FromQuery] string? keyword,
     [FromQuery] bool? active,
     [FromQuery] string? sort = "displayOrder",
     [FromQuery] string? direction = "asc",
     [FromQuery] int page = 1,
     [FromQuery] int pageSize = 10)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var q = db.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLowerInvariant();
            q = q.Where(c =>
                c.CategoryCode.ToLower().Contains(kw) ||
                c.CategoryName.ToLower().Contains(kw) ||
                (c.Description != null && c.Description.ToLower().Contains(kw))
            );
        }

        if (active is not null)
            q = q.Where(c => c.IsActive == active);

        sort = sort?.Trim().ToLowerInvariant();
        direction = direction?.Trim().ToLowerInvariant();

        q = (sort, direction) switch
        {
            ("name", "asc") => q.OrderBy(c => c.CategoryName),
            ("name", "desc") => q.OrderByDescending(c => c.CategoryName),
            ("code", "asc") => q.OrderBy(c => c.CategoryCode),
            ("code", "desc") => q.OrderByDescending(c => c.CategoryCode),
            ("displayorder", "asc") => q.OrderBy(c => c.DisplayOrder),
            ("displayorder", "desc") => q.OrderByDescending(c => c.DisplayOrder),
            ("active", "asc") => q.OrderBy(c => c.IsActive),
            ("active", "desc") => q.OrderByDescending(c => c.IsActive),
            _ => q.OrderBy(c => c.DisplayOrder)
        };

        // ===== Pagination =====
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var total = await q.CountAsync();

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoryListItemDto(
                c.CategoryId,
                c.CategoryCode,
                c.CategoryName,
                c.IsActive,
                c.DisplayOrder,
                c.Products.Count() // productsCount giống trước đây
            ))
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }


    [HttpGet("{id:int}")]
    /**
     * Summary: Retrieve a single category by id (includes ProductCount).
     * Route: GET /api/categories/{id}
     * Params:
     *   - id (route, int): category identifier
     * Returns: 200 OK with CategoryDetailDto, or 404 Not Found
     */
    public async Task<ActionResult<CategoryDetailDto>> GetById(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var dto = await db.Categories
            .AsNoTracking()
            .Where(c => c.CategoryId == id)
            .Select(c => new CategoryDetailDto(
                c.CategoryId,
                c.CategoryCode,
                c.CategoryName,
                c.Description,
                c.IsActive,
                c.DisplayOrder,
                c.Products.Count()
            ))
            .FirstOrDefaultAsync();

        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    /**
     * Summary: Create a new category.
     * Route: POST /api/categories
     * Body: CategoryCreateDto { CategoryCode, CategoryName, Description?, IsActive, DisplayOrder }
     * Behavior: Normalizes CategoryCode to slug; ensures uniqueness.
     * Returns: 201 Created with Location header (GetById), 400/409 on validation errors
     */
    public async Task<ActionResult<CategoryDetailDto>> Create(CategoryCreateDto dto)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var code = NormalizeSlug(dto.CategoryCode);
        if (string.IsNullOrEmpty(code))
            return BadRequest(new { message = "CategoryCode (slug) is required" });

        if (await db.Categories.AnyAsync(c => c.CategoryCode == code))
            return Conflict(new { message = "CategoryCode already exists" });

        var e = new Category
        {
            CategoryCode = code,
            CategoryName = dto.CategoryName.Trim(),
            Description = dto.Description,
            IsActive = dto.IsActive,
            DisplayOrder = dto.DisplayOrder,
            CreatedAt = _clock.UtcNow
        };

        db.Categories.Add(e);
        await db.SaveChangesAsync();

        // ProductCount = 0 khi mới tạo
        return CreatedAtAction(nameof(GetById), new { id = e.CategoryId },
            new CategoryDetailDto(e.CategoryId, e.CategoryCode, e.CategoryName, e.Description, e.IsActive, e.DisplayOrder, 0));
    }


    [HttpPut("{id:int}")]
    /**
     * Summary: Update an existing category by id.
     * Route: PUT /api/categories/{id}
     * Params:
     *   - id (route, int): category identifier
     * Body: CategoryUpdateDto { CategoryName, Description?, IsActive, DisplayOrder }
     * Returns: 204 No Content, 404 Not Found
     */
    public async Task<IActionResult> Update(int id, CategoryUpdateDto dto)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Categories.FirstOrDefaultAsync(c => c.CategoryId == id);
        if (e is null) return NotFound();

        e.CategoryName = dto.CategoryName.Trim();
        e.Description = dto.Description;
        e.IsActive = dto.IsActive;
        e.DisplayOrder = dto.DisplayOrder;
        e.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    /**
     * Summary: Delete a category by id.
     * Route: DELETE /api/categories/{id}
     * Params:
     *   - id (route, int): category identifier
     * Returns: 204 No Content, 404 Not Found
     */
    public async Task<IActionResult> Delete(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Categories.FindAsync(id);
        if (e is null) return NotFound();

        db.Categories.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/toggle")]
    /**
     * Summary: Toggle the IsActive state of a category.
     * Route: PATCH /api/categories/{id}/toggle
     * Params:
     *   - id (route, int): category identifier
     * Returns: 200 OK with { CategoryId, IsActive }, 404 Not Found
     */
    public async Task<IActionResult> Toggle(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Categories.FirstOrDefaultAsync(c => c.CategoryId == id);
        if (e is null) return NotFound();

        e.IsActive = !e.IsActive;
        e.UpdatedAt = _clock.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { e.CategoryId, e.IsActive });
    }

    [HttpPost("bulk-upsert")]
    /**
     * Summary: Bulk upsert categories from payload items.
     * Route: POST /api/categories/bulk-upsert
     * Body: CategoryBulkUpsertDto { Items: List<{ CategoryCode, CategoryName, Description?, IsActive, DisplayOrder }> }
     * Behavior: Insert if not exists (by normalized CategoryCode), else update fields.
     * Returns: 200 OK with { created, updated, changed }
     */
    public async Task<ActionResult<object>> BulkUpsert(CategoryBulkUpsertDto dto)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            return BadRequest(new { message = "Empty payload" });

        await using var db = await _dbFactory.CreateDbContextAsync();

        int created = 0, updated = 0;
        foreach (var item in dto.Items)
        {
            var code = NormalizeSlug(item.CategoryCode);
            if (string.IsNullOrEmpty(code)) continue;

            var e = await db.Categories.FirstOrDefaultAsync(c => c.CategoryCode == code);
            if (e is null)
            {
                db.Categories.Add(new Category
                {
                    CategoryCode = code,
                    CategoryName = item.CategoryName.Trim(),
                    Description = item.Description,
                    IsActive = item.IsActive,
                    DisplayOrder = item.DisplayOrder,
                    CreatedAt = _clock.UtcNow
                });
                created++;
            }
            else
            {
                e.CategoryName = item.CategoryName.Trim();
                e.Description = item.Description;
                e.IsActive = item.IsActive;
                e.DisplayOrder = item.DisplayOrder;
                e.UpdatedAt = _clock.UtcNow;
                updated++;
            }
        }

        var changed = await db.SaveChangesAsync();
        return Ok(new { created, updated, changed });
    }

    public record CategoryCsvRow(string CategoryCode, string CategoryName, string Description, bool IsActive, int DisplayOrder);

    [HttpGet("export.csv")]
    /**
     * Summary: Export categories as CSV.
     * Route: GET /api/categories/export.csv
     * Params: none
     * Returns: 200 OK with CSV file (text/csv)
     */
    public async Task<IActionResult> ExportCsv()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Categories.AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CategoryCsvRow(x.CategoryCode, x.CategoryName, x.Description ?? "", x.IsActive, x.DisplayOrder))
            .ToListAsync();

        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
        await using var ms = new MemoryStream();
        await using (var writer = new StreamWriter(ms, new UTF8Encoding(true)))
        await using (var csv = new CsvWriter(writer, cfg))
        {
            csv.WriteRecords(rows);
            await writer.FlushAsync();
        }
        return File(ms.ToArray(), "text/csv", "categories.csv");
    }

    [HttpPost("import.csv")]
    /**
     * Summary: Import categories from a CSV file.
     * Route: POST /api/categories/import.csv
     * Body: IFormFile file (CSV with headers: CategoryCode,CategoryName,Description,IsActive,DisplayOrder)
     * Behavior: Upserts by normalized CategoryCode.
     * Returns: 200 OK with { created, updated, total }
     */
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "CSV file is required" });

        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null, BadDataFound = null };
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var csv = new CsvReader(reader, cfg);

        var rows = csv.GetRecords<CategoryCsvRow>().ToList();
        await using var db = await _dbFactory.CreateDbContextAsync();

        int created = 0, updated = 0;
        foreach (var r in rows)
        {
            var code = NormalizeSlug(r.CategoryCode);
            if (string.IsNullOrWhiteSpace(code)) continue;

            var e = await db.Categories.FirstOrDefaultAsync(x => x.CategoryCode == code);
            if (e == null)
            {
                db.Categories.Add(new Category
                {
                    CategoryCode = code,
                    CategoryName = r.CategoryName.Trim(),
                    Description = r.Description,
                    IsActive = r.IsActive,
                    DisplayOrder = r.DisplayOrder,
                    CreatedAt = _clock.UtcNow
                });
                created++;
            }
            else
            {
                e.CategoryName = r.CategoryName.Trim();
                e.Description = r.Description;
                e.IsActive = r.IsActive;
                e.DisplayOrder = r.DisplayOrder;
                e.UpdatedAt = _clock.UtcNow;
                updated++;
            }
        }
        await db.SaveChangesAsync();
        return Ok(new { created, updated, total = rows.Count });
    }
}
