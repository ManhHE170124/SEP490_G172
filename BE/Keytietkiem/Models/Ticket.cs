using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Ticket
{
    public Guid TicketId { get; set; }

    public Guid UserId { get; set; }

    public string Subject { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<TicketReply> TicketReplies { get; set; } = new List<TicketReply>();

    public virtual User User { get; set; } = null!;
    public string? TicketCode { get; set; }
    public string? Severity { get; set; }          // Low|Medium|High|Critical
    public string? SlaStatus { get; set; }         // OK|Warning|Overdue
    public string? AssignmentState { get; set; }   // Unassigned|Assigned|Technical
    public Guid? AssigneeId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public virtual User? Assignee { get; set; }
}
