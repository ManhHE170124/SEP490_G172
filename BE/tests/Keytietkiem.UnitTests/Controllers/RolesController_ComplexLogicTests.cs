using System;
using System.Collections.Generic;
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
    public class RolesController_ComplexLogicTests
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

        [Fact]
        public async Task UpdateRolePermissions_ShouldUpdateExisting_WhenValid()
        {
            var roleId = "ROLE1";
            var moduleId = 1L;
            var permissionId = 1L;

            var controller = CreateController(
                "UpdateRolePermissions_UpdateExisting",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Role 1", "ROLE1"));
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Module 1", "MOD1"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Permission 1", "PERM1"));
                    db.RolePermissions.Add(TestDataBuilder.CreateRolePermission(roleId, moduleId, permissionId, false));
                });

            var dto = new BulkRolePermissionUpdateDTO
            {
                RoleId = roleId,
                RolePermissions = new List<RolePermissionUpdateDTO>
                {
                    new RolePermissionUpdateDTO
                    {
                        RoleId = roleId,
                        ModuleId = moduleId,
                        PermissionId = permissionId,
                        IsActive = true
                    }
                }
            };

            var result = await controller.UpdateRolePermissions(roleId, dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<RolePermissionResponseDTO>(ok.Value);
            Assert.Single(body.RolePermissions);
            Assert.True(body.RolePermissions[0].IsActive);
        }

        [Fact]
        public async Task UpdateRolePermissions_ShouldCreateNew_WhenNotExists()
        {
            var roleId = "ROLE1";
            var moduleId = 1L;
            var permissionId = 1L;

            var controller = CreateController(
                "UpdateRolePermissions_CreateNew",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Role 1", "ROLE1"));
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Module 1", "MOD1"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Permission 1", "PERM1"));
                });

            var dto = new BulkRolePermissionUpdateDTO
            {
                RoleId = roleId,
                RolePermissions = new List<RolePermissionUpdateDTO>
                {
                    new RolePermissionUpdateDTO
                    {
                        RoleId = roleId,
                        ModuleId = moduleId,
                        PermissionId = permissionId,
                        IsActive = true
                    }
                }
            };

            var result = await controller.UpdateRolePermissions(roleId, dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<RolePermissionResponseDTO>(ok.Value);
            Assert.Single(body.RolePermissions);
        }

        [Fact]
        public async Task UpdateRolePermissions_ShouldReturnBadRequest_WhenRoleIdMismatch()
        {
            var roleId = "ROLE1";
            var controller = CreateController("UpdateRolePermissions_Mismatch");

            var dto = new BulkRolePermissionUpdateDTO
            {
                RoleId = "DIFFERENT_ROLE",
                RolePermissions = new List<RolePermissionUpdateDTO>()
            };

            var result = await controller.UpdateRolePermissions(roleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateRolePermissions_ShouldReturnNotFound_WhenRoleNotFound()
        {
            var invalidId = "INVALID_ROLE";
            var controller = CreateController("UpdateRolePermissions_NotFound");

            var dto = new BulkRolePermissionUpdateDTO
            {
                RoleId = invalidId,
                RolePermissions = new List<RolePermissionUpdateDTO>
                {
                    new RolePermissionUpdateDTO
                    {
                        RoleId = invalidId,
                        ModuleId = 1,
                        PermissionId = 1,
                        IsActive = true
                    }
                }
            };

            var result = await controller.UpdateRolePermissions(invalidId, dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldReturnHasAccessTrue_WhenValid()
        {
            var roleId = "ROLE1";
            var moduleId = 1L;
            var permissionId = 1L;

            var controller = CreateController(
                "CheckPermission_Valid",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Role 1", "ROLE1"));
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Module 1", "MOD1"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Permission 1", "PERM1"));
                    db.RolePermissions.Add(TestDataBuilder.CreateRolePermission(roleId, moduleId, permissionId, true));
                });

            var request = new CheckPermissionRequestDTO
            {
                RoleCode = "ROLE1",
                ModuleCode = "MOD1",
                PermissionCode = "PERM1"
            };

            var result = await controller.CheckPermission(request);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<CheckPermissionResponseDTO>(ok.Value);
            Assert.True(body.HasAccess);
        }

        [Fact]
        public async Task CheckPermission_ShouldReturnHasAccessFalse_WhenIsActiveFalse()
        {
            var roleId = "ROLE1";
            var moduleId = 1L;
            var permissionId = 1L;

            var controller = CreateController(
                "CheckPermission_Inactive",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Role 1", "ROLE1"));
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Module 1", "MOD1"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Permission 1", "PERM1"));
                    db.RolePermissions.Add(TestDataBuilder.CreateRolePermission(roleId, moduleId, permissionId, false));
                });

            var request = new CheckPermissionRequestDTO
            {
                RoleCode = "ROLE1",
                ModuleCode = "MOD1",
                PermissionCode = "PERM1"
            };

            var result = await controller.CheckPermission(request);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<CheckPermissionResponseDTO>(ok.Value);
            Assert.False(body.HasAccess);
        }

        [Fact]
        public async Task GetModuleAccessForRoles_ShouldReturnModules_WhenValid()
        {
            var roleId = "ROLE1";
            var moduleId = 1L;
            var permissionId = 1L;

            var controller = CreateController(
                "GetModuleAccess_Valid",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Role 1", "ROLE1"));
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Module 1", "MOD1"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Permission 1", "ACCESS"));
                    db.RolePermissions.Add(TestDataBuilder.CreateRolePermission(roleId, moduleId, permissionId, true));
                });

            var request = new ModuleAccessRequestDTO
            {
                RoleCodes = new List<string> { "ROLE1" },
                PermissionCode = "ACCESS"
            };

            var result = await controller.GetModuleAccessForRoles(request);

            var ok = Assert.IsType<OkObjectResult>(result);
            var modules = Assert.IsAssignableFrom<IEnumerable<ModuleAccessDTO>>(ok.Value).ToList();
            Assert.Single(modules);
        }

        [Fact]
        public async Task GetModuleAccessForRoles_ShouldReturnBadRequest_WhenEmptyRoleCodes()
        {
            var controller = CreateController("GetModuleAccess_Empty");

            var request = new ModuleAccessRequestDTO
            {
                RoleCodes = new List<string>()
            };

            var result = await controller.GetModuleAccessForRoles(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetUserPermissions_ShouldReturnPermissions_WhenValid()
        {
            var roleId = "ROLE1";
            var moduleId = 1L;
            var permissionId = 1L;

            var controller = CreateController(
                "GetUserPermissions_Valid",
                seed: db =>
                {
                    db.Roles.Add(TestDataBuilder.CreateRole(roleId, "Role 1", "ROLE1"));
                    db.Modules.Add(TestDataBuilder.CreateModule(moduleId, "Module 1", "MOD1"));
                    db.Permissions.Add(TestDataBuilder.CreatePermission(permissionId, "Permission 1", "PERM1"));
                    db.RolePermissions.Add(TestDataBuilder.CreateRolePermission(roleId, moduleId, permissionId, true));
                });

            var request = new UserPermissionsRequestDTO
            {
                RoleCodes = new List<string> { "ROLE1" }
            };

            var result = await controller.GetUserPermissions(request);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<UserPermissionsResponseDTO>(ok.Value);
            Assert.Single(body.Permissions);
        }
    }
}

