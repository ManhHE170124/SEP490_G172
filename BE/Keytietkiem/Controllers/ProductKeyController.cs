using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Storage Staff,Admin")]
    public class ProductKeyController : ControllerBase
    {
        private readonly IProductKeyService _productKeyService;

        public ProductKeyController(IProductKeyService productKeyService)
        {
            _productKeyService = productKeyService;
        }

        /// <summary>
        /// Get a paginated and filtered list of product keys
        /// </summary>
        [HttpGet]
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
        public async Task<IActionResult> CreateProductKey(
            [FromBody] CreateProductKeyDto dto,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var result = await _productKeyService.CreateProductKeyAsync(dto, actorId, cancellationToken);
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
                var result = await _productKeyService.UpdateProductKeyAsync(dto, actorId, cancellationToken);
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
        public async Task<IActionResult> DeleteProductKey(
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _productKeyService.DeleteProductKeyAsync(keyId, actorId, cancellationToken);
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
        public async Task<IActionResult> AssignKeyToOrder(
            [FromBody] AssignKeyToOrderDto dto,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _productKeyService.AssignKeyToOrderAsync(dto, actorId, cancellationToken);
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
        public async Task<IActionResult> UnassignKeyFromOrder(
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                await _productKeyService.UnassignKeyFromOrderAsync(keyId, actorId, cancellationToken);
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
        public async Task<IActionResult> BulkUpdateKeyStatus(
            [FromBody] BulkUpdateKeyStatusDto dto,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var count = await _productKeyService.BulkUpdateKeyStatusAsync(dto, actorId, cancellationToken);
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
        /// Export product keys to CSV
        /// </summary>
        [HttpGet("export")]
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
