/**
* File: DTOs/Users/UserDtos.cs
* Author: Keytietkiem Team
* Created: 2025-10-24
* Last Updated: 2025-10-24
* Version: 1.1.0
*
* Purpose:
*   Data Transfer Objects (DTOs) for the User Management feature of Keytietkiem.
*   These DTOs define the shape of data exchanged between the ASP.NET Core Web API
*   and the React frontend (admin-user-management). They are intentionally decoupled
*   from EF Core entities to:
*     - Limit over-posting/under-posting risks
*     - Enforce validation rules at the API boundary
*     - Hide internal domain details from external consumers
*
* Contents:
*   - UserListItemDto   : Compact projection used for paginated listing
*   - UserDetailDto     : Full-detail projection for view/edit dialogs
*   - UserCreateDto     : Payload for creating a new user (optional account creation)
*   - UserUpdateDto     : Payload for updating an existing user (optional password reset)
*
* Notes:
*   - Role filtering for any role containing the substring "admin" (case-insensitive)
*     must be handled in the controller/service layer. DTOs here only model data.
*   - Status values should align with the UserStatus enum (Active, Locked, Disabled).
*   - Timestamps are represented in UTC at the API layer (CreatedAt/UpdatedAt mapping).
*/

using System;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Users
{
    /// <summary>
    /// Represents a compact user row for list screens (paging, sorting, filtering).
    /// Optimized for table rendering and light-weight network usage.
    /// </summary>
    public class UserListItemDto
    {
        /// <summary>Unique identifier of the user.</summary>
        public Guid UserId { get; set; }

        /// <summary>Display name shown in list (usually FirstName + LastName).</summary>
        public string FullName { get; set; } = "";

        /// <summary>Primary email address (unique across the system).</summary>
        public string Email { get; set; } = "";

        /// <summary>Resolved role name for display (single primary role if applicable).</summary>
        public string? RoleName { get; set; }

        /// <summary>Timestamp of the user's last successful login (UTC).</summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>Current account status (Active | Locked | Disabled).</summary>
        public string Status { get; set; } = "";

        /// <summary>User creation timestamp (UTC).</summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Represents the full detail of a user used by the view/edit drawer or modal.
    /// Includes contact info, status, account flags and a single selected RoleId for UI binding.
    /// </summary>
    public class UserDetailDto
    {
        /// <summary>Unique identifier of the user.</summary>
        public Guid UserId { get; set; }

        /// <summary>User's first name.</summary>
        public string FirstName { get; set; } = "";

        /// <summary>User's last name.</summary>
        public string LastName { get; set; } = "";

        /// <summary>Computed display name for convenience (may mirror domain FullName).</summary>
        public string FullName { get; set; } = "";

        /// <summary>Primary email address (unique across the system).</summary>
        public string Email { get; set; } = "";

        /// <summary>Optional phone number.</summary>
        public string? Phone { get; set; }

        /// <summary>Optional address.</summary>
        public string? Address { get; set; }

        /// <summary>Current account status (Active | Locked | Disabled).</summary>
        public string Status { get; set; } = "Active";

        /// <summary>Timestamp of the user's last successful login (UTC).</summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Selected role identifier for the UI (string key).
        /// When null/empty, the user has no selectable non-admin role assigned.
        /// </summary>
        public string? RoleId { get; set; }

        /// <summary>
        /// Indicates whether an authentication account exists for this user.
        /// Useful for enabling/disabling password-related actions in the UI.
        /// </summary>
        public bool HasAccount { get; set; }

        /// <summary>
        /// Plain password materialized by the backend only for admin views that are explicitly allowed.
        /// In most cases this should be null for security; if present, FE must NEVER log or persist it.
        /// </summary>
        public string? PasswordPlain { get; set; }
    }

    /// <summary>
    /// Payload for creating a new user from the admin interface.
    /// If <see cref="NewPassword"/> is provided, an authentication account will be created.
    /// </summary>
    public class UserCreateDto
    {
        /// <summary>User's first name.</summary>
        [Required, StringLength(100)]
        public string FirstName { get; set; } = "";

        /// <summary>User's last name.</summary>
        [Required, StringLength(100)]
        public string LastName { get; set; } = "";

        /// <summary>Primary email address (unique across the system).</summary>
        [Required, EmailAddress, StringLength(255)]
        public string Email { get; set; } = "";

        /// <summary>Optional phone number.</summary>
        [Phone, StringLength(30)]
        public string? Phone { get; set; }

        /// <summary>Optional address.</summary>
        [StringLength(500)]
        public string? Address { get; set; }

        /// <summary>Initial status (defaults to Active).</summary>
        [Required, RegularExpression("Active|Locked|Disabled", ErrorMessage = "Status must be Active, Locked or Disabled")]
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Selected role identifier to assign. The API layer will reject any role whose name contains "admin".
        /// </summary>
        public string? RoleId { get; set; }

        /// <summary>
        /// Optional password to create an authentication account along with the user.
        /// When null/empty, only the profile is created without login credentials.
        /// </summary>
        [StringLength(200)]
        public string? NewPassword { get; set; }
    }

    /// <summary>
    /// Payload for updating an existing user. Fields without validation attributes are optional.
    /// If <see cref="NewPassword"/> is provided, the user's account password will be reset.
    /// </summary>
    public class UserUpdateDto
    {
        /// <summary>Unique identifier of the user being updated.</summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>User's first name.</summary>
        [Required, StringLength(100)]
        public string FirstName { get; set; } = "";

        /// <summary>Primary email address (unique across the system).</summary>
        [Required, EmailAddress, StringLength(255)]
        public string Email { get; set; } = "";

        /// <summary>User's last name.</summary>
        [Required, StringLength(100)]
        public string LastName { get; set; } = "";

        /// <summary>Optional phone number.</summary>
        [Phone, StringLength(30)]
        public string? Phone { get; set; }

        /// <summary>Optional address.</summary>
        [StringLength(500)]
        public string? Address { get; set; }

        /// <summary>Updated status (Active | Locked | Disabled).</summary>
        [Required, RegularExpression("Active|Locked|Disabled", ErrorMessage = "Status must be Active, Locked or Disabled")]
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Selected role identifier to assign. The API layer will reject any role whose name contains "admin".
        /// </summary>
        public string? RoleId { get; set; }

        /// <summary>
        /// Optional new password. When provided, the backend will reset the credential.
        /// </summary>
        [StringLength(200)]
        public string? NewPassword { get; set; }
    }
}
