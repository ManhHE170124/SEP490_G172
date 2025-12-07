/**
* File: TagsController.cs
* Author: HieuNDHE173169
* Created: 21/10/2025
* Last Updated: 24/10/2025
* Version: 1.0.0
* Purpose: Manage tags (CRUD). Ensures unique tag names and slugs,
*          and maintains referential integrity on updates/deletions.
* Endpoints:
*   - GET    /api/tags              : List all tags
*   - GET    /api/tags/{id}         : Get a tag by id
*   - POST   /api/tags              : Create a tag
*   - PUT    /api/tags/{id}         : Update a tag
*   - DELETE /api/tags/{id}         : Delete a tag
*/

using Keytietkiem.DTOs.Post;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TagsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public TagsController(KeytietkiemDbContext context, IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        /**
         * Summary: Retrieve all tags.
         * Route: GET /api/tags
         * Params: none
         * Returns: 200 OK with list of tags
         * ⚠️ Không audit log GET để tránh spam log.
         */
        [HttpGet]
        public async Task<IActionResult> GetTags()
        {
            var tags = await _context.Tags
                .Select(t => new TagDTO
                {
                    TagId = t.TagId,
                    TagName = t.TagName,
                    Slug = t.Slug
                })
                .ToListAsync();
            return Ok(tags);
        }

        /**
         * Summary: Retrieve a tag by id.
         * Route: GET /api/tags/{id}
         * Params: id (Guid) - tag identifier
         * Returns: 200 OK with tag, 404 if not found
         * ⚠️ Không audit log GET để tránh spam log.
         */
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTagById(Guid id)
        {
            var tag = await _context.Tags
                .FirstOrDefaultAsync(t => t.TagId == id);
            if (tag == null)
            {
                return NotFound();
            }

            var tagDto = new TagDTO
            {
                TagId = tag.TagId,
                TagName = tag.TagName,
                Slug = tag.Slug
            };

            return Ok(tagDto);
        }

        /**
         * Summary: Create a new tag.
         * Route: POST /api/tags
         * Body: CreateTagDTO createTagDto
         * Returns: 201 Created with created tag, 400/409 on validation errors
         * ✅ Có audit log khi tạo thành công.
         */
        [HttpPost]
        public async Task<IActionResult> CreateTag([FromBody] CreateTagDTO createTagDto)
        {
            if (createTagDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(" ", errors) });
            }

            var existingByName = await _context.Tags
                .FirstOrDefaultAsync(t => t.TagName == createTagDto.TagName);
            if (existingByName != null)
            {
                return Conflict(new { message = "Tên thẻ đã tồn tại." });
            }

            var existingBySlug = await _context.Tags
                .FirstOrDefaultAsync(t => t.Slug == createTagDto.Slug);
            if (existingBySlug != null)
            {
                return Conflict(new { message = "Slug đã tồn tại." });
            }

            var newTag = new Tag
            {
                TagName = createTagDto.TagName,
                Slug = createTagDto.Slug
            };

            _context.Tags.Add(newTag);
            await _context.SaveChangesAsync();

            var tagDto = new TagDTO
            {
                TagId = newTag.TagId,
                TagName = newTag.TagName,
                Slug = newTag.Slug
            };

            // 🔐 AUDIT LOG – CREATE TAG
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Create",
                entityType: "Tag",
                entityId: newTag.TagId.ToString(),
                before: null,
                after: new
                {
                    newTag.TagId,
                    newTag.TagName,
                    newTag.Slug
                }
            );

            return CreatedAtAction(nameof(GetTagById), new { id = newTag.TagId }, tagDto);
        }

        /**
         * Summary: Update an existing tag by id.
         * Route: PUT /api/tags/{id}
         * Params: id (Guid)
         * Body: UpdateTagDTO updateTagDto
         * Returns: 204 No Content, 400/404/409 on errors
         * ✅ Có audit log khi cập nhật thành công.
         */
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTag(Guid id, [FromBody] UpdateTagDTO updateTagDto)
        {
            if (updateTagDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(" ", errors) });
            }

            var existing = await _context.Tags
                .FirstOrDefaultAsync(t => t.TagId == id);
            if (existing == null)
            {
                return NotFound();
            }

            var existingByName = await _context.Tags
                .FirstOrDefaultAsync(t => t.TagName == updateTagDto.TagName && t.TagId != id);
            if (existingByName != null)
            {
                return Conflict(new { message = "Tên thẻ đã tồn tại." });
            }

            var existingBySlug = await _context.Tags
                .FirstOrDefaultAsync(t => t.Slug == updateTagDto.Slug && t.TagId != id);
            if (existingBySlug != null)
            {
                return Conflict(new { message = "Slug trùng với thẻ đã có sẵn." });
            }

            var before = new
            {
                existing.TagId,
                existing.TagName,
                existing.Slug
            };

            existing.TagName = updateTagDto.TagName;
            existing.Slug = updateTagDto.Slug;

            _context.Tags.Update(existing);
            await _context.SaveChangesAsync();

            var after = new
            {
                existing.TagId,
                existing.TagName,
                existing.Slug
            };

            // 🔐 AUDIT LOG – UPDATE TAG
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Update",
                entityType: "Tag",
                entityId: existing.TagId.ToString(),
                before: before,
                after: after
            );

            return NoContent();
        }

        /**
         * Summary: Delete a tag by id.
         * Route: DELETE /api/tags/{id}
         * Params: id (Guid)
         * Returns: 204 No Content, 404 if not found
         * ✅ Có audit log khi xoá thành công.
         */
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTag(Guid id)
        {
            var existingTag = await _context.Tags
                .FirstOrDefaultAsync(t => t.TagId == id);
            if (existingTag == null)
            {
                return NotFound();
            }

            var before = new
            {
                existingTag.TagId,
                existingTag.TagName,
                existingTag.Slug
            };

            _context.Tags.Remove(existingTag);
            await _context.SaveChangesAsync();

            // 🔐 AUDIT LOG – DELETE TAG
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Delete",
                entityType: "Tag",
                entityId: existingTag.TagId.ToString(),
                before: before,
                after: null
            );

            return NoContent();
        }
    }
}
