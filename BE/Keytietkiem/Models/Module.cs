/**
  File: Module.cs
  Author: Keytietkiem Team
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Represents application modules in the RBAC system. Modules group related
           functionality and permissions together. Used to organize permissions
           by functional areas of the application.
  Properties:
    - ModuleId (long)          : Unique module identifier
    - ModuleName (string)      : Module name (unique)
    - Description (string)    : Module description
    - CreatedAt (DateTime)     : Module creation timestamp
    - UpdatedAt (DateTime?)    : Last update timestamp
  Relationships:
    - Many RolePermissions (1:N)
  RBAC Features:
    - Module-based permission organization
    - Functional area grouping
    - Permission scope definition
*/

using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Module
{
    public long ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
