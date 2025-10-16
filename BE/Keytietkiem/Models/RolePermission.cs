using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class RolePermission
{
    public long RoleId { get; set; }

    public long PermissionId { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public virtual PermissionCatalog Permission { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
