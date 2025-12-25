using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class NotificationUser
{
    public long Id { get; set; }

    public int NotificationId { get; set; }

    public Guid UserId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? DismissedAtUtc { get; set; }

    public virtual Notification Notification { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
