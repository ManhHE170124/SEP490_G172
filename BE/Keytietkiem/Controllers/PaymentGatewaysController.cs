/**
 * File: PaymentGatewaysController.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 5/11/2025
 * Purpose: Manage payment gateway configurations (CRUD operations).
 *          Handles enabling, updating, and removing available payment methods
 *          for the website’s checkout and donation systems.
 * Endpoints:
 *   - GET    /api/paymentgateways          : Retrieve all payment gateways
 *   - GET    /api/paymentgateways/{id}     : Retrieve a payment gateway by ID
 *   - POST   /api/paymentgateways          : Create a new payment gateway
 *   - PUT    /api/paymentgateways/{id}     : Update an existing payment gateway
 *   - DELETE /api/paymentgateways/{id}     : Delete a payment gateway
 */

using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentGatewaysController : ControllerBase
    {
        private readonly IPaymentGatewayService _service;

        public PaymentGatewaysController(IPaymentGatewayService service)
        {
            _service = service;
        }

        /**
         * Summary: Retrieve all payment gateways.
         * Route: GET /api/paymentgateways
         * Params: none
         * Returns: 200 OK with a list of all configured payment gateways.
         */
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentGatewayDto>>> GetAll()
        {
            var data = await _service.GetAllAsync();
            return Ok(data);
        }

        /**
         * Summary: Retrieve a specific payment gateway by its ID.
         * Route: GET /api/paymentgateways/{id}
         * Params:
         *   - id (int): The unique identifier of the payment gateway.
         * Returns:
         *   - 200 OK with payment gateway data.
         *   - 404 Not Found if the gateway does not exist.
         */
        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentGatewayDto>> Get(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /**
         * Summary: Create a new payment gateway.
         * Route: POST /api/paymentgateways
         * Body: PaymentGatewayDto dto
         * Returns:
         *   - 201 Created with created gateway details.
         *   - 400 Bad Request if input is invalid.
         */
        [HttpPost]
        public async Task<ActionResult<PaymentGatewayDto>> Create(PaymentGatewayDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        /**
         * Summary: Update an existing payment gateway.
         * Route: PUT /api/paymentgateways/{id}
         * Params:
         *   - id (int): The ID of the gateway to update.
         * Body: PaymentGatewayDto dto
         * Returns:
         *   - 204 No Content if updated successfully.
         *   - 404 Not Found if the gateway does not exist.
         */
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, PaymentGatewayDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return NoContent();
        }

        /**
         * Summary: Delete a payment gateway by its ID.
         * Route: DELETE /api/paymentgateways/{id}
         * Params:
         *   - id (int): The ID of the gateway to delete.
         * Returns:
         *   - 204 No Content if deleted successfully.
         *   - 404 Not Found if the gateway does not exist.
         */
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
