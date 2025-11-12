using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class LicensePackage
{
    public Guid PackageId { get; set; }

    public int SupplierId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal PricePerUnit { get; set; }

    public int ImportedToStock { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Notes { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;
}
