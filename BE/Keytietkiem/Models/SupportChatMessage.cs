using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SupportChatMessage
{
    public long MessageId { get; set; }

    public Guid ChatSessionId { get; set; }

    public Guid SenderId { get; set; }

    public bool IsFromStaff { get; set; }

    public string Content { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public virtual SupportChatSession ChatSession { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
