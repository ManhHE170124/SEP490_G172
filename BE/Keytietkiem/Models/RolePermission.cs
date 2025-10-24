using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class RolePermission
{
    public string RoleId { get; set; } = null!;

    public long PermissionId { get; set; }

    public long ModuleId { get; set; }

    public bool IsActive { get; set; }

    public virtual Module Module { get; set; } = null!;

    public virtual Permission Permission { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
