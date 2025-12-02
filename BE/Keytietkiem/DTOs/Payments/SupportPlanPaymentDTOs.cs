// File: DTOs/Payments/SupportPlanPaymentDTOs.cs
using System;

namespace Keytietkiem.DTOs.Payments
{
    /// <summary>
    /// Request body khi tạo Payment PayOS cho gói hỗ trợ (subscription 1 tháng).
    /// </summary>
    public class CreateSupportPlanPayOSPaymentDTO
    {
        /// <summary>
        /// Id của gói hỗ trợ mà user muốn mua.
        /// </summary>
        public int SupportPlanId { get; set; }

        /// <summary>
        /// Ghi chú optional cho subscription.
        /// FE có thể giữ giá trị này và gửi lại ở bước confirm
        /// (sẽ được lưu vào UserSupportPlanSubscription.Note).
        /// </summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// Response khi tạo Payment PayOS cho gói hỗ trợ.
    /// Format tương tự CreatePayOSPaymentResponseDTO.
    /// </summary>
    public class CreateSupportPlanPayOSPaymentResponseDTO
    {
        /// <summary>
        /// Id payment trong bảng Payments, dùng cho bước confirm.
        /// </summary>
        public Guid PaymentId { get; set; }

        /// <summary>
        /// Id gói hỗ trợ mà payment này gắn với.
        /// </summary>
        public int SupportPlanId { get; set; }

        /// <summary>
        /// Tên gói hỗ trợ (hiển thị trên FE khi redirect xong).
        /// </summary>
        public string SupportPlanName { get; set; } = string.Empty;

        /// <summary>
        /// Giá của gói hỗ trợ tại thời điểm tạo payment.
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// URL thanh toán PayOS để FE redirect user.
        /// </summary>
        public string PaymentUrl { get; set; } = null!;
    }

    /// <summary>
    /// Yêu cầu xác nhận thanh toán thành công & tạo subscription 1 tháng.
    /// Được dùng ở API: POST /api/supportplans/confirm-payment
    /// </summary>
    public class ConfirmSupportPlanPaymentDTO
    {
        /// <summary>
        /// PaymentId tương ứng với giao dịch PayOS (bản ghi trong bảng Payments).
        /// </summary>
        public Guid PaymentId { get; set; }

        /// <summary>
        /// Id gói hỗ trợ mà user đang confirm.
        /// Dùng để double-check khớp với payment.Amount & payment.TransactionType.
        /// </summary>
        public int SupportPlanId { get; set; }

        /// <summary>
        /// Ghi chú cho subscription, lưu vào UserSupportPlanSubscription.Note.
        /// </summary>
        public string? Note { get; set; }
    }
}
