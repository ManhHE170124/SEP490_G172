using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Cart
{
    public Guid CartId { get; set; }

    public Guid? UserId { get; set; }

    public string? AnonymousId { get; set; }

    public string Status { get; set; } = null!;

    public Guid? ConvertedOrderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? ReceiverEmail { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
