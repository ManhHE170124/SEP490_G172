using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class UserRole
{
    public Guid AccountId { get; set; }

    public long RoleId { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
