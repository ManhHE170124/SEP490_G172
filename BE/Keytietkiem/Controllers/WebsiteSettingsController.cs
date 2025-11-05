using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebsiteSettingsController : ControllerBase
    {
        private readonly IWebsiteSettingService _service;
        public WebsiteSettingsController(IWebsiteSettingService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var setting = await _service.GetAsync();
            if (setting == null) return NotFound();
            return Ok(setting);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, WebsiteSettingDto dto)
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null) return NotFound();
            return NoContent();
        }
    }
}
