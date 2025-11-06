/**
 * File: PostDTO.cs
 * Author: HieuNDHE173169
 * Created: 21/10/2025
 * Last Updated: 24/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Object for Post operations. Provides a clean interface
 *          for API communication without exposing internal entity structure.
 *          Supports post creation, updates, and responses with navigation properties.
 * Usage:
 *   - Input DTO for post creation/updates
 *   - Output DTO for post responses
 *   - Validation and data transfer
 */

using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.Post
{
    public class PostDTO
    {
        public Guid PostId { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? Content { get; set; }
        public string? Thumbnail { get; set; }
        public Guid? PostTypeId { get; set; }
        public Guid? AuthorId { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? Status { get; set; }
        public int? ViewCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? AuthorName { get; set; }
        public string? PostTypeName { get; set; }
        public List<TagDTO> Tags { get; set; } = new List<TagDTO>();
        public List<PostImageDTO> PostImages { get; set; } = new List<PostImageDTO>();
    }

    public class PostListItemDTO
    {
        public Guid PostId { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? Thumbnail { get; set; }
        public Guid? PostTypeId { get; set; }
        public Guid? AuthorId { get; set; }
        public string? Status { get; set; }
        public int? ViewCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? AuthorName { get; set; }
        public string? PostTypeName { get; set; }
        public List<TagDTO> Tags { get; set; } = new List<TagDTO>();
    }

    public class CreatePostDTO
    {
        public string Title { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? Content { get; set; }
        public string? Thumbnail { get; set; }
        public Guid? PostTypeId { get; set; }
        public Guid? AuthorId { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? Status { get; set; }
        public List<Guid> TagIds { get; set; } = new List<Guid>();
    }

    public class UpdatePostDTO
    {
        public string Title { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? Content { get; set; }
        public string? Thumbnail { get; set; }
        public Guid? PostTypeId { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? Status { get; set; }
        public List<Guid> TagIds { get; set; } = new List<Guid>();
    }

    public class PostTypeDTO
    {
        public Guid PostTypeId { get; set; }
        public string PostTypeName { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class ImageUploadRequest
    {
        public IFormFile File { get; set; }
    }
    public class ImageDeleteRequest
    {
        public string PublicId { get; set; }
    }
    public class PostImageDTO
    {
        public Guid ImageId { get; set; }
        public Guid PostId { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string? Caption { get; set; }
        public int? DisplayOrder { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CreatePostImageDTO
    {
        public string ImageUrl { get; set; } = null!;
        public string? Caption { get; set; }
        public int? DisplayOrder { get; set; }
    }

    public class UpdatePostImageDTO
    {
        public string ImageUrl { get; set; } = null!;
        public string? Caption { get; set; }
        public int? DisplayOrder { get; set; }
    }
}
