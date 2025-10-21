/**
  File: Order.cs
  Author: Keytietkiem Team
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Represents customer orders in the e-commerce system. Contains order
           information, pricing details, status tracking, and relationships to
           order details, payments, and refunds.
  Properties:
    - OrderId (Guid)           : Unique order identifier
    - UserId (Guid)           : Foreign key to User (customer)
    - TotalAmount (decimal)   : Order total before discounts
    - DiscountAmount (decimal) : Total discount applied
    - FinalAmount (decimal?)   : Final amount after discounts (computed)
    - Status (string)         : Order status (Pending/Processing/Completed/etc.)
    - CreatedAt (DateTime)    : Order creation timestamp
  Relationships:
    - One User (N:1)
    - Many OrderDetails (1:N)
    - Many Payments (1:N)
    - Many RefundRequests (1:N)
  E-commerce Features:
    - Order tracking
    - Discount calculation
    - Payment processing
    - Refund management
    - Customer order history
*/

using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Order
{
    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal? FinalAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<RefundRequest> RefundRequests { get; set; } = new List<RefundRequest>();

    public virtual User User { get; set; } = null!;
}
