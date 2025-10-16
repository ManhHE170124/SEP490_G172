using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Role
{
    public long RoleId { get; set; }

    public string Name { get; set; } = null!;

    public string? Desc { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
