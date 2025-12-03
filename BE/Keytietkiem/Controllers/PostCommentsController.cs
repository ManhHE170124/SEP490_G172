/**
 * File: PostCommentsController.cs
 * Created: 2025-01-15
 * Purpose: Manage post comments (CRUD). Handles comment creation, updates, deletion,
 *          and approval with support for nested comments (replies).
 * Endpoints:
 *   - GET    /api/comments                    : List all comments (with filters)
 *   - GET    /api/comments/{id}               : Get comment by id
 *   - GET    /api/posts/{postId}/comments      : Get top-level comments for a post
 *   - GET    /api/comments/{id}/replies       : Get replies for a comment
 *   - POST   /api/comments                    : Create a new comment or reply
 *   - PUT    /api/comments/{id}               : Update comment
 *   - DELETE /api/comments/{id}               : Delete comment
 *   - PATCH  /api/comments/{id}/approve       : Approve comment
 *   - PATCH  /api/comments/{id}/reject        : Reject comment
 */

using Microsoft.AspNetCore.Mvc;
using Keytietkiem.Models;
using Keytietkiem.DTOs.Post;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [Route("api/comments")]
    [ApiController]
    public class PostCommentsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;

        public PostCommentsController(KeytietkiemDbContext context)
        {
            _context = context;
        }

        /**
         * Summary: Retrieve all comments with optional filters.
         * Route: GET /api/comments
         * Params: postId, userId, isApproved, parentCommentId (all optional query params)
         * Returns: 200 OK with list of comments
         */
        [HttpGet]
        public async Task<IActionResult> GetComments(
            [FromQuery] Guid? postId,
            [FromQuery] Guid? userId,
            [FromQuery] bool? isApproved,
            [FromQuery] Guid? parentCommentId)
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

            var comments = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var commentDtos = comments.Select(c => new PostCommentListItemDTO
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
                ReplyCount = _context.PostComments.Count(r => r.ParentCommentId == c.CommentId)
            }).ToList();

            return Ok(commentDtos);
        }

        /**
         * Summary: Retrieve a comment by id with nested replies.
         * Route: GET /api/comments/{id}
         * Params: id (Guid) - comment identifier
         * Returns: 200 OK with comment, 404 if not found
         */
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCommentById(Guid id)
        {
            var comment = await _context.PostComments
                .Include(c => c.User)
                .Include(c => c.Post)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(c => c.CommentId == id);

            if (comment == null)
            {
                return NotFound(new { message = "Comment không được tìm thấy" });
            }

            var commentDto = MapToCommentDTO(comment);
            return Ok(commentDto);
        }

        /**
         * Summary: Get top-level comments for a specific post (ParentCommentId = NULL).
         * Route: GET /api/posts/{postId}/comments
         * Params: postId (Guid) - post identifier
         * Returns: 200 OK with list of top-level comments and their replies
         */
        [HttpGet("posts/{postId}/comments")]
        public async Task<IActionResult> GetPostComments(
            Guid postId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var post = await _context.Posts.FindAsync(postId);
            if (post == null)
            {
                return NotFound(new { message = "Bài viết không được tìm thấy" });
            }

            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            // Load all comments for this post with their users
            var allComments = await _context.PostComments
                .Include(c => c.User)
                .Where(c => c.PostId == postId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Build flat structure: group by root parent (top-level comment)
            // A comment is a root if ParentCommentId is null
            // A comment's root is found by traversing up the parent chain
            var commentDict = allComments.ToDictionary(c => c.CommentId);
            var rootCommentMap = new Dictionary<Guid, Guid>(); // Maps commentId to its root commentId

            // Find root for each comment
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

            // Group comments into threads (root + all children)
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
                    .OrderBy(c => c.CreatedAt) // Children in chronological order
                    .ToList();

                thread.AddRange(children);
                threads.Add(thread);
            }

            // Apply pagination on threads (not individual comments)
            // This ensures a complete thread (root + all children) stays on the same page
            var totalComments = threads.Sum(t => t.Count);

            // Calculate which threads to include in this page
            // We'll try to fit as many complete threads as possible within pageSize
            var pagedThreads = new List<List<PostComment>>();
            var currentPageCount = 0;
            var targetStartIndex = (page - 1) * pageSize;

            // Find the starting thread by counting comments
            // Always include complete threads to avoid splitting parent and children
            var commentCount = 0;
            var startThreadIndex = 0;
            foreach (var thread in threads)
            {
                if (commentCount + thread.Count > targetStartIndex)
                {
                    // This thread contains or starts after the target start index
                    startThreadIndex = threads.IndexOf(thread);
                    // Always include the complete thread to avoid splitting
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
                    // Add the entire thread if it fits
                    pagedThreads.Add(thread);
                    currentPageCount += thread.Count;
                }
                else
                {
                    // Thread doesn't fit, stop here to avoid splitting
                    break;
                }
            }

            // Flatten the paged threads into a list of comments
            var pagedComments = pagedThreads.SelectMany(t => t).ToList();

            // IMPORTANT: Never skip comments from the middle of a thread to avoid splitting parent and children
            // If targetStartIndex is in the middle of the first thread, we include the whole thread
            // This means the page might start slightly before targetStartIndex, but threads stay intact

            // Convert to DTOs with root information
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
                    ReplyCount = 0, // Not used in flat structure
                    Replies = new List<PostCommentDTO>() // Empty - flat structure
                };

                // Add root comment ID for frontend grouping
                var rootId = GetRootCommentId(c.CommentId);
                if (rootId.HasValue && rootId.Value != c.CommentId)
                {
                    // This is a child, store root ID in a custom property
                    // We'll use ParentCommentId to track the immediate parent
                }

                return dto;
            }).ToList();

            return Ok(new
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
            });
        }

        /**
         * Summary: Get direct replies for a specific comment.
         * Route: GET /api/comments/{id}/replies
         * Params: id (Guid) - parent comment identifier
         * Returns: 200 OK with list of replies, 404 if parent comment not found
         */
        [HttpGet("{id}/replies")]
        public async Task<IActionResult> GetCommentReplies(Guid id)
        {
            var parentComment = await _context.PostComments.FindAsync(id);
            if (parentComment == null)
            {
                return NotFound(new { message = "Comment không được tìm thấy" });
            }

            var replies = await _context.PostComments
                .Include(c => c.User)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.User)
                .Where(c => c.ParentCommentId == id)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            var replyDtos = replies.Select(c => MapToCommentDTO(c)).ToList();
            return Ok(replyDtos);
        }

        /**
         * Summary: Create a new comment or reply.
         * Route: POST /api/comments
         * Body: CreatePostCommentDTO createCommentDto
         * Returns: 201 Created with created comment, 400/404 on validation errors
         */
        [HttpPost]
        public async Task<IActionResult> CreateComment([FromBody] CreatePostCommentDTO createCommentDto)
        {
            if (createCommentDto == null || string.IsNullOrWhiteSpace(createCommentDto.Content))
            {
                return BadRequest(new { message = "Nội dung comment không được để trống." });
            }

            // Validate Post exists
            var post = await _context.Posts.FindAsync(createCommentDto.PostId);
            if (post == null)
            {
                return NotFound(new { message = "Bài viết không được tìm thấy." });
            }

            // Validate User exists
            var user = await _context.Users.FindAsync(createCommentDto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Người dùng không được tìm thấy." });
            }

            // Validate ParentComment if provided (for replies)
            Guid? actualParentId = null;
            if (createCommentDto.ParentCommentId.HasValue)
            {
                var parentComment = await _context.PostComments
                    .FirstOrDefaultAsync(c => c.CommentId == createCommentDto.ParentCommentId.Value);

                if (parentComment == null)
                {
                    return NotFound(new { message = "Comment cha không được tìm thấy." });
                }

                // Validate that parent comment belongs to the same post
                if (parentComment.PostId != createCommentDto.PostId)
                {
                    return BadRequest(new { message = "Comment cha phải thuộc cùng một bài viết." });
                }

                // Check if parent comment is hidden
                if (parentComment.IsApproved == false)
                {
                    return BadRequest(new { message = "Không thể trả lời bình luận đã bị ẩn." });
                }

                // New logic: If parent is a child (has ParentCommentId), make this reply a child of parent's parent
                // Otherwise, make it a child of the parent
                if (parentComment.ParentCommentId.HasValue)
                {
                    // Parent is a child, so this reply should be a child of parent's parent
                    // But first check if parent's parent is visible
                    var grandParent = await _context.PostComments
                        .FirstOrDefaultAsync(c => c.CommentId == parentComment.ParentCommentId.Value);

                    if (grandParent != null && grandParent.IsApproved == false)
                    {
                        return BadRequest(new { message = "Không thể trả lời bình luận đã bị ẩn." });
                    }

                    actualParentId = parentComment.ParentCommentId.Value;
                }
                else
                {
                    // Parent is a root, so this reply should be a child of the parent
                    actualParentId = createCommentDto.ParentCommentId.Value;
                }
            }

            var newComment = new PostComment
            {
                PostId = createCommentDto.PostId,
                UserId = createCommentDto.UserId,
                ParentCommentId = actualParentId, // Use calculated parent ID
                Content = createCommentDto.Content.Trim(),
                CreatedAt = DateTime.Now,
                IsApproved = true // Default to visible
            };

            _context.PostComments.Add(newComment);
            await _context.SaveChangesAsync();

            // Reload with relations
            var createdComment = await _context.PostComments
                .Include(c => c.User)
                .Include(c => c.Post)
                .FirstOrDefaultAsync(c => c.CommentId == newComment.CommentId);

            var commentDto = MapToCommentDTO(createdComment!);
            return CreatedAtAction(nameof(GetCommentById), new { id = createdComment!.CommentId }, commentDto);
        }

        /**
         * Summary: Update an existing comment.
         * Route: PUT /api/comments/{id}
         * Params: id (Guid)
         * Body: UpdatePostCommentDTO updateCommentDto
         * Returns: 200 OK, 400/404 on errors
         */
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateComment(Guid id, [FromBody] UpdatePostCommentDTO updateCommentDto)
        {
            if (updateCommentDto == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(updateCommentDto.Content))
            {
                return BadRequest(new { message = "Nội dung comment không được để trống." });
            }

            var existing = await _context.PostComments.FindAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = "Comment không được tìm thấy." });
            }

            existing.Content = updateCommentDto.Content.Trim();
            if (updateCommentDto.IsApproved.HasValue)
            {
                existing.IsApproved = updateCommentDto.IsApproved.Value;
            }

            await _context.SaveChangesAsync();

            // Reload with relations
            var updatedComment = await _context.PostComments
                .Include(c => c.User)
                .Include(c => c.Post)
                .FirstOrDefaultAsync(c => c.CommentId == id);

            var commentDto = MapToCommentDTO(updatedComment!);
            return Ok(commentDto);
        }

        /**
         * Summary: Delete a comment and all its replies recursively.
         * Route: DELETE /api/comments/{id}
         * Params: id (Guid)
         * Returns: 204 No Content, 404 if not found
         */
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(Guid id)
        {
            var comment = await _context.PostComments
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.CommentId == id);

            if (comment == null)
            {
                return NotFound(new { message = "Comment không được tìm thấy." });
            }

            // Recursively delete all replies
            await DeleteCommentRecursive(comment);

            _context.PostComments.Remove(comment);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /**
         * Helper method to recursively delete comment and all its replies
         */
        private async Task DeleteCommentRecursive(PostComment comment)
        {
            if (comment.Replies != null && comment.Replies.Any())
            {
                var replies = comment.Replies.ToList(); // Create a copy to avoid modification during iteration
                foreach (var reply in replies)
                {
                    // Load replies of this reply
                    var replyWithChildren = await _context.PostComments
                        .Include(r => r.Replies)
                        .FirstOrDefaultAsync(r => r.CommentId == reply.CommentId);

                    if (replyWithChildren != null)
                    {
                        await DeleteCommentRecursive(replyWithChildren);
                        _context.PostComments.Remove(replyWithChildren);
                    }
                }
            }
        }

        /**
         * Summary: Show a comment (set IsApproved = true).
         * Route: PATCH /api/comments/{id}/show
         * Params: id (Guid)
         * Returns: 200 OK, 400 if parent is hidden, 404 if not found
         */
        [HttpPatch("{id}/show")]
        public async Task<IActionResult> ShowComment(Guid id)
        {
            var comment = await _context.PostComments
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.CommentId == id);

            if (comment == null)
            {
                return NotFound(new { message = "Comment không được tìm thấy." });
            }

            // Check if comment has a parent and if parent is hidden
            if (comment.ParentCommentId.HasValue)
            {
                var parent = await _context.PostComments
                    .FirstOrDefaultAsync(c => c.CommentId == comment.ParentCommentId.Value);

                if (parent != null && parent.IsApproved == false)
                {
                    return BadRequest(new { message = "Không thể hiển thị bình luận con khi bình luận cha đang bị ẩn." });
                }
            }

            // Show comment and all its replies recursively
            await ShowCommentRecursive(comment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Comment đã được hiển thị.", commentId = id });
        }

        /**
         * Summary: Hide a comment (set IsApproved = false).
         * Route: PATCH /api/comments/{id}/hide
         * Params: id (Guid)
         * Returns: 200 OK, 404 if not found
         */
        [HttpPatch("{id}/hide")]
        public async Task<IActionResult> HideComment(Guid id)
        {
            var comment = await _context.PostComments
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.CommentId == id);

            if (comment == null)
            {
                return NotFound(new { message = "Comment không được tìm thấy." });
            }

            // Hide comment and all its replies recursively
            await HideCommentRecursive(comment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Comment đã bị ẩn.", commentId = id });
        }

        /**
         * Helper method to recursively show comment and all its replies
         */
        private async Task ShowCommentRecursive(PostComment comment)
        {
            comment.IsApproved = true;

            if (comment.Replies != null && comment.Replies.Any())
            {
                foreach (var reply in comment.Replies)
                {
                    var replyWithChildren = await _context.PostComments
                        .Include(r => r.Replies)
                        .FirstOrDefaultAsync(r => r.CommentId == reply.CommentId);

                    if (replyWithChildren != null)
                    {
                        await ShowCommentRecursive(replyWithChildren);
                    }
                }
            }
        }

        /**
         * Helper method to recursively hide comment and all its replies
         */
        private async Task HideCommentRecursive(PostComment comment)
        {
            comment.IsApproved = false;

            if (comment.Replies != null && comment.Replies.Any())
            {
                foreach (var reply in comment.Replies)
                {
                    var replyWithChildren = await _context.PostComments
                        .Include(r => r.Replies)
                        .FirstOrDefaultAsync(r => r.CommentId == reply.CommentId);

                    if (replyWithChildren != null)
                    {
                        await HideCommentRecursive(replyWithChildren);
                    }
                }
            }
        }

        /**
         * Helper method to map PostComment entity to PostCommentDTO
         */
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
                ReplyCount = comment.Replies?.Count ?? 0
            };

            // Map nested replies recursively
            if (comment.Replies != null && comment.Replies.Any())
            {
                dto.Replies = comment.Replies
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => MapToCommentDTO(r))
                    .ToList();
            }

            return dto;
        }
    }
}

