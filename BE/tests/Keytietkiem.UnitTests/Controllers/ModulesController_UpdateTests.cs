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
    public class ModulesController_UpdateTests
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
        public async Task UpdateModule_ShouldReturnNoContent_WhenValidUpdate()
        {
            var moduleId = 1L;

            var controller = CreateController(
                "UpdateModule_Valid",
                seed: db =>
                {
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL", "Original desc"));
                });

            var dto = new UpdateModuleDTO
            {
                ModuleName = "Updated Module",
                Code = "UPDATED",
                Description = "Updated description"
            };

            var result = await controller.UpdateModule(moduleId, dto);

            Assert.IsType<NoContentResult>(result);

            // Verify update
            using var db = new KeytietkiemDbContext(
                new DbContextOptionsBuilder<KeytietkiemDbContext>()
                    .UseInMemoryDatabase("UpdateModule_Valid")
                    .Options);
            var updated = await db.Modules.FindAsync(moduleId);
            Assert.NotNull(updated);
            Assert.Equal("Updated Module", updated.ModuleName);
            Assert.Equal("UPDATED", updated.Code);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnConflict_WhenCodeDuplicate()
        {
            var moduleId = 1L;
            var otherModuleId = 2L;
            var existingCode = "EXISTING_CODE";

            var controller = CreateController(
                "UpdateModule_DuplicateCode",
                seed: db =>
                {
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL"));
                    db.Modules.Add(TestDataBuilder.CreateModule(otherModuleId, "Other", existingCode));
                });

            var dto = new UpdateModuleDTO
            {
                ModuleName = "Updated",
                Code = existingCode,
                Description = "Desc"
            };

            var result = await controller.UpdateModule(moduleId, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Mã module đã tồn tại", GetMessage(conflict) ?? "");
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnNotFound_WhenModuleNotFound()
        {
            var invalidId = 999L;
            var controller = CreateController("UpdateModule_NotFound");

            var dto = new UpdateModuleDTO
            {
                ModuleName = "Updated",
                Code = "UPDATED",
                Description = "Desc"
            };

            var result = await controller.UpdateModule(invalidId, dto);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnBadRequest_WhenNullDto()
        {
            var moduleId = 1L;
            var controller = CreateController("UpdateModule_Null");

            var result = await controller.UpdateModule(moduleId, null!);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Dữ liệu không hợp lệ.", badRequest.Value);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnBadRequest_WhenModelStateInvalid()
        {
            var moduleId = 1L;
            var controller = CreateController("UpdateModule_InvalidModel");
            controller.ModelState.AddModelError("ModuleName", "ModuleName is required");

            var dto = new UpdateModuleDTO
            {
                ModuleName = "",
                Code = "TEST"
            };

            var result = await controller.UpdateModule(moduleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // ModuleName validation tests
        [Fact]
        public async Task UpdateModule_ShouldReturnBadRequest_WhenModuleNameLengthLessThan2()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_ModuleNameTooShort",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
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

            var result = await controller.UpdateModule(moduleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnNoContent_WhenModuleNameLengthEquals2()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_ModuleNameLength2",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
            {
                ModuleName = "AB", // Exactly 2 characters
                Code = "VALID_CODE"
            };

            var result = await controller.UpdateModule(moduleId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnNoContent_WhenModuleNameLengthEquals80()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_ModuleNameLength80",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
            {
                ModuleName = new string('A', 80), // Exactly 80 characters
                Code = "VALID_CODE"
            };

            var result = await controller.UpdateModule(moduleId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnBadRequest_WhenModuleNameLengthGreaterThan80()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_ModuleNameTooLong",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
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

            var result = await controller.UpdateModule(moduleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Code validation tests
        [Fact]
        public async Task UpdateModule_ShouldReturnBadRequest_WhenCodeLengthLessThan2()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_CodeTooShort",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
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

            var result = await controller.UpdateModule(moduleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnNoContent_WhenCodeLengthEquals2()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_CodeLength2",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
            {
                ModuleName = "Valid Module Name",
                Code = "AB" // Exactly 2 characters
            };

            var result = await controller.UpdateModule(moduleId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnNoContent_WhenCodeLengthEquals50()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_CodeLength50",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
            {
                ModuleName = "Valid Module Name",
                Code = new string('A', 50) // Exactly 50 characters
            };

            var result = await controller.UpdateModule(moduleId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnBadRequest_WhenCodeLengthGreaterThan50()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_CodeTooLong",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
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

            var result = await controller.UpdateModule(moduleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnBadRequest_WhenCodeInvalidFormat()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_CodeInvalidFormat",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
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

            var result = await controller.UpdateModule(moduleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        // Description validation tests
        [Fact]
        public async Task UpdateModule_ShouldReturnNoContent_WhenDescriptionLengthEquals200()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_DescriptionLength200",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
            {
                ModuleName = "Valid Module Name",
                Code = "VALID_CODE",
                Description = new string('A', 200) // Exactly 200 characters
            };

            var result = await controller.UpdateModule(moduleId, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateModule_ShouldReturnBadRequest_WhenDescriptionLengthGreaterThan200()
        {
            var moduleId = 1L;
            var controller = CreateController(
                "UpdateModule_DescriptionTooLong",
                seed: db => db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Original", "ORIGINAL")));

            var dto = new UpdateModuleDTO
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

            var result = await controller.UpdateModule(moduleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }
    }
}

