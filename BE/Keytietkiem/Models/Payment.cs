using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Payment
{
    public Guid PaymentId { get; set; }

    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = null!;

    public string Email { get; set; }

    public DateTime CreatedAt { get; set; }

    // 👇 Thêm 2 trường mới map với DB
    public long? ProviderOrderCode { get; set; }

    public string? Provider { get; set; }

    public virtual Order Order { get; set; } = null!;
}
