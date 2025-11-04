using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductFaq
{
    public Guid FaqId { get; set; }

    public Guid ProductId { get; set; }

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;
}
