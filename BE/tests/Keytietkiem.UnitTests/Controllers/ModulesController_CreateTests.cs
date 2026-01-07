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
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class ModulesController_CreateTests
    {
        private static ModulesController CreateController(
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
            var controller = new ModulesController(factory.CreateDbContext(), auditLogger)
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
        public async Task CreateModule_ShouldCreateRolePermissions_WhenValidData()
        {
            var roleId1 = "ROLE1";
            var roleId2 = "ROLE2";
            var permissionId1 = 1L;
            var permissionId2 = 2L;

            var controller = CreateController(
                "CreateModule_WithRolePermissions",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId1, "Role 1", "ROLE1"));
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId2, "Role 2", "ROLE2"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId1, "Permission 1", "PERM1"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId2, "Permission 2", "PERM2"));
                });

            var dto = new CreateModuleDTO
            {
                ModuleName = "New Module",
                Code = "NEW_MODULE",
                Description = "Test description"
            };

            var result = await controller.CreateModule(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<ModuleDTO>(created.Value);
            Assert.Equal("New Module", body.ModuleName);
            Assert.Equal("NEW_MODULE", body.Code);

            // Verify RolePermissions were created (2 roles * 2 permissions = 4 RolePermissions)
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("CreateModule_WithRolePermissions")
                    .Options);
            var module = await db.Modules
                .Include(m => m.RolePermissions)
                .FirstOrDefaultAsync(m => m.Code == "NEW_MODULE");
            Assert.NotNull(module);
            Assert.Equal(4, module.RolePermissions.Count);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnConflict_WhenModuleNameDuplicate()
        {
            var existingName = "Existing Module";

            var controller = CreateController(
                "CreateModule_DuplicateName",
                seed: db =>
                {
                    db.Modules.Add(TestDataBuilder.CreateModule(1, existingName, "EXISTING"));
                });

            var dto = new CreateModuleDTO
            {
                ModuleName = existingName,
                Code = "NEW_MODULE",
                Description = "Test"
            };

            var result = await controller.CreateModule(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Tên module đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task CreateModule_ShouldReturnConflict_WhenCodeDuplicate()
        {
            var existingCode = "EXISTING_CODE";

            var controller = CreateController(
                "CreateModule_DuplicateCode",
                seed: db =>
                {
                    db.Modules.Add(TestDataBuilder.CreateModule(1, "Existing", existingCode));
                });

            var dto = new CreateModuleDTO
            {
                ModuleName = "New Module",
                Code = existingCode,
                Description = "Test"
            };

            var result = await controller.CreateModule(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Mã module đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task CreateModule_ShouldReturnBadRequest_WhenNullDto()
        {
            var controller = CreateController("CreateModule_Null");

            var result = await controller.CreateModule(null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var controller = CreateController("CreateModule_InvalidModel");
            controller.ModelState.AddModelError("ModuleName", "ModuleName is required");

            var dto = new CreateModuleDTO
            {
                ModuleName = "",
                Code = "TEST"
            };

            var result = await controller.CreateModule(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // ModuleName validaion tests
        [Fact]
        public async Task CreateModule_ShouldReturnBadRequest_WhenModuleNameLengthLessThan2()
        {
            var controller = CreateController("CreateModule_ModuleNameTooShort");

            var dto = new CreateModuleDTO
            {
                ModuleName = "A", // < 2 characters
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

            var result = await controller.CreateModule(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnCreated_WhenModuleNameLengthEquals2()
        {
            var controller = CreateController("CreateModule_ModuleNameLength2");

            var dto = new CreateModuleDTO
            {
                ModuleName = "AB", // Exactly 2 characters
                Code = "VALID_CODE"
            };

            var result = await controller.CreateModule(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<ModuleDTO>(created.Value);
            Assert.Equal(2, body.ModuleName.Length);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnCreated_WhenModuleNameLengthEquals80()
        {
            var controller = CreateController("CreateModule_ModuleNameLength80");

            var dto = new CreateModuleDTO
            {
                ModuleName = new string('A', 80), // Exactly 80 characters
                Code = "VALID_CODE"
            };

            var result = await controller.CreateModule(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<ModuleDTO>(created.Value);
            Assert.Equal(80, body.ModuleName.Length);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnBadRequest_WhenModuleNameLengthGreaterThan80()
        {
            var controller = CreateController("CreateModule_ModuleNameTooLong");

            var dto = new CreateModuleDTO
            {
                ModuleName = new string('A', 81), // > 80 characters
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

            var result = await controller.CreateModule(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Code validation tests
        [Fact]
        public async Task CreateModule_ShouldReturnBadRequest_WhenCodeLengthLessThan2()
        {
            var controller = CreateController("CreateModule_CodeTooShort");

            var dto = new CreateModuleDTO
            {
                ModuleName = "Valid Module Name",
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

            var result = await controller.CreateModule(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnCreated_WhenCodeLengthEquals2()
        {
            var controller = CreateController("CreateModule_CodeLength2");

            var dto = new CreateModuleDTO
            {
                ModuleName = "Valid Module Name",
                Code = "AB" // Exactly 2 characters
            };

            var result = await controller.CreateModule(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<ModuleDTO>(created.Value);
            Assert.Equal(2, body.Code?.Length);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnCreated_WhenCodeLengthEquals50()
        {
            var controller = CreateController("CreateModule_CodeLength50");

            var dto = new CreateModuleDTO
            {
                ModuleName = "Valid Module Name",
                Code = new string('A', 50) // Exactly 50 characters
            };

            var result = await controller.CreateModule(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<ModuleDTO>(created.Value);
            Assert.Equal(50, body.Code?.Length);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnBadRequest_WhenCodeLengthGreaterThan50()
        {
            var controller = CreateController("CreateModule_CodeTooLong");

            var dto = new CreateModuleDTO
            {
                ModuleName = "Valid Module Name",
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

            var result = await controller.CreateModule(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnBadRequest_WhenCodeInvalidFormat()
        {
            var controller = CreateController("CreateModule_CodeInvalidFormat");

            var dto = new CreateModuleDTO
            {
                ModuleName = "Valid Module Name",
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

            var result = await controller.CreateModule(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Description validation tests
        [Fact]
        public async Task CreateModule_ShouldReturnCreated_WhenDescriptionLengthEquals200()
        {
            var controller = CreateController("CreateModule_DescriptionLength200");

            var dto = new CreateModuleDTO
            {
                ModuleName = "Valid Module Name",
                Code = "VALID_CODE",
                Description = new string('A', 200) // Exactly 200 characters
            };

            var result = await controller.CreateModule(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<ModuleDTO>(created.Value);
            Assert.Equal(200, body.Description?.Length);
        }

        [Fact]
        public async Task CreateModule_ShouldReturnBadRequest_WhenDescriptionLengthGreaterThan200()
        {
            var controller = CreateController("CreateModule_DescriptionTooLong");

            var dto = new CreateModuleDTO
            {
                ModuleName = "Valid Module Name",
                Code = "VALID_CODE",
                Description = new string('A', 201) // > 200 characters
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

            var result = await controller.CreateModule(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }
    }
}

