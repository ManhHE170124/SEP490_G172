/**
 * File: RequireRoleAttribute.cs
 * Author: Keytietkiem Team
 * Created: 2025
 * Purpose: Custom authorization attribute to check if user has specific role(s).
 *          Replaces the complex permission-based system with simple role-based checks.
 * Usage:
 *   [RequireRole("ADMIN")]
 *   [RequireRole("ADMIN", "STORAGE_STAFF")]
 *   [RequireRole(RoleGroups.STORAGE)]
 */

using Microsoft.AspNetCore.Authorization;

namespace Keytietkiem.Attributes
{
    /// <summary>
    /// Authorization attribute that requires the user to have at least one of the specified roles.
    /// Uses JWT claims for role checking - no database queries needed.
    /// </summary>
    public class RequireRoleAttribute : AuthorizeAttribute
    {
        /// <summary>
        /// The roles that are allowed to access the endpoint.
        /// User must have at least one of these roles.
        /// </summary>
        public string[] AllowedRoles { get; }

        /// <summary>
        /// Creates a new RequireRole attribute with specified roles.
        /// </summary>
        /// <param name="roles">One or more role codes (e.g., "ADMIN", "STORAGE_STAFF")</param>
        public RequireRoleAttribute(params string[] roles)
        {
            AllowedRoles = roles?.Select(r => r?.Trim().ToUpper() ?? "").Where(r => !string.IsNullOrEmpty(r)).ToArray() 
                ?? Array.Empty<string>();
            
            // Set policy name that will be used by RoleAuthorizationHandler
            Policy = $"RequireRole:{string.Join(",", AllowedRoles)}";
        }
    }
}

