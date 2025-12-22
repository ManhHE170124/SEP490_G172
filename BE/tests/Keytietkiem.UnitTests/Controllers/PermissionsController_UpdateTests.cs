using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Roles;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.UnitTests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class PermissionsController_UpdateTests
    {
        private static PermissionsController CreateController(
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
            var controller = new PermissionsController(factory.CreateDbContext(), auditLogger)
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
        public async Task UpdatePermission_ShouldReturnNoContent_WhenValidUpdate()
        {
            var permissionId = 1L;

            var controller = CreateController(
                "UpdatePermission_Valid",
                seed: db =>
                {
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL", "Original desc"));
                });

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Updated Permission",
                Code = "UPDATED",
                Description = "Updated description"
            };

            var result = await controller.UpdatePermission(permissionId, dto);

            Assert.IsType<NoContentResult>(result);

            // Verify update
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("UpdatePermission_Valid")
                    .Options);
            var updated = await db.Permissions.FindAsync(permissionId);
            Assert.NotNull(updated);
            Assert.Equal("Updated Permission", updated.PermissionName);
            Assert.Equal("UPDATED", updated.Code);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnConflict_WhenCodeDuplicate()
        {
            var permissionId = 1L;
            var otherPermissionId = 2L;
            var existingCode = "EXISTING_CODE";

            var controller = CreateController(
                "UpdatePermission_DuplicateCode",
                seed: db =>
                {
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(otherPermissionId, "Other", existingCode));
                });

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Updated",
                Code = existingCode,
                Description = "Desc"
            };

            var result = await controller.UpdatePermission(permissionId, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Mã quyền đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnNotFound_WhenPermissionNotFound()
        {
            var invalidId = 999L;
            var controller = CreateController("UpdatePermission_NotFound");

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Updated",
                Code = "UPDATED",
                Description = "Desc"
            };

            var result = await controller.UpdatePermission(invalidId, dto);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnBadRequest_WhenNullDto()
        {
            var permissionId = 1L;
            var controller = CreateController("UpdatePermission_Null");

            var result = await controller.UpdatePermission(permissionId, null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var permissionId = 1L;
            var controller = CreateController("UpdatePermission_InvalidModel");
            controller.ModelState.AddModelError("PermissionName", "PermissionName is required");

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "",
                Code = "TEST"
            };

            var result = await controller.UpdatePermission(permissionId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // PermissionName validation tests
        [Fact]
        public async Task UpdatePermission_ShouldReturnBadRequest_WhenPermissionNameLengthLessThan2()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_PermissionNameTooShort",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "A", // < 2 characters
                Code = "VALID_CODE"
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

            var result = await controller.UpdatePermission(permissionId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnNoContent_WhenPermissionNameLengthEquals2()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_PermissionNameLength2",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "AB", // Exactly 2 characters
                Code = "VALID_CODE"
            };

            var result = await controller.UpdatePermission(permissionId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnNoContent_WhenPermissionNameLengthEquals100()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_PermissionNameLength100",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = new string('A', 100), // Exactly 100 characters
                Code = "VALID_CODE"
            };

            var result = await controller.UpdatePermission(permissionId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnBadRequest_WhenPermissionNameLengthGreaterThan100()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_PermissionNameTooLong",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = new string('A', 101), // > 100 characters
                Code = "VALID_CODE"
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

            var result = await controller.UpdatePermission(permissionId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Code validation tests
        [Fact]
        public async Task UpdatePermission_ShouldReturnBadRequest_WhenCodeLengthLessThan2()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_CodeTooShort",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = "A" // < 2 characters
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

            var result = await controller.UpdatePermission(permissionId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnNoContent_WhenCodeLengthEquals2()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_CodeLength2",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = "AB" // Exactly 2 characters
            };

            var result = await controller.UpdatePermission(permissionId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnNoContent_WhenCodeLengthEquals50()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_CodeLength50",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = new string('A', 50) // Exactly 50 characters
            };

            var result = await controller.UpdatePermission(permissionId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnBadRequest_WhenCodeLengthGreaterThan50()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_CodeTooLong",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = new string('A', 51) // > 50 characters
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

            var result = await controller.UpdatePermission(permissionId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnBadRequest_WhenCodeInvalidFormat()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_CodeInvalidFormat",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = "invalid-code" // Contains lowercase and dash
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

            var result = await controller.UpdatePermission(permissionId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Description validation tests
        [Fact]
        public async Task UpdatePermission_ShouldReturnNoContent_WhenDescriptionLengthEquals300()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_DescriptionLength300",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = "VALID_CODE",
                Description = new string('A', 300) // Exactly 300 characters
            };

            var result = await controller.UpdatePermission(permissionId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdatePermission_ShouldReturnBadRequest_WhenDescriptionLengthGreaterThan300()
        {
            var permissionId = 1L;
            var controller = CreateController(
                "UpdatePermission_DescriptionTooLong",
                seed: db => db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Original", "ORIGINAL")));

            var dto = new UpdatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = "VALID_CODE",
                Description = new string('A', 301) // > 300 characters
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

            var result = await controller.UpdatePermission(permissionId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }
    }
}

