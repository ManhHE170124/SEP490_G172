/**
 * File: PostService.cs
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: Service implementation for Post, PostComment, Tag, and PostType operations.
 *          Service is a pass-through layer that delegates to Repository.
 */
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;

namespace Keytietkiem.Services;

/// <summary>
/// Service implementation for Post-related operations.
/// Service is a pass-through layer that delegates to Repository and returns entities.
/// </summary>
public class PostService : IPostService
{
    private readonly IPostRepository _postRepository;

    public PostService(IPostRepository postRepository)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
    }

    // ===== Post Operations =====

    public async Task<Post?> GetPostByIdAsync(Guid id, bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetPostByIdAsync(id, includeRelations, cancellationToken);
    }

    public async Task<Post?> GetPostBySlugAsync(string slug, bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetPostBySlugAsync(slug, includeRelations, cancellationToken);
    }

    public async Task<IEnumerable<Post>> GetAllPostsAsync(bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetAllPostsAsync(includeRelations, cancellationToken);
    }

    public async Task<IEnumerable<Post>> GetPostsByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetPostsByStatusAsync(status, cancellationToken);
    }

    public async Task<IEnumerable<Post>> GetRelatedPostsAsync(Guid postId, Guid? postTypeId, int limit, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetRelatedPostsAsync(postId, postTypeId, limit, cancellationToken);
    }

    public async Task<bool> PostExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _postRepository.PostExistsAsync(id, cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludePostId = null, CancellationToken cancellationToken = default)
    {
        return await _postRepository.SlugExistsAsync(slug, excludePostId, cancellationToken);
    }

    // ===== PostType Operations =====

    public async Task<IEnumerable<PostType>> GetAllPostTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetAllPostTypesAsync(cancellationToken);
    }

    public async Task<PostType?> GetPostTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetPostTypeByIdAsync(id, cancellationToken);
    }

    public async Task<bool> PostTypeExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _postRepository.PostTypeExistsAsync(id, cancellationToken);
    }

    public async Task<bool> PostTypeHasPostsAsync(Guid postTypeId, CancellationToken cancellationToken = default)
    {
        return await _postRepository.PostTypeHasPostsAsync(postTypeId, cancellationToken);
    }

    // ===== PostComment Operations =====

    public async Task<PostComment?> GetCommentByIdAsync(Guid id, bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetCommentByIdAsync(id, includeRelations, cancellationToken);
    }

    public async Task<IEnumerable<PostComment>> GetCommentsByPostIdAsync(Guid postId, bool includeReplies = false, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetCommentsByPostIdAsync(postId, includeReplies, cancellationToken);
    }

    public async Task<IEnumerable<PostComment>> GetCommentsByFilterAsync(
        Guid? postId,
        Guid? userId,
        bool? isApproved,
        Guid? parentCommentId,
        CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetCommentsByFilterAsync(postId, userId, isApproved, parentCommentId, cancellationToken);
    }

    public async Task<IEnumerable<PostComment>> GetCommentRepliesAsync(Guid parentCommentId, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetCommentRepliesAsync(parentCommentId, cancellationToken);
    }

    public async Task<bool> CommentExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _postRepository.CommentExistsAsync(id, cancellationToken);
    }

    // ===== Tag Operations =====

    public async Task<IEnumerable<Tag>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetAllTagsAsync(cancellationToken);
    }

    public async Task<Tag?> GetTagByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _postRepository.GetTagByIdAsync(id, cancellationToken);
    }

    public async Task<bool> TagExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _postRepository.TagExistsAsync(id, cancellationToken);
    }

    public async Task<bool> TagNameExistsAsync(string tagName, Guid? excludeTagId = null, CancellationToken cancellationToken = default)
    {
        return await _postRepository.TagNameExistsAsync(tagName, excludeTagId, cancellationToken);
    }

    public async Task<bool> TagSlugExistsAsync(string slug, Guid? excludeTagId = null, CancellationToken cancellationToken = default)
    {
        return await _postRepository.TagSlugExistsAsync(slug, excludeTagId, cancellationToken);
    }
}
