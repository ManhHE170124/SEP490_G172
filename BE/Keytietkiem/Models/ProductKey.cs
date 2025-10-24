using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductKey
{
    public Guid KeyId { get; set; }

    public Guid ProductId { get; set; }

    public string KeyString { get; set; } = null!;

    public string Status { get; set; } = null!;

    public Guid? ImportedBy { get; set; }

    public DateTime ImportedAt { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual Product Product { get; set; } = null!;
}
