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

            var totalPaid = order.Payments?
                .Where(p => p.Status == "Success" || p.Status == "Completed" || p.Status == "Paid")
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

            // 👇 CHUẨN BỊ DATA PAYOS TRƯỚC
            var description = EncodeOrderIdToDescription(order.OrderId); // vẫn có thể giữ để dễ debug/log

            // 👇 orderCode unique (int) – chính là cái sẽ lưu xuống Payment.ProviderOrderCode
            var orderCode = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);

            var frontendBaseUrl = _config["PayOS:FrontendBaseUrl"]?.TrimEnd('/')
                                  ?? "https://keytietkiem.com";

            var returnUrl = $"{frontendBaseUrl}/payment-result?orderId={order.OrderId}";
            var cancelUrl = $"{frontendBaseUrl}/payment-cancel?orderId={order.OrderId}";

            var buyerEmail = order.Email;
            var buyerName = order.User?.FullName ?? order.Email;
            var buyerPhone = order.User?.Phone ?? "";

            var amountInt = (int)Math.Round(amountToPay, 0, MidpointRounding.AwayFromZero);

            // 👇 TẠO PAYMENT VỚI Provider + ProviderOrderCode
            var payment = new Payment
            {
                OrderId = order.OrderId,
                Amount = amountToPay,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Provider = "PayOS",           // 👈
                ProviderOrderCode = orderCode // 👈 lưu lại cho webhook
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

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
        // POST /api/payments/payos/webhook
        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookModel payload)
        {
            if (payload == null || payload.Data == null)
                return BadRequest();

            var data = payload.Data;

            var topCode = (payload.Code ?? "").Trim();
            var dataCode = (data.Code ?? "").Trim();

            // 👇 TÌM PAYMENT THEO ProviderOrderCode THAY VÌ DESCRIPTION
            var payment = await _context.Payments
                .Include(p => p.Order)
                    .ThenInclude(o => o.OrderDetails)
                        .ThenInclude(od => od.Variant)
                .Include(p => p.Order)
                    .ThenInclude(o => o.Payments)
                .FirstOrDefaultAsync(p =>
                    p.Provider == "PayOS" &&
                    p.ProviderOrderCode == data.OrderCode
                );

            if (payment == null)
            {
                // Không tìm được payment trùng orderCode -> có thể log lại
                return Ok(); // trả 200 cho PayOS để nó không spam webhook
            }

            var order = payment.Order;
            if (order == null)
            {
                return Ok();
            }

            // Nếu order không còn Pending thì thôi, tránh xử lý lại
            if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return Ok();
            }

            if (topCode == "00" && dataCode == "00")
            {
                // Thanh toán thành công
                var amountDecimal = (decimal)data.Amount;

                // Cập nhật đúng payment tương ứng với orderCode này
                payment.Status = "Paid";
                payment.Amount = amountDecimal;

                // Cập nhật order
                order.Status = "Paid";

                // Nếu muốn, chỉ set FinalAmount khi chưa có:
                if (!(order.FinalAmount.HasValue && order.FinalAmount.Value > 0))
                {
                    order.FinalAmount = amountDecimal;
                }

                await _context.SaveChangesAsync();
                return Ok();
            }

            // Các trường hợp code != "00" (giao dịch thất bại / huỷ)
            order.Status = "Cancelled";

            if (payment.Status == "Pending")
            {
                payment.Status = "Cancelled";
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

        
    }
}
