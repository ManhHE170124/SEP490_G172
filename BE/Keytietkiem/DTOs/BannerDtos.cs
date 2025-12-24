// Keytietkiem/Dtos/BannerDtos.cs
using System;

namespace Keytietkiem.Dtos;

public class BannerUpsertDto
{
    public string Placement { get; set; } = "";
    public string? Title { get; set; }

    public string MediaUrl { get; set; } = "";
    public string MediaType { get; set; } = "image";

    public string? LinkUrl { get; set; }
    public string LinkTarget { get; set; } = "_self";

    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}

public class BannerAdminDto
{
    public long Id { get; set; }
    public string Placement { get; set; } = "";
    public string? Title { get; set; }

    public string MediaUrl { get; set; } = "";
    public string MediaType { get; set; } = "";

    public string? LinkUrl { get; set; }
    public string LinkTarget { get; set; } = "";

    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BannerPublicDto
{
    public long Id { get; set; }
    public string Placement { get; set; } = "";
    public string? Title { get; set; }

    public string MediaUrl { get; set; } = "";
    public string MediaType { get; set; } = "";

    public string? LinkUrl { get; set; }
    public string LinkTarget { get; set; } = "";

    public int SortOrder { get; set; }
}
