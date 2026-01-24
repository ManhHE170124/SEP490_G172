using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using Keytietkiem.Utils.Constants;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupplierController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly IAuditLogger _auditLogger;

    public SupplierController(
        ISupplierService supplierService,
        IAuditLogger auditLogger)
    {
        _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    /// <summary>
    /// Get all suppliers with pagination and optional search
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="searchTerm">Optional search term for name or email</param>
    [HttpGet]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> GetAllSuppliers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null)
    {
        if (pageNumber < 1)
            return BadRequest(new { message = "Số trang phải lớn hơn 0" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Kích thước trang phải từ 1 đến 100" });

        var result = await _supplierService.GetAllSuppliersAsync(pageNumber, pageSize, status, searchTerm);
        return Ok(result);
    }

    /// <summary>
    /// Get supplier by ID
    /// </summary>
    /// <param name="id">Supplier ID</param>
    [HttpGet("{id}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> GetSupplierById(int id)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(id);
        return Ok(supplier);
    }

    /// <summary>
    /// Create a new supplier
    /// </summary>
    /// <param name="dto">Supplier creation data</param>
    [HttpPost]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierDto dto)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        var supplier = await _supplierService.CreateSupplierAsync(dto, actorId, actorEmail);

        // Audit log: CREATE (success only)
        await _auditLogger.LogAsync(
            HttpContext,
            action: "CreateSupplier",
            entityType: "Supplier",
            entityId: supplier.SupplierId.ToString(),
            before: null,
            after: new
            {
                ActorId = actorId,
                ActorEmail = actorEmail,
                Supplier = supplier
            }
        );

        return CreatedAtAction(nameof(GetSupplierById), new { id = supplier.SupplierId }, supplier);
    }

    /// <summary>
    /// Update an existing supplier
    /// </summary>
    /// <param name="id">Supplier ID</param>
    /// <param name="dto">Supplier update data</param>
    [HttpPut("{id}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> UpdateSupplier(int id, [FromBody] UpdateSupplierDto dto)
    {
        if (id != dto.SupplierId)
            return BadRequest(new { message = "ID trong URL và body không khớp" });

        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        var supplier = await _supplierService.UpdateSupplierAsync(dto, actorId, actorEmail);

        // Audit log: UPDATE (success only)
        await _auditLogger.LogAsync(
            HttpContext,
            action: "UpdateSupplier",
            entityType: "Supplier",
            entityId: supplier.SupplierId.ToString(),
            before: new
            {
                SupplierId = dto.SupplierId
            },
            after: new
            {
                ActorId = actorId,
                ActorEmail = actorEmail,
                Supplier = supplier
            }
        );

        return Ok(supplier);
    }

    /// <summary>
    /// Deactivate a supplier
    /// </summary>
    /// <param name="id">Supplier ID</param>
    /// <param name="dto">Deactivation data with confirmation</param>
    [HttpDelete("{id}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> DeactivateSupplier(int id, [FromBody] DeactivateSupplierDto dto)
    {
        if (id != dto.SupplierId)
            return BadRequest(new { message = "ID trong URL và body không khớp" });

        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        await _supplierService.DeactivateSupplierAsync(dto, actorId, actorEmail);

        // Audit log: DEACTIVATE (success only)
        await _auditLogger.LogAsync(
            HttpContext,
            action: "DeactivateSupplier",
            entityType: "Supplier",
            entityId: dto.SupplierId.ToString(),
            before: new
            {
                SupplierId = dto.SupplierId,
                Reason = dto.Reason
            },
            after: new
            {
                ActorId = actorId,
                ActorEmail = actorEmail,
                Status = "Deactivated"
            }
        );

        return Ok(new { message = "Nhà cung cấp đã được tạm dừng thành công" });
    }

    /// <summary>
    /// Toggle supplier status (Active/Deactive)
    /// </summary>
    /// <param name="id">Supplier ID</param>
    [HttpPatch("{id}/toggle-status")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> ToggleSupplierStatus(int id)
    {
        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        var supplier = await _supplierService.ToggleSupplierStatusAsync(id, actorId, actorEmail);

        // Audit log: TOGGLE STATUS (success only)
        await _auditLogger.LogAsync(
            HttpContext,
            action: "ToggleSupplierStatus",
            entityType: "Supplier",
            entityId: supplier.SupplierId.ToString(),
            before: new
            {
                SupplierId = id
            },
            after: new
            {
                ActorId = actorId,
                ActorEmail = actorEmail,
                Supplier = supplier
            }
        );

        return Ok(supplier);
    }

    /// <summary>
    /// Get active suppliers that provide a specific product
    /// </summary>
    [HttpGet("by-product/{productId:guid}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> GetSuppliersByProduct(Guid productId)
    {
        if (productId == Guid.Empty)
            return BadRequest(new { message = "ID sản phẩm không hợp lệ" });

        var suppliers = await _supplierService.GetSuppliersByProductAsync(productId);
        return Ok(suppliers);
    }

    /// <summary>
    /// Check if supplier name exists
    /// </summary>
    /// <param name="name">Supplier name</param>
    /// <param name="excludeId">Optional supplier ID to exclude (for updates)</param>
    [HttpGet("check-name")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> CheckSupplierName(
        [FromQuery] string name,
        [FromQuery] int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Tên nhà cung cấp là bắt buộc" });

        var exists = await _supplierService.IsSupplierNameExistsAsync(name, excludeId);
        return Ok(new { exists });
    }
}
