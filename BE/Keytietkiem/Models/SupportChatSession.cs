using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SupportChatSession
{
    public Guid ChatSessionId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? AssignedStaffId { get; set; }

    public string Status { get; set; } = null!;

    public int PriorityLevel { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public string? LastMessagePreview { get; set; }

    public virtual User? AssignedStaff { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<SupportChatMessage> SupportChatMessages { get; set; } = new List<SupportChatMessage>();
}
