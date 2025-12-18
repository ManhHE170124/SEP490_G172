using System.Threading.Tasks;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/banners")]
    public class BannersPublicController : ControllerBase
    {
        private readonly IBannerService _service;

        public BannersPublicController(IBannerService service)
        {
            _service = service;
        }

        // GET /api/banners/public?placement=HOME_MAIN
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublic([FromQuery] string placement)
        {
            var data = await _service.GetPublicByPlacementAsync(placement);
            return Ok(data);
        }
    }
}
