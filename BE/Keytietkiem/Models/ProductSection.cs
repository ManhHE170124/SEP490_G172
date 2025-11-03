using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductSection
{
    public Guid SectionId { get; set; }

    public Guid VariantId { get; set; }

    public string SectionType { get; set; } = null!;

    public string? Title { get; set; }

    public string Content { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ProductVariant Variant { get; set; } = null!;
}
