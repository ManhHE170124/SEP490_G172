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
    }

    /// <summary>
    /// Response khi tạo Payment PayOS cho gói hỗ trợ.
    /// </summary>
    public class CreateSupportPlanPayOSPaymentResponseDTO
    {
        /// <summary>
        /// Id payment đã tạo trong hệ thống.
        /// </summary>
        public Guid PaymentId { get; set; }

        /// <summary>
        /// Id gói hỗ trợ mà user đang mua.
        /// </summary>
        public int SupportPlanId { get; set; }

        /// <summary>
        /// Tên gói hỗ trợ để FE hiển thị.
        /// </summary>
        public string SupportPlanName { get; set; } = string.Empty;

        /// <summary>
        /// Giá gốc của gói (theo 1 kỳ, ví dụ 1 tháng).
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Số tiền thực tế user cần thanh toán sau khi đã điều chỉnh
        /// theo Priority gốc + subscription hiện tại (pro-rate, chênh lệch...).
        /// Đây là giá sẽ được gửi sang PayOS.
        /// </summary>
        public decimal AdjustedAmount { get; set; }

        /// <summary>
        /// Url thanh toán PayOS để redirect user.
        /// </summary>
        public string PaymentUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO confirm sau khi thanh toán thành công, dùng bởi FE để yêu cầu
    /// hệ thống kích hoạt / gia hạn subscription.
    /// </summary>
    public class ConfirmSupportPlanPaymentDTO
    {
        /// <summary>
        /// PaymentId đã được PayOS báo trạng thái Paid (qua webhook).
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
