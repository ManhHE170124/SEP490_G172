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

        [StringLength(200)]
        public string? Search { get; set; }

        public byte? Severity { get; set; }
        public bool? IsSystemGenerated { get; set; }
        public bool? IsGlobal { get; set; }

        public DateTime? CreatedFromUtc { get; set; }
        public DateTime? CreatedToUtc { get; set; }

        public string? SortBy { get; set; } = "CreatedAtUtc";
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
        public string? CreatedByUserEmail { get; set; }

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
        public string RoleNames { get; set; } = string.Empty;
        public bool IsRead { get; set; }
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
        public string? CreatedByUserEmail { get; set; }

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

        // Option A increased length
        [StringLength(1024)]
        public string? RelatedUrl { get; set; }

        public List<string>? TargetRoleIds { get; set; }
        public List<Guid>? TargetUserIds { get; set; }
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

    // ✅ New DTOs for new endpoints
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
