/**
 * File: PostService.cs
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: Service implementation for Post, PostComment, Tag, and PostType business logic.
 */
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Post;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Services;

/// <summary>
/// Service implementation for Post-related business logic operations.
/// </summary>
public class PostService : IPostService
{
    private readonly KeytietkiemDbContext _context;
    private readonly IPostRepository _postRepository;
    private readonly IClock _clock;
    private readonly ILogger<PostService> _logger;
    private readonly IAuditLogger _auditLogger;

    public PostService(
        KeytietkiemDbContext context,
        IPostRepository postRepository,
        IClock clock,
        ILogger<PostService> logger,
        IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    // ===== Post Operations =====

    public async Task<PostDTO> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetPostByIdAsync(id, includeRelations: true, cancellationToken);
        if (post == null)
        {
            throw new InvalidOperationException("Bài viết không được tìm thấy.");
        }

        return MapToPostDTO(post);
    }

    public async Task<PostDTO> GetPostBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetPostBySlugAsync(slug, includeRelations: true, cancellationToken);
        if (post == null)
        {
            throw new InvalidOperationException("Không tìm thấy bài viết");
        }

        // Increment view count
        if (post.Status == "Published")
        {
            post.ViewCount = (post.ViewCount ?? 0) + 1;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return MapToPostDTO(post);
    }

    public async Task<IEnumerable<PostListItemDTO>> GetAllPostsAsync(bool excludeStaticContent = false, CancellationToken cancellationToken = default)
    {
        var posts = await _postRepository.GetAllPostsAsync(includeRelations: true, cancellationToken);
        
        if (excludeStaticContent)
        {
            // Filter out static content posts
            posts = posts.Where(post =>
            {
                if (!post.PostTypeId.HasValue) return true;
                
                var postType = post.PostType;
                if (postType == null) return true;
                
                var postTypeSlug = StaticContentTypes.GenerateSlugFromName(postType.PostTypeName);
                return !StaticContentTypes.IsStaticContent(postTypeSlug);
            });
        }
        
        return posts.Select(MapToPostListItemDTO);
    }

    public async Task<PostDTO> CreatePostAsync(CreatePostDTO createDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (createDto == null)
        {
            throw new ArgumentNullException(nameof(createDto));
        }

        // Validate PostType exists
        PostType? postType = null;
        if (createDto.PostTypeId.HasValue)
        {
            postType = await _postRepository.GetPostTypeByIdAsync(createDto.PostTypeId.Value, cancellationToken);
            if (postType == null)
            {
                throw new InvalidOperationException("Danh mục bài viết không được tìm thấy.");
            }
            
            // Check if this is a static content PostType
            var postTypeSlug = StaticContentTypes.GenerateSlugFromName(postType.PostTypeName);
            if (StaticContentTypes.IsStaticContent(postTypeSlug))
            {
                // Check if PostType already has a Post
                var existingPosts = await _postRepository.GetAllPostsAsync(includeRelations: false, cancellationToken);
                var hasPost = existingPosts.Any(p => p.PostTypeId == createDto.PostTypeId.Value);
                if (hasPost)
                {
                    throw new InvalidOperationException("PostType này chỉ được phép có 1 Post.");
                }
            }
        }

        // Validate Author exists
        if (createDto.AuthorId.HasValue)
        {
            var author = await _context.Users.FindAsync(new object[] { createDto.AuthorId.Value }, cancellationToken);
            if (author == null)
            {
                throw new InvalidOperationException("Không tìm thấy thông tin tác giả.");
            }
        }

        // Validate Tags exist
        if (createDto.TagIds != null && createDto.TagIds.Any())
        {
            var tagCount = await _context.Tags
                .CountAsync(t => createDto.TagIds.Contains(t.TagId), cancellationToken);
            if (tagCount != createDto.TagIds.Count)
            {
                throw new InvalidOperationException("Không tìm thấy thẻ nào được gán cho bài viết này.");
            }
        }

        // Check if Slug is unique
        if (await _postRepository.SlugExistsAsync(createDto.Slug, null, cancellationToken))
        {
            throw new InvalidOperationException("Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác.");
        }

        var newPost = new Post
        {
            Title = createDto.Title,
            Slug = createDto.Slug,
            ShortDescription = createDto.ShortDescription,
            Content = createDto.Content,
            Thumbnail = createDto.Thumbnail,
            PostTypeId = createDto.PostTypeId,
            AuthorId = createDto.AuthorId,
            MetaTitle = createDto.MetaTitle,
            Status = postType != null && StaticContentTypes.IsStaticContent(StaticContentTypes.GenerateSlugFromName(postType.PostTypeName))
                ? "Published" 
                : (createDto.Status ?? "Draft"),
            ViewCount = 0,
            CreatedAt = _clock.UtcNow
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Posts.Add(newPost);
            await _context.SaveChangesAsync(cancellationToken);

            // Add Tags
            if (createDto.TagIds != null && createDto.TagIds.Any())
            {
                var tags = await _context.Tags
                    .Where(t => createDto.TagIds.Contains(t.TagId))
                    .ToListAsync(cancellationToken);
                newPost.Tags = tags;
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Post {PostId} created by {ActorId}", newPost.PostId, actorId);

            // Reload with relations
            var createdPost = await _postRepository.GetPostByIdAsync(newPost.PostId, includeRelations: true, cancellationToken);
            return MapToPostDTO(createdPost!);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (ex.InnerException?.Message?.Contains("duplicate key") == true ||
                ex.InnerException?.Message?.Contains("UNIQUE KEY") == true)
            {
                throw new InvalidOperationException("Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác.");
            }
            throw;
        }
    }

    public async Task UpdatePostAsync(Guid id, UpdatePostDTO updateDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (updateDto == null)
        {
            throw new ArgumentNullException(nameof(updateDto));
        }

        var existing = await _postRepository.GetPostByIdAsync(id, includeRelations: true, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException("Bài viết không được tìm thấy.");
        }

        // Validate PostType exists
        PostType? newPostType = null;
        if (updateDto.PostTypeId.HasValue)
        {
            newPostType = await _postRepository.GetPostTypeByIdAsync(updateDto.PostTypeId.Value, cancellationToken);
            if (newPostType == null)
            {
                throw new InvalidOperationException("Không tìm thấy danh mục bài viết.");
            }
            
            // If changing PostType to a static content type, check if it already has a Post
            if (updateDto.PostTypeId.Value != existing.PostTypeId)
            {
                var newPostTypeSlug = StaticContentTypes.GenerateSlugFromName(newPostType.PostTypeName);
                if (StaticContentTypes.IsStaticContent(newPostTypeSlug))
                {
                    var existingPosts = await _postRepository.GetAllPostsAsync(includeRelations: false, cancellationToken);
                    var hasPost = existingPosts.Any(p => p.PostTypeId == updateDto.PostTypeId.Value && p.PostId != id);
                    if (hasPost)
                    {
                        throw new InvalidOperationException("PostType này chỉ được phép có 1 Post.");
                    }
                }
            }
        }

        // Validate Tags exist
        if (updateDto.TagIds != null && updateDto.TagIds.Any())
        {
            var tagCount = await _context.Tags
                .CountAsync(t => updateDto.TagIds.Contains(t.TagId), cancellationToken);
            if (tagCount != updateDto.TagIds.Count)
            {
                throw new InvalidOperationException("Không tìm thấy thẻ nào được gán cho bài viết này.");
            }
        }

        // Check if Slug is unique (excluding current post)
        if (existing.Slug != updateDto.Slug)
        {
            if (await _postRepository.SlugExistsAsync(updateDto.Slug, id, cancellationToken))
            {
                throw new InvalidOperationException("Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác.");
            }
        }

        existing.Title = updateDto.Title;
        existing.Slug = updateDto.Slug;
        existing.ShortDescription = updateDto.ShortDescription;
        existing.Content = updateDto.Content;
        existing.Thumbnail = updateDto.Thumbnail;
        existing.PostTypeId = updateDto.PostTypeId;
        existing.MetaTitle = updateDto.MetaTitle;
        
        // If PostType is static content, Status must be "Published"
        var currentPostType = newPostType ?? (existing.PostTypeId.HasValue 
            ? await _postRepository.GetPostTypeByIdAsync(existing.PostTypeId.Value, cancellationToken) 
            : null);
        if (currentPostType != null)
        {
            var postTypeSlug = StaticContentTypes.GenerateSlugFromName(currentPostType.PostTypeName);
            if (StaticContentTypes.IsStaticContent(postTypeSlug))
            {
                existing.Status = "Published";
            }
            else
            {
                existing.Status = updateDto.Status;
            }
        }
        else
        {
            existing.Status = updateDto.Status;
        }
        
        existing.UpdatedAt = _clock.UtcNow;

        // Update Tags
        if (updateDto.TagIds != null)
        {
            var tags = await _context.Tags
                .Where(t => updateDto.TagIds.Contains(t.TagId))
                .ToListAsync(cancellationToken);
            existing.Tags = tags;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Posts.Update(existing);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Post {PostId} updated by {ActorId}", id, actorId);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (ex.InnerException?.Message?.Contains("duplicate key") == true ||
                ex.InnerException?.Message?.Contains("UNIQUE KEY") == true)
            {
                throw new InvalidOperationException("Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác.");
            }
            throw;
        }
    }

    public async Task DeletePostAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var existingPost = await _postRepository.GetPostByIdAsync(id, includeRelations: false, cancellationToken);
        if (existingPost == null)
        {
            throw new InvalidOperationException("Bài viết không được tìm thấy.");
        }

        // Check if this Post belongs to a static content PostType
        if (existingPost.PostTypeId.HasValue)
        {
            var postType = await _postRepository.GetPostTypeByIdAsync(existingPost.PostTypeId.Value, cancellationToken);
            if (postType != null)
            {
                var postTypeSlug = StaticContentTypes.GenerateSlugFromName(postType.PostTypeName);
                if (StaticContentTypes.IsStaticContent(postTypeSlug))
                {
                    throw new InvalidOperationException("Không thể xóa Post này.");
                }
            }
        }

        _context.Posts.Remove(existingPost);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Post {PostId} deleted by {ActorId}", id, actorId);
    }

    public async Task<IEnumerable<PostListItemDTO>> GetRelatedPostsAsync(Guid postId, int limit, CancellationToken cancellationToken = default)
    {
        var currentPost = await _postRepository.GetPostByIdAsync(postId, includeRelations: false, cancellationToken);
        if (currentPost == null)
        {
            throw new InvalidOperationException("Bài viết không được tìm thấy.");
        }

        var relatedPosts = await _postRepository.GetRelatedPostsAsync(postId, currentPost.PostTypeId, limit, cancellationToken);
        return relatedPosts.Select(MapToPostListItemDTO);
    }

    public async Task IncrementViewCountAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetPostByIdAsync(postId, includeRelations: false, cancellationToken);
        if (post != null)
        {
            post.ViewCount = (post.ViewCount ?? 0) + 1;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<PostDTO> GetStaticContentAsync(string type, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Type không được để trống.", nameof(type));
        }

        // Normalize type to lowercase
        type = type.ToLower();

        // Validate type is a static content type
        if (!StaticContentTypes.IsStaticContent(type))
        {
            throw new InvalidOperationException($"Type '{type}' không phải là static content type hợp lệ.");
        }

        // Find PostType by matching slug
        var allPostTypes = await _postRepository.GetAllPostTypesAsync(cancellationToken);
        var postType = allPostTypes.FirstOrDefault(pt => 
            StaticContentTypes.GenerateSlugFromName(pt.PostTypeName).ToLower() == type);

        if (postType == null)
        {
            throw new InvalidOperationException($"Không tìm thấy PostType với type '{type}'.");
        }

        // Get all posts for this PostType
        var allPosts = await _postRepository.GetAllPostsAsync(includeRelations: true, cancellationToken);
        var post = allPosts.FirstOrDefault(p => p.PostTypeId == postType.PostTypeId);

        if (post == null)
        {
            throw new InvalidOperationException($"Không tìm thấy Post cho type '{type}'.");
        }

        return MapToPostDTO(post);
    }

    public async Task<PostDTO> CreateStaticContentAsync(CreateStaticContentDTO createDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (createDto == null)
        {
            throw new ArgumentNullException(nameof(createDto));
        }

        // Validate PostType exists and is a static content type
        var postType = await _postRepository.GetPostTypeByIdAsync(createDto.PostTypeId, cancellationToken);
        if (postType == null)
        {
            throw new InvalidOperationException("Danh mục bài viết không được tìm thấy.");
        }

        var postTypeSlug = StaticContentTypes.GenerateSlugFromName(postType.PostTypeName);
        if (!StaticContentTypes.IsStaticContent(postTypeSlug))
        {
            throw new InvalidOperationException("PostType này không phải là static content type.");
        }

        // Check if PostType already has a Post
        var existingPosts = await _postRepository.GetAllPostsAsync(includeRelations: false, cancellationToken);
        var hasPost = existingPosts.Any(p => p.PostTypeId == createDto.PostTypeId);
        if (hasPost)
        {
            throw new InvalidOperationException("PostType này chỉ được phép có 1 Post.");
        }

        // Validate Author exists
        if (createDto.AuthorId.HasValue)
        {
            var author = await _context.Users.FindAsync(new object[] { createDto.AuthorId.Value }, cancellationToken);
            if (author == null)
            {
                throw new InvalidOperationException("Không tìm thấy thông tin tác giả.");
            }
        }

        // Check if Slug is unique
        if (await _postRepository.SlugExistsAsync(createDto.Slug, null, cancellationToken))
        {
            throw new InvalidOperationException("Slug đã tồn tại. Vui lòng chọn slug khác.");
        }

        var newPost = new Post
        {
            Title = createDto.Title,
            Slug = createDto.Slug,
            ShortDescription = createDto.ShortDescription,
            Content = createDto.Content,
            Thumbnail = createDto.Thumbnail,
            PostTypeId = createDto.PostTypeId,
            AuthorId = createDto.AuthorId,
            Status = "Published", // Static content is always Published
            ViewCount = 0,
            CreatedAt = _clock.UtcNow
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Posts.Add(newPost);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Static content Post {PostId} created by {ActorId}", newPost.PostId, actorId);

            // Reload with relations
            var createdPost = await _postRepository.GetPostByIdAsync(newPost.PostId, includeRelations: true, cancellationToken);
            return MapToPostDTO(createdPost!);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (ex.InnerException?.Message?.Contains("duplicate key") == true ||
                ex.InnerException?.Message?.Contains("UNIQUE KEY") == true)
            {
                throw new InvalidOperationException("Slug đã tồn tại. Vui lòng chọn slug khác.");
            }
            throw;
        }
    }

    public async Task UpdateStaticContentAsync(Guid id, UpdateStaticContentDTO updateDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (updateDto == null)
        {
            throw new ArgumentNullException(nameof(updateDto));
        }

        var existing = await _postRepository.GetPostByIdAsync(id, includeRelations: true, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException("Bài viết không được tìm thấy.");
        }

        // Validate PostType exists and is a static content type
        var postType = await _postRepository.GetPostTypeByIdAsync(updateDto.PostTypeId, cancellationToken);
        if (postType == null)
        {
            throw new InvalidOperationException("Danh mục bài viết không được tìm thấy.");
        }

        var postTypeSlug = StaticContentTypes.GenerateSlugFromName(postType.PostTypeName);
        if (!StaticContentTypes.IsStaticContent(postTypeSlug))
        {
            throw new InvalidOperationException("PostType này không phải là static content type.");
        }

        // If changing PostType, ensure the new PostType doesn't already have a post
        if (existing.PostTypeId != updateDto.PostTypeId)
        {
            var existingPosts = await _postRepository.GetAllPostsAsync(includeRelations: false, cancellationToken);
            var hasPost = existingPosts.Any(p => p.PostTypeId == updateDto.PostTypeId && p.PostId != id);
            if (hasPost)
            {
                throw new InvalidOperationException("PostType này chỉ được phép có 1 Post.");
            }
        }

        // Check if Slug is unique (excluding current post)
        if (await _postRepository.SlugExistsAsync(updateDto.Slug, id, cancellationToken))
        {
            throw new InvalidOperationException("Slug đã tồn tại. Vui lòng chọn slug khác.");
        }

        // Update fields
        existing.Title = updateDto.Title;
        existing.Slug = updateDto.Slug;
        existing.ShortDescription = updateDto.ShortDescription;
        existing.Content = updateDto.Content;
        existing.Thumbnail = updateDto.Thumbnail;
        existing.PostTypeId = updateDto.PostTypeId;
        existing.Status = "Published"; // Static content is always Published
        existing.UpdatedAt = _clock.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Posts.Update(existing);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Static content Post {PostId} updated by {ActorId}", id, actorId);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (ex.InnerException?.Message?.Contains("duplicate key") == true ||
                ex.InnerException?.Message?.Contains("UNIQUE KEY") == true)
            {
                throw new InvalidOperationException("Slug đã tồn tại. Vui lòng chọn slug khác.");
            }
            throw;
        }
    }

    // ===== PostType Operations =====

    public async Task<IEnumerable<PostTypeDTO>> GetAllPostTypesAsync(CancellationToken cancellationToken = default)
    {
        var postTypes = await _postRepository.GetAllPostTypesAsync(cancellationToken);
        return postTypes.Select(MapToPostTypeDTO);
    }

    public async Task<PostTypeDTO> GetPostTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var postType = await _postRepository.GetPostTypeByIdAsync(id, cancellationToken);
        if (postType == null)
        {
            throw new InvalidOperationException("Danh mục bài viết không được tìm thấy.");
        }

        return MapToPostTypeDTO(postType);
    }

    public async Task<PostTypeDTO> CreatePostTypeAsync(CreatePostTypeDTO createDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (createDto == null)
        {
            throw new ArgumentNullException(nameof(createDto));
        }

        var newPostType = new PostType
        {
            PostTypeName = createDto.PostTypeName,
            Description = createDto.Description,
            CreatedAt = _clock.UtcNow
        };

        _context.PostTypes.Add(newPostType);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PostType {PostTypeId} created by {ActorId}", newPostType.PostTypeId, actorId);

        return MapToPostTypeDTO(newPostType);
    }

    public async Task UpdatePostTypeAsync(Guid id, UpdatePostTypeDTO updateDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (updateDto == null)
        {
            throw new ArgumentNullException(nameof(updateDto));
        }

        var existing = await _postRepository.GetPostTypeByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException("Danh mục bài viết không được tìm thấy.");
        }

        existing.PostTypeName = updateDto.PostTypeName;
        existing.Description = updateDto.Description;
        existing.UpdatedAt = _clock.UtcNow;

        _context.PostTypes.Update(existing);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PostType {PostTypeId} updated by {ActorId}", id, actorId);
    }

    public async Task DeletePostTypeAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var existing = await _postRepository.GetPostTypeByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException("Danh mục bài viết không được tìm thấy.");
        }

        // Check if this is a static content PostType
        var postTypeSlug = StaticContentTypes.GenerateSlugFromName(existing.PostTypeName);
        if (StaticContentTypes.IsStaticContent(postTypeSlug))
        {
            throw new InvalidOperationException("Không thể xóa PostType này.");
        }

        if (await _postRepository.PostTypeHasPostsAsync(id, cancellationToken))
        {
            throw new InvalidOperationException("Không thể xóa danh mục này.");
        }

        _context.PostTypes.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PostType {PostTypeId} deleted by {ActorId}", id, actorId);
    }

    // ===== PostComment Operations =====

    public async Task<PostCommentDTO> GetCommentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var comment = await _postRepository.GetCommentByIdAsync(id, includeRelations: true, cancellationToken);
        if (comment == null)
        {
            throw new InvalidOperationException("Comment không được tìm thấy");
        }

        return MapToCommentDTO(comment);
    }

    public async Task<object> GetCommentsByPostIdAsync(Guid postId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetPostByIdAsync(postId, includeRelations: false, cancellationToken);
        if (post == null)
        {
            throw new InvalidOperationException("Bài viết không được tìm thấy");
        }

        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Load all comments for this post with their users
        var allComments = await _postRepository.GetCommentsByPostIdAsync(postId, includeReplies: false, cancellationToken);

        // Build flat structure: group by root parent (top-level comment)
        var commentDict = allComments.ToDictionary(c => c.CommentId);
        var rootCommentMap = new Dictionary<Guid, Guid>(); // Maps commentId to its root commentId

        // Helper to find root for each comment
        Guid? GetRootCommentId(Guid commentId)
        {
            if (!commentDict.ContainsKey(commentId))
                return null;

            var comment = commentDict[commentId];
            if (comment.ParentCommentId == null)
                return commentId; // This is a root

            // Traverse up to find root
            var current = comment;
            var visited = new HashSet<Guid>(); // Prevent infinite loops

            while (current.ParentCommentId.HasValue && !visited.Contains(current.CommentId))
            {
                visited.Add(current.CommentId);
                if (rootCommentMap.ContainsKey(current.CommentId))
                {
                    return rootCommentMap[current.CommentId];
                }

                if (!commentDict.ContainsKey(current.ParentCommentId.Value))
                    break;

                current = commentDict[current.ParentCommentId.Value];
                if (current.ParentCommentId == null)
                {
                    rootCommentMap[commentId] = current.CommentId;
                    return current.CommentId;
                }
            }

            return null;
        }

        // Build threads: each thread contains a root comment and all its children
        var rootComments = allComments
            .Where(c => c.ParentCommentId == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        var threads = new List<List<PostComment>>();
        foreach (var root in rootComments)
        {
            var thread = new List<PostComment> { root };

            // Add all children of this root (direct and indirect) in chronological order
            var children = allComments
                .Where(c =>
                {
                    var rootId = GetRootCommentId(c.CommentId);
                    return rootId == root.CommentId && c.CommentId != root.CommentId;
                })
                .OrderBy(c => c.CreatedAt)
                .ToList();

            thread.AddRange(children);
            threads.Add(thread);
        }

        // Apply pagination on threads (not individual comments)
        var totalComments = threads.Sum(t => t.Count);

        var pagedThreads = new List<List<PostComment>>();
        var currentPageCount = 0;
        var targetStartIndex = (page - 1) * pageSize;

        // Find the starting thread by counting comments
        var commentCount = 0;
        var startThreadIndex = 0;
        foreach (var thread in threads)
        {
            if (commentCount + thread.Count > targetStartIndex)
            {
                startThreadIndex = threads.IndexOf(thread);
                pagedThreads.Add(thread);
                currentPageCount = commentCount + thread.Count - targetStartIndex;
                startThreadIndex++;
                break;
            }
            commentCount += thread.Count;
        }

        // Continue adding complete threads until we reach pageSize
        for (int i = startThreadIndex; i < threads.Count; i++)
        {
            var thread = threads[i];
            if (currentPageCount + thread.Count <= pageSize)
            {
                pagedThreads.Add(thread);
                currentPageCount += thread.Count;
            }
            else
            {
                break;
            }
        }

        var pagedComments = pagedThreads.SelectMany(t => t).ToList();

        var commentDtos = pagedComments.Select(c =>
        {
            var dto = new PostCommentDTO
            {
                CommentId = c.CommentId,
                PostId = c.PostId,
                UserId = c.UserId,
                ParentCommentId = c.ParentCommentId,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                IsApproved = c.IsApproved ?? false,
                UserName = c.User != null ? (c.User.FullName ?? $"{c.User.FirstName} {c.User.LastName}".Trim()) : null,
                UserEmail = c.User?.Email,
                PostTitle = c.Post?.Title,
                ReplyCount = 0,
                Replies = new List<PostCommentDTO>()
            };

            return dto;
        }).ToList();

        return new
        {
            comments = commentDtos,
            pagination = new
            {
                page = page,
                pageSize = pageSize,
                totalCount = totalComments,
                totalPages = (int)Math.Ceiling(totalComments / (double)pageSize),
                hasNextPage = startThreadIndex + pagedThreads.Count < threads.Count,
                hasPreviousPage = page > 1
            }
        };
    }

    public async Task<IEnumerable<PostCommentListItemDTO>> GetCommentsByFilterAsync(
        Guid? postId,
        Guid? userId,
        bool? isApproved,
        Guid? parentCommentId,
        CancellationToken cancellationToken = default)
    {
        var comments = await _postRepository.GetCommentsByFilterAsync(postId, userId, isApproved, parentCommentId, cancellationToken);

        // Load all reply counts in one query
        var commentIds = comments.Select(c => c.CommentId).ToList();
        var replyCounts = await _context.PostComments
            .Where(r => commentIds.Contains(r.ParentCommentId!.Value))
            .GroupBy(r => r.ParentCommentId!.Value)
            .Select(g => new { CommentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CommentId, x => x.Count, cancellationToken);

        return comments.Select(c => new PostCommentListItemDTO
        {
            CommentId = c.CommentId,
            PostId = c.PostId,
            UserId = c.UserId,
            ParentCommentId = c.ParentCommentId,
            Content = c.Content,
            CreatedAt = c.CreatedAt,
            IsApproved = c.IsApproved ?? false,
            UserName = c.User != null ? (c.User.FullName ?? $"{c.User.FirstName} {c.User.LastName}".Trim()) : null,
            UserEmail = c.User?.Email,
            ReplyCount = replyCounts.GetValueOrDefault(c.CommentId, 0)
        });
    }

    public async Task<IEnumerable<PostCommentDTO>> GetCommentRepliesAsync(Guid parentCommentId, CancellationToken cancellationToken = default)
    {
        var parentComment = await _postRepository.GetCommentByIdAsync(parentCommentId, includeRelations: false, cancellationToken);
        if (parentComment == null)
        {
            throw new InvalidOperationException("Comment không được tìm thấy");
        }

        var replies = await _postRepository.GetCommentRepliesAsync(parentCommentId, cancellationToken);
        return replies.Select(MapToCommentDTO);
    }

    public async Task<PostCommentDTO> CreateCommentAsync(CreatePostCommentDTO createDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (createDto == null || string.IsNullOrWhiteSpace(createDto.Content))
        {
            throw new InvalidOperationException("Nội dung comment không được để trống.");
        }

        // Validate Post exists
        var post = await _postRepository.GetPostByIdAsync(createDto.PostId, includeRelations: false, cancellationToken);
        if (post == null)
        {
            throw new InvalidOperationException("Bài viết không được tìm thấy.");
        }

        // Validate User exists
        var user = await _context.Users.FindAsync(new object[] { createDto.UserId }, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException("Người dùng không được tìm thấy.");
        }

        // Validate ParentComment if provided (for replies)
        Guid? actualParentId = null;
        if (createDto.ParentCommentId.HasValue)
        {
            var parentComment = await _postRepository.GetCommentByIdAsync(createDto.ParentCommentId.Value, includeRelations: false, cancellationToken);
            if (parentComment == null)
            {
                throw new InvalidOperationException("Comment cha không được tìm thấy.");
            }

            // Validate that parent comment belongs to the same post
            if (parentComment.PostId != createDto.PostId)
            {
                throw new InvalidOperationException("Comment cha phải thuộc cùng một bài viết.");
            }

            // Check if parent comment is hidden
            if (parentComment.IsApproved == false)
            {
                throw new InvalidOperationException("Không thể trả lời bình luận đã bị ẩn.");
            }

            // New logic: If parent is a child (has ParentCommentId), make this reply a child of parent's parent
            if (parentComment.ParentCommentId.HasValue)
            {
                var grandParent = await _postRepository.GetCommentByIdAsync(parentComment.ParentCommentId.Value, includeRelations: false, cancellationToken);
                if (grandParent != null && grandParent.IsApproved == false)
                {
                    throw new InvalidOperationException("Không thể trả lời bình luận đã bị ẩn.");
                }

                actualParentId = parentComment.ParentCommentId.Value;
            }
            else
            {
                actualParentId = createDto.ParentCommentId.Value;
            }
        }

        var newComment = new PostComment
        {
            PostId = createDto.PostId,
            UserId = createDto.UserId,
            ParentCommentId = actualParentId,
            Content = createDto.Content.Trim(),
            CreatedAt = _clock.UtcNow,
            IsApproved = true // Default to visible
        };

        _context.PostComments.Add(newComment);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with relations
        var createdComment = await _postRepository.GetCommentByIdAsync(newComment.CommentId, includeRelations: true, cancellationToken);
        var commentDto = MapToCommentDTO(createdComment!);

        return commentDto;
    }

    public async Task<PostCommentDTO> UpdateCommentAsync(Guid id, UpdatePostCommentDTO updateDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (updateDto == null)
        {
            throw new ArgumentNullException(nameof(updateDto));
        }

        if (string.IsNullOrWhiteSpace(updateDto.Content))
        {
            throw new InvalidOperationException("Nội dung comment không được để trống.");
        }

        var existing = await _postRepository.GetCommentByIdAsync(id, includeRelations: false, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException("Comment không được tìm thấy.");
        }

        existing.Content = updateDto.Content.Trim();
        if (updateDto.IsApproved.HasValue)
        {
            existing.IsApproved = updateDto.IsApproved.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Reload with relations
        var updatedComment = await _postRepository.GetCommentByIdAsync(id, includeRelations: true, cancellationToken);
        return MapToCommentDTO(updatedComment!);
    }

    public async Task DeleteCommentAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var comment = await _postRepository.GetCommentByIdAsync(id, includeRelations: true, cancellationToken);
        if (comment == null)
        {
            throw new InvalidOperationException("Comment không được tìm thấy.");
        }

        // Recursively delete all replies
        await DeleteCommentRecursive(comment, cancellationToken);

        _context.PostComments.Remove(comment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ShowCommentAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var comment = await _postRepository.GetCommentByIdAsync(id, includeRelations: true, cancellationToken);
        if (comment == null)
        {
            throw new InvalidOperationException("Comment không được tìm thấy.");
        }

        // Check if comment has a parent and if parent is hidden
        if (comment.ParentCommentId.HasValue)
        {
            var parent = await _postRepository.GetCommentByIdAsync(comment.ParentCommentId.Value, includeRelations: false, cancellationToken);
            if (parent != null && parent.IsApproved == false)
            {
                throw new InvalidOperationException("Không thể hiển thị bình luận con khi bình luận cha đang bị ẩn.");
            }
        }

        // Show comment and all its replies recursively
        await ShowCommentRecursive(comment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task HideCommentAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var comment = await _postRepository.GetCommentByIdAsync(id, includeRelations: true, cancellationToken);
        if (comment == null)
        {
            throw new InvalidOperationException("Comment không được tìm thấy.");
        }

        // Hide comment and all its replies recursively
        await HideCommentRecursive(comment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ===== Tag Operations =====

    public async Task<IEnumerable<TagDTO>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        var tags = await _postRepository.GetAllTagsAsync(cancellationToken);
        return tags.Select(MapToTagDTO);
    }

    public async Task<TagDTO> GetTagByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tag = await _postRepository.GetTagByIdAsync(id, cancellationToken);
        if (tag == null)
        {
            throw new InvalidOperationException("Thẻ không được tìm thấy.");
        }

        return MapToTagDTO(tag);
    }

    public async Task<TagDTO> CreateTagAsync(CreateTagDTO createDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (createDto == null)
        {
            throw new ArgumentNullException(nameof(createDto));
        }

        if (await _postRepository.TagNameExistsAsync(createDto.TagName, null, cancellationToken))
        {
            throw new InvalidOperationException("Tên thẻ đã tồn tại.");
        }

        if (await _postRepository.TagSlugExistsAsync(createDto.Slug, null, cancellationToken))
        {
            throw new InvalidOperationException("Slug đã tồn tại.");
        }

        var newTag = new Tag
        {
            TagName = createDto.TagName,
            Slug = createDto.Slug,
            CreatedAt = _clock.UtcNow
        };

        _context.Tags.Add(newTag);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tag {TagId} created by {ActorId}", newTag.TagId, actorId);

        return MapToTagDTO(newTag);
    }

    public async Task UpdateTagAsync(Guid id, UpdateTagDTO updateDto, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (updateDto == null)
        {
            throw new ArgumentNullException(nameof(updateDto));
        }

        var existing = await _postRepository.GetTagByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException("Thẻ không được tìm thấy.");
        }

        if (await _postRepository.TagNameExistsAsync(updateDto.TagName, id, cancellationToken))
        {
            throw new InvalidOperationException("Tên thẻ đã tồn tại.");
        }

        if (await _postRepository.TagSlugExistsAsync(updateDto.Slug, id, cancellationToken))
        {
            throw new InvalidOperationException("Slug trùng với thẻ đã có sẵn.");
        }

        existing.TagName = updateDto.TagName;
        existing.Slug = updateDto.Slug;
        existing.UpdatedAt = _clock.UtcNow;

        _context.Tags.Update(existing);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tag {TagId} updated by {ActorId}", id, actorId);
    }

    public async Task DeleteTagAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var existingTag = await _postRepository.GetTagByIdAsync(id, cancellationToken);
        if (existingTag == null)
        {
            throw new InvalidOperationException("Thẻ không được tìm thấy.");
        }

        _context.Tags.Remove(existingTag);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tag {TagId} deleted by {ActorId}", id, actorId);
    }

    // ===== Helper Methods =====

    private PostDTO MapToPostDTO(Post post)
    {
        return new PostDTO
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
    }

    private PostListItemDTO MapToPostListItemDTO(Post post)
    {
        return new PostListItemDTO
        {
            PostId = post.PostId,
            Title = post.Title,
            Slug = post.Slug,
            ShortDescription = post.ShortDescription,
            Thumbnail = post.Thumbnail,
            PostTypeId = post.PostTypeId,
            AuthorId = post.AuthorId,
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
                Slug = t.Slug,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList()
        };
    }

    private PostTypeDTO MapToPostTypeDTO(PostType postType)
    {
        return new PostTypeDTO
        {
            PostTypeId = postType.PostTypeId,
            PostTypeName = postType.PostTypeName,
            Description = postType.Description,
            CreatedAt = postType.CreatedAt,
            UpdatedAt = postType.UpdatedAt
        };
    }

    private PostCommentDTO MapToCommentDTO(PostComment comment)
    {
        var dto = new PostCommentDTO
        {
            CommentId = comment.CommentId,
            PostId = comment.PostId,
            UserId = comment.UserId,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            IsApproved = comment.IsApproved ?? false,
            UserName = comment.User != null ? (comment.User.FullName ?? $"{comment.User.FirstName} {comment.User.LastName}".Trim()) : null,
            UserEmail = comment.User?.Email,
            PostTitle = comment.Post?.Title,
            ReplyCount = comment.InverseParentComment?.Count ?? 0
        };

        // Map nested replies recursively
        if (comment.InverseParentComment != null && comment.InverseParentComment.Any())
        {
            dto.Replies = comment.InverseParentComment
                .OrderBy(r => r.CreatedAt)
                .Select(r => MapToCommentDTO(r))
                .ToList();
        }

        return dto;
    }

    private TagDTO MapToTagDTO(Tag tag)
    {
        return new TagDTO
        {
            TagId = tag.TagId,
            TagName = tag.TagName,
            Slug = tag.Slug,
            CreatedAt = tag.CreatedAt,
            UpdatedAt = tag.UpdatedAt
        };
    }

    private async Task DeleteCommentRecursive(PostComment comment, CancellationToken cancellationToken)
    {
        if (comment.InverseParentComment != null && comment.InverseParentComment.Any())
        {
            var replies = comment.InverseParentComment.ToList(); // Create a copy to avoid modification during iteration
            foreach (var reply in replies)
            {
                var replyWithChildren = await _postRepository.GetCommentByIdAsync(reply.CommentId, includeRelations: true, cancellationToken);
                if (replyWithChildren != null)
                {
                    await DeleteCommentRecursive(replyWithChildren, cancellationToken);
                    _context.PostComments.Remove(replyWithChildren);
                }
            }
        }
    }

    private async Task ShowCommentRecursive(PostComment comment, CancellationToken cancellationToken)
    {
        comment.IsApproved = true;

        if (comment.InverseParentComment != null && comment.InverseParentComment.Any())
        {
            foreach (var reply in comment.InverseParentComment)
            {
                var replyWithChildren = await _postRepository.GetCommentByIdAsync(reply.CommentId, includeRelations: true, cancellationToken);
                if (replyWithChildren != null)
                {
                    await ShowCommentRecursive(replyWithChildren, cancellationToken);
                }
            }
        }
    }

    private async Task HideCommentRecursive(PostComment comment, CancellationToken cancellationToken)
    {
        comment.IsApproved = false;

        if (comment.InverseParentComment != null && comment.InverseParentComment.Any())
        {
            foreach (var reply in comment.InverseParentComment)
            {
                var replyWithChildren = await _postRepository.GetCommentByIdAsync(reply.CommentId, includeRelations: true, cancellationToken);
                if (replyWithChildren != null)
                {
                    await HideCommentRecursive(replyWithChildren, cancellationToken);
                }
            }
        }
    }
}
