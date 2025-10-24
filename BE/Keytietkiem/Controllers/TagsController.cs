///**
// * File: TagsController.cs
// * Author: HieuNDHE173169
// * Created: 21/10/2025
// * Last Updated: 24/10/2025
// * Version: 1.0.0
// * Purpose: Manage tags (CRUD). Ensures unique tag names and slugs,
// *          and maintains referential integrity on updates/deletions.
// * Endpoints:
// *   - GET    /api/tags              : List tags
// *   - GET    /api/tags/{id}         : Get tag by ID
// *   - POST   /api/tags              : Create tag
// *   - PUT    /api/tags/{id}         : Update tag
// *   - DELETE /api/tags/{id}         : Delete tag
// */

//using Microsoft.AspNetCore.Mvc;
//using Keytietkiem.Models;
//using Keytietkiem.DTOs;
//using Microsoft.EntityFrameworkCore;

//namespace Keytietkiem.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class TagsController : ControllerBase
//    {
//        private readonly KeytietkiemDbContext _context;

//        public TagsController(KeytietkiemDbContext context)
//        {
//            _context = context;
//        }

//        /**
//        * Summary: Retrieve all tags.
//        * Route: GET /api/tags
//        * Params: none
//        * Returns: 200 OK with list of tags
//        */
//        [HttpGet]
//        public async Task<IActionResult> GetTagsAsync()
//        {
//            var tags = await _context.Tags
//                .Select(t => new TagDTO
//                {
//                    TagId = t.TagId,
//                    TagName = t.TagName,
//                    Slug = t.Slug,
//                    CreatedAt = t.CreatedAt
//                })
//                .ToListAsync();

//            return Ok(tags);
//        }

//        /**
//        * Summary: Retrieve a tag by ID.
//        * Route: GET /api/tags/{id}
//        * Params: id (int) - tag identifier
//        * Returns: 200 OK with tag, 404 if not found
//        */
//        [HttpGet("{id}")]
//        public async Task<IActionResult> GetTagByIdAsync(int id)
//        {
//            var tag = await _context.Tags
//                .FirstOrDefaultAsync(t => t.TagId == id);

//            if (tag == null)
//            {
//                return NotFound();
//            }

//            var tagDto = new TagDTO
//            {
//                TagId = tag.TagId,
//                TagName = tag.TagName,
//                Slug = tag.Slug,
//                CreatedAt = tag.CreatedAt
//            };

//            return Ok(tagDto);
//        }

//        /**
//        * Summary: Create a new tag.
//        * Route: POST /api/tags
//        * Body: CreateTagDTO createTagDto
//        * Returns: 201 Created with created tag, 400/409 on validation errors
//        */
//        [HttpPost]
//        public async Task<IActionResult> CreateTagAsync([FromBody] CreateTagDTO createTagDto)
//        {
//            if (createTagDto == null || string.IsNullOrWhiteSpace(createTagDto.TagName))
//            {
//                return BadRequest("Tag name is required.");
//            }

//            if (string.IsNullOrWhiteSpace(createTagDto.Slug))
//            {
//                return BadRequest("Slug is required.");
//            }

//            // Check for duplicate tag name
//            var existingByName = await _context.Tags
//                .FirstOrDefaultAsync(t => t.TagName == createTagDto.TagName);
//            if (existingByName != null)
//            {
//                return Conflict(new { message = "Tag name already exists." });
//            }

//            // Check for duplicate slug
//            var existingBySlug = await _context.Tags
//                .FirstOrDefaultAsync(t => t.Slug == createTagDto.Slug);
//            if (existingBySlug != null)
//            {
//                return Conflict(new { message = "Slug already exists." });
//            }

//            var newTag = new Tag
//            {
//                TagName = createTagDto.TagName,
//                Slug = createTagDto.Slug,
//                CreatedAt = DateTime.UtcNow
//            };

//            _context.Tags.Add(newTag);
//            await _context.SaveChangesAsync();

//            var tagDto = new TagDTO
//            {
//                TagId = newTag.TagId,
//                TagName = newTag.TagName,
//                Slug = newTag.Slug,
//                CreatedAt = newTag.CreatedAt
//            };

//            return CreatedAtAction(nameof(GetTagByIdAsync), new { id = newTag.TagId }, tagDto);
//        }

//        /**
//        * Summary: Update an existing tag by ID.
//        * Route: PUT /api/tags/{id}
//        * Params: id (int) - tag identifier
//        * Body: UpdateTagDTO updateTagDto
//        * Returns: 204 No Content, 400/404/409 on errors
//        */
//        [HttpPut("{id}")]
//        public async Task<IActionResult> UpdateTagAsync(int id, [FromBody] UpdateTagDTO updateTagDto)
//        {
//            if (updateTagDto == null)
//            {
//                return BadRequest("Invalid tag data.");
//            }

//            if (string.IsNullOrWhiteSpace(updateTagDto.TagName))
//            {
//                return BadRequest("Tag name is required.");
//            }

//            if (string.IsNullOrWhiteSpace(updateTagDto.Slug))
//            {
//                return BadRequest("Slug is required.");
//            }

//            var existingTag = await _context.Tags
//                .FirstOrDefaultAsync(t => t.TagId == id);

//            if (existingTag == null)
//            {
//                return NotFound();
//            }

//            // Check for duplicate tag name (excluding current tag)
//            var existingByName = await _context.Tags
//                .FirstOrDefaultAsync(t => t.TagName == updateTagDto.TagName && t.TagId != id);
//            if (existingByName != null)
//            {
//                return Conflict(new { message = "Tag name already exists." });
//            }

//            // Check for duplicate slug (excluding current tag)
//            var existingBySlug = await _context.Tags
//                .FirstOrDefaultAsync(t => t.Slug == updateTagDto.Slug && t.TagId != id);
//            if (existingBySlug != null)
//            {
//                return Conflict(new { message = "Slug already exists." });
//            }

//            existingTag.TagName = updateTagDto.TagName;
//            existingTag.Slug = updateTagDto.Slug;

//            _context.Tags.Update(existingTag);
//            await _context.SaveChangesAsync();

//            return NoContent();
//        }

//        /**
//        * Summary: Delete a tag by ID.
//        * Route: DELETE /api/tags/{id}
//        * Params: id (int) - tag identifier
//        * Returns: 204 No Content, 404 if not found
//        */
//        [HttpDelete("{id}")]
//        public async Task<IActionResult> DeleteTagAsync(int id)
//        {
//            var existingTag = await _context.Tags
//                .FirstOrDefaultAsync(t => t.TagId == id);

//            if (existingTag == null)
//            {
//                return NotFound();
//            }

//            _context.Tags.Remove(existingTag);
//            await _context.SaveChangesAsync();

//            return NoContent();
//        }
//    }
//}
