// File: DTOs/Tickets/TicketDtos.cs
using System;
using System.Collections.Generic;

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
        public string CustomerName { get; set; } = "";
        public string CustomerEmail { get; set; } = "";
        public string? AssigneeName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class TicketDetailDto
    {
        public Guid TicketId { get; set; }
        public string TicketCode { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Status { get; set; } = "New";
        public string CustomerName { get; set; } = "";
        public string CustomerEmail { get; set; } = "";
        public string? CustomerPhone { get; set; }
        public string? AssigneeName { get; set; }
        public TicketSeverity Severity { get; set; } = TicketSeverity.Medium;
        public SlaState SlaStatus { get; set; } = SlaState.OK;
        public AssignmentState AssignmentState { get; set; } = AssignmentState.Unassigned;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<TicketReplyDto> Replies { get; set; } = new();
    }

    public class TicketReplyDto
    {
        public long ReplyId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public bool IsStaffReply { get; set; }
        public string Message { get; set; } = "";
        public DateTime SentAt { get; set; }
    }
}
