/**
 * File: WebsiteSettingService.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 15/11/2025
 * Description:
 *   Service implementation for managing website configuration and metadata.
 *   ✅ FIXED: Always update the FIRST record only, never create duplicates.
 */

using Keytietkiem.DTOs;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Services
{
    public class WebsiteSettingService : IWebsiteSettingService
    {
        private readonly KeytietkiemDbContext _context;

        public WebsiteSettingService(KeytietkiemDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get website settings (always return the first record)
        /// </summary>
        public async Task<WebsiteSettingDto?> GetAsync()
        {
            // ✅ FIXED: Always get the first record by Id
            var setting = await _context.WebsiteSettings
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (setting == null) return null;

            return new WebsiteSettingDto
            {
                Id = setting.Id,
                SiteName = setting.SiteName,
                LogoUrl = setting.LogoUrl,
                Slogan = setting.Slogan,
                MetaDescription = setting.MetaDescription,
                PrimaryColor = setting.PrimaryColor,
                SecondaryColor = setting.SecondaryColor,
                FontFamily = setting.FontFamily,
                CompanyAddress = setting.CompanyAddress,
                Phone = setting.Phone,
                Email = setting.Email,
                SmtpHost = setting.SmtpHost,
                SmtpPort = setting.SmtpPort,
                SmtpUsername = setting.SmtpUsername,
                SmtpPassword = setting.SmtpPassword,
                UseTls = setting.UseTls,
                UseDns = setting.UseDns,
                UploadLimitMb = setting.UploadLimitMb,
                AllowedExtensions = setting.AllowedExtensions,
                Facebook = setting.Facebook,
                Zalo = setting.Zalo,
                Instagram = setting.Instagram,
                TikTok = setting.TikTok,
                UpdatedAt = setting.UpdatedAt
            };
        }

        /// <summary>
        /// ✅ NEW METHOD: Get or create settings (for Controller use)
        /// Always returns the first record, creates one if none exists
        /// </summary>
        public async Task<WebsiteSetting> GetOrCreateAsync()
        {
            // ✅ Always get the first record by Id
            var setting = await _context.WebsiteSettings
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (setting == null)
            {
                // Create default settings only once
                setting = new WebsiteSetting
                {
                    SiteName = "Key Tiết Kiệm",
                    Slogan = "Tiết kiệm thông minh - Dễ tư liệu quá",
                    PrimaryColor = "#2563EB",
                    SecondaryColor = "#111827",
                    FontFamily = "Inter (khuyên dùng)",
                    UploadLimitMb = 10,
                    AllowedExtensions = "jpg,png,webp",
                    UpdatedAt = DateTime.UtcNow
                };

                _context.WebsiteSettings.Add(setting);
                await _context.SaveChangesAsync();
            }

            return setting;
        }

        /// <summary>
        /// Update website settings
        /// ⚠️ DEPRECATED: Use SaveFromRequestAsync instead
        /// </summary>
        public async Task<WebsiteSettingDto?> UpdateAsync(int id, WebsiteSettingDto dto)
        {
            var setting = await _context.WebsiteSettings.FindAsync(id);
            if (setting == null) return null;

            setting.SiteName = dto.SiteName ?? setting.SiteName;
            setting.LogoUrl = dto.LogoUrl ?? setting.LogoUrl;
            setting.Slogan = dto.Slogan ?? setting.Slogan;
            setting.MetaDescription = dto.MetaDescription ?? setting.MetaDescription;
            setting.PrimaryColor = dto.PrimaryColor ?? setting.PrimaryColor;
            setting.SecondaryColor = dto.SecondaryColor ?? setting.SecondaryColor;
            setting.FontFamily = dto.FontFamily ?? setting.FontFamily;
            setting.CompanyAddress = dto.CompanyAddress ?? setting.CompanyAddress;
            setting.Phone = dto.Phone ?? setting.Phone;
            setting.Email = dto.Email ?? setting.Email;
            setting.SmtpHost = dto.SmtpHost ?? setting.SmtpHost;
            setting.SmtpPort = dto.SmtpPort ?? setting.SmtpPort;
            setting.SmtpUsername = dto.SmtpUsername ?? setting.SmtpUsername;
            setting.SmtpPassword = dto.SmtpPassword ?? setting.SmtpPassword;
            setting.UseTls = dto.UseTls ?? setting.UseTls;
            setting.UseDns = dto.UseDns ?? setting.UseDns;
            setting.UploadLimitMb = dto.UploadLimitMb ?? setting.UploadLimitMb;
            setting.AllowedExtensions = dto.AllowedExtensions ?? setting.AllowedExtensions;
            setting.Facebook = dto.Facebook ?? setting.Facebook;
            setting.Zalo = dto.Zalo ?? setting.Zalo;
            setting.Instagram = dto.Instagram ?? setting.Instagram;
            setting.TikTok = dto.TikTok ?? setting.TikTok;
            setting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return dto;
        }

        /// <summary>
        /// ✅ NEW METHOD: Save settings from WebsiteSettingsRequestDto
        /// Always updates the FIRST record only
        /// </summary>
        public async Task<WebsiteSetting> SaveFromRequestAsync(WebsiteSettingsRequestDto dto, string? logoUrl)
        {
            // ✅ Always get the first record
            var setting = await GetOrCreateAsync();

            // Update fields only if provided (non-null)
            if (!string.IsNullOrEmpty(dto.Name))
                setting.SiteName = dto.Name;

            if (!string.IsNullOrEmpty(dto.Slogan))
                setting.Slogan = dto.Slogan;

            if (!string.IsNullOrEmpty(logoUrl))
                setting.LogoUrl = logoUrl;
            else if (!string.IsNullOrEmpty(dto.LogoUrl))
                setting.LogoUrl = dto.LogoUrl;

            if (!string.IsNullOrEmpty(dto.PrimaryColor))
                setting.PrimaryColor = dto.PrimaryColor;

            if (!string.IsNullOrEmpty(dto.SecondaryColor))
                setting.SecondaryColor = dto.SecondaryColor;

            if (!string.IsNullOrEmpty(dto.Font))
                setting.FontFamily = dto.Font;

            // Contact
            if (dto.Contact != null)
            {
                if (!string.IsNullOrEmpty(dto.Contact.Address))
                    setting.CompanyAddress = dto.Contact.Address;
                if (!string.IsNullOrEmpty(dto.Contact.Phone))
                    setting.Phone = dto.Contact.Phone;
                if (!string.IsNullOrEmpty(dto.Contact.Email))
                    setting.Email = dto.Contact.Email;
            }

            // SMTP
            if (dto.Smtp != null)
            {
                if (!string.IsNullOrEmpty(dto.Smtp.Server))
                    setting.SmtpHost = dto.Smtp.Server;
                if (dto.Smtp.Port.HasValue)
                    setting.SmtpPort = dto.Smtp.Port.Value;
                if (!string.IsNullOrEmpty(dto.Smtp.User))
                    setting.SmtpUsername = dto.Smtp.User;
                if (!string.IsNullOrEmpty(dto.Smtp.Password))
                    setting.SmtpPassword = dto.Smtp.Password;
                if (dto.Smtp.Tls.HasValue)
                    setting.UseTls = dto.Smtp.Tls.Value;
                if (dto.Smtp.Dkim.HasValue)
                    setting.UseDns = dto.Smtp.Dkim.Value;
            }

            // Media
            if (dto.Media != null)
            {
                if (dto.Media.UploadLimitMB.HasValue)
                    setting.UploadLimitMb = dto.Media.UploadLimitMB.Value;
                if (dto.Media.Formats != null && dto.Media.Formats.Any())
                    setting.AllowedExtensions = string.Join(",", dto.Media.Formats);
            }

            // Social
            if (dto.Social != null)
            {
                if (!string.IsNullOrEmpty(dto.Social.Facebook))
                    setting.Facebook = dto.Social.Facebook;
                if (!string.IsNullOrEmpty(dto.Social.Instagram))
                    setting.Instagram = dto.Social.Instagram;
                if (!string.IsNullOrEmpty(dto.Social.Zalo))
                    setting.Zalo = dto.Social.Zalo;
                if (!string.IsNullOrEmpty(dto.Social.Tiktok))
                    setting.TikTok = dto.Social.Tiktok;
            }

            setting.UpdatedAt = DateTime.UtcNow;

            _context.WebsiteSettings.Update(setting);
            await _context.SaveChangesAsync();

            return setting;
        }
    }
}