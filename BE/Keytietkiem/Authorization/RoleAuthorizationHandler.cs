/**
 * File: RoleAuthorizationHandler.cs
 * Author: Keytietkiem Team
 * Created: 2025
 * Purpose: Authorization handler that checks if user has required role(s).
 *          Uses JWT claims for role checking - no database queries needed.
 *          This is a simpler replacement for PermissionAuthorizationHandler.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Keytietkiem.Authorization
{
    /// <summary>
    /// Requirement for role-based authorization
    /// </summary>
    public class RoleRequirement : IAuthorizationRequirement
    {
        public string[] AllowedRoles { get; }

        public RoleRequirement(string[] roles)
        {
            AllowedRoles = roles?.Select(r => r?.Trim().ToUpper() ?? "").Where(r => !string.IsNullOrEmpty(r)).ToArray()
                ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Authorization handler that checks if user has at least one of the required roles.
    /// Uses JWT claims - no database queries needed for better performance.
    /// </summary>
    public class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            RoleRequirement requirement)
        {
            // Check if user is authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return Task.CompletedTask;
            }

            // Get user roles from claims
            var userRoles = context.User.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
                .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(r => r.Trim().ToUpper())
                .ToHashSet();

            // ADMIN has full access to everything
            if (userRoles.Contains("ADMIN"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Check if user has any of the allowed roles
            if (requirement.AllowedRoles.Any(role => userRoles.Contains(role)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Policy provider for dynamic role-based policies
    /// </summary>
    public class RolePolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

        public RolePolicyProvider(IOptions<AuthorizationOptions> options)
        {
            _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
            _fallbackPolicyProvider.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
            _fallbackPolicyProvider.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            // Handle RequireRole:ROLE1,ROLE2 format
            if (policyName.StartsWith("RequireRole:", StringComparison.OrdinalIgnoreCase))
            {
                var rolesStr = policyName.Substring("RequireRole:".Length);
                var roles = rolesStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

                var policy = new AuthorizationPolicyBuilder()
                    .AddRequirements(new RoleRequirement(roles))
                    .Build();

                return Task.FromResult<AuthorizationPolicy?>(policy);
            }

            return _fallbackPolicyProvider.GetPolicyAsync(policyName);
        }
    }
}

