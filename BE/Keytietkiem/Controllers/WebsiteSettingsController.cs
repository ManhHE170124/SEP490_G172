/**
 * File: WebsiteSettingsController.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 15/11/2025
 * Purpose: Manage global website configuration
 * ✅ FIXED: Always update the FIRST record only
 */

using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using System.Text.Json;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/admin/settings")]
    [Authorize]
    public class WebsiteSettingsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IWebsiteSettingService _settingService;
        private readonly IAuditLogger _auditLogger;

        public WebsiteSettingsController(
            KeytietkiemDbContext context,
            IWebHostEnvironment env,
            IWebsiteSettingService settingService,
            IAuditLogger auditLogger)
        {
            _context = context;
            _env = env;
            _settingService = settingService;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// GET /api/admin/settings
        /// ✅ FIXED: Always get the first record
        /// </summary>
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF,RoleCodes.CUSTOMER_CARE)]
        public async Task<IActionResult> Get()
        {
            // ✅ Use service to get settings
            var setting = await _settingService.GetOrCreateAsync();
            var payments = await _context.PaymentGateways.ToListAsync();

            return Ok(new
            {
                name = setting.SiteName,
                slogan = setting.Slogan,
                logoUrl = setting.LogoUrl,
                primaryColor = setting.PrimaryColor,
                secondaryColor = setting.SecondaryColor,
                font = setting.FontFamily,
                contact = new
                {
                    address = setting.CompanyAddress,
                    phone = setting.Phone,
                    email = setting.Email
                },
                smtp = new
                {
                    server = setting.SmtpHost,
                    port = setting.SmtpPort,
                    user = setting.SmtpUsername,
                    password = setting.SmtpPassword,
                    tls = setting.UseTls,
                    dkim = setting.UseDns
                },
                media = new
                {
                    uploadLimitMB = setting.UploadLimitMb,
                    formats = (setting.AllowedExtensions ?? "jpg,png,webp")
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => f.Trim()).ToArray()
                },
                social = new
                {
                    facebook = setting.Facebook,
                    instagram = setting.Instagram,
                    zalo = setting.Zalo,
                    tiktok = setting.TikTok
                },
                payments = payments.Select(p => new
                {
                    name = p.Name,
                    callback = p.CallbackUrl,
                    enabled = p.IsActive ?? false
                }).ToArray()
            });
        }

        /// <summary>
        /// POST /api/admin/settings
        /// ✅ FIXED: Always update the FIRST record only
        /// </summary>
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN)]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Save()
            {
                WebsiteSettingsRequestDto? data = null;
                string? logoUrl = null;

            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                if (Request.HasFormContentType)
                {
                    var form = await Request.ReadFormAsync();

                    // Handle logo upload
                    var logo = form.Files.GetFile("logo");
                    if (logo != null)
                    {
                        var uploadsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
                        Directory.CreateDirectory(uploadsFolder);
                        var fileName = $"logo_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(logo.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, fileName);

                        using (var stream = System.IO.File.Create(filePath))
                        {
                            await logo.CopyToAsync(stream);
                        }
                        logoUrl = "/uploads/" + fileName;
                    }

                    var payload = form["payload"].ToString();
                    if (string.IsNullOrEmpty(payload))
                    {
                        return BadRequest(new { message = "Thiếu trường 'payload' trong form-data." });
                    }

                    data = JsonSerializer.Deserialize<WebsiteSettingsRequestDto>(payload, jsonOptions);
                }
                else
                {
                    using var reader = new StreamReader(Request.Body, encoding: System.Text.Encoding.UTF8);
                    var body = await reader.ReadToEndAsync();

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        return BadRequest(new { message = "Request body rỗng." });
                    }

                    data = JsonSerializer.Deserialize<WebsiteSettingsRequestDto>(body, jsonOptions);
                }

                if (data == null)
                {
                    return BadRequest(new { message = "Không thể parse dữ liệu từ request." });
                }

                // ✅ FIXED: Use service to save (always updates first record)
                var updatedSetting = await _settingService.SaveFromRequestAsync(data, logoUrl);

                // 🔐 AUDIT LOG – SAVE WEBSITE SETTINGS (chỉ log summary, không log mật khẩu)
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "Save",
                    entityType: "WebsiteSettings",
                    entityId: "global",
                    before: null,
                    after: new
                    {
                        updatedSetting.SiteName,
                        updatedSetting.Slogan,
                        updatedSetting.LogoUrl,
                        updatedSetting.PrimaryColor,
                        updatedSetting.SecondaryColor,
                        updatedSetting.FontFamily,
                        updatedSetting.CompanyAddress,
                        updatedSetting.Phone,
                        updatedSetting.Email,
                        updatedSetting.SmtpHost,
                        updatedSetting.SmtpPort,
                        updatedSetting.SmtpUsername,
                        // Không log SmtpPassword để tránh lộ secret
                        updatedSetting.UseTls,
                        updatedSetting.UseDns,
                        updatedSetting.UploadLimitMb,
                        updatedSetting.AllowedExtensions,
                        updatedSetting.Facebook,
                        updatedSetting.Instagram,
                        updatedSetting.Zalo,
                        updatedSetting.TikTok
                    }
                );

                return Ok(new
                {
                    message = "Cập nhật thành công",
                    logoUrl = updatedSetting.LogoUrl
                });
            }
            catch (JsonException ex)
            {
                return BadRequest(new
                {
                    message = "Lỗi parse JSON",
                    error = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi hệ thống",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublic()
        {
            var setting = await _settingService.GetOrCreateAsync();
            return Ok(new
            {
                contact = new { address = setting.CompanyAddress, phone = setting.Phone, email = setting.Email },
                social = new { facebook = setting.Facebook, instagram = setting.Instagram, zalo = setting.Zalo, tiktok = setting.TikTok },
                logoUrl = setting.LogoUrl,
                name = setting.SiteName,
                slogan = setting.Slogan
            });
        }
    }
}
