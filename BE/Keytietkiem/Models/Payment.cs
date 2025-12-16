using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Payment
{
    public Guid PaymentId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? ProviderOrderCode { get; set; }

    public string Provider { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string TargetType { get; set; } = null!;

    public string? TargetId { get; set; }

    public string? PaymentLinkId { get; set; }
}
