using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductImage
{
    public int ImageId { get; set; }

    public Guid ProductVariantId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? Caption { get; set; }

    public int? DisplayOrder { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ProductVariant ProductVariant { get; set; } = null!;
}
