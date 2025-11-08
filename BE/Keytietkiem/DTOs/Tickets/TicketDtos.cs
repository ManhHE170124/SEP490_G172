// File: DTOs/Tickets/TicketDtos.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Tickets
{
    public enum TicketSeverity { Low, Medium, High, Critical }
    public enum SlaState { OK, Warning, Overdue }
    public enum AssignmentState { Unassigned, Assigned, Technical }

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
        public string? AssigneeName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class TicketReplyDto
    {
        // IMPORTANT: bigint -> long (KHÔNG phải Guid)
        public long ReplyId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public bool IsStaffReply { get; set; }
        public string Message { get; set; } = "";
        public DateTime SentAt { get; set; }
    }

    // Đơn hàng gần nhất (hiển thị bên phải)
    public class LatestOrderMiniDto
    {
        public Guid OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class TicketDetailDto
    {
        public Guid TicketId { get; set; }
        public string TicketCode { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Status { get; set; } = "New";
        public TicketSeverity Severity { get; set; } = TicketSeverity.Medium;
        public SlaState SlaStatus { get; set; } = SlaState.OK;
        public AssignmentState AssignmentState { get; set; } = AssignmentState.Unassigned;

        public string CustomerName { get; set; } = "";
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public string? AssigneeName { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Lịch sử trao đổi
        public List<TicketReplyDto> Replies { get; set; } = new();

        // Ticket liên quan (các ticket khác cùng khách hàng)
        public List<TicketListItemDto> RelatedTickets { get; set; } = new();

        // Đơn hàng gần nhất của khách hàng
        public LatestOrderMiniDto? LatestOrder { get; set; }
    }

    // DTO tạo tin nhắn chat
    public class CreateTicketReplyDto
    {
        [Required, MinLength(1)]
        public string Message { get; set; } = "";
    }
}
