using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.Orders
{
    /// <summary>
    /// DTO for Order Detail item
    /// </summary>
    public class OrderDetailDTO
    {
        public long OrderDetailId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string? ProductCode { get; set; }
        public string? ProductType { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public Guid? KeyId { get; set; }
        public string? KeyString { get; set; }
        public decimal SubTotal { get; set; } // Quantity * UnitPrice
    }

    /// <summary>
    /// DTO for Payment information
    /// </summary>
    public class PaymentDTO
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Full Order DTO for Order Detail page
    /// </summary>
    public class OrderDTO
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string? UserPhone { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public List<OrderDetailDTO> OrderDetails { get; set; } = new List<OrderDetailDTO>();
        public List<PaymentDTO> Payments { get; set; } = new List<PaymentDTO>();
        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Partial, Paid, Refunded
    }

    /// <summary>
    /// Order List Item DTO for Order Management (Admin)
    /// </summary>
    public class OrderListItemDTO
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public string PaymentStatus { get; set; } = "Unpaid";
    }

    /// <summary>
    /// Order History Item DTO for Order History (User)
    /// </summary>
    public class OrderHistoryItemDTO
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = null!; // Format: ORD-YYYYMMDD-XXXX
        public decimal TotalAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public List<string> ProductNames { get; set; } = new List<string>();
        public string? ThumbnailUrl { get; set; }
        public string PaymentStatus { get; set; } = "Unpaid";
    }

    /// <summary>
    /// DTO for creating a new order
    /// </summary>
    public class CreateOrderDTO
    {
        public Guid UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; } = 0;
        public string Status { get; set; } = "Pending";
        public List<CreateOrderDetailDTO> OrderDetails { get; set; } = new List<CreateOrderDetailDTO>();
    }

    /// <summary>
    /// DTO for creating order detail
    /// </summary>
    public class CreateOrderDetailDTO
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public Guid? KeyId { get; set; }
    }

    /// <summary>
    /// DTO for updating an order
    /// </summary>
    public class UpdateOrderDTO
    {
        public string Status { get; set; } = null!;
        public decimal? DiscountAmount { get; set; }
    }
}
