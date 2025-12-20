using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
    public class PostsController_UpdateTests
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

        private static string? GetMessage(ObjectResult result)
        {
            var value = result.Value;
            var prop = value?.GetType().GetProperty("message");
            return prop?.GetValue(value)?.ToString();
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnNoContent_WhenValidUpdateWithTagChanges()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var tagId1 = Guid.NewGuid();
            var tagId2 = Guid.NewGuid();
            var tagId3 = Guid.NewGuid();

            var controller = CreateController(
                "UpdatePost_ValidWithTags",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    var post = TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId);
                    var tag1 = TestDataBuilder.CreateTag(tagId1, "Tag 1", "tag-1");
                    post.Tags.Add(tag1);
                    db.Posts.Add(post);
                    db.Tags.Add(TestDataBuilder.CreateTag(tagId2, "Tag 2", "tag-2"));
                    db.Tags.Add(TestDataBuilder.CreateTag(tagId3, "Tag 3", "tag-3"));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Updated Title",
                Slug = "updated-slug",
                ShortDescription = "Updated description",
                Content = "Updated content",
                TagIds = new List<Guid> { tagId2, tagId3 }
            };

            var result = await controller.UpdatePost(postId, dto);

            Assert.IsType<NoContentResult>(result);

            // Verify update
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("UpdatePost_ValidWithTags")
                    .Options);
            var updatedPost = await db.Posts
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.PostId == postId);
            Assert.NotNull(updatedPost);
            Assert.Equal("Updated Title", updatedPost.Title);
            Assert.Equal(2, updatedPost.Tags.Count);
            Assert.Contains(updatedPost.Tags, t => t.TagId == tagId2);
            Assert.Contains(updatedPost.Tags, t => t.TagId == tagId3);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnNotFound_WhenPostTypeIdInvalid()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var invalidPostTypeId = Guid.NewGuid();

            var controller = CreateController(
                "UpdatePost_InvalidPostType",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test", "test", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Updated",
                Slug = "updated",
                PostTypeId = invalidPostTypeId
            };

            var result = await controller.UpdatePost(postId, dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Không tìm thấy danh mục bài viết", GetMessage(notFound) ?? "");
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenTagIdsInvalid()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var invalidTagId = Guid.NewGuid();

            var controller = CreateController(
                "UpdatePost_InvalidTags",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Test", "test", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Updated",
                Slug = "updated",
                TagIds = new List<Guid> { invalidTagId }
            };

            var result = await controller.UpdatePost(postId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Không tìm thấy thẻ nào được gán cho bài viết này", GetMessage(badRequest) ?? "");
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenSlugDuplicate()
        {
            var postId = Guid.NewGuid();
            var postId2 = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var existingSlug = "existing-slug";

            var controller = CreateController(
                "UpdatePost_DuplicateSlug",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Post 1", "post-1", authorId, postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId2, "Post 2", existingSlug, authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Updated",
                Slug = existingSlug
            };

            var result = await controller.UpdatePost(postId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Tiêu đề đã tồn tại", GetMessage(badRequest) ?? "");
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnNotFound_WhenPostNotFound()
        {
            var invalidPostId = Guid.NewGuid();

            var controller = CreateController("UpdatePost_NotFound");

            var dto = new UpdatePostDTO
            {
                Title = "Updated",
                Slug = "updated"
            };

            var result = await controller.UpdatePost(invalidPostId, dto);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenNullDto()
        {
            var postId = Guid.NewGuid();

            var controller = CreateController("UpdatePost_NullDto");

            var result = await controller.UpdatePost(postId, null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var postId = Guid.NewGuid();
            var controller = CreateController("UpdatePost_InvalidModel");
            controller.ModelState.AddModelError("Title", "Title is required");

            var dto = new UpdatePostDTO
            {
                Title = "",
                Slug = "test"
            };

            var result = await controller.UpdatePost(postId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Title validation tests
        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenTitleLengthLessThan10()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_TitleTooShort",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Short", // < 10 characters
                Slug = "test-post-slug"
            };

            // Validate using Data Annotations
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(dto, validationContext, validationResults, true);
            foreach (var error in validationResults)
            {
                foreach (var memberName in error.MemberNames)
                {
                    controller.ModelState.AddModelError(memberName, error.ErrorMessage ?? "");
                }
            }

            var result = await controller.UpdatePost(postId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnNoContent_WhenTitleLengthEquals10()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_TitleLength10",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = new string('A', 10), // Exactly 10 characters
                Slug = "test-post-slug"
            };

            var result = await controller.UpdatePost(postId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnNoContent_WhenTitleLengthEquals250()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_TitleLength250",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = new string('A', 250), // Exactly 250 characters
                Slug = "test-post-slug"
            };

            var result = await controller.UpdatePost(postId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenTitleLengthGreaterThan250()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_TitleTooLong",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = new string('A', 251), // > 250 characters
                Slug = "test-post-slug"
            };

            // Validate using Data Annotations
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(dto, validationContext, validationResults, true);
            foreach (var error in validationResults)
            {
                foreach (var memberName in error.MemberNames)
                {
                    controller.ModelState.AddModelError(memberName, error.ErrorMessage ?? "");
                }
            }

            var result = await controller.UpdatePost(postId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Slug validation tests
        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenSlugLengthLessThan10()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_SlugTooShort",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Test Post Title",
                Slug = "short" // < 10 characters
            };

            // Validate using Data Annotations
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(dto, validationContext, validationResults, true);
            foreach (var error in validationResults)
            {
                foreach (var memberName in error.MemberNames)
                {
                    controller.ModelState.AddModelError(memberName, error.ErrorMessage ?? "");
                }
            }

            var result = await controller.UpdatePost(postId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnNoContent_WhenSlugLengthEquals10()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_SlugLength10",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Test Post Title",
                Slug = new string('a', 10) // Exactly 10 characters
            };

            var result = await controller.UpdatePost(postId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnNoContent_WhenSlugLengthEquals250()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_SlugLength250",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Test Post Title",
                Slug = new string('a', 250) // Exactly 250 characters
            };

            var result = await controller.UpdatePost(postId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenSlugLengthGreaterThan250()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_SlugTooLong",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Test Post Title",
                Slug = new string('a', 251) // > 250 characters
            };

            // Validate using Data Annotations
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(dto, validationContext, validationResults, true);
            foreach (var error in validationResults)
            {
                foreach (var memberName in error.MemberNames)
                {
                    controller.ModelState.AddModelError(memberName, error.ErrorMessage ?? "");
                }
            }

            var result = await controller.UpdatePost(postId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenSlugInvalidFormat()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_SlugInvalidFormat",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Test Post Title",
                Slug = "Invalid Slug!" // Contains uppercase and special characters
            };

            // Validate using Data Annotations
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(dto, validationContext, validationResults, true);
            foreach (var error in validationResults)
            {
                foreach (var memberName in error.MemberNames)
                {
                    controller.ModelState.AddModelError(memberName, error.ErrorMessage ?? "");
                }
            }

            var result = await controller.UpdatePost(postId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // ShortDescription validation tests
        [Fact]
        public async Task UpdatePost_ShouldReturnNoContent_WhenShortDescriptionLengthEquals255()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_ShortDescLength255",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Test Post Title",
                Slug = "test-post-slug",
                ShortDescription = new string('A', 255) // Exactly 255 characters
            };

            var result = await controller.UpdatePost(postId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePost_ShouldReturnBadRequest_WhenShortDescriptionLengthGreaterThan255()
        {
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var controller = CreateController(
                "UpdatePost_ShortDescTooLong",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(postId, "Original Title", "original-slug", authorId, postTypeId));
                });

            var dto = new UpdatePostDTO
            {
                Title = "Test Post Title",
                Slug = "test-post-slug",
                ShortDescription = new string('A', 256) // > 255 characters
            };

            // Validate using Data Annotations
            var validationContext = new ValidationContext(dto);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(dto, validationContext, validationResults, true);
            foreach (var error in validationResults)
            {
                foreach (var memberName in error.MemberNames)
                {
                    controller.ModelState.AddModelError(memberName, error.ErrorMessage ?? "");
                }
            }

            var result = await controller.UpdatePost(postId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }
    }
}

