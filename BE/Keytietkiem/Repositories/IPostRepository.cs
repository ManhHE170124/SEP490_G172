/**
 * File: IPostRepository.cs
 * Author: HieuNDHE173169
 * Created: 31/12/2025
 * Version: 1.0.0
 * Purpose: Repository interface for Post, PostComment, Tag, and PostType data access operations.
 */
using Keytietkiem.Models;

namespace Keytietkiem.Repositories;

/// <summary>
/// Repository interface for Post-related entities (Post, PostComment, Tag, PostType).
/// </summary>
public interface IPostRepository
{
    // ===== Post Operations =====
    
    /// <summary>
    /// Gets a post by ID with optional relations.
    /// </summary>
    Task<Post?> GetPostByIdAsync(Guid id, bool includeRelations = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a post by slug with optional relations.
    /// </summary>
    Task<Post?> GetPostBySlugAsync(string slug, bool includeRelations = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all posts with optional relations.
    /// </summary>
    Task<IEnumerable<Post>> GetAllPostsAsync(bool includeRelations = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets posts by status.
    /// </summary>
    Task<IEnumerable<Post>> GetPostsByStatusAsync(string status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets related posts (same post type, excluding current post).
    /// </summary>
    Task<IEnumerable<Post>> GetRelatedPostsAsync(Guid postId, Guid? postTypeId, int limit, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a post exists by ID.
    /// </summary>
    Task<bool> PostExistsAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a slug exists (optionally excluding a specific post ID).
    /// </summary>
    Task<bool> SlugExistsAsync(string slug, Guid? excludePostId = null, CancellationToken cancellationToken = default);
    
    // ===== PostComment Operations =====
    
    /// <summary>
    /// Gets a comment by ID with optional relations.
    /// </summary>
    Task<PostComment?> GetCommentByIdAsync(Guid id, bool includeRelations = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all comments for a specific post.
    /// </summary>
    Task<IEnumerable<PostComment>> GetCommentsByPostIdAsync(Guid postId, bool includeReplies = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets comments by filter criteria.
    /// </summary>
    Task<IEnumerable<PostComment>> GetCommentsByFilterAsync(
        Guid? postId, 
        Guid? userId, 
        bool? isApproved, 
        Guid? parentCommentId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets direct replies for a parent comment.
    /// </summary>
    Task<IEnumerable<PostComment>> GetCommentRepliesAsync(Guid parentCommentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a comment exists by ID.
    /// </summary>
    Task<bool> CommentExistsAsync(Guid id, CancellationToken cancellationToken = default);
    
    // ===== Tag Operations =====
    
    /// <summary>
    /// Gets a tag by ID.
    /// </summary>
    Task<Tag?> GetTagByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all tags.
    /// </summary>
    Task<IEnumerable<Tag>> GetAllTagsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a tag exists by ID.
    /// </summary>
    Task<bool> TagExistsAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a tag name exists (optionally excluding a specific tag ID).
    /// </summary>
    Task<bool> TagNameExistsAsync(string tagName, Guid? excludeTagId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a tag slug exists (optionally excluding a specific tag ID).
    /// </summary>
    Task<bool> TagSlugExistsAsync(string slug, Guid? excludeTagId = null, CancellationToken cancellationToken = default);
    
    // ===== PostType Operations =====
    
    /// <summary>
    /// Gets a post type by ID.
    /// </summary>
    Task<PostType?> GetPostTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all post types.
    /// </summary>
    Task<IEnumerable<PostType>> GetAllPostTypesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a post type exists by ID.
    /// </summary>
    Task<bool> PostTypeExistsAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a post type has any posts.
    /// </summary>
    Task<bool> PostTypeHasPostsAsync(Guid postTypeId, CancellationToken cancellationToken = default);
}

