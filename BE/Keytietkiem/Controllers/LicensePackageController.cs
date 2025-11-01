using System.Security.Claims;
using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Staff,Admin")]
public class LicensePackageController : ControllerBase
{
    private readonly ILicensePackageService _licensePackageService;

    public LicensePackageController(ILicensePackageService licensePackageService)
    {
        _licensePackageService = licensePackageService ?? throw new ArgumentNullException(nameof(licensePackageService));
    }

    /// <summary>
    /// Get all license packages with pagination and optional filters
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    /// <param name="supplierId">Optional supplier ID filter</param>
    /// <param name="productId">Optional product ID filter</param>
    [HttpGet]
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
    public async Task<IActionResult> CreateLicensePackage([FromBody] CreateLicensePackageDto dto)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        var package = await _licensePackageService.CreateLicensePackageAsync(dto, actorId, actorEmail);
        return CreatedAtAction(nameof(GetLicensePackageById), new { id = package.PackageId }, package);
    }

    /// <summary>
    /// Update an existing license package
    /// </summary>
    /// <param name="id">License package ID</param>
    /// <param name="dto">License package update data</param>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLicensePackage(int id, [FromBody] UpdateLicensePackageDto dto)
    {
        if (id != dto.PackageId)
            return BadRequest(new { message = "ID trong URL và body không khớp" });

        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        var package = await _licensePackageService.UpdateLicensePackageAsync(dto, actorId, actorEmail);
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
        return Ok(new { message = "Gói license đã được xóa thành công" });
    }

    /// <summary>
    /// Upload CSV file containing license keys and import to stock
    /// </summary>
    /// <param name="packageId">License package ID</param>
    /// <param name="supplierId">Supplier ID</param>
    /// <param name="file">CSV file containing license keys</param>
    [HttpPost("upload-csv")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadLicenseCsv(
        [FromForm] Guid packageId,
        [FromForm] int supplierId,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File CSV là bắt buộc" });

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "File phải có định dạng CSV" });

        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        var result = await _licensePackageService.UploadLicenseCsvAsync(
            packageId,
            supplierId,
            file,
            actorId,
            actorEmail);
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
