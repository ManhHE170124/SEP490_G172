/**
 * File: PostCommentsController.cs
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
    [Route("api/comments")]
    [ApiController]
    [Authorize]
    public class PostCommentsController : ControllerBase
    {
        private readonly IPostService _postService;

        public PostCommentsController(IPostService postService)
        {
            _postService = postService ?? throw new ArgumentNullException(nameof(postService));
        }

        /**
         * Summary: Retrieve all comments with optional filters.
         * Route: GET /api/comments
         * Params: postId, userId, isApproved, parentCommentId (all optional query params)
         * Returns: 200 OK with list of comments
         */
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

        /**
         * Summary: Retrieve a comment by id with nested replies.
         * Route: GET /api/comments/{id}
         * Params: id (Guid) - comment identifier
         * Returns: 200 OK with comment, 404 if not found
         */
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

        /**
         * Summary: Get top-level comments for a specific post (ParentCommentId = NULL).
         * Route: GET /api/comments/posts/{postId}/comments
         * Params: postId (Guid) - post identifier
         * Returns: 200 OK with list of top-level comments and their replies
         */
        [HttpGet("posts/{postId}/comments")]
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

        /**
         * Summary: Get direct replies for a specific comment.
         * Route: GET /api/comments/{id}/replies
         * Params: id (Guid) - parent comment identifier
         * Returns: 200 OK with list of replies, 404 if parent comment not found
         */
        [HttpGet("{id}/replies")]
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

        /**
         * Summary: Delete a comment and all its replies recursively.
         * Route: DELETE /api/comments/{id}
         * Params: id (Guid)
         * Returns: 204 No Content, 404 if not found
         */
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

        /**
         * Summary: Show a comment (set IsApproved = true).
         * Route: PATCH /api/comments/{id}/show
         * Params: id (Guid)
         * Returns: 200 OK, 400 if parent is hidden, 404 if not found
         */
        [HttpPatch("{id}/show")]
        public async Task<IActionResult> ShowComment(Guid id)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.ShowCommentAsync(id, actorId);
                return Ok(new { message = "Comment đã được hiển thị.", commentId = id });
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

        /**
         * Summary: Hide a comment (set IsApproved = false).
         * Route: PATCH /api/comments/{id}/hide
         * Params: id (Guid)
         * Returns: 200 OK, 404 if not found
         */
        [HttpPatch("{id}/hide")]
        public async Task<IActionResult> HideComment(Guid id)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _postService.HideCommentAsync(id, actorId);
                return Ok(new { message = "Comment đã bị ẩn.", commentId = id });
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

