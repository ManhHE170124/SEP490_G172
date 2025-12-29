/**
 * File: PostsController.cs
 * Author: HieuNDHE173169
 * Created: 21/10/2025
 * Last Updated: 24/10/2025
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
using Keytietkiem.Models;
using Keytietkiem.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.DTOs.Post;
using Microsoft.AspNetCore.Authorization;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Keytietkiem.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly IPhotoService _photoService;
        private readonly KeytietkiemDbContext _context;
        private readonly IClock _clock;
        private readonly ILogger<PostsController> _logger;
        
        public PostsController(
            IPostService postService, 
            IPhotoService photoService,
            KeytietkiemDbContext context,
            IClock clock,
            ILogger<PostsController> logger)
        {
            _postService = postService ?? throw new ArgumentNullException(nameof(postService));
            _photoService = photoService ?? throw new ArgumentNullException(nameof(photoService));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /**
         * Summary: Retrieve all posts.
         * Route: GET /api/posts
         * Params: none
         * Returns: 200 OK with list of posts
         */
        [HttpGet]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetPosts()
        {
            try
            {
                var posts = await _postService.GetAllPostsAsync(includeRelations: true);
                var postDtos = posts.Select(MapToPostListItemDTO).ToList();
                return Ok(postDtos);
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
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetPostById(Guid id)
        {
            try
            {
                var post = await _postService.GetPostByIdAsync(id, includeRelations: true);
                if (post == null)
                {
                    return NotFound();
                }

                var postDto = MapToPostDTO(post);
                return Ok(postDto);
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

            // Validate PostType exists
            if (createPostDto.PostTypeId.HasValue)
            {
                var postType = await _postService.GetPostTypeByIdAsync(createPostDto.PostTypeId.Value);
                if (postType == null)
                {
                    return NotFound(new { message = "Danh mục bài viết không được tìm thấy." });
                }
            }

            // Validate Author exists
            if (createPostDto.AuthorId.HasValue)
            {
                var author = await _context.Users.FindAsync(new object[] { createPostDto.AuthorId.Value });
                if (author == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông tin tác giả." });
                }
            }

            // Validate Tags exist
            if (createPostDto.TagIds != null && createPostDto.TagIds.Any())
            {
                var tagCount = await _context.Tags
                    .CountAsync(t => createPostDto.TagIds.Contains(t.TagId));
                if (tagCount != createPostDto.TagIds.Count)
                {
                    return BadRequest(new { message = "Không tìm thấy thẻ nào được gán cho bài viết này." });
                }
            }

            // Check if Slug is unique
            if (await _postService.SlugExistsAsync(createPostDto.Slug, null))
            {
                return BadRequest(new { message = "Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác." });
            }

            var newPost = new Post
            {
                Title = createPostDto.Title,
                Slug = createPostDto.Slug,
                ShortDescription = createPostDto.ShortDescription,
                Content = createPostDto.Content,
                Thumbnail = createPostDto.Thumbnail,
                PostTypeId = createPostDto.PostTypeId,
                AuthorId = createPostDto.AuthorId,
                MetaTitle = createPostDto.MetaTitle,
                Status = createPostDto.Status ?? "Draft",
                ViewCount = 0,
                CreatedAt = _clock.UtcNow
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Posts.Add(newPost);
                await _context.SaveChangesAsync();

                // Add Tags
                if (createPostDto.TagIds != null && createPostDto.TagIds.Any())
                {
                    var tags = await _context.Tags
                        .Where(t => createPostDto.TagIds.Contains(t.TagId))
                        .ToListAsync();
                    newPost.Tags = tags;
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Post {PostId} created by {ActorId}", newPost.PostId, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));

                // Reload post with relations
                var createdPost = await _postService.GetPostByIdAsync(newPost.PostId, includeRelations: true);
                var postDto = MapToPostDTO(createdPost!);

                return CreatedAtAction(nameof(GetPostById), new { id = createdPost!.PostId }, postDto);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                if (ex.InnerException?.Message?.Contains("duplicate key") == true ||
                    ex.InnerException?.Message?.Contains("UNIQUE KEY") == true)
                {
                    return BadRequest(new { message = "Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác." });
                }
                throw;
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

            var existing = await _postService.GetPostByIdAsync(id, includeRelations: true);
            if (existing == null)
            {
                return NotFound();
            }

            // Validate PostType exists
            if (updatePostDto.PostTypeId.HasValue)
            {
                var postType = await _postService.GetPostTypeByIdAsync(updatePostDto.PostTypeId.Value);
                if (postType == null)
                {
                    return NotFound(new { message = "Không tìm thấy danh mục bài viết." });
                }
            }

            // Validate Tags exist
            if (updatePostDto.TagIds != null && updatePostDto.TagIds.Any())
            {
                var tagCount = await _context.Tags
                    .CountAsync(t => updatePostDto.TagIds.Contains(t.TagId));
                if (tagCount != updatePostDto.TagIds.Count)
                {
                    return BadRequest(new { message = "Không tìm thấy thẻ nào được gán cho bài viết này." });
                }
            }

            // Check if Slug is unique (excluding current post)
            if (existing.Slug != updatePostDto.Slug)
            {
                if (await _postService.SlugExistsAsync(updatePostDto.Slug, id))
                {
                    return BadRequest(new { message = "Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác." });
                }
            }

            existing.Title = updatePostDto.Title;
            existing.Slug = updatePostDto.Slug;
            existing.ShortDescription = updatePostDto.ShortDescription;
            existing.Content = updatePostDto.Content;
            existing.Thumbnail = updatePostDto.Thumbnail;
            existing.PostTypeId = updatePostDto.PostTypeId;
            existing.MetaTitle = updatePostDto.MetaTitle;
            existing.Status = updatePostDto.Status;
            existing.UpdatedAt = _clock.UtcNow;

            // Update Tags
            if (updatePostDto.TagIds != null)
            {
                var tags = await _context.Tags
                    .Where(t => updatePostDto.TagIds.Contains(t.TagId))
                    .ToListAsync();
                existing.Tags = tags;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Posts.Update(existing);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Post {PostId} updated by {ActorId}", id, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));

                return NoContent();
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                if (ex.InnerException?.Message?.Contains("duplicate key") == true ||
                    ex.InnerException?.Message?.Contains("UNIQUE KEY") == true)
                {
                    return BadRequest(new { message = "Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác." });
                }
                throw;
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
            var existingPost = await _postService.GetPostByIdAsync(id, includeRelations: false);
            if (existingPost == null)
            {
                return NotFound();
            }

            _context.Posts.Remove(existingPost);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Post {PostId} deleted by {ActorId}", id, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));

            return NoContent();
        }

        /**
         * Summary: Retrieve all post types.
         * Route: GET /api/posts/posttypes
         * Params: none
         * Returns: 200 OK with list of post types
         */
        [HttpGet("posttypes")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> GetPosttypes()
        {
            try
            {
                var postTypes = await _postService.GetAllPostTypesAsync();
                var postTypeDtos = postTypes.Select(MapToPostTypeDTO).ToList();
                return Ok(postTypeDtos);
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

            var newPostType = new PostType
            {
                PostTypeName = createPostTypeDto.PostTypeName,
                Description = createPostTypeDto.Description,
                CreatedAt = _clock.UtcNow
            };

            _context.PostTypes.Add(newPostType);
            await _context.SaveChangesAsync();

            _logger.LogInformation("PostType {PostTypeId} created by {ActorId}", newPostType.PostTypeId, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));

            var postTypeDto = MapToPostTypeDTO(newPostType);
            return CreatedAtAction(nameof(GetPosttypes), new { id = newPostType.PostTypeId }, postTypeDto);
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

            var existing = await _postService.GetPostTypeByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.PostTypeName = updatePostTypeDto.PostTypeName;
            existing.Description = updatePostTypeDto.Description;
            existing.UpdatedAt = _clock.UtcNow;

            _context.PostTypes.Update(existing);
            await _context.SaveChangesAsync();

            _logger.LogInformation("PostType {PostTypeId} updated by {ActorId}", id, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));

            return NoContent();
        }


        [HttpDelete("posttypes/{id}")]
        [Authorize]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> DeletePosttype(Guid id)
        {
            var existing = await _postService.GetPostTypeByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (await _postService.PostTypeHasPostsAsync(id))
            {
                return BadRequest("Không thể xóa danh mục này.");
            }

            _context.PostTypes.Remove(existing);
            await _context.SaveChangesAsync();

            _logger.LogInformation("PostType {PostTypeId} deleted by {ActorId}", id, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value));

            return NoContent();
        }

        /**
         * Summary: Get post by slug for public viewing
         * Route: GET /api/posts/slug/{slug}
         * Params: slug (string) - post URL slug
         * Returns: 200 OK with post detail, 404 if not found
         */
        [HttpGet("slug/{slug}")]
        [AllowAnonymous] // Public endpoint - không cần auth
        public async Task<IActionResult> GetPostBySlug(string slug)
        {
            var post = await _postService.GetPostBySlugAsync(slug, includeRelations: true);
            if (post == null)
            {
                return NotFound(new { message = "Không tìm thấy bài viết" });
            }

            // Increment view count
            if (post.Status == "Published")
            {
                post.ViewCount = (post.ViewCount ?? 0) + 1;
                await _context.SaveChangesAsync();
            }

            var postDto = MapToPostDTO(post);
            return Ok(postDto);
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
            var currentPost = await _postService.GetPostByIdAsync(id, includeRelations: false);
            if (currentPost == null)
            {
                return NotFound();
            }

            var relatedPosts = await _postService.GetRelatedPostsAsync(id, currentPost.PostTypeId, limit);
            var relatedPostDtos = relatedPosts.Select(MapToPostListItemDTO).ToList();
            return Ok(relatedPostDtos);
        }

        // ===== Helper Methods =====

        private PostDTO MapToPostDTO(Post post)
        {
            return new PostDTO
            {
                PostId = post.PostId,
                Title = post.Title,
                Slug = post.Slug,
                ShortDescription = post.ShortDescription,
                Content = post.Content,
                Thumbnail = post.Thumbnail,
                PostTypeId = post.PostTypeId,
                AuthorId = post.AuthorId,
                MetaTitle = post.MetaTitle,
                Status = post.Status,
                ViewCount = post.ViewCount,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                AuthorName = post.Author != null ? (post.Author.FullName ?? $"{post.Author.FirstName} {post.Author.LastName}".Trim()) : null,
                PostTypeName = post.PostType != null ? post.PostType.PostTypeName : null,
                Tags = post.Tags.Select(t => new TagDTO
                {
                    TagId = t.TagId,
                    TagName = t.TagName,
                    Slug = t.Slug
                }).ToList()
            };
        }

        private PostListItemDTO MapToPostListItemDTO(Post post)
        {
            return new PostListItemDTO
            {
                PostId = post.PostId,
                Title = post.Title,
                Slug = post.Slug,
                ShortDescription = post.ShortDescription,
                Thumbnail = post.Thumbnail,
                PostTypeId = post.PostTypeId,
                AuthorId = post.AuthorId,
                Status = post.Status,
                ViewCount = post.ViewCount,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                AuthorName = post.Author != null ? (post.Author.FullName ?? $"{post.Author.FirstName} {post.Author.LastName}".Trim()) : null,
                PostTypeName = post.PostType != null ? post.PostType.PostTypeName : null,
                Tags = post.Tags.Select(t => new TagDTO
                {
                    TagId = t.TagId,
                    TagName = t.TagName,
                    Slug = t.Slug,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                }).ToList()
            };
        }

        private PostTypeDTO MapToPostTypeDTO(PostType postType)
        {
            return new PostTypeDTO
            {
                PostTypeId = postType.PostTypeId,
                PostTypeName = postType.PostTypeName,
                Description = postType.Description,
                CreatedAt = postType.CreatedAt,
                UpdatedAt = postType.UpdatedAt
            };
        }
    }
}