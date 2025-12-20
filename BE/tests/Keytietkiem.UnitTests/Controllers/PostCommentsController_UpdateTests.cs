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
    public class PostCommentsController_UpdateTests
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
        public async Task UpdateComment_ShouldReturnOk_WhenValidUpdate()
        {
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var controller = CreateController(
                "UpdateComment_Valid",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(commentId, postId, userId, null, "Original content", true));
                });

            var dto = new UpdatePostCommentDTO
            {
                Content = "Updated content"
            };

            var result = await controller.UpdateComment(commentId, dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<PostCommentDTO>(ok.Value);
            Assert.Equal("Updated content", body.Content);
        }

        [Fact]
        public async Task UpdateComment_ShouldUpdateIsApproved_WhenProvided()
        {
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var controller = CreateController(
                "UpdateComment_IsApproved",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(commentId, postId, userId, null, "Content", true));
                });

            var dto = new UpdatePostCommentDTO
            {
                Content = "Updated content",
                IsApproved = false
            };

            var result = await controller.UpdateComment(commentId, dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<PostCommentDTO>(ok.Value);
            Assert.False(body.IsApproved);
        }

        [Fact]
        public async Task UpdateComment_ShouldReturnNotFound_WhenCommentNotFound()
        {
            var invalidId = Guid.NewGuid();
            var controller = CreateController("UpdateComment_NotFound");

            var dto = new UpdatePostCommentDTO
            {
                Content = "Updated content"
            };

            var result = await controller.UpdateComment(invalidId, dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Comment không được tìm thấy", GetMessage(notFound) ?? "");
        }

        [Fact]
        public async Task UpdateComment_ShouldReturnBadRequest_WhenContentEmpty()
        {
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var controller = CreateController(
                "UpdateComment_EmptyContent",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(commentId, postId, userId, null, "Content", true));
                });

            var dto = new UpdatePostCommentDTO
            {
                Content = "   " // Empty/whitespace
            };

            var result = await controller.UpdateComment(commentId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Nội dung comment không được để trống", GetMessage(badRequest) ?? "");
        }

        [Fact]
        public async Task UpdateComment_ShouldReturnBadRequest_WhenNullDto()
        {
            var commentId = Guid.NewGuid();
            var controller = CreateController("UpdateComment_Null");

            var result = await controller.UpdateComment(commentId, null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Dữ liệu không hợp lệ", GetMessage(badRequest) ?? "");
        }
    }
}

