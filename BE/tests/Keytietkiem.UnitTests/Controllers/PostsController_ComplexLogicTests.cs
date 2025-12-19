using System;
using System.Linq;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Post;
using Keytietkiem.Models;
using Keytietkiem.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class PostsController_ComplexLogicTests
    {
        private static PostsController CreateController(
            string databaseName,
            Action<KeytietkiemDbContext>? seed = null)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            var factory = new TestDbContextFactory(options);
            var photoService = MockPhotoService.CreateMock().SetupUploadSuccess().Object;

            using (var db = factory.CreateDbContext())
            {
                seed?.Invoke(db);
                db.SaveChanges();
            }

            return new PostsController(factory.CreateDbContext(), photoService);
        }

        [Fact]
        public async Task GetPostBySlug_ShouldIncrementViewCount_WhenValidSlugAndPublished()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var slug = "test-post-slug";

            var controller = CreateController(
                "GetPostBySlug_IncrementViewCount",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    var post = TestDataBuilder.CreatePost(postId, "Test Post", slug, authorId, postTypeId, "Published");
                    post.ViewCount = 5;
                    db.Posts.Add(post);
                });

            var result = await controller.GetPostBySlug(slug);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<PostDTO>(ok.Value);
            Assert.Equal(6, body.ViewCount); // Should be incremented from 5 to 6
        }

        [Fact]
        public async Task GetPostBySlug_ShouldReturnNotFound_WhenInvalidSlug()
        {
            var controller = CreateController("GetPostBySlug_InvalidSlug");

            var result = await controller.GetPostBySlug("non-existent-slug");

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetPostBySlug_ShouldReturnNotFound_WhenStatusNotPublished()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var slug = "draft-post";

            var controller = CreateController(
                "GetPostBySlug_NotPublished",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Draft Post", slug, authorId, postTypeId, "Draft"));
                });

            var result = await controller.GetPostBySlug(slug);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetRelatedPosts_ShouldReturnRelatedPosts_WhenSamePostType()
        {
            var currentPostId = Guid.NewGuid();
            var relatedPostId1 = Guid.NewGuid();
            var relatedPostId2 = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var otherPostTypeId = Guid.NewGuid();

            var controller = CreateController(
                "GetRelatedPosts_SameType",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId, "Type 1"));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(otherPostTypeId, "Type 2"));
                    db.Posts.Add(TestDataBuilder.CreatePost(currentPostId, "Current", "current", authorId, postTypeId, "Published"));
                    db.Posts.Add(TestDataBuilder.CreatePost(relatedPostId1, "Related 1", "related-1", authorId, postTypeId, "Published"));
                    db.Posts.Add(TestDataBuilder.CreatePost(relatedPostId2, "Related 2", "related-2", authorId, postTypeId, "Published"));
                    db.Posts.Add(TestDataBuilder.CreatePost(Guid.NewGuid(), "Other", "other", authorId, otherPostTypeId, "Published"));
                });

            var result = await controller.GetRelatedPosts(currentPostId, 3);

            var ok = Assert.IsType<OkObjectResult>(result);
            var posts = Assert.IsAssignableFrom<IEnumerable<PostListItemDTO>>(ok.Value).ToList();
            Assert.Equal(2, posts.Count);
            Assert.All(posts, p => Assert.Equal(postTypeId, p.PostTypeId));
            Assert.All(posts, p => Assert.NotEqual(currentPostId, p.PostId));
        }

        [Fact]
        public async Task GetRelatedPosts_ShouldReturnEmpty_WhenNoRelatedPosts()
        {
            var currentPostId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "GetRelatedPosts_Empty",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(currentPostId, "Current", "current", authorId, postTypeId, "Published"));
                });

            var result = await controller.GetRelatedPosts(currentPostId, 3);

            var ok = Assert.IsType<OkObjectResult>(result);
            var posts = Assert.IsAssignableFrom<IEnumerable<PostListItemDTO>>(ok.Value).ToList();
            Assert.Empty(posts);
        }

        [Fact]
        public async Task GetRelatedPosts_ShouldRespectLimit()
        {
            var currentPostId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "GetRelatedPosts_Limit",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(currentPostId, "Current", "current", authorId, postTypeId, "Published"));
                    for (int i = 1; i <= 5; i++)
                    {
                        db.Posts.Add(TestDataBuilder.CreatePost(
                            Guid.NewGuid(),
                            $"Related {i}",
                            $"related-{i}",
                            authorId,
                            postTypeId,
                            "Published"));
                    }
                });

            var result = await controller.GetRelatedPosts(currentPostId, 2);

            var ok = Assert.IsType<OkObjectResult>(result);
            var posts = Assert.IsAssignableFrom<IEnumerable<PostListItemDTO>>(ok.Value).ToList();
            Assert.Equal(2, posts.Count);
        }

        [Fact]
        public async Task CreatePosttype_ShouldReturnCreated_WhenValidData()
        {
            var controller = CreateController("CreatePosttype_Valid");

            var dto = new CreatePostTypeDTO
            {
                PostTypeName = "New Post Type",
                Description = "Test description"
            };

            var result = await controller.CreatePosttype(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostTypeDTO>(created.Value);
            Assert.Equal("New Post Type", body.PostTypeName);
            Assert.Equal("Test description", body.Description);
        }

        [Fact]
        public async Task CreatePosttype_ShouldReturnBadRequest_WhenNullDto()
        {
            var controller = CreateController("CreatePosttype_Null");

            var result = await controller.CreatePosttype(null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task UpdatePosttype_ShouldReturnNoContent_WhenValidUpdate()
        {
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "UpdatePosttype_Valid",
                seed: db =>
                {
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId, "Original", "Original desc"));
                });

            var dto = new UpdatePostTypeDTO
            {
                PostTypeName = "Updated Name",
                Description = "Updated description"
            };

            var result = await controller.UpdatePosttype(postTypeId, dto);

            Assert.IsType<NoContentResult>(result);

            // Verify update
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("UpdatePosttype_Valid")
                    .Options);
            var updated = await db.PostTypes.FindAsync(postTypeId);
            Assert.NotNull(updated);
            Assert.Equal("Updated Name", updated.PostTypeName);
            Assert.Equal("Updated description", updated.Description);
        }

        [Fact]
        public async Task UpdatePosttype_ShouldReturnNotFound_WhenPostTypeNotFound()
        {
            var invalidId = Guid.NewGuid();
            var controller = CreateController("UpdatePosttype_NotFound");

            var dto = new UpdatePostTypeDTO
            {
                PostTypeName = "Updated",
                Description = "Desc"
            };

            var result = await controller.UpdatePosttype(invalidId, dto);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}

