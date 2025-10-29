/**
 * File: PostsController.cs
 * Author: HieuNDHE173169
 * Created: 21/10/2025
 * Last Updated: 24/10/2025
 * Version: 1.0.0
 * Purpose: Manage blog posts (CRUD). Handles post creation, updates, and deletion
 *          with proper relationships to authors, post types, and tags.
 * Endpoints:
 *   - GET    /api/posts              : List posts (simplified)
 *   - GET    /api/posts/{id}         : Get post by ID with full details
 *   - POST   /api/posts              : Create post with tag associations
 *   - PUT    /api/posts/{id}         : Update post and tag associations
 *   - DELETE /api/posts/{id}         : Delete post
 */

using Microsoft.AspNetCore.Mvc;
using Keytietkiem.Models;
using Keytietkiem.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;

        public PostsController(KeytietkiemDbContext context)
        {
            _context = context;
        }

        /**
        * Summary: Retrieve all posts with simplified information.
        * Route: GET /api/posts
        * Params: none
        * Returns: 200 OK with list of posts without full content
        */
        [HttpGet]
        public async Task<IActionResult> GetPosts()
        {
            var posts = await _context.Posts
                .AsNoTracking()
                .Include(p => p.Author)
                .Include(p => p.PostType)
                .Include(p => p.Tags)
                .Select(p => new PostListItemDTO
                {
                    PostId = p.PostId,
                    Title = p.Title,
                    Content = p.Content,
                    Thumbnail = p.Thumbnail,
                    PostTypeId = p.PostTypeId,
                    AuthorId = p.AuthorId,
                    Status = p.Status,
                    ViewCount = p.ViewCount,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    PublishedAt = p.PublishedAt,
                    AuthorName = p.Author.FullName,
                    PostTypeName = p.PostType.TypeName,
                    Tags = p.Tags.Select(t => new TagDTO
                    {
                        TagId = t.TagId,
                        TagName = t.TagName,
                        Slug = t.Slug,
                        CreatedAt = t.CreatedAt
                    }).ToList()
                })
                .ToListAsync();

            return Ok(posts);
        }

        /**
        * Summary: Retrieve a post by ID with full details including author, post type, and tags.
        * Route: GET /api/posts/{id}
        * Params: id (Guid) - post identifier
        * Returns: 200 OK with post with full details, 404 if not found
        */
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPostById(Guid id)
        {
            var post = await _context.Posts
                .AsNoTracking()
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
                Content = post.Content,
                Thumbnail = post.Thumbnail,
                PostTypeId = post.PostTypeId,
                AuthorId = post.AuthorId,
                Status = post.Status,
                ViewCount = post.ViewCount,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                PublishedAt = post.PublishedAt,
                AuthorName = post.Author.FullName,
                PostTypeName = post.PostType.TypeName,
                Tags = post.Tags.Select(t => new TagDTO
                {
                    TagId = t.TagId,
                    TagName = t.TagName,
                    Slug = t.Slug,
                    CreatedAt = t.CreatedAt
                }).ToList()
            };

            return Ok(postDto);
        }

        /**
        * Summary: Create a new post with tag associations.
        * Route: POST /api/posts
        * Body: CreatePostDTO createPostDto
        * Returns: 201 Created with created post, 400/404 on validation errors
        */
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostDTO createPostDto)
        {
            if (createPostDto == null || string.IsNullOrWhiteSpace(createPostDto.Title))
            {
                return BadRequest(new { message = "Post title is required." });
            }

            // Validate PostType exists
            var postType = await _context.PostTypes
                .FirstOrDefaultAsync(pt => pt.PostTypeId == createPostDto.PostTypeId);
            if (postType == null)
            {
                return BadRequest(new { message = "Invalid PostTypeId." });
            }

            // Validate Author exists
            var author = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == createPostDto.AuthorId);
            if (author == null)
            {
                return BadRequest(new { message = "Invalid AuthorId." });
            }

            // Validate Tags exist
            if (createPostDto.TagIds.Any())
            {
                var existingTags = await _context.Tags
                    .Where(t => createPostDto.TagIds.Contains(t.TagId))
                    .ToListAsync();

                if (existingTags.Count != createPostDto.TagIds.Count)
                {
                    return BadRequest(new { message = "One or more TagIds are invalid." });
                }
            }

            var newPost = new Post
            {
                PostId = Guid.NewGuid(),
                Title = createPostDto.Title,
                Content = createPostDto.Content,
                Thumbnail = createPostDto.Thumbnail,
                PostTypeId = createPostDto.PostTypeId,
                AuthorId = createPostDto.AuthorId,
                Status = createPostDto.Status,
                CreatedAt = DateTime.UtcNow,
                ViewCount = 0
            };

            if (createPostDto.Status == "Published")
            {
                newPost.PublishedAt = DateTime.UtcNow;
            }

            _context.Posts.Add(newPost);
            await _context.SaveChangesAsync();

            // Associate tags
            if (createPostDto.TagIds.Any())
            {
                var tags = await _context.Tags
                    .Where(t => createPostDto.TagIds.Contains(t.TagId))
                    .ToListAsync();

                foreach (var tag in tags)
                {
                    newPost.Tags.Add(tag);
                }
            }

            await _context.SaveChangesAsync();

            // Return created post with full details
            return await GetPostById(newPost.PostId);
        }

        /**
        * Summary: Update an existing post and its tag associations.
        * Route: PUT /api/posts/{id}
        * Params: id (Guid) - post identifier
        * Body: UpdatePostDTO updatePostDto
        * Returns: 204 No Content, 400/404 on errors
        */
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePost(Guid id, [FromBody] UpdatePostDTO updatePostDto)
        {
            if (updatePostDto == null)
            {
                return BadRequest(new { message = "Invalid post data." });
            }

            var existingPost = await _context.Posts
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (existingPost == null)
            {
                return NotFound();
            }

            // Validate PostType exists
            var postType = await _context.PostTypes
                .FirstOrDefaultAsync(pt => pt.PostTypeId == updatePostDto.PostTypeId);
            if (postType == null)
            {
                return BadRequest(new { message = "Invalid PostTypeId." });
            }

            // Validate Tags exist
            if (updatePostDto.TagIds.Any())
            {
                var existingTags = await _context.Tags
                    .Where(t => updatePostDto.TagIds.Contains(t.TagId))
                    .ToListAsync();

                if (existingTags.Count != updatePostDto.TagIds.Count)
                {
                    return BadRequest(new { message = "One or more TagIds are invalid." });
                }
            }

            // Update post properties
            existingPost.Title = updatePostDto.Title;
            existingPost.Content = updatePostDto.Content;
            existingPost.Thumbnail = updatePostDto.Thumbnail;
            existingPost.PostTypeId = updatePostDto.PostTypeId;
            existingPost.Status = updatePostDto.Status;
            existingPost.UpdatedAt = DateTime.UtcNow;

            if (updatePostDto.Status == "Published" && existingPost.PublishedAt == null)
            {
                existingPost.PublishedAt = DateTime.UtcNow;
            }

            // Update tag associations
            existingPost.Tags.Clear();
            if (updatePostDto.TagIds.Any())
            {
                var tags = await _context.Tags
                    .Where(t => updatePostDto.TagIds.Contains(t.TagId))
                    .ToListAsync();

                foreach (var tag in tags)
                {
                    existingPost.Tags.Add(tag);
                }
            }

            _context.Posts.Update(existingPost);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /**
        * Summary: Delete a post by ID.
        * Route: DELETE /api/posts/{id}
        * Params: id (Guid) - post identifier
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
        public async Task<IActionResult> GetPostTypes()
        {
            var postTypes = await _context.PostTypes
                .AsNoTracking()
                .Select(pt => new PostTypeDTO
                {
                    PostTypeId = pt.PostTypeId,
                    TypeName = pt.TypeName,
                    Slug = pt.Slug,
                    Description = pt.Description,
                    CreatedAt = pt.CreatedAt,
                    UpdatedAt = pt.UpdatedAt
                })
                .ToListAsync();

            return Ok(postTypes);
        }
    }
}
