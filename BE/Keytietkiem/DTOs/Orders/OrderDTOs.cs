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
        public Guid VariantId { get; set; }
        public string VariantTitle { get; set; } = null!;
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string? ProductCode { get; set; }
        public string? ProductType { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public Guid? KeyId { get; set; }
        public string? KeyString { get; set; }

        public decimal SubTotal { get; set; } // Quantity * UnitPrice
    }

    /// <summary>
    /// Full Order DTO for Order Detail page (read-only)
    /// </summary>
    public class OrderDTO
    {
        public Guid OrderId { get; set; }

        public Guid? UserId { get; set; }

        public string Email { get; set; } = null!;

        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string? UserPhone { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string Status { get; set; } = null!; // Lấy từ Payment.Status

        public DateTime CreatedAt { get; set; }

        public List<OrderDetailDTO> OrderDetails { get; set; } = new();
    }

    /// <summary>
    /// Order List Item DTO for Order Management (Admin) – read-only
    /// </summary>
    public class OrderListItemDTO
    {
        public Guid OrderId { get; set; }
        public Guid? UserId { get; set; }

        public string Email { get; set; } = null!;

        public string? UserName { get; set; }
        public string? UserEmail { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal? FinalAmount { get; set; }

        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
    }

    /// <summary>
    /// Order History Item DTO for Order History (User) – read-only
    /// </summary>
    public class OrderHistoryItemDTO
    {
        public Guid OrderId { get; set; }
        public Guid? UserId { get; set; }

        public string OrderNumber { get; set; } = null!; // ORD-YYYYMMDD-XXXX

        // Email của đơn hàng
        public string Email { get; set; } = null!;

        public decimal TotalAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }

        public List<string> ProductNames { get; set; } = new();
    }
}
