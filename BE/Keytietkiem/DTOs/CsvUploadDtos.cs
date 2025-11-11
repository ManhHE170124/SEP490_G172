using System.ComponentModel.DataAnnotations;
using Keytietkiem.DTOs.Enums;
using Microsoft.AspNetCore.Http;

namespace Keytietkiem.DTOs;

/// <summary>
/// Request DTO for uploading CSV file with license keys
/// </summary>
public class UploadLicenseCsvDto
{
    [Required(ErrorMessage = "ID gói license là bắt buộc")]
    public Guid PackageId { get; set; }

    [Required(ErrorMessage = "ID nhà cung cấp là bắt buộc")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "File CSV là bắt buộc")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Type of product key to apply to all imported keys
    /// </summary>
    public ProductKeyType KeyType { get; set; } = ProductKeyType.Individual;

    /// <summary>
    /// Optional expiry date to apply to all imported keys
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
}

/// <summary>
/// Response DTO for CSV upload result
/// </summary>
public class CsvUploadResultDto
{
    public Guid PackageId { get; set; }
    public int TotalKeysInFile { get; set; }
    public int SuccessfullyImported { get; set; }
    public int DuplicateKeys { get; set; }
    public int InvalidKeys { get; set; }
    public List<string> Errors { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for importing product keys directly from storage monitor
/// </summary>
public class ImportProductKeysFromCsvDto
{
    [Required(ErrorMessage = "ID sản phẩm là bắt buộc")]
    public Guid ProductId { get; set; }

    [Required(ErrorMessage = "ID nhà cung cấp là bắt buộc")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "Giá vốn là bắt buộc")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá vốn phải lớn hơn hoặc bằng 0")]
    public decimal CogsPrice { get; set; }

    [Required(ErrorMessage = "File CSV là bắt buộc")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Key type to apply to all imported keys
    /// </summary>
    public string KeyType { get; set; } = nameof(ProductKeyType.Individual);

    /// <summary>
    /// Optional expiry date applied to every imported key
    /// </summary>
    public DateTime? ExpiryDate { get; set; }
}
