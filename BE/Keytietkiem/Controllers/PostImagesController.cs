/**
 * File: PostImagesController.cs
 * Author: HieuNDHE173169
 * Created: 24/10/2025
 * Last Updated: 24/10/2025
 * Version: 1.0.0
 * Purpose:
 * Endpoints:
 *   - POST    /api/postimages/uploadImage              : Upload image to Cloudinary cloud storage and return the image URL.
 *   - DELETE  /api/postimages/deleteImage              : Delete image from Cloudinary using public ID.
 */
using Keytietkiem.DTOs.Post;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Keytietkiem.Utils;
using Keytietkiem.Constants;

namespace Keytietkiem.Controllers
{
    /// <summary>
    /// Controller for managing post images.
    /// Handles image upload to Cloudinary cloud storage and image deletion.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PostImagesController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IPhotoService _photoService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostImagesController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="photoService">The photo service for cloud storage operations.</param>
        public PostImagesController(
            KeytietkiemDbContext context,
            IPhotoService photoService)
        {
            _context = context;
            _photoService = photoService;
        }

        /// <summary>
        /// Uploads an image file to Cloudinary.
        /// </summary>
        /// <param name="request">The image upload request containing the file.</param>
        /// <returns>200 OK with image path, or 400 on errors.</returns>
        [HttpPost("uploadImage")]
        [Consumes("multipart/form-data")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> UploadImage([FromForm] ImageUploadRequest request)
        {
            try
            {
                var imageUrl = await _photoService.UploadPhotoAsync(request.File);
                return Ok(new { path = imageUrl });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi upload ảnh." });
            }
        }

        /// <summary>
        /// Deletes an image from Cloudinary.
        /// </summary>
        /// <param name="request">The image delete request containing the public ID.</param>
        /// <returns>200 OK on success, or 400/500 on error.</returns>
        [HttpDelete("deleteImage")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> DeleteImage([FromBody] ImageDeleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicId))
            {
                return BadRequest(new { message = "Id ảnh không đúng hoặc không tìm thấy." });
            }

            try
            {
                await _photoService.DeletePhotoAsync(request.PublicId);
                return Ok(new { message = "Hình ảnh đã được gỡ thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Đã xảy ra lỗi hệ thống khi gỡ ảnh" });
            }
        }
    }
}
