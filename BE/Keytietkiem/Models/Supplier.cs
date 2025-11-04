using System;
using System.Collections.Generic;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;

namespace Keytietkiem.Models;

public partial class Supplier
{
    public int SupplierId { get; set; }

    public string Name { get; set; } = null!;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public DateTime CreatedAt { get; set; }

    public SupplierStatus Status { get; set; }

    public string? LicenseTerms { get; set; }

    public virtual ICollection<LicensePackage> LicensePackages { get; set; } = new List<LicensePackage>();

    public virtual ICollection<ProductKey> ProductKeys { get; set; } = new List<ProductKey>();
}
