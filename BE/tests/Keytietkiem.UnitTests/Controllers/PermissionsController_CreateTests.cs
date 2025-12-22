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
    public class PermissionsController_CreateTests
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
        public async Task CreatePermission_ShouldCreateRolePermissions_WhenValidData()
        {
            var roleId1 = "ROLE1";
            var roleId2 = "ROLE2";
            var moduleId1 = 1L;
            var moduleId2 = 2L;

            var controller = CreateController(
                "CreatePermission_WithRolePermissions",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId1, "Role 1", "ROLE1"));
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId2, "Role 2", "ROLE2"));
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId1, "Module 1", "MOD1"));
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId2, "Module 2", "MOD2"));
                });

            var dto = new CreatePermissionDTO
            {
                PermissionName = "New Permission",
                Code = "NEW_PERMISSION",
                Description = "Test description"
            };

            var result = await controller.CreatePermission(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PermissionDTO>(created.Value);
            Assert.Equal("New Permission", body.PermissionName);
            Assert.Equal("NEW_PERMISSION", body.Code);

            // Verify RolePermissions were created (2 roles * 2 modules = 4 RolePermissions)
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("CreatePermission_WithRolePermissions")
                    .Options);
            var permission = await db.Permissions
                .Include(p => p.RolePermissions)
                .FirstOrDefaultAsync(p => p.Code == "NEW_PERMISSION");
            Assert.NotNull(permission);
            Assert.Equal(4, permission.RolePermissions.Count);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnConflict_WhenPermissionNameDuplicate()
        {
            var existingName = "Existing Permission";

            var controller = CreateController(
                "CreatePermission_DuplicateName",
                seed: db =>
                {
                    db.Permissions.Add(TestDataBuilder.CreatePermission(1, existingName, "EXISTING"));
                });

            var dto = new CreatePermissionDTO
            {
                PermissionName = existingName,
                Code = "NEW_PERMISSION",
                Description = "Test"
            };

            var result = await controller.CreatePermission(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Tên quyền đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnConflict_WhenCodeDuplicate()
        {
            var existingCode = "EXISTING_CODE";

            var controller = CreateController(
                "CreatePermission_DuplicateCode",
                seed: db =>
                {
                    db.Permissions.Add(TestDataBuilder.CreatePermission(1, "Existing", existingCode));
                });

            var dto = new CreatePermissionDTO
            {
                PermissionName = "New Permission",
                Code = existingCode,
                Description = "Test"
            };

            var result = await controller.CreatePermission(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Mã quyền đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnBadRequest_WhenNullDto()
        {
            var controller = CreateController("CreatePermission_Null");

            var result = await controller.CreatePermission(null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var controller = CreateController("CreatePermission_InvalidModel");
            controller.ModelState.AddModelError("PermissionName", "PermissionName is required");

            var dto = new CreatePermissionDTO
            {
                PermissionName = "",
                Code = "TEST"
            };

            var result = await controller.CreatePermission(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // PermissionName validation tests
        [Fact]
        public async Task CreatePermission_ShouldReturnBadRequest_WhenPermissionNameLengthLessThan2()
        {
            var controller = CreateController("CreatePermission_PermissionNameTooShort");

            var dto = new CreatePermissionDTO
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

            var result = await controller.CreatePermission(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnCreated_WhenPermissionNameLengthEquals2()
        {
            var controller = CreateController("CreatePermission_PermissionNameLength2");

            var dto = new CreatePermissionDTO
            {
                PermissionName = "AB", // Exactly 2 characters
                Code = "VALID_CODE"
            };

            var result = await controller.CreatePermission(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PermissionDTO>(created.Value);
            Assert.Equal(2, body.PermissionName.Length);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnCreated_WhenPermissionNameLengthEquals100()
        {
            var controller = CreateController("CreatePermission_PermissionNameLength100");

            var dto = new CreatePermissionDTO
            {
                PermissionName = new string('A', 100), // Exactly 100 characters
                Code = "VALID_CODE"
            };

            var result = await controller.CreatePermission(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PermissionDTO>(created.Value);
            Assert.Equal(100, body.PermissionName.Length);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnBadRequest_WhenPermissionNameLengthGreaterThan100()
        {
            var controller = CreateController("CreatePermission_PermissionNameTooLong");

            var dto = new CreatePermissionDTO
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

            var result = await controller.CreatePermission(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Code validation tests
        [Fact]
        public async Task CreatePermission_ShouldReturnBadRequest_WhenCodeLengthLessThan2()
        {
            var controller = CreateController("CreatePermission_CodeTooShort");

            var dto = new CreatePermissionDTO
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

            var result = await controller.CreatePermission(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnCreated_WhenCodeLengthEquals2()
        {
            var controller = CreateController("CreatePermission_CodeLength2");

            var dto = new CreatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = "AB" // Exactly 2 characters
            };

            var result = await controller.CreatePermission(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PermissionDTO>(created.Value);
            Assert.Equal(2, body.Code?.Length);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnCreated_WhenCodeLengthEquals50()
        {
            var controller = CreateController("CreatePermission_CodeLength50");

            var dto = new CreatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = new string('A', 50) // Exactly 50 characters
            };

            var result = await controller.CreatePermission(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PermissionDTO>(created.Value);
            Assert.Equal(50, body.Code?.Length);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnBadRequest_WhenCodeLengthGreaterThan50()
        {
            var controller = CreateController("CreatePermission_CodeTooLong");

            var dto = new CreatePermissionDTO
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

            var result = await controller.CreatePermission(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnBadRequest_WhenCodeInvalidFormat()
        {
            var controller = CreateController("CreatePermission_CodeInvalidFormat");

            var dto = new CreatePermissionDTO
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

            var result = await controller.CreatePermission(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Description validation tests
        [Fact]
        public async Task CreatePermission_ShouldReturnCreated_WhenDescriptionLengthEquals300()
        {
            var controller = CreateController("CreatePermission_DescriptionLength300");

            var dto = new CreatePermissionDTO
            {
                PermissionName = "Valid Permission Name",
                Code = "VALID_CODE",
                Description = new string('A', 300) // Exactly 300 characters
            };

            var result = await controller.CreatePermission(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<PermissionDTO>(created.Value);
            Assert.Equal(300, body.Description?.Length);
        }

        [Fact]
        public async Task CreatePermission_ShouldReturnBadRequest_WhenDescriptionLengthGreaterThan300()
        {
            var controller = CreateController("CreatePermission_DescriptionTooLong");

            var dto = new CreatePermissionDTO
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

            var result = await controller.CreatePermission(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }
    }
}

