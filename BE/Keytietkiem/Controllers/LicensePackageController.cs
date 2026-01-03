using Keytietkiem.DTOs;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using Keytietkiem.Utils; // ✅ NEW
using System;            // ✅ ensure
using System.Collections.Generic;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LicensePackageController : ControllerBase
{
    private readonly ILicensePackageService _licensePackageService;
    private readonly IAuditLogger _auditLogger;
    private readonly INotificationSystemService _notificationSystemService;
    private readonly IConfiguration _config;

    public LicensePackageController(
        ILicensePackageService licensePackageService,
        IAuditLogger auditLogger,
        INotificationSystemService notificationSystemService, IConfiguration config)
    {
        _licensePackageService = licensePackageService ?? throw new ArgumentNullException(nameof(licensePackageService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _notificationSystemService = notificationSystemService ?? throw new ArgumentNullException(nameof(notificationSystemService));
        _config = config;
    }

    [HttpGet]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> GetAllLicensePackages(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? supplierId = null,
        [FromQuery] Guid? productId = null)
    {
        if (pageNumber < 1)
            return BadRequest(new { message = "Số trang phải lớn hơn 0" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Kích thước trang phải từ 1 đến 100" });

        var result = await _licensePackageService.GetAllLicensePackagesAsync(
            pageNumber,
            pageSize,
            supplierId,
            productId);

        return Ok(result);
    }

    [HttpGet("{id}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> GetLicensePackageById(Guid id)
    {
        var package = await _licensePackageService.GetLicensePackageByIdAsync(id);
        return Ok(package);
    }

    [HttpPost]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> CreateLicensePackage([FromBody] CreateLicensePackageDto dto)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        var package = await _licensePackageService.CreateLicensePackageAsync(dto, actorId, actorEmail);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "CreateLicensePackage",
            entityType: "LicensePackage",
            entityId: package.PackageId.ToString(),
            before: dto,
            after: new
            {
                package.PackageId
            }
        );

        return CreatedAtAction(nameof(GetLicensePackageById), new { id = package.PackageId }, package);
    }

    [HttpPut("{id}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> UpdateLicensePackage(int id, [FromBody] UpdateLicensePackageDto dto)
    {
        if (id != dto.PackageId)
        {
            const string msg = "ID trong URL và body không khớp";
            return BadRequest(new { message = msg });
        }

        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        var package = await _licensePackageService.UpdateLicensePackageAsync(dto, actorId, actorEmail);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "UpdateLicensePackage",
            entityType: "LicensePackage",
            entityId: dto.PackageId.ToString(),
            before: null,
            after: new
            {
                package.PackageId
            }
        );

        return Ok(package);
    }

    [HttpPost("import-to-stock")]
    public async Task<IActionResult> ImportLicenseToStock([FromBody] ImportLicenseToStockDto dto)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        await _licensePackageService.ImportLicenseToStockAsync(dto, actorId, actorEmail);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "ImportLicenseToStock",
            entityType: "LicensePackage",
            entityId: null,
            before: null,
            after: dto
        );

        return Ok(new { message = "Đã nhập license vào kho thành công" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLicensePackage(Guid id)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        await _licensePackageService.DeleteLicensePackageAsync(id, actorId, actorEmail);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "DeleteLicensePackage",
            entityType: "LicensePackage",
            entityId: id.ToString(),
            before: null,
            after: null
        );

        return Ok(new { message = "Gói license đã được xóa thành công" });
    }

    [HttpPost("upload-csv")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadLicenseCsv(
        [FromForm] Guid packageId,
        [FromForm] Guid variantId,
        [FromForm] int supplierId,
        IFormFile file,
        [FromForm] string keyType = "Individual",
        [FromForm] DateTime? expiryDate = null)
    {
        if (file == null || file.Length == 0)
        {
            const string msg = "File CSV là bắt buộc";
            return BadRequest(new { message = msg });
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            const string msg = "File phải có định dạng CSV";
            return BadRequest(new { message = msg });
        }

        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // Email vẫn giữ để audit/CreatedByEmail (FE/back-end thường dùng)
        var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "(unknown)";

        // ✅ Ưu tiên username/display name (ClaimTypes.Name) -> fallback email
        var actorDisplayName =
            (User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(actorDisplayName))
            actorDisplayName = actorEmail;

        var result = await _licensePackageService.UploadLicenseCsvAsync(
            packageId,
            variantId,
            supplierId,
            file,
            actorId,
            actorEmail,
            keyType,
            expiryDate);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "UploadLicenseCsv",
            entityType: "LicensePackage",
            entityId: packageId.ToString(),
            before: null,
            after: new
            {
                packageId,
                variantId,
                supplierId,
                fileName = file.FileName,
                fileLength = file.Length,
                keyType,
                expiryDate
            }
        );

        // ✅ System notification: Import CSV -> notify ADMIN (best-effort)
        try
        {
            // ✅ route FE đúng: /suppliers/:id (SupplierDetailPage)
            var origin = PublicUrlHelper.GetPublicOrigin(HttpContext, _config);
            var relatedUrl = $"{origin}/suppliers/{supplierId}";

            // Thuần Việt keyType
            var keyTypeVi =
                string.Equals(keyType, "Individual", StringComparison.OrdinalIgnoreCase) ? "Key cá nhân"
                : string.Equals(keyType, "Pool", StringComparison.OrdinalIgnoreCase) ? "Key dùng chung (pool)"
                : keyType;

            var expiryVi = expiryDate.HasValue
                ? expiryDate.Value.ToString("dd/MM/yyyy")
                : "Không có";

            await _notificationSystemService.CreateForRoleCodesAsync(new SystemNotificationCreateRequest
            {
                Title = "Nhập kho product key từ file CSV",
                Message =
                    $"Nhân viên: {actorDisplayName}\n" +
                    $"Đã nhập kho product key từ file CSV.\n" +
                    $"- Nhà cung cấp (Supplier): #{supplierId}\n" +
                    $"- Gói mua (Package): {packageId}\n" +
                    $"- Biến thể (Variant): {variantId}\n" +
                    $"- Tệp CSV: {file.FileName}\n" +
                    $"- Loại key: {keyTypeVi}\n" +
                    $"- Ngày hết hạn: {expiryVi}",
                Severity = 1, // Success
                CreatedByUserId = actorId,
                CreatedByEmail = actorEmail,
                Type = "Key.ImportCsv",

                RelatedEntityType = "LicensePackage",
                RelatedEntityId = packageId.ToString(),
                RelatedUrl = relatedUrl,

                TargetRoleCodes = new List<string> { RoleCodes.ADMIN }
            });
        }
        catch { }

        return Ok(result);
    }

    [HttpGet("{packageId}/keys")]
    public async Task<IActionResult> GetLicenseKeysByPackage(
        Guid packageId,
        [FromQuery] int supplierId)
    {
        var result = await _licensePackageService.GetLicenseKeysByPackageAsync(packageId, supplierId);
        return Ok(result);
    }

    [HttpGet("download-template")]
    [AllowAnonymous]
    public IActionResult DownloadCsvTemplate()
    {
        var csvContent = "key\nExampleKey123\nExampleKey456";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
        return File(bytes, "text/csv", "import_keys_template.csv");
    }
}
