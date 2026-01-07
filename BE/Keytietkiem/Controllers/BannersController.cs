/**
 * File: BannersController.cs
 * Author: TungNVHE170677
 * Last Updated: 22/12/2025
 * Purpose: Admin APIs to manage Banners/Sliders (CRUD) used on the client website.
 *
 * Notes:
 * - This controller is intended for ADMIN (or authenticated) usage.
 * - Business rules/validation (placement, order, type, etc.) should be enforced in IBannerService.
 */

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
    [Authorize]
    public class BannersAdminController : ControllerBase
    {
        private readonly IBannerService _service;

        public BannersAdminController(IBannerService service)
        {
            _service = service;
        }

        /// GET /api/admin/banners?placement=HOME_MAIN
        /// List banners for admin screen (may include inactive/draft, depending on service design).
        /// - placement: optional filter by placement code (e.g., HOME_MAIN, HOME_RIGHT, ...).
        [HttpGet]
        public async Task<IActionResult> ListBanner([FromQuery] string? placement)
        {
            var data = await _service.GetAdminListAsync(placement);
            return Ok(data);
        }

        /// GET /api/admin/banners/{id}
        /// Get a single banner by id for admin edit screen.
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdBanner(long id)
        {
            var data = await _service.GetByIdAsync(id);
            return data == null ? NotFound(new { message = "Banner không tồn tại" }) : Ok(data);
        }

        /// POST /api/admin/banners
        /// Create a new banner/slider item.
        /// - dto: includes placement, image url, link, type (banner/slider), order, active flag, etc.
        [HttpPost]
        public async Task<IActionResult> CreateBanner([FromBody] BannerUpsertDto dto)
        {
            try
            {
                var created = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetByIdBanner), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// PUT /api/admin/banners/{id}
        /// Update an existing banner.
        [HttpPut("{id:long}")]
        public async Task<IActionResult> UpdateBanner(long id, [FromBody] BannerUpsertDto dto)
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

        /// DELETE /api/admin/banners/{id}
        /// Delete a banner item.
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteBanner(long id)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? Ok(new { success = true }) : NotFound(new { message = "Banner không tồn tại" });
        }
    }
}
