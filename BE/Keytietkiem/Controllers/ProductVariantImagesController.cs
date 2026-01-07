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
using Keytietkiem.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Keytietkiem.Utils;
using Keytietkiem.Constants;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductVariantImagesController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly IAuditLogger _auditLogger;

        public ProductVariantImagesController(
            KeytietkiemDbContext context,
            IPhotoService photoService,
            IAuditLogger auditLogger)
        {
            _context = context;
            _photoService = photoService;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// Upload an image file and return its URL (Cloudinary).
        /// </summary>
        [HttpPost("uploadImage")]
        [Consumes("multipart/form-data")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> UploadImage([FromForm] VariantImageUploadRequest request)
        {
            try
            {
                if (request?.File == null || request.File.Length == 0)
                {
                    return BadRequest(new { message = "File rỗng hoặc không hợp lệ." });
                }

                var imageUrl = await _photoService.UploadPhotoAsync(request.File);

                // Chỉ log success – tránh spam log lỗi cho các request rác
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "UploadImage",
                    entityType: "ProductVariantImage",
                    entityId: null,
                    before: null,
                    after: new { path = imageUrl }
 );

                return Ok(new { path = imageUrl });
            }
            catch (ArgumentException ex)
            {
                // Lỗi validate file: không audit log lỗi để tránh spam
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Lỗi hệ thống chung: cũng không audit log lỗi để tránh spam
                return StatusCode(500, new { message = "Error uploading file." });
            }
        }

        /// <summary>
        /// Delete an image from Cloudinary by public id.
        /// </summary>
        [HttpDelete("deleteImage")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> DeleteImage([FromBody] VariantImageDeleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.PublicId))
            {
                // Không log lỗi 400 để tránh spam
                return BadRequest(new { message = "PublicId is required." });
            }

            try
            {
                await _photoService.DeletePhotoAsync(request.PublicId);

                // Chỉ log success delete
                await _auditLogger.LogAsync(
                    HttpContext,

                    action: "DeleteImage",
                    entityType: "ProductVariantImage",
                    entityId: request.PublicId,
                    before: null,
                    after: new { PublicId = request.PublicId, Deleted = true }
   );

                return Ok(new { message = "Image deleted successfully." });
            }
            catch (Exception ex)
            {
                // Không audit log lỗi delete để tránh spam
                return StatusCode(500, new { message = $"Error deleting image: {ex.Message}" });
            }
        }
    }
}
