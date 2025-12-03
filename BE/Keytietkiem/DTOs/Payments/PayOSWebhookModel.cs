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
    /// DTO dùng chung cho bảng Payments (bây giờ là bảng độc lập, không còn OrderId).
    /// </summary>
    public class PaymentDTO
    {
        public Guid PaymentId { get; set; }

        public decimal Amount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Email gắn với giao dịch (giống Email trong Orders).
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Loại giao dịch:
        /// - "DEPOSIT"          : nạp tiền vào tài khoản
        /// - "SERVICE_PAYMENT"  : thanh toán cho dịch vụ
        /// - "ORDER_PAYMENT"    : thanh toán cho đơn hàng
        /// </summary>
        public string TransactionType { get; set; } = null!;

        public string? Provider { get; set; }
        public long? ProviderOrderCode { get; set; }
    }

    /// <summary>
    /// DTO cho màn list payment ở phía admin.
    /// (hiện tại không còn thông tin Order kèm theo nữa)
    /// </summary>
    public class PaymentAdminListItemDTO : PaymentDTO
    {
        // Có thể bổ sung field khác nếu cần sau này.
    }

    /// <summary>
    /// DTO xem chi tiết 1 payment (admin).
    /// Hiện giờ giống PaymentDTO, tách riêng để sau này mở rộng thêm field chi tiết.
    /// </summary>
    public class PaymentDetailDTO : PaymentDTO
    {
    }

    // ====== DTO TẠO PAYMENT PAYOS ======

    /// <summary>
    /// Request body khi tạo payment PayOS cho 1 đơn hàng.
    /// OrderId chỉ dùng ở tầng API/service, bảng Payments không còn FK tới Orders.
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
    public class ConfirmCartPaymentRequestDto
    {
        public Guid PaymentId { get; set; }
        public string? Code { get; set; }
        public string? Status { get; set; }
    }

    public class CancelCartPaymentRequestDto
    {
        public Guid PaymentId { get; set; }

        // Có thể dùng để log thêm nếu cần
        public string? Code { get; set; }
        public string? Status { get; set; }
    }
}
