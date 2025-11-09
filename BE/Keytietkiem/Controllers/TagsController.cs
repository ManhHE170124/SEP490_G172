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
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.DTOs.Post;

namespace Keytietkiem.Controllers
{
   [Route("api/[controller]")]
   [ApiController]
   public class TagsController : ControllerBase
   {
       private readonly KeytietkiemDbContext _context;

       public TagsController(KeytietkiemDbContext context)
       {
           _context = context;
       }

       /**
        * Summary: Retrieve all tags.
        * Route: GET /api/tags
        * Params: none
        * Returns: 200 OK with list of tags
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
        */
       [HttpPost]
       public async Task<IActionResult> CreateTag([FromBody] CreateTagDTO createTagDto)
       {
           if (createTagDto == null || string.IsNullOrWhiteSpace(createTagDto.TagName))
           {
               return BadRequest("Tag name is required.");
           }

           if (string.IsNullOrWhiteSpace(createTagDto.Slug))
           {
               return BadRequest("Slug is required.");
           }

           var existingByName = await _context.Tags
               .FirstOrDefaultAsync(t => t.TagName == createTagDto.TagName);
           if (existingByName != null)
           {
               return Conflict(new { message = "Tag name already exists." });
           }

           var existingBySlug = await _context.Tags
               .FirstOrDefaultAsync(t => t.Slug == createTagDto.Slug);
           if (existingBySlug != null)
           {
               return Conflict(new { message = "Slug already exists." });
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
       public async Task<IActionResult> UpdateTag(Guid id, [FromBody] UpdateTagDTO updateTagDto)
       {
           if (updateTagDto == null)
           {
               return BadRequest("Invalid tag data.");
           }

           if (string.IsNullOrWhiteSpace(updateTagDto.TagName))
           {
               return BadRequest("Tên th? không ???c ?? tr?ng.");
           }

           if (string.IsNullOrWhiteSpace(updateTagDto.Slug))
           {
               return BadRequest("Slug không ???c ?? tr?ng.");
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
               return Conflict(new { message = "Tên th? ?ã t?n t?i." });
           }

           var existingBySlug = await _context.Tags
               .FirstOrDefaultAsync(t => t.Slug == updateTagDto.Slug && t.TagId != id);
           if (existingBySlug != null)
           {
               return Conflict(new { message = "Slug trùng v?i th? ?ã có s?n." });
           }

           existing.TagName = updateTagDto.TagName;
           existing.Slug = updateTagDto.Slug;

           _context.Tags.Update(existing);
           await _context.SaveChangesAsync();

           return NoContent();
       }

       /**
        * Summary: Delete a tag by id.
        * Route: DELETE /api/tags/{id}
        * Params: id (Guid)
        * Returns: 204 No Content, 404 if not found
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

           _context.Tags.Remove(existingTag);
           await _context.SaveChangesAsync();

           return NoContent();
       }
   }
}
