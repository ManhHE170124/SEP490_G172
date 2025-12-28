/**
 * File: CategoriesController.cs
 * Author: ManhLDHE170124
 * Created: 24/10/2025
 * Last Updated: 07/12/2025
 * Version: 1.3.0
 * Purpose: Manage product categories (CRUD + toggle) with audit logging on important operations.
 * Endpoints:
 *   - GET    /api/categories              : List categories (keyword/active filter, sort, paging)
 *   - GET    /api/categories/{id}         : Get category by id
 *   - POST   /api/categories              : Create a new category
 *   - PUT    /api/categories/{id}         : Update a category
 *   - DELETE /api/categories/{id}         : Delete a category
 *   - PATCH  /api/categories/{id}/toggle  : Toggle IsActive
 */
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
    private readonly IClock _clock;
    private readonly IAuditLogger _auditLogger;

    private const int CategoryCodeMaxLength = 50;
    private const int CategoryNameMaxLength = 100;
    private const int CategoryDescriptionMaxLength = 200;

    public CategoriesController(
        IDbContextFactory<KeytietkiemDbContext> dbFactory,
        IClock clock,
        IAuditLogger auditLogger)
    {
        _dbFactory = dbFactory;
        _clock = clock;
        _auditLogger = auditLogger;
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
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]    
    /**
     * Summary: Retrieve category list with optional keyword/active filters; supports sorting & pagination.
     * Route: GET /api/categories
     * Params:
     *   - keyword   (query, optional): search across CategoryCode, CategoryName, Description
     *   - active    (query, optional): filter by IsActive (true/false)
     *   - sort      (query, optional): one of [name|code|active], default "name"
     *   - direction (query, optional): "asc" | "desc", default "asc"
     *   - page      (query, optional): page index starts from 1, default 1
     *   - pageSize  (query, optional): page size (1..200), default 10
     * Returns:
     *   - 200 OK with { items, total, page, pageSize } where items is IEnumerable<CategoryListItemDto-like>
     */
    public async Task<IActionResult> Get(
        [FromQuery] string? keyword,
        [FromQuery] bool? active,
        [FromQuery] string? sort = "name",
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
            ("active", "asc") => q.OrderBy(c => c.IsActive),
            ("active", "desc") => q.OrderByDescending(c => c.IsActive),
            _ => q.OrderBy(c => c.CategoryId)
        };

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var total = await q.CountAsync();

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.CategoryId,
                c.CategoryCode,
                c.CategoryName,
                c.Description,
                c.IsActive,
                ProductsCount = c.Products.Count()
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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
                c.Products.Count()
            ))
            .FirstOrDefaultAsync();

        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    /**
     * Summary: Create a new category.
     * Route: POST /api/categories
     * Body: CategoryCreateDto { CategoryCode, CategoryName, Description?, IsActive }
     * Behavior:
     *   - Slug chính lưu trong CategoryCode được sinh từ CategoryName.
     *   - Nếu Admin truyền CategoryCode thì:
     *       + Vẫn dùng CategoryName để sinh slug lưu
     *       + Nhưng CategoryCode sẽ được normalize & validate:
     *           * Nếu normalize ra rỗng -> 400
     *           * Nếu quá dài -> 400
     *           * Nếu trùng slug trong DB -> 409
     *   - Nếu không truyền CategoryCode thì dùng slug normalize từ CategoryName,
     *     validate required/length/duplicate như bình thường.
     */
    public async Task<ActionResult<CategoryDetailDto>> Create(CategoryCreateDto dto)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // ===== 1. Validate CategoryName =====
        var name = dto.CategoryName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            const string msg = "CategoryName is required";
            return BadRequest(new { message = msg });
        }

        if (name.Length > CategoryNameMaxLength)
        {
            var msg = $"CategoryName cannot exceed {CategoryNameMaxLength} characters";
            return BadRequest(new { message = msg });
        }

        // ===== 2. Validate Description =====
        var desc = dto.Description;
        if (desc != null && desc.Length > CategoryDescriptionMaxLength)
        {
            var msg = $"Description cannot exceed {CategoryDescriptionMaxLength} characters";
            return BadRequest(new { message = msg });
        }

        // ===== 3. Slug từ CategoryName (slug chính sẽ lưu vào DB) =====
        var slugFromName = NormalizeSlug(name);
        if (string.IsNullOrEmpty(slugFromName))
        {
            const string msg = "CategoryCode (slug) is required";
            return BadRequest(new { message = msg });
        }

        if (slugFromName.Length > CategoryCodeMaxLength)
        {
            var msg = $"CategoryCode cannot exceed {CategoryCodeMaxLength} characters";
            return BadRequest(new { message = msg });
        }

        // ===== 4. Nếu Admin có nhập CategoryCode thì validate riêng =====
        string? slugFromCode = null;
        if (!string.IsNullOrWhiteSpace(dto.CategoryCode))
        {
            slugFromCode = NormalizeSlug(dto.CategoryCode!);

            if (string.IsNullOrEmpty(slugFromCode))
            {
                // Trường hợp test: Create_SlugRequired_WhenNormalizedEmpty_Returns400
                const string msg = "CategoryCode (slug) is required";
                return BadRequest(new { message = msg });
            }

            if (slugFromCode.Length > CategoryCodeMaxLength)
            {
                // Trường hợp test: Create_SlugTooLong_Returns400
                var msg = $"CategoryCode cannot exceed {CategoryCodeMaxLength} characters";
                return BadRequest(new { message = msg });
            }
        }

        // ===== 5. Check trùng slug trong DB (theo cả slugFromName & slugFromCode nếu có) =====
        var slugsToCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        slugFromName
    };
        if (!string.IsNullOrEmpty(slugFromCode))
            slugsToCheck.Add(slugFromCode);

        var duplicate = await db.Categories
            .AnyAsync(c => slugsToCheck.Contains(c.CategoryCode));

        if (duplicate)
        {
            // Trường hợp test: Create_DuplicateSlug_Returns409
            const string msg = "CategoryCode already exists";
            return Conflict(new { message = msg });
        }

        // ===== 6. Tạo entity: luôn lưu slug chính theo CategoryName =====
        var e = new Category
        {
            CategoryCode = slugFromName,   // ví dụ "software-keys"
            CategoryName = name,
            Description = desc,
            IsActive = dto.IsActive,
            CreatedAt = _clock.UtcNow
        };

        db.Categories.Add(e);
        await db.SaveChangesAsync();

        // AUDIT: tạo category mới
        await _auditLogger.LogAsync(
            HttpContext,
            action: "CreateCategory",
            entityType: "Category",
            entityId: e.CategoryId.ToString(),
            before: null,
            after: new
            {
                e.CategoryId,
                e.CategoryCode,
                e.CategoryName,
                e.Description,
                e.IsActive
            }
        );

        return CreatedAtAction(nameof(GetById), new { id = e.CategoryId },
            new CategoryDetailDto(
                e.CategoryId,
                e.CategoryCode,
                e.CategoryName,
                e.Description,
                e.IsActive,
                0
            ));
    }



    [HttpPut("{id:int}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    /**
     * Summary: Update an existing category by id.
     * Route: PUT /api/categories/{id}
     * Params:
     *   - id (route, int): category identifier
     * Body: CategoryUpdateDto { CategoryCode?, CategoryName, Description?, IsActive }
     * Returns: 204 No Content, 404 Not Found
     */
    public async Task<IActionResult> Update(int id, CategoryUpdateDto dto)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Categories.FirstOrDefaultAsync(c => c.CategoryId == id);
        if (e is null)
        {
            const string msg = "Category not found";
            return NotFound(new { message = msg });
        }

        var beforeSnapshot = new
        {
            e.CategoryId,
            e.CategoryCode,
            e.CategoryName,
            e.Description,
            e.IsActive
        };

        var name = dto.CategoryName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            const string msg = "CategoryName is required";
            return BadRequest(new { message = msg });
        }

        if (name.Length > CategoryNameMaxLength)
        {
            var msg = $"CategoryName cannot exceed {CategoryNameMaxLength} characters";
            return BadRequest(new { message = msg });
        }

        var desc = dto.Description;
        if (desc != null && desc.Length > CategoryDescriptionMaxLength)
        {
            var msg = $"Description cannot exceed {CategoryDescriptionMaxLength} characters";
            return BadRequest(new { message = msg });
        }

        // Optional update of CategoryCode
        if (dto.CategoryCode is not null)
        {
            var rawCode = dto.CategoryCode;
            var code = NormalizeSlug(rawCode);

            if (string.IsNullOrEmpty(code))
            {
                const string msg = "CategoryCode (slug) is required";
                return BadRequest(new { message = msg });
            }

            if (code.Length > CategoryCodeMaxLength)
            {
                var msg = $"CategoryCode cannot exceed {CategoryCodeMaxLength} characters";
                return BadRequest(new { message = msg });
            }

            var exists = await db.Categories
                .AnyAsync(c => c.CategoryCode == code && c.CategoryId != id);

            if (exists)
            {
                const string msg = "CategoryCode already exists";
                return Conflict(new { message = msg });
            }

            e.CategoryCode = code;
        }

        e.CategoryName = name;
        e.Description = desc;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync();

        var afterSnapshot = new
        {
            e.CategoryId,
            e.CategoryCode,
            e.CategoryName,
            e.Description,
            e.IsActive
        };

        // AUDIT: cập nhật category
        await _auditLogger.LogAsync(
            HttpContext,
            action: "UpdateCategory",
            entityType: "Category",
            entityId: id.ToString(),
            before: beforeSnapshot,
            after: afterSnapshot
        );

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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
        if (e is null)
        {
            const string msg = "Category not found";
            return NotFound(new { message = msg });
        }

        var beforeSnapshot = new
        {
            e.CategoryId,
            e.CategoryCode,
            e.CategoryName,
            e.Description,
            e.IsActive
        };

        db.Categories.Remove(e);
        await db.SaveChangesAsync();

        // AUDIT: xóa category
        await _auditLogger.LogAsync(
            HttpContext,
            action: "DeleteCategory",
            entityType: "Category",
            entityId: id.ToString(),
            before: beforeSnapshot,
            after: null
        );

        return NoContent();
    }

    [HttpPatch("{id:int}/toggle")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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
        if (e is null)
        {
            const string msg = "Category not found";
            return NotFound(new { message = msg });
        }

        var beforeSnapshot = new
        {
            e.CategoryId,
            e.IsActive
        };

        e.IsActive = !e.IsActive;
        e.UpdatedAt = _clock.UtcNow;
        await db.SaveChangesAsync();

        var afterSnapshot = new
        {
            e.CategoryId,
            e.IsActive
        };

        // AUDIT: bật/tắt category
        await _auditLogger.LogAsync(
            HttpContext,
            action: "ToggleCategory",
            entityType: "Category",
            entityId: id.ToString(),
            before: beforeSnapshot,
            after: afterSnapshot
        );

        return Ok(new { e.CategoryId, e.IsActive });
    }
}
