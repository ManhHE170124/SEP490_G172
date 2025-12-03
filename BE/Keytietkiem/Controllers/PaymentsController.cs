using Keytietkiem.DTOs.Payments;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        private Guid? GetCurrentUserIdOrNull()
        {
            var claim = User.FindFirst("uid") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;

            return Guid.TryParse(claim.Value, out var id) ? id : (Guid?)null;
        }

        // ===== API ADMIN: list payment với filter cơ bản =====
        // GET /api/payments?status=Paid&provider=PayOS&email=...&transactionType=ORDER_PAYMENT
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
        // ===== API: Tạo Payment + PayOS checkoutUrl cho gói hỗ trợ (subscription 1 tháng) =====
        // POST /api/payments/payos/create-support-plan
        [HttpPost("payos/create-support-plan")]
        [Authorize] // Customer phải đăng nhập
        public async Task<IActionResult> CreateSupportPlanPayOSPayment(
            [FromBody] CreateSupportPlanPayOSPaymentDTO dto)
        {
            if (dto == null || dto.SupportPlanId <= 0)
            {
                return BadRequest(new { message = "Gói hỗ trợ không hợp lệ" });
            }

            var currentUserId = GetCurrentUserIdOrNull();
            if (currentUserId == null) return Unauthorized();

            // Lấy user hiện tại
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return BadRequest(new { message = "Tài khoản của bạn chưa có email, không thể tạo thanh toán." });
            }

            var nowUtc = DateTime.UtcNow;

            // Lấy gói hỗ trợ target
            var plan = await _context.SupportPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.SupportPlanId == dto.SupportPlanId &&
                    p.IsActive);

            if (plan == null)
            {
                return BadRequest(new { message = "Gói hỗ trợ không tồn tại hoặc đã bị khóa." });
            }

            if (plan.Price <= 0)
            {
                return BadRequest(new { message = "Giá gói hỗ trợ không hợp lệ." });
            }

            // ================== TÍNH AdjustedAmount (theo số ngày) ==================
            // 1 gói = 30 ngày. Số ngày còn lại = ExpiresAt.Date - Today.Date
            // Công thức: Số tiền phải thanh toán = Giá gói được chọn - (Giá gói ban đầu * Số ngày còn lại / 30)
            var basePriority = user.SupportPriorityLevel;
            var targetPriority = plan.PriorityLevel;

            // Nếu gói mới có Priority <= priority gốc thì không cho mua
            if (targetPriority <= basePriority)
            {
                return BadRequest(new
                {
                    message = "Priority level gốc của bạn đã >= gói hỗ trợ này, không cần mua thêm."
                });
            }

            // Tìm subscription đang Active (nếu có)
            var activeSub = await _context.UserSupportPlanSubscriptions
                .Include(s => s.SupportPlan)
                .Where(s =>
                    s.UserId == user.UserId &&
                    s.Status == "Active" &&
                    s.ExpiresAt.HasValue &&
                    s.ExpiresAt > nowUtc)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            var effectiveCurrentPriority = basePriority;
            if (activeSub?.SupportPlan != null &&
                activeSub.SupportPlan.PriorityLevel > effectiveCurrentPriority)
            {
                effectiveCurrentPriority = activeSub.SupportPlan.PriorityLevel;
            }

            // Nếu gói mới vẫn <= priority hiện tại (tính cả subscription) thì không cho nâng
            if (targetPriority <= effectiveCurrentPriority)
            {
                return BadRequest(new
                {
                    message = "Bạn đã có Priority Level hiện tại >= gói hỗ trợ này. Chỉ có thể nâng cấp lên gói cao hơn."
                });
            }

            decimal adjustedAmount;
            const decimal periodDays = 30m;

            // basePrice = giá gói ban đầu (gói cấp thấp hơn dùng để khấu trừ)
            // remainingDays = số ngày còn lại (0..30)
            decimal? basePrice = null;
            decimal? remainingDays = null;

            if (activeSub != null && activeSub.SupportPlan != null)
            {
                // ==== TRƯỜNG HỢP 1: ĐANG CÓ GÓI HỖ TRỢ -> NÂNG CẤP GIỮA KỲ ====
                var oldPlan = activeSub.SupportPlan;

                if (oldPlan.PriorityLevel >= targetPriority)
                {
                    return BadRequest(new
                    {
                        message = "Gói hỗ trợ hiện tại của bạn đã có Priority Level >= gói mới."
                    });
                }

                basePrice = oldPlan.Price;

                // Số ngày còn lại (tính theo ngày, không quan tâm giờ)
                var days = (decimal)(activeSub.ExpiresAt!.Value.Date - nowUtc.Date).TotalDays;
                if (days < 0) days = 0;
                if (days > periodDays) days = periodDays;
                remainingDays = days;
            }
            else if (basePriority > 0)
            {
                // ==== TRƯỜNG HỢP 2: KHÔNG CÓ SUBSCRIPTION, NHƯNG BASE PRIORITY > 0 ====
                // (theo note: coi như user có quyền lợi gói base trong full kỳ, luôn tính như còn "max ngày")
                var basePlan = await _context.SupportPlans
                    .AsNoTracking()
                    .Where(p => p.IsActive && p.PriorityLevel == basePriority)
                    .OrderBy(p => p.Price)
                    .FirstOrDefaultAsync();

                if (basePlan != null)
                {
                    basePrice = basePlan.Price;
                    remainingDays = periodDays; // coi như còn full 30 ngày
                }
            }

            if (basePrice.HasValue &&
                remainingDays.HasValue &&
                basePrice.Value > 0 &&
                basePrice.Value < plan.Price &&
                remainingDays.Value > 0)
            {
                // Áp dụng công thức chung:
                // Số tiền phải thanh toán = Giá gói được chọn - (Giá gói ban đầu * Số ngày còn lại / 30)
                var ratio = periodDays == 0 ? 0 : (remainingDays.Value / periodDays); // 0..1
                var discount = basePrice.Value * ratio;
                adjustedAmount = plan.Price - discount;
            }
            else
            {
                // Không có gói cũ hợp lệ để khấu trừ -> thu full giá gói mới
                adjustedAmount = plan.Price;
            }

            if (adjustedAmount < 0) adjustedAmount = 0;
            adjustedAmount = Math.Round(adjustedAmount, 2, MidpointRounding.AwayFromZero);

            if (adjustedAmount <= 0)
            {
                return BadRequest(new { message = "Số tiền thanh toán không hợp lệ sau khi điều chỉnh." });
            }

            var amountInt = (int)Math.Round(adjustedAmount, 0, MidpointRounding.AwayFromZero);
            if (amountInt <= 0)
            {
                return BadRequest(new { message = "Số tiền thanh toán không hợp lệ (amountInt <= 0)." });
            }

            // ================== TẠO PAYMENT + PAYOS (giống /payos/create) ==================
            using var tx = await _context.Database.BeginTransactionAsync();

            // Dùng UnixTimeSeconds làm orderCode đơn giản (giống Order Payment)
            var orderCode = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);

            // Tạo bản ghi Payment Pending – bảng độc lập, TransactionType = SERVICE_PAYMENT
            var payment = new Payment
            {
                Amount = adjustedAmount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Provider = "PayOS",
                ProviderOrderCode = orderCode,
                Email = user.Email,
                TransactionType = "SERVICE_PAYMENT"
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            var frontendBaseUrl = _config["PayOS:FrontendBaseUrl"]?.TrimEnd('/')
                                  ?? "https://keytietkiem.com";

            // FE sẽ đọc PaymentId/SupportPlanId từ query để gọi /api/supportplans/confirm-payment
            var returnUrl =
                $"{frontendBaseUrl}/support/subscription?paymentId={payment.PaymentId}&supportPlanId={plan.SupportPlanId}";
            var cancelUrl =
                $"{frontendBaseUrl}/support/subscription";

            // Description đơn giản, giới hạn <= 25 ký tự cho PayOS
            var description = $"SP_{plan.SupportPlanId}";
            if (description.Length > 25)
            {
                description = description.Substring(0, 25);
            }

            var buyerEmail = user.Email;
            var buyerName = string.IsNullOrWhiteSpace(user.FullName)
                ? user.Email
                : user.FullName!;
            var buyerPhone = user.Phone ?? string.Empty;

            string paymentUrl;
            try
            {
                // Gọi PayOS để lấy checkoutUrl
                paymentUrl = await _payOs.CreatePayment(
                    orderCode,
                    amountInt,
                    description,
                    returnUrl,
                    cancelUrl,
                    buyerPhone,
                    buyerName,
                    buyerEmail
                );
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();

                return StatusCode(502, new
                {
                    message = "Không tạo được link thanh toán PayOS (support plan). Chi tiết: " + ex.Message
                });
            }

            await tx.CommitAsync();

            var resp = new CreateSupportPlanPayOSPaymentResponseDTO
            {
                PaymentId = payment.PaymentId,
                SupportPlanId = plan.SupportPlanId,
                SupportPlanName = plan.Name,
                Price = plan.Price,              // giá gốc gói
                AdjustedAmount = adjustedAmount, // giá thực thu (đã tính lại)
                PaymentUrl = paymentUrl
            };

            return Ok(resp);
        }
    }
}