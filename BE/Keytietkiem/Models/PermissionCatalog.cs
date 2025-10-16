using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class PermissionCatalog
{
    public long PermissionId { get; set; }

    public string Module { get; set; } = null!;

    public string Action { get; set; } = null!;

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
