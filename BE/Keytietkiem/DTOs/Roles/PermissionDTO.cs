/**
 * File: PermissionDTO.cs
 * Author: HieuNDHE173169
 * Created: 20/10/2025
 * Last Updated: 20/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Object for Permission operations. Provides a clean interface
 *          for API communication without exposing internal entity structure.
 *          Supports permission creation, updates, and responses.
 * Properties:
 *   - PermissionId (long)      : Unique permission identifier
 *   - PermissionName (string)  : Permission name (unique)
 *   - Description (string)    : Detailed permission description
 *   - CreatedAt (DateTime)    : Permission creation timestamp
 *   - UpdatedAt (DateTime?)    : Last update timestamp
 * Usage:
 *   - Input DTO for permission creation/updates
 *   - Output DTO for permission responses
 *   - Validation and data transfer
 */

using System;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Roles
{
    public class PermissionDTO
    {
        public long PermissionId { get; set; }
        public string PermissionName { get; set; } = null!;
        public string? Code { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreatePermissionDTO
    {
        [Required(ErrorMessage = "Tên quyền không được để trống.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên quyền phải có từ 2 đến 100 ký tự.")]
        public string PermissionName { get; set; } = null!;
        
        [Required(ErrorMessage = "Mã quyền không được để trống.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã quyền phải có từ 2 đến 50 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã quyền chỉ được chứa chữ in hoa, số và dấu gạch dưới.")]
        public string Code { get; set; } = null!;
        
        [StringLength(300, ErrorMessage = "Mô tả không được vượt quá 300 ký tự.")]
        public string? Description { get; set; }
    }

    public class UpdatePermissionDTO
    {
        [Required(ErrorMessage = "Tên quyền không được để trống.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên quyền phải có từ 2 đến 100 ký tự.")]
        public string PermissionName { get; set; } = null!;
        
        [Required(ErrorMessage = "Mã quyền không được để trống.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã quyền phải có từ 2 đến 50 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã quyền chỉ được chứa chữ in hoa, số và dấu gạch dưới.")]
        public string Code { get; set; } = null!;
        
        [StringLength(300, ErrorMessage = "Mô tả không được vượt quá 300 ký tự.")]
        public string? Description { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
