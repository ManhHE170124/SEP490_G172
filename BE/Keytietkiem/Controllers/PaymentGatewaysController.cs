/**
 * File: PaymentGatewaysController.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 2025-01-20
 * Purpose: Manage payment gateway configurations - Simple CRUD + Toggle
 */

using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers.Admin
{
    [Route("api/admin/payment-gateways")]
    [ApiController]
    public class PaymentGatewaysController : ControllerBase
    {
        private readonly IPaymentGatewayService _service;
        private readonly IAuditLogger _auditLogger;

        public PaymentGatewaysController(
            IPaymentGatewayService service,
            IAuditLogger auditLogger)
        {
            _service = service;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// Get all payment gateways
        /// GET /api/admin/payment-gateways
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentGatewayDto>>> GetAll()
        {
            var data = await _service.GetAllAsync();
            return Ok(data);
        }

        /// <summary>
        /// Get payment gateway by ID
        /// GET /api/admin/payment-gateways/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentGatewayDto>> Get(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound(new { message = "Payment gateway not found" });
            }

            return Ok(result);
        }

        /// <summary>
        /// Create new payment gateway
        /// POST /api/admin/payment-gateways
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<PaymentGatewayDto>> Create([FromBody] PaymentGatewayDto dto)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Tên cổng thanh toán là bắt buộc" });

            if (string.IsNullOrWhiteSpace(dto.CallbackUrl))
                return BadRequest(new { message = "Callback URL là bắt buộc" });

            try
            {
                var created = await _service.CreateAsync(dto);

                // Audit log (success) – chỉ log metadata, không log secret
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "CreatePaymentGateway",
                    entityType: "PaymentGateway",
                    entityId: created.Id.ToString(),
                    before: null,
                    after: new
                    {
                        created.Id,
                        created.Name,
                        created.IsActive,
                        created.CallbackUrl
                    });

                return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                // Không log lỗi để tránh spam audit
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update payment gateway
        /// PUT /api/admin/payment-gateways/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentGatewayDto>> Update(int id, [FromBody] PaymentGatewayDto dto)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Tên cổng thanh toán là bắt buộc" });

            if (string.IsNullOrWhiteSpace(dto.CallbackUrl))
                return BadRequest(new { message = "Callback URL là bắt buộc" });

            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                if (updated == null)
                {
                    // Không audit trường hợp not found
                    return NotFound(new { message = "Payment gateway not found" });
                }

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "UpdatePaymentGateway",
                    entityType: "PaymentGateway",
                    entityId: updated.Id.ToString(),
                    before: null, // Nếu cần before thì service/fetch thêm, ở đây giữ đơn giản
                    after: new
                    {
                        updated.Id,
                        updated.Name,
                        updated.IsActive,
                        updated.CallbackUrl
                    });

                return Ok(updated);
            }
            catch (Exception ex)
            {
                // Không log lỗi để tránh spam audit
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete payment gateway
        /// DELETE /api/admin/payment-gateways/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _service.DeleteAsync(id);
                if (!deleted)
                {
                    // Không audit trường hợp not found
                    return NotFound(new { message = "Payment gateway not found" });
                }

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "DeletePaymentGateway",
                    entityType: "PaymentGateway",
                    entityId: id.ToString(),
                    before: null,
                    after: null);

                return NoContent();
            }
            catch (Exception ex)
            {
                // Không log lỗi để tránh spam audit
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Toggle payment gateway active status
        /// PATCH /api/admin/payment-gateways/{id}/toggle
        /// </summary>
        [HttpPatch("{id}/toggle")]
        public async Task<ActionResult<PaymentGatewayDto>> Toggle(int id)
        {
            try
            {
                var toggled = await _service.ToggleActiveAsync(id);
                if (toggled == null)
                {
                    // Không audit trường hợp not found
                    return NotFound(new { message = "Payment gateway not found" });
                }

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "TogglePaymentGateway",
                    entityType: "PaymentGateway",
                    entityId: toggled.Id.ToString(),
                    before: null,
                    after: new
                    {
                        toggled.Id,
                        toggled.Name,
                        toggled.IsActive
                    });

                return Ok(toggled);
            }
            catch (Exception ex)
            {
                // Không log lỗi để tránh spam audit
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
