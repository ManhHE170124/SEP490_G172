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

        public List<Guid> KeyIds { get; set; } = new();
        public List<string> KeyStrings { get; set; } = new();

        public string? AccountEmail { get; set; }

        // ✅ NEW
        public string? AccountUsername { get; set; }

        public string? AccountPassword { get; set; }

        public List<OrderAccountCredentialDTO> Accounts { get; set; } = new();

        public decimal SubTotal { get; set; }
    }

    public class OrderAccountCredentialDTO
    {
        public string Email { get; set; } = null!;

        // ✅ NEW (optional)
        public string? Username { get; set; }

        public string Password { get; set; } = null!;
    }

    public class OrderDTO
    {
        public Guid OrderId { get; set; }
        public Guid? UserId { get; set; }
        public string? Email { get; set; }

        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string? UserPhone { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }

        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<OrderDetailDTO> OrderDetails { get; set; } = new();

        public string? OrderNumber { get; set; }
        public OrderPaymentSummaryDTO? Payment { get; set; }
        public List<OrderPaymentAttemptDTO>? PaymentAttempts { get; set; }
    }

    public class OrderPaymentSummaryDTO
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }

        public string? Provider { get; set; }
        public long? ProviderOrderCode { get; set; }
        public string? PaymentLinkId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsExpired { get; set; }
        public string? CheckoutUrl { get; set; }
    }


    public class OrderPaymentAttemptDTO
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }

        public string? Provider { get; set; }
        public long? ProviderOrderCode { get; set; }
        public string? PaymentLinkId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsExpired { get; set; }
    }


    public class OrderListItemDTO
    {
        public Guid OrderId { get; set; }
        public Guid? UserId { get; set; }
        public string? Email { get; set; }

        public string? UserName { get; set; }
        public string? UserEmail { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal FinalAmount { get; set; }

        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }

        // ✅ NEW
        public string? Status { get; set; }
        public string? OrderNumber { get; set; }

        public OrderPaymentSummaryDTO? Payment { get; set; }
        public int PaymentAttemptCount { get; set; }
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

    // ✅ Admin Order Detail response: Order + OrderItems (đúng yêu cầu)
    public class OrderDetailResponseDto
    {
        public OrderDTO Order { get; set; } = null!;
        public List<OrderDetailDTO> OrderItems { get; set; } = new();

        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
    }
}
