using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Order
{
    public Guid OrderId { get; set; }

    public Guid? UserId { get; set; }

    public string Email { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal? FinalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<OrderInventoryReservation> OrderInventoryReservations { get; set; } = new List<OrderInventoryReservation>();

    public virtual ICollection<ProductAccountCustomer> ProductAccountCustomers { get; set; } = new List<ProductAccountCustomer>();

    public virtual User? User { get; set; }
}
