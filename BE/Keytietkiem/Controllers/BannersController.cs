using System;
using System.Threading.Tasks;
using Keytietkiem.Dtos;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/admin/banners")]
    [Authorize] // hoặc [Authorize(Roles="Admin")]
    public class BannersAdminController : ControllerBase
    {
        private readonly IBannerService _service;

        public BannersAdminController(IBannerService service)
        {
            _service = service;
        }

        // GET /api/admin/banners?placement=HOME_MAIN
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? placement)
        {
            var data = await _service.GetAdminListAsync(placement);
            return Ok(data);
        }

        // GET /api/admin/banners/{id}
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var data = await _service.GetByIdAsync(id);
            return data == null ? NotFound(new { message = "Banner không tồn tại" }) : Ok(data);
        }

        // POST /api/admin/banners
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BannerUpsertDto dto)
        {
            try
            {
                var created = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT /api/admin/banners/{id}
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] BannerUpsertDto dto)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                return updated == null ? NotFound(new { message = "Banner không tồn tại" }) : Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE /api/admin/banners/{id}
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? Ok(new { success = true }) : NotFound(new { message = "Banner không tồn tại" });
        }
    }
}
