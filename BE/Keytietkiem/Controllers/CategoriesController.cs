using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    // GET: api/categories?keyword=&code=&active=true
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryListItemDto>>> Get(
      [FromQuery] string? keyword, [FromQuery] string? code, [FromQuery] bool? active)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var q = db.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(c => c.CategoryName.Contains(keyword) || c.CategoryCode.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(code))
            q = q.Where(c => c.CategoryCode == code);
        if (active is not null)
            q = q.Where(c => c.IsActive == active);

        var items = await q
            .OrderBy(c => c.DisplayOrder)                
            .Select(c => new CategoryListItemDto(       
                c.CategoryId,
                c.CategoryCode,
                c.CategoryName,
                c.IsActive,
                c.DisplayOrder,
                c.Products.Count()                      
            ))
            .ToListAsync();

        return Ok(items);
    }


    [HttpGet("{id:int}")]
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
                c.DisplayOrder
            ))
            .FirstOrDefaultAsync();

        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
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

        return CreatedAtAction(nameof(GetById), new { id = e.CategoryId },
            new CategoryDetailDto(e.CategoryId, e.CategoryCode, e.CategoryName, e.Description, e.IsActive, e.DisplayOrder));
    }

    [HttpPut("{id:int}")]
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

    // BULK UPSERT: map với nút "Tải danh mục / Lưu danh mục" trên UI
    [HttpPost("bulk-upsert")]
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
}
