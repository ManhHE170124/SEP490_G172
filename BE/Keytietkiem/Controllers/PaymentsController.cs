// Keytietkiem/Controllers/PaymentsController.cs
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Cart;
using Keytietkiem.DTOs.Payments;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading;
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
        private readonly ILogger<PaymentsController> _logger;
        private readonly IProductKeyService _productKeyService;
        private readonly IProductAccountService _productAccountService;
        private readonly IEmailService _emailService;
        private readonly IAuditLogger _auditLogger;
        private readonly IAccountService _accountService;

        // Khóa global đơn giản để tránh xử lý trùng Payment (duplicate Order)
        private static readonly SemaphoreSlim CartPaymentSemaphore = new SemaphoreSlim(1, 1);

        private static readonly string[] AllowedPaymentStatuses = new[]
        {
            "Pending", "Paid", "Success", "Completed", "Cancelled", "Failed", "Refunded"
        };

        public PaymentsController(
            KeytietkiemDbContext context,
            PayOSService payOs,
            IConfiguration config,
            IMemoryCache cache,
            ILogger<PaymentsController> logger,
            IProductKeyService productKeyService,
            IProductAccountService productAccountService,
            IEmailService emailService,
            IAuditLogger auditLogger,
            IAccountService accountService)
        {
            _context = context;
            _payOs = payOs;
            _config = config;
            _cache = cache;
            _logger = logger;
            _productKeyService = productKeyService;
            _productAccountService = productAccountService;
            _emailService = emailService;
            _auditLogger = auditLogger;
            _accountService = accountService;
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

        // =========================================================
        //          WEBHOOK PayOS – CHUẨN CHO ORDER_PAYMENT
        // =========================================================
        // URL cấu hình trong PayOS: POST /api/payments/payos/webhook
        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> HandlePayOSWebhook([FromBody] PayOSWebhookModel payload)
        {
            if (payload == null || payload.Data == null)
            {
                return BadRequest(new { message = "Payload từ PayOS không hợp lệ." });
            }

            var orderCode = payload.Data.OrderCode;
            if (orderCode <= 0)
            {
                _logger.LogWarning("PayOS webhook thiếu hoặc sai orderCode: {@Payload}", payload);
                return BadRequest(new { message = "orderCode không hợp lệ." });
            }

            // Tìm payment theo ProviderOrderCode + Provider = PayOS
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p =>
                    p.Provider == "PayOS" &&
                    p.ProviderOrderCode == orderCode);

            if (payment == null)
            {
                _logger.LogWarning(
                    "PayOS webhook cho orderCode {OrderCode} nhưng không tìm thấy Payment.",
                    orderCode);

                // Vẫn trả 200 để PayOS không retry vô hạn
                return Ok(new { message = "Không tìm thấy payment, đã bỏ qua." });
            }

            // Hiện tại chỉ xử lý chuẩn cho ORDER_PAYMENT (cart).
            if (!string.Equals(payment.TransactionType, "ORDER_PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Nhận PayOS webhook cho payment {PaymentId} với TransactionType {Type} – chưa có logic xử lý riêng, bỏ qua.",
                    payment.PaymentId,
                    payment.TransactionType);

                return Ok(new { message = "Đã bỏ qua vì không phải ORDER_PAYMENT." });
            }

            var topCode = payload.Code ?? string.Empty; // code tổng
            var dataCode = !string.IsNullOrWhiteSpace(payload.Data.Code)
                ? payload.Data.Code
                : topCode; // code chi tiết nếu có
            var amountFromGateway = (long)payload.Data.Amount;

            try
            {
                // Dùng wrapper có SemaphoreSlim để tránh xử lý trùng
                await HandleCartPaymentWebhook(payment, topCode, dataCode, amountFromGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Lỗi khi xử lý PayOS webhook cho payment {PaymentId}, orderCode {OrderCode}",
                    payment.PaymentId, orderCode);

                // Cho PayOS biết là lỗi để nó có thể retry
                return StatusCode(500, new { message = "Lỗi nội bộ khi xử lý webhook." });
            }

            return Ok(new { message = "Webhook đã được xử lý." });
        }

        // ===== API: FE xác nhận thanh toán Cart sau khi PayOS redirect (SUCCESS) =====
        // POST /api/payments/cart/confirm-from-return
        //
        // MỚI: Không còn tự đổi trạng thái nữa.
        // - Trạng thái chuẩn được cập nhật bởi webhook PayOS.
        // - API này chỉ đọc status hiện tại cho FE hiển thị.
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

            // Không tự xử lý nữa – chỉ trả về trạng thái hiện tại
            return Ok(new
            {
                message = "Cart payment status",
                status = payment.Status ?? "Pending"
            });
        }

        // ===== API: FE huỷ thanh toán Cart sau khi PayOS redirect (CANCEL) =====
        // POST /api/payments/cart/cancel-from-return
        //
        // Nếu Payment vẫn Pending: coi như CANCEL (user hủy / đóng tab),
        // gọi HandleCartPaymentWebhook với code lỗi để hoàn kho + set Cancelled.
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
                // Không tìm thấy payment -> coi như đã xử lý xong, không audit để tránh spam
                return Ok(new { message = "Payment không tồn tại" });
            }

            if (!string.Equals(payment.TransactionType, "ORDER_PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                // Không audit để tránh spam
                return Ok(new { message = "Không phải payment tạo từ cart" });
            }

            // Nếu không còn Pending (đã Paid hoặc Cancelled) thì chỉ trả status hiện tại.
            if (!string.Equals(payment.Status ?? "", "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    message = "Cart payment status",
                    status = payment.Status
                });
            }

            // Thanh toán bị huỷ / user đóng tab: xử lý như CANCEL
            await HandleCartPaymentWebhook(payment, "99", "99", 0L);

            return Ok(new
            {
                message = "Cart payment status",
                status = payment.Status
            });
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
        /// Wrapper có khóa để tránh xử lý trùng một Payment khi có nhiều request song song.
        /// </summary>
        private async Task HandleCartPaymentWebhook(
            Payment paymentParam,
            string topCode,
            string dataCode,
            long amountFromGateway)
        {
            await CartPaymentSemaphore.WaitAsync();
            try
            {
                // Reload Payment từ DB để tránh dùng entity cũ đọc từ context khác
                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.PaymentId == paymentParam.PaymentId);

                if (payment == null)
                {
                    return;
                }

                await HandleCartPaymentWebhookCore(payment, topCode, dataCode, amountFromGateway);
            }
            finally
            {
                CartPaymentSemaphore.Release();
            }
        }

        /// <summary>
        /// - Success (00/00): Payment = Paid, tạo Order + OrderDetails từ snapshot, gắn key/tài khoản, gửi email, xoá snapshot.
        /// - Ngược lại: Payment = Cancelled, hoàn kho theo snapshot, xoá snapshot.
        /// </summary>
        private async Task HandleCartPaymentWebhookCore(
            Payment payment,
            string topCode,
            string dataCode,
            long amountFromGateway)
        {
            var currentStatus = payment.Status ?? "Pending";
            if (!string.Equals(currentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                // Đã xử lý trước đó (Paid / Cancelled / ...)
                return;
            }

            var isSuccess = topCode == "00" && dataCode == "00";

            if (isSuccess)
            {
                var (items, userId, email) = GetCartSnapshot(payment.PaymentId);
                if (items == null || !items.Any() || string.IsNullOrWhiteSpace(email))
                {
                    // TH: PayOS báo thành công nhưng snapshot cart đã mất (hết TTL, app restart, scale-out...).
                    // Không thể tạo Order tự động, nhưng tuyệt đối KHÔNG set Cancelled vì user đã thanh toán.
                    // -> Đánh dấu là Paid để reconcile thủ công, log cảnh báo.

                    payment.Status = "Paid";
                    payment.Amount = (decimal)amountFromGateway;

                    await _context.SaveChangesAsync();

                    _logger.LogError(
                        "Cart payment {PaymentId} was confirmed as success but cart snapshot is missing. " +
                        "Marked payment as Paid without creating order - requires manual handling.",
                        payment.PaymentId);

                    // Không clear snapshot nữa (đa phần đã null rồi), tránh mất thêm dữ liệu nếu còn gì đó.
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

                // Order tạo từ cart: immutable log
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

                var orderDetails = new List<OrderDetail>();
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

                    orderDetails.Add(orderDetail);
                }
                _context.OrderDetails.AddRange(orderDetails);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // Sau khi đã có Order + OrderDetails, tiến hành gắn key/tài khoản & gửi email
                try
                {
                    if (orderDetails.Count > 0)
                    {
                        // Reload order with details for processing
                        var orderWithDetails = await _context.Orders
                            .Include(o => o.OrderDetails)
                            .FirstOrDefaultAsync(o => o.OrderId == order.OrderId);

                        if (orderWithDetails != null)
                        {
                            var variantIds = orderDetails.Select(od => od.VariantId).ToList();
                            var variants = await _context.ProductVariants
                                .AsNoTracking()
                                .Include(v => v.Product)
                                .Where(v => variantIds.Contains(v.VariantId))
                                .ToListAsync();

                            await ProcessVariants(variants, orderWithDetails);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process variants for order {OrderId}", order.OrderId);
                    // Không audit lỗi để tránh spam
                }

                ClearCartSnapshot(payment.PaymentId);
                return;
            }

            // ==== Các trường hợp còn lại: user huỷ, thất bại, hết hạn... ====
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

            // ================== TẠO PAYMENT + PAYOS ==================
            using var tx2 = await _context.Database.BeginTransactionAsync();

            // Dùng UnixTimeSeconds làm orderCode đơn giản
            var orderCode = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);

            // Tạo bản ghi Payment Pending – TransactionType = SERVICE_PAYMENT
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
                await tx2.RollbackAsync();

                // Không audit lỗi để tránh spam
                return StatusCode(502, new
                {
                    message = "Không tạo được link thanh toán PayOS (support plan). Chi tiết: " + ex.Message
                });
            }

            await tx2.CommitAsync();

            var resp = new CreateSupportPlanPayOSPaymentResponseDTO
            {
                PaymentId = payment.PaymentId,
                SupportPlanId = plan.SupportPlanId,
                SupportPlanName = plan.Name,
                Price = plan.Price,              // giá gốc gói
                AdjustedAmount = adjustedAmount, // giá thực thu (đã tính lại)
                PaymentUrl = paymentUrl
            };

            await _auditLogger.LogAsync(
                HttpContext,
                action: "CreateSupportPlanPayOSPayment",
                entityType: "Payment",
                entityId: payment.PaymentId.ToString(),
                before: null,
                after: new
                {
                    payment.PaymentId,
                    payment.Status,
                    payment.Amount,
                    payment.Provider,
                    payment.ProviderOrderCode,
                    payment.TransactionType,
                    UserId = user.UserId,
                    SupportPlanId = plan.SupportPlanId,
                    SupportPlanPriority = plan.PriorityLevel,
                    AdjustedAmount = adjustedAmount
                });

            return Ok(resp);
        }

        // ===== Gắn key/tài khoản + gửi email sau khi đơn được tạo từ cart =====
        private async Task ProcessVariants(List<ProductVariant> variants, Order order)
        {
            var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var userEmail = order.Email;

            // Get the user ID from the order
            if (!order.UserId.HasValue)
            {
                var user = await _accountService.GetUserAsync(userEmail);
                if (user == null)
                {
                    user = await _accountService.CreateTempUserAsync(userEmail);
                }
                order.UserId = user.UserId;
            }

            var userId = order.UserId.Value;

            // Collect all products to send in a single email
            var orderProducts = new List<OrderProductEmailDto>();

            // Process PERSONAL_KEY type variants
            var personalKeyVariants = variants
                .Where(x => x.Product.ProductType == ProductEnums.PERSONAL_KEY)
                .ToList();

            foreach (var variant in personalKeyVariants)
            {
                var orderDetail = order.OrderDetails.FirstOrDefault(od => od.VariantId == variant.VariantId);
                if (orderDetail == null) continue;

                var quantity = orderDetail.Quantity;

                // Get available keys for this variant
                var availableKeys = await _context.ProductKeys
                    .AsNoTracking()
                    .Where(pk => pk.VariantId == variant.VariantId &&
                                 pk.Status == "Available")
                    .Take(quantity)
                    .ToListAsync();

                if (availableKeys.Count < quantity)
                {
                    _logger.LogWarning(
                        "Not enough available keys for variant {VariantId}. Required: {Quantity}, Available: {Available}",
                        variant.VariantId, quantity, availableKeys.Count);
                    continue;
                }

                // Assign keys to order and collect for email
                foreach (var key in availableKeys)
                {
                    try
                    {
                        // Assign key to order
                        var assignDto = new AssignKeyToOrderDto
                        {
                            KeyId = key.KeyId,
                            OrderId = order.OrderId
                        };

                        await _productKeyService.AssignKeyToOrderAsync(assignDto, systemUserId);

                        // Clear change tracker to prevent tracking conflicts
                        _context.ChangeTracker.Clear();

                        // Add to products list for consolidated email
                        orderProducts.Add(new OrderProductEmailDto
                        {
                            ProductName = variant.Product.ProductName,
                            VariantTitle = variant.Title,
                            ProductType = "KEY",
                            KeyString = key.KeyString,
                            ExpiryDate = key.ExpiryDate
                        });

                        _logger.LogInformation("Assigned key {KeyId} to order {OrderId}",
                            key.KeyId, order.OrderId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to assign key {KeyId} to order {OrderId}",
                            key.KeyId, order.OrderId);
                    }
                }
            }

            // Process PERSONAL_ACCOUNT type variants
            var personalAccountVariants = variants
                .Where(x => x.Product.ProductType == ProductEnums.PERSONAL_ACCOUNT)
                .ToList();

            foreach (var variant in personalAccountVariants)
            {
                var orderDetail = order.OrderDetails.FirstOrDefault(od => od.VariantId == variant.VariantId);
                if (orderDetail == null) continue;

                var quantity = orderDetail.Quantity;

                // Get available personal accounts for this variant
                var availableAccounts = await _context.ProductAccounts
                    .AsNoTracking()
                    .Where(pa => pa.VariantId == variant.VariantId &&
                                 pa.Status == "Active" &&
                                 pa.MaxUsers == 1) // Personal accounts have MaxUsers = 1
                    .Include(pa => pa.ProductAccountCustomers)
                    .Where(pa => !pa.ProductAccountCustomers.Any(pac => pac.IsActive)) // Not assigned to anyone
                    .Take(quantity)
                    .ToListAsync();

                if (availableAccounts.Count < quantity)
                {
                    _logger.LogWarning(
                        "Not enough available personal accounts for variant {VariantId}. Required: {Quantity}, Available: {Available}",
                        variant.VariantId, quantity, availableAccounts.Count);
                    continue;
                }

                // Assign accounts to order and collect for email
                foreach (var account in availableAccounts)
                {
                    try
                    {
                        // Assign account to order
                        var assignDto = new AssignAccountToOrderDto
                        {
                            ProductAccountId = account.ProductAccountId,
                            OrderId = order.OrderId,
                            UserId = userId
                        };

                        await _productAccountService.AssignAccountToOrderAsync(assignDto, systemUserId);

                        // Get decrypted password
                        var decryptedPassword =
                            await _productAccountService.GetDecryptedPasswordAsync(account.ProductAccountId);

                        // Clear change tracker to prevent tracking conflicts
                        _context.ChangeTracker.Clear();

                        // Add to products list for consolidated email
                        orderProducts.Add(new OrderProductEmailDto
                        {
                            ProductName = variant.Product.ProductName,
                            VariantTitle = variant.Title,
                            ProductType = "ACCOUNT",
                            AccountEmail = account.AccountEmail,
                            AccountUsername = account.AccountUsername,
                            AccountPassword = decryptedPassword,
                            ExpiryDate = account.ExpiryDate
                        });

                        _logger.LogInformation("Assigned account {AccountId} to order {OrderId}",
                            account.ProductAccountId, order.OrderId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to assign account {AccountId} to order {OrderId}",
                            account.ProductAccountId, order.OrderId);
                    }
                }
            }

            // Process SHARED_ACCOUNT type variants
            var sharedAccountVariants = variants
                .Where(x => x.Product.ProductType == ProductEnums.SHARED_ACCOUNT)
                .ToList();

            foreach (var variant in sharedAccountVariants)
            {
                var orderDetail = order.OrderDetails.FirstOrDefault(od => od.VariantId == variant.VariantId);
                if (orderDetail == null) continue;

                var quantity = orderDetail.Quantity;

                // Get available shared accounts for this variant (not full)
                // Order by number of active customers descending to fill nearly-full accounts first
                var availableAccounts = await _context.ProductAccounts
                    .Where(pa => pa.VariantId == variant.VariantId &&
                                 pa.Status == "Active" &&
                                 pa.MaxUsers > 1) // Shared accounts have MaxUsers > 1
                    .Include(pa => pa.ProductAccountCustomers)
                    .Where(pa => pa.ProductAccountCustomers.Count(pac => pac.IsActive) < pa.MaxUsers) // Not full
                    .Select(pa => new
                    {
                        Account = pa,
                        ActiveCustomerCount = pa.ProductAccountCustomers.Count(pac => pac.IsActive),
                        AvailableSlots = pa.MaxUsers - pa.ProductAccountCustomers.Count(pac => pac.IsActive)
                    })
                    .OrderByDescending(x => x.ActiveCustomerCount) // Fill nearly-full accounts first
                    .ToListAsync();

                var totalAvailableSlots = availableAccounts.Sum(x => x.AvailableSlots);
                if (totalAvailableSlots < quantity)
                {
                    _logger.LogWarning("Not enough available slots for variant {VariantId}. Required: {Quantity}, Available: {AvailableSlots}",
                        variant.VariantId, quantity, totalAvailableSlots);
                    continue;
                }

                // Add customer to shared accounts and collect for email
                var addedToSharedAccount = false;
                var assignedCount = 0;

                foreach (var accountInfo in availableAccounts)
                {
                    if (assignedCount >= quantity)
                        break;

                    var slotsToAssign = Math.Min(accountInfo.AvailableSlots, quantity - assignedCount);

                    for (int i = 0; i < slotsToAssign; i++)
                    {
                        try
                        {
                            // Add customer to shared account
                            var assignDto = new AssignAccountToOrderDto
                            {
                                ProductAccountId = accountInfo.Account.ProductAccountId,
                                OrderId = order.OrderId,
                                UserId = userId
                            };

                            await _productAccountService.AssignAccountToOrderAsync(assignDto, systemUserId);

                            // Clear change tracker to prevent tracking conflicts
                            _context.ChangeTracker.Clear();

                            addedToSharedAccount = true;
                            assignedCount++;

                            _logger.LogInformation("Added customer to shared account {AccountId} for order {OrderId} (slot {SlotNumber}/{TotalSlots})",
                                accountInfo.Account.ProductAccountId, order.OrderId, assignedCount, quantity);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to add customer to shared account {AccountId} for order {OrderId}",
                                accountInfo.Account.ProductAccountId, order.OrderId);
                        }
                    }
                }

                // For shared accounts, send email with instructions (no credentials)
                if (addedToSharedAccount)
                {
                    orderProducts.Add(new OrderProductEmailDto
                    {
                        ProductName = variant.Product.ProductName,
                        VariantTitle = variant.Title,
                        ProductType = "SHARED_ACCOUNT",
                        ExpiryDate = availableAccounts.FirstOrDefault()?.Account.ExpiryDate,
                        Notes = $"Tài khoản chia sẻ - Vui lòng làm theo hướng dẫn trong email để hoàn tất việc thêm bạn vào family account."
                    });
                }
            }

            // Send a single consolidated email with all products
            if (orderProducts.Any())
            {
                try
                {
                    await _emailService.SendOrderProductsEmailAsync(userEmail, orderProducts);
                    _logger.LogInformation(
                        "Sent consolidated order email with {Count} products to {Email}",
                        orderProducts.Count, userEmail);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send consolidated order email to {Email}", userEmail);
                }
            }
        }
    }
}
