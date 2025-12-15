using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class CartItem
{
    public long CartItemId { get; set; }

    public Guid CartId { get; set; }

    public Guid VariantId { get; set; }

    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Cart Cart { get; set; } = null!;

    public virtual ProductVariant Variant { get; set; } = null!;
}
