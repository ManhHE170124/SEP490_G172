/**
 * File: RoleDTO.cs
 * Author: HieuNDHE173169
 * Created: 20/10/2025
 * Last Updated: 20/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Object for Role operations. Provides a clean interface
 *          for API communication without exposing internal entity structure.
 *          Supports role creation, updates, and responses.
 * Usage:
 *   - Input DTO for role creation/updates
 *   - Output DTO for role responses
 *   - Validation and data transfer
 */

using System;

namespace Keytietkiem.DTOs
{
    public class RoleDTO
    {
        public string RoleId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateRoleDTO
    {
        public string Name { get; set; } = null!;
        public bool IsSystem { get; set; } = false;
    }

    public class UpdateRoleDTO
    {
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; }
    }

    public class RoleResponseDTO : RoleDTO
    {
        public List<RolePermissionDTO> RolePermissions { get; set; } = new List<RolePermissionDTO>();
    }

    public class RolePermissionDTO
    {
        public string RoleId { get; set; } = null!;
        public long ModuleId { get; set; }
        public long PermissionId { get; set; }
        public bool IsActive { get; set; }
        public string? ModuleName { get; set; }
        public string? PermissionName { get; set; }
    }

    public class RolePermissionUpdateDTO
    {
        public string RoleId { get; set; } = null!;
        public long ModuleId { get; set; }
        public long PermissionId { get; set; }
        public bool IsActive { get; set; }
    }

    public class BulkRolePermissionUpdateDTO
    {
        public string RoleId { get; set; } = null!;
        public List<RolePermissionUpdateDTO> RolePermissions { get; set; } = new List<RolePermissionUpdateDTO>();
    }

    public class RolePermissionResponseDTO
    {
        public string RoleId { get; set; } = null!;
        public string RoleName { get; set; } = null!;
        public List<RolePermissionDTO> RolePermissions { get; set; } = new List<RolePermissionDTO>();
    }
}
