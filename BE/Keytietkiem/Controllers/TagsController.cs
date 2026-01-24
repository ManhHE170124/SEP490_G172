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
using Keytietkiem.DTOs.Post;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using System.Security.Claims;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.Utils.Constants;

namespace Keytietkiem.Controllers
{
    /// <summary>
    /// Controller for managing tags (CRUD operations).
    /// Ensures unique tag names and slugs, and maintains referential integrity on updates/deletions.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TagsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly KeytietkiemDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="TagsController"/> class.
        /// </summary>
        /// <param name="postService">The post service for business logic operations.</param>
        /// <param name="keytietkiemDbContext">The database context.</param>
        /// <exception cref="ArgumentNullException">Thrown when postService is null.</exception>
        public TagsController(IPostService postService, KeytietkiemDbContext keytietkiemDbContext)
        {
            _postService = postService ?? throw new ArgumentNullException(nameof(postService));
            _context = keytietkiemDbContext;
        }

        /// <summary>
        /// Retrieves all tags.
        /// </summary>
        /// <returns>200 OK with list of tags.</returns>
        [HttpGet]
        [AllowAnonymous]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetTags()
        {
            try
            {
                var tags = await _postService.GetAllTagsAsync();
                return Ok(tags);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a tag by its identifier.
        /// </summary>
        /// <param name="id">The tag identifier.</param>
        /// <returns>200 OK with tag details, or 404 if not found.</returns>
        [HttpGet("{id}")]
        [AllowAnonymous]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetTagById(Guid id)
        {
            try
            {
                var tag = await _postService.GetTagByIdAsync(id);
                return Ok(tag);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Creates a new tag.
        /// </summary>
        /// <param name="createTagDto">The tag creation data.</param>
        /// <returns>201 Created with the created tag, or 400/409 on validation errors.</returns>
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

            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var tag = await _postService.CreateTagAsync(createTagDto, actorId);
                return CreatedAtAction(nameof(GetTagById), new { id = tag.TagId }, tag);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing tag.
        /// </summary>
        /// <param name="id">The tag identifier.</param>
        /// <param name="updateTagDto">The tag update data.</param>
        /// <returns>204 No Content on success, or 400/404/409 on errors.</returns>
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

            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.UpdateTagAsync(id, updateTagDto, actorId);
                return NoContent();
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("tồn tại") || ex.Message.Contains("trùng"))
                {
                    return Conflict(new { message = ex.Message });
                }
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Deletes a tag.
        /// </summary>
        /// <param name="id">The tag identifier.</param>
        /// <returns>204 No Content on success, or 404 if not found.</returns>
        [HttpDelete("{id}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> DeleteTag(Guid id)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.DeleteTagAsync(id, actorId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }


    }
}
