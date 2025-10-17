using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Module
{
    public Guid ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
