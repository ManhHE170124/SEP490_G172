using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class AuditLog
{
    public long AuditId { get; set; }

    public DateTime OccurredAt { get; set; }

    public Guid? ActorId { get; set; }

    public string? ActorEmail { get; set; }

    public string Action { get; set; } = null!;

    public string Resource { get; set; } = null!;

    public string? EntityId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? DetailJson { get; set; }
}
