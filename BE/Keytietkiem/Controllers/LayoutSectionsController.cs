/**
 * File: LayoutSectionsController.cs
 * Author: Tungnvhe
 * Created: 2025-01-20
 * Last Updated: 2025-01-20
 * Purpose: Manage layout sections - Simple CRUD without service layer
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers.Admin
{
    [Route("api/admin/layout-sections")]
    [ApiController]
    public class LayoutSectionsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;

        public LayoutSectionsController(KeytietkiemDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all layout sections
        /// GET /api/admin/layout-sections
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var sections = await _context.LayoutSections
                    .OrderBy(x => x.DisplayOrder ?? 0)
                    .ThenBy(x => x.SectionName)
                    .ToListAsync();

                return Ok(sections);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tải danh sách sections", error = ex.Message });
            }
        }

        /// <summary>
        /// Get layout section by ID
        /// GET /api/admin/layout-sections/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                var section = await _context.LayoutSections.FindAsync(id);

                if (section == null)
                    return NotFound(new { message = "Không tìm thấy section" });

                return Ok(section);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tải section", error = ex.Message });
            }
        }

        /// <summary>
        /// Create new layout section
        /// POST /api/admin/layout-sections
        /// Body: { sectionKey, sectionName, displayOrder, isActive }
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSectionRequest request)
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(request.SectionName))
                    return BadRequest(new { message = "Tên section là bắt buộc" });

                if (string.IsNullOrWhiteSpace(request.SectionKey))
                    return BadRequest(new { message = "Section key là bắt buộc" });

                // Check duplicate SectionKey
                var exists = await _context.LayoutSections
                    .AnyAsync(x => x.SectionKey == request.SectionKey.Trim());

                if (exists)
                    return Conflict(new { message = $"Section key '{request.SectionKey}' đã tồn tại" });

                // Auto DisplayOrder if not provided
                int displayOrder = request.DisplayOrder ?? 0;
                if (displayOrder <= 0)
                {
                    var maxOrder = await _context.LayoutSections
                        .MaxAsync(x => (int?)x.DisplayOrder) ?? 0;
                    displayOrder = maxOrder + 1;
                }

                // Create new section
                var section = new LayoutSection
                {
                    SectionKey = request.SectionKey.Trim(),
                    SectionName = request.SectionName.Trim(),
                    DisplayOrder = displayOrder,
                    IsActive = request.IsActive ?? true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.LayoutSections.Add(section);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(Get), new { id = section.Id }, section);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tạo section", error = ex.Message });
            }
        }

        /// <summary>
        /// Update layout section
        /// PUT /api/admin/layout-sections/{id}
        /// Body: { sectionKey, sectionName, displayOrder, isActive }
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSectionRequest request)
        {
            try
            {
                var section = await _context.LayoutSections.FindAsync(id);

                if (section == null)
                    return NotFound(new { message = "Không tìm thấy section" });

                // Validation
                if (string.IsNullOrWhiteSpace(request.SectionName))
                    return BadRequest(new { message = "Tên section là bắt buộc" });

                if (string.IsNullOrWhiteSpace(request.SectionKey))
                    return BadRequest(new { message = "Section key là bắt buộc" });

                // Check duplicate SectionKey (excluding current)
                if (section.SectionKey != request.SectionKey.Trim())
                {
                    var keyExists = await _context.LayoutSections
                        .AnyAsync(x => x.SectionKey == request.SectionKey.Trim() && x.Id != id);

                    if (keyExists)
                        return Conflict(new { message = $"Section key '{request.SectionKey}' đã tồn tại" });
                }

                // Update fields
                section.SectionKey = request.SectionKey.Trim();
                section.SectionName = request.SectionName.Trim();
                section.DisplayOrder = request.DisplayOrder ?? section.DisplayOrder ?? 0;
                section.IsActive = request.IsActive ?? section.IsActive;
                section.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(section);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi cập nhật section", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete layout section
        /// DELETE /api/admin/layout-sections/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var section = await _context.LayoutSections.FindAsync(id);

                if (section == null)
                    return NotFound(new { message = "Không tìm thấy section" });

                _context.LayoutSections.Remove(section);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi xóa section", error = ex.Message });
            }
        }

        /// <summary>
        /// Reorder layout sections
        /// PATCH /api/admin/layout-sections/reorder
        /// Body: [{ id: 1, displayOrder: 1 }, { id: 2, displayOrder: 2 }]
        /// </summary>
        [HttpPatch("reorder")]
        public async Task<IActionResult> Reorder([FromBody] List<ReorderItem> items)
        {
            try
            {
                if (items == null || items.Count == 0)
                    return BadRequest(new { message = "Dữ liệu reorder là bắt buộc" });

                foreach (var item in items)
                {
                    var section = await _context.LayoutSections.FindAsync(item.Id);
                    if (section != null)
                    {
                        section.DisplayOrder = item.DisplayOrder;
                        section.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi sắp xếp lại", error = ex.Message });
            }
        }
    }

    // ============================================
    // REQUEST MODELS
    // ============================================

    /// <summary>
    /// Request model for creating section
    /// </summary>
    public class CreateSectionRequest
    {
        public string SectionKey { get; set; } = null!;
        public string SectionName { get; set; } = null!;
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Request model for updating section
    /// </summary>
    public class UpdateSectionRequest
    {
        public string SectionKey { get; set; } = null!;
        public string SectionName { get; set; } = null!;
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Model for reorder operation
    /// </summary>
    public class ReorderItem
    {
        public int Id { get; set; }
        public int DisplayOrder { get; set; }
    }
}