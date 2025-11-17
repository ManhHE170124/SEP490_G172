using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Product
{
    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public string ProductType { get; set; } = null!;

    public int StockQty { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public string Slug { get; set; } = null!;

    public virtual ICollection<LicensePackage> LicensePackages { get; set; } = new List<LicensePackage>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<ProductAccount> ProductAccounts { get; set; } = new List<ProductAccount>();

    public virtual ICollection<ProductBadge> ProductBadges { get; set; } = new List<ProductBadge>();

    public virtual ICollection<Faq> Faqs { get; set; } = new List<Faq>();

    public virtual ICollection<ProductKey> ProductKeys { get; set; } = new List<ProductKey>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
}
