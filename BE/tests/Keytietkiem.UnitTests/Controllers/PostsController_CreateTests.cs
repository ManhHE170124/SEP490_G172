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
    public class PostsController_CreateTests
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
        public async Task CreatePost_ShouldReturnCreated_WhenValidDataWithTags()
        {
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var tagId1 = Guid.NewGuid();
            var tagId2 = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_ValidData",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Tags.Add(TestDataBuilder.CreateTag(tagId1, "Tag 1", "tag-1"));
                    db.Tags.Add(TestDataBuilder.CreateTag(tagId2, "Tag 2", "tag-2"));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post Title",
                Slug = "test-post-title",
                ShortDescription = "Test description",
                Content = "Test content",
                PostTypeId = postTypeId,
                AuthorId = authorId,
                Status = "Published",
                TagIds = new List<Guid> { tagId1, tagId2 }
            };

            var result = await controller.CreatePost(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostDTO>(created.Value);
            Assert.Equal("Test Post Title", body.Title);
            Assert.Equal("test-post-title", body.Slug);
            Assert.Equal(2, body.Tags.Count);
            Assert.Equal("Published", body.Status);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnNotFound_WhenPostTypeIdInvalid()
        {
            var authorId = Guid.NewGuid();
            var invalidPostTypeId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_InvalidPostType",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post",
                Slug = "test-post",
                PostTypeId = invalidPostTypeId
            };

            var result = await controller.CreatePost(dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Danh mục bài viết không được tìm thấy", GetMessage(notFound) ?? "");
        }

        [Fact]
        public async Task CreatePost_ShouldReturnNotFound_WhenAuthorIdInvalid()
        {
            var postTypeId = Guid.NewGuid();
            var invalidAuthorId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_InvalidAuthor",
                seed: db =>
                {
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post",
                Slug = "test-post",
                PostTypeId = postTypeId,
                AuthorId = invalidAuthorId
            };

            var result = await controller.CreatePost(dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Không tìm thấy thông tin tác giả", GetMessage(notFound) ?? "");
        }

        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenTagIdsInvalid()
        {
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var invalidTagId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_InvalidTags",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post",
                Slug = "test-post",
                PostTypeId = postTypeId,
                AuthorId = authorId,
                TagIds = new List<Guid> { invalidTagId }
            };

            var result = await controller.CreatePost(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Không tìm thấy thẻ nào được gán cho bài viết này", GetMessage(badRequest) ?? "");
        }

        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenSlugDuplicate()
        {
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();
            var existingSlug = "existing-slug";

            var controller = CreateController(
                "CreatePost_DuplicateSlug",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                    db.Posts.Add(TestDataBuilder.CreatePost(
                        Guid.NewGuid(),
                        "Existing Post",
                        existingSlug,
                        authorId,
                        postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = "New Post",
                Slug = existingSlug,
                PostTypeId = postTypeId,
                AuthorId = authorId
            };

            var result = await controller.CreatePost(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Tiêu đề đã tồn tại", GetMessage(badRequest) ?? "");
        }

        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenNullDto()
        {
            var controller = CreateController("CreatePost_NullDto");

            var result = await controller.CreatePost(null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var controller = CreateController("CreatePost_InvalidModel");
            controller.ModelState.AddModelError("Title", "Title is required");

            var dto = new CreatePostDTO
            {
                Title = "", // Invalid
                Slug = "test"
            };

            var result = await controller.CreatePost(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreatePost_ShouldSetDefaultStatus_WhenStatusNotProvided()
        {
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_DefaultStatus",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post",
                Slug = "test-post",
                PostTypeId = postTypeId,
                AuthorId = authorId,
                Status = null // Not provided
            };

            var result = await controller.CreatePost(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostDTO>(created.Value);
            Assert.Equal("Draft", body.Status);
        }

        // Title validation tests
        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenTitleLengthLessThan10()
        {
            var controller = CreateController("CreatePost_TitleTooShort");

            var dto = new CreatePostDTO
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

            var result = await controller.CreatePost(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnCreated_WhenTitleLengthEquals10()
        {
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_TitleLength10",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = new string('A', 10), // Exactly 10 characters
                Slug = "test-post-slug",
                PostTypeId = postTypeId,
                AuthorId = authorId
            };

            var result = await controller.CreatePost(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostDTO>(created.Value);
            Assert.Equal(10, body.Title.Length);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnCreated_WhenTitleLengthEquals250()
        {
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_TitleLength250",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = new string('A', 250), // Exactly 250 characters
                Slug = "test-post-slug",
                PostTypeId = postTypeId,
                AuthorId = authorId
            };

            var result = await controller.CreatePost(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostDTO>(created.Value);
            Assert.Equal(250, body.Title.Length);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenTitleLengthGreaterThan250()
        {
            var controller = CreateController("CreatePost_TitleTooLong");

            var dto = new CreatePostDTO
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

            var result = await controller.CreatePost(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Slug validation tests
        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenSlugLengthLessThan10()
        {
            var controller = CreateController("CreatePost_SlugTooShort");

            var dto = new CreatePostDTO
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

            var result = await controller.CreatePost(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnCreated_WhenSlugLengthEquals10()
        {
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_SlugLength10",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post Title",
                Slug = new string('a', 10), // Exactly 10 characters
                PostTypeId = postTypeId,
                AuthorId = authorId
            };

            var result = await controller.CreatePost(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostDTO>(created.Value);
            Assert.Equal(10, body.Slug.Length);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnCreated_WhenSlugLengthEquals250()
        {
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_SlugLength250",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post Title",
                Slug = new string('a', 250), // Exactly 250 characters
                PostTypeId = postTypeId,
                AuthorId = authorId
            };

            var result = await controller.CreatePost(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostDTO>(created.Value);
            Assert.Equal(250, body.Slug.Length);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenSlugLengthGreaterThan250()
        {
            var controller = CreateController("CreatePost_SlugTooLong");

            var dto = new CreatePostDTO
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

            var result = await controller.CreatePost(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenSlugInvalidFormat()
        {
            var controller = CreateController("CreatePost_SlugInvalidFormat");

            var dto = new CreatePostDTO
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

            var result = await controller.CreatePost(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // ShortDescription validation tests
        [Fact]
        public async Task CreatePost_ShouldReturnCreated_WhenShortDescriptionLengthEquals255()
        {
            var authorId = Guid.NewGuid();
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_ShortDescLength255",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post Title",
                Slug = "test-post-slug",
                ShortDescription = new string('A', 255), // Exactly 255 characters
                PostTypeId = postTypeId,
                AuthorId = authorId
            };

            var result = await controller.CreatePost(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostDTO>(created.Value);
            Assert.Equal(255, body.ShortDescription?.Length);
        }

        [Fact]
        public async Task CreatePost_ShouldReturnBadRequest_WhenShortDescriptionLengthGreaterThan255()
        {
            var controller = CreateController("CreatePost_ShortDescTooLong");

            var dto = new CreatePostDTO
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

            var result = await controller.CreatePost(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // PostTypeId null test
        [Fact]
        public async Task CreatePost_ShouldReturnCreated_WhenPostTypeIdIsNull()
        {
            var authorId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_PostTypeIdNull",
                seed: db =>
                {
                    db.Users.Add(TestDataBuilder.CreateUser(authorId));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post Title",
                Slug = "test-post-slug",
                PostTypeId = null, // Null PostTypeId
                AuthorId = authorId
            };

            var result = await controller.CreatePost(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostDTO>(created.Value);
            Assert.Null(body.PostTypeId);
        }

        // AuthorId null test
        [Fact]
        public async Task CreatePost_ShouldReturnCreated_WhenAuthorIdIsNull()
        {
            var postTypeId = Guid.NewGuid();

            var controller = CreateController(
                "CreatePost_AuthorIdNull",
                seed: db =>
                {
                    db.PostTypes.Add(TestDataBuilder.CreatePostType(postTypeId));
                });

            var dto = new CreatePostDTO
            {
                Title = "Test Post Title",
                Slug = "test-post-slug",
                PostTypeId = postTypeId,
                AuthorId = null // Null AuthorId
            };

            var result = await controller.CreatePost(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PostDTO>(created.Value);
            Assert.Null(body.AuthorId);
        }
    }
}

