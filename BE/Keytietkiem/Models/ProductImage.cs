using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductImage
{
    public int ImageId { get; set; }

    public Guid ProductId { get; set; }

    public string Url { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? AltText { get; set; }

    public virtual Product Product { get; set; } = null!;
}
