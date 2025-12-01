// File: DTOs/Payments/SupportPlanPaymentDTOs.cs
using System;

namespace Keytietkiem.DTOs.Payments
{
    /// <summary>
    /// Yêu cầu tạo Payment PayOS cho gói hỗ trợ (1 tháng).
    /// </summary>
    public class CreateSupportPlanPayOSPaymentDTO
    {
        public int SupportPlanId { get; set; }

        /// <summary>
        /// Ghi chú optional cho subscription (Lưu vào UserSupportPlanSubscription.Note nếu cần).
        /// </summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// Kết quả trả về sau khi tạo Payment PayOS cho gói hỗ trợ.
    /// </summary>
    public class CreateSupportPlanPayOSPaymentResponseDTO
    {
        public Guid PaymentId { get; set; }

        public int SupportPlanId { get; set; }

        public string SupportPlanName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        /// <summary>
        /// URL checkout PayOS – FE redirect user tới đây.
        /// </summary>
        public string PaymentUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Yêu cầu xác nhận thanh toán thành công & tạo subscription 1 tháng.
    /// </summary>
    public class ConfirmSupportPlanPaymentDTO
    {
        public Guid PaymentId { get; set; }

        public int SupportPlanId { get; set; }

        public string? Note { get; set; }
    }
}
