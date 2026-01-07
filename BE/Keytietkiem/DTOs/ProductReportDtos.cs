using System.ComponentModel.DataAnnotations;
using Keytietkiem.DTOs.Enums;

namespace Keytietkiem.DTOs;

/// <summary>
/// DTO for creating a new product report
/// </summary>
public class CreateProductReportDto
{
    [Required(ErrorMessage = "Tên báo cáo là bắt buộc")]
    [MaxLength(200, ErrorMessage = "Tên báo cáo không được vượt quá 200 ký tự")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Mô tả là bắt buộc")]
    [MaxLength(2000, ErrorMessage = "Mô tả không được vượt quá 2000 ký tự")]
    public string Description { get; set; } = null!;

    [Required(ErrorMessage = "ProductVariantId là bắt buộc")]
    public Guid ProductVariantId { get; set; }

    public Guid? ProductKeyId { get; set; }

    public Guid? ProductAccountId { get; set; }

    public Guid? UserId { get; set; }
}

/// <summary>
/// DTO for updating a product report status
/// </summary>
public class UpdateProductReportDto
{
    [Required(ErrorMessage = "ID báo cáo là bắt buộc")]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Trạng thái là bắt buộc")]
    public ProductReportStatus Status { get; set; }
}

/// <summary>
/// DTO for product report response
/// </summary>
public class ProductReportResponseDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public Guid? ProductKeyId { get; set; }
    public string? ProductKeyString { get; set; }
    public Guid? ProductAccountId { get; set; }
    public string? ProductAccountUsername { get; set; }
    public Guid ProductVariantId { get; set; }
    public string ProductVariantTitle { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string? SupplierName { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = null!;
    public string UserFullName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for product report list item
/// </summary>
public class ProductReportListDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string ProductVariantTitle { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string UserEmail { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
