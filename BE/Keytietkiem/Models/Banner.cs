using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Banner
{
    public long Id { get; set; }

    public string Placement { get; set; } = null!;

    public string? Title { get; set; }

    public string MediaUrl { get; set; } = null!;

    public string MediaType { get; set; } = null!;

    public string? LinkUrl { get; set; }

    public string LinkTarget { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
