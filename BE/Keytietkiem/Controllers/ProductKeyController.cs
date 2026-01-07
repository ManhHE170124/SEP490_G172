using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Infrastructure;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Keytietkiem.Utils;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductKeyController : ControllerBase
    {
        private readonly IProductKeyService _productKeyService;
        private readonly ILicensePackageService _licensePackageService;
        private readonly IAuditLogger _auditLogger;
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

        public ProductKeyController(
            IProductKeyService productKeyService,
            ILicensePackageService licensePackageService,
            IAuditLogger auditLogger,
            IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _productKeyService = productKeyService;
            _licensePackageService = licensePackageService;
            _auditLogger = auditLogger;
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// Get a paginated and filtered list of product keys
        /// </summary>
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF, RoleCodes.CUSTOMER_CARE)]
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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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

                // ✅ Sync stock to DB for variant + product (computed stock types)
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    await VariantStockRecalculator.SyncVariantStockAndStatusAsync(
                        db,
                        dto.VariantId,
                        DateTime.UtcNow,
                        cancellationToken);
                }

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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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

                // ✅ Sync stock
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    await VariantStockRecalculator.SyncVariantStockAndStatusAsync(
                        db,
                        before.VariantId,
                        DateTime.UtcNow,
                        cancellationToken);
                }

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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> DeleteProductKey(
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                // Lấy trước để biết VariantId, tránh sync mù
                var before = await _productKeyService.GetProductKeyByIdAsync(keyId, cancellationToken);

                await _productKeyService.DeleteProductKeyAsync(keyId, actorId, cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "Delete",
                    entityType: "ProductKey",
                    entityId: keyId.ToString(),
                    before: null,
                    after: new { KeyId = keyId, Deleted = true }
                );

                // ✅ Sync stock
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    await VariantStockRecalculator.SyncVariantStockAndStatusAsync(
                        db,
                        before.VariantId,
                        DateTime.UtcNow,
                        cancellationToken);
                }

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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> AssignKeyToOrder(
            [FromBody] AssignKeyToOrderDto dto,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                // Lấy trước để biết VariantId
                var before = await _productKeyService.GetProductKeyByIdAsync(dto.KeyId, cancellationToken);

                await _productKeyService.AssignKeyToOrderAsync(dto, actorId, cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "AssignToOrder",
                    entityType: "ProductKeyAssignment",
                    entityId: null,
                    before: null,
                    after: dto
                );

                // ✅ Sync stock
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    await VariantStockRecalculator.SyncVariantStockAndStatusAsync(
                        db,
                        before.VariantId,
                        DateTime.UtcNow,
                        cancellationToken);
                }

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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> UnassignKeyFromOrder(
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                var before = await _productKeyService.GetProductKeyByIdAsync(keyId, cancellationToken);

                await _productKeyService.UnassignKeyFromOrderAsync(keyId, actorId, cancellationToken);

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "UnassignFromOrder",
                    entityType: "ProductKeyAssignment",
                    entityId: keyId.ToString(),
                    before: null,
                    after: new { KeyId = keyId, Unassigned = true }
                );

                // ✅ Sync stock
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    await VariantStockRecalculator.SyncVariantStockAndStatusAsync(
                        db,
                        before.VariantId,
                        DateTime.UtcNow,
                        cancellationToken);
                }

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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> BulkUpdateKeyStatus(
            [FromBody] BulkUpdateKeyStatusDto dto,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Lấy danh sách VariantId bị ảnh hưởng (để sync stock) – query 1 lần
                var affectedVariantIds = new List<Guid>();
                await using (var dbLookup = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    affectedVariantIds = await dbLookup.Set<ProductKey>()
                        .AsNoTracking()
                        .Where(k => dto.KeyIds.Contains(k.KeyId))
                        .Select(k => k.VariantId)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                }

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

                // ✅ Sync stock for all affected variants
                if (affectedVariantIds.Count > 0)
                {
                    await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                    {
                        await VariantStockRecalculator.SyncVariantStockAndStatusAsync(
                            db,
                            affectedVariantIds,
                            DateTime.UtcNow,
                            cancellationToken);
                    }
                }

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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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

                // ✅ Sync stock
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    await VariantStockRecalculator.SyncVariantStockAndStatusAsync(
                        db,
                        dto.VariantId,
                        DateTime.UtcNow,
                        cancellationToken);
                }

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
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
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


        /// <summary>
        /// Get expired product keys
        /// </summary>
        [HttpGet("expired")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> GetExpiredKeys(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _productKeyService.GetExpiredKeysAsync(pageNumber, pageSize, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        /// <summary>
        /// Get product keys expiring soon
        /// </summary>
        [HttpGet("expiring-soon")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> GetKeysExpiringSoon(
            [FromQuery] int days = 5,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _productKeyService.GetKeysExpiringSoonAsync(days, pageNumber, pageSize, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
