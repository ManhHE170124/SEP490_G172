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

        // hiển thị assignee ở list/detail
        public Guid? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
        public string? AssigneeEmail { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class TicketReplyDto
    {
        public long ReplyId { get; set; }           // bigint -> long
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = "";
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

        // thông tin nhân viên phụ trách
        public Guid? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
        public string? AssigneeEmail { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<TicketReplyDto> Replies { get; set; } = new();
        public List<RelatedTicketDto> RelatedTickets { get; set; } = new();

        public LatestOrderMiniDto? LatestOrder { get; set; }
    }

    public class CustomerCreateTicketDto
    {
        [Required]
        [StringLength(120)]
        public string Subject { get; set; } = "";

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

    public class CreateTicketReplyDto
    {
        [Required, MinLength(1)]
        public string Message { get; set; } = "";
    }
}
