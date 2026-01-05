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
using Keytietkiem.Constants;
using System.Security.Claims;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        
        public PostsController(
            IPostService postService)
        {
            _postService = postService ?? throw new ArgumentNullException(nameof(postService));
        }

        /**
         * Summary: Retrieve all posts.
         * Route: GET /api/posts
         * Params: excludeStaticContent (bool, optional) - If true, excludes static content posts
         * Returns: 200 OK with list of posts
         */
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

        /**
         * Summary: Retrieve a post by id.
         * Route: GET /api/posts/{id}
         * Params: id (Guid) - post identifier
         * Returns: 200 OK with post, 404 if not found
         */
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

        /**
         * Summary: Create a new post.
         * Route: POST /api/posts
         * Body: CreatePostDTO createPostDto
         * Returns: 201 Created with created post, 400/404 on validation errors
         */
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

        /**
         * Summary: Update an existing post by id.
         * Route: PUT /api/posts/{id}
         * Params: id (Guid)
         * Body: UpdatePostDTO updatePostDto
         * Returns: 204 No Content, 400/404 on errors
         */
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

        /**
         * Summary: Delete a post by id and cascade remove related post images.
         * Route: DELETE /api/posts/{id}
         * Params: id (Guid)
         * Returns: 204 No Content, 404 if not found
         */
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

        /**
         * Summary: Retrieve all post types.
         * Route: GET /api/posts/posttypes
         * Params: none
         * Returns: 200 OK with list of post types
         */
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

        /**
         * Summary: Get post by slug for public viewing
         * Route: GET /api/posts/slug/{slug}
         * Params: slug (string) - post URL slug
         * Returns: 200 OK with post detail, 404 if not found
         */
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

        /**
         * Summary: Get related posts (same postType, exclude current)
         * Route: GET /api/posts/{id}/related
         * Params: id (Guid), limit (int, optional, default 3)
         * Returns: 200 OK with list of related posts
         */
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

        /**
         * Summary: Create static content (Policy, UserGuide, AboutUs)
         * Route: POST /api/posts/static
         * Body: CreateStaticContentDTO
         * Returns: 201 Created with created post, 400 on validation errors
         */
        [HttpPost("static")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> CreateStaticContent([FromBody] CreateStaticContentDTO createDto)
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
                var post = await _postService.CreateStaticContentAsync(createDto, actorId);
                return CreatedAtAction(nameof(GetStaticContent), new { type = createDto.Slug }, post);
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

        /**
         * Summary: Update static content (Policy, UserGuide, AboutUs)
         * Route: PUT /api/posts/static/{id}
         * Params: id (Guid) - post identifier
         * Body: UpdateStaticContentDTO
         * Returns: 204 No Content, 400/404 on errors
         */
        [HttpPut("static/{id}")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> UpdateStaticContent(Guid id, [FromBody] UpdateStaticContentDTO updateDto)
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
                await _postService.UpdateStaticContentAsync(id, updateDto, actorId);
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

        /**
         * Summary: Get static content by type (policy, user-guide, about-us)
         * Route: GET /api/posts/static/{type}
         * Params: type (string) - static content type (policy, user-guide, about-us)
         * Returns: 200 OK with post detail, 404 if not found
         */
        [HttpGet("static/{type}")]
        [AllowAnonymous] 
        public async Task<IActionResult> GetStaticContent(string type)
        {
            try
            {
                var post = await _postService.GetStaticContentAsync(type);
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


        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicPosts()
        {
            var posts = await _context.Posts
                .AsNoTracking()
                .Include(p => p.Author)
                .Include(p => p.PostType)
                .Include(p => p.Tags)
                .Where(p => p.Status == "Published")
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PostListItemDTO
                {
                    PostId = p.PostId,
                    Title = p.Title,
                    Slug = p.Slug,
                    ShortDescription = p.ShortDescription,
                    Thumbnail = p.Thumbnail,
                    PostTypeId = p.PostTypeId,
                    AuthorId = p.AuthorId,
                    Status = p.Status,
                    ViewCount = p.ViewCount,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    AuthorName = p.Author != null
                        ? (p.Author.FullName ?? $"{p.Author.FirstName} {p.Author.LastName}".Trim())
                        : null,
                    PostTypeName = p.PostType != null ? p.PostType.PostTypeName : null,
                    Tags = p.Tags.Select(t => new TagDTO
                    {
                        TagId = t.TagId,
                        TagName = t.TagName,
                        Slug = t.Slug,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt
                    }).ToList()
                })
                .ToListAsync();

            return Ok(posts);
        }

        [HttpGet("posttypes/public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicPostTypes()
        {
            var postTypes = await _context.PostTypes
                .AsNoTracking()
                .OrderBy(pt => pt.PostTypeName)
                .Select(pt => new PostTypeDTO
                {
                    PostTypeId = pt.PostTypeId,
                    PostTypeName = pt.PostTypeName,
                    Description = pt.Description,
                    CreatedAt = pt.CreatedAt,
                    UpdatedAt = pt.UpdatedAt
                })
                .ToListAsync();

            return Ok(postTypes);
        }


    }
}