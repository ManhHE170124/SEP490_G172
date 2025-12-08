using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class NotificationTargetRole
{
    public int Id { get; set; }

    public int NotificationId { get; set; }

    public string RoleId { get; set; } = null!;

    public virtual Notification Notification { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}