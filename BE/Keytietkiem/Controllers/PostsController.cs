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
using Keytietkiem.DTOs.Post;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IPhotoService _photoService;
        public PostsController(KeytietkiemDbContext context, IPhotoService photoService)
        {
            _context = context;
            _photoService = photoService;
        }

        /**
         * Summary: Retrieve all posts.
         * Route: GET /api/posts
         * Params: none
         * Returns: 200 OK with list of posts
         */
        [HttpGet]
        public async Task<IActionResult> GetPosts()
        {
            var posts = await _context.Posts
                .Include(p => p.Author)
                .Include(p => p.PostType)
                .Include(p => p.Tags)
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
                    AuthorName = p.Author != null ? (p.Author.FullName ?? $"{p.Author.FirstName} {p.Author.LastName}".Trim()) : null,
                    PostTypeName = p.PostType != null ? p.PostType.PostTypeName : null,
                    Tags = p.Tags.Select(t => new TagDTO
                    {
                        TagId = t.TagId,
                        TagName = t.TagName,
                        Slug = t.Slug
                    }).ToList()
                })
                .ToListAsync();
            return Ok(posts);
        }

        /**
         * Summary: Retrieve a post by id.
         * Route: GET /api/posts/{id}
         * Params: id (Guid) - post identifier
         * Returns: 200 OK with post, 404 if not found
         */
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPostById(Guid id)
        {
            var post = await _context.Posts
                .Include(p => p.Author)
                .Include(p => p.PostType)
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound();
            }

            var postDto = new PostDTO
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
                MetaDescription = post.MetaDescription,
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

            return Ok(postDto);
        }

        /**
         * Summary: Create a new post.
         * Route: POST /api/posts
         * Body: CreatePostDTO createPostDto
         * Returns: 201 Created with created post, 400/404 on validation errors
         */
        [HttpPost]
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
                var postType = await _context.PostTypes
                    .FirstOrDefaultAsync(pt => pt.PostTypeId == createPostDto.PostTypeId.Value);
                if (postType == null)
                {
                    return NotFound(new { message = "Danh mục bài viết không được tìm thấy." });
                }
            }

            // Validate Author exists
            if (createPostDto.AuthorId.HasValue)
            {
                var author = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserId == createPostDto.AuthorId.Value);
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
                MetaDescription = createPostDto.MetaDescription,
                Status = createPostDto.Status ?? "Draft",
                ViewCount = 0,
                CreatedAt = DateTime.Now
            };

            _context.Posts.Add(newPost);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Kiểm tra unique constraint violation trên Slug
                if (
                    ex.InnerException?.Message?.Contains("duplicate key") == true ||
                    ex.InnerException?.Message?.Contains("UNIQUE KEY") == true)
                {
                    return BadRequest(new { message = "Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác." });
                }
                throw; // Re-throw nếu không phải lỗi unique constraint
            }

            // Add Tags
            if (createPostDto.TagIds != null && createPostDto.TagIds.Any())
            {
                var tags = await _context.Tags
                    .Where(t => createPostDto.TagIds.Contains(t.TagId))
                    .ToListAsync();
                newPost.Tags = tags;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    // Kiểm tra unique constraint violation trên Slug
                    if (
                        ex.InnerException?.Message?.Contains("duplicate key") == true ||
                        ex.InnerException?.Message?.Contains("UNIQUE KEY") == true)
                    {
                        return BadRequest(new { message = "Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác." });
                    }
                    throw; // Re-throw nếu không phải lỗi unique constraint
                }
            }

            // Reload post with relations
            var createdPost = await _context.Posts
                .Include(p => p.Author)
                .Include(p => p.PostType)
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.PostId == newPost.PostId);

            var postDto = new PostDTO
            {
                PostId = createdPost!.PostId,
                Title = createdPost.Title,
                Slug = createdPost.Slug,
                ShortDescription = createdPost.ShortDescription,
                Content = createdPost.Content,
                Thumbnail = createdPost.Thumbnail,
                PostTypeId = createdPost.PostTypeId,
                AuthorId = createdPost.AuthorId,
                MetaTitle = createdPost.MetaTitle,
                MetaDescription = createdPost.MetaDescription,
                Status = createdPost.Status,
                ViewCount = createdPost.ViewCount,
                CreatedAt = createdPost.CreatedAt,
                UpdatedAt = createdPost.UpdatedAt,
                AuthorName = createdPost.Author != null ? (createdPost.Author.FullName ?? $"{createdPost.Author.FirstName} {createdPost.Author.LastName}".Trim()) : null,
                PostTypeName = createdPost.PostType != null ? createdPost.PostType.PostTypeName : null,
                Tags = createdPost.Tags.Select(t => new TagDTO
                {
                    TagId = t.TagId,
                    TagName = t.TagName,
                    Slug = t.Slug
                }).ToList()
            };

            return CreatedAtAction(nameof(GetPostById), new { id = createdPost.PostId }, postDto);
        }

        /**
         * Summary: Update an existing post by id.
         * Route: PUT /api/posts/{id}
         * Params: id (Guid)
         * Body: UpdatePostDTO updatePostDto
         * Returns: 204 No Content, 400/404 on errors
         */
        [HttpPut("{id}")]
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

            var existing = await _context.Posts
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (existing == null)
            {
                return NotFound();
            }

            // Validate PostType exists
            if (updatePostDto.PostTypeId.HasValue)
            {
                var postType = await _context.PostTypes
                    .FirstOrDefaultAsync(pt => pt.PostTypeId == updatePostDto.PostTypeId.Value);
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

            existing.Title = updatePostDto.Title;
            existing.Slug = updatePostDto.Slug;
            existing.ShortDescription = updatePostDto.ShortDescription;
            existing.Content = updatePostDto.Content;
            existing.Thumbnail = updatePostDto.Thumbnail;
            existing.PostTypeId = updatePostDto.PostTypeId;
            existing.MetaTitle = updatePostDto.MetaTitle;
            existing.MetaDescription = updatePostDto.MetaDescription;
            existing.Status = updatePostDto.Status;
            existing.UpdatedAt = DateTime.Now;

            // Update Tags
            if (updatePostDto.TagIds != null)
            {
                var tags = await _context.Tags
                    .Where(t => updatePostDto.TagIds.Contains(t.TagId))
                    .ToListAsync();
                existing.Tags = tags;
            }

            _context.Posts.Update(existing);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Kiểm tra unique constraint violation trên Slug
                if (
                    ex.InnerException?.Message?.Contains("duplicate key") == true ||
                    ex.InnerException?.Message?.Contains("UNIQUE KEY") == true)
                {
                    return BadRequest(new { message = "Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác." });
                }
                throw; // Re-throw nếu không phải lỗi unique constraint
            }

            return NoContent();
        }

        /**
         * Summary: Delete a post by id and cascade remove related post images.
         * Route: DELETE /api/posts/{id}
         * Params: id (Guid)
         * Returns: 204 No Content, 404 if not found
         */
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(Guid id)
        {
            var existingPost = await _context.Posts

                .FirstOrDefaultAsync(p => p.PostId == id);

            if (existingPost == null)
            {
                return NotFound();
            }

            _context.Posts.Remove(existingPost);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /**
         * Summary: Retrieve all post types.
         * Route: GET /api/posts/posttypes
         * Params: none
         * Returns: 200 OK with list of post types
         */
        [HttpGet("posttypes")]
        public async Task<IActionResult> GetPosttypes()
        {
            var postTypes = await _context.PostTypes
                .Select(pt => new PostTypeDTO
                {
                    PostTypeId = pt.PostTypeId,
                    PostTypeName = pt.PostTypeName,
                    Slug = pt.Slug,
                    Description = pt.Description
                })
                .ToListAsync();
            return Ok(postTypes);
        }

        [HttpPost("posttypes")]
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
                Slug = createPostTypeDto.Slug,
                CreatedAt = DateTime.Now
            };
            _context.PostTypes.Add(newPostType);
            await _context.SaveChangesAsync();
            var postTypeDto = new PostTypeDTO
            {
                PostTypeId = newPostType.PostTypeId,
                PostTypeName = newPostType.PostTypeName,
                Slug = newPostType.Slug,
                Description = newPostType.Description
            };
            return CreatedAtAction(nameof(GetPosttypes), new { id = newPostType.PostTypeId }, postTypeDto);
        }

        [HttpPut("posttypes/{id}")]
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
            var existing = await _context.PostTypes
                .FirstOrDefaultAsync(pt => pt.PostTypeId == id);
            if (existing == null)
            {
                return NotFound();
            }
            existing.PostTypeName = updatePostTypeDto.PostTypeName;
            existing.Description = updatePostTypeDto.Description;
            existing.Slug = updatePostTypeDto.Slug;
            _context.PostTypes.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();

        }


        [HttpDelete("posttypes/{id}")]
        public async Task<IActionResult> DeletePosttype(Guid id)
        {
            var existing = await _context.PostTypes
                .Include(pt => pt.Posts)
                .FirstOrDefaultAsync(pt => pt.PostTypeId == id);
            if (existing == null)
            {
                return NotFound();
            }
            if (existing.Posts != null && existing.Posts.Any())
            {
                return BadRequest("Không thể xóa danh mục này.");
            }
            _context.PostTypes.Remove(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}