/**
 * File: WebsiteSettingsRequestDto.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 
 * Version: 1.0.0
 * Purpose:
 *   Data Transfer Object (DTO) used for creating or updating website configuration settings.
 *   This request model encapsulates nested configuration groups such as contact info,
 *   SMTP setup, media upload policies, and social links.
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
        public ContactInfo? Contact { get; set; }
        public SmtpInfo? Smtp { get; set; }
        public MediaInfo? Media { get; set; }
        public SocialInfo? Social { get; set; }
    }

    public class ContactInfo
    {
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class SmtpInfo
    {
        public string? Server { get; set; }
        public int? Port { get; set; }
        public string? User { get; set; }
        public string? Password { get; set; }
        public bool? Tls { get; set; }
        public bool? Dkim { get; set; }
    }

    public class MediaInfo
    {
        public int? UploadLimitMB { get; set; }
        public IEnumerable<string>? Formats { get; set; }
    }

    public class SocialInfo
    {
        public string? Facebook { get; set; }
        public string? Instagram { get; set; }
        public string? Zalo { get; set; }
        public string? Tiktok { get; set; }
    }
}