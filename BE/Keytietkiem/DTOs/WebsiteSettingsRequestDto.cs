/**
 * File: WebsiteSettingsRequestDto.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 15/11/2025
 * Version: 1.0.0
 * Purpose: Request DTO for updating website settings
 */

namespace Keytietkiem.DTOs
{
    public class WebsiteSettingsRequestDto
    {
        public string? Name { get; set; }
        public string? Slogan { get; set; }
        public string? LogoUrl { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? Font { get; set; }

        public ContactDto? Contact { get; set; }
        public SmtpDto? Smtp { get; set; }
        public MediaDto? Media { get; set; }
        public SocialDto? Social { get; set; }
    }

    public class ContactDto
    {
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class SmtpDto
    {
        public string? Server { get; set; }
        public int? Port { get; set; }
        public string? User { get; set; }
        public string? Password { get; set; }
        public bool? Tls { get; set; }
        public bool? Dkim { get; set; }
    }

    public class MediaDto
    {
        public int? UploadLimitMB { get; set; }
        public List<string>? Formats { get; set; }
    }

    public class SocialDto
    {
        public string? Facebook { get; set; }
        public string? Instagram { get; set; }
        public string? Zalo { get; set; }
        public string? Tiktok { get; set; }
    }
}