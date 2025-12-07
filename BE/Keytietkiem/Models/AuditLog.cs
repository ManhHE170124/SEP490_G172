// File: Models/AuditLog.cs
using System;

namespace Keytietkiem.Models;

public partial class AuditLog
{
    public long AuditId { get; set; }

    public DateTime OccurredAt { get; set; }

    public Guid? ActorId { get; set; }

    public string? ActorEmail { get; set; }

    public string? ActorRole { get; set; }

    public string? SessionId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string Action { get; set; } = null!;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? BeforeDataJson { get; set; }

    public string? AfterDataJson { get; set; }
}
