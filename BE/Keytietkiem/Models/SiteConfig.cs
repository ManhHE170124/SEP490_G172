using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SiteConfig
{
    public string ConfigKey { get; set; } = null!;

    public string SiteName { get; set; } = null!;

    public string? Slogan { get; set; }

    public string PrimaryColor { get; set; } = null!;

    public string? SecondaryColor { get; set; }

    public string? FontFamily { get; set; }

    public long? LogoMediaId { get; set; }

    public int UploadMaxMb { get; set; }

    public string UploadFormats { get; set; } = null!;

    public string? ContactAddress { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactEmail { get; set; }

    public bool MaintenanceOn { get; set; }

    public string? MaintenanceMsg { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }
}
