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
    public class RolesController_CreateTests
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
        public async Task CreateRole_ShouldCreateRolePermissions_WhenValidData()
        {
            var moduleId1 = 1L;
            var moduleId2 = 2L;
            var permissionId1 = 1L;
            var permissionId2 = 2L;

            var controller = CreateController(
                "CreateRole_WithRolePermissions",
                seed: db =>
                {
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId1, "Module 1", "MOD1"));
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId2, "Module 2", "MOD2"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId1, "Permission 1", "PERM1"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId2, "Permission 2", "PERM2"));
                });

            var dto = new CreateRoleDTO
            {
                Name = "New Role",
                Code = "NEW_ROLE",
                IsSystem = false
            };

            var result = await controller.CreateRole(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RoleDTO>(created.Value);
            Assert.Equal("New Role", body.Name);
            Assert.Equal("NEW_ROLE", body.Code);
            Assert.True(body.IsActive);

            // Verify RolePermissions were created (2 modules * 2 permissions = 4 RolePermissions)
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("CreateRole_WithRolePermissions")
                    .Options);
            var role = await db.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Code == "NEW_ROLE");
            Assert.NotNull(role);
            Assert.Equal(4, role.RolePermissions.Count);
        }

        [Fact]
        public async Task CreateRole_ShouldUseCodeAsRoleId_WhenRoleIdNotProvided()
        {
            var controller = CreateController("CreateRole_CodeAsRoleId");

            var dto = new CreateRoleDTO
            {
                Name = "New Role",
                Code = "NEW_ROLE",
                IsSystem = false
            };

            var result = await controller.CreateRole(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RoleDTO>(created.Value);
            Assert.Equal("NEW_ROLE", body.RoleId); // Should use Code as RoleId
        }

        [Fact]
        public async Task CreateRole_ShouldUseProvidedRoleId_WhenRoleIdProvided()
        {
            var customRoleId = "CUSTOM_ROLE_ID";

            var controller = CreateController("CreateRole_CustomRoleId");

            var dto = new CreateRoleDTO
            {
                RoleId = customRoleId,
                Name = "New Role",
                Code = "NEW_ROLE",
                IsSystem = false
            };

            var result = await controller.CreateRole(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RoleDTO>(created.Value);
            Assert.Equal(customRoleId, body.RoleId);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnConflict_WhenNameDuplicate()
        {
            var existingName = "Existing Role";

            var controller = CreateController(
                "CreateRole_DuplicateName",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole("ROLE1", existingName, "EXISTING"));
                });

            var dto = new CreateRoleDTO
            {
                Name = existingName,
                Code = "NEW_ROLE",
                IsSystem = false
            };

            var result = await controller.CreateRole(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Tên vai trò đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task CreateRole_ShouldReturnConflict_WhenCodeDuplicate()
        {
            var existingCode = "EXISTING_CODE";

            var controller = CreateController(
                "CreateRole_DuplicateCode",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole("ROLE1", "Existing", existingCode));
                });

            var dto = new CreateRoleDTO
            {
                Name = "New Role",
                Code = existingCode,
                IsSystem = false
            };

            var result = await controller.CreateRole(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Mã vai trò đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task CreateRole_ShouldReturnConflict_WhenRoleIdDuplicate()
        {
            var existingRoleId = "EXISTING_ROLE_ID";

            var controller = CreateController(
                "CreateRole_DuplicateRoleId",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(existingRoleId, "Existing", "EXISTING"));
                });

            var dto = new CreateRoleDTO
            {
                RoleId = existingRoleId,
                Name = "New Role",
                Code = "NEW_ROLE",
                IsSystem = false
            };

            var result = await controller.CreateRole(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("ID vai trò đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task CreateRole_ShouldReturnBadRequest_WhenNullDto()
        {
            var controller = CreateController("CreateRole_Null");

            var result = await controller.CreateRole(null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var controller = CreateController("CreateRole_InvalidModel");
            controller.ModelState.AddModelError("Name", "Name is required");

            var dto = new CreateRoleDTO
            {
                Name = "",
                Code = "TEST"
            };

            var result = await controller.CreateRole(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Name validation tests
        [Fact]
        public async Task CreateRole_ShouldReturnBadRequest_WhenNameLengthLessThan2()
        {
            var controller = CreateController("CreateRole_NameTooShort");

            var dto = new CreateRoleDTO
            {
                Name = "A", // < 2 characters
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

            var result = await controller.CreateRole(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnCreated_WhenNameLengthEquals2()
        {
            var controller = CreateController("CreateRole_NameLength2");

            var dto = new CreateRoleDTO
            {
                Name = "AB", // Exactly 2 characters
                Code = "VALID_CODE"
            };

            var result = await controller.CreateRole(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RoleDTO>(created.Value);
            Assert.Equal(2, body.Name.Length);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnCreated_WhenNameLengthEquals60()
        {
            var controller = CreateController("CreateRole_NameLength60");

            var dto = new CreateRoleDTO
            {
                Name = new string('A', 60), // Exactly 60 characters
                Code = "VALID_CODE"
            };

            var result = await controller.CreateRole(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RoleDTO>(created.Value);
            Assert.Equal(60, body.Name.Length);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnBadRequest_WhenNameLengthGreaterThan60()
        {
            var controller = CreateController("CreateRole_NameTooLong");

            var dto = new CreateRoleDTO
            {
                Name = new string('A', 61), // > 60 characters
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

            var result = await controller.CreateRole(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Code validation tests
        [Fact]
        public async Task CreateRole_ShouldReturnBadRequest_WhenCodeLengthLessThan2()
        {
            var controller = CreateController("CreateRole_CodeTooShort");

            var dto = new CreateRoleDTO
            {
                Name = "Valid Role Name",
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

            var result = await controller.CreateRole(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnCreated_WhenCodeLengthEquals2()
        {
            var controller = CreateController("CreateRole_CodeLength2");

            var dto = new CreateRoleDTO
            {
                Name = "Valid Role Name",
                Code = "AB" // Exactly 2 characters
            };

            var result = await controller.CreateRole(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RoleDTO>(created.Value);
            Assert.Equal(2, body.Code?.Length);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnCreated_WhenCodeLengthEquals50()
        {
            var controller = CreateController("CreateRole_CodeLength50");

            var dto = new CreateRoleDTO
            {
                Name = "Valid Role Name",
                Code = new string('A', 50) // Exactly 50 characters
            };

            var result = await controller.CreateRole(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RoleDTO>(created.Value);
            Assert.Equal(50, body.Code?.Length);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnBadRequest_WhenCodeLengthGreaterThan50()
        {
            var controller = CreateController("CreateRole_CodeTooLong");

            var dto = new CreateRoleDTO
            {
                Name = "Valid Role Name",
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

            var result = await controller.CreateRole(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnBadRequest_WhenCodeInvalidFormat()
        {
            var controller = CreateController("CreateRole_CodeInvalidFormat");

            var dto = new CreateRoleDTO
            {
                Name = "Valid Role Name",
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

            var result = await controller.CreateRole(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // RoleId validation tests (optional, max 50)
        [Fact]
        public async Task CreateRole_ShouldReturnCreated_WhenRoleIdLengthEquals50()
        {
            var controller = CreateController("CreateRole_RoleIdLength50");

            var dto = new CreateRoleDTO
            {
                RoleId = new string('A', 50), // Exactly 50 characters
                Name = "Valid Role Name",
                Code = "VALID_CODE"
            };

            var result = await controller.CreateRole(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<RoleDTO>(created.Value);
            Assert.Equal(50, body.RoleId.Length);
        }

        [Fact]
        public async Task CreateRole_ShouldReturnBadRequest_WhenRoleIdLengthGreaterThan50()
        {
            var controller = CreateController("CreateRole_RoleIdTooLong");

            var dto = new CreateRoleDTO
            {
                RoleId = new string('A', 51), // > 50 characters
                Name = "Valid Role Name",
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

            var result = await controller.CreateRole(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }
    }
}

