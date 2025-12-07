/**
 * File: TagDTO.cs
 * Author: HieuNDHE173169
 * Created: 21/10/2025
 * Last Updated: 24/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Object for Tag operations. Provides a clean interface
 *          for API communication without exposing internal entity structure.
 *          Supports tag creation, updates, and responses.
 * Usage:
 *   - Input DTO for tag creation/updates
 *   - Output DTO for tag responses
 *   - Validation and data transfer
 */

using System;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Post
{
    public class TagDTO
    {
        public Guid TagId { get; set; }
        public string TagName { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateTagDTO
    {
        [Required(ErrorMessage = "Tên thẻ không được để trống.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên thẻ phải có từ 2 đến 100 ký tự.")]
        public string TagName { get; set; } = null!;
        
        [Required(ErrorMessage = "Slug không được để trống.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Slug phải có từ 2 đến 100 ký tự.")]
        [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug chỉ được chứa chữ thường, số và dấu gạch ngang.")]
        public string Slug { get; set; } = null!;
    }

    public class UpdateTagDTO
    {
        [Required(ErrorMessage = "Tên thẻ không được để trống.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên thẻ phải có từ 2 đến 100 ký tự.")]
        public string TagName { get; set; } = null!;
        
        [Required(ErrorMessage = "Slug không được để trống.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Slug phải có từ 2 đến 100 ký tự.")]
        [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug chỉ được chứa chữ thường, số và dấu gạch ngang.")]
        public string Slug { get; set; } = null!;
    }
}
