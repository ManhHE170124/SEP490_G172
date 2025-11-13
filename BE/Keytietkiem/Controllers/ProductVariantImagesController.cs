/**
 * File: ProductVariantImagesController.cs
 * Purpose:
 *   - Upload ảnh cho Product Variant (Cloudinary) và trả về URL
 *   - Xóa ảnh theo publicId trên Cloudinary
 * Endpoints:
 *   - POST   /api/productvariantimages/uploadImage
 *   - DELETE /api/productvariantimages/deleteImage
 */

using Keytietkiem.DTOs.Products;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductVariantImagesController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IPhotoService _photoService;

        public ProductVariantImagesController(KeytietkiemDbContext context, IPhotoService photoService)
        {
            _context = context;
            _photoService = photoService;
        }

        /// <summary>
        /// Upload an image file and return its URL (Cloudinary).
        /// </summary>
        [HttpPost("uploadImage")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromForm] VariantImageUploadRequest request)
        {
            try
            {
                if (request?.File == null || request.File.Length == 0)
                    return BadRequest(new { message = "File rỗng hoặc không hợp lệ." });

                var imageUrl = await _photoService.UploadPhotoAsync(request.File);
                return Ok(new { path = imageUrl });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch
            {
                return StatusCode(500, new { message = "Error uploading file." });
            }
        }

        /// <summary>
        /// Delete an image from Cloudinary by public id.
        /// </summary>
        [HttpDelete("deleteImage")]
        public async Task<IActionResult> DeleteImage([FromBody] VariantImageDeleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.PublicId))
                return BadRequest(new { message = "PublicId is required." });

            try
            {
                await _photoService.DeletePhotoAsync(request.PublicId);
                return Ok(new { message = "Image deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error deleting image: {ex.Message}" });
            }
        }
    }
}
