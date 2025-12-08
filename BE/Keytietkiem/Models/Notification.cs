using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Notification
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public byte Severity { get; set; }

    public bool IsSystemGenerated { get; set; }

    public bool IsGlobal { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string? RelatedEntityType { get; set; }

    public string? RelatedEntityId { get; set; }

    public string? RelatedUrl { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<NotificationTargetRole> NotificationTargetRoles { get; set; } = new List<NotificationTargetRole>();

    public virtual ICollection<NotificationUser> NotificationUsers { get; set; } = new List<NotificationUser>();
}
