/**
 * File: PermissionPolicyProvider.cs
 * Author: Keytietkiem Team
 * Created: 29/10/2025
 * Version: 1.0.0
 * Purpose: Dynamic policy provider that creates policies for RequirePermission attribute.
 *          Parses policy name "RequirePermission:MODULE_CODE:PERMISSION_CODE" and creates
 *          a policy with PermissionRequirement.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Keytietkiem.Authorization
{
    /// <summary>
    /// Policy provider that dynamically creates policies for RequirePermission attribute
    /// </summary>
    public class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
        }

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            // Check if this is a RequirePermission policy
            if (policyName != null && policyName.StartsWith("RequirePermission:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = policyName.Substring("RequirePermission:".Length).Split(':');
                if (parts.Length == 2)
                {
                    var moduleCode = parts[0].Trim().ToUpper();
                    var permissionCode = parts[1].Trim().ToUpper();

                    var policy = new AuthorizationPolicyBuilder()
                        .AddRequirements(new PermissionRequirement(moduleCode, permissionCode))
                        .Build();

                    return Task.FromResult<AuthorizationPolicy?>(policy);
                }
            }

            // Fall back to default policy provider
            return _fallbackPolicyProvider.GetPolicyAsync(policyName);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        {
            return _fallbackPolicyProvider.GetDefaultPolicyAsync();
        }

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        {
            return _fallbackPolicyProvider.GetFallbackPolicyAsync();
        }
    }
}

