using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductReport
{
    public Guid Id { get; set; }

    public string Status { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public Guid? ProductKeyId { get; set; }

    public Guid? ProductAccountId { get; set; }

    public Guid ProductVariantId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ProductAccount? ProductAccount { get; set; }

    public virtual ProductKey? ProductKey { get; set; }

    public virtual ProductVariant ProductVariant { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
