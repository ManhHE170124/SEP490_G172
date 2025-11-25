using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class RefundRequest
{
    public Guid RefundId { get; set; }

    public Guid OrderId { get; set; }

    public string Reason { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public string Status { get; set; } = null!;
}
