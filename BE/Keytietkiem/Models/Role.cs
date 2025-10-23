/**
  File: Role.cs
  Author: Keytietkiem Team
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Represents user roles in the RBAC (Role-Based Access Control) system.
           Defines different user roles with their permissions and access levels.
           Supports system roles and custom roles with activation status.
  Properties:
    - RoleId (string)           : Unique role identifier
    - Name (string)             : Role display name
    - IsSystem (bool)           : Indicates if this is a system-defined role
    - IsActive (bool)           : Role activation status
    - CreatedAt (DateTime)      : Role creation timestamp
    - UpdatedAt (DateTime?)      : Last update timestamp
  Relationships:
    - Many RolePermissions (1:N)
    - Many Users (M:N via UserRoles)
  RBAC Features:
    - System vs Custom roles
    - Role activation/deactivation
    - Permission-based access control
*/

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

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
