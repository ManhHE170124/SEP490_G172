/**
 * File: BadgesController.cs
 * Author: ManhLDHE170124
 * Created: 24/10/2025
 * Last Updated: 28/10/2025
 * Version: 1.0.0
 * Purpose: Manage badges (CRUD, toggle/status) and assign badges to products.
 * Endpoints:
 *   - GET    /api/badges                         : List badges (keyword filter, active filter, sort)
 *   - GET    /api/badges/{code}                  : Get a badge by code
 *   - POST   /api/badges                         : Create a new badge
 *   - PUT    /api/badges/{code}                  : Update a badge by code
 *   - DELETE /api/badges/{code}                  : Delete a badge by code
 *   - PATCH  /api/badges/{code}/toggle           : Toggle IsActive
 *   - PATCH  /api/badges/{code}/status           : Set IsActive explicitly
 *   - POST   /api/badges/products/{productId}    : Replace a product's badges by codes
 */
using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/badges")]
public class BadgesController : ControllerBase
{
    private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

    public BadgesController(IDbContextFactory<KeytietkiemDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [HttpGet]
    /**
     * Summary: Retrieve badge list with optional keyword and active filters; supports sorting.
     * Route: GET /api/badges
     * Params:
     *   - keyword   (query, optional): search across BadgeCode, DisplayName, ColorHex, Icon
     *   - active    (query, optional): filter by IsActive (true/false)
     *   - sort      (query, optional): one of [code|name|color|active|icon], default "displayName"
     *   - direction (query, optional): "asc" | "desc", default "asc"
     * Returns: 200 OK with IEnumerable<BadgeListItemDto>
     */
    public async Task<ActionResult<IEnumerable<BadgeListItemDto>>> List(
        [FromQuery] string? keyword,
        [FromQuery] bool? active,
        [FromQuery] string? sort = "displayName",
        [FromQuery] string? direction = "asc")
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var q = db.Badges.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLowerInvariant();
            q = q.Where(b =>
                b.BadgeCode.ToLower().Contains(kw) ||
                b.DisplayName.ToLower().Contains(kw) ||
                (b.ColorHex != null && b.ColorHex.ToLower().Contains(kw)) ||
                (b.Icon != null && b.Icon.ToLower().Contains(kw))
            );
        }

        if (active is not null)
            q = q.Where(b => b.IsActive == active);

        sort = sort?.Trim().ToLowerInvariant();
        direction = direction?.Trim().ToLowerInvariant();

        q = (sort, direction) switch
        {
            ("code", "asc") => q.OrderBy(b => b.BadgeCode),
            ("code", "desc") => q.OrderByDescending(b => b.BadgeCode),
            ("name", "asc") => q.OrderBy(b => b.DisplayName),
            ("name", "desc") => q.OrderByDescending(b => b.DisplayName),
            ("color", "asc") => q.OrderBy(b => b.ColorHex),
            ("color", "desc") => q.OrderByDescending(b => b.ColorHex),
            ("active", "asc") => q.OrderBy(b => b.IsActive),
            ("active", "desc") => q.OrderByDescending(b => b.IsActive),
            ("icon", "asc") => q.OrderBy(b => b.Icon),
            ("icon", "desc") => q.OrderByDescending(b => b.Icon),
            _ => q.OrderBy(b => b.DisplayName)
        };

        var items = await q
            .Select(b => new BadgeListItemDto(
                b.BadgeCode,
                b.DisplayName,
                b.ColorHex,
                b.Icon,
                b.IsActive
            ))
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{code}")]
    /**
     * Summary: Retrieve a single badge by code.
     * Route: GET /api/badges/{code}
     * Params:
     *   - code (route): unique badge code
     * Returns: 200 OK with BadgeListItemDto, or 404 Not Found
     */
    public async Task<ActionResult<BadgeListItemDto>> Get(string code)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var b = await db.Badges.AsNoTracking().FirstOrDefaultAsync(x => x.BadgeCode == code);
        if (b is null) return NotFound();
        return Ok(new BadgeListItemDto(b.BadgeCode, b.DisplayName, b.ColorHex, b.Icon, b.IsActive));
    }

    [HttpPost]
    /**
     * Summary: Create a new badge.
     * Route: POST /api/badges
     * Body: BadgeCreateDto { BadgeCode, DisplayName, ColorHex?, Icon?, IsActive }
     * Returns: 201 Created with Location header (Get by code), 409 if BadgeCode exists
     */
    public async Task<IActionResult> Create(BadgeCreateDto dto)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        if (await db.Badges.AnyAsync(x => x.BadgeCode == dto.BadgeCode))
            return Conflict(new { message = "BadgeCode already exists" });

        db.Badges.Add(new Badge
        {
            BadgeCode = dto.BadgeCode.Trim(),
            DisplayName = dto.DisplayName.Trim(),
            ColorHex = dto.ColorHex,
            Icon = dto.Icon,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { code = dto.BadgeCode }, null);
    }

    [HttpPut("{code}")]
    /**
     * Summary: Update an existing badge by code.
     * Route: PUT /api/badges/{code}
     * Params:
     *   - code (route): badge code to update
     * Body: BadgeUpdateDto { DisplayName, ColorHex?, Icon?, IsActive }
     * Returns: 204 No Content, 404 Not Found
     */
    public async Task<IActionResult> Update(string code, BadgeUpdateDto dto)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Badges.FirstOrDefaultAsync(x => x.BadgeCode == code);
        if (e is null) return NotFound();
        e.DisplayName = dto.DisplayName.Trim();
        e.ColorHex = dto.ColorHex;
        e.Icon = dto.Icon;
        e.IsActive = dto.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{code}")]
    /**
     * Summary: Delete a badge by code.
     * Route: DELETE /api/badges/{code}
     * Params:
     *   - code (route): badge code to delete
     * Returns: 204 No Content, 404 Not Found
     */
    public async Task<IActionResult> Delete(string code)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Badges.FirstOrDefaultAsync(x => x.BadgeCode == code);
        if (e is null) return NotFound();
        db.Badges.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{code}/toggle")]
    /**
     * Summary: Toggle the IsActive state of a badge.
     * Route: PATCH /api/badges/{code}/toggle
     * Params:
     *   - code (route): badge code to toggle
     * Returns: 200 OK with { BadgeCode, IsActive }, 404 Not Found
     */
    public async Task<IActionResult> Toggle(string code)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Badges.FirstOrDefaultAsync(x => x.BadgeCode == code);
        if (e is null) return NotFound(new { message = "Badge not found" });

        e.IsActive = !e.IsActive;
        await db.SaveChangesAsync();
        return Ok(new { e.BadgeCode, e.IsActive });
    }

    [HttpPatch("{code}/status")]
    /**
     * Summary: Set the IsActive state explicitly.
     * Route: PATCH /api/badges/{code}/status
     * Params:
     *   - code (route): badge code
     * Body: bool active
     * Returns: 200 OK with { BadgeCode, IsActive }, 404 Not Found
     */
    public async Task<IActionResult> SetStatus(string code, [FromBody] bool active)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Badges.FirstOrDefaultAsync(x => x.BadgeCode == code);
        if (e is null) return NotFound(new { message = "Badge not found" });

        e.IsActive = active;
        await db.SaveChangesAsync();
        return Ok(new { e.BadgeCode, e.IsActive });
    }

    [HttpPost("products/{productId:guid}")]
    /**
     * Summary: Replace a product's badges with a given set of active badge codes.
     * Route: POST /api/badges/products/{productId}
     * Params:
     *   - productId (route, Guid): target product
     * Body: IEnumerable<string> codes (badge codes)
     * Behavior: Removes all existing ProductBadges then inserts provided active codes.
     * Returns: 204 No Content, 404 if product not found
     */
    public async Task<IActionResult> SetBadgesForProduct(Guid productId, [FromBody] IEnumerable<string> codes)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var exists = await db.Products.AnyAsync(p => p.ProductId == productId);
        if (!exists) return NotFound(new { message = "Product not found" });

        var set = codes?.Select(c => c.Trim())
                       .Where(c => !string.IsNullOrWhiteSpace(c))
                       .ToHashSet(StringComparer.OrdinalIgnoreCase)
                  ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var valid = await db.Badges.Where(b => b.IsActive && set.Contains(b.BadgeCode))
                                   .Select(b => b.BadgeCode)
                                   .ToListAsync();

        var current = await db.ProductBadges.Where(p => p.ProductId == productId).ToListAsync();
        db.ProductBadges.RemoveRange(current);
        db.ProductBadges.AddRange(valid.Select(code => new ProductBadge
        {
            ProductId = productId,
            Badge = code,
            CreatedAt = DateTime.UtcNow
        }));

        await db.SaveChangesAsync();
        return NoContent();
    }
}
