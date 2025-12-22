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
    public class TagsController_UpdateTests
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
        public async Task UpdateTag_ShouldReturnNoContent_WhenValidUpdate()
        {
            var tagId = Guid.NewGuid();

            var controller = CreateController(
                "UpdateTag_Valid",
                seed: db =>
                {
                    db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original Tag", "original-slug"));
                });

            var dto = new UpdateTagDTO
            {
                TagName = "Updated Tag",
                Slug = "updated-slug"
            };

            var result = await controller.UpdateTag(tagId, dto);

            Assert.IsType<NoContentResult>(result);

            // Verify update
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("UpdateTag_Valid")
                    .Options);
            var updated = await db.Tags.FindAsync(tagId);
            Assert.NotNull(updated);
            Assert.Equal("Updated Tag", updated.TagName);
            Assert.Equal("updated-slug", updated.Slug);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnConflict_WhenTagNameDuplicate()
        {
            var tagId = Guid.NewGuid();
            var otherTagId = Guid.NewGuid();
            var existingTagName = "Existing Tag";

            var controller = CreateController(
                "UpdateTag_DuplicateName",
                seed: db =>
                {
                    db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug"));
                    db.Tags.Add(TestDataBuilder.CreateTag(otherTagId, existingTagName, "existing-slug"));
                });

            var dto = new UpdateTagDTO
            {
                TagName = existingTagName,
                Slug = "updated-slug"
            };

            var result = await controller.UpdateTag(tagId, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Tên thẻ đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnConflict_WhenSlugDuplicate()
        {
            var tagId = Guid.NewGuid();
            var otherTagId = Guid.NewGuid();
            var existingSlug = "existing-slug";

            var controller = CreateController(
                "UpdateTag_DuplicateSlug",
                seed: db =>
                {
                    db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug"));
                    db.Tags.Add(TestDataBuilder.CreateTag(otherTagId, "Other Tag", existingSlug));
                });

            var dto = new UpdateTagDTO
            {
                TagName = "Updated Tag",
                Slug = existingSlug
            };

            var result = await controller.UpdateTag(tagId, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Slug trùng với thẻ đã có sẵn", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnNotFound_WhenTagNotFound()
        {
            var invalidId = Guid.NewGuid();
            var controller = CreateController("UpdateTag_NotFound");

            var dto = new UpdateTagDTO
            {
                TagName = "Updated",
                Slug = "updated"
            };

            var result = await controller.UpdateTag(invalidId, dto);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnBadRequest_WhenNullDto()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController("UpdateTag_Null");

            var result = await controller.UpdateTag(tagId, null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController("UpdateTag_InvalidModel");
            controller.ModelState.AddModelError("TagName", "TagName is required");

            var dto = new UpdateTagDTO
            {
                TagName = "",
                Slug = "test"
            };

            var result = await controller.UpdateTag(tagId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // TagName validation tests
        [Fact]
        public async Task UpdateTag_ShouldReturnBadRequest_WhenTagNameLengthLessThan2()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController(
                "UpdateTag_TagNameTooShort",
                seed: db => db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug")));

            var dto = new UpdateTagDTO
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

            var result = await controller.UpdateTag(tagId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnNoContent_WhenTagNameLengthEquals2()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController(
                "UpdateTag_TagNameLength2",
                seed: db => db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug")));

            var dto = new UpdateTagDTO
            {
                TagName = "AB", // Exactly 2 characters
                Slug = "valid-slug-name"
            };

            var result = await controller.UpdateTag(tagId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnNoContent_WhenTagNameLengthEquals100()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController(
                "UpdateTag_TagNameLength100",
                seed: db => db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug")));

            var dto = new UpdateTagDTO
            {
                TagName = new string('A', 100), // Exactly 100 characters
                Slug = "valid-slug-name"
            };

            var result = await controller.UpdateTag(tagId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnBadRequest_WhenTagNameLengthGreaterThan100()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController(
                "UpdateTag_TagNameTooLong",
                seed: db => db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug")));

            var dto = new UpdateTagDTO
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

            var result = await controller.UpdateTag(tagId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Slug validation tests
        [Fact]
        public async Task UpdateTag_ShouldReturnBadRequest_WhenSlugLengthLessThan2()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController(
                "UpdateTag_SlugTooShort",
                seed: db => db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug")));

            var dto = new UpdateTagDTO
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

            var result = await controller.UpdateTag(tagId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnNoContent_WhenSlugLengthEquals2()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController(
                "UpdateTag_SlugLength2",
                seed: db => db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug")));

            var dto = new UpdateTagDTO
            {
                TagName = "Valid Tag Name",
                Slug = "ab" // Exactly 2 characters
            };

            var result = await controller.UpdateTag(tagId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnNoContent_WhenSlugLengthEquals100()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController(
                "UpdateTag_SlugLength100",
                seed: db => db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug")));

            var dto = new UpdateTagDTO
            {
                TagName = "Valid Tag Name",
                Slug = new string('a', 100) // Exactly 100 characters
            };

            var result = await controller.UpdateTag(tagId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnBadRequest_WhenSlugLengthGreaterThan100()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController(
                "UpdateTag_SlugTooLong",
                seed: db => db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug")));

            var dto = new UpdateTagDTO
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

            var result = await controller.UpdateTag(tagId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdateTag_ShouldReturnBadRequest_WhenSlugInvalidFormat()
        {
            var tagId = Guid.NewGuid();
            var controller = CreateController(
                "UpdateTag_SlugInvalidFormat",
                seed: db => db.Tags.Add(TestDataBuilder.CreateTag(tagId, "Original", "original-slug")));

            var dto = new UpdateTagDTO
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

            var result = await controller.UpdateTag(tagId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }
    }
}

