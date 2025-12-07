/**
 * File: LayoutSectionsController.cs
 * Author: Tungnvhe
 * Created: 2025-01-20
 * Last Updated: 2025-01-20
 * Purpose: Manage layout sections - Simple CRUD without service layer
 */

using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers.Admin
{
    [Route("api/admin/layout-sections")]
    [ApiController]
    public class LayoutSectionsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public LayoutSectionsController(KeytietkiemDbContext context, IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
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
                // Không audit log cho GET và lỗi - chỉ trả về 500
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
                // Không audit log cho GET và lỗi - chỉ trả về 500
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
                // ===== Validation (KHÔNG audit 400 để tránh spam) =====
                if (string.IsNullOrWhiteSpace(request.SectionName))
                {
                    const string msg = "Tên section là bắt buộc";
                    return BadRequest(new { message = msg });
                }

                if (string.IsNullOrWhiteSpace(request.SectionKey))
                {
                    const string msg = "Section key là bắt buộc";
                    return BadRequest(new { message = msg });
                }

                // Check duplicate SectionKey
                var trimmedKey = request.SectionKey.Trim();
                var exists = await _context.LayoutSections
                    .AnyAsync(x => x.SectionKey == trimmedKey);

                if (exists)
                {
                    var msg = $"Section key '{request.SectionKey}' đã tồn tại";
                    return Conflict(new { message = msg });
                }

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
                    SectionKey = trimmedKey,
                    SectionName = request.SectionName.Trim(),
                    DisplayOrder = displayOrder,
                    IsActive = request.IsActive ?? true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.LayoutSections.Add(section);
                await _context.SaveChangesAsync();

                // AUDIT: tạo mới layout section
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "CreateSection",
                    entityType: "LayoutSection",
                    entityId: section.Id.ToString(),
                    before: null,
                    after: new
                    {
                        section.Id,
                        section.SectionKey,
                        section.SectionName,
                        section.DisplayOrder,
                        section.IsActive,
                        section.CreatedAt,
                        section.UpdatedAt
                    }
                );

                return CreatedAtAction(nameof(Get), new { id = section.Id }, section);
            }
            catch (Exception ex)
            {
                // Không audit log lỗi nữa, chỉ trả về 500
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
                {
                    const string msg = "Không tìm thấy section";
                    // Không audit log lỗi 404 để tránh spam
                    return NotFound(new { message = msg });
                }

                var beforeSnapshot = new
                {
                    section.Id,
                    section.SectionKey,
                    section.SectionName,
                    section.DisplayOrder,
                    section.IsActive,
                    section.CreatedAt,
                    section.UpdatedAt
                };

                // ===== Validation (KHÔNG audit 400/409 để tránh spam) =====
                if (string.IsNullOrWhiteSpace(request.SectionName))
                {
                    const string msg = "Tên section là bắt buộc";
                    return BadRequest(new { message = msg });
                }

                if (string.IsNullOrWhiteSpace(request.SectionKey))
                {
                    const string msg = "Section key là bắt buộc";
                    return BadRequest(new { message = msg });
                }

                var trimmedKey = request.SectionKey.Trim();

                // Check duplicate SectionKey (excluding current)
                if (!string.Equals(section.SectionKey, trimmedKey, StringComparison.Ordinal))
                {
                    var keyExists = await _context.LayoutSections
                        .AnyAsync(x => x.SectionKey == trimmedKey && x.Id != id);

                    if (keyExists)
                    {
                        var msg = $"Section key '{request.SectionKey}' đã tồn tại";
                        return Conflict(new { message = msg });
                    }
                }

                // Update fields
                section.SectionKey = trimmedKey;
                section.SectionName = request.SectionName.Trim();
                section.DisplayOrder = request.DisplayOrder ?? section.DisplayOrder ?? 0;
                section.IsActive = request.IsActive ?? section.IsActive;
                section.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var afterSnapshot = new
                {
                    section.Id,
                    section.SectionKey,
                    section.SectionName,
                    section.DisplayOrder,
                    section.IsActive,
                    section.CreatedAt,
                    section.UpdatedAt
                };

                // AUDIT: cập nhật layout section
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "UpdateSection",
                    entityType: "LayoutSection",
                    entityId: id.ToString(),
                    before: beforeSnapshot,
                    after: afterSnapshot
                );

                return Ok(section);
            }
            catch (Exception ex)
            {
                // Không audit log lỗi nữa, chỉ trả về 500
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
                {
                    const string msg = "Không tìm thấy section";
                    // Không audit log lỗi 404 để tránh spam
                    return NotFound(new { message = msg });
                }

                var beforeSnapshot = new
                {
                    section.Id,
                    section.SectionKey,
                    section.SectionName,
                    section.DisplayOrder,
                    section.IsActive,
                    section.CreatedAt,
                    section.UpdatedAt
                };

                _context.LayoutSections.Remove(section);
                await _context.SaveChangesAsync();

                // AUDIT: xóa layout section
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "DeleteSection",
                    entityType: "LayoutSection",
                    entityId: id.ToString(),
                    before: beforeSnapshot,
                    after: null
                );

                return NoContent();
            }
            catch (Exception ex)
            {
                // Không audit log lỗi nữa, chỉ trả về 500
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
                // Validation (không audit 400 để tránh spam)
                if (items == null || items.Count == 0)
                {
                    const string msg = "Dữ liệu reorder là bắt buộc";
                    return BadRequest(new { message = msg });
                }

                var beforeList = new List<object>();
                var afterList = new List<object>();

                foreach (var item in items)
                {
                    var section = await _context.LayoutSections.FindAsync(item.Id);
                    if (section != null)
                    {
                        beforeList.Add(new
                        {
                            section.Id,
                            section.DisplayOrder
                        });

                        section.DisplayOrder = item.DisplayOrder;
                        section.UpdatedAt = DateTime.UtcNow;

                        afterList.Add(new
                        {
                            section.Id,
                            section.DisplayOrder
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // AUDIT: sắp xếp lại thứ tự sections
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "ReorderSections",
                    entityType: "LayoutSection",
                    entityId: null,
                    before: beforeList,
                    after: afterList
                );

                return NoContent();
            }
            catch (Exception ex)
            {
                // Không audit log lỗi nữa, chỉ trả về 500
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
