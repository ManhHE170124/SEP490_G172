using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductReview
{
    public Guid ReviewId { get; set; }

    public Guid VariantId { get; set; }

    public Guid? UserId { get; set; }

    public byte Rating { get; set; }

    public string? Title { get; set; }

    public string Content { get; set; } = null!;

    public bool IsVerifiedPurchase { get; set; }

    public int HelpfulCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ProductVariant Variant { get; set; } = null!;
}
