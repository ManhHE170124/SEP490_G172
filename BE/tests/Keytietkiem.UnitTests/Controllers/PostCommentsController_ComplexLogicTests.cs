using System;
using System.Linq;
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
    public class PostCommentsController_ComplexLogicTests
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

        [Fact]
        public async Task GetPostComments_ShouldReturnPagedComments_WhenValid()
        {
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var rootCommentId = Guid.NewGuid();

            var controller = CreateController(
                "GetPostComments_Paged",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(rootCommentId, postId, userId, null, "Root comment", true));
                });

            var result = await controller.GetPostComments(postId, 1, 20);

            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value;
            var commentsProp = value?.GetType().GetProperty("comments");
            var comments = commentsProp?.GetValue(value) as System.Collections.Generic.IEnumerable<PostCommentDTO>;
            Assert.NotNull(comments);
            Assert.Single(comments);
        }

        [Fact]
        public async Task GetPostComments_ShouldReturnEmpty_WhenNoComments()
        {
            var postId = Guid.NewGuid();

            var controller = CreateController(
                "GetPostComments_Empty",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                });

            var result = await controller.GetPostComments(postId, 1, 20);

            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value;
            var commentsProp = value?.GetType().GetProperty("comments");
            var comments = commentsProp?.GetValue(value) as System.Collections.Generic.IEnumerable<PostCommentDTO>;
            Assert.NotNull(comments);
            Assert.Empty(comments);
        }

        [Fact]
        public async Task GetPostComments_ShouldReturnNotFound_WhenPostNotFound()
        {
            var invalidPostId = Guid.NewGuid();
            var controller = CreateController("GetPostComments_NotFound");

            var result = await controller.GetPostComments(invalidPostId, 1, 20);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task ShowComment_ShouldShowCommentAndReplies_WhenValid()
        {
            var commentId = Guid.NewGuid();
            var replyId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var controller = CreateController(
                "ShowComment_Recursive",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    var comment = TestDataBuilder.CreatePostComment(commentId, postId, userId, null, "Comment", false);
                    db.PostComments.Add(comment);
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(replyId, postId, userId, commentId, "Reply", false));
                });

            var result = await controller.ShowComment(commentId);

            var ok = Assert.IsType<OkObjectResult>(result);

            // Verify both comment and reply are shown
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("ShowComment_Recursive")
                    .Options);
            var comment = await db.PostComments.FindAsync(commentId);
            var reply = await db.PostComments.FindAsync(replyId);
            Assert.NotNull(comment);
            Assert.NotNull(reply);
            Assert.True(comment.IsApproved);
            Assert.True(reply.IsApproved);
        }

        [Fact]
        public async Task ShowComment_ShouldReturnBadRequest_WhenParentHidden()
        {
            var commentId = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var controller = CreateController(
                "ShowComment_ParentHidden",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(parentId, postId, userId, null, "Parent", false));
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(commentId, postId, userId, parentId, "Child", false));
                });

            var result = await controller.ShowComment(commentId);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task HideComment_ShouldHideCommentAndReplies_WhenValid()
        {
            var commentId = Guid.NewGuid();
            var replyId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var controller = CreateController(
                "HideComment_Recursive",
                seed: db =>
                {
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test Post", "test-post"));
                    db.Users.Add(TestDataBuilder.CreateUser(userId));
                    var comment = TestDataBuilder.CreatePostComment(commentId, postId, userId, null, "Comment", true);
                    db.PostComments.Add(comment);
                    db.PostComments.Add(TestDataBuilder.CreatePostComment(replyId, postId, userId, commentId, "Reply", true));
                });

            var result = await controller.HideComment(commentId);

            var ok = Assert.IsType<OkObjectResult>(result);

            // Verify both comment and reply are hidden
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("HideComment_Recursive")
                    .Options);
            var comment = await db.PostComments.FindAsync(commentId);
            var reply = await db.PostComments.FindAsync(replyId);
            Assert.NotNull(comment);
            Assert.NotNull(reply);
            Assert.False(comment.IsApproved);
            Assert.False(reply.IsApproved);
        }

        [Fact]
        public async Task HideComment_ShouldReturnNotFound_WhenCommentNotFound()
        {
            var invalidId = Guid.NewGuid();
            var controller = CreateController("HideComment_NotFound");

            var result = await controller.HideComment(invalidId);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}

