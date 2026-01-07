using System;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Post;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.UnitTests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class PostCommentsController_CreateTests
    {
        private static PostCommentsController CreateController(
            string databaseName,
            Action<KeytietkiemDbContext>? seed = null)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            var factory = new TestDbContextFactory(options);
            var auditLogger = MockAuditLogger.CreateMock().Object;

            using (var db = factory.CreateDbContext())
            {
                seed?.Invoke(db);
                db.SaveChanges();
            }

            var httpContext = new DefaultHttpContext();
            var controller = new PostCommentsController(factory.CreateDbContext(), auditLogger)
            {
                ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
                {
                    HttpContext = httpContext
                }
            };

            return controller;
        }

        private static string? GetMessage(ObjectResult result)
        {
            var value = result.Value;
            var prop = value?.GetType().GetProperty("message");
            return prop?.GetValue(value)?.ToString();
        }

        [Fact]
        public async Task CreateComment_ShouldReturnCreated_WhenValidTopLevelComment()
        {
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var controller = CreateController(
                "CreateComment_TopLevel",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                });

            var dto = new CreatePostCommentDTO
            {
                PostId = postId,
                UserId = userId,
                Content = "Test comment content"
            };

            var result = await controller.CreateComment(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostCommentDTO>(created.Value);
            Assert.Equal("Test comment content", body.Content);
            Assert.Null(body.ParentCommentId);
            Assert.True(body.IsApproved);
        }

        [Fact]
        public async Task CreateComment_ShouldReturnCreated_WhenValidReply()
        {
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();

            var controller = CreateController(
                "CreateComment_Reply",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(parentCommentId, postId, userId, null, "Parent comment", true));
                });

            var dto = new CreatePostCommentDTO
            {
                PostId = postId,
                UserId = userId,
                ParentCommentId = parentCommentId,
                Content = "Reply content"
            };

            var result = await controller.CreateComment(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostCommentDTO>(created.Value);
            Assert.Equal("Reply content", body.Content);
            Assert.Equal(parentCommentId, body.ParentCommentId);
        }

        [Fact]
        public async Task CreateComment_ShouldReturnBadRequest_WhenReplyToHiddenParent()
        {
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();

            var controller = CreateController(
                "CreateComment_HiddenParent",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(parentCommentId, postId, userId, null, "Hidden parent", false));
                });

            var dto = new CreatePostCommentDTO
            {
                PostId = postId,
                UserId = userId,
                ParentCommentId = parentCommentId,
                Content = "Reply content"
            };

            var result = await controller.CreateComment(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Không thể trả lời bình luận đã bị ẩn", GetMessage(badRequest) ?? "");
        }

        [Fact]
        public async Task CreateComment_ShouldBecomeChildOfGrandparent_WhenReplyingToChild()
        {
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var rootCommentId = Guid.NewGuid();
            var childCommentId = Guid.NewGuid();

            var controller = CreateController(
                "CreateComment_ReplyToChild",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(rootCommentId, postId, userId, null, "Root", true));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(childCommentId, postId, userId, rootCommentId, "Child", true));
                });

            var dto = new CreatePostCommentDTO
            {
                PostId = postId,
                UserId = userId,
                ParentCommentId = childCommentId,
                Content = "Reply to child"
            };

            var result = await controller.CreateComment(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostCommentDTO>(created.Value);
            Assert.Equal(rootCommentId, body.ParentCommentId); // Should be child of grandparent
        }

        [Fact]
        public async Task CreateComment_ShouldReturnNotFound_WhenPostIdInvalid()
        {
            var invalidPostId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var controller = CreateController(
                "CreateComment_InvalidPost",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                });

            var dto = new CreatePostCommentDTO
            {
                PostId = invalidPostId,
                UserId = userId,
                Content = "Test comment"
            };

            var result = await controller.CreateComment(dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Bài viết không được tìm thấy", GetMessage(notFound) ?? "");
        }

        [Fact]
        public async Task CreateComment_ShouldReturnNotFound_WhenUserIdInvalid()
        {
            var postId = Guid.NewGuid();
            var invalidUserId = Guid.NewGuid();

            var controller = CreateController(
                "CreateComment_InvalidUser",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                });

            var dto = new CreatePostCommentDTO
            {
                PostId = postId,
                UserId = invalidUserId,
                Content = "Test comment"
            };

            var result = await controller.CreateComment(dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Người dùng không được tìm thấy", GetMessage(notFound) ?? "");
        }

        [Fact]
        public async Task CreateComment_ShouldReturnNotFound_WhenParentCommentIdInvalid()
        {
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var invalidParentId = Guid.NewGuid();

            var controller = CreateController(
                "CreateComment_InvalidParent",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                });

            var dto = new CreatePostCommentDTO
            {
                PostId = postId,
                UserId = userId,
                ParentCommentId = invalidParentId,
                Content = "Test comment"
            };

            var result = await controller.CreateComment(dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Comment cha không được tìm thấy", GetMessage(notFound) ?? "");
        }

        [Fact]
        public async Task CreateComment_ShouldReturnBadRequest_WhenParentCommentDifferentPost()
        {
            var postId = Guid.NewGuid();
            var otherPostId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();

            var controller = CreateController(
                "CreateComment_DifferentPost",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Post 1", "post-1"));
                    db.Posts.Add(TestDataBuilder.CreatePost(otherPostId, "Post 2", "post-2"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(parentCommentId, otherPostId, userId, null, "Parent", true));
                });

            var dto = new CreatePostCommentDTO
            {
                PostId = postId,
                UserId = userId,
                ParentCommentId = parentCommentId,
                Content = "Test comment"
            };

            var result = await controller.CreateComment(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Comment cha phải thuộc cùng một bài viết", GetMessage(badRequest) ?? "");
        }

        [Fact]
        public async Task CreateComment_ShouldReturnBadRequest_WhenContentEmpty()
        {
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var controller = CreateController(
                "CreateComment_EmptyContent",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                });

            var dto = new CreatePostCommentDTO
            {
                PostId = postId,
                UserId = userId,
                Content = "   " // Empty/whitespace
            };

            var result = await controller.CreateComment(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Nội dung comment không được để trống", GetMessage(badRequest) ?? "");
        }
    }
}

