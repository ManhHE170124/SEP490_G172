// File: DTOs/NotificationDtos.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs
{
    public class NotificationAdminFilterDto
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// Search: Title/Message/CreatedByEmail/CreatedByFullName/Type/CorrelationId...
        /// </summary>
        [StringLength(200)]
        public string? Search { get; set; }

        // ✅ Explicit filters (FE uses these)
        [StringLength(50)]
        public string? Type { get; set; }

        [StringLength(254)]
        public string? CreatedByEmail { get; set; }

        /// <summary>
        /// Status filter (case-insensitive):
        /// - Active   : not archived, (no expiry OR expiry > now)
        /// - Expired  : not archived, expiry <= now
        /// - Archived : archivedAtUtc != null
        /// </summary>
        [StringLength(20)]
        public string? Status { get; set; }

        public byte? Severity { get; set; }
        public bool? IsSystemGenerated { get; set; }
        public bool? IsGlobal { get; set; }

        public DateTime? CreatedFromUtc { get; set; }
        public DateTime? CreatedToUtc { get; set; }

        /// <summary>
        /// SortBy (case-insensitive, accept aliases):
        /// - CreatedAtUtc, Title, Severity
        /// - Type
        /// - CreatedByEmail, CreatedByUserEmail
        /// - ReadCount, TotalTargetUsers
        /// - ExpiresAtUtc, ArchivedAtUtc
        /// </summary>
        public string? SortBy { get; set; } = "CreatedAtUtc";

        /// <summary>
        /// true = DESC, false = ASC.
        /// </summary>
        public bool SortDescending { get; set; } = true;
    }

    public class NotificationListItemDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public byte Severity { get; set; }

        public bool IsSystemGenerated { get; set; }
        public bool IsGlobal { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public Guid? CreatedByUserId { get; set; }

        // ✅ NEW: list hiển thị 2 dòng (FullName + Email)
        public string? CreatedByFullName { get; set; }

        // ✅ Backward compatible (old FE)
        public string? CreatedByUserEmail { get; set; }

        // ✅ New alias (preferred)
        public string? CreatedByEmail { get; set; }

        public string? RelatedEntityType { get; set; }
        public string? RelatedEntityId { get; set; }
        public string? RelatedUrl { get; set; }

        public int TotalTargetUsers { get; set; }
        public int ReadCount { get; set; }

        // Option A (optional)
        public string? Type { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime? ArchivedAtUtc { get; set; }

        public int TargetRolesCount { get; set; }
        public int TargetUsersCount { get; set; }
    }

    public class NotificationTargetRoleDto
    {
        public string RoleId { get; set; } = null!;
        public string? RoleName { get; set; }
    }

    public class NotificationRecipientDto
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// e.g. "Admin, Customer"
        /// </summary>
        public string RoleNames { get; set; } = string.Empty;

        public bool IsRead { get; set; }
    }

    /// <summary>
    /// Admin recipients paging/filter for detail modal.
    /// Pagination UI: "Trang trước / sau".
    /// </summary>
    public class NotificationRecipientsFilterDto
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 200)]
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// Optional search by recipient FullName/Email.
        /// </summary>
        [StringLength(200)]
        public string? Search { get; set; }

        /// <summary>
        /// Optional filter: true = only read, false = only unread.
        /// </summary>
        public bool? IsRead { get; set; }
    }

    public class NotificationRecipientsPagedResponseDto
    {
        public int TotalCount { get; set; }
        public IReadOnlyList<NotificationRecipientDto> Items { get; set; } = Array.Empty<NotificationRecipientDto>();
    }

    public class NotificationDetailDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public byte Severity { get; set; }

        public bool IsSystemGenerated { get; set; }
        public bool IsGlobal { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public Guid? CreatedByUserId { get; set; }

        public string? CreatedByFullName { get; set; }

        // ✅ Backward compatible
        public string? CreatedByUserEmail { get; set; }

        // ✅ Alias
        public string? CreatedByEmail { get; set; }

        public string? RelatedEntityType { get; set; }
        public string? RelatedEntityId { get; set; }
        public string? RelatedUrl { get; set; }

        public int TotalTargetUsers { get; set; }
        public int ReadCount { get; set; }
        public int UnreadCount { get; set; }

        public List<NotificationTargetRoleDto> TargetRoles { get; set; } = new();
        public List<NotificationRecipientDto> Recipients { get; set; } = new();

        // Option A (optional)
        public string? Type { get; set; }
        public string? DedupKey { get; set; }
        public string? CorrelationId { get; set; }
        public string? PayloadJson { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime? ArchivedAtUtc { get; set; }
    }

    public class NotificationUserFilterDto
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        public bool OnlyUnread { get; set; } = false;

        public byte? Severity { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }

        [StringLength(200)]
        public string? Search { get; set; }

        public string? SortBy { get; set; } = "CreatedAtUtc";
        public bool SortDescending { get; set; } = true;
    }

    public class NotificationUserListItemDto
    {
        public long NotificationUserId { get; set; }
        public int NotificationId { get; set; }

        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public byte Severity { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ReadAtUtc { get; set; }

        public bool IsSystemGenerated { get; set; }
        public bool IsGlobal { get; set; }

        public string? RelatedEntityType { get; set; }
        public string? RelatedEntityId { get; set; }
        public string? RelatedUrl { get; set; }

        // Option A (optional)
        public string? Type { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }

    public class CreateNotificationDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = null!;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = null!;

        [Range(0, 3)]
        public byte Severity { get; set; } = 0;

        public bool IsGlobal { get; set; } = false;

        public string? RelatedEntityType { get; set; }
        public string? RelatedEntityId { get; set; }

        [StringLength(1024)]
        public string? RelatedUrl { get; set; }

        public List<string>? TargetRoleIds { get; set; }
        public List<Guid>? TargetUserIds { get; set; }

        // ✅ Option A create fields (manual defaults in Controller)
        [StringLength(50)]
        public string? Type { get; set; } // default "Manual"

        [StringLength(64)]
        public string? CorrelationId { get; set; } // default generated

        [StringLength(200)]
        public string? DedupKey { get; set; } // optional (idempotency)

        public string? PayloadJson { get; set; } // optional (technical payload)

        public DateTime? ExpiresAtUtc { get; set; } // optional TTL
    }

    public class NotificationListResponseDto
    {
        public int TotalCount { get; set; }
        public IReadOnlyList<NotificationListItemDto> Items { get; set; } = Array.Empty<NotificationListItemDto>();
    }

    public class NotificationUserListResponseDto
    {
        public int TotalCount { get; set; }
        public IReadOnlyList<NotificationUserListItemDto> Items { get; set; } = Array.Empty<NotificationUserListItemDto>();
    }

    public class NotificationTargetRoleOptionDto
    {
        public string RoleId { get; set; } = string.Empty;
        public string? RoleName { get; set; }
    }

    public class NotificationTargetUserOptionDto
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public class NotificationManualTargetOptionsDto
    {
        public List<NotificationTargetRoleOptionDto> Roles { get; set; } = new();
        public List<NotificationTargetUserOptionDto> Users { get; set; } = new();
    }

    public class NotificationFilterOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class NotificationAdminFilterOptionsDto
    {
        public List<NotificationFilterOptionDto> Types { get; set; } = new();
        public List<NotificationFilterOptionDto> Creators { get; set; } = new();
    }

    public class NotificationUnreadCountDto
    {
        public int UnreadCount { get; set; }
    }

    public class NotificationUpsertResultDto
    {
        public long NotificationUserId { get; set; }
        public int NotificationId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAtUtc { get; set; }
        public DateTime? DismissedAtUtc { get; set; }
    }
}
