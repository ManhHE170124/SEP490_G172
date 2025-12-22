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
    public class TagsController_CreateTests
    {
        private static TagsController CreateController(
            string databaseName,
            Action<KeytietkiemDbContext>? seed = null)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var db = factory.CreateDbContext())
            {
                seed?.Invoke(db);
                db.SaveChanges();
            }

            return new TagsController(factory.CreateDbContext());
        }

        private static string? GetMessage(ObjectResult result)
        {
            var value = result.Value;
            var prop = value?.GetType().GetProperty("message");
            return prop?.GetValue(value)?.ToString();
        }

        [Fact]
        public async Task CreateTag_ShouldReturnCreated_WhenValidData()
        {
            var controller = CreateController("CreateTag_Valid");

            var dto = new CreateTagDTO
            {
                TagName = "New Tag",
                Slug = "new-tag"
            };

            var result = await controller.CreateTag(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<TagDTO>(created.Value);
            Assert.Equal("New Tag", body.TagName);
            Assert.Equal("new-tag", body.Slug);
        }

        [Fact]
        public async Task CreateTag_ShouldReturnConflict_WhenTagNameDuplicate()
        {
            var existingTagName = "Existing Tag";

            var controller = CreateController(
                "CreateTag_DuplicateName",
                seed: db =>
                {
                    db.Tags.Add(TestDataBuilder.CreateTag(Guid.NewGuid(), existingTagName, "existing-slug"));
                });

            var dto = new CreateTagDTO
            {
                TagName = existingTagName,
                Slug = "different-slug"
            };

            var result = await controller.CreateTag(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Tên thẻ đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task CreateTag_ShouldReturnConflict_WhenSlugDuplicate()
        {
            var existingSlug = "existing-slug";

            var controller = CreateController(
                "CreateTag_DuplicateSlug",
                seed: db =>
                {
                    db.Tags.Add(TestDataBuilder.CreateTag(Guid.NewGuid(), "Existing Tag", existingSlug));
                });

            var dto = new CreateTagDTO
            {
                TagName = "Different Tag",
                Slug = existingSlug
            };

            var result = await controller.CreateTag(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Slug đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task CreateTag_ShouldReturnBadRequest_WhenNullDto()
        {
            var controller = CreateController("CreateTag_Null");

            var result = await controller.CreateTag(null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task CreateTag_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var controller = CreateController("CreateTag_InvalidModel");
            controller.ModelState.AddModelError("TagName", "TagName is required");

            var dto = new CreateTagDTO
            {
                TagName = "",
                Slug = "test"
            };

            var result = await controller.CreateTag(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // TagName validation tests
        [Fact]
        public async Task CreateTag_ShouldReturnBadRequest_WhenTagNameLengthLessThan2()
        {
            var controller = CreateController("CreateTag_TagNameTooShort");

            var dto = new CreateTagDTO
            {
                TagName = "A", // < 2 characters
                Slug = "valid-slug-name"
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

            var result = await controller.CreateTag(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreateTag_ShouldReturnCreated_WhenTagNameLengthEquals2()
        {
            var controller = CreateController("CreateTag_TagNameLength2");

            var dto = new CreateTagDTO
            {
                TagName = "AB", // Exactly 2 characters
                Slug = "valid-slug-name"
            };

            var result = await controller.CreateTag(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<TagDTO>(created.Value);
            Assert.Equal(2, body.TagName.Length);
        }

        [Fact]
        public async Task CreateTag_ShouldReturnCreated_WhenTagNameLengthEquals100()
        {
            var controller = CreateController("CreateTag_TagNameLength100");

            var dto = new CreateTagDTO
            {
                TagName = new string('A', 100), // Exactly 100 characters
                Slug = "valid-slug-name"
            };

            var result = await controller.CreateTag(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<TagDTO>(created.Value);
            Assert.Equal(100, body.TagName.Length);
        }

        [Fact]
        public async Task CreateTag_ShouldReturnBadRequest_WhenTagNameLengthGreaterThan100()
        {
            var controller = CreateController("CreateTag_TagNameTooLong");

            var dto = new CreateTagDTO
            {
                TagName = new string('A', 101), // > 100 characters
                Slug = "valid-slug-name"
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

            var result = await controller.CreateTag(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Slug validation tests
        [Fact]
        public async Task CreateTag_ShouldReturnBadRequest_WhenSlugLengthLessThan2()
        {
            var controller = CreateController("CreateTag_SlugTooShort");

            var dto = new CreateTagDTO
            {
                TagName = "Valid Tag Name",
                Slug = "a" // < 2 characters
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

            var result = await controller.CreateTag(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreateTag_ShouldReturnCreated_WhenSlugLengthEquals2()
        {
            var controller = CreateController("CreateTag_SlugLength2");

            var dto = new CreateTagDTO
            {
                TagName = "Valid Tag Name",
                Slug = "ab" // Exactly 2 characters
            };

            var result = await controller.CreateTag(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<TagDTO>(created.Value);
            Assert.Equal(2, body.Slug.Length);
        }

        [Fact]
        public async Task CreateTag_ShouldReturnCreated_WhenSlugLengthEquals100()
        {
            var controller = CreateController("CreateTag_SlugLength100");

            var dto = new CreateTagDTO
            {
                TagName = "Valid Tag Name",
                Slug = new string('a', 100) // Exactly 100 characters
            };

            var result = await controller.CreateTag(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<TagDTO>(created.Value);
            Assert.Equal(100, body.Slug.Length);
        }

        [Fact]
        public async Task CreateTag_ShouldReturnBadRequest_WhenSlugLengthGreaterThan100()
        {
            var controller = CreateController("CreateTag_SlugTooLong");

            var dto = new CreateTagDTO
            {
                TagName = "Valid Tag Name",
                Slug = new string('a', 101) // > 100 characters
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

            var result = await controller.CreateTag(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreateTag_ShouldReturnBadRequest_WhenSlugInvalidFormat()
        {
            var controller = CreateController("CreateTag_SlugInvalidFormat");

            var dto = new CreateTagDTO
            {
                TagName = "Valid Tag Name",
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

            var result = await controller.CreateTag(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }
    }
}

