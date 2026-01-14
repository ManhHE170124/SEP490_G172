/**
 * File: IPostService.cs
 * Author: HieuNDHE173169
 * Created: 31/12/2025
 * Version: 1.0.0
 * Purpose: Service interface for Post, PostComment, Tag, and PostType business logic operations.
 */
using Keytietkiem.DTOs.Post;

namespace Keytietkiem.Services.Interfaces;

/// <summary>
/// Service interface for Post-related business logic operations.
/// Service handles validation, mapping, transactions, and returns DTOs.
/// </summary>
public interface IPostService
{
    // ===== Post Operations =====
    
    /// <summary>
    /// Gets a post by ID.
    /// </summary>
    Task<PostDTO> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a post by slug (for public viewing).
    /// </summary>
    Task<PostDTO> GetPostBySlugAsync(string slug, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all posts.
    /// </summary>
    /// <param name="excludeStaticContent">If true, excludes posts with static content PostTypes (policy, user-guide, about-us).</param>
    Task<IEnumerable<PostListItemDTO>> GetAllPostsAsync(bool excludeStaticContent = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new post.
    /// </summary>
    Task<PostDTO> CreatePostAsync(CreatePostDTO createDto, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing post.
    /// </summary>
    Task UpdatePostAsync(Guid id, UpdatePostDTO updateDto, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a post by ID.
    /// </summary>
    Task DeletePostAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets related posts (same post type, excluding current post).
    /// </summary>
    Task<IEnumerable<PostListItemDTO>> GetRelatedPostsAsync(Guid postId, int limit, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Increments view count for a post.
    /// </summary>
    Task IncrementViewCountAsync(Guid postId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all SpecificDocumentation posts.
    /// </summary>
    Task<IEnumerable<PostListItemDTO>> GetAllSpecificDocumentationAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a SpecificDocumentation post by slug.
    /// </summary>
    Task<PostDTO> GetSpecificDocumentationBySlugAsync(string slug, CancellationToken cancellationToken = default);
    
    // ===== PostType Operations =====
    
    /// <summary>
    /// Gets all post types.
    /// </summary>
    Task<IEnumerable<PostTypeDTO>> GetAllPostTypesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a post type by ID.
    /// </summary>
    Task<PostTypeDTO> GetPostTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new post type.
    /// </summary>
    Task<PostTypeDTO> CreatePostTypeAsync(CreatePostTypeDTO createDto, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing post type.
    /// </summary>
    Task UpdatePostTypeAsync(Guid id, UpdatePostTypeDTO updateDto, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a post type by ID.
    /// </summary>
    Task DeletePostTypeAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    
    // ===== PostComment Operations =====
    
    /// <summary>
    /// Gets a comment by ID.
    /// </summary>
    Task<PostCommentDTO> GetCommentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets comments for a specific post with pagination.
    /// </summary>
    Task<object> GetCommentsByPostIdAsync(Guid postId, int page, int pageSize, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets comments by filter criteria.
    /// </summary>
    Task<IEnumerable<PostCommentListItemDTO>> GetCommentsByFilterAsync(
        Guid? postId,
        Guid? userId,
        bool? isApproved,
        Guid? parentCommentId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets direct replies for a parent comment.
    /// </summary>
    Task<IEnumerable<PostCommentDTO>> GetCommentRepliesAsync(Guid parentCommentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new comment or reply.
    /// </summary>
    Task<PostCommentDTO> CreateCommentAsync(CreatePostCommentDTO createDto, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing comment.
    /// </summary>
    Task<PostCommentDTO> UpdateCommentAsync(Guid id, UpdatePostCommentDTO updateDto, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a comment and all its replies recursively.
    /// </summary>
    Task DeleteCommentAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Shows a comment (sets IsApproved = true) and all its replies recursively.
    /// </summary>
    Task ShowCommentAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Hides a comment (sets IsApproved = false) and all its replies recursively.
    /// </summary>
    Task HideCommentAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    
    // ===== Tag Operations =====
    
    /// <summary>
    /// Gets all tags.
    /// </summary>
    Task<IEnumerable<TagDTO>> GetAllTagsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a tag by ID.
    /// </summary>
    Task<TagDTO> GetTagByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new tag.
    /// </summary>
    Task<TagDTO> CreateTagAsync(CreateTagDTO createDto, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing tag.
    /// </summary>
    Task UpdateTagAsync(Guid id, UpdateTagDTO updateDto, Guid actorId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a tag by ID.
    /// </summary>
    Task DeleteTagAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
}

