using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductKey
{
    public Guid KeyId { get; set; }

    public string KeyString { get; set; } = null!;

    public string Status { get; set; } = null!;

    public Guid? ImportedBy { get; set; }

    public DateTime ImportedAt { get; set; }

    public int SupplierId { get; set; }

    public string Type { get; set; } = null!;

    public DateTime? ExpiryDate { get; set; }

    public string? Notes { get; set; }

    public Guid? AssignedToOrderId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid VariantId { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<ProductReport> ProductReports { get; set; } = new List<ProductReport>();

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual ProductVariant Variant { get; set; } = null!;
}
