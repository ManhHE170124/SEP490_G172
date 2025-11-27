using System;

namespace Keytietkiem.DTOs.Payments
{
    // ====== WEBHOOK TỪ PAYOS ======

    public class PayOSWebhookData
    {
        public long OrderCode { get; set; }
        public int Amount { get; set; }
        public string Description { get; set; } = "";

        // Các field còn lại không bắt buộc dùng, nhưng khai cho đầy đủ nếu muốn log
        public string AccountNumber { get; set; } = "";
        public string Reference { get; set; } = "";
        public string TransactionDateTime { get; set; } = "";
        public string Currency { get; set; } = "";
        public string PaymentLinkId { get; set; } = "";
        public string Code { get; set; } = "";  // "00" = thành công
        public string Desc { get; set; } = "";
    }

    public class PayOSWebhookModel
    {
        public string Code { get; set; } = "";     // "00" = success
        public string Desc { get; set; } = "";
        public bool Success { get; set; }
        public PayOSWebhookData Data { get; set; } = new PayOSWebhookData();

        // Chữ ký từ PayOS – mày có thể verify sau
        public string Signature { get; set; } = "";
    }

    // ====== DTO PAYMENT DÙNG CHUNG ======

    /// <summary>
    /// DTO dùng chung để embed vào Order, hoặc trả về khi query payment.
    /// </summary>
    public class PaymentDTO
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }

        public decimal Amount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public string? Provider { get; set; }
        public long? ProviderOrderCode { get; set; }
    }

    /// <summary>
    /// DTO cho màn list payment ở phía admin.
    /// </summary>
    public class PaymentAdminListItemDTO : PaymentDTO
    {
        public string OrderEmail { get; set; } = null!;
        public string OrderStatus { get; set; } = null!;
        public DateTime OrderCreatedAt { get; set; }
    }

    /// <summary>
    /// DTO xem chi tiết 1 payment (admin).
    /// </summary>
    public class PaymentDetailDTO : PaymentDTO
    {
        public string OrderEmail { get; set; } = null!;
        public string? OrderStatus { get; set; }
        public decimal OrderTotalAmount { get; set; }
        public decimal? OrderFinalAmount { get; set; }
    }

    // ====== DTO TẠO PAYMENT PAYOS ======

    /// <summary>
    /// Request body khi tạo payment PayOS.
    /// </summary>
    public class CreatePayOSPaymentDTO
    {
        public Guid OrderId { get; set; }
    }

    /// <summary>
    /// Response khi tạo payment PayOS.
    /// </summary>
    public class CreatePayOSPaymentResponseDTO
    {
        public Guid OrderId { get; set; }
        public Guid PaymentId { get; set; }
        public string PaymentUrl { get; set; } = null!;
    }

    // ====== DTO ADMIN UPDATE STATUS PAYMENT ======

    public class UpdatePaymentStatusDTO
    {
        public string Status { get; set; } = null!;
    }
}
