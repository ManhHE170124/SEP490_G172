using System;

namespace Keytietkiem.DTOs.Payments
{
    public class PayOSWebhookModel
    {
        public long OrderCode { get; set; }           // int/long PayOS trả về
        public int Amount { get; set; }               // Số tiền thanh toán (int)
        public string Description { get; set; } = ""; // Mô tả, ta sẽ nhét OrderId vào đây
        public string Status { get; set; } = "";      // "PAID", "CANCELLED", "EXPIRED", ...
        public string TransactionId { get; set; } = "";
        public long Time { get; set; }
        public string Signature { get; set; } = "";   // Tạm thời chưa verify
    }
}
