/**
 * File: PostRepository.cs
 * Author: HieuNDHE173169
 * Created: 31/12/2025
 * Version: 1.0.0
 * Purpose: Repository implementation for Post, PostComment, Tag, and PostType data access.
 *          Inherits from BaseRepository to share DbContext connection.
 */
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Repositories;

/// <summary>
/// Repository implementation for Post-related entities.
/// Inherits from BaseRepository to use shared DbContext instance.
/// </summary>
public class PostRepository : BaseRepository, IPostRepository
{
    /// <summary>
    /// Initializes a new instance of the PostRepository class.
    /// </summary>
    /// <param name="context">The database context instance.</param>
    public PostRepository(KeytietkiemDbContext context) : base(context)
    {
    }

    // ===== Post Operations =====

    public async Task<Post?> GetPostByIdAsync(Guid id, bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Posts.AsQueryable();

        if (includeRelations)
        {
            query = query
                .Include(p => p.Author)
                .Include(p => p.PostType)
                .Include(p => p.Tags);
        }

        return await query.FirstOrDefaultAsync(p => p.PostId == id, cancellationToken);
    }

    public async Task<Post?> GetPostBySlugAsync(string slug, bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Posts.AsQueryable();

        if (includeRelations)
        {
            query = query
                .Include(p => p.Author)
                .Include(p => p.PostType)
                .Include(p => p.Tags);
        }

        return await query.FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);
    }

    public async Task<IEnumerable<Post>> GetAllPostsAsync(bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Posts.AsQueryable();

        if (includeRelations)
        {
            query = query
                .Include(p => p.Author)
                .Include(p => p.PostType)
                .Include(p => p.Tags);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Post>> GetPostsByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        return await _context.Posts
            .Where(p => p.Status == status)
            .Include(p => p.Author)
            .Include(p => p.PostType)
            .Include(p => p.Tags)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Post>> GetRelatedPostsAsync(Guid postId, Guid? postTypeId, int limit, CancellationToken cancellationToken = default)
    {
        var query = _context.Posts
            .Include(p => p.PostType)
            .Where(p => p.PostId != postId && p.Status == "Published");

        if (postTypeId.HasValue)
        {
            query = query.Where(p => p.PostTypeId == postTypeId.Value);
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> PostExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Posts.AnyAsync(p => p.PostId == id, cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludePostId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Posts.Where(p => p.Slug == slug);

        if (excludePostId.HasValue)
        {
            query = query.Where(p => p.PostId != excludePostId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    // ===== PostComment Operations =====

    public async Task<PostComment?> GetCommentByIdAsync(Guid id, bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        var query = _context.PostComments.AsQueryable();

        if (includeRelations)
        {
            query = query
                .Include(c => c.User)
                .Include(c => c.Post)
                .Include(c => c.InverseParentComment)
                    .ThenInclude(r => r.User);
        }

        return await query.FirstOrDefaultAsync(c => c.CommentId == id, cancellationToken);
    }

    public async Task<IEnumerable<PostComment>> GetCommentsByPostIdAsync(Guid postId, bool includeReplies = false, CancellationToken cancellationToken = default)
    {
        var query = _context.PostComments
            .Include(c => c.User)
            .Where(c => c.PostId == postId);

        if (includeReplies)
        {
            query = query
                .Include(c => c.InverseParentComment)
                    .ThenInclude(r => r.User);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PostComment>> GetCommentsByFilterAsync(
        Guid? postId,
        Guid? userId,
        bool? isApproved,
        Guid? parentCommentId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PostComments
            .Include(c => c.User)
            .Include(c => c.Post)
            .AsQueryable();

        if (postId.HasValue)
        {
            query = query.Where(c => c.PostId == postId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(c => c.UserId == userId.Value);
        }

        if (isApproved.HasValue)
        {
            query = query.Where(c => c.IsApproved == isApproved.Value);
        }

        if (parentCommentId.HasValue)
        {
            query = query.Where(c => c.ParentCommentId == parentCommentId.Value);
        }
        else if (parentCommentId == null && !postId.HasValue)
        {
            // If parentCommentId is explicitly null in query, get only top-level comments
            query = query.Where(c => c.ParentCommentId == null);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PostComment>> GetCommentRepliesAsync(Guid parentCommentId, CancellationToken cancellationToken = default)
    {
        return await _context.PostComments
            .Include(c => c.User)
            .Include(c => c.InverseParentComment)
                .ThenInclude(r => r.User)
            .Where(c => c.ParentCommentId == parentCommentId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CommentExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PostComments.AnyAsync(c => c.CommentId == id, cancellationToken);
    }

    // ===== Tag Operations =====

    public async Task<Tag?> GetTagByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tags.FirstOrDefaultAsync(t => t.TagId == id, cancellationToken);
    }

    public async Task<IEnumerable<Tag>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tags.ToListAsync(cancellationToken);
    }

    public async Task<bool> TagExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tags.AnyAsync(t => t.TagId == id, cancellationToken);
    }

    public async Task<bool> TagNameExistsAsync(string tagName, Guid? excludeTagId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Tags.Where(t => t.TagName == tagName);

        if (excludeTagId.HasValue)
        {
            query = query.Where(t => t.TagId != excludeTagId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> TagSlugExistsAsync(string slug, Guid? excludeTagId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Tags.Where(t => t.Slug == slug);

        if (excludeTagId.HasValue)
        {
            query = query.Where(t => t.TagId != excludeTagId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    // ===== PostType Operations =====

    public async Task<PostType?> GetPostTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PostTypes.FirstOrDefaultAsync(pt => pt.PostTypeId == id, cancellationToken);
    }

    public async Task<IEnumerable<PostType>> GetAllPostTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PostTypes.ToListAsync(cancellationToken);
    }

    public async Task<bool> PostTypeExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PostTypes.AnyAsync(pt => pt.PostTypeId == id, cancellationToken);
    }

    public async Task<bool> PostTypeHasPostsAsync(Guid postTypeId, CancellationToken cancellationToken = default)
    {
        return await _context.Posts.AnyAsync(p => p.PostTypeId == postTypeId, cancellationToken);
    }
}

