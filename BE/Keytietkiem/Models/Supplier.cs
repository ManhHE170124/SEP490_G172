using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Supplier
{
    public int SupplierId { get; set; }

    public string Name { get; set; } = null!;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = null!;

    public string? LicenseTerms { get; set; }

    public virtual ICollection<LicensePackage> LicensePackages { get; set; } = new List<LicensePackage>();

    public virtual ICollection<ProductKey> ProductKeys { get; set; } = new List<ProductKey>();
}
