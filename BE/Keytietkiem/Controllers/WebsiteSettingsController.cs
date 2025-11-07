/**
 * File: WebsiteSettingsController.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 4/11/2025
 * Purpose: Manage global website configuration such as branding, contact, SMTP, media, and social settings.
 * Endpoints:
 *   - GET    /api/admin/settings          : Retrieve website settings (including payments)
 *   - POST   /api/admin/settings          : Update website settings and upload logo
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.Models;
using Keytietkiem.DTOs;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/admin/settings")]
    public class WebsiteSettingsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IWebHostEnvironment _env;

        public WebsiteSettingsController(KeytietkiemDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /**
         * Summary: Retrieve website settings and payment gateway configurations.
         * Route: GET /api/admin/settings
         * Params: none
         * Returns: 200 OK with website settings and payment methods.
         */
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var settings = await _context.WebsiteSettings.FirstOrDefaultAsync();
            var payments = await _context.PaymentGateways.ToListAsync();
            return Ok(new
            {
                name = settings?.SiteName,
                slogan = settings?.Slogan,
                logoUrl = settings?.LogoUrl,
                primaryColor = settings?.PrimaryColor,
                secondaryColor = settings?.SecondaryColor,
                font = settings?.FontFamily,
                contact = new
                {
                    address = settings?.CompanyAddress,
                    phone = settings?.Phone,
                    email = settings?.Email
                },
                smtp = new
                {
                    server = settings?.SmtpHost,
                    port = settings?.SmtpPort,
                    user = settings?.SmtpUsername,
                    password = settings?.SmtpPassword,
                    tls = settings?.UseTls,
                    dkim = settings?.UseDns
                },
                media = new
                {
                    uploadLimitMB = settings?.UploadLimitMb,
                    formats = (settings?.AllowedExtensions ?? "jpg,png,webp")
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => f.Trim()).ToArray()
                },
                social = new
                {
                    facebook = settings?.Facebook,
                    instagram = settings?.Instagram,
                    zalo = settings?.Zalo,
                    tiktok = settings?.TikTok
                },
                payments = payments.Select(p => new
                {
                    name = p.Name,
                    callback = p.CallbackUrl,
                    enabled = p.IsActive ?? false
                }).ToArray()
            });
        }

        /**
         * Summary: Save or update website settings, optionally upload logo.
         * Route: POST /api/admin/settings
         * Content-Type:
         *   - application/json: for updating text-based settings
         *   - multipart/form-data: for uploading a logo image + payload JSON
         * Request Body:
         *   - WebsiteSettingsRequestDto (in payload)
         *   - Optional file: "logo"
         * Returns: 200 OK with confirmation and logo URL
         * Errors:
         *   - 400 BadRequest for malformed payload
         *   - 500 InternalServerError for unexpected exceptions
         */
        [HttpPost]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Save()
        {
            try
            {
                WebsiteSettingsRequestDto? data = null;
                string? logoUrl = null;

                if (Request.HasFormContentType) // multipart/form-data (FE upload logo)
                {
                    var form = await Request.ReadFormAsync();
                    // Handle logo upload
                    var logo = form.Files.GetFile("logo");
                    if (logo != null)
                    {
                        var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                        Directory.CreateDirectory(uploadsFolder);
                        var fileName = $"logo_{DateTime.UtcNow:yyyyMMddHHmmss}.png";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        using (var stream = System.IO.File.Create(filePath))
                        {
                            await logo.CopyToAsync(stream);
                        }
                        logoUrl = "/uploads/" + fileName;
                    }
                    // Deserialize payload (JSON string in form-data)
                    var payload = form["payload"];
                    if (string.IsNullOrEmpty(payload))
                        return BadRequest("Thiếu trường 'payload' trong form-data.");
                    data = System.Text.Json.JsonSerializer.Deserialize<WebsiteSettingsRequestDto>(payload!);
                }
                else // JSON (FE không upload logo)
                {
                    using (var reader = new StreamReader(Request.Body))
                    {
                        var body = await reader.ReadToEndAsync();
                        if (string.IsNullOrWhiteSpace(body))
                            return BadRequest("Request body rỗng.");
                        data = System.Text.Json.JsonSerializer.Deserialize<WebsiteSettingsRequestDto>(body);
                    }
                }

                if (data == null)
                    return BadRequest("Payload empty or malformed");

                var setting = await _context.WebsiteSettings.FirstOrDefaultAsync();
                if (setting == null)
                {
                    setting = new WebsiteSetting();
                    _context.WebsiteSettings.Add(setting);
                }

                setting.SiteName = data.Name;
                setting.Slogan = data.Slogan;
                setting.LogoUrl = logoUrl ?? (data.LogoUrl ?? setting.LogoUrl);
                setting.PrimaryColor = data.PrimaryColor;
                setting.SecondaryColor = data.SecondaryColor;
                setting.FontFamily = data.Font;
                setting.CompanyAddress = data.Contact?.Address;
                setting.Phone = data.Contact?.Phone;
                setting.Email = data.Contact?.Email;
                setting.SmtpHost = data.Smtp?.Server;
                setting.SmtpPort = data.Smtp?.Port;
                setting.SmtpUsername = data.Smtp?.User;
                setting.SmtpPassword = data.Smtp?.Password;
                setting.UseTls = data.Smtp?.Tls;
                setting.UseDns = data.Smtp?.Dkim;
                setting.UploadLimitMb = data.Media?.UploadLimitMB ?? 10;
                setting.AllowedExtensions = (data.Media?.Formats != null) ? string.Join(",", data.Media.Formats) : "jpg,png,webp";
                setting.Facebook = data.Social?.Facebook;
                setting.Zalo = data.Social?.Zalo;
                setting.Instagram = data.Social?.Instagram;
                setting.TikTok = data.Social?.Tiktok;
                setting.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Saved", logoUrl = setting.LogoUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi hệ thống: " + ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }
    }
}