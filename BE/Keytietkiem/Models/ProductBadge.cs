using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductBadge
{
    public Guid ProductId { get; set; }

    public string Badge { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Badge BadgeNavigation { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
