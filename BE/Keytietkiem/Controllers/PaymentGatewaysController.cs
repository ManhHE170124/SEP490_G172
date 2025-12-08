/**
 * File: PaymentGatewaysController.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 2025-01-20
 * Purpose: Manage payment gateway configurations - Simple CRUD + Toggle
 */

using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers.Admin
{
    [Route("api/admin/payment-gateways")]
    [ApiController]
    public class PaymentGatewaysController : ControllerBase
    {
        private readonly IPaymentGatewayService _service;

        public PaymentGatewaysController(IPaymentGatewayService service)
        {
            _service = service;
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
                return NotFound(new { message = "Payment gateway not found" });
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
                return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
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
                    return NotFound(new { message = "Payment gateway not found" });
                return Ok(updated);
            }
            catch (Exception ex)
            {
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
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { message = "Payment gateway not found" });
            return NoContent();
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
                    return NotFound(new { message = "Payment gateway not found" });
                return Ok(toggled);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}