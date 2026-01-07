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

        public PaymentGatewaysController(IPaymentGatewayService service)
        {
            _service = service;
        }

        // GET /api/admin/payment-gateways/payos
        [HttpGet("payos")]
        public async Task<ActionResult<PayOSConfigViewDto>> GetPayOS()
        {
            var data = await _service.GetPayOSAsync();
            return Ok(data);
        }

        // PUT /api/admin/payment-gateways/payos
        [HttpPut("payos")]
        public async Task<ActionResult<PayOSConfigViewDto>> UpdatePayOS([FromBody] PayOSConfigUpdateDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Body không hợp lệ" });

            // ClientId có thể rỗng nếu bạn muốn dùng fallback appsettings
            var updated = await _service.UpdatePayOSAsync(dto);
            return Ok(updated);
        }
    }
}
