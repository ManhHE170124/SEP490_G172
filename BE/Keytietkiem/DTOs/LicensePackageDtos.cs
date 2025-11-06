using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs;

/// <summary>
/// Request DTO for creating a new license package
/// </summary>
public class CreateLicensePackageDto
{
    [Required(ErrorMessage = "ID nhà cung cấp là bắt buộc")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "ID sản phẩm là bắt buộc")]
    public Guid ProductId { get; set; }

    [Required(ErrorMessage = "Số lượng là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "Giá mỗi gói là bắt buộc")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn hoặc bằng 0")]
    public decimal PricePerUnit { get; set; }

    public DateTime? EffectiveDate { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for updating an existing license package
/// </summary>
public class UpdateLicensePackageDto
{
    [Required(ErrorMessage = "ID gói license là bắt buộc")]
    public int PackageId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
    public int? Quantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn hoặc bằng 0")]
    public decimal? PricePerUnit { get; set; }

    public DateTime? EffectiveDate { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Notes { get; set; }
}

/// <summary>
/// Response DTO for license package information
/// </summary>
public class LicensePackageResponseDto
{
    public Guid PackageId { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PricePerUnit { get; set; }
    public int ImportedToStock { get; set; }
    public int RemainingQuantity { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Simplified license package info for list views
/// </summary>
public class LicensePackageListDto
{
    public Guid PackageId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PricePerUnit { get; set; }
    public int ImportedToStock { get; set; }
    public int RemainingQuantity { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

/// <summary>
/// Request DTO for importing license package to stock
/// </summary>
public class ImportLicenseToStockDto
{
    [Required(ErrorMessage = "ID gói license là bắt buộc")]
    public Guid PackageId { get; set; }

    [Required(ErrorMessage = "Số lượng nhập kho là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
    public int QuantityToImport { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Notes { get; set; }
}

/// <summary>
/// Response DTO for license key details
/// </summary>
public class LicenseKeyDetailDto
{
    public Guid KeyId { get; set; }
    public string KeyString { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public string? ImportedByEmail { get; set; }
}

/// <summary>
/// Response DTO for paginated license keys list
/// </summary>
public class LicenseKeysListResponseDto
{
    public Guid PackageId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public int TotalKeys { get; set; }
    public List<LicenseKeyDetailDto> Keys { get; set; } = new();
}