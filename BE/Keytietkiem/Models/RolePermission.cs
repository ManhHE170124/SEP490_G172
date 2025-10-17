using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Keytietkiem.Models;

public partial class RolePermission
{
    public long RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public Guid ModuleId { get; set; }

    public bool IsActive { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }
    [JsonIgnore]
    public virtual Module Module { get; set; } = null!;
    [JsonIgnore]

    public virtual Permission Permission { get; set; } = null!;
    [JsonIgnore]

    public virtual Role Role { get; set; } = null!;
}
