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
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Post
{
    /// <summary>
    /// Data Transfer Object for Post operations.
    /// Represents a complete post with all related data including tags and images.
    /// </summary>
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
        public string? Status { get; set; }
        public int? ViewCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? AuthorName { get; set; }
        public string? PostTypeName { get; set; }
        public List<TagDTO> Tags { get; set; } = new List<TagDTO>();
        public List<PostImageDTO> PostImages { get; set; } = new List<PostImageDTO>();
    }

    /// <summary>
    /// Data Transfer Object for Post list items.
    /// Represents a simplified post view for list displays.
    /// </summary>
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

    /// <summary>
    /// Data Transfer Object for creating a new post.
    /// </summary>
    public class CreatePostDTO
    {
        [Required(ErrorMessage = "Tiêu đề không được để trống.")]
        [StringLength(250, MinimumLength = 10, ErrorMessage = "Tiêu đề phải có từ 10 đến 250 ký tự.")]
        public string Title { get; set; } = null!;
        
        [Required(ErrorMessage = "Slug không được để trống.")]
        [StringLength(250, MinimumLength = 10, ErrorMessage = "Slug phải có từ 10 đến 250 ký tự.")]
        [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug chỉ được chứa chữ thường, số và dấu gạch ngang.")]
        public string Slug { get; set; } = null!;
        
        [StringLength(255, ErrorMessage = "Mô tả ngắn không được vượt quá 255 ký tự.")]
        public string? ShortDescription { get; set; }
        
        public string? Content { get; set; }
        public string? Thumbnail { get; set; }
        public Guid? PostTypeId { get; set; }
        public Guid? AuthorId { get; set; }
        
        [StringLength(60, ErrorMessage = "Meta title không được vượt quá 60 ký tự.")]
        public string? MetaTitle { get; set; }
        public string? Status { get; set; }
        public List<Guid> TagIds { get; set; } = new List<Guid>();
    }

    /// <summary>
    /// Data Transfer Object for updating an existing post.
    /// </summary>
    public class UpdatePostDTO
    {
        [Required(ErrorMessage = "Tiêu đề không được để trống.")]
        [StringLength(250, MinimumLength = 10, ErrorMessage = "Tiêu đề phải có từ 10 đến 250 ký tự.")]
        public string Title { get; set; } = null!;
        
        [Required(ErrorMessage = "Slug không được để trống.")]
        [StringLength(250, MinimumLength = 10, ErrorMessage = "Slug phải có từ 10 đến 250 ký tự.")]
        [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug chỉ được chứa chữ thường, số và dấu gạch ngang.")]
        public string Slug { get; set; } = null!;
        
        [StringLength(255, ErrorMessage = "Mô tả ngắn không được vượt quá 255 ký tự.")]
        public string? ShortDescription { get; set; }
        
        public string? Content { get; set; }
        public string? Thumbnail { get; set; }
        public Guid? PostTypeId { get; set; }
        
        [StringLength(60, ErrorMessage = "Meta title không được vượt quá 60 ký tự.")]
        public string? MetaTitle { get; set; }
        public string? Status { get; set; }
        public List<Guid> TagIds { get; set; } = new List<Guid>();
    }

    /// <summary>
    /// Data Transfer Object for PostType operations.
    /// </summary>
    public class PostTypeDTO
    {
        public Guid PostTypeId { get; set; }
        public string PostTypeName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    // public class PostTypeDto
    // {
    //     public Guid PostTypeId { get; set; }
    //     public string PostTypeName { get; set; }
    //     public string Description { get; set; }
    //     public DateTime CreatedAt { get; set; }
    //     public int PostCount { get; set; } 
    // }

    /// <summary>
    /// Data Transfer Object for creating a new post type.
    /// </summary>
    public class CreatePostTypeDTO
    {
        [Required(ErrorMessage = "Tên danh mục không được để trống.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên danh mục phải có từ 2 đến 100 ký tự.")]
        public string PostTypeName { get; set; }

        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Data Transfer Object for updating an existing post type.
    /// </summary>
    public class UpdatePostTypeDTO
    {
        [Required(ErrorMessage = "Tên danh mục không được để trống.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên danh mục phải có từ 2 đến 100 ký tự.")]
        public string PostTypeName { get; set; }

        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
        public string Description { get; set; }
    }

    public class TagDto
    {
        public Guid TagId { get; set; }
        public string TagName { get; set; }
        public string Slug { get; set; }
        public int PostCount { get; set; } 
    }

    public class CreateTagDto
    {
        public string TagName { get; set; }

        public string Slug { get; set; }
    }

    public class UpdateTagDto : CreateTagDto
    {
        public bool IsActive { get; set; } = true;
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
