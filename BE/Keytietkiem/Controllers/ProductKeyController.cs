using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Infrastructure;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using static Keytietkiem.Constants.ModuleCodes;
using static Keytietkiem.Constants.PermissionCodes;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Storage Staff,Admin")]
    public class ProductKeyController : ControllerBase
    {
        private readonly IProductKeyService _productKeyService;
        private readonly ILicensePackageService _licensePackageService;
        private readonly IAuditLogger _auditLogger;

        public ProductKeyController(
            IProductKeyService productKeyService,
            ILicensePackageService licensePackageService,
            IAuditLogger auditLogger)
        {
            _productKeyService = productKeyService;
            _licensePackageService = licensePackageService;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// Get a paginated and filtered list of product keys
        /// </summary>
        [HttpGet]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.VIEW_LIST)]
        public async Task<IActionResult> GetProductKeys(
            [FromQuery] ProductKeyFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _productKeyService.GetProductKeysAsync(filter, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed information about a specific product key
        /// </summary>
        [HttpGet("{keyId}")]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.VIEW_DETAIL)]
        public async Task<IActionResult> GetProductKeyById(
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _productKeyService.GetProductKeyByIdAsync(keyId, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Create a new product key
        /// </summary>
        [HttpPost]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.CREATE)]
        public async Task<IActionResult> CreateProductKey(
            [FromBody] CreateProductKeyDto dto,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var result = await _productKeyService.CreateProductKeyAsync(dto, actorId, cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "Create",
                    entityType: "ProductKey",
                    entityId: result.KeyId.ToString(),
                    before: null,
                    after: result
);

                return CreatedAtAction(nameof(GetProductKeyById), new { keyId = result.KeyId }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing product key
        /// </summary>
        [HttpPut("{keyId}")]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.EDIT)]
        public async Task<IActionResult> UpdateProductKey(
            Guid keyId,
            [FromBody] UpdateProductKeyDto dto,
            CancellationToken cancellationToken = default)
        {
            if (keyId != dto.KeyId)
            {
                return BadRequest(new { message = "Key ID trong URL và body không khớp" });
            }

            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                // Lấy bản hiện tại để log before
                var before = await _productKeyService.GetProductKeyByIdAsync(keyId, cancellationToken);

                var result = await _productKeyService.UpdateProductKeyAsync(dto, actorId, cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,

                    action: "Update",
                    entityType: "ProductKey",
                    entityId: keyId.ToString(),
                    before: before,
                    after: result
);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete a product key
        /// </summary>
        [HttpDelete("{keyId}")]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.DELETE)]
        public async Task<IActionResult> DeleteProductKey(
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _productKeyService.DeleteProductKeyAsync(keyId, actorId, cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "Delete",
                    entityType: "ProductKey",
                    entityId: keyId.ToString(),
                    before: null,
                    after: new { KeyId = keyId, Deleted = true }
);

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Assign a product key to an order
        /// </summary>
        [HttpPost("assign")]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.EDIT)]
        public async Task<IActionResult> AssignKeyToOrder(
            [FromBody] AssignKeyToOrderDto dto,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _productKeyService.AssignKeyToOrderAsync(dto, actorId, cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "AssignToOrder",
                    entityType: "ProductKeyAssignment",
                    entityId: null,
                    before: null,
                    after: dto
);

                return Ok(new { message = "Gán key cho đơn hàng thành công" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Unassign a product key from an order
        /// </summary>
        [HttpPost("{keyId}/unassign")]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.EDIT)]
        public async Task<IActionResult> UnassignKeyFromOrder(
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _productKeyService.UnassignKeyFromOrderAsync(keyId, actorId, cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "UnassignFromOrder",
                    entityType: "ProductKeyAssignment",
                    entityId: keyId.ToString(),
                    before: null,
                    after: new { KeyId = keyId, Unassigned = true }
);

                return Ok(new { message = "Gỡ key khỏi đơn hàng thành công" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bulk update status for multiple product keys
        /// </summary>
        [HttpPost("bulk-update-status")]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.EDIT)]
        public async Task<IActionResult> BulkUpdateKeyStatus(
            [FromBody] BulkUpdateKeyStatusDto dto,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var count = await _productKeyService.BulkUpdateKeyStatusAsync(dto, actorId, cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "BulkUpdateStatus",
                    entityType: "ProductKey",
                    entityId: null,
                    before: null,
                    after: new { UpdatedCount = count, Request = dto }
);

                return Ok(new { message = $"Đã cập nhật trạng thái {count} product keys", count });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Import product keys from CSV and automatically create license package
        /// </summary>
        [HttpPost("import-csv")]
        [Consumes("multipart/form-data")]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.CREATE)]
        public async Task<IActionResult> ImportKeysFromCsv(
            [FromForm] ImportProductKeysFromCsvDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.File == null || dto.File.Length == 0)
            {
                return BadRequest(new { message = "File CSV là bắt buộc" });
            }

            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;
                var keyType = string.IsNullOrWhiteSpace(dto.KeyType)
                    ? nameof(ProductKeyType.Individual)
                    : dto.KeyType;

                var result = await _licensePackageService.CreatePackageAndUploadCsvAsync(
                    dto.VariantId,
                    dto.SupplierId,
                    dto.File,
                    actorId,
                    actorEmail,
                    keyType,
                    dto.CogsPrice,
                    dto.ExpiryDate,
                    cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "ImportCsv",
                    entityType: "ProductKey",
                    entityId: null,
                    before: null,
                    after: result
);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Export product keys to CSV
        /// </summary>
        [HttpGet("export")]
        [RequirePermission(ModuleCodes.WAREHOUSE_MANAGER, PermissionCodes.VIEW_DETAIL)]
        public async Task<IActionResult> ExportKeysToCSV(
            [FromQuery] ProductKeyFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var csvData = await _productKeyService.ExportKeysToCSVAsync(filter, cancellationToken);
                return File(csvData, "text/csv", $"product-keys-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
