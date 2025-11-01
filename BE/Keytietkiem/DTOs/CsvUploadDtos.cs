using System.ComponentModel.DataAnnotations;
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
