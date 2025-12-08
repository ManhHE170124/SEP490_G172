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
        /// Tìm kiếm theo Title/Message.
        /// </summary>
        [StringLength(200)]
        public string? Search { get; set; }

        /// <summary>
        /// 0 = Info, 1 = Success, 2 = Warning, 3 = Error.
        /// </summary>
        public byte? Severity { get; set; }

        /// <summary>
        /// Lọc theo thông báo hệ thống hay thủ công.
        /// </summary>
        public bool? IsSystemGenerated { get; set; }

        /// <summary>
        /// Lọc theo thông báo global hay không.
        /// </summary>
        public bool? IsGlobal { get; set; }

        public DateTime? CreatedFromUtc { get; set; }
        public DateTime? CreatedToUtc { get; set; }

        /// <summary>
        /// Trường sort: "CreatedAtUtc" (default), "Title", "Severity".
        /// </summary>
        public string? SortBy { get; set; } = "CreatedAtUtc";

        /// <summary>
        /// true = DESC, false = ASC.
        /// </summary>
        public bool SortDescending { get; set; } = true;
    }

    /// <summary>
    /// Item cho list thông báo (admin).
    /// </summary>
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

        /// <summary>
        /// Tổng số user đã được gán thông báo này (NotificationUser).
        /// </summary>
        public int TotalTargetUsers { get; set; }

        /// <summary>
        /// Số user đã đọc.
        /// </summary>
        public int ReadCount { get; set; }
    }

    /// <summary>
    /// Chi tiết role mục tiêu của thông báo.
    /// </summary>
    public class NotificationTargetRoleDto
    {
        public string RoleId { get; set; } = null!;
        public string? RoleName { get; set; }
    }

    /// <summary>
    /// Chi tiết thông báo (admin view).
    /// </summary>
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
    }

    /// <summary>
    /// Request filter cho lịch sử thông báo của user hiện tại.
    /// </summary>
    public class NotificationUserFilterDto
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// Chỉ lấy thông báo chưa đọc.
        /// </summary>
        public bool OnlyUnread { get; set; } = false;

        public byte? Severity { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }

        [StringLength(200)]
        public string? Search { get; set; }

        /// <summary>
        /// "CreatedAtUtc" (default), "Severity", "IsRead".
        /// </summary>
        public string? SortBy { get; set; } = "CreatedAtUtc";

        public bool SortDescending { get; set; } = true;
    }

    /// <summary>
    /// Item lịch sử thông báo của user (NotificationUser + Notification).
    /// </summary>
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
    }

    /// <summary>
    /// Tạo thông báo thủ công và gán cho user cụ thể.
    /// (Ở đây thiết kế tối thiểu: bắt buộc có TargetUserIds; TargetRoleIds chỉ để lưu mapping.)
    /// </summary>
    public class CreateNotificationDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = null!;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = null!;

        /// <summary>
        /// 0 = Info, 1 = Success, 2 = Warning, 3 = Error.
        /// </summary>
        [Range(0, 3)]
        public byte Severity { get; set; } = 0;

        public bool IsGlobal { get; set; } = false;

        public string? RelatedEntityType { get; set; }
        public string? RelatedEntityId { get; set; }
        public string? RelatedUrl { get; set; }

        /// <summary>
        /// Danh sách RoleId mà thông báo nhắm tới (mapping vào NotificationTargetRole).
        /// </summary>
        public List<string>? TargetRoleIds { get; set; }

        /// <summary>
        /// Danh sách user (Guid) sẽ nhận thông báo (NotificationUser).
        /// BẮT BUỘC có ít nhất 1 user cho luồng thủ công tối thiểu.
        /// </summary>
        public List<Guid>? TargetUserIds { get; set; }
    }

    /// <summary>
    /// Response phân trang danh sách thông báo (admin).
    /// </summary>
    public class NotificationListResponseDto
    {
        public int TotalCount { get; set; }
        public IReadOnlyList<NotificationListItemDto> Items { get; set; } = Array.Empty<NotificationListItemDto>();
    }

    /// <summary>
    /// Response phân trang lịch sử thông báo user hiện tại.
    /// </summary>
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

    /// <summary>
    /// Dữ liệu dropdown phục vụ tạo thông báo thủ công (Admin).
    /// </summary>
    public class NotificationManualTargetOptionsDto
    {
        public List<NotificationTargetRoleOptionDto> Roles { get; set; } = new();
        public List<NotificationTargetUserOptionDto> Users { get; set; } = new();
    }
}
