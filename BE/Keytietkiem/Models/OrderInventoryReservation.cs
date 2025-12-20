using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class OrderInventoryReservation
{
    public long ReservationId { get; set; }

    public Guid OrderId { get; set; }

    public Guid VariantId { get; set; }

    public int Quantity { get; set; }

    public string Status { get; set; } = null!;

    public DateTime ReservedUntilUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ProductVariant Variant { get; set; } = null!;
}
