/**
 * File: PostsController.cs
 * Author: HieuNDHE173169
 * Created: 21/10/2025
 * Version: 1.0.0
 * Purpose: Manage blog posts (CRUD). Handles post creation, updates, and deletion
 *          with proper relationships to authors, post types, tags, and post images.
 * Endpoints:
 *   - GET    /api/posts              : List all posts
 *   - GET    /api/posts/{id}         : Get a post by id
 *   - POST   /api/posts              : Create a post
 *   - PUT    /api/posts/{id}         : Update a post
 *   - DELETE /api/posts/{id}         : Delete a post and its images
 *   - GET    /api/posts/{id}/images  : Get images for a post
 *   - POST   /api/posts/{id}/images  : Add image to a post
 *   - DELETE /api/posts/{id}/images/{imageId} : Delete a post image
 *   - POST   /api/posts/upload       : Upload an image file
 *   - GET    /api/posts/posttypes     : List all post types
 *   - PUT    /api/posts/{id}/images/{imageId} : Update a post image
 */

using Microsoft.AspNetCore.Mvc;
using Keytietkiem.DTOs.Post;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Keytietkiem.Utils;
using System.Security.Claims;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.Utils.Constants;

namespace Keytietkiem.Controllers
{
    /// <summary>
    /// Controller for managing blog posts (CRUD operations).
    /// Handles post creation, updates, and deletion with proper relationships to authors, post types, tags, and post images.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly KeytietkiemDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostsController"/> class.
        /// </summary>
        /// <param name="postService">The post service for business logic operations.</param>
        /// <param name="keytietkiemDbContext">The database context.</param>
        /// <exception cref="ArgumentNullException">Thrown when postService is null.</exception>
        public PostsController(
            IPostService postService, KeytietkiemDbContext keytietkiemDbContext)
        {
            _postService = postService ?? throw new ArgumentNullException(nameof(postService));
            _context = keytietkiemDbContext;
        }

        /// <summary>
        /// Retrieves all posts.
        /// </summary>
        /// <param name="excludeStaticContent">If true, excludes static content posts.</param>
        /// <returns>200 OK with list of posts.</returns>
        [HttpGet]
        [AllowAnonymous]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetPosts([FromQuery] bool excludeStaticContent = false)
        {
            try
            {
                var posts = await _postService.GetAllPostsAsync(excludeStaticContent);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a post by its identifier.
        /// </summary>
        /// <param name="id">The post identifier.</param>
        /// <returns>200 OK with post details, or 404 if not found.</returns>
        [HttpGet("{id}")]
        [AllowAnonymous]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetPostById(Guid id)
        {
            try
            {
                var post = await _postService.GetPostByIdAsync(id);
                return Ok(post);
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
        /// Creates a new post.
        /// </summary>
        /// <param name="createPostDto">The post creation data.</param>
        /// <returns>201 Created with the created post, or 400/404 on validation errors.</returns>
        [HttpPost]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostDTO createPostDto)
        {
            if (createPostDto == null)
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
                var post = await _postService.CreatePostAsync(createPostDto, actorId);
                return CreatedAtAction(nameof(GetPostById), new { id = post.PostId }, post);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing post.
        /// </summary>
        /// <param name="id">The post identifier.</param>
        /// <param name="updatePostDto">The post update data.</param>
        /// <returns>204 No Content on success, or 400/404 on errors.</returns>
        [HttpPut("{id}")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> UpdatePost(Guid id, [FromBody] UpdatePostDTO updatePostDto)
        {
            if (updatePostDto == null)
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
                await _postService.UpdatePostAsync(id, updatePostDto, actorId);
                return NoContent();
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
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
        /// Deletes a post and cascades removal of related post images.
        /// </summary>
        /// <param name="id">The post identifier.</param>
        /// <returns>204 No Content on success, or 404 if not found.</returns>
        [HttpDelete("{id}")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> DeletePost(Guid id)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.DeletePostAsync(id, actorId);
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

        /// <summary>
        /// Retrieves all post types.
        /// </summary>
        /// <returns>200 OK with list of post types.</returns>
        [HttpGet("posttypes")]
        [AllowAnonymous]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetPosttypes()
        {
            try
            {
                var postTypes = await _postService.GetAllPostTypesAsync();
            return Ok(postTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Creates a new post type.
        /// </summary>
        /// <param name="createPostTypeDto">The post type creation data.</param>
        /// <returns>201 Created with the created post type, or 400 on validation errors.</returns>
        [HttpPost("posttypes")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> CreatePosttype([FromBody] CreatePostTypeDTO createPostTypeDto)
        {
            if (createPostTypeDto == null)
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
                var postType = await _postService.CreatePostTypeAsync(createPostTypeDto, actorId);
                return CreatedAtAction(nameof(GetPosttypes), new { id = postType.PostTypeId }, postType);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing post type.
        /// </summary>
        /// <param name="id">The post type identifier.</param>
        /// <param name="updatePostTypeDto">The post type update data.</param>
        /// <returns>204 No Content on success, or 400/404 on errors.</returns>
        [HttpPut("posttypes/{id}")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> UpdatePosttype(Guid id, [FromBody] UpdatePostTypeDTO updatePostTypeDto)
        {
            if (updatePostTypeDto == null)
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
                await _postService.UpdatePostTypeAsync(id, updatePostTypeDto, actorId);
                return NoContent();
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
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
        /// Deletes a post type.
        /// </summary>
        /// <param name="id">The post type identifier.</param>
        /// <returns>204 No Content on success, or 400/404 on errors.</returns>
        [HttpDelete("posttypes/{id}")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> DeletePosttype(Guid id)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.DeletePostTypeAsync(id, actorId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a post by its slug for public viewing.
        /// </summary>
        /// <param name="slug">The post URL slug.</param>
        /// <returns>200 OK with post details, or 404 if not found.</returns>
        [HttpGet("slug/{slug}")]
        [AllowAnonymous] 
        public async Task<IActionResult> GetPostBySlug(string slug)
        {
            try
            {
                var post = await _postService.GetPostBySlugAsync(slug);
                return Ok(post);
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
        /// Retrieves related posts (same post type, excluding current post).
        /// </summary>
        /// <param name="id">The post identifier.</param>
        /// <param name="limit">Maximum number of related posts to return (default: 3).</param>
        /// <returns>200 OK with list of related posts.</returns>
        [HttpGet("{id}/related")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRelatedPosts(Guid id, [FromQuery] int limit = 3)
        {
            try
            {
                var relatedPosts = await _postService.GetRelatedPostsAsync(id, limit);
                return Ok(relatedPosts);
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
        /// Retrieves all SpecificDocumentation posts.
        /// </summary>
        /// <returns>200 OK with list of SpecificDocumentation posts.</returns>
        [HttpGet("specific-documentation")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllSpecificDocumentation()
        {
            try
            {
                var posts = await _postService.GetAllSpecificDocumentationAsync();
                return Ok(posts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a SpecificDocumentation post by its slug.
        /// </summary>
        /// <param name="slug">The post slug.</param>
        /// <returns>200 OK with post details, or 404 if not found.</returns>
        [HttpGet("specific-documentation/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSpecificDocumentationBySlug(string slug)
        {
            try
            {
                var post = await _postService.GetSpecificDocumentationBySlugAsync(slug);
                return Ok(post);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
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
        /// Creates a new SpecificDocumentation post.
        /// </summary>
        /// <param name="createDto">The post creation data (with PostTypeId set to SpecificDocumentation).</param>
        /// <returns>201 Created with post details, or 400 on errors.</returns>
        [HttpPost("specific-documentation")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> CreateSpecificDocumentation([FromBody] CreatePostDTO createDto)
        {
            if (createDto == null)
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
                var post = await _postService.CreatePostAsync(createDto, actorId);
                return CreatedAtAction(nameof(GetSpecificDocumentationBySlug), new { slug = post.Slug }, post);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates a SpecificDocumentation post.
        /// </summary>
        /// <param name="id">The post identifier.</param>
        /// <param name="updateDto">The post update data.</param>
        /// <returns>204 No Content on success, or 400/404 on errors.</returns>
        [HttpPut("specific-documentation/{id}")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> UpdateSpecificDocumentation(Guid id, [FromBody] UpdatePostDTO updateDto)
        {
            if (updateDto == null)
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
                await _postService.UpdatePostAsync(id, updateDto, actorId);
                return NoContent();
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
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
        /// Deletes a SpecificDocumentation post.
        /// </summary>
        /// <param name="id">The post identifier.</param>
        /// <returns>204 No Content on success, or 404 if not found.</returns>
        [HttpDelete("specific-documentation/{id}")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> DeleteSpecificDocumentation(Guid id)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.DeletePostAsync(id, actorId);
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