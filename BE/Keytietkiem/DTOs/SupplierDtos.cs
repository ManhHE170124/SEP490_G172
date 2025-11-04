using System.ComponentModel.DataAnnotations;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Models;

namespace Keytietkiem.DTOs;

/// <summary>
/// Request DTO for creating a new supplier
/// </summary>
public class CreateSupplierDto
{
    [Required(ErrorMessage = "Tên nhà cung cấp là bắt buộc")]
    [StringLength(100, ErrorMessage = "Tên nhà cung cấp không được vượt quá 100 ký tự")]
    public string Name { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự")]
    public string? ContactEmail { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(32, ErrorMessage = "Số điện thoại không được vượt quá 32 ký tự")]
    public string? ContactPhone { get; set; }

    [StringLength(500, ErrorMessage = "Điều khoản giấy phép không được vượt quá 500 ký tự")]
    public string? LicenseTerms { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự")]
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for updating an existing supplier
/// </summary>
public class UpdateSupplierDto
{
    [Required(ErrorMessage = "ID nhà cung cấp là bắt buộc")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "Tên nhà cung cấp là bắt buộc")]
    [StringLength(100, ErrorMessage = "Tên nhà cung cấp không được vượt quá 100 ký tự")]
    public string Name { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự")]
    public string? ContactEmail { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(32, ErrorMessage = "Số điện thoại không được vượt quá 32 ký tự")]
    public string? ContactPhone { get; set; }

    [StringLength(500, ErrorMessage = "Điều khoản giấy phép không được vượt quá 500 ký tự")]
    public string? LicenseTerms { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự")]
    public string? Notes { get; set; }
}

/// <summary>
/// Response DTO for supplier information
/// </summary>
public class SupplierResponseDto
{
    public int SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? LicenseTerms { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public SupplierStatus Status { get; set; }
    public int ActiveProductCount { get; set; }
    public int TotalProductKeyCount { get; set; }
}

/// <summary>
/// Simplified supplier info for list views
/// </summary>
public class SupplierListDto
{
    public int SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime CreatedAt { get; set; }
    public SupplierStatus Status { get; set; }
    public int ActiveProductCount { get; set; }
}

/// <summary>
/// Request DTO for deactivating a supplier
/// </summary>
public class DeactivateSupplierDto
{
    [Required(ErrorMessage = "ID nhà cung cấp là bắt buộc")]
    public int SupplierId { get; set; }

    public bool ConfirmReassignment { get; set; }

    public int? ReassignToSupplierId { get; set; }

    [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự")]
    public string? Reason { get; set; }
}

/// <summary>
/// Response for supplier deactivation validation
/// </summary>
public class DeactivateSupplierValidationDto
{
    public bool CanDeactivate { get; set; }
    public int ActiveProductCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> AffectedProducts { get; set; } = new();
}
