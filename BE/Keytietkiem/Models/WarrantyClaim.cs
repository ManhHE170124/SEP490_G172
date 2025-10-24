using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class WarrantyClaim
{
    public Guid ClaimId { get; set; }

    public long OrderDetailId { get; set; }

    public string Reason { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual OrderDetail OrderDetail { get; set; } = null!;
}
