/**
 * File: WebsiteSettingDto.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 
 * Purpose:
 *   Data Transfer Object (DTO) representing website-wide configuration settings.
 *   These settings control appearance, branding, SEO metadata, and SMTP configuration
 *   for system-wide usage across frontend and backend modules.
 */

namespace Keytietkiem.DTOs
{
    public class WebsiteSettingDto
{
    public int Id { get; set; }
    public string? SiteName { get; set; }
    public string? LogoUrl { get; set; }
    public string? Slogan { get; set; }
    public string? MetaDescription { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? FontFamily { get; set; }
    public string? CompanyAddress { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string?   SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool? UseTls { get; set; }
    public bool? UseDns { get; set; }
    public int? UploadLimitMb { get; set; }
    public string? AllowedExtensions { get; set; }
    public string? Facebook { get; set; }
    public string? Zalo { get; set; }
    public string? Instagram { get; set; }
    public string? TikTok { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
}
