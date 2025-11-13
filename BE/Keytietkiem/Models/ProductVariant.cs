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

    public string? Thumbnail { get; set; }          // nvarchar(255)
    public string? MetaTitle { get; set; }          // nvarchar(255)
    public string? MetaDescription { get; set; }    // nvarchar(255)
    public int ViewCount { get; set; }
    public int? WarrantyDays { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int StockQty { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<ProductSection> ProductSections { get; set; } = new List<ProductSection>();
}
