/**
  File: Permission.cs
  Author: Keytietkiem Team
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Represents individual permissions in the RBAC system. Defines specific
           actions or access rights that can be granted to roles. Permissions are
           combined with modules to create granular access control.
  Properties:
    - PermissionId (long)        : Unique permission identifier
    - PermissionName (string)    : Permission name (unique)
    - Description (string)      : Detailed permission description
    - CreatedAt (DateTime)      : Permission creation timestamp
    - UpdatedAt (DateTime?)      : Last update timestamp
  Relationships:
    - Many RolePermissions (1:N)
  RBAC Features:
    - Granular permission control
    - Permission naming and description
    - Module-based permission grouping
*/

using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Permission
{
    public long PermissionId { get; set; }

    public string PermissionName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
