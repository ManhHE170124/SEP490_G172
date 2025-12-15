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
    /// Each controller is mapped to a distinct module code.
    /// </summary>
    public static class ModuleCodes
    {
        public const string ACCOUNT = "ACCOUNT";
        public const string AUDIT_LOG = "AUDIT_LOG";
        public const string BADGE = "BADGE";
        public const string CATEGORY = "CATEGORY";
        public const string FAQ = "FAQ";
        public const string LAYOUT_SECTION = "LAYOUT_SECTION";
        public const string LICENSE_PACKAGE = "LICENSE_PACKAGE";
        public const string MODULE = "MODULE";
        public const string NOTIFICATION = "NOTIFICATION";
        public const string ORDER = "ORDER";
        public const string PAYMENT_GATEWAY = "PAYMENT_GATEWAY";
        public const string PAYMENT = "PAYMENT";
        public const string PERMISSION = "PERMISSION";
        public const string POST = "POST";
        public const string POST_COMMENT = "POST_COMMENT";
        public const string POST_IMAGE = "POST_IMAGE";
        public const string POST_TAG = "POST_TAG";
        public const string POST_TYPE = "POST_TYPE";
        public const string PRODUCT = "PRODUCT";
        public const string PRODUCT_VARIANT = "PRODUCT_VARIANT";
        public const string PRODUCT_VARIANT_IMAGE = "PRODUCT_VARIANT_IMAGE";
        public const string PRODUCT_SECTION = "PRODUCT_SECTION";
        public const string PRODUCT_ACCOUNT = "PRODUCT_ACCOUNT";
        public const string PRODUCT_KEY = "PRODUCT_KEY";
        public const string PRODUCT_REPORT = "PRODUCT_REPORT";
        public const string ROLE = "ROLE";
        public const string SLA_RULE_ADMIN = "SLA_RULE_ADMIN";
        public const string STOREFRONT_CART = "STOREFRONT_CART";
        public const string STOREFRONT_HOMEPAGE = "STOREFRONT_HOMEPAGE";
        public const string STOREFRONT_PRODUCT = "STOREFRONT_PRODUCT";
        public const string SUPPLIER = "SUPPLIER";
        public const string SUPPORT_CHAT = "SUPPORT_CHAT";
        public const string SUPPORT_DASHBOARD = "SUPPORT_DASHBOARD";
        public const string SUPPORT_PLAN = "SUPPORT_PLAN";
        public const string SUPPORT_PLAN_ADMIN = "SUPPORT_PLAN_ADMIN";
        public const string SUPPORT_PRIORITY_LOYALTY_RULE = "SUPPORT_PRIORITY_LOYALTY_RULE";
        public const string TAG = "TAG";
        public const string TICKET = "TICKET";
        public const string TICKET_REPLY = "TICKET_REPLY";
        public const string TICKET_SUBJECT_TEMPLATE = "TICKET_SUBJECT_TEMPLATE";
        public const string USER = "USER";
        public const string WEBSITE_SETTING = "WEBSITE_SETTING";

        /// <summary>
        /// Array of all valid module codes
        /// </summary>
        public static readonly string[] All = new[]
        {
            ACCOUNT,
            AUDIT_LOG,
            BADGE,
            CATEGORY,
            FAQ,
            LAYOUT_SECTION,
            LICENSE_PACKAGE,
            MODULE,
            NOTIFICATION,
            ORDER,
            PAYMENT_GATEWAY,
            PAYMENT,
            PERMISSION,
            POST,
            POST_COMMENT,
            POST_IMAGE,
            POST_TAG,
            POST_TYPE,
            PRODUCT,
            PRODUCT_VARIANT,
            PRODUCT_VARIANT_IMAGE,
            PRODUCT_SECTION,
            PRODUCT_ACCOUNT,
            PRODUCT_KEY,
            PRODUCT_REPORT,
            ROLE,
            SLA_RULE_ADMIN,
            STOREFRONT_CART,
            STOREFRONT_HOMEPAGE,
            STOREFRONT_PRODUCT,
            SUPPLIER,
            SUPPORT_CHAT,
            SUPPORT_DASHBOARD,
            SUPPORT_PLAN,
            SUPPORT_PLAN_ADMIN,
            SUPPORT_PRIORITY_LOYALTY_RULE,
            TAG,
            TICKET,
            TICKET_REPLY,
            TICKET_SUBJECT_TEMPLATE,
            USER,
            WEBSITE_SETTING
        };
    }
}

