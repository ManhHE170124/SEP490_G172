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
 */

using Microsoft.AspNetCore.Mvc;
using Keytietkiem.Models;
using Keytietkiem.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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
         * Summary: Generate slug from title.
         * @Params: title (string)
         * @Returns: slug (string)
         */
        private static string GenerateSlug(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            string slug = title.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", " ").Trim();
            slug = slug.Substring(0, slug.Length <= 100 ? slug.Length : 100).Trim();
            slug = Regex.Replace(slug, @"\s", "-");

            return slug;
        }

        /**
         * Summary: Generate unique slug from title.
         * @Params: title (string)
         * @Returns: unique slug (string)
         */
        private async Task<string> GenerateUniqueSlugAsync(string title)
        {
            string baseSlug = GenerateSlug(title);
            string slug = baseSlug;
            int counter = 1;

            while (await _context.Posts.AnyAsync(p => p.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
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
                .Include(p => p.PostImages)
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
                }).ToList(),
                PostImages = post.PostImages.OrderBy(pi => pi.DisplayOrder ?? 0).Select(pi => new PostImageDTO
                {
                    ImageId = pi.ImageId,
                    PostId = pi.PostId,
                    ImageUrl = pi.ImageUrl,
                    Caption = pi.Caption,
                    DisplayOrder = pi.DisplayOrder,
                    CreatedAt = pi.CreatedAt
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
            if (createPostDto == null || string.IsNullOrWhiteSpace(createPostDto.Title))
            {
                return BadRequest("Post title is required.");
            }

            // Validate PostType exists
            if (createPostDto.PostTypeId.HasValue)
            {
                var postType = await _context.PostTypes
                    .FirstOrDefaultAsync(pt => pt.PostTypeId == createPostDto.PostTypeId.Value);
                if (postType == null)
                {
                    return NotFound(new { message = "Post type not found." });
                }
            }

            // Validate Author exists
            if (createPostDto.AuthorId.HasValue)
            {
                var author = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserId == createPostDto.AuthorId.Value);
                if (author == null)
                {
                    return NotFound(new { message = "Author not found." });
                }
            }

            // Validate Tags exist
            if (createPostDto.TagIds != null && createPostDto.TagIds.Any())
            {
                var tagCount = await _context.Tags
                    .CountAsync(t => createPostDto.TagIds.Contains(t.TagId));
                if (tagCount != createPostDto.TagIds.Count)
                {
                    return BadRequest(new { message = "One or more tags not found." });
                }
            }

            // Generate unique slug
            string slug = await GenerateUniqueSlugAsync(createPostDto.Title);

            var newPost = new Post
            {
                Title = createPostDto.Title,
                Slug = slug,
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

            // Add PostImages
            if (createPostDto.PostImages != null && createPostDto.PostImages.Any())
            {
                var postImages = createPostDto.PostImages.Select(pi => new PostImage
                {
                    PostId = newPost.PostId,
                    ImageUrl = pi.ImageUrl,
                    Caption = pi.Caption,
                    DisplayOrder = pi.DisplayOrder,
                    CreatedAt = DateTime.Now
                }).ToList();

                _context.PostImages.AddRange(postImages);
                await _context.SaveChangesAsync();
            }

            // Reload post with relations
            var createdPost = await _context.Posts
                .Include(p => p.Author)
                .Include(p => p.PostType)
                .Include(p => p.Tags)
                .Include(p => p.PostImages)
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
                }).ToList(),
                PostImages = createdPost.PostImages.OrderBy(pi => pi.DisplayOrder ?? 0).Select(pi => new PostImageDTO
                {
                    ImageId = pi.ImageId,
                    PostId = pi.PostId,
                    ImageUrl = pi.ImageUrl,
                    Caption = pi.Caption,
                    DisplayOrder = pi.DisplayOrder,
                    CreatedAt = pi.CreatedAt
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
                return BadRequest("Invalid post data.");
            }

            if (string.IsNullOrWhiteSpace(updatePostDto.Title))
            {
                return BadRequest("Post title is required.");
            }

            var existing = await _context.Posts
                .Include(p => p.Tags)
                .Include(p => p.PostImages)
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
                    return NotFound(new { message = "Post type not found." });
                }
            }

            // Validate Tags exist
            if (updatePostDto.TagIds != null && updatePostDto.TagIds.Any())
            {
                var tagCount = await _context.Tags
                    .CountAsync(t => updatePostDto.TagIds.Contains(t.TagId));
                if (tagCount != updatePostDto.TagIds.Count)
                {
                    return BadRequest(new { message = "One or more tags not found." });
                }
            }

            // Generate new slug if title changed
            if (existing.Title != updatePostDto.Title)
            {
                existing.Slug = await GenerateUniqueSlugAsync(updatePostDto.Title);
            }

            existing.Title = updatePostDto.Title;
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
            await _context.SaveChangesAsync();

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
                .Include(p => p.PostImages)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (existingPost == null)
            {
                return NotFound();
            }

            // Remove PostImages (cascade)
            if (existingPost.PostImages != null && existingPost.PostImages.Any())
            {
                _context.PostImages.RemoveRange(existingPost.PostImages);
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

        /**
         * Summary: Get images for a post.
         * Route: GET /api/posts/{id}/images
         * Params: id (Guid) - post identifier
         * Returns: 200 OK with list of post images, 404 if post not found
         */
        [HttpGet("{id}/images")]
        public async Task<IActionResult> GetPostImages(Guid id)
        {
            var post = await _context.Posts
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { message = "Post not found." });
            }

            var images = await _context.PostImages
                .Where(pi => pi.PostId == id)
                .OrderBy(pi => pi.DisplayOrder ?? 0)
                .Select(pi => new PostImageDTO
                {
                    ImageId = pi.ImageId,
                    PostId = pi.PostId,
                    ImageUrl = pi.ImageUrl,
                    Caption = pi.Caption,
                    DisplayOrder = pi.DisplayOrder,
                    CreatedAt = pi.CreatedAt
                })
                .ToListAsync();

            return Ok(images);
        }

        /**
         * Summary: Add image to a post.
         * Route: POST /api/posts/{id}/images
         * Params: id (Guid) - post identifier
         * Body: CreatePostImageDTO createPostImageDto
         * Returns: 201 Created with created image, 400/404 on validation errors
         */
        [HttpPost("{id}/images")]
        public async Task<IActionResult> AddPostImage(Guid id, [FromBody] CreatePostImageDTO createPostImageDto)
        {
            if (createPostImageDto == null || string.IsNullOrWhiteSpace(createPostImageDto.ImageUrl))
            {
                return BadRequest("Image URL is required.");
            }

            var post = await _context.Posts
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { message = "Post not found." });
            }

            var newImage = new PostImage
            {
                PostId = id,
                ImageUrl = createPostImageDto.ImageUrl,
                Caption = createPostImageDto.Caption,
                DisplayOrder = createPostImageDto.DisplayOrder,
                CreatedAt = DateTime.Now
            };

            _context.PostImages.Add(newImage);
            await _context.SaveChangesAsync();

            var imageDto = new PostImageDTO
            {
                ImageId = newImage.ImageId,
                PostId = newImage.PostId,
                ImageUrl = newImage.ImageUrl,
                Caption = newImage.Caption,
                DisplayOrder = newImage.DisplayOrder,
                CreatedAt = newImage.CreatedAt
            };

            return CreatedAtAction(nameof(GetPostImages), new { id = id }, imageDto);
        }


        /**
         * Summary: Update a post image by id.
         * Route: PUT /api/posts/{id}/images/{imageId}
         * Params: id (Guid) - post identifier, imageId (Guid) - image identifier
         * Body: CreatePostImageDTO (reuse) for update fields
         * Returns: 204 No Content, 404 if not found
         */
        [HttpPut("{id}/images/{imageId}")]
        public async Task<IActionResult> UpdatePostImage(Guid id, Guid imageId, [FromBody] CreatePostImageDTO updateDto)
        {
            var post = await _context.Posts
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { message = "Post not found." });
            }

            var existingImage = await _context.PostImages
                .FirstOrDefaultAsync(pi => pi.ImageId == imageId && pi.PostId == id);

            if (existingImage == null)
            {
                return NotFound(new { message = "Post image not found." });
            }

            if (updateDto == null)
            {
                return BadRequest("Invalid image data.");
            }

            // Update allowed fields if provided
            if (!string.IsNullOrWhiteSpace(updateDto.ImageUrl))
            {
                existingImage.ImageUrl = updateDto.ImageUrl;
            }
            existingImage.Caption = updateDto.Caption;
            existingImage.DisplayOrder = updateDto.DisplayOrder;
            existingImage.CreatedAt = existingImage.CreatedAt; // keep original

            _context.PostImages.Update(existingImage);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        
        /**
         * Summary: Delete a post image by id.
         * Route: DELETE /api/posts/{id}/images/{imageId}
         * Params: id (Guid) - post identifier, imageId (Guid) - image identifier
         * Returns: 204 No Content, 404 if not found
         */
        [HttpDelete("{id}/images/{imageId}")]
        public async Task<IActionResult> DeletePostImage(Guid id, Guid imageId)
        {
            var post = await _context.Posts
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { message = "Post not found." });
            }

            var existingImage = await _context.PostImages
                .FirstOrDefaultAsync(pi => pi.ImageId == imageId && pi.PostId == id);

            if (existingImage == null)
            {
                return NotFound(new { message = "Post image not found." });
            }

            _context.PostImages.Remove(existingImage);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /**
         * Summary: Upload an image file.
         * Route: POST /api/posts/upload
         * Body: IFormFile file
         * Returns: 200 OK with image path, 400 on errors
         */
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] ImageUploadRequest request)
        {
            var file = request.File;

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Images");
            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            var safeFileName = Path.GetFileName(file.FileName);
            var ext = Path.GetExtension(safeFileName);
            var finalName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsRoot, finalName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/images/{finalName}";
            return Ok(new { path = relativePath });
        }
    }
}
