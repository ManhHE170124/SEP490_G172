using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Badge
{
    public string BadgeCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? ColorHex { get; set; }

    public string? Icon { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ProductBadge> ProductBadges { get; set; } = new List<ProductBadge>();
}
