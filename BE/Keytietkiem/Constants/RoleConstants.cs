/**
 * File: RoleConstants.cs
 * Author: Keytietkiem Team
 * Created: 29/10/2025
 * Version: 1.0.0
 * Purpose: Centralized constants for roles.
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
}

