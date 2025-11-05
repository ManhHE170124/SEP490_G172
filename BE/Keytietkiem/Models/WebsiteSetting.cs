using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class WebsiteSetting
{
    public int Id { get; set; }

    public string SiteName { get; set; } = null!;

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

    public string? SmtpUsername { get; set; }

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
