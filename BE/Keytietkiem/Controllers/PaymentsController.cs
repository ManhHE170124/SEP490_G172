using Keytietkiem.DTOs.Payments;
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

        private static readonly string[] AllowedPaymentStatuses = new[]
        {
            "Pending", "Paid", "Success", "Completed", "Cancelled", "Failed", "Refunded"
        };

        public PaymentsController(
            KeytietkiemDbContext context,
            PayOSService payOs,
            IConfiguration config)
        {
            _context = context;
            _payOs = payOs;
            _config = config;
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
                    OrderId = p.OrderId,
                    Amount = p.Amount,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    Provider = p.Provider,
                    ProviderOrderCode = p.ProviderOrderCode
                })
                .ToList() ?? new List<PaymentDTO>();

            return Ok(payments);
        }

        // ===== API ADMIN: list payment với filter cơ bản =====
        // GET /api/payments?status=Paid&provider=PayOS&orderId=...
        [HttpGet]
        public async Task<IActionResult> GetPayments(
            [FromQuery] string? status,
            [FromQuery] string? provider,
            [FromQuery] Guid? orderId)
        {
            var query = _context.Payments
                .Include(p => p.Order)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim();
                query = query.Where(p => p.Status == normalized);
            }

            if (!string.IsNullOrWhiteSpace(provider))
            {
                var normalizedProvider = provider.Trim();
                query = query.Where(p => p.Provider == normalizedProvider);
            }

            if (orderId.HasValue && orderId.Value != Guid.Empty)
            {
                query = query.Where(p => p.OrderId == orderId.Value);
            }

            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PaymentAdminListItemDTO
                {
                    PaymentId = p.PaymentId,
                    OrderId = p.OrderId,
                    Amount = p.Amount,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    Provider = p.Provider,
                    ProviderOrderCode = p.ProviderOrderCode,
                    OrderEmail = p.Order != null ? p.Order.Email : string.Empty,
                    OrderStatus = p.Order != null ? p.Order.Status : string.Empty,
                    OrderCreatedAt = p.Order != null ? p.Order.CreatedAt : DateTime.MinValue
                })
                .ToListAsync();

            return Ok(items);
        }

        // ===== API ADMIN: xem chi tiết 1 payment =====
        // GET /api/payments/{paymentId}
        [HttpGet("{paymentId:guid}")]
        public async Task<IActionResult> GetPaymentById(Guid paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
            {
                return NotFound(new { message = "Payment không tồn tại" });
            }

            var dto = new PaymentDetailDTO
            {
                PaymentId = payment.PaymentId,
                OrderId = payment.OrderId,
                Amount = payment.Amount,
                Status = payment.Status,
                CreatedAt = payment.CreatedAt,
                Provider = payment.Provider,
                ProviderOrderCode = payment.ProviderOrderCode,
                OrderEmail = payment.Order?.Email ?? string.Empty,
                OrderStatus = payment.Order?.Status,
                OrderTotalAmount = payment.Order?.TotalAmount ?? 0,
                OrderFinalAmount = payment.Order?.FinalAmount
            };

            return Ok(dto);
        }

        // ===== API ADMIN: đổi trạng thái payment bằng tay =====
        // PUT /api/payments/{paymentId}/status
        [HttpPut("{paymentId:guid}/status")]
        public async Task<IActionResult> UpdatePaymentStatus(
            Guid paymentId,
            [FromBody] UpdatePaymentStatusDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
            {
                return BadRequest(new { message = "Trạng thái thanh toán không hợp lệ" });
            }

            var normalizedStatus = dto.Status.Trim();

            if (!AllowedPaymentStatuses.Contains(normalizedStatus))
            {
                return BadRequest(new { message = "Trạng thái thanh toán không hợp lệ" });
            }

            var payment = await _context.Payments
                .Include(p => p.Order)
                    .ThenInclude(o => o.Payments)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
            {
                return NotFound(new { message = "Payment không tồn tại" });
            }

            var currentStatus = payment.Status ?? "Pending";

            // Nếu payment đã Paid hoặc Cancelled thì không cho chỉnh nữa
            if (currentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase) ||
                currentStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Không thể cập nhật trạng thái thanh toán khi đã ở trạng thái Paid hoặc Cancelled." });
            }

            // Chỉ cho phép chỉnh tay khi đang Pending hoặc Failed
            if (!currentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                !currentStatus.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ được phép chỉnh tay trạng thái thanh toán khi đang ở Pending hoặc Failed." });
            }

            // Chỉ cho phép chuyển sang Paid hoặc Cancelled
            if (!normalizedStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase) &&
                !normalizedStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ được phép chuyển trạng thái thanh toán sang Paid hoặc Cancelled." });
            }

            payment.Status = normalizedStatus;

            // Recompute Order.Status dựa trên tổng tiền đã trả
            var order = payment.Order;
            if (order != null)
            {
                var finalAmount = order.FinalAmount ?? (order.TotalAmount - order.DiscountAmount);
                var totalPaid = order.Payments
                    .Where(p => p.Status == "Paid" || p.Status == "Success" || p.Status == "Completed")
                    .Sum(p => p.Amount);

                if (finalAmount > 0 && totalPaid >= finalAmount)
                {
                    order.Status = "Paid";
                }
                else if (order.Status == "Paid" && totalPaid < finalAmount)
                {
                    order.Status = "Pending";
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
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

            // Chuẩn bị data gọi PayOS
            var description = EncodeOrderIdToDescription(order.OrderId);

            // orderCode unique (int) – chính là cái sẽ lưu xuống Payment.ProviderOrderCode
            var orderCode = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);

            var frontendBaseUrl = _config["PayOS:FrontendBaseUrl"]?.TrimEnd('/')
                                  ?? "https://keytietkiem.com";

            var returnUrl = $"{frontendBaseUrl}/payment-result?orderId={order.OrderId}";
            var cancelUrl = $"{frontendBaseUrl}/payment-cancel?orderId={order.OrderId}";

            var buyerEmail = order.Email;
            var buyerName = order.User?.FullName ?? order.Email;
            var buyerPhone = order.User?.Phone ?? "";
            var amountInt = (int)Math.Round(amountToPay, 0, MidpointRounding.AwayFromZero);

            // Tạo Payment Pending
            var paymentNew = new Payment
            {
                OrderId = order.OrderId,
                Amount = amountToPay,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Provider = "PayOS",
                ProviderOrderCode = orderCode
            };

            _context.Payments.Add(paymentNew);
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
                PaymentId = paymentNew.PaymentId,
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
            if (payload == null || payload.Data == null)
                return BadRequest();

            var data = payload.Data;

            var topCode = (payload.Code ?? "").Trim();
            var dataCode = (data.Code ?? "").Trim();

            // TÌM PAYMENT THEO ProviderOrderCode THAY VÌ DESCRIPTION
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

                payment.Status = "Paid";
                payment.Amount = amountDecimal;

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

        // ===== Helpers encode OrderId -> description (<= 25 ký tự) =====

        /// <summary>
        /// Encode Guid OrderId thành chuỗi Base64 URL-safe, bỏ padding,
        /// thêm tiền tố 'K' → độ dài luôn &lt;= 23 ký tự (thỏa điều kiện &lt;= 25).
        /// </summary>
        private static string EncodeOrderIdToDescription(Guid orderId)
        {
            var bytes = orderId.ToByteArray();

            var base64 = Convert.ToBase64String(bytes)
                .TrimEnd('=')          // bỏ padding
                .Replace('+', '-')     // URL-safe
                .Replace('/', '_');    // URL-safe

            return "K" + base64;
        }
    }
}
