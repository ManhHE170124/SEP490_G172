using System.Text.RegularExpressions;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/products/{productId:guid}/variants/{variantId:guid}/sections")]
    public class ProductSectionsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;

        private const int SectionTitleMaxLength = 200;

        public ProductSectionsController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock)
        {
            _dbFactory = dbFactory;
            _clock = clock;
        }

        private static string NormType(string? t) => ProductSectionEnums.Normalize(t);

        /// <summary>
        /// Trả true nếu HTML rỗng sau khi bỏ tag + khoảng trắng (&nbsp;, space, newline…)
        /// </summary>
        private static bool IsHtmlBlank(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return true;

            // Bỏ tag HTML
            var text = Regex.Replace(html, "<[^>]+>", string.Empty);

            // Bỏ &nbsp; và các khoảng trắng
            text = text.Replace("&nbsp;", " ");
            text = Regex.Replace(text, "\\s+", string.Empty);

            return string.IsNullOrWhiteSpace(text);
        }

        // ===== LIST: search + filter + sort + paging =====
        [HttpGet]
        public async Task<ActionResult<PagedResult<ProductSectionListItemDto>>> List(
            Guid productId,
            Guid variantId,
            [FromQuery] ProductSectionListQuery query)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Bảo đảm product/variant tồn tại và đúng quan hệ
            var variantExists = await db.ProductVariants
                .AnyAsync(v => v.ProductId == productId && v.VariantId == variantId);
            if (!variantExists) return NotFound();

            var q = db.ProductSections.AsNoTracking()
                                      .Where(s => s.VariantId == variantId);

            // Search
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var s = query.Q.Trim();
                q = q.Where(x =>
                    EF.Functions.Like(x.Title, $"%{s}%") ||
                    EF.Functions.Like(x.Content ?? "", $"%{s}%"));
            }

            // Filter type
            if (!string.IsNullOrWhiteSpace(query.Type))
            {
                var type = NormType(query.Type);
                q = q.Where(x => x.SectionType.ToUpper() == type);
            }

            // Filter active
            if (query.Active.HasValue)
            {
                q = q.Where(x => x.IsActive == query.Active.Value);
            }

            // Sort
            var sort = (query.Sort ?? "sort").Trim().ToLowerInvariant();
            var desc = string.Equals(query.Dir, "desc", StringComparison.OrdinalIgnoreCase);

            q = sort switch
            {
                "title" => desc
                    ? q.OrderByDescending(x => x.Title)
                       .ThenByDescending(x => x.CreatedAt)
                    : q.OrderBy(x => x.Title)
                       .ThenBy(x => x.CreatedAt),

                "type" => desc
                    ? q.OrderByDescending(x => x.SectionType)
                       .ThenByDescending(x => x.CreatedAt)
                    : q.OrderBy(x => x.SectionType)
                       .ThenBy(x => x.CreatedAt),

                "active" => desc
                    ? q.OrderByDescending(x => x.IsActive)
                       .ThenByDescending(x => x.CreatedAt)
                    : q.OrderBy(x => x.IsActive)
                       .ThenBy(x => x.CreatedAt),

                "created" => desc
                    ? q.OrderByDescending(x => x.CreatedAt)
                       .ThenByDescending(x => x.SectionId)
                    : q.OrderBy(x => x.CreatedAt)
                       .ThenBy(x => x.SectionId),

                "updated" => desc
                    ? q.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                       .ThenByDescending(x => x.SectionId)
                    : q.OrderBy(x => x.UpdatedAt ?? x.CreatedAt)
                       .ThenBy(x => x.SectionId),

                // Mặc định: SortOrder, nếu trùng thì CreatedAt để ổn định
                _ => desc
                    ? q.OrderByDescending(x => x.SortOrder)
                       .ThenByDescending(x => x.CreatedAt)
                    : q.OrderBy(x => x.SortOrder)
                       .ThenBy(x => x.CreatedAt),
            };

            // Paging
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);

            var total = await q.CountAsync();

            var items = await q.Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .Select(s => new ProductSectionListItemDto(
                                   s.SectionId, s.VariantId, s.SectionType, s.Title, s.Content ?? "",
                                   s.SortOrder, s.IsActive, s.CreatedAt, s.UpdatedAt))
                               .ToListAsync();

            return Ok(new PagedResult<ProductSectionListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                Items = items
            });
        }

        // ===== GET DETAIL =====
        [HttpGet("{sectionId:guid}")]
        public async Task<ActionResult<ProductSectionDetailDto>> Get(Guid productId, Guid variantId, Guid sectionId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var s = await db.ProductSections
                .FirstOrDefaultAsync(x => x.SectionId == sectionId && x.VariantId == variantId);
            if (s is null) return NotFound();

            return Ok(new ProductSectionDetailDto(
                s.SectionId, s.VariantId, s.SectionType, s.Title, s.Content ?? "",
                s.SortOrder, s.IsActive, s.CreatedAt, s.UpdatedAt));
        }

        // ===== CREATE =====
        [HttpPost]
        public async Task<ActionResult<ProductSectionDetailDto>> Create(
            Guid productId,
            Guid variantId,
            ProductSectionCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var v = await db.ProductVariants
                .FirstOrDefaultAsync(x => x.ProductId == productId && x.VariantId == variantId);
            if (v is null) return NotFound();

            // Validate type
            var type = NormType(dto.SectionType);
            if (!ProductSectionEnums.IsValid(type))
                return BadRequest(new
                {
                    code = "SECTION_TYPE_INVALID",
                    message = "Invalid SectionType. Allowed: WARRANTY | NOTE | DETAIL"
                });

            // Validate title
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest(new
                {
                    code = "SECTION_TITLE_REQUIRED",
                    message = "Tiêu đề section là bắt buộc."
                });
            }

            var title = dto.Title.Trim();
            if (title.Length > SectionTitleMaxLength)
            {
                return BadRequest(new
                {
                    code = "SECTION_TITLE_TOO_LONG",
                    message = $"Tiêu đề section không được vượt quá {SectionTitleMaxLength} ký tự."
                });
            }

            // Validate content
            if (IsHtmlBlank(dto.Content))
            {
                return BadRequest(new
                {
                    code = "SECTION_CONTENT_REQUIRED",
                    message = "Nội dung section là bắt buộc."
                });
            }

            var content = dto.Content?.Trim();

            // Validate sort
            int? sortOrder = dto.SortOrder;
            if (sortOrder.HasValue && sortOrder.Value < 0)
            {
                return BadRequest(new
                {
                    code = "SECTION_SORT_INVALID",
                    message = "Thứ tự phải là số nguyên không âm."
                });
            }

            // Nếu không gửi sort ⇒ lấy max + 1
            var nextSort = await db.ProductSections
                .Where(x => x.VariantId == variantId)
                .Select(x => (int?)x.SortOrder)
                .MaxAsync() ?? -1;

            var sortToUse = sortOrder ?? (nextSort + 1);

            var s = new ProductSection
            {
                SectionId = Guid.NewGuid(),
                VariantId = variantId,
                SectionType = type,
                Title = title,
                Content = content,
                SortOrder = sortToUse,
                IsActive = dto.IsActive,
                CreatedAt = _clock.UtcNow
            };

            db.ProductSections.Add(s);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get),
                new { productId, variantId, sectionId = s.SectionId },
                new ProductSectionDetailDto(
                    s.SectionId, s.VariantId, s.SectionType, s.Title, s.Content ?? "",
                    s.SortOrder, s.IsActive, s.CreatedAt, s.UpdatedAt));
        }

        // ===== UPDATE =====
        [HttpPut("{sectionId:guid}")]
        public async Task<IActionResult> Update(
            Guid productId,
            Guid variantId,
            Guid sectionId,
            ProductSectionUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var s = await db.ProductSections
                .FirstOrDefaultAsync(x => x.SectionId == sectionId && x.VariantId == variantId);
            if (s is null) return NotFound();

            // Validate type
            var type = NormType(dto.SectionType);
            if (!ProductSectionEnums.IsValid(type))
                return BadRequest(new
                {
                    code = "SECTION_TYPE_INVALID",
                    message = "Invalid SectionType. Allowed: WARRANTY | NOTE | DETAIL"
                });

            // Validate title
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest(new
                {
                    code = "SECTION_TITLE_REQUIRED",
                    message = "Tiêu đề section là bắt buộc."
                });
            }

            var title = dto.Title.Trim();
            if (title.Length > SectionTitleMaxLength)
            {
                return BadRequest(new
                {
                    code = "SECTION_TITLE_TOO_LONG",
                    message = $"Tiêu đề section không được vượt quá {SectionTitleMaxLength} ký tự."
                });
            }

            // Validate content
            if (IsHtmlBlank(dto.Content))
            {
                return BadRequest(new
                {
                    code = "SECTION_CONTENT_REQUIRED",
                    message = "Nội dung section là bắt buộc."
                });
            }

            var content = dto.Content?.Trim();

            // Validate sort (cho phép null => giữ nguyên sort cũ)
            int? sortOrder = dto.SortOrder;
            if (sortOrder.HasValue && sortOrder.Value < 0)
            {
                return BadRequest(new
                {
                    code = "SECTION_SORT_INVALID",
                    message = "Thứ tự phải là số nguyên không âm."
                });
            }

            s.SectionType = type;
            s.Title = title;
            s.Content = content;
            if (sortOrder.HasValue)
            {
                s.SortOrder = sortOrder.Value;
            }

            s.IsActive = dto.IsActive;
            s.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            return NoContent();
        }

        // ===== DELETE =====
        [HttpDelete("{sectionId:guid}")]
        public async Task<IActionResult> Delete(Guid productId, Guid variantId, Guid sectionId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var s = await db.ProductSections
                .FirstOrDefaultAsync(x => x.SectionId == sectionId && x.VariantId == variantId);
            if (s is null) return NotFound();

            db.ProductSections.Remove(s);
            await db.SaveChangesAsync();
            return NoContent();
        }

        // ===== TOGGLE ACTIVE =====
        [HttpPatch("{sectionId:guid}/toggle")]
        public async Task<IActionResult> Toggle(Guid productId, Guid variantId, Guid sectionId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var s = await db.ProductSections
                .FirstOrDefaultAsync(x => x.SectionId == sectionId && x.VariantId == variantId);
            if (s is null) return NotFound();

            s.IsActive = !s.IsActive;
            s.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync();
            return Ok(new { s.SectionId, s.IsActive });
        }

        // ===== REORDER =====
        [HttpPost("reorder")]
        public async Task<IActionResult> Reorder(Guid productId, Guid variantId, SectionReorderDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var list = await db.ProductSections
                .Where(x => x.VariantId == variantId)
                .ToListAsync();

            var pos = 0;
            foreach (var id in dto.SectionIdsInOrder)
            {
                var found = list.FirstOrDefault(x => x.SectionId == id);
                if (found != null) found.SortOrder = pos++;
            }

            await db.SaveChangesAsync();
            return NoContent();
        }
    }
}
