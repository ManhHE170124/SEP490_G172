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
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string Provider { get; set; } = null!;
        public long? ProviderOrderCode { get; set; } // ✅ DB mới bigint
        public string? PaymentLinkId { get; set; }
        public string Email { get; set; } = null!;
        public string TargetType { get; set; } = null!;
        public string? TargetId { get; set; }
    }

    public class PaymentAdminListItemDTO
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string Provider { get; set; } = null!;
        public long? ProviderOrderCode { get; set; } // ✅ DB mới bigint
        public string? PaymentLinkId { get; set; }
        public string Email { get; set; } = null!;
        public string TargetType { get; set; } = null!;
        public string? TargetId { get; set; }
    }
}
