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
    public class RolesController_UpdateTests
    {
        private static RolesController CreateController(
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
            var controller = new RolesController(factory.CreateDbContext(), auditLogger)
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
        public async Task UpdateRole_ShouldReturnNoContent_WhenValidUpdate()
        {
            var roleId = "ROLE1";

            var controller = CreateController(
                "UpdateRole_Valid",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL", false, true));
                });

            var dto = new UpdateRoleDTO
            {
                Name = "Updated Role",
                Code = "UPDATED",
                IsActive = false
            };

            var result = await controller.UpdateRole(roleId, dto);

            Assert.IsType<NoContentResult>(result);

            // Verify update
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("UpdateRole_Valid")
                    .Options);
            var updated = await db.Roles.FindAsync(roleId);
            Assert.NotNull(updated);
            Assert.Equal("Updated Role", updated.Name);
            Assert.Equal("UPDATED", updated.Code);
            Assert.False(updated.IsActive);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnConflict_WhenCodeDuplicate()
        {
            var roleId = "ROLE1";
            var otherRoleId = "ROLE2";
            var existingCode = "EXISTING_CODE";

            var controller = CreateController(
                "UpdateRole_DuplicateCode",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL"));
                    db.Roles.Add(TestDataBuilder.CreateRole(otherRoleId, "Other", existingCode));
                });

            var dto = new UpdateRoleDTO
            {
                Name = "Updated",
                Code = existingCode,
                IsActive = true
            };

            var result = await controller.UpdateRole(roleId, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Mã vai trò đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnNotFound_WhenRoleNotFound()
        {
            var invalidId = "INVALID_ROLE";
            var controller = CreateController("UpdateRole_NotFound");

            var dto = new UpdateRoleDTO
            {
                Name = "Updated",
                Code = "UPDATED",
                IsActive = true
            };

            var result = await controller.UpdateRole(invalidId, dto);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnBadRequest_WhenNullDto()
        {
            var roleId = "ROLE1";
            var controller = CreateController("UpdateRole_Null");

            var result = await controller.UpdateRole(roleId, null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var roleId = "ROLE1";
            var controller = CreateController("UpdateRole_InvalidModel");
            controller.ModelState.AddModelError("Name", "Name is required");

            var dto = new UpdateRoleDTO
            {
                Name = "",
                Code = "TEST",
                IsActive = true
            };

            var result = await controller.UpdateRole(roleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Name validation tests
        [Fact]
        public async Task UpdateRole_ShouldReturnBadRequest_WhenNameLengthLessThan2()
        {
            var roleId = "ROLE1";
            var controller = CreateController(
                "UpdateRole_NameTooShort",
                seed: db => db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL")));

            var dto = new UpdateRoleDTO
            {
                Name = "A", // < 2 characters
                Code = "VALID_CODE",
                IsActive = true
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

            var result = await controller.UpdateRole(roleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnNoContent_WhenNameLengthEquals2()
        {
            var roleId = "ROLE1";
            var controller = CreateController(
                "UpdateRole_NameLength2",
                seed: db => db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL")));

            var dto = new UpdateRoleDTO
            {
                Name = "AB", // Exactly 2 characters
                Code = "VALID_CODE",
                IsActive = true
            };

            var result = await controller.UpdateRole(roleId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnNoContent_WhenNameLengthEquals60()
        {
            var roleId = "ROLE1";
            var controller = CreateController(
                "UpdateRole_NameLength60",
                seed: db => db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL")));

            var dto = new UpdateRoleDTO
            {
                Name = new string('A', 60), // Exactly 60 characters
                Code = "VALID_CODE",
                IsActive = true
            };

            var result = await controller.UpdateRole(roleId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnBadRequest_WhenNameLengthGreaterThan60()
        {
            var roleId = "ROLE1";
            var controller = CreateController(
                "UpdateRole_NameTooLong",
                seed: db => db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL")));

            var dto = new UpdateRoleDTO
            {
                Name = new string('A', 61), // > 60 characters
                Code = "VALID_CODE",
                IsActive = true
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

            var result = await controller.UpdateRole(roleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Code validation tests
        [Fact]
        public async Task UpdateRole_ShouldReturnBadRequest_WhenCodeLengthLessThan2()
        {
            var roleId = "ROLE1";
            var controller = CreateController(
                "UpdateRole_CodeTooShort",
                seed: db => db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL")));

            var dto = new UpdateRoleDTO
            {
                Name = "Valid Role Name",
                Code = "A", // < 2 characters
                IsActive = true
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

            var result = await controller.UpdateRole(roleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnNoContent_WhenCodeLengthEquals2()
        {
            var roleId = "ROLE1";
            var controller = CreateController(
                "UpdateRole_CodeLength2",
                seed: db => db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL")));

            var dto = new UpdateRoleDTO
            {
                Name = "Valid Role Name",
                Code = "AB", // Exactly 2 characters
                IsActive = true
            };

            var result = await controller.UpdateRole(roleId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnNoContent_WhenCodeLengthEquals50()
        {
            var roleId = "ROLE1";
            var controller = CreateController(
                "UpdateRole_CodeLength50",
                seed: db => db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL")));

            var dto = new UpdateRoleDTO
            {
                Name = "Valid Role Name",
                Code = new string('A', 50), // Exactly 50 characters
                IsActive = true
            };

            var result = await controller.UpdateRole(roleId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnBadRequest_WhenCodeLengthGreaterThan50()
        {
            var roleId = "ROLE1";
            var controller = CreateController(
                "UpdateRole_CodeTooLong",
                seed: db => db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL")));

            var dto = new UpdateRoleDTO
            {
                Name = "Valid Role Name",
                Code = new string('A', 51), // > 50 characters
                IsActive = true
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

            var result = await controller.UpdateRole(roleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdateRole_ShouldReturnBadRequest_WhenCodeInvalidFormat()
        {
            var roleId = "ROLE1";
            var controller = CreateController(
                "UpdateRole_CodeInvalidFormat",
                seed: db => db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Original", "ORIGINAL")));

            var dto = new UpdateRoleDTO
            {
                Name = "Valid Role Name",
                Code = "invalid-code", // Contains lowercase and dash
                IsActive = true
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

            var result = await controller.UpdateRole(roleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }
    }
}

