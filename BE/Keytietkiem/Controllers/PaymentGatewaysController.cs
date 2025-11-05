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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentGatewayDto>>> GetAll()
        {
            var data = await _service.GetAllAsync();
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentGatewayDto>> Get(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<PaymentGatewayDto>> Create(PaymentGatewayDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, PaymentGatewayDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
