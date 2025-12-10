/**
 * File: PermissionAuthorizationHandler.cs
 * Author: Keytietkiem Team
 * Created: 29/10/2025
 * Version: 1.0.0
 * Purpose: Authorization handler that checks if a user has a specific permission for a module.
 *          Always checks database to ensure real-time accuracy when permissions are changed.
 *          Uses requirement-based authorization with dynamic policy registration.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.Models;
using System.Security.Claims;

namespace Keytietkiem.Authorization
{
    /// <summary>
    /// Requirement for permission-based authorization
    /// </summary>
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string ModuleCode { get; }
        public string PermissionCode { get; }

        public PermissionRequirement(string moduleCode, string permissionCode)
        {
            ModuleCode = moduleCode?.Trim().ToUpper() ?? throw new ArgumentNullException(nameof(moduleCode));
            PermissionCode = permissionCode?.Trim().ToUpper() ?? throw new ArgumentNullException(nameof(permissionCode));
        }
    }

    /// <summary>
    /// Authorization handler that checks if user has the required permission.
    /// Always checks database to ensure real-time accuracy when permissions are changed.
    /// </summary>
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbContextFactory;

        public PermissionAuthorizationHandler(IDbContextFactory<KeytietkiemDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            // Check if user is authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return;
            }

            // Always check database for permissions to ensure real-time accuracy
            // JWT claims may be stale if permissions were changed after login
            // This ensures that permission changes take effect immediately without requiring re-login
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            // Get user ID from claims
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return;
            }

            // Get user's roles
            var user = await dbContext.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null || !user.Roles.Any())
            {
                return;
            }

            // Get role codes
            var roleCodes = user.Roles
                .Where(r => r.IsActive && !string.IsNullOrWhiteSpace(r.Code))
                .Select(r => r.Code!.Trim().ToUpper())
                .ToList();

            if (roleCodes.Count == 0)
            {
                return;
            }

            // Check if any role has the required permission
            var hasPermission = await dbContext.RolePermissions
                .Include(rp => rp.Role)
                .Include(rp => rp.Module)
                .Include(rp => rp.Permission)
                .AnyAsync(rp =>
                    rp.IsActive &&
                    rp.Role != null &&
                    rp.Module != null &&
                    rp.Permission != null &&
                    !string.IsNullOrWhiteSpace(rp.Role.Code) &&
                    roleCodes.Contains(rp.Role.Code.ToUpper()) &&
                    !string.IsNullOrWhiteSpace(rp.Module.Code) &&
                    rp.Module.Code.ToUpper() == requirement.ModuleCode &&
                    !string.IsNullOrWhiteSpace(rp.Permission.Code) &&
                    rp.Permission.Code.ToUpper() == requirement.PermissionCode);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
        }
    }
}

