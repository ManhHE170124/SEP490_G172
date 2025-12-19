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
using Keytietkiem.Attributes;
using Keytietkiem.Constants;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PostImagesController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IPhotoService _photoService;

        public PostImagesController(
            KeytietkiemDbContext context,
            IPhotoService photoService)
        {
            _context = context;
            _photoService = photoService;
        }

        /**
         * Summary: Upload an image file.
         * Route: POST /api/uploadImage
         * Body: IFormFile file {"path": "res.cloudinary.com/doifb7f6k/image/upload/v1762196927/posts/abc123xyz.png" }
         * Returns: 200 OK with image path, 400 on errors
         */
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
                // Có thể log lỗi bằng ILogger nếu cần
                return StatusCode(500, new { message = "Error uploading file." });
            }
        }

        /**
         * Summary: Delete an image from Cloudinary.
         * Route: DELETE /api/deleteImage
         * Body: JSON { "publicId": "posts/abc123xyz" }
         * Returns: 200 OK on success, 400 or 500 on error.
         */
        [HttpDelete("deleteImage")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CONTENT_CREATOR)]
        public async Task<IActionResult> DeleteImage([FromBody] ImageDeleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicId))
            {
                return BadRequest(new { message = "PublicId is required." });
            }

            try
            {
                await _photoService.DeletePhotoAsync(request.PublicId);
                return Ok(new { message = "Image deleted successfully." });
            }
            catch (Exception ex)
            {
                // Có thể log lỗi bằng ILogger nếu cần
                return StatusCode(500, new { message = $"Error deleting image: {ex.Message}" });
            }
        }
    }
}
