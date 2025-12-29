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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.DTOs.Post;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Keytietkiem.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TagsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly KeytietkiemDbContext _context;
        private readonly IClock _clock;
        private readonly ILogger<TagsController> _logger;

        public TagsController(
            IPostService postService,
            KeytietkiemDbContext context,
            IClock clock,
            ILogger<TagsController> logger)
        {
            _postService = postService ?? throw new ArgumentNullException(nameof(postService));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /**
         * Summary: Retrieve all tags.
         * Route: GET /api/tags
         * Params: none
         * Returns: 200 OK with list of tags
         */
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetTags()
        {
            try
            {
                var tags = await _postService.GetAllTagsAsync();
                var tagDtos = tags.Select(MapToTagDTO).ToList();
                return Ok(tagDtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /**
         * Summary: Retrieve a tag by id.
         * Route: GET /api/tags/{id}
         * Params: id (Guid) - tag identifier
         * Returns: 200 OK with tag, 404 if not found
         */
        [HttpGet("{id}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetTagById(Guid id)
        {
            var tag = await _postService.GetTagByIdAsync(id);
            if (tag == null)
            {
                return NotFound();
            }

            var tagDto = MapToTagDTO(tag);
            return Ok(tagDto);
        }

        /**
         * Summary: Create a new tag.
         * Route: POST /api/tags
         * Body: CreateTagDTO createTagDto
         * Returns: 201 Created with created tag, 400/409 on validation errors
         */
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
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

            if (await _postService.TagNameExistsAsync(createTagDto.TagName, null))
            {
                return Conflict(new { message = "Tên thẻ đã tồn tại." });
            }

            if (await _postService.TagSlugExistsAsync(createTagDto.Slug, null))
            {
                return Conflict(new { message = "Slug đã tồn tại." });
            }

            var newTag = new Tag
            {
                TagName = createTagDto.TagName,
                Slug = createTagDto.Slug,
                CreatedAt = _clock.UtcNow
            };

            _context.Tags.Add(newTag);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag {TagId} created by {ActorId}", newTag.TagId, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));

            var tagDto = MapToTagDTO(newTag);
            return CreatedAtAction(nameof(GetTagById), new { id = newTag.TagId }, tagDto);
        }

        /**
         * Summary: Update an existing tag by id.
         * Route: PUT /api/tags/{id}
         * Params: id (Guid)
         * Body: UpdateTagDTO updateTagDto
         * Returns: 204 No Content, 400/404/409 on errors
         */
        [HttpPut("{id}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
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

            var existing = await _postService.GetTagByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (await _postService.TagNameExistsAsync(updateTagDto.TagName, id))
            {
                return Conflict(new { message = "Tên thẻ đã tồn tại." });
            }

            if (await _postService.TagSlugExistsAsync(updateTagDto.Slug, id))
            {
                return Conflict(new { message = "Slug trùng với thẻ đã có sẵn." });
            }

            existing.TagName = updateTagDto.TagName;
            existing.Slug = updateTagDto.Slug;
            existing.UpdatedAt = _clock.UtcNow;

            _context.Tags.Update(existing);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag {TagId} updated by {ActorId}", id, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));

            return NoContent();
        }

        /**
         * Summary: Delete a tag by id.
         * Route: DELETE /api/tags/{id}
         * Params: id (Guid)
         * Returns: 204 No Content, 404 if not found
         */
        [HttpDelete("{id}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> DeleteTag(Guid id)
        {
            var existingTag = await _postService.GetTagByIdAsync(id);
            if (existingTag == null)
            {
                return NotFound();
            }

            _context.Tags.Remove(existingTag);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag {TagId} deleted by {ActorId}", id, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));

            return NoContent();
        }

        // ===== Helper Methods =====

        private TagDTO MapToTagDTO(Tag tag)
        {
            return new TagDTO
            {
                TagId = tag.TagId,
                TagName = tag.TagName,
                Slug = tag.Slug,
                CreatedAt = tag.CreatedAt,
                UpdatedAt = tag.UpdatedAt
            };
        }
    }
}
