/**
 * File: PostCommentDTO.cs
 * Created: 2025-01-15
 * Purpose: Data Transfer Object for PostComment operations. Provides a clean interface
 *          for API communication without exposing internal entity structure.
 *          Supports comment creation, updates, and responses with nested replies.
 * Usage:
 *   - Input DTO for comment creation/updates
 *   - Output DTO for comment responses
 *   - Validation and data transfer
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Post
{
    public class PostCommentDTO
    {
        public Guid CommentId { get; set; }
        public Guid? PostId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? ParentCommentId { get; set; }
        public string Content { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public bool? IsApproved { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string? PostTitle { get; set; }
        public List<PostCommentDTO> Replies { get; set; } = new List<PostCommentDTO>();
        public int ReplyCount { get; set; }
    }

    public class PostCommentListItemDTO
    {
        public Guid CommentId { get; set; }
        public Guid? PostId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? ParentCommentId { get; set; }
        public string Content { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public bool? IsApproved { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public int ReplyCount { get; set; }
    }

    public class CreatePostCommentDTO
    {
        public Guid PostId { get; set; }

        public Guid UserId { get; set; }

        public string Content { get; set; } = null!;

        public Guid? ParentCommentId { get; set; }
    }

    public class UpdatePostCommentDTO
    {
        public string Content { get; set; } = null!;

        public bool? IsApproved { get; set; }
    }

    public class PostCommentResponseDTO : PostCommentDTO
    {
        public string? PostTitle { get; set; }
    }
}

