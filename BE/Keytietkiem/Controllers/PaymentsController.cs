// Keytietkiem/Controllers/PaymentsController.cs
using Keytietkiem.DTOs.Cart;
using Keytietkiem.DTOs.Payments;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using static Keytietkiem.DTOs.Cart.StorefrontCartDto;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly PayOSService _payOs;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;

        private static readonly string[] AllowedPaymentStatuses = new[]
        {
            "Pending", "Paid", "Success", "Completed", "Cancelled", "Failed", "Refunded"
        };

        public PaymentsController(
            KeytietkiemDbContext context,
            PayOSService payOs,
            IConfiguration config,
            IMemoryCache cache)
        {
            _context = context;
            _payOs = payOs;
            _config = config;
            _cache = cache;
        }

        private Guid? GetCurrentUserIdOrNull()
        {
            var claim = User.FindFirst("uid") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;

            return Guid.TryParse(claim.Value, out var id) ? id : (Guid?)null;
        }

        // ===== API ADMIN: list payment =====
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

        // ===== Webhook PayOS =====
        // - ORDER_PAYMENT (cart checkout): dùng snapshot cart → Paid/Cancelled + Order.
        // - SUPPORT_PLAN / các loại khác: chỉ cập nhật Payment.Status.
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

            if (payment == null)
            {
                // Không có payment nào khớp orderCode -> bỏ qua
                return Ok();
            }

            // 1. Payment từ Cart (ORDER_PAYMENT với snapshot cart)
            if (string.Equals(payment.TransactionType, "ORDER_PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                await HandleCartPaymentWebhook(payment, topCode, dataCode, data.Amount);
                return Ok();
            }

            // 2. Các loại payment khác: SUPPORT_PLAN, ...
            var isSuccess = topCode == "00" && dataCode == "00";

            if (isSuccess)
            {
                var amountDecimal = (decimal)data.Amount;
                payment.Status = "Paid";
                payment.Amount = amountDecimal;
            }
            else if (payment.Status == "Pending")
            {
                payment.Status = "Cancelled";
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ===== API: FE xác nhận thanh toán Cart sau khi PayOS redirect (SUCCESS) =====
        // POST /api/payments/cart/confirm-from-return
        [HttpPost("cart/confirm-from-return")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmCartPaymentFromReturn(
            [FromBody] ConfirmCartPaymentRequestDto dto)
        {
            if (dto == null || dto.PaymentId == Guid.Empty)
            {
                return BadRequest(new { message = "PaymentId không hợp lệ" });
            }

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == dto.PaymentId);

            if (payment == null)
            {
                return NotFound(new { message = "Payment không tồn tại" });
            }

            if (!string.Equals(payment.TransactionType, "ORDER_PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Payment không thuộc loại ORDER_PAYMENT (cart checkout)" });
            }

            // Nếu đã được webhook xử lý rồi thì không làm lại (idempotent)
            if (!string.Equals(payment.Status ?? "", "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { message = "Payment đã được xử lý", status = payment.Status });
            }

            // Mặc định coi là success khi FE vào trang /cart/payment-result
            // (có thể kiểm tra thêm dto.Code/dto.Status nếu muốn)
            var amountFromGateway = (long)Math.Round(payment.Amount, 0, MidpointRounding.AwayFromZero);

            await HandleCartPaymentWebhook(payment, "00", "00", amountFromGateway);

            return Ok(new { message = "Đã xác nhận thanh toán thành công", status = payment.Status });
        }

        // ===== API: FE huỷ thanh toán Cart sau khi PayOS redirect (CANCEL) =====
        // POST /api/payments/cart/cancel-from-return
        [HttpPost("cart/cancel-from-return")]
        [AllowAnonymous]
        public async Task<IActionResult> CancelCartPaymentFromReturn(
            [FromBody] CancelCartPaymentRequestDto dto)
        {
            if (dto == null || dto.PaymentId == Guid.Empty)
            {
                return BadRequest(new { message = "PaymentId không hợp lệ" });
            }

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == dto.PaymentId);

            if (payment == null)
            {
                // Không tìm thấy payment -> coi như đã xử lý xong
                return Ok(new { message = "Payment không tồn tại" });
            }

            if (!string.Equals(payment.TransactionType, "ORDER_PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { message = "Không phải payment tạo từ cart" });
            }

            // Chỉ xử lý khi còn Pending
            if (string.Equals(payment.Status ?? "", "Pending", StringComparison.OrdinalIgnoreCase))
            {
                // Gọi logic cancel: status = Cancelled + hoàn kho + clear snapshot
                await HandleCartPaymentWebhook(payment, "XX", "XX", 0);
            }

            return Ok(new { message = "Cart payment cancelled", status = payment.Status });
        }

        // ===== Helpers cho snapshot Cart Payment =====

        private static string GetCartPaymentItemsCacheKey(Guid paymentId)
            => $"cart:payment:{paymentId:D}:items";

        private static string GetCartPaymentMetaCacheKey(Guid paymentId)
            => $"cart:payment:{paymentId:D}:meta";

        private (List<StorefrontCartItemDto>? Items, Guid? UserId, string? Email) GetCartSnapshot(Guid paymentId)
        {
            var itemsKey = GetCartPaymentItemsCacheKey(paymentId);
            var metaKey = GetCartPaymentMetaCacheKey(paymentId);

            _cache.TryGetValue(itemsKey, out List<StorefrontCartItemDto>? items);
            _cache.TryGetValue(metaKey, out (Guid? UserId, string Email) meta);

            if (items == null || items.Count == 0)
            {
                return (null, null, null);
            }

            var email = string.IsNullOrWhiteSpace(meta.Email) ? null : meta.Email;
            return (items, meta.UserId, email);
        }

        private void ClearCartSnapshot(Guid paymentId)
        {
            _cache.Remove(GetCartPaymentItemsCacheKey(paymentId));
            _cache.Remove(GetCartPaymentMetaCacheKey(paymentId));
        }

        /// <summary>
        /// - Success (00/00): Payment = Paid, tạo Order + OrderDetails từ snapshot, xoá snapshot.
        /// - Ngược lại: Payment = Cancelled, hoàn kho theo snapshot, xoá snapshot.
        /// </summary>
        private async Task HandleCartPaymentWebhook(
            Payment payment,
            string topCode,
            string dataCode,
            long amountFromGateway)
        {
            var currentStatus = payment.Status ?? "Pending";
            if (!string.Equals(currentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                // Đã xử lý trước đó
                return;
            }

            var isSuccess = topCode == "00" && dataCode == "00";

            if (isSuccess)
            {
                var (items, userId, email) = GetCartSnapshot(payment.PaymentId);
                if (items == null || !items.Any() || string.IsNullOrWhiteSpace(email))
                {
                    // Không còn dữ liệu cart -> không thể tạo order; cancel payment để tránh treo.
                    payment.Status = "Cancelled";
                    await _context.SaveChangesAsync();
                    ClearCartSnapshot(payment.PaymentId);
                    return;
                }

                decimal totalListAmount = 0m;
                decimal totalAmount = 0m;

                foreach (var item in items)
                {
                    var qty = item.Quantity < 0 ? 0 : item.Quantity;

                    var listPrice = item.ListPrice != 0 ? item.ListPrice : item.UnitPrice;
                    if (listPrice < 0) listPrice = 0;

                    var unitPrice = item.UnitPrice;
                    if (unitPrice < 0) unitPrice = 0;

                    totalListAmount += listPrice * qty;
                    totalAmount += unitPrice * qty;
                }

                totalListAmount = Math.Round(totalListAmount, 2, MidpointRounding.AwayFromZero);
                totalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);

                var amountDecimal = (decimal)amountFromGateway;

                payment.Status = "Paid";
                payment.Amount = amountDecimal;

                using var tx = await _context.Database.BeginTransactionAsync();

                // Order tạo từ cart: chỉ là log immutable
                var order = new Order
                {
                    UserId = userId,
                    Email = email!,
                    TotalAmount = totalListAmount,
                    DiscountAmount = totalListAmount - totalAmount,
                    FinalAmount = totalAmount,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in items)
                {
                    if (item.Quantity <= 0) continue;

                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.OrderId,
                        VariantId = item.VariantId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    };

                    _context.OrderDetails.Add(orderDetail);
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                ClearCartSnapshot(payment.PaymentId);
                return;
            }

            // Các trường hợp còn lại: user huỷ, thất bại, hết hạn...
            payment.Status = "Cancelled";

            // Hoàn kho dựa trên snapshot
            var (cancelItems, _, _) = GetCartSnapshot(payment.PaymentId);
            if (cancelItems != null && cancelItems.Any())
            {
                var variantIds = cancelItems.Select(i => i.VariantId).Distinct().ToList();

                var variants = await _context.ProductVariants
                    .Where(v => variantIds.Contains(v.VariantId))
                    .ToListAsync();

                foreach (var snapshotItem in cancelItems)
                {
                    var variant = variants.FirstOrDefault(v => v.VariantId == snapshotItem.VariantId);
                    if (variant != null && snapshotItem.Quantity > 0)
                    {
                        variant.StockQty += snapshotItem.Quantity;
                    }
                }
            }

            ClearCartSnapshot(payment.PaymentId);
            await _context.SaveChangesAsync();
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

            // Dùng UnixTimeSeconds làm orderCode đơn giản
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
