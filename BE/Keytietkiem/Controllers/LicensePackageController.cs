using Keytietkiem.DTOs;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Keytietkiem.Attributes;
using static Keytietkiem.Constants.ModuleCodes;
using static Keytietkiem.Constants.PermissionCodes;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LicensePackageController : ControllerBase
{
    private readonly ILicensePackageService _licensePackageService;
    private readonly IAuditLogger _auditLogger;

    public LicensePackageController(
        ILicensePackageService licensePackageService,
        IAuditLogger auditLogger)
    {
        _licensePackageService = licensePackageService ?? throw new ArgumentNullException(nameof(licensePackageService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    /// <summary>
    /// Get all license packages with pagination and optional filters
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    /// <param name="supplierId">Optional supplier ID filter</param>
    /// <param name="productId">Optional product ID filter</param>
    [HttpGet]
    [RequirePermission(LICENSE_PACKAGE, VIEW_LIST)]
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

    /// <summary>
    /// Get license package by ID
    /// </summary>
    /// <param name="id">License package ID</param>
    [HttpGet("{id}")]
    [RequirePermission(LICENSE_PACKAGE, VIEW_DETAIL)]
    public async Task<IActionResult> GetLicensePackageById(Guid id)
    {
        var package = await _licensePackageService.GetLicensePackageByIdAsync(id);
        return Ok(package);
    }

    /// <summary>
    /// Create a new license package
    /// </summary>
    /// <param name="dto">License package creation data</param>
    [HttpPost]
    [RequirePermission(LICENSE_PACKAGE, CREATE)]
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

    /// <summary>
    /// Update an existing license package
    /// </summary>
    /// <param name="id">License package ID</param>
    /// <param name="dto">License package update data</param>
    [HttpPut("{id}")]
    [RequirePermission(LICENSE_PACKAGE, EDIT)]
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

    /// <summary>
    /// Import licenses from package to stock
    /// </summary>
    /// <param name="dto">Import request with quantity</param>
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
            entityId: null,           // PackageId nằm trong dto nếu cần truy ngược
            before: null,
            after: dto
        );

        return Ok(new { message = "Đã nhập license vào kho thành công" });
    }

    /// <summary>
    /// Delete a license package
    /// </summary>
    /// <param name="id">License package ID</param>
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

    /// <summary>
    /// Upload CSV file containing license keys and import to stock
    /// </summary>
    /// <param name="packageId">License package ID</param>
    /// <param name="variantId">Product variant ID</param>
    /// <param name="supplierId">Supplier ID</param>
    /// <param name="file">CSV file containing license keys</param>
    /// <param name="keyType">Key type (Individual/Pool)</param>
    /// <param name="expiryDate">Optional expiry date</param>
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
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

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

        return Ok(result);
    }

    /// <summary>
    /// Get license keys imported from a specific package
    /// </summary>
    /// <param name="packageId">License package ID</param>
    /// <param name="supplierId">Supplier ID</param>
    [HttpGet("{packageId}/keys")]
    public async Task<IActionResult> GetLicenseKeysByPackage(
        Guid packageId,
        [FromQuery] int supplierId)
    {
        var result = await _licensePackageService.GetLicenseKeysByPackageAsync(packageId, supplierId);
        return Ok(result);
    }
}
