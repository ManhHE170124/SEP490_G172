using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Role
{
    public string RoleId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Code { get; set; }

    public virtual ICollection<NotificationTargetRole> NotificationTargetRoles { get; set; } = new List<NotificationTargetRole>();

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
