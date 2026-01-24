// File: DTOs/Tickets/TicketDtos.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Tickets
{
    public enum TicketSeverity { Low, Medium, High, Critical }
    public enum SlaState { OK, Warning, Overdue }
    public enum AssignmentState { Unassigned, Assigned, Technical }

    /// <summary>
    /// DTO list cơ bản (dùng chung cho nhiều màn, không chứa SLA deadline).
    /// </summary>
    public class TicketListItemDto
    {
        public Guid TicketId { get; set; }
        public string TicketCode { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Status { get; set; } = "New"; // New | InProgress | Completed | Closed
        public TicketSeverity Severity { get; set; } = TicketSeverity.Medium;
        public SlaState SlaStatus { get; set; } = SlaState.OK;
        public AssignmentState AssignmentState { get; set; } = AssignmentState.Unassigned;

        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }

        // hiển thị assignee ở list/detail
        public Guid? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
        public string? AssigneeEmail { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO list mở rộng cho màn Admin/Staff – thêm PriorityLevel + deadline SLA.
    /// Giữ nguyên TicketListItemDto để không phá vỡ chỗ khác.
    /// </summary>
    public class TicketListItemWithSlaDto : TicketListItemDto
    {
        /// <summary>
        /// Cấp ưu tiên (1 = cao nhất).
        /// </summary>
        public int PriorityLevel { get; set; }

        /// <summary>
        /// Hạn phản hồi đầu tiên (dùng cho bảng Unassigned).
        /// </summary>
        public DateTime? FirstResponseDueAt { get; set; }

        /// <summary>
        /// Hạn giải quyết (dùng cho bảng Ticket của tôi).
        /// </summary>
        public DateTime? ResolutionDueAt { get; set; }
    }

    public class TicketReplyDto
    {
        public long ReplyId { get; set; }           // bigint -> long
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = "";

        // ✅ NEW: Avatar của người gửi (User.AvatarUrl)
        public string? SenderAvatarUrl { get; set; }

        public bool IsStaffReply { get; set; }
        public string Message { get; set; } = "";
        public DateTime SentAt { get; set; }
    }

    // Panel "Ticket liên quan"
    public class RelatedTicketDto
    {
        public Guid TicketId { get; set; }
        public string TicketCode { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Status { get; set; } = "New";
        public TicketSeverity Severity { get; set; } = TicketSeverity.Medium;
        public SlaState SlaStatus { get; set; } = SlaState.OK;
        public DateTime CreatedAt { get; set; }
    }

    public class LatestOrderMiniDto
    {
        public Guid OrderId { get; set; }
        /// <summary>
        /// Mã đơn hiển thị (theo format ORD-yyyyMMdd-XXXX)
        /// </summary>
        public string OrderNumber { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    // ======= DTO list ticket dành cho khách hàng =======
    public class CustomerTicketListItemDto
    {
        public Guid TicketId { get; set; }
        public string TicketCode { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Status { get; set; } = "New";
        public TicketSeverity Severity { get; set; } = TicketSeverity.Medium;
        public SlaState SlaStatus { get; set; } = SlaState.OK;

        // Chỉ cho khách thấy người đang xử lý (nếu có)
        public string? AssigneeName { get; set; }
        public string? AssigneeEmail { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class TicketDetailDto
    {
        public Guid TicketId { get; set; }
        public string TicketCode { get; set; } = "";
        public string Subject { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "New";
        public TicketSeverity Severity { get; set; } = TicketSeverity.Medium;
        public int PriorityLevel { get; set; }
        public SlaState SlaStatus { get; set; } = SlaState.OK;
        public AssignmentState AssignmentState { get; set; } = AssignmentState.Unassigned;

        public DateTime? FirstResponseDueAt { get; set; }
        public DateTime? FirstRespondedAt { get; set; }
        public DateTime? ResolutionDueAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public string CustomerName { get; set; } = "";
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }

        // thông tin nhân viên phụ trách
        public Guid? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
        public string? AssigneeEmail { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<TicketReplyDto> Replies { get; set; } = new();
        public List<RelatedTicketDto> RelatedTickets { get; set; } = new();

        /// <summary>
        /// Toàn bộ đơn hàng của người tạo ticket (sắp xếp CreatedAt giảm dần).
        /// </summary>
        public List<LatestOrderMiniDto> CustomerOrders { get; set; } = new();

        public LatestOrderMiniDto? LatestOrder { get; set; }
    }

    public class CustomerCreateTicketDto
    {
        /// <summary>
        /// Mã template tiêu đề ticket (TicketSubjectTemplates.TemplateCode)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string TemplateCode { get; set; } = "";

        /// <summary>
        /// Mô tả chi tiết do khách hàng nhập (optional)
        /// </summary>
        [StringLength(1000)]
        public string? Description { get; set; }
    }

    public class CustomerTicketCreatedDto
    {
        public Guid TicketId { get; set; }
        public string TicketCode { get; set; } = "";
        public string Subject { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "New";
        public TicketSeverity Severity { get; set; } = TicketSeverity.Medium;
        public SlaState SlaStatus { get; set; } = SlaState.OK;
        public DateTime CreatedAt { get; set; }
    }

    public class TicketSubjectTemplateDto
    {
        public string TemplateCode { get; set; } = "";
        public string Title { get; set; } = "";

        /// <summary>
        /// Low / Medium / High / Critical
        /// </summary>
        public string Severity { get; set; } = TicketSeverity.Medium.ToString();

        /// <summary>
        /// Payment, Key, Account, Refund, Support, Security, General...
        /// (FE sẽ dịch sang tiếng Việt)
        /// </summary>
        public string? Category { get; set; }

        public bool IsActive { get; set; }
    }

    public class CreateTicketReplyDto
    {
        [Required, MinLength(1)]
        public string Message { get; set; } = "";
    }
}
