/**
 * File: PostCommentsController.cs
 * Author: HieuNDHE173169
 * Created: 2025-01-15
 * Purpose: Manage post comments (CRUD). Handles comment creation, updates, deletion,
 *          and approval with support for nested comments (replies).
 * Endpoints:
 *   - GET    /api/comments                    : List all comments (with filters)
 *   - GET    /api/comments/{id}               : Get comment by id
 *   - GET    /api/posts/{postId}/comments     : Get top-level comments for a post
 *   - GET    /api/comments/{id}/replies       : Get replies for a comment
 *   - POST   /api/comments                    : Create a new comment or reply
 *   - PUT    /api/comments/{id}               : Update comment
 *   - DELETE /api/comments/{id}               : Delete comment
 *   - PATCH  /api/comments/{id}/show          : Show comment (IsApproved = true)
 *   - PATCH  /api/comments/{id}/hide          : Hide comment (IsApproved = false)
 */

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Keytietkiem.DTOs.Post;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using System.Security.Claims;

namespace Keytietkiem.Controllers
{
    /// <summary>
    /// Controller for managing post comments (CRUD operations).
    /// Handles comment creation, updates, deletion, and approval with support for nested comments (replies).
    /// </summary>
    [Route("api/comments")]
    [ApiController]
    [Authorize]
    public class PostCommentsController : ControllerBase
    {
        private readonly IPostService _postService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostCommentsController"/> class.
        /// </summary>
        /// <param name="postService">The post service for business logic operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when postService is null.</exception>
        public PostCommentsController(IPostService postService)
        {
            _postService = postService ?? throw new ArgumentNullException(nameof(postService));
        }

        /// <summary>
        /// Retrieves all comments with optional filters.
        /// </summary>
        /// <param name="postId">Optional post identifier to filter by.</param>
        /// <param name="userId">Optional user identifier to filter by.</param>
        /// <param name="isApproved">Optional approval status to filter by.</param>
        /// <param name="parentCommentId">Optional parent comment identifier to filter by.</param>
        /// <returns>200 OK with list of comments.</returns>
    [HttpGet]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
    public async Task<IActionResult> GetComments(
            [FromQuery] Guid? postId,
            [FromQuery] Guid? userId,
            [FromQuery] bool? isApproved,
            [FromQuery] Guid? parentCommentId)
        {
            try
            {
                var comments = await _postService.GetCommentsByFilterAsync(postId, userId, isApproved, parentCommentId);
                return Ok(comments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a comment by its identifier with nested replies.
        /// </summary>
        /// <param name="id">The comment identifier.</param>
        /// <returns>200 OK with comment details, or 404 if not found.</returns>
    [HttpGet("{id}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
    public async Task<IActionResult> GetCommentById(Guid id)
        {
            try
            {
                var comment = await _postService.GetCommentByIdAsync(id);
                return Ok(comment);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves top-level comments for a specific post (ParentCommentId = NULL).
        /// </summary>
        /// <param name="postId">The post identifier.</param>
        /// <param name="page">Page number (default: 1).</param>
        /// <param name="pageSize">Page size (default: 20).</param>
        /// <returns>200 OK with list of top-level comments and their replies.</returns>
        [HttpGet("posts/{postId}/comments")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPostComments(
            Guid postId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _postService.GetCommentsByPostIdAsync(postId, page, pageSize);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves direct replies for a specific comment.
        /// </summary>
        /// <param name="id">The parent comment identifier.</param>
        /// <returns>200 OK with list of replies, or 404 if parent comment not found.</returns>
        [HttpGet("{id}/replies")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCommentReplies(Guid id)
        {
            try
            {
                var replies = await _postService.GetCommentRepliesAsync(id);
                return Ok(replies);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Creates a new comment or reply.
        /// </summary>
        /// <param name="createCommentDto">The comment creation data.</param>
        /// <returns>201 Created with the created comment, or 400/404 on validation errors.</returns>
        [HttpPost]
        public async Task<IActionResult> CreateComment([FromBody] CreatePostCommentDTO createCommentDto)
        {
            if (createCommentDto == null || string.IsNullOrWhiteSpace(createCommentDto.Content))
            {
                return BadRequest(new { message = "Nội dung phản hồi không được để trống." });
            }

            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var comment = await _postService.CreateCommentAsync(createCommentDto, actorId);
                return CreatedAtAction(nameof(GetCommentById), new { id = comment.CommentId }, comment);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing comment.
        /// </summary>
        /// <param name="id">The comment identifier.</param>
        /// <param name="updateCommentDto">The comment update data.</param>
        /// <returns>200 OK on success, or 400/404 on errors.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateComment(Guid id, [FromBody] UpdatePostCommentDTO updateCommentDto)
        {
            if (updateCommentDto == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(updateCommentDto.Content))
            {
                return BadRequest(new { message = "Nội dung phản hổi không được để trống." });
            }

            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var comment = await _postService.UpdateCommentAsync(id, updateCommentDto, actorId);
                return Ok(comment);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Deletes a comment and all its replies recursively.
        /// </summary>
        /// <param name="id">The comment identifier.</param>
        /// <returns>204 No Content on success, or 404 if not found.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(Guid id)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.DeleteCommentAsync(id, actorId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Shows a comment (sets IsApproved = true).
        /// </summary>
        /// <param name="id">The comment identifier.</param>
        /// <returns>200 OK on success, 400 if parent is hidden, or 404 if not found.</returns>
        [HttpPatch("{id}/show")]
        public async Task<IActionResult> ShowComment(Guid id)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.ShowCommentAsync(id, actorId);
                return Ok(new { message = "Phản hồi đã được hiển thị.", commentId = id });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Hides a comment (sets IsApproved = false).
        /// </summary>
        /// <param name="id">The comment identifier.</param>
        /// <returns>200 OK on success, or 404 if not found.</returns>
        [HttpPatch("{id}/hide")]
        public async Task<IActionResult> HideComment(Guid id)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.HideCommentAsync(id, actorId);
                return Ok(new { message = "Phản hồi đã bị ẩn.", commentId = id });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}

