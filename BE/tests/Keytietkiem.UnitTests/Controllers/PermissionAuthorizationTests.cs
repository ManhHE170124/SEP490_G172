/**
 * File: PermissionAuthorizationTests.cs
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: Comprehensive unit tests for PermissionAuthorizationHandler.
 *          Tests all role x module x permission combinations to ensure
 *          authorization logic works correctly.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Keytietkiem.Authorization;
using Keytietkiem.Constants;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class PermissionAuthorizationTests
    {
        #region Helpers

        private KeytietkiemDbContext CreateDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new KeytietkiemDbContext(options);
        }

        private IDbContextFactory<KeytietkiemDbContext> CreateDbContextFactory(KeytietkiemDbContext dbContext)
        {
            var factoryMock = new Mock<IDbContextFactory<KeytietkiemDbContext>>();
            factoryMock.Setup(f => f.CreateDbContextAsync(default))
                .ReturnsAsync(dbContext);
            return factoryMock.Object;
        }

        private async Task SeedRolesAndPermissions(KeytietkiemDbContext db)
        {
            var now = DateTime.UtcNow;

            // Seed all roles
            var roles = new[]
            {
                new Role { RoleId = "ADMIN", Code = RoleCodes.ADMIN, Name = "Admin", IsActive = true, IsSystem = true, CreatedAt = now },
                new Role { RoleId = "CONTENT_CREATOR", Code = RoleCodes.CONTENT_CREATOR, Name = "Content Creator", IsActive = true, IsSystem = false, CreatedAt = now },
                new Role { RoleId = "CUSTOMER_CARE", Code = RoleCodes.CUSTOMER_CARE, Name = "Customer Care", IsActive = true, IsSystem = false, CreatedAt = now },
                new Role { RoleId = "STORAGE_STAFF", Code = RoleCodes.STORAGE_STAFF, Name = "Storage Staff", IsActive = true, IsSystem = false, CreatedAt = now },
                new Role { RoleId = "CUSTOMER", Code = RoleCodes.CUSTOMER, Name = "Customer", IsActive = true, IsSystem = false, CreatedAt = now }
            };
            db.Roles.AddRange(roles);
            await db.SaveChangesAsync();

            // Seed all modules
            var modules = ModuleCodes.All.Select(code => new Module
            {
                Code = code,
                ModuleName = code,
                Description = code,
                CreatedAt = now
            }).ToList();
            db.Modules.AddRange(modules);
            await db.SaveChangesAsync();

            // Seed all permissions
            var permissions = PermissionCodes.All.Select(code => new Permission
            {
                Code = code,
                PermissionName = code,
                Description = code,
                CreatedAt = now
            }).ToList();
            db.Permissions.AddRange(permissions);
            await db.SaveChangesAsync();

            // Get role and permission maps
            var roleMap = await db.Roles.ToDictionaryAsync(r => r.Code!.Trim().ToUpper());
            var moduleMap = await db.Modules.ToDictionaryAsync(m => m.Code!.Trim().ToUpper());
            var permissionMap = await db.Permissions.ToDictionaryAsync(p => p.Code!.Trim().ToUpper());

            // Admin gets all permissions for all modules
            var adminRole = roleMap[RoleCodes.ADMIN];
            foreach (var module in modules)
            {
                foreach (var permission in permissions)
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = adminRole.RoleId,
                        ModuleId = module.ModuleId,
                        PermissionId = permission.PermissionId,
                        IsActive = true
                    });
                }
            }

            // CONTENT_CREATOR: POST, POST_TAG, POST_TYPE, POST_COMMENT, POST_IMAGE
            var contentCreatorRole = roleMap[RoleCodes.CONTENT_CREATOR];
            var contentModules = new[] { ModuleCodes.POST, ModuleCodes.POST_TAG, ModuleCodes.POST_TYPE, ModuleCodes.POST_COMMENT, ModuleCodes.POST_IMAGE };
            foreach (var moduleCode in contentModules)
            {
                if (!moduleMap.TryGetValue(moduleCode, out var module)) continue;
                foreach (var permission in permissions)
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = contentCreatorRole.RoleId,
                        ModuleId = module.ModuleId,
                        PermissionId = permission.PermissionId,
                        IsActive = true
                    });
                }
            }

            // CUSTOMER_CARE: TICKET, TICKET_REPLY, SUPPORT_CHAT, NOTIFICATION
            var customerCareRole = roleMap[RoleCodes.CUSTOMER_CARE];
            var careModules = new[] { ModuleCodes.TICKET, ModuleCodes.TICKET_REPLY, ModuleCodes.SUPPORT_CHAT, ModuleCodes.NOTIFICATION };
            foreach (var moduleCode in careModules)
            {
                if (!moduleMap.TryGetValue(moduleCode, out var module)) continue;
                foreach (var permission in permissions)
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = customerCareRole.RoleId,
                        ModuleId = module.ModuleId,
                        PermissionId = permission.PermissionId,
                        IsActive = true
                    });
                }
            }

            // STORAGE_STAFF: PRODUCT_KEY, PRODUCT_ACCOUNT, SUPPLIER, LICENSE_PACKAGE
            var storageRole = roleMap[RoleCodes.STORAGE_STAFF];
            var storageModules = new[] { ModuleCodes.PRODUCT_KEY, ModuleCodes.PRODUCT_ACCOUNT, ModuleCodes.SUPPLIER, ModuleCodes.LICENSE_PACKAGE };
            foreach (var moduleCode in storageModules)
            {
                if (!moduleMap.TryGetValue(moduleCode, out var module)) continue;
                foreach (var permission in permissions)
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = storageRole.RoleId,
                        ModuleId = module.ModuleId,
                        PermissionId = permission.PermissionId,
                        IsActive = true
                    });
                }
            }

            // CUSTOMER: No admin module permissions (only customer-facing endpoints)

            await db.SaveChangesAsync();
        }

        private ClaimsPrincipal CreateUserPrincipal(string userId, string roleCode)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, roleCode),
                new Claim("uid", userId)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            return new ClaimsPrincipal(identity);
        }

        private User CreateUserWithRole(KeytietkiemDbContext db, Guid userId, string roleCode)
        {
            var role = db.Roles.FirstOrDefault(r => r.Code == roleCode);
            if (role == null)
                throw new InvalidOperationException($"Role {roleCode} not found");

            var user = new User
            {
                UserId = userId,
                Email = $"{roleCode.ToLower()}@test.com",
                FullName = $"{roleCode} User",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                IsTemp = false
            };
            user.Roles.Add(role);
            db.Users.Add(user);
            return user;
        }

        #endregion

        #region Admin Bypass Tests

        [Fact(DisplayName = "Admin should bypass all permission checks")]
        public async Task PermissionAuthorizationHandler_AdminRole_BypassesAllChecks()
        {
            // Arrange
            var db = CreateDbContext("AdminBypassTest");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.ADMIN);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(ModuleCodes.USER, PermissionCodes.DELETE);
            var adminUser = CreateUserPrincipal(userId.ToString(), RoleCodes.ADMIN);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                adminUser,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.True(context.HasSucceeded, "Admin should bypass all permission checks");
        }

        [Theory(DisplayName = "Admin should have access to all modules and permissions")]
        [InlineData(ModuleCodes.PRODUCT, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.USER, PermissionCodes.DELETE)]
        [InlineData(ModuleCodes.ROLE, PermissionCodes.CREATE)]
        [InlineData(ModuleCodes.TICKET, PermissionCodes.EDIT)]
        [InlineData(ModuleCodes.POST, PermissionCodes.DELETE)]
        public async Task PermissionAuthorizationHandler_AdminRole_HasAccessToAllModules(
            string moduleCode,
            string permissionCode)
        {
            // Arrange
            var db = CreateDbContext($"AdminAccess_{moduleCode}_{permissionCode}");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.ADMIN);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(moduleCode, permissionCode);
            var adminUser = CreateUserPrincipal(userId.ToString(), RoleCodes.ADMIN);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                adminUser,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.True(context.HasSucceeded, $"Admin should have access to {moduleCode}:{permissionCode}");
        }

        #endregion

        #region Content Creator Tests

        [Theory(DisplayName = "Content Creator should have access to content modules")]
        [InlineData(ModuleCodes.POST, PermissionCodes.CREATE)]
        [InlineData(ModuleCodes.POST, PermissionCodes.EDIT)]
        [InlineData(ModuleCodes.POST, PermissionCodes.DELETE)]
        [InlineData(ModuleCodes.POST, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.POST_TAG, PermissionCodes.CREATE)]
        [InlineData(ModuleCodes.POST_TYPE, PermissionCodes.EDIT)]
        [InlineData(ModuleCodes.POST_COMMENT, PermissionCodes.DELETE)]
        [InlineData(ModuleCodes.POST_IMAGE, PermissionCodes.VIEW_DETAIL)]
        public async Task PermissionAuthorizationHandler_ContentCreator_HasAccessToContentModules(
            string moduleCode,
            string permissionCode)
        {
            // Arrange
            var db = CreateDbContext($"ContentCreator_{moduleCode}_{permissionCode}");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.CONTENT_CREATOR);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(moduleCode, permissionCode);
            var principal = CreateUserPrincipal(userId.ToString(), RoleCodes.CONTENT_CREATOR);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.True(context.HasSucceeded, $"Content Creator should have access to {moduleCode}:{permissionCode}");
        }

        [Theory(DisplayName = "Content Creator should NOT have access to non-content modules")]
        [InlineData(ModuleCodes.PRODUCT, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.USER, PermissionCodes.DELETE)]
        [InlineData(ModuleCodes.TICKET, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.PRODUCT_KEY, PermissionCodes.CREATE)]
        public async Task PermissionAuthorizationHandler_ContentCreator_NoAccessToNonContentModules(
            string moduleCode,
            string permissionCode)
        {
            // Arrange
            var db = CreateDbContext($"ContentCreator_Deny_{moduleCode}_{permissionCode}");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.CONTENT_CREATOR);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(moduleCode, permissionCode);
            var principal = CreateUserPrincipal(userId.ToString(), RoleCodes.CONTENT_CREATOR);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, $"Content Creator should NOT have access to {moduleCode}:{permissionCode}");
        }

        #endregion

        #region Customer Care Tests

        [Theory(DisplayName = "Customer Care should have access to support modules")]
        [InlineData(ModuleCodes.TICKET, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.TICKET, PermissionCodes.VIEW_DETAIL)]
        [InlineData(ModuleCodes.TICKET, PermissionCodes.EDIT)]
        [InlineData(ModuleCodes.TICKET_REPLY, PermissionCodes.CREATE)]
        [InlineData(ModuleCodes.SUPPORT_CHAT, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.NOTIFICATION, PermissionCodes.CREATE)]
        public async Task PermissionAuthorizationHandler_CustomerCare_HasAccessToSupportModules(
            string moduleCode,
            string permissionCode)
        {
            // Arrange
            var db = CreateDbContext($"CustomerCare_{moduleCode}_{permissionCode}");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.CUSTOMER_CARE);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(moduleCode, permissionCode);
            var principal = CreateUserPrincipal(userId.ToString(), RoleCodes.CUSTOMER_CARE);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.True(context.HasSucceeded, $"Customer Care should have access to {moduleCode}:{permissionCode}");
        }

        [Theory(DisplayName = "Customer Care should NOT have access to non-support modules")]
        [InlineData(ModuleCodes.PRODUCT, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.POST, PermissionCodes.CREATE)]
        [InlineData(ModuleCodes.PRODUCT_KEY, PermissionCodes.EDIT)]
        [InlineData(ModuleCodes.ROLE, PermissionCodes.VIEW_DETAIL)]
        public async Task PermissionAuthorizationHandler_CustomerCare_NoAccessToNonSupportModules(
            string moduleCode,
            string permissionCode)
        {
            // Arrange
            var db = CreateDbContext($"CustomerCare_Deny_{moduleCode}_{permissionCode}");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.CUSTOMER_CARE);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(moduleCode, permissionCode);
            var principal = CreateUserPrincipal(userId.ToString(), RoleCodes.CUSTOMER_CARE);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, $"Customer Care should NOT have access to {moduleCode}:{permissionCode}");
        }

        #endregion

        #region Storage Staff Tests

        [Theory(DisplayName = "Storage Staff should have access to storage modules")]
        [InlineData(ModuleCodes.PRODUCT_KEY, PermissionCodes.CREATE)]
        [InlineData(ModuleCodes.PRODUCT_KEY, PermissionCodes.EDIT)]
        [InlineData(ModuleCodes.PRODUCT_KEY, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.CREATE)]
        [InlineData(ModuleCodes.SUPPLIER, PermissionCodes.EDIT)]
        [InlineData(ModuleCodes.LICENSE_PACKAGE, PermissionCodes.VIEW_DETAIL)]
        public async Task PermissionAuthorizationHandler_StorageStaff_HasAccessToStorageModules(
            string moduleCode,
            string permissionCode)
        {
            // Arrange
            var db = CreateDbContext($"StorageStaff_{moduleCode}_{permissionCode}");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.STORAGE_STAFF);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(moduleCode, permissionCode);
            var principal = CreateUserPrincipal(userId.ToString(), RoleCodes.STORAGE_STAFF);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.True(context.HasSucceeded, $"Storage Staff should have access to {moduleCode}:{permissionCode}");
        }

        [Theory(DisplayName = "Storage Staff should NOT have access to non-storage modules")]
        [InlineData(ModuleCodes.TICKET, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.POST, PermissionCodes.CREATE)]
        [InlineData(ModuleCodes.USER, PermissionCodes.EDIT)]
        [InlineData(ModuleCodes.PRODUCT, PermissionCodes.VIEW_DETAIL)]
        public async Task PermissionAuthorizationHandler_StorageStaff_NoAccessToNonStorageModules(
            string moduleCode,
            string permissionCode)
        {
            // Arrange
            var db = CreateDbContext($"StorageStaff_Deny_{moduleCode}_{permissionCode}");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.STORAGE_STAFF);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(moduleCode, permissionCode);
            var principal = CreateUserPrincipal(userId.ToString(), RoleCodes.STORAGE_STAFF);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, $"Storage Staff should NOT have access to {moduleCode}:{permissionCode}");
        }

        #endregion

        #region Customer Tests

        [Theory(DisplayName = "Customer should NOT have access to admin modules")]
        [InlineData(ModuleCodes.PRODUCT, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.TICKET, PermissionCodes.VIEW_LIST)]
        [InlineData(ModuleCodes.POST, PermissionCodes.CREATE)]
        [InlineData(ModuleCodes.USER, PermissionCodes.EDIT)]
        [InlineData(ModuleCodes.ROLE, PermissionCodes.VIEW_DETAIL)]
        public async Task PermissionAuthorizationHandler_Customer_NoAccessToAdminModules(
            string moduleCode,
            string permissionCode)
        {
            // Arrange
            var db = CreateDbContext($"Customer_Deny_{moduleCode}_{permissionCode}");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.CUSTOMER);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(moduleCode, permissionCode);
            var principal = CreateUserPrincipal(userId.ToString(), RoleCodes.CUSTOMER);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, $"Customer should NOT have access to {moduleCode}:{permissionCode}");
        }

        #endregion

        #region Edge Cases

        [Fact(DisplayName = "Unauthenticated user should fail")]
        public async Task PermissionAuthorizationHandler_UnauthenticatedUser_Fails()
        {
            // Arrange
            var db = CreateDbContext("UnauthenticatedTest");
            await SeedRolesAndPermissions(db);

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(ModuleCodes.PRODUCT, PermissionCodes.VIEW_LIST);
            var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                unauthenticatedUser,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, "Unauthenticated user should not have access");
        }

        [Fact(DisplayName = "User without roles should fail")]
        public async Task PermissionAuthorizationHandler_UserWithoutRoles_Fails()
        {
            // Arrange
            var db = CreateDbContext("UserWithoutRolesTest");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = new User
            {
                UserId = userId,
                Email = "norole@test.com",
                FullName = "No Role User",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                IsTemp = false
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(ModuleCodes.PRODUCT, PermissionCodes.VIEW_LIST);
            var principal = CreateUserPrincipal(userId.ToString(), "");
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, "User without roles should not have access");
        }

        [Fact(DisplayName = "Inactive role permission should fail")]
        public async Task PermissionAuthorizationHandler_InactiveRolePermission_Fails()
        {
            // Arrange
            var db = CreateDbContext("InactivePermissionTest");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var user = CreateUserWithRole(db, userId, RoleCodes.CONTENT_CREATOR);
            await db.SaveChangesAsync();

            // Deactivate a permission
            var rolePermission = await db.RolePermissions
                .Include(rp => rp.Role)
                .Include(rp => rp.Module)
                .Include(rp => rp.Permission)
                .FirstOrDefaultAsync(rp =>
                    rp.Role!.Code == RoleCodes.CONTENT_CREATOR &&
                    rp.Module!.Code == ModuleCodes.POST &&
                    rp.Permission!.Code == PermissionCodes.CREATE);
            
            if (rolePermission != null)
            {
                rolePermission.IsActive = false;
                await db.SaveChangesAsync();
            }

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(ModuleCodes.POST, PermissionCodes.CREATE);
            var principal = CreateUserPrincipal(userId.ToString(), RoleCodes.CONTENT_CREATOR);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, "Inactive role permission should not grant access");
        }

        [Fact(DisplayName = "Inactive role should fail")]
        public async Task PermissionAuthorizationHandler_InactiveRole_Fails()
        {
            // Arrange
            var db = CreateDbContext("InactiveRoleTest");
            await SeedRolesAndPermissions(db);

            var userId = Guid.NewGuid();
            var role = db.Roles.First(r => r.Code == RoleCodes.CONTENT_CREATOR);
            role.IsActive = false;
            await db.SaveChangesAsync();

            var user = CreateUserWithRole(db, userId, RoleCodes.CONTENT_CREATOR);
            await db.SaveChangesAsync();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(ModuleCodes.POST, PermissionCodes.CREATE);
            var principal = CreateUserPrincipal(userId.ToString(), RoleCodes.CONTENT_CREATOR);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, "Inactive role should not grant access");
        }

        [Fact(DisplayName = "Invalid user ID should fail")]
        public async Task PermissionAuthorizationHandler_InvalidUserId_Fails()
        {
            // Arrange
            var db = CreateDbContext("InvalidUserIdTest");
            await SeedRolesAndPermissions(db);

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(ModuleCodes.PRODUCT, PermissionCodes.VIEW_LIST);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "INVALID_GUID")
            }, "Test"));
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, "Invalid user ID should not grant access");
        }

        [Fact(DisplayName = "User not found in database should fail")]
        public async Task PermissionAuthorizationHandler_UserNotFound_Fails()
        {
            // Arrange
            var db = CreateDbContext("UserNotFoundTest");
            await SeedRolesAndPermissions(db);

            var nonExistentUserId = Guid.NewGuid();

            var factory = CreateDbContextFactory(db);
            var handler = new PermissionAuthorizationHandler(factory);
            var requirement = new PermissionRequirement(ModuleCodes.PRODUCT, PermissionCodes.VIEW_LIST);
            var principal = CreateUserPrincipal(nonExistentUserId.ToString(), RoleCodes.ADMIN);
            var context = new AuthorizationHandlerContext(
                new[] { requirement },
                principal,
                null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            Assert.False(context.HasSucceeded, "User not found should not grant access");
        }

        #endregion
    }
}

