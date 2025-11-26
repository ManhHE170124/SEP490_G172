using System;

namespace Keytietkiem.DTOs.Payments
{
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
}
