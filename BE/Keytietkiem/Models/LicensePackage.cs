using System;

namespace Keytietkiem.Models;

/// <summary>
/// Represents a bulk license package purchased from a supplier
/// </summary>
public partial class LicensePackage
{
    public Guid PackageId { get; set; }

    public int SupplierId { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>
    /// Total quantity in the package
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Price per unit/license
    /// </summary>
    public decimal PricePerUnit { get; set; }

    /// <summary>
    /// Number of licenses already imported to stock
    /// </summary>
    public int ImportedToStock { get; set; }

    /// <summary>
    /// Effective date of the package (when it becomes valid)
    /// </summary>
    public DateTime? EffectiveDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Notes { get; set; }

    // Navigation properties
    public virtual Supplier Supplier { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
