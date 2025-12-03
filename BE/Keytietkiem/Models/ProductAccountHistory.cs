using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductAccountHistory
{
    public long HistoryId { get; set; }

    public Guid ProductAccountId { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = null!;

    public Guid ActionBy { get; set; }

    public DateTime ActionAt { get; set; }

    public string? Notes { get; set; }

    public virtual ProductAccount ProductAccount { get; set; } = null!;

    public virtual User? User { get; set; }
}
