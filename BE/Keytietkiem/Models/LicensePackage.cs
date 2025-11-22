using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class LicensePackage
{
    public Guid PackageId { get; set; }

    public int SupplierId { get; set; }

    public Guid VariantId { get; set; }

    public int Quantity { get; set; }

    public int ImportedToStock { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Notes { get; set; }

    public virtual ProductVariant ProductVariant { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;
}
