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

        // backward-compatible (single)
        public Guid? KeyId { get; set; }
        public string? KeyString { get; set; }

        // ✅ DB mới + bán multiple keys: trả list (nếu FE dùng)
        public List<Guid> KeyIds { get; set; } = new();
        public List<string> KeyStrings { get; set; } = new();

        public decimal SubTotal { get; set; } // Quantity * UnitPrice
    }

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

        // ✅ DB mới: computed, nhưng DTO vẫn trả về
        public decimal? FinalAmount { get; set; }

        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public List<OrderDetailDTO> OrderDetails { get; set; } = new();
    }

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

    public class OrderHistoryItemDTO
    {
        public Guid OrderId { get; set; }
        public Guid? UserId { get; set; }

        public string OrderNumber { get; set; } = null!; // ORD-YYYYMMDD-XXXX

        public string Email { get; set; } = null!;

        public decimal TotalAmount { get; set; }
        public decimal? FinalAmount { get; set; }

        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }

        public List<string> ProductNames { get; set; } = new();
    }
    public class CheckoutFromCartRequestDto
    {
        // Guest cart identify
        public string? AnonymousId { get; set; }

        // Guest bắt buộc
        public string? DeliveryEmail { get; set; }

        // Optional buyer info
        public string? BuyerName { get; set; }
        public string? BuyerPhone { get; set; }

        // Optional override return/cancel url
        public string? ReturnUrl { get; set; }
        public string? CancelUrl { get; set; }
    }

    public class CheckoutFromCartResponseDto
    {
        public Guid OrderId { get; set; }
        public Guid PaymentId { get; set; }

        // DB không lưu checkoutUrl, chỉ trả về cho FE
        public string? CheckoutUrl { get; set; }

        // Lưu trong DB để idempotency (fetch lại url)
        public string? PaymentLinkId { get; set; }

        public DateTime ExpiresAtUtc { get; set; }
    }
}
