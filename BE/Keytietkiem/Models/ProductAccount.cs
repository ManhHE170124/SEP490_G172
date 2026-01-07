using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductAccount
{
    public Guid ProductAccountId { get; set; }

    public string AccountEmail { get; set; } = null!;

    public string? AccountUsername { get; set; }

    public string AccountPassword { get; set; } = null!;

    public int MaxUsers { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? ExpiryDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public decimal? CogsPrice { get; set; }

    public Guid VariantId { get; set; }

    public int SupplierId { get; set; }

    public virtual ICollection<ProductAccountCustomer> ProductAccountCustomers { get; set; } = new List<ProductAccountCustomer>();

    public virtual ICollection<ProductAccountHistory> ProductAccountHistories { get; set; } = new List<ProductAccountHistory>();

    public virtual ICollection<ProductReport> ProductReports { get; set; } = new List<ProductReport>();

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual ProductVariant Variant { get; set; } = null!;
}
