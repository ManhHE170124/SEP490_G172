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

        public async Task<WebsiteSettingDto> GetAsync()
        {
            var setting = await _context.WebsiteSettings.FirstOrDefaultAsync();
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

        public async Task<WebsiteSettingDto> UpdateAsync(int id, WebsiteSettingDto dto)
        {
            var setting = await _context.WebsiteSettings.FindAsync(id);
            if (setting == null) return null;

            setting.SiteName = dto.SiteName;
            setting.LogoUrl = dto.LogoUrl;
            setting.Slogan = dto.Slogan;
            setting.MetaDescription = dto.MetaDescription;
            setting.PrimaryColor = dto.PrimaryColor;
            setting.SecondaryColor = dto.SecondaryColor;
            setting.FontFamily = dto.FontFamily;
            setting.CompanyAddress = dto.CompanyAddress;
            setting.Phone = dto.Phone;
            setting.Email = dto.Email;
            setting.SmtpHost = dto.SmtpHost;
            setting.SmtpPort = dto.SmtpPort;
            setting.SmtpUsername = dto.SmtpUsername;
            setting.SmtpPassword = dto.SmtpPassword;
            setting.UseTls = dto.UseTls;
            setting.UseDns = dto.UseDns;
            setting.UploadLimitMb = dto.UploadLimitMb;
            setting.AllowedExtensions = dto.AllowedExtensions;
            setting.Facebook = dto.Facebook;
            setting.Zalo = dto.Zalo;
            setting.Instagram = dto.Instagram;
            setting.TikTok = dto.TikTok;
            setting.UpdatedAt = System.DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return dto;
        }
    }

}
