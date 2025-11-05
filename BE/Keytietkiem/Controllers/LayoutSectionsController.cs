using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LayoutSectionsController : ControllerBase
    {
        private readonly ILayoutSectionService _service;
        public LayoutSectionsController(ILayoutSectionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LayoutSectionDto>>> GetAll()
        {
            var data = await _service.GetAllAsync();
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<LayoutSectionDto>> Get(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<LayoutSectionDto>> Create(LayoutSectionDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, LayoutSectionDto dto)
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
