/**
 * File: ModuleDTO.cs
 * Author: HieuNDHE173169
 * Created: 20/10/2025
 * Last Updated: 20/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Object for Module operations. Provides a clean interface
 *          for API communication without exposing internal entity structure.
 *          Supports module creation, updates, and responses.
 * Properties:
 *   - ModuleId (long)          : Unique module identifier
 *   - ModuleName (string)      : Module name (unique)
 *   - Description (string)    : Module description
 *   - CreatedAt (DateTime)     : Module creation timestamp
 *   - UpdatedAt (DateTime?)    : Last update timestamp
 * Usage:
 *   - Input DTO for module creation/updates
 *   - Output DTO for module responses
 *   - Validation and data transfer
 */

using System;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Roles
{
    public class ModuleDTO
    {
        public long ModuleId { get; set; }
        public string ModuleName { get; set; } = null!;
        public string? Code { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateModuleDTO
    {
        [Required(ErrorMessage = "Tên module không được để trống.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "Tên module phải có từ 2 đến 80 ký tự.")]
        public string ModuleName { get; set; } = null!;
        
        [Required(ErrorMessage = "Mã module không được để trống.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã module phải có từ 2 đến 50 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã module chỉ được chứa chữ in hoa, số và dấu gạch dưới.")]
        public string Code { get; set; } = null!;
        
        [StringLength(200, ErrorMessage = "Mô tả không được vượt quá 200 ký tự.")]
        public string? Description { get; set; }
    }

    public class UpdateModuleDTO
    {
        [Required(ErrorMessage = "Tên module không được để trống.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "Tên module phải có từ 2 đến 80 ký tự.")]
        public string ModuleName { get; set; } = null!;
        
        [Required(ErrorMessage = "Mã module không được để trống.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã module phải có từ 2 đến 50 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã module chỉ được chứa chữ in hoa, số và dấu gạch dưới.")]
        public string Code { get; set; } = null!;
        
        [StringLength(200, ErrorMessage = "Mô tả không được vượt quá 200 ký tự.")]
        public string? Description { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
