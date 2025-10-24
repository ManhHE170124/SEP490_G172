using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class TicketReply
{
    public long ReplyId { get; set; }

    public Guid TicketId { get; set; }

    public Guid SenderId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public bool IsStaffReply { get; set; }

    public virtual User Sender { get; set; } = null!;

    public virtual Ticket Ticket { get; set; } = null!;
}
