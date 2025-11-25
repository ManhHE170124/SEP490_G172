using Keytietkiem.DTOs.Orders;     // dùng PaymentDTO
using Keytietkiem.DTOs.Payments;  // dùng PayOSWebhookModel
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly PayOSService _payOs;
        private readonly IConfiguration _config;

        public PaymentsController(
            KeytietkiemDbContext context,
            PayOSService payOs,
            IConfiguration config)
        {
            _context = context;
            _payOs = payOs;
            _config = config;
        }

        // ===== DTO nội bộ cho PaymentController =====

        public class CreatePayOSPaymentDTO
        {
            public Guid OrderId { get; set; }
        }

        public class CreatePayOSPaymentResponseDTO
        {
            public Guid OrderId { get; set; }
            public Guid PaymentId { get; set; }
            public string PaymentUrl { get; set; } = null!;
        }

        // ===== API: Lấy danh sách payment của 1 order =====
        // GET /api/payments/order/{orderId}
        [HttpGet("order/{orderId:guid}")]
        public async Task<IActionResult> GetPaymentsByOrder(Guid orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không tồn tại" });
            }

            var payments = order.Payments?
                .Select(p => new PaymentDTO
                {
                    PaymentId = p.PaymentId,
                    Amount = p.Amount,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt
                })
                .ToList() ?? new List<PaymentDTO>();

            return Ok(payments);
        }

        // ===== API: Tạo Payment + PayOS checkoutUrl từ 1 Order =====
        // POST /api/payments/payos/create
        [HttpPost("payos/create")]
        public async Task<IActionResult> CreatePayOSPayment([FromBody] CreatePayOSPaymentDTO dto)
        {
            if (dto == null || dto.OrderId == Guid.Empty)
            {
                return BadRequest(new { message = "OrderId không hợp lệ" });
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId);

            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không tồn tại" });
            }

            if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ có thể thanh toán đơn hàng ở trạng thái Pending" });
            }

            // Số tiền phải thanh toán
            var finalAmount = order.FinalAmount ?? (order.TotalAmount - order.DiscountAmount);
            if (finalAmount <= 0)
            {
                return BadRequest(new { message = "Số tiền thanh toán không hợp lệ" });
            }

            // Nếu sau này có partial payment thì trừ ra; hiện tại giả sử chưa có
            var totalPaid = order.Payments?
                .Where(p => p.Status == "Success" || p.Status == "Completed")
                .Sum(p => p.Amount) ?? 0m;

            if (totalPaid >= finalAmount)
            {
                return BadRequest(new { message = "Đơn hàng đã được thanh toán đủ" });
            }

            var amountToPay = finalAmount - totalPaid;
            if (amountToPay <= 0)
            {
                return BadRequest(new { message = "Không còn số tiền phải thanh toán" });
            }

            using var tx = await _context.Database.BeginTransactionAsync();

            // Tạo Payment Pending
            var payment = new Payment
            {
                OrderId = order.OrderId,
                Amount = amountToPay,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Chuẩn bị data gọi PayOS
            // Mô tả gửi lên PayOS: encode OrderId thành chuỗi <= 25 ký tự
            var description = EncodeOrderIdToDescription(order.OrderId);

            // orderCode: int unique (tạm dùng timestamp)
            var orderCode = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);

            // URL FE – đã có trong appsettings: PayOS:FrontendBaseUrl
            var frontendBaseUrl = _config["PayOS:FrontendBaseUrl"]?.TrimEnd('/')
                                  ?? "https://keytietkiem.com";

            var returnUrl = $"{frontendBaseUrl}/payment-result?orderId={order.OrderId}";
            var cancelUrl = $"{frontendBaseUrl}/payment-cancel?orderId={order.OrderId}";

            var buyerEmail = order.Email;
            var buyerName = order.User?.FullName ?? order.Email;
            var buyerPhone = order.User?.Phone ?? "";

            var amountInt = (int)Math.Round(amountToPay, 0, MidpointRounding.AwayFromZero);

            // Gọi PayOS để lấy checkoutUrl
            var paymentUrl = await _payOs.CreatePayment(
                orderCode,
                amountInt,
                description,
                returnUrl,
                cancelUrl,
                buyerPhone,
                buyerName,
                buyerEmail
            );

            await tx.CommitAsync();

            var resp = new CreatePayOSPaymentResponseDTO
            {
                OrderId = order.OrderId,
                PaymentId = payment.PaymentId,
                PaymentUrl = paymentUrl
            };

            return Ok(resp);
        }

        // ===== Webhook PayOS =====
        // POST /api/payments/payos/webhook
        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookModel payload)
        {
            if (payload == null)
                return BadRequest();

            // Parse OrderId từ Description đã encode (Base64 rút gọn)
            if (!TryDecodeOrderIdFromDescription(payload.Description, out var orderId) ||
                orderId == Guid.Empty)
            {
                // Không parse được thì bỏ qua (hoặc log sau)
                return Ok();
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return NotFound();

            // Payment mới nhất (pending)
            var payment = order.Payments?
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            // Nếu order không còn Pending thì thôi, tránh xử lý lại
            if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return Ok();
            }

            var status = (payload.Status ?? "").ToUpperInvariant();

            if (status == "PAID")
            {
                // Thanh toán thành công
                order.Status = "Paid";

                if (payment != null)
                {
                    payment.Status = "Success";
                }

                // FinalAmount set đúng bằng số thực tế trả
                var amountDecimal = (decimal)payload.Amount;
                order.FinalAmount = amountDecimal;

                // TODO: gắn key / account cho order (nếu bạn muốn làm sau)

                await _context.SaveChangesAsync();
                return Ok();
            }

            // Các trạng thái coi như fail/cancel
            if (status == "CANCELLED" || status == "FAILED" || status == "EXPIRED")
            {
                order.Status = "Cancelled";

                if (payment != null && payment.Status == "Pending")
                {
                    payment.Status = "Failed";
                }

                // Hoàn kho
                if (order.OrderDetails != null)
                {
                    foreach (var od in order.OrderDetails)
                    {
                        if (od.Variant != null)
                        {
                            od.Variant.StockQty += od.Quantity;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return Ok();
            }

            // Trạng thái khác: kệ
            return Ok();
        }

        // ===== Helpers encode/decode OrderId ↔ description (<= 25 ký tự) =====

        /// <summary>
        /// Encode Guid OrderId thành chuỗi Base64 URL-safe, bỏ padding,
        /// thêm tiền tố 'K' → độ dài luôn &lt;= 23 ký tự (thỏa điều kiện &lt;= 25).
        /// </summary>
        private static string EncodeOrderIdToDescription(Guid orderId)
        {
            var bytes = orderId.ToByteArray();

            // 16 bytes -> Base64 ~ 24 ký tự; bỏ '=' -> 22 ký tự
            var base64 = Convert.ToBase64String(bytes)
                .TrimEnd('=')          // bỏ padding
                .Replace('+', '-')     // URL-safe
                .Replace('/', '_');    // URL-safe

            // Thêm tiền tố 'K' cho dễ nhận diện → tổng ~ 23 ký tự
            return "K" + base64;
        }

        /// <summary>
        /// Decode chuỗi description (đã encode từ EncodeOrderIdToDescription) về lại Guid OrderId.
        /// </summary>
        private static bool TryDecodeOrderIdFromDescription(string? description, out Guid orderId)
        {
            orderId = Guid.Empty;

            if (string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            var encoded = description.Trim();

            // Bỏ tiền tố 'K' nếu có
            if (encoded.StartsWith("K"))
            {
                encoded = encoded.Substring(1);
            }

            // Đổi lại về Base64 "chuẩn"
            encoded = encoded
                .Replace('-', '+')
                .Replace('_', '/');

            // Thêm padding '=' cho đủ bội số của 4
            var mod = encoded.Length % 4;
            if (mod != 0)
            {
                encoded = encoded.PadRight(encoded.Length + (4 - mod), '=');
            }

            try
            {
                var bytes = Convert.FromBase64String(encoded);
                if (bytes.Length != 16) return false;

                orderId = new Guid(bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
