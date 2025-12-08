using Keytietkiem.DTOs.Products;
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

    private const int BadgeCodeMaxLength = 32;
    private const int BadgeDisplayNameMaxLength = 64;
    private static bool IsValidHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return false;
        var c = color.Trim();
        if (c[0] != '#') return false;
        // #RGB (4) hoặc #RRGGBB (7)
        if (c.Length != 4 && c.Length != 7) return false;
        for (int i = 1; i < c.Length; i++)
        {
            var ch = c[i];
            bool isHex = (ch >= '0' && ch <= '9') ||
                         (ch >= 'a' && ch <= 'f') ||
                         (ch >= 'A' && ch <= 'F');
            if (!isHex) return false;
        }
        return true;
    }

    public BadgesController(IDbContextFactory<KeytietkiemDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] bool? active,
        [FromQuery] string? sort = "displayName",
        [FromQuery] string? direction = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
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

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var total = await q.CountAsync();

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BadgeListItemDto(
                b.BadgeCode,
                b.DisplayName,
                b.ColorHex,
                b.Icon,
                b.IsActive,
                db.ProductBadges.Count(pb => pb.Badge == b.BadgeCode)
            ))
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<BadgeListItemDto>> Get(string code)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var dto = await db.Badges
            .AsNoTracking()
            .Where(b => b.BadgeCode == code)
            .Select(b => new BadgeListItemDto(
                b.BadgeCode,
                b.DisplayName,
                b.ColorHex,
                b.Icon,
                b.IsActive,
                db.ProductBadges.Count(pb => pb.Badge == b.BadgeCode)
            ))
            .FirstOrDefaultAsync();

        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create(BadgeCreateDto dto)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // ===== Validate BadgeCode =====
        var rawCode = dto.BadgeCode ?? string.Empty;
        var code = rawCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "BadgeCode is required" });

        if (code.Contains(' '))
            return BadRequest(new { message = "BadgeCode cannot contain spaces" });

        if (code.Length > BadgeCodeMaxLength)
            return BadRequest(new { message = $"BadgeCode cannot exceed {BadgeCodeMaxLength} characters" });

        if (await db.Badges.AnyAsync(x => x.BadgeCode == code))
            return Conflict(new { message = "BadgeCode already exists" });

        // ===== Validate DisplayName =====
        var name = dto.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "DisplayName is required" });

        if (name.Length > BadgeDisplayNameMaxLength)
            return BadRequest(new { message = $"DisplayName cannot exceed {BadgeDisplayNameMaxLength} characters" });

        // ===== Validate ColorHex =====
        string? color = null;
        if (!string.IsNullOrWhiteSpace(dto.ColorHex))
        {
            color = dto.ColorHex.Trim();
            if (!IsValidHexColor(color))
                return BadRequest(new { message = "ColorHex must be a valid hex color, e.g. #1e40af" });
        }

        var e = new Badge
        {
            BadgeCode = code,
            DisplayName = name,
            ColorHex = color,
            Icon = dto.Icon?.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        db.Badges.Add(e);
        await db.SaveChangesAsync();

        var body = new BadgeListItemDto(
            e.BadgeCode,
            e.DisplayName,
            e.ColorHex,
            e.Icon,
            e.IsActive,
            0
        );

        return CreatedAtAction(nameof(Get), new { code = e.BadgeCode }, body);
    }

    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, BadgeUpdateDto dto)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Badges.FirstOrDefaultAsync(x => x.BadgeCode == code);
        if (e is null) return NotFound(new { message = "Badge not found" });

        // ===== Validate DisplayName =====
        var name = dto.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "DisplayName is required" });

        if (name.Length > BadgeDisplayNameMaxLength)
            return BadRequest(new { message = $"DisplayName cannot exceed {BadgeDisplayNameMaxLength} characters" });

        // ===== Validate BadgeCode (mới) =====
        var rawCode = dto.BadgeCode ?? string.Empty;
        var newCode = rawCode.Trim();
        if (string.IsNullOrWhiteSpace(newCode))
            return BadRequest(new { message = "BadgeCode is required" });

        if (newCode.Contains(' '))
            return BadRequest(new { message = "BadgeCode cannot contain spaces" });

        if (newCode.Length > BadgeCodeMaxLength)
            return BadRequest(new { message = $"BadgeCode cannot exceed {BadgeCodeMaxLength} characters" });

        var codeChanged = !string.Equals(newCode, e.BadgeCode, StringComparison.Ordinal);

        if (codeChanged)
        {
            var exists = await db.Badges.AnyAsync(b => b.BadgeCode == newCode && b.BadgeCode != e.BadgeCode);
            if (exists)
                return Conflict(new { message = "BadgeCode already exists" });
        }

        // ===== Validate ColorHex =====
        string? color = null;
        if (!string.IsNullOrWhiteSpace(dto.ColorHex))
        {
            color = dto.ColorHex.Trim();
            if (!IsValidHexColor(color))
                return BadRequest(new { message = "ColorHex must be a valid hex color, e.g. #1e40af" });
        }

        // Cập nhật info chung
        e.DisplayName = name;
        e.ColorHex = color;
        e.Icon = dto.Icon?.Trim();
        e.IsActive = dto.IsActive;

        // Nếu KHÔNG đổi mã -> chỉ update metadata
        if (!codeChanged)
        {
            await db.SaveChangesAsync();
            return NoContent();
        }

        // ========== ĐỔI MÃ NHÃN (BadgeCode) ==========

        var oldCode = e.BadgeCode;

        // 1) Tạo badge mới với mã mới (copy metadata)
        var newBadge = new Badge
        {
            BadgeCode = newCode,
            DisplayName = e.DisplayName,
            ColorHex = e.ColorHex,
            Icon = e.Icon,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt
        };
        db.Badges.Add(newBadge);

        // 2) Lấy tất cả ProductBadges dùng oldCode
        var related = await db.ProductBadges
            .Where(pb => pb.Badge == oldCode)
            .ToListAsync();

        if (related.Count > 0)
        {
            // Tạo các ProductBadge mới với Badge = newCode
            var newProductBadges = related.Select(pb => new ProductBadge
            {
                ProductId = pb.ProductId,
                Badge = newCode,
                CreatedAt = pb.CreatedAt
            }).ToList();

            db.ProductBadges.AddRange(newProductBadges);

            // Xoá các ProductBadge cũ (Badge = oldCode)
            db.ProductBadges.RemoveRange(related);
        }

        // 3) Xoá badge cũ
        db.Badges.Remove(e);

        await db.SaveChangesAsync();
        return NoContent();
    }





    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var e = await db.Badges.FirstOrDefaultAsync(x => x.BadgeCode == code);
        if (e is null) return NotFound(new { message = "Badge not found" });

        // Xóa quan hệ ProductBadges trước (tránh FK error + badge không còn hiển thị ở product)
        var related = await db.ProductBadges
            .Where(pb => pb.Badge == code)
            .ToListAsync();

        if (related.Count > 0)
            db.ProductBadges.RemoveRange(related);

        db.Badges.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{code}/toggle")]
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
