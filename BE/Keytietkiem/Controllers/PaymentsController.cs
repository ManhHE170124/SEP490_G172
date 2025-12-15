// File: Controllers/PaymentsController.cs
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Payments;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using static Keytietkiem.Constants.ModuleCodes;
using static Keytietkiem.Constants.PermissionCodes;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly PayOSService _payOs;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentsController> _logger;
        private readonly IInventoryReservationService _inventoryReservation;

        // ✅ dùng để fulfill order (gắn key/account + email) theo ProcessVariants
        private readonly IProductKeyService _productKeyService;
        private readonly IProductAccountService _productAccountService;
        private readonly IEmailService _emailService;
        private readonly IAuditLogger _auditLogger;
        private readonly IAccountService _accountService;

        // ✅ đồng bộ status với OrdersController
        private const string PaymentStatusPending = "Pending";
        private const string PaymentStatusPaid = "Paid";
        private const string PaymentStatusCancelled = "Cancelled";
        private const string PaymentStatusTimeout = "Timeout";
        private const string PaymentStatusDupCancelled = "DupCancelled";
        private const string PaymentStatusNeedReview = "NeedReview";
        private const string PaymentStatusReplaced = "Replaced";

        public PaymentsController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            PayOSService payOs,
            IConfiguration config,
            ILogger<PaymentsController> logger,
            IInventoryReservationService inventoryReservation,
            IProductKeyService productKeyService,
            IProductAccountService productAccountService,
            IEmailService emailService,
            IAuditLogger auditLogger,
            IAccountService accountService)
        {
            _dbFactory = dbFactory;
            _payOs = payOs;
            _config = config;
            _logger = logger;
            _inventoryReservation = inventoryReservation;

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

        // ================== ADMIN LIST/DETAIL ==================

        [HttpGet]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.VIEW_LIST)]
        public async Task<IActionResult> GetPayments(
            [FromQuery] string? status,
            [FromQuery] string? provider,
            [FromQuery] string? email,
            [FromQuery] string? targetType,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDir)
        {
            await using var db = _dbFactory.CreateDbContext();

            var query = db.Payments.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(p => p.Status == status.Trim());

            if (!string.IsNullOrWhiteSpace(provider))
                query = query.Where(p => p.Provider == provider.Trim());

            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(p => p.Email == email.Trim());

            if (!string.IsNullOrWhiteSpace(targetType))
                query = query.Where(p => p.TargetType == targetType.Trim());

            var sortByNorm = (sortBy ?? "CreatedAt").Trim();
            var sortDirNorm = (sortDir ?? "desc").Trim().ToLowerInvariant();
            var asc = sortDirNorm == "asc";

            switch (sortByNorm.ToLowerInvariant())
            {
                case "paymentid":
                    query = asc ? query.OrderBy(p => p.PaymentId) : query.OrderByDescending(p => p.PaymentId);
                    break;
                case "amount":
                    query = asc ? query.OrderBy(p => p.Amount) : query.OrderByDescending(p => p.Amount);
                    break;
                case "status":
                    query = asc ? query.OrderBy(p => p.Status) : query.OrderByDescending(p => p.Status);
                    break;
                case "provider":
                    query = asc ? query.OrderBy(p => p.Provider) : query.OrderByDescending(p => p.Provider);
                    break;
                case "email":
                    query = asc ? query.OrderBy(p => p.Email) : query.OrderByDescending(p => p.Email);
                    break;
                case "targettype":
                    query = asc ? query.OrderBy(p => p.TargetType) : query.OrderByDescending(p => p.TargetType);
                    break;
                case "providerordercode":
                    query = asc ? query.OrderBy(p => p.ProviderOrderCode) : query.OrderByDescending(p => p.ProviderOrderCode);
                    break;
                default:
                    query = asc ? query.OrderBy(p => p.CreatedAt) : query.OrderByDescending(p => p.CreatedAt);
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
                    PaymentLinkId = p.PaymentLinkId,
                    Email = p.Email,
                    TargetType = p.TargetType,
                    TargetId = p.TargetId
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{paymentId:guid}")]
        [RequirePermission(ModuleCodes.PRODUCT_MANAGER, PermissionCodes.VIEW_DETAIL)]
        public async Task<IActionResult> GetPaymentById(Guid paymentId)
        {
            await using var db = _dbFactory.CreateDbContext();

            var p = await db.Payments.FirstOrDefaultAsync(x => x.PaymentId == paymentId);
            if (p == null) return NotFound(new { message = "Payment không tồn tại" });

            return Ok(new PaymentDetailDTO
            {
                PaymentId = p.PaymentId,
                Amount = p.Amount,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                Provider = p.Provider,
                ProviderOrderCode = p.ProviderOrderCode,
                PaymentLinkId = p.PaymentLinkId,
                Email = p.Email,
                TargetType = p.TargetType,
                TargetId = p.TargetId
            });
        }

        // ================== PAYOS WEBHOOK ==================

        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> HandlePayOSWebhook([FromBody] PayOSWebhookModel payload)
        {
            if (payload?.Data == null)
                return BadRequest(new { message = "Payload từ PayOS không hợp lệ." });

            if (!_payOs.VerifyWebhookSignature(payload.Data, payload.Signature))
            {
                _logger.LogWarning("PayOS webhook invalid signature. orderCode={OrderCode}", payload.Data.OrderCode);
                return Unauthorized(new { message = "Invalid signature." });
            }

            var orderCode = payload.Data.OrderCode;
            if (orderCode <= 0)
                return BadRequest(new { message = "orderCode không hợp lệ." });

            await using var db = _dbFactory.CreateDbContext();

            var payment = await db.Payments
                .FirstOrDefaultAsync(p => p.Provider == "PayOS" && p.ProviderOrderCode == orderCode);

            if (payment == null)
            {
                _logger.LogWarning("PayOS webhook orderCode={OrderCode} nhưng không tìm thấy Payment.", orderCode);
                return Ok(new { message = "Payment not found - ignored." });
            }

            // đối soát paymentLinkId nếu có
            if (!string.IsNullOrWhiteSpace(payload.Data.PaymentLinkId)
                && !string.IsNullOrWhiteSpace(payment.PaymentLinkId)
                && !string.Equals(payload.Data.PaymentLinkId, payment.PaymentLinkId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("PayOS webhook paymentLinkId mismatch. orderCode={OrderCode}, db={DbLink}, payload={PayloadLink}",
                    orderCode, payment.PaymentLinkId, payload.Data.PaymentLinkId);

                // đánh dấu NeedReview + nếu Order thì NeedsManualAction
                if (string.Equals(payment.TargetType, "Order", StringComparison.OrdinalIgnoreCase)
                    && Guid.TryParse(payment.TargetId, out var oid))
                {
                    var o = await db.Orders.FirstOrDefaultAsync(x => x.OrderId == oid);
                    if (o != null) o.Status = "NeedsManualAction";
                }

                payment.Status = PaymentStatusNeedReview;
                await db.SaveChangesAsync();
                return Ok(new { message = "Webhook processed - paymentLinkId mismatch (NeedReview)." });
            }

            var topCode = payload.Code ?? "";
            var dataCode = !string.IsNullOrWhiteSpace(payload.Data.Code) ? payload.Data.Code : topCode;
            var isSuccess = topCode == "00" && dataCode == "00";
            var gatewayAmount = (long)payload.Data.Amount;

            if (string.Equals(payment.Status ?? "", PaymentStatusPaid, StringComparison.OrdinalIgnoreCase))
                return Ok(new { message = "Already paid." });

            var nowUtc = DateTime.UtcNow;

            var expectedAmount = (long)Math.Round(payment.Amount, 0, MidpointRounding.AwayFromZero);
            if (gatewayAmount != expectedAmount)
            {
                _logger.LogWarning("PayOS webhook amount mismatch. orderCode={OrderCode}, expected={Expected}, gateway={Gateway}",
                    orderCode, expectedAmount, gatewayAmount);

                payment.Status = PaymentStatusNeedReview;

                if (string.Equals(payment.TargetType, "Order", StringComparison.OrdinalIgnoreCase)
                    && Guid.TryParse(payment.TargetId, out var orderIdMismatch))
                {
                    var orderMismatch = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderIdMismatch);
                    if (orderMismatch != null)
                        orderMismatch.Status = "NeedsManualAction";
                }

                await db.SaveChangesAsync();
                return Ok(new { message = "Webhook processed - amount mismatch (NeedReview)." });
            }

            if (db.Database.IsRelational())
            {
                await using var tx = await db.Database.BeginTransactionAsync();

                try
                {
                    // reload payment trong transaction
                    payment = await db.Payments
                        .FirstOrDefaultAsync(p => p.Provider == "PayOS" && p.ProviderOrderCode == orderCode);

                    if (payment == null)
                    {
                        await tx.RollbackAsync();
                        return Ok(new { message = "Payment not found - ignored." });
                    }

                    if (string.Equals(payment.Status ?? "", PaymentStatusPaid, StringComparison.OrdinalIgnoreCase))
                    {
                        await tx.CommitAsync();
                        return Ok(new { message = "Already paid." });
                    }

                    if (isSuccess)
                    {
                        // ===== 1) SUPPORT PLAN PAID => APPLY SUBSCRIPTION =====
                        if (string.Equals(payment.TargetType, "SupportPlan", StringComparison.OrdinalIgnoreCase))
                        {
                            await ApplySupportPlanPurchaseAsync(db, payment, nowUtc, HttpContext.RequestAborted);

                            payment.Status = PaymentStatusPaid;

                            // cancel other pending attempts (same target)
                            var otherPending = await db.Payments
                                .Where(p => p.TargetType == payment.TargetType
                                            && p.TargetId == payment.TargetId
                                            && p.PaymentId != payment.PaymentId
                                            && p.Status == PaymentStatusPending)
                                .ToListAsync();

                            foreach (var p in otherPending)
                                p.Status = PaymentStatusDupCancelled;

                            await db.SaveChangesAsync();
                            await tx.CommitAsync();
                            return Ok(new { message = "Webhook processed - SupportPlan Paid." });
                        }

                        // ===== 2) ORDER PAID =====
                        Order? order = null;
                        Guid orderId = Guid.Empty;

                        if (string.Equals(payment.TargetType, "Order", StringComparison.OrdinalIgnoreCase)
                            && Guid.TryParse(payment.TargetId, out orderId))
                        {
                            order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
                        }

                        // paid về muộn sau khi order cancel/timeout => giữ Paid, order manual
                        if (order != null && (
                                string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(order.Status, "CancelledByTimeout", StringComparison.OrdinalIgnoreCase)))
                        {
                            payment.Status = PaymentStatusPaid;
                            order.Status = "NeedsManualAction";
                            await db.SaveChangesAsync();
                            await tx.CommitAsync();
                            return Ok(new { message = "Webhook processed - Paid late (NeedsManualAction)." });
                        }

                        if (order != null && string.Equals(order.Status, "PendingPayment", StringComparison.OrdinalIgnoreCase))
                        {
                            var lines = await db.OrderDetails
                                .AsNoTracking()
                                .Where(od => od.OrderId == orderId)
                                .Select(od => new { od.VariantId, od.Quantity })
                                .ToListAsync();

                            var req = lines.Select(x => (x.VariantId, x.Quantity)).ToList();
                            var until = nowUtc.AddMinutes(5);

                            // ✅ tránh reserve double: ưu tiên extend; nếu chưa có reservation thì reserve
                            try
                            {
                                await _inventoryReservation.ExtendReservationAsync(db, orderId, until, nowUtc, HttpContext.RequestAborted);
                            }
                            catch
                            {
                                await _inventoryReservation.ReserveForOrderAsync(
                                    db, orderId, req, nowUtc, until, HttpContext.RequestAborted);
                            }

                            await _inventoryReservation.FinalizeReservationAsync(
                                db, orderId, nowUtc, HttpContext.RequestAborted);

                            payment.Status = PaymentStatusPaid;
                            order.Status = "Paid";

                            var otherPending = await db.Payments
                                .Where(p => p.TargetType == "Order"
                                            && p.TargetId == payment.TargetId
                                            && p.PaymentId != payment.PaymentId
                                            && p.Status == PaymentStatusPending)
                                .ToListAsync();

                            foreach (var p in otherPending)
                                p.Status = PaymentStatusDupCancelled;

                            await db.SaveChangesAsync();
                            await tx.CommitAsync();

                            // ✅ sau khi commit: fulfill order (gắn key/account + gửi mail)
                            if (orderId != Guid.Empty)
                                await TryFulfillOrderAsync(orderId, HttpContext.RequestAborted);

                            return Ok(new { message = "Webhook processed - Order Paid." });
                        }

                        // Order không ở PendingPayment => vẫn set Paid nhưng order/manual để staff xử lý
                        payment.Status = PaymentStatusPaid;
                        if (order != null) order.Status = "NeedsManualAction";
                        await db.SaveChangesAsync();
                        await tx.CommitAsync();
                        return Ok(new { message = "Webhook processed - Paid (NeedsManualAction)." });
                    }
                    else
                    {
                        // ===== CANCEL / FAIL =====
                        payment.Status = PaymentStatusCancelled;

                        if (string.Equals(payment.TargetType, "Order", StringComparison.OrdinalIgnoreCase)
                            && Guid.TryParse(payment.TargetId, out var cancelOrderId))
                        {
                            var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == cancelOrderId);
                            if (order != null && string.Equals(order.Status, "PendingPayment", StringComparison.OrdinalIgnoreCase))
                            {
                                var hasOtherActiveAttempt = await db.Payments.AnyAsync(p =>
                                    p.TargetType == "Order"
                                    && p.TargetId == payment.TargetId
                                    && p.PaymentId != payment.PaymentId
                                    && (p.Status == PaymentStatusPending || p.Status == PaymentStatusPaid));

                                if (!hasOtherActiveAttempt)
                                {
                                    order.Status = "Cancelled";
                                    await _inventoryReservation.ReleaseReservationAsync(db, cancelOrderId, nowUtc, HttpContext.RequestAborted);
                                }
                            }
                        }

                        await db.SaveChangesAsync();
                        await tx.CommitAsync();
                        return Ok(new { message = "Webhook processed - Cancelled." });
                    }
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();

                    _logger.LogError(ex, "PayOS webhook transaction failed. orderCode={OrderCode}", orderCode);

                    // cố gắng set NeedReview để reconcile
                    try
                    {
                        payment.Status = PaymentStatusNeedReview;

                        if (string.Equals(payment.TargetType, "Order", StringComparison.OrdinalIgnoreCase)
                            && Guid.TryParse(payment.TargetId, out var oid))
                        {
                            var o = await db.Orders.FirstOrDefaultAsync(x => x.OrderId == oid);
                            if (o != null) o.Status = "NeedsManualAction";
                        }

                        await db.SaveChangesAsync();
                    }
                    catch { }

                    return Ok(new { message = "Webhook processed - NeedReview." });
                }
            }

            // ===== Non-relational (InMemory/test) =====
            if (isSuccess)
            {
                if (string.Equals(payment.TargetType, "SupportPlan", StringComparison.OrdinalIgnoreCase))
                {
                    await ApplySupportPlanPurchaseAsync(db, payment, nowUtc, HttpContext.RequestAborted);
                    payment.Status = PaymentStatusPaid;
                    await db.SaveChangesAsync();
                    return Ok(new { message = "Webhook processed - SupportPlan Paid." });
                }

                payment.Status = PaymentStatusPaid;
                await db.SaveChangesAsync();
                return Ok(new { message = "Webhook processed - Paid." });
            }

            payment.Status = PaymentStatusCancelled;
            await db.SaveChangesAsync();
            return Ok(new { message = "Webhook processed - Cancelled." });
        }

        // ================== CONFIRM/CANCEL FROM RETURN ==================

        [HttpPost("order/confirm-from-return")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmOrderPaymentFromReturn([FromBody] ConfirmOrderPaymentRequestDto dto)
        {
            if (dto == null || dto.PaymentId == Guid.Empty)
                return BadRequest(new { message = "PaymentId không hợp lệ" });

            await using var db = _dbFactory.CreateDbContext();

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentId == dto.PaymentId);
            if (payment == null) return NotFound(new { message = "Payment không tồn tại" });

            return Ok(new
            {
                message = "Payment status",
                status = payment.Status ?? PaymentStatusPending,
                targetType = payment.TargetType,
                targetId = payment.TargetId
            });
        }

        [HttpPost("order/cancel-from-return")]
        [AllowAnonymous]
        public async Task<IActionResult> CancelOrderPaymentFromReturn([FromBody] CancelOrderPaymentRequestDto dto)
        {
            if (dto == null || dto.PaymentId == Guid.Empty)
                return BadRequest(new { message = "PaymentId không hợp lệ" });

            await using var db = _dbFactory.CreateDbContext();

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentId == dto.PaymentId);
            if (payment == null) return Ok(new { message = "Payment không tồn tại" });

            if (!string.Equals(payment.Status ?? "", PaymentStatusPending, StringComparison.OrdinalIgnoreCase))
                return Ok(new { message = "Payment status", status = payment.Status });

            payment.Status = PaymentStatusCancelled;

            var nowUtc = DateTime.UtcNow;

            if (string.Equals(payment.TargetType, "Order", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(payment.TargetId, out var orderId))
            {
                var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (order != null && string.Equals(order.Status, "PendingPayment", StringComparison.OrdinalIgnoreCase))
                {
                    var hasOtherActiveAttempt = await db.Payments.AnyAsync(p =>
                        p.TargetType == "Order"
                        && p.TargetId == payment.TargetId
                        && p.PaymentId != payment.PaymentId
                        && (p.Status == PaymentStatusPending || p.Status == PaymentStatusPaid));

                    if (!hasOtherActiveAttempt)
                    {
                        order.Status = "Cancelled";
                        await _inventoryReservation.ReleaseReservationAsync(db, orderId, nowUtc, HttpContext.RequestAborted);
                    }
                }
            }

            await db.SaveChangesAsync();
            return Ok(new { message = "Payment status", status = payment.Status });
        }

        // ✅ thêm confirm/cancel cho support plan để FE dùng riêng (an toàn)
        [HttpPost("support-plan/confirm-from-return")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmSupportPlanPaymentFromReturn([FromBody] ConfirmOrderPaymentRequestDto dto)
        {
            if (dto == null || dto.PaymentId == Guid.Empty)
                return BadRequest(new { message = "PaymentId không hợp lệ" });

            await using var db = _dbFactory.CreateDbContext();

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentId == dto.PaymentId);
            if (payment == null) return NotFound(new { message = "Payment không tồn tại" });

            return Ok(new
            {
                message = "Support plan payment status",
                status = payment.Status ?? PaymentStatusPending,
                targetType = payment.TargetType,
                targetId = payment.TargetId
            });
        }

        [HttpPost("support-plan/cancel-from-return")]
        [AllowAnonymous]
        public async Task<IActionResult> CancelSupportPlanPaymentFromReturn([FromBody] CancelOrderPaymentRequestDto dto)
        {
            if (dto == null || dto.PaymentId == Guid.Empty)
                return BadRequest(new { message = "PaymentId không hợp lệ" });

            await using var db = _dbFactory.CreateDbContext();

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentId == dto.PaymentId);
            if (payment == null) return Ok(new { message = "Payment không tồn tại" });

            if (!string.Equals(payment.Status ?? "", PaymentStatusPending, StringComparison.OrdinalIgnoreCase))
                return Ok(new { message = "Payment status", status = payment.Status });

            payment.Status = PaymentStatusCancelled;
            await db.SaveChangesAsync();
            return Ok(new { message = "Support plan payment status", status = payment.Status });
        }

        // ================== CREATE SUPPORT PLAN PAYOS ==================

        [HttpPost("payos/create-support-plan")]
        [Authorize]
        public async Task<IActionResult> CreateSupportPlanPayOSPayment([FromBody] CreateSupportPlanPayOSPaymentDTO dto)
        {
            if (dto == null || dto.SupportPlanId <= 0)
                return BadRequest(new { message = "Gói hỗ trợ không hợp lệ" });

            var currentUserId = GetCurrentUserIdOrNull();
            if (!currentUserId.HasValue) return Unauthorized();

            await using var db = _dbFactory.CreateDbContext();

            var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(user.Email))
                return BadRequest(new { message = "Tài khoản của bạn chưa có email, không thể tạo thanh toán." });

            var plan = await db.SupportPlans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.SupportPlanId == dto.SupportPlanId && p.IsActive);

            if (plan == null)
                return BadRequest(new { message = "Gói hỗ trợ không tồn tại hoặc đã bị khóa." });

            if (plan.Price <= 0)
                return BadRequest(new { message = "Giá gói hỗ trợ không hợp lệ." });

            var nowUtc = DateTime.UtcNow;

            // ✅ TÍNH adjustedAmount giống code cũ (upgrade giữa kỳ / khấu trừ remainingDays)
            var basePriority = user.SupportPriorityLevel;
            var targetPriority = plan.PriorityLevel;

            var activeSub = await db.UserSupportPlanSubscriptions
                .Include(s => s.SupportPlan)
                .Where(s => s.UserId == user.UserId
                            && s.Status == "Active"
                            && s.ExpiresAt.HasValue
                            && s.ExpiresAt > nowUtc)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            var effectiveCurrentPriority = basePriority;
            if (activeSub?.SupportPlan != null && activeSub.SupportPlan.PriorityLevel > effectiveCurrentPriority)
                effectiveCurrentPriority = activeSub.SupportPlan.PriorityLevel;

            if (targetPriority <= effectiveCurrentPriority)
            {
                return BadRequest(new
                {
                    message = "Bạn đã có Priority Level hiện tại >= gói hỗ trợ này. Chỉ có thể nâng cấp lên gói cao hơn."
                });
            }

            const decimal periodDays = 30m;
            decimal adjustedAmount;

            decimal? basePrice = null;
            decimal? remainingDays = null;

            if (activeSub?.SupportPlan != null)
            {
                basePrice = activeSub.SupportPlan.Price;

                var days = (decimal)(activeSub.ExpiresAt!.Value.Date - nowUtc.Date).TotalDays;
                if (days < 0) days = 0;
                if (days > periodDays) days = periodDays;
                remainingDays = days;
            }
            else if (basePriority > 0)
            {
                var basePlan = await db.SupportPlans.AsNoTracking()
                    .Where(p => p.IsActive && p.PriorityLevel == basePriority)
                    .OrderBy(p => p.Price)
                    .FirstOrDefaultAsync();

                if (basePlan != null)
                {
                    basePrice = basePlan.Price;
                    remainingDays = periodDays;
                }
            }

            if (basePrice.HasValue && remainingDays.HasValue
                && basePrice.Value > 0
                && basePrice.Value < plan.Price
                && remainingDays.Value > 0)
            {
                var ratio = remainingDays.Value / periodDays;
                var discount = basePrice.Value * ratio;
                adjustedAmount = plan.Price - discount;
            }
            else
            {
                adjustedAmount = plan.Price;
            }

            if (adjustedAmount < 0) adjustedAmount = 0;
            adjustedAmount = Math.Round(adjustedAmount, 2, MidpointRounding.AwayFromZero);

            if (adjustedAmount <= 0)
                return BadRequest(new { message = "Số tiền thanh toán không hợp lệ sau khi điều chỉnh." });

            var amountInt = (int)Math.Round(adjustedAmount, 0, MidpointRounding.AwayFromZero);
            if (amountInt <= 0)
                return BadRequest(new { message = "Số tiền thanh toán không hợp lệ." });

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                Amount = adjustedAmount,
                Status = PaymentStatusPending,
                CreatedAt = nowUtc,
                Provider = "PayOS",
                Email = user.Email,
                TargetType = "SupportPlan",
                TargetId = plan.SupportPlanId.ToString()
            };

            // ✅ FIX overflow + giảm trùng orderCode
            var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var baseCode = (int)(epoch % 2_000_000);
            var random = Random.Shared.Next(100, 999);
            var orderCode = Math.Abs(baseCode * 1000 + random);

            var desc = $"SP_{plan.SupportPlanId}";
            if (desc.Length > 25) desc = desc.Substring(0, 25);

            var frontendBaseUrl = _config["PayOS:FrontendBaseUrl"]?.TrimEnd('/') ?? "https://keytietkiem.com";
            var returnUrl = $"{frontendBaseUrl}/support/subscription?paymentId={payment.PaymentId}&supportPlanId={plan.SupportPlanId}";
            var cancelUrl = $"{frontendBaseUrl}/support/subscription?paymentId={payment.PaymentId}&supportPlanId={plan.SupportPlanId}";

            var payosRes = await _payOs.CreatePaymentV2(
                orderCode: orderCode,
                amount: amountInt,
                description: desc,
                returnUrl: returnUrl,
                cancelUrl: cancelUrl,
                buyerPhone: user.Phone ?? "",
                buyerName: string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName!,
                buyerEmail: user.Email
            );

            payment.ProviderOrderCode = orderCode;
            payment.PaymentLinkId = payosRes.PaymentLinkId;

            db.Payments.Add(payment);
            await db.SaveChangesAsync();

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
                    payment.TargetType,
                    payment.TargetId,
                    UserId = user.UserId,
                    SupportPlanId = plan.SupportPlanId,
                    SupportPlanPriority = plan.PriorityLevel,
                    AdjustedAmount = adjustedAmount
                });

            return Ok(new CreateSupportPlanPayOSPaymentResponseDTO
            {
                PaymentId = payment.PaymentId,
                SupportPlanId = plan.SupportPlanId,
                SupportPlanName = plan.Name,
                Price = plan.Price,
                AdjustedAmount = adjustedAmount,
                PaymentUrl = payosRes.CheckoutUrl
            });
        }

        // ================== HELPERS ==================

        private async Task ApplySupportPlanPurchaseAsync(KeytietkiemDbContext db, Payment payment, DateTime nowUtc, CancellationToken ct)
        {
            if (!int.TryParse(payment.TargetId, out var supportPlanId))
                throw new InvalidOperationException("SupportPlanId không hợp lệ trong Payment.TargetId");

            var plan = await db.SupportPlans.FirstOrDefaultAsync(p => p.SupportPlanId == supportPlanId && p.IsActive, ct);
            if (plan == null)
                throw new InvalidOperationException("SupportPlan không tồn tại hoặc đã bị khóa.");

            if (string.IsNullOrWhiteSpace(payment.Email))
                throw new InvalidOperationException("Payment.Email rỗng, không xác định user.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == payment.Email, ct);
            if (user == null)
                throw new InvalidOperationException("Không tìm thấy user theo Payment.Email.");

            var activeSub = await db.UserSupportPlanSubscriptions
                .Include(s => s.SupportPlan)
                .Where(s => s.UserId == user.UserId
                            && s.Status == "Active"
                            && s.ExpiresAt.HasValue
                            && s.ExpiresAt > nowUtc)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(ct);

            DateTime expiresAt;
            if (activeSub?.ExpiresAt.HasValue == true && activeSub.ExpiresAt.Value > nowUtc)
            {
                expiresAt = activeSub.ExpiresAt.Value; // giữ nguyên thời hạn còn lại
                activeSub.Status = "Upgraded";
            }
            else
            {
                expiresAt = nowUtc.AddDays(30);
            }

            var newSub = new UserSupportPlanSubscription
            {
                UserId = user.UserId,
                SupportPlanId = plan.SupportPlanId,
                Status = "Active",
                StartedAt = nowUtc,
                ExpiresAt = expiresAt
            };

            db.UserSupportPlanSubscriptions.Add(newSub);

            if (user.SupportPriorityLevel < plan.PriorityLevel)
                user.SupportPriorityLevel = plan.PriorityLevel;

            await db.SaveChangesAsync(ct);
        }

        private async Task TryFulfillOrderAsync(Guid orderId, CancellationToken ct)
        {
            try
            {
                await using var db = _dbFactory.CreateDbContext();

                var order = await db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

                if (order == null) return;
                if (!string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase)) return;

                var variantIds = order.OrderDetails.Select(x => x.VariantId).Distinct().ToList();

                var variants = await db.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => variantIds.Contains(v.VariantId))
                    .ToListAsync(ct);

                await ProcessVariants(db, variants, order, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TryFulfillOrderAsync failed for OrderId={OrderId}", orderId);

                try
                {
                    await using var db2 = _dbFactory.CreateDbContext();
                    var o = await db2.Orders.FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
                    if (o != null)
                    {
                        o.Status = "NeedsManualAction";
                        await db2.SaveChangesAsync(ct);
                    }
                }
                catch { }
            }
        }

        // ✅ gắn key/account + gửi mail (dựa trên ProcessVariants code cũ, chỉnh sang dbFactory/db hiện tại)
        private async Task ProcessVariants(KeytietkiemDbContext db, List<ProductVariant> variants, Order order, CancellationToken ct)
        {
            var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var userEmail = order.Email;

            if (string.IsNullOrWhiteSpace(userEmail)) return;

            // ensure UserId cho order (đặc biệt guest)
            if (!order.UserId.HasValue)
            {
                var u = await _accountService.GetUserAsync(userEmail);
                if (u == null) u = await _accountService.CreateTempUserAsync(userEmail);

                order.UserId = u.UserId;

                var tracked = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == order.OrderId, ct);
                if (tracked != null)
                {
                    tracked.UserId = order.UserId;
                    await db.SaveChangesAsync(ct);
                }
            }

            var userId = order.UserId!.Value;

            var orderProducts = new List<OrderProductEmailDto>();

            // PERSONAL_KEY
            var personalKeyVariants = variants
                .Where(x => x.Product != null && x.Product.ProductType == ProductEnums.PERSONAL_KEY)
                .ToList();

            foreach (var variant in personalKeyVariants)
            {
                var orderDetail = order.OrderDetails.FirstOrDefault(od => od.VariantId == variant.VariantId);
                if (orderDetail == null) continue;

                var quantity = orderDetail.Quantity;
                if (quantity <= 0) continue;

                // idempotent nhẹ: nếu đã có đủ key assigned theo order+variant thì skip
                var alreadyAssigned = await db.Set<ProductKey>()
                    .AsNoTracking()
                    .CountAsync(k => k.AssignedToOrderId == order.OrderId
                                     && k.VariantId == variant.VariantId
                                     && k.Status == "Sold", ct);

                var need = Math.Max(0, quantity - alreadyAssigned);
                if (need == 0) continue;

                var availableKeys = await db.Set<ProductKey>()
                    .AsNoTracking()
                    .Where(pk => pk.VariantId == variant.VariantId && pk.Status == "Available")
                    .Take(need)
                    .ToListAsync(ct);

                if (availableKeys.Count < need)
                {
                    _logger.LogWarning("Not enough available keys for variant {VariantId}. Need={Need}, Available={Available}",
                        variant.VariantId, need, availableKeys.Count);
                    continue;
                }

                foreach (var key in availableKeys)
                {
                    try
                    {
                        await _productKeyService.AssignKeyToOrderAsync(
                            new AssignKeyToOrderDto { KeyId = key.KeyId, OrderId = order.OrderId },
                            systemUserId);

                        db.ChangeTracker.Clear();

                        orderProducts.Add(new OrderProductEmailDto
                        {
                            ProductName = variant.Product!.ProductName,
                            VariantTitle = variant.Title,
                            ProductType = "KEY",
                            KeyString = key.KeyString,
                            ExpiryDate = key.ExpiryDate
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to assign key {KeyId} to order {OrderId}", key.KeyId, order.OrderId);
                    }
                }
            }

            // PERSONAL_ACCOUNT
            var personalAccountVariants = variants
                .Where(x => x.Product != null && x.Product.ProductType == ProductEnums.PERSONAL_ACCOUNT)
                .ToList();

            foreach (var variant in personalAccountVariants)
            {
                var orderDetail = order.OrderDetails.FirstOrDefault(od => od.VariantId == variant.VariantId);
                if (orderDetail == null) continue;

                var quantity = orderDetail.Quantity;
                if (quantity <= 0) continue;

                var availableAccounts = await db.Set<ProductAccount>()
                    .AsNoTracking()
                    .Where(pa => pa.VariantId == variant.VariantId
                                 && pa.Status == "Active"
                                 && pa.MaxUsers == 1)
                    .Include(pa => pa.ProductAccountCustomers)
                    .Where(pa => !pa.ProductAccountCustomers.Any(pac => pac.IsActive))
                    .Take(quantity)
                    .ToListAsync(ct);

                if (availableAccounts.Count < quantity)
                {
                    _logger.LogWarning("Not enough available personal accounts for variant {VariantId}. Need={Need}, Available={Available}",
                        variant.VariantId, quantity, availableAccounts.Count);
                    continue;
                }

                foreach (var account in availableAccounts)
                {
                    try
                    {
                        await _productAccountService.AssignAccountToOrderAsync(
                            new AssignAccountToOrderDto
                            {
                                ProductAccountId = account.ProductAccountId,
                                OrderId = order.OrderId,
                                UserId = userId
                            },
                            systemUserId);

                        var decryptedPassword = await _productAccountService.GetDecryptedPasswordAsync(account.ProductAccountId);

                        db.ChangeTracker.Clear();

                        orderProducts.Add(new OrderProductEmailDto
                        {
                            ProductName = variant.Product!.ProductName,
                            VariantTitle = variant.Title,
                            ProductType = "ACCOUNT",
                            AccountEmail = account.AccountEmail,
                            AccountUsername = account.AccountUsername,
                            AccountPassword = decryptedPassword,
                            ExpiryDate = account.ExpiryDate,
                            Notes = account.Notes
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to assign account {AccountId} to order {OrderId}",
                            account.ProductAccountId, order.OrderId);
                    }
                }
            }

            // SHARED_ACCOUNT
            var sharedAccountVariants = variants
                .Where(x => x.Product != null && x.Product.ProductType == ProductEnums.SHARED_ACCOUNT)
                .ToList();

            foreach (var variant in sharedAccountVariants)
            {
                var orderDetail = order.OrderDetails.FirstOrDefault(od => od.VariantId == variant.VariantId);
                if (orderDetail == null) continue;

                var quantity = orderDetail.Quantity;
                if (quantity <= 0) continue;

                var availableAccounts = await db.Set<ProductAccount>()
                    .Where(pa => pa.VariantId == variant.VariantId
                                 && pa.Status == "Active"
                                 && pa.MaxUsers > 1)
                    .Include(pa => pa.ProductAccountCustomers)
                    .Where(pa => pa.ProductAccountCustomers.Count(pac => pac.IsActive) < pa.MaxUsers)
                    .Select(pa => new
                    {
                        Account = pa,
                        ActiveCustomerCount = pa.ProductAccountCustomers.Count(pac => pac.IsActive),
                        AvailableSlots = pa.MaxUsers - pa.ProductAccountCustomers.Count(pac => pac.IsActive)
                    })
                    .OrderByDescending(x => x.ActiveCustomerCount)
                    .ToListAsync(ct);

                var totalSlots = availableAccounts.Sum(x => x.AvailableSlots);
                if (totalSlots < quantity)
                {
                    _logger.LogWarning("Not enough available shared slots for variant {VariantId}. Need={Need}, Slots={Slots}",
                        variant.VariantId, quantity, totalSlots);
                    continue;
                }

                var assigned = 0;

                foreach (var acc in availableAccounts)
                {
                    if (assigned >= quantity) break;

                    var slotsToAssign = Math.Min(acc.AvailableSlots, quantity - assigned);

                    for (int i = 0; i < slotsToAssign; i++)
                    {
                        try
                        {
                            await _productAccountService.AssignAccountToOrderAsync(
                                new AssignAccountToOrderDto
                                {
                                    ProductAccountId = acc.Account.ProductAccountId,
                                    OrderId = order.OrderId,
                                    UserId = userId
                                },
                                systemUserId);

                            db.ChangeTracker.Clear();
                            assigned++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to add customer to shared account {AccountId} for order {OrderId}",
                                acc.Account.ProductAccountId, order.OrderId);
                        }
                    }
                }

                if (assigned > 0)
                {
                    orderProducts.Add(new OrderProductEmailDto
                    {
                        ProductName = variant.Product!.ProductName,
                        VariantTitle = variant.Title,
                        ProductType = "SHARED_ACCOUNT",
                        ExpiryDate = availableAccounts.FirstOrDefault()?.Account.ExpiryDate,
                        Notes = "Tài khoản chia sẻ - Vui lòng làm theo hướng dẫn trong email để hoàn tất."
                    });
                }
            }

            // gửi 1 email tổng
            if (orderProducts.Any())
            {
                try
                {
                    await _emailService.SendOrderProductsEmailAsync(userEmail, orderProducts);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send consolidated order email to {Email}", userEmail);
                }
            }
        }
    }
}
