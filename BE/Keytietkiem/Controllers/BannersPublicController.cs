/**
 * File: BannersPublicController.cs
 * Author: TungNVHE170677
 * Last Updated: 22/12/2025
 * Purpose: Public APIs to retrieve active banners/sliders by placement for client pages.
 *
 * Notes:
 * - This controller is AllowAnonymous.
 * - Filtering only "public/active" banners should be handled inside IBannerService.
 */

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

        /// GET /api/banners/public?placement=HOME_MAIN
        /// Get public (active) banners for a placement.
        /// - placement: required placement code
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicBanner([FromQuery] string placement)
        {
            var data = await _service.GetPublicByPlacementAsync(placement);
            return Ok(data);
        }
    }
}
