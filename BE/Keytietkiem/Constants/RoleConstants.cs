/**
 * File: RoleConstants.cs
 * Author: Keytietkiem Team
 * Created: 29/10/2025
 * Version: 1.0.0
 * Purpose: Centralized constants for roles, permissions, and modules.
 *          These constants are fixed and should not be changed without proper migration.
 */

namespace Keytietkiem.Constants
{
    /// <summary>
    /// Predefined role codes in the system. These are fixed constants.
    /// </summary>
    public static class RoleCodes
    {
        public const string ADMIN = "ADMIN";
        public const string CONTENT_CREATOR = "CONTENT_CREATOR";
        public const string CUSTOMER_CARE = "CUSTOMER_CARE";
        public const string STORAGE_STAFF = "STORAGE_STAFF";
        public const string CUSTOMER = "CUSTOMER";

        /// <summary>
        /// Array of all valid role codes
        /// </summary>
        public static readonly string[] All = new[]
        {
            ADMIN,
            CONTENT_CREATOR,
            CUSTOMER_CARE,
            STORAGE_STAFF,
            CUSTOMER
        };
    }

    /// <summary>
    /// Predefined permission codes in the system. These are fixed constants.
    /// </summary>
    public static class PermissionCodes
    {
        public const string CREATE = "CREATE";
        public const string EDIT = "EDIT";
        public const string DELETE = "DELETE";
        public const string VIEW_LIST = "VIEW_LIST";
        public const string VIEW_DETAIL = "VIEW_DETAIL";
        public const string ACCESS = "ACCESS";

        /// <summary>
        /// Array of all valid permission codes
        /// </summary>
        public static readonly string[] All = new[]
        {
            CREATE,
            EDIT,
            DELETE,
            VIEW_LIST,
            VIEW_DETAIL,
            ACCESS
        };
    }

    /// <summary>
    /// Predefined module codes in the system. These are fixed constants.
    /// </summary>
    public static class ModuleCodes
    {
        public const string PRODUCT_MANAGER = "PRODUCT_MANAGER";
        public const string USER_MANAGER = "USER_MANAGER";
        public const string SUPPORT_MANAGER = "SUPPORT_MANAGER";
        public const string POST_MANAGER = "POST_MANAGER";
        public const string ROLE_MANAGER = "ROLE_MANAGER";
        public const string SETTINGS_MANAGER = "SETTINGS_MANAGER";
        public const string WAREHOUSE_MANAGER = "WAREHOUSE_MANAGER";

        /// <summary>
        /// Array of all valid module codes
        /// </summary>
        public static readonly string[] All = new[]
        {
            PRODUCT_MANAGER,
            USER_MANAGER,
            SUPPORT_MANAGER,
            POST_MANAGER,
            ROLE_MANAGER,
            SETTINGS_MANAGER,
            WAREHOUSE_MANAGER
        };
    }
}

