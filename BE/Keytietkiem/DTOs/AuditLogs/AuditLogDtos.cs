// File: DTOs/AuditLogs/AuditLogDtos.cs
using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.AuditLogs
{
    /// <summary>
    /// Một field thay đổi trong audit log.
    /// </summary>
    public class AuditChangeDto
    {
        /// <summary>
        /// Đường dẫn field, ví dụ: "email", "status", "roles[0].code"
        /// </summary>
        public string FieldPath { get; set; } = null!;

        /// <summary>
        /// Giá trị trước khi thay đổi (string hóa).
        /// </summary>
        public string? Before { get; set; }

        /// <summary>
        /// Giá trị sau khi thay đổi (string hóa).
        /// </summary>
        public string? After { get; set; }
    }

    /// <summary>
    /// Filter query khi search/list audit logs.
    /// Dùng với [FromQuery] trong controller.
    /// </summary>
    public class AuditLogListFilterDto
    {
        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        /// <summary>
        /// Từ khóa search chung:
        /// tìm trong ActorEmail, ActorRole, Action, EntityType, EntityId
        /// </summary>
        public string? ActorEmail { get; set; }

        /// <summary>
        /// Filter dropdown theo ActorRole (Admin / Customer / StorageStaff...)
        /// </summary>
        public string? ActorRole { get; set; }

        /// <summary>
        /// Filter dropdown theo Action (CreateUser, UpdateUser...)
        /// </summary>
        public string? Action { get; set; }

        /// <summary>
        /// Filter dropdown theo EntityType (User, Order...)
        /// </summary>
        public string? EntityType { get; set; }

        /// <summary>
        /// (Không dùng trực tiếp nữa vì đã đưa vào search chung)
        /// Giữ lại để không breaking nếu FE đang truyền.
        /// </summary>
        public string? EntityId { get; set; }

        /// <summary>
        /// Trường sort: OccurredAt, ActorEmail, ActorRole, Action, EntityType, EntityId
        /// Nếu null/empty → mặc định OccurredAt.
        /// </summary>
        public string? SortBy { get; set; }

        /// <summary>
        /// Hướng sort: "asc" hoặc "desc".
        /// Nếu null/empty → mặc định "desc" cho OccurredAt.
        /// </summary>
        public string? SortDirection { get; set; }
    }

    public class AuditLogListItemDto
    {
        public long AuditId { get; set; }

        public DateTime OccurredAt { get; set; }

        public Guid? ActorId { get; set; }

        public string? ActorEmail { get; set; }

        public string? ActorRole { get; set; }

        public string? SessionId { get; set; }

        public string? IpAddress { get; set; }

        public string Action { get; set; } = null!;

        public string? EntityType { get; set; }

        public string? EntityId { get; set; }

        /// <summary>
        /// Danh sách field có thay đổi (chỉ phần diff).
        /// Dùng để hiển thị tóm tắt trên màn list.
        /// </summary>
        public List<AuditChangeDto> Changes { get; set; } = new();
    }

    public class AuditLogDetailDto
    {
        public long AuditId { get; set; }

        public DateTime OccurredAt { get; set; }

        public Guid? ActorId { get; set; }

        public string? ActorEmail { get; set; }

        public string? ActorRole { get; set; }

        public string? SessionId { get; set; }

        public string? IpAddress { get; set; }

        public string Action { get; set; } = null!;

        public string? EntityType { get; set; }

        public string? EntityId { get; set; }

        /// <summary>
        /// JSON trước khi thay đổi (full, dùng cho view chi tiết).
        /// </summary>
        public string? BeforeDataJson { get; set; }

        /// <summary>
        /// JSON sau khi thay đổi (full, dùng cho view chi tiết).
        /// </summary>
        public string? AfterDataJson { get; set; }

        /// <summary>
        /// Danh sách field có diff (giống list, nhưng detail có thể show đầy đủ).
        /// </summary>
        public List<AuditChangeDto> Changes { get; set; } = new();
    }

    public class AuditLogListResponseDto
    {
        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalItems { get; set; }

        public IReadOnlyList<AuditLogListItemDto> Items { get; set; }
            = Array.Empty<AuditLogListItemDto>();
    }
}
