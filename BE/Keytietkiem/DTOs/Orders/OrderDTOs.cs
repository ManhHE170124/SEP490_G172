using System;
using System.Collections.Generic;
using Keytietkiem.DTOs.Payments; // 👈 dùng PaymentDTO ở namespace Payments

namespace Keytietkiem.DTOs.Orders
{
    /// <summary>
    /// DTO for Order Detail item
    /// </summary>
    public class OrderDetailDTO
    {
        public long OrderDetailId { get; set; }

        // Dùng VariantId
        public Guid VariantId { get; set; }
        public string VariantTitle { get; set; } = null!;

        // Info Product để FE hiển thị
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
    /// Full Order DTO for Order Detail page
    /// </summary>
    public class OrderDTO
    {
        public Guid OrderId { get; set; }

        // UserId cho phép null
        public Guid? UserId { get; set; }

        // Email gắn với đơn hàng
        public string Email { get; set; } = null!;

        // Info user (nếu có)
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string? UserPhone { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public List<OrderDetailDTO> OrderDetails { get; set; } = new();
        public List<PaymentDTO> Payments { get; set; } = new(); // 👈 DTO payment mới

        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Partial, Paid, Refunded
    }

    /// <summary>
    /// Order List Item DTO for Order Management (Admin)
    /// </summary>
    public class OrderListItemDTO
    {
        public Guid OrderId { get; set; }
        public Guid? UserId { get; set; }

        // Email đơn hàng
        public string Email { get; set; } = null!;

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
        public string PaymentStatus { get; set; } = "Unpaid";
    }

    /// <summary>
    /// DTO dùng chung cho tạo đơn (admin hoặc storefront) – 
    /// ĐỐI VỚI LUỒNG CHECKOUT, Status sẽ bị BỎ QUA và luôn tạo "Pending".
    /// </summary>
    public class CreateOrderDTO
    {
        public Guid? UserId { get; set; }

        // Email bắt buộc
        public string Email { get; set; } = null!;

        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; } = 0;

        // Giữ lại cho admin, nhưng khi checkout sẽ không dùng giá trị client gửi lên
        public string Status { get; set; } = "Pending";

        public List<CreateOrderDetailDTO> OrderDetails { get; set; } = new();
    }

    /// <summary>
    /// DTO for creating order detail
    /// </summary>
    public class CreateOrderDetailDTO
    {
        // VariantId
        public Guid VariantId { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // LUỒNG CHECKOUT: chưa gắn Key, logic gắn key xử lý sau khi thanh toán thành công
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

    /// <summary>
    /// DTO trả về cho FE khi gọi /api/orders/checkout
    /// (hiện tại mày đang trả { orderId } anonymous, có thể dùng DTO này sau nếu muốn).
    /// </summary>
    public class CheckoutOrderResponseDTO
    {
        public Guid OrderId { get; set; }
        public string PaymentUrl { get; set; } = null!;
    }
}
