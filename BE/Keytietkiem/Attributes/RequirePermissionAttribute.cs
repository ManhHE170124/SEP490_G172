/**
 * File: RequirePermissionAttribute.cs
 * Author: Keytietkiem Team
 * Created: 29/10/2025
 * Version: 1.0.0
 * Purpose: Custom authorization attribute to check specific module and permission.
 *          Replaces hardcoded [Authorize(Roles = "...")] with permission-based checks.
 * Usage:
 *   [RequirePermission(ModuleCodes.POST_MANAGER, PermissionCodes.CREATE)]
 *   public async Task<IActionResult> CreatePost() { }
 */

using Microsoft.AspNetCore.Authorization;
using Keytietkiem.Authorization;

namespace Keytietkiem.Attributes
{
    /// <summary>
    /// Authorization attribute that requires a specific permission for a module.
    /// The permission is checked by PermissionAuthorizationHandler.
    /// </summary>
    public class RequirePermissionAttribute : AuthorizeAttribute
    {
        /// <summary>
        /// Module code that the user must have access to
        /// </summary>
        public string ModuleCode { get; }

        /// <summary>
        /// Permission code that the user must have (CREATE, EDIT, DELETE, VIEW_DETAIL, ACCESS)
        /// </summary>
        public string PermissionCode { get; }

        /// <summary>
        /// Creates a new RequirePermission attribute
        /// </summary>
        /// <param name="moduleCode">The module code (e.g., "POST_MANAGER")</param>
        /// <param name="permissionCode">The permission code (e.g., "CREATE", "EDIT", "DELETE", "VIEW_DETAIL", "ACCESS")</param>
        public RequirePermissionAttribute(string moduleCode, string permissionCode)
        {
            ModuleCode = moduleCode?.Trim().ToUpper() ?? throw new ArgumentNullException(nameof(moduleCode));
            PermissionCode = permissionCode?.Trim().ToUpper() ?? throw new ArgumentNullException(nameof(permissionCode));
            
            // Set policy name that will be used to dynamically create policy with PermissionRequirement
            Policy = $"RequirePermission:{ModuleCode}:{PermissionCode}";
        }
    }
}

