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

        // ===== API ADMIN: list payment với filter cơ bản + sort =====
        // GET /api/payments?status=Paid&provider=PayOS&email=...&transactionType=ORDER_PAYMENT&sortBy=CreatedAt&sortDir=desc
        [HttpGet]
        public async Task<IActionResult> GetPayments(
            [FromQuery] string? status,
            [FromQuery] string? provider,
            [FromQuery] string? email,
            [FromQuery] string? transactionType,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDir)
        {
            var query = _context.Payments.AsQueryable();

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

            if (!string.IsNullOrWhiteSpace(email))
            {
                var normalizedEmail = email.Trim();
                query = query.Where(p => p.Email == normalizedEmail);
            }

            if (!string.IsNullOrWhiteSpace(transactionType))
            {
                var normalizedType = transactionType.Trim();
                query = query.Where(p => p.TransactionType == normalizedType);
            }

            // Sort phía server
            var sortByNorm = (sortBy ?? "CreatedAt").Trim();
            var sortDirNorm = (sortDir ?? "desc").Trim().ToLowerInvariant();
            var asc = sortDirNorm == "asc";

            switch (sortByNorm.ToLowerInvariant())
            {
                case "paymentid":
                    query = asc
                        ? query.OrderBy(p => p.PaymentId)
                        : query.OrderByDescending(p => p.PaymentId);
                    break;

                case "amount":
                    query = asc
                        ? query.OrderBy(p => p.Amount)
                        : query.OrderByDescending(p => p.Amount);
                    break;

                case "status":
                    query = asc
                        ? query.OrderBy(p => p.Status)
                        : query.OrderByDescending(p => p.Status);
                    break;

                case "provider":
                    query = asc
                        ? query.OrderBy(p => p.Provider)
                        : query.OrderByDescending(p => p.Provider);
                    break;

                case "transactiontype":
                    query = asc
                        ? query.OrderBy(p => p.TransactionType)
                        : query.OrderByDescending(p => p.TransactionType);
                    break;

                case "email":
                    query = asc
                        ? query.OrderBy(p => p.Email)
                        : query.OrderByDescending(p => p.Email);
                    break;

                case "providerordercode":
                    query = asc
                        ? query.OrderBy(p => p.ProviderOrderCode)
                        : query.OrderByDescending(p => p.ProviderOrderCode);
                    break;

                default: // CreatedAt
                    query = asc
                        ? query.OrderBy(p => p.CreatedAt)
                        : query.OrderByDescending(p => p.CreatedAt);
                    break;
            }

            var items = await query
                .Select(p => new PaymentAdminListItemDTO
                {
                    PaymentId = p.PaymentId,
                    Amount = p.Amount,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    Provider = p.Provider,
                    ProviderOrderCode = p.ProviderOrderCode,
                    Email = p.Email,
                    TransactionType = p.TransactionType
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
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
            {
                return NotFound(new { message = "Payment không tồn tại" });
            }

            var dto = new PaymentDetailDTO
            {
                PaymentId = payment.PaymentId,
                Amount = payment.Amount,
                Status = payment.Status,
                CreatedAt = payment.CreatedAt,
                Provider = payment.Provider,
                ProviderOrderCode = payment.ProviderOrderCode,
                Email = payment.Email,
                TransactionType = payment.TransactionType
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
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId);

            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không tồn tại" });
            }

            if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ có thể thanh toán đơn hàng ở trạng thái Pending" });
            }

            var finalAmount = order.FinalAmount ?? (order.TotalAmount - order.DiscountAmount);
            if (finalAmount <= 0)
            {
                return BadRequest(new { message = "Số tiền thanh toán không hợp lệ" });
            }

            using var tx = await _context.Database.BeginTransactionAsync();

            // description encode OrderId để webhook decode lại
            var description = EncodeOrderIdToDescription(order.OrderId);

            // orderCode unique (int) – lưu vào Payment.ProviderOrderCode
            var orderCode = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);

            var frontendBaseUrl = _config["PayOS:FrontendBaseUrl"]?.TrimEnd('/')
                                  ?? "https://keytietkiem.com";

            var returnUrl = $"{frontendBaseUrl}/payment-result?orderId={order.OrderId}";
            var cancelUrl = $"{frontendBaseUrl}/payment-cancel?orderId={order.OrderId}";

            var buyerEmail = order.Email;
            var buyerName = order.User?.FullName ?? order.Email;
            var buyerPhone = order.User?.Phone ?? "";
            var amountInt = (int)Math.Round(finalAmount, 0, MidpointRounding.AwayFromZero);

            // Tạo Payment Pending – BẢNG ĐỘC LẬP, KHÔNG CÓ OrderId
            var paymentNew = new Payment
            {
                Amount = finalAmount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Provider = "PayOS",
                ProviderOrderCode = orderCode,
                Email = order.Email,
                TransactionType = "ORDER_PAYMENT"
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

            // TÌM PAYMENT THEO ProviderOrderCode
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p =>
                    p.Provider == "PayOS" &&
                    p.ProviderOrderCode == data.OrderCode
                );

            // Decode OrderId từ description
            if (!TryDecodeOrderIdFromDescription(data.Description, out var orderId))
            {
                // Không decode được OrderId => chỉ update payment (nếu có), không động tới Orders
                if (payment != null)
                {
                    if (topCode == "00" && dataCode == "00")
                    {
                        payment.Status = "Paid";
                        payment.Amount = data.Amount;
                    }
                    else if (payment.Status == "Pending")
                    {
                        payment.Status = "Cancelled";
                    }

                    await _context.SaveChangesAsync();
                }

                return Ok();
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                // Không tìm thấy Order -> vẫn update payment nếu có
                if (payment != null)
                {
                    if (topCode == "00" && dataCode == "00")
                    {
                        payment.Status = "Paid";
                        payment.Amount = data.Amount;
                    }
                    else if (payment.Status == "Pending")
                    {
                        payment.Status = "Cancelled";
                    }

                    await _context.SaveChangesAsync();
                }

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

                if (payment != null)
                {
                    payment.Status = "Paid";
                    payment.Amount = amountDecimal;
                }

                order.Status = "Paid";

                if (!(order.FinalAmount.HasValue && order.FinalAmount.Value > 0))
                {
                    order.FinalAmount = amountDecimal;
                }

                await _context.SaveChangesAsync();
                return Ok();
            }

            // Các trường hợp code != "00" (giao dịch thất bại / huỷ)
            order.Status = "Cancelled";

            if (payment != null && payment.Status == "Pending")
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

        // ===== Helpers encode/decode OrderId =====

        private static string EncodeOrderIdToDescription(Guid orderId)
        {
            var bytes = orderId.ToByteArray();

            var base64 = Convert.ToBase64String(bytes)
                .TrimEnd('=')          // bỏ padding
                .Replace('+', '-')     // URL-safe
                .Replace('/', '_');    // URL-safe

            return "K" + base64;
        }

        private static bool TryDecodeOrderIdFromDescription(string description, out Guid orderId)
        {
            orderId = Guid.Empty;

            if (string.IsNullOrWhiteSpace(description) || !description.StartsWith("K"))
                return false;

            var base64 = description.Substring(1)
                .Replace('-', '+')
                .Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            try
            {
                var bytes = Convert.FromBase64String(base64);
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