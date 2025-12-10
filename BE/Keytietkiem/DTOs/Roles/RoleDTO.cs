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
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Roles
{
    public class RoleDTO
    {
        public string RoleId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateRoleDTO
    {
        [StringLength(50, ErrorMessage = "ID vai trò không được vượt quá 50 ký tự.")]
        public string? RoleId { get; set; }
        
        [Required(ErrorMessage = "Tên vai trò không được để trống.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "Tên vai trò phải có từ 2 đến 60 ký tự.")]
        public string Name { get; set; } = null!;
        
        [Required(ErrorMessage = "Mã vai trò không được để trống.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã vai trò phải có từ 2 đến 50 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã vai trò chỉ được chứa chữ in hoa, số và dấu gạch dưới.")]
        public string Code { get; set; } = null!;
        public bool IsSystem { get; set; } = false;
    }

    public class UpdateRoleDTO
    {
        [Required(ErrorMessage = "Tên vai trò không được để trống.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "Tên vai trò phải có từ 2 đến 60 ký tự.")]
        public string Name { get; set; } = null!;
        
        [Required(ErrorMessage = "Mã vai trò không được để trống.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã vai trò phải có từ 2 đến 50 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã vai trò chỉ được chứa chữ in hoa, số và dấu gạch dưới.")]
        public string Code { get; set; } = null!;
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

    public class CheckPermissionRequestDTO
    {
        public string RoleCode { get; set; } = null!;
        public string ModuleCode { get; set; } = null!;
        public string PermissionCode { get; set; } = null!;
    }

    public class CheckPermissionResponseDTO
    {
        public bool HasAccess { get; set; }
        public string? Message { get; set; }
    }

    public class ModuleAccessRequestDTO
    {
        public List<string> RoleCodes { get; set; } = new List<string>();
        public string PermissionCode { get; set; } = "ACCESS";
    }

    public class ModuleAccessDTO
    {
        public long ModuleId { get; set; }
        public string ModuleName { get; set; } = null!;
        public string? ModuleCode { get; set; }
    }

    public class UserPermissionsRequestDTO
    {
        public List<string> RoleCodes { get; set; } = new List<string>();
    }

    public class UserPermissionItemDTO
    {
        public string ModuleCode { get; set; } = null!;
        public string PermissionCode { get; set; } = null!;
    }

    public class UserPermissionsResponseDTO
    {
        public List<UserPermissionItemDTO> Permissions { get; set; } = new List<UserPermissionItemDTO>();
    }
}
