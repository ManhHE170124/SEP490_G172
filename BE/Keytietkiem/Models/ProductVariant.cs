using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductVariant
{
    public Guid VariantId { get; set; }

    public Guid ProductId { get; set; }

    public string? VariantCode { get; set; }

    public string Title { get; set; } = null!;

    public int? DurationDays { get; set; }

    public int? WarrantyDays { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int StockQty { get; set; }

    public string? Thumbnail { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public int ViewCount { get; set; }

    public decimal SellPrice { get; set; }

    public decimal ListPrice { get; set; }

    public decimal CogsPrice { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductAccount> ProductAccounts { get; set; } = new List<ProductAccount>();

    public virtual ICollection<ProductKey> ProductKeys { get; set; } = new List<ProductKey>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<ProductSection> ProductSections { get; set; } = new List<ProductSection>();
    public virtual ICollection<LicensePackage> LicensePackages { get; set; } = new List<LicensePackage>();
}
