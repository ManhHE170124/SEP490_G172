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

namespace Keytietkiem.DTOs
{
    public class PostDTO
    {
        public Guid PostId { get; set; }
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string? Thumbnail { get; set; }
        public int PostTypeId { get; set; }
        public Guid AuthorId { get; set; }
        public string Status { get; set; } = null!;
        public int ViewCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? AuthorName { get; set; }
        public string? PostTypeName { get; set; }
        public List<TagDTO> Tags { get; set; } = new List<TagDTO>();
    }

    public class PostListItemDTO
    {
        public Guid PostId { get; set; }
        public string Title { get; set; } = null!;
        public string? Thumbnail { get; set; }
        public int PostTypeId { get; set; }
        public Guid AuthorId { get; set; }
        public string Status { get; set; } = null!;
        public int ViewCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? AuthorName { get; set; }
        public string? PostTypeName { get; set; }
        public List<TagDTO> Tags { get; set; } = new List<TagDTO>();
    }

    public class CreatePostDTO
    {
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string? Thumbnail { get; set; }
        public int PostTypeId { get; set; }
        public Guid AuthorId { get; set; }
        public string Status { get; set; } = "Draft";
        public List<int> TagIds { get; set; } = new List<int>();
    }

    public class UpdatePostDTO
    {
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string? Thumbnail { get; set; }
        public int PostTypeId { get; set; }
        public string Status { get; set; } = null!;
        public List<int> TagIds { get; set; } = new List<int>();
    }
}
