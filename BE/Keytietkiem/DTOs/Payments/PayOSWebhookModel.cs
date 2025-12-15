using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Keytietkiem.DTOs.Payments
{
    // ====== WEBHOOK TỪ PAYOS ======

    public class PayOSWebhookData
    {
        [JsonPropertyName("orderCode")]
        public long OrderCode { get; set; }

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("paymentLinkId")]
        public string? PaymentLinkId { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("desc")]
        public string? Desc { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("reference")]
        public string? Reference { get; set; }

        [JsonPropertyName("transactionDateTime")]
        public string? TransactionDateTime { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("accountNumber")]
        public string? AccountNumber { get; set; }

        [JsonPropertyName("counterAccountBankId")]
        public string? CounterAccountBankId { get; set; }

        [JsonPropertyName("counterAccountBankName")]
        public string? CounterAccountBankName { get; set; }

        [JsonPropertyName("counterAccountName")]
        public string? CounterAccountName { get; set; }

        [JsonPropertyName("counterAccountNumber")]
        public string? CounterAccountNumber { get; set; }

        [JsonPropertyName("virtualAccountName")]
        public string? VirtualAccountName { get; set; }

        [JsonPropertyName("virtualAccountNumber")]
        public string? VirtualAccountNumber { get; set; }

        // ✅ Giữ các field phát sinh về sau để verify signature không bị lệch
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    public class PayOSWebhookModel
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("desc")]
        public string? Desc { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public PayOSWebhookData? Data { get; set; }

        // ✅ PayOS gửi signature ở top-level
        [JsonPropertyName("signature")]
        public string? Signature { get; set; }
    }

    // ====== RETURN DTOs (FE return flow cho ORDER) ======

    public class ConfirmOrderPaymentRequestDto
    {
        public Guid PaymentId { get; set; }
        public string? Code { get; set; }
        public string? Status { get; set; }
    }

    public class CancelOrderPaymentRequestDto
    {
        public Guid PaymentId { get; set; }
        public string? Code { get; set; }
        public string? Status { get; set; }
    }

    public class PaymentDetailDTO
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Provider { get; set; }
        public long? ProviderOrderCode { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? Email { get; set; }
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }

        // ✅ thêm để phù hợp flow mới
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsExpired { get; set; }

        // ✅ Admin có thể bấm mở lại QR/link nếu cần (lấy từ PayOS theo PaymentLinkId)
        public string? CheckoutUrl { get; set; }

        // ✅ snapshot target (Order/SupportPlan + User)
        public PaymentTargetSnapshotDTO? TargetSnapshot { get; set; }

        // ✅ toàn bộ attempts cho cùng target (hữu ích khi multi-tab tạo nhiều payment)
        public List<PaymentAttemptDTO>? Attempts { get; set; }
    }

    public class PaymentAttemptDTO
    {
        public Guid PaymentId { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Provider { get; set; }
        public long? ProviderOrderCode { get; set; }
        public string? PaymentLinkId { get; set; }

        public DateTime ExpiresAtUtc { get; set; }
        public bool IsExpired { get; set; }
    }

    public class PaymentTargetSnapshotDTO
    {
        // user (nếu resolve được)
        public Guid? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }

        // order
        public Guid? OrderId { get; set; }
        public string? OrderStatus { get; set; }
        public string? OrderEmail { get; set; }
        public DateTime? OrderCreatedAt { get; set; }
        public decimal? OrderTotalAmount { get; set; }
        public decimal? OrderDiscountAmount { get; set; }
        public decimal? OrderFinalAmount { get; set; }

        // support plan
        public int? SupportPlanId { get; set; }
        public string? SupportPlanName { get; set; }
        public int? SupportPlanPriorityLevel { get; set; }
        public decimal? SupportPlanPrice { get; set; }
    }

    public class PaymentAdminListItemDTO
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Provider { get; set; }
        public long? ProviderOrderCode { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? Email { get; set; }
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }

        // ✅ thêm để phù hợp flow mới
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsExpired { get; set; }
        public bool IsLatestAttemptForTarget { get; set; }

        // ✅ target snapshot (admin nhìn nhanh)
        public Guid? OrderId { get; set; }
        public string? OrderStatus { get; set; }

        public Guid? TargetUserId { get; set; }
        public string? TargetUserEmail { get; set; }
        public string? TargetUserName { get; set; }

        public int? SupportPlanId { get; set; }
        public string? SupportPlanName { get; set; }
        public int? SupportPlanPriority { get; set; }
    }
}
