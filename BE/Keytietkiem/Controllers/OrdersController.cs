using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Orders;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using static Keytietkiem.Constants.RoleCodes;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IProductAccountService _productAccountService;
        private readonly IAuditLogger _auditLogger;
        private readonly PayOSService _payOs;
        private readonly IConfiguration _config;
        private readonly ILogger<OrdersController> _logger;
        private readonly IInventoryReservationService _inventoryReservation;

        private static readonly SemaphoreSlim CheckoutSemaphore = new SemaphoreSlim(1, 1);

        private static readonly TimeSpan PaymentTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ConvertingLockTimeout = TimeSpan.FromMinutes(5);

        // ✅ DB mới: Payment.Status varchar(15) => chuẩn hoá set dùng chung với PaymentsController
        private const string PayStatus_Pending = "Pending";
        private const string PayStatus_Paid = "Paid";
        private const string PayStatus_Cancelled = "Cancelled";
        private const string PayStatus_Timeout = "Timeout";
        private const string PayStatus_NeedReview = "NeedReview";
        private const string PayStatus_DupCancelled = "DupCancelled";
        private const string PayStatus_Replaced = "Replaced";

        private sealed class PaymentLite
        {
            public Guid PaymentId { get; set; }
            public decimal Amount { get; set; }
            public string? Status { get; set; }
            public DateTime CreatedAt { get; set; }
            public string? Provider { get; set; }
            public long? ProviderOrderCode { get; set; }
            public string? PaymentLinkId { get; set; }
            public string? TargetId { get; set; }
        }

        public OrdersController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IProductAccountService productAccountService,
            IAuditLogger auditLogger,
            PayOSService payOs,
            IConfiguration config,
            ILogger<OrdersController> logger,
            IInventoryReservationService inventoryReservation)
        {
            _dbFactory = dbFactory;
            _productAccountService = productAccountService;
            _auditLogger = auditLogger;
            _payOs = payOs;
            _config = config;
            _logger = logger;
            _inventoryReservation = inventoryReservation;
        }

        [HttpPost("checkout")]
        [EnableRateLimiting("CartPolicy")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckoutFromCart([FromBody] CheckoutFromCartRequestDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Body không hợp lệ" });

            await CheckoutSemaphore.WaitAsync();
            try
            {
                await using var db = _dbFactory.CreateDbContext();

                var nowUtc = DateTime.UtcNow;
                var currentUserId = GetCurrentUserIdOrNull();

                var anonymousId = string.IsNullOrWhiteSpace(dto.AnonymousId)
                    ? (Request.Cookies["ktk_anon_id"] ?? Request.Headers["X-Guest-Cart-Id"].FirstOrDefault())
                    : dto.AnonymousId;

                var cartLookupWindow = PaymentTimeout + ConvertingLockTimeout + TimeSpan.FromMinutes(1);

                Cart? cart = null;

                if (currentUserId.HasValue)
                {
                    cart = await db.Carts
                        .Include(c => c.CartItems)
                            .ThenInclude(ci => ci.Variant)
                                .ThenInclude(v => v.Product)
                        .Where(c => c.UserId == currentUserId.Value
                                    && (c.Status == "Active" || c.Status == "Converting"))
                        .OrderByDescending(c => c.UpdatedAt)
                        .FirstOrDefaultAsync();

                    if (cart == null)
                    {
                        var cutoff = nowUtc - cartLookupWindow;

                        cart = await db.Carts
                            .Include(c => c.CartItems)
                                .ThenInclude(ci => ci.Variant)
                                    .ThenInclude(v => v.Product)
                            .Where(c => c.UserId == currentUserId.Value
                                        && c.Status == "Converted"
                                        && c.ConvertedOrderId != null
                                        && c.UpdatedAt >= cutoff)
                            .OrderByDescending(c => c.UpdatedAt)
                            .FirstOrDefaultAsync();
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(anonymousId))
                        return BadRequest(new { message = "AnonymousId is required for guest checkout" });

                    cart = await db.Carts
                        .Include(c => c.CartItems)
                            .ThenInclude(ci => ci.Variant)
                                .ThenInclude(v => v.Product)
                        .Where(c => c.AnonymousId == anonymousId
                                    && (c.Status == "Active" || c.Status == "Converting"))
                        .OrderByDescending(c => c.UpdatedAt)
                        .FirstOrDefaultAsync();

                    if (cart == null)
                    {
                        var cutoff = nowUtc - cartLookupWindow;

                        cart = await db.Carts
                            .Include(c => c.CartItems)
                                .ThenInclude(ci => ci.Variant)
                                    .ThenInclude(v => v.Product)
                            .Where(c => c.AnonymousId == anonymousId
                                        && c.Status == "Converted"
                                        && c.ConvertedOrderId != null
                                        && c.UpdatedAt >= cutoff)
                            .OrderByDescending(c => c.UpdatedAt)
                            .FirstOrDefaultAsync();
                    }
                }

                if (cart == null)
                    return BadRequest(new { message = "Cart không tồn tại" });

                if (string.Equals(cart.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    var ttl = cart.UserId.HasValue ? TimeSpan.FromDays(30) : TimeSpan.FromDays(7);
                    var isExpired =
                        (cart.ExpiresAt.HasValue && cart.ExpiresAt.Value < nowUtc) ||
                        (cart.UpdatedAt.Add(ttl) < nowUtc);

                    if (isExpired)
                    {
                        cart.Status = "Expired";
                        cart.UpdatedAt = nowUtc;
                        cart.ExpiresAt = nowUtc;
                        await db.SaveChangesAsync();
                        return BadRequest(new { message = "Cart đã hết hạn. Vui lòng tạo giỏ hàng mới." });
                    }
                }

                if (string.Equals(cart.Status, "Converted", StringComparison.OrdinalIgnoreCase)
                    && cart.ConvertedOrderId.HasValue)
                {
                    return await BuildCheckoutResponseFromExistingOrderAsync(db, cart.ConvertedOrderId.Value, dto, nowUtc);
                }

                if (string.Equals(cart.Status, "Converting", StringComparison.OrdinalIgnoreCase)
                    && !cart.ConvertedOrderId.HasValue)
                {
                    var recovered = await TryRecoverStuckConvertingCartAsync(db, cart.CartId, nowUtc);
                    if (recovered)
                        await db.Entry(cart).ReloadAsync();
                }

                if (string.Equals(cart.Status, "Converting", StringComparison.OrdinalIgnoreCase))
                {
                    if (cart.ConvertedOrderId.HasValue)
                        return await BuildCheckoutResponseFromExistingOrderAsync(db, cart.ConvertedOrderId.Value, dto, nowUtc);

                    return Conflict(new { message = "Cart đang được checkout. Vui lòng thử lại sau." });
                }

                if (!string.Equals(cart.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { message = $"Cart status không hợp lệ: {cart.Status}" });

                var validItems = (cart.CartItems ?? new List<CartItem>())
                    .Where(ci => ci.Quantity > 0)
                    .ToList();

                if (validItems.Count == 0)
                    return BadRequest(new { message = "Giỏ hàng rỗng" });

                string buyerEmail;
                string buyerName;
                string buyerPhone;

                if (currentUserId.HasValue)
                {
                    var user = await db.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);

                    buyerEmail = !string.IsNullOrWhiteSpace(user?.Email) ? user!.Email! : (dto.DeliveryEmail ?? "");
                    buyerName = !string.IsNullOrWhiteSpace(user?.FullName) ? user!.FullName! : buyerEmail;
                    buyerPhone = user?.Phone ?? "";
                }
                else
                {
                    buyerEmail = dto.DeliveryEmail ?? "";
                    buyerName = string.IsNullOrWhiteSpace(dto.BuyerName) ? buyerEmail : dto.BuyerName!;
                    buyerPhone = dto.BuyerPhone ?? "";
                }

                if (string.IsNullOrWhiteSpace(buyerEmail))
                    return BadRequest(new { message = "DeliveryEmail is required" });

                // ✅ Guard/reprice:
                // - Relational: phải snapshot SAU khi claim cart (trong transaction).
                // - InMemory/test: vẫn tính ở đây vì không có transaction.
                List<(Guid VariantId, int Quantity)> lines = new();
                Dictionary<Guid, decimal> unitPriceByVariantId = new();
                decimal totalListAmount = 0m;
                decimal totalAmount = 0m;

                if (!db.Database.IsRelational())
                {
                    (lines, unitPriceByVariantId, totalListAmount, totalAmount) =
                        await GuardAndRepriceCartAsync(db, validItems, HttpContext.RequestAborted);

                    totalListAmount = Math.Round(totalListAmount, 2, MidpointRounding.AwayFromZero);
                    totalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
                }

                Order order;
                Payment payment;

                if (db.Database.IsRelational())
                {
                    await using var tx = await db.Database.BeginTransactionAsync();

                    var claimed = await TryClaimCartForCheckoutAsync(db, cart.CartId, nowUtc);
                    if (!claimed)
                    {
                        await tx.RollbackAsync();

                        var latestCart = await db.Carts.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CartId == cart.CartId);

                        if (latestCart != null
                            && string.Equals(latestCart.Status, "Converted", StringComparison.OrdinalIgnoreCase)
                            && latestCart.ConvertedOrderId.HasValue)
                        {
                            return await BuildCheckoutResponseFromExistingOrderAsync(db, latestCart.ConvertedOrderId.Value, dto, nowUtc);
                        }

                        return Conflict(new { message = "Cart đang được checkout. Vui lòng thử lại sau." });
                    }

                    await db.Entry(cart).ReloadAsync();

                    // ✅ LOAD cart + items lại trong transaction để snapshot chuẩn tại thời điểm claim
                    cart = await db.Carts
                        .Include(c => c.CartItems)
                            .ThenInclude(ci => ci.Variant)
                                .ThenInclude(v => v.Product)
                        .FirstOrDefaultAsync(c => c.CartId == cart.CartId);

                    if (cart == null)
                    {
                        await tx.RollbackAsync();
                        return BadRequest(new { message = "Cart không tồn tại" });
                    }

                    var txItems = (cart.CartItems ?? new List<CartItem>())
                        .Where(ci => ci.Quantity > 0)
                        .ToList();

                    if (txItems.Count == 0)
                    {
                        await tx.RollbackAsync();
                        return BadRequest(new { message = "Giỏ hàng rỗng" });
                    }

                    // ✅ Reprice + Guard snapshot SAU claim (đúng nghiệp vụ)
                    (lines, unitPriceByVariantId, totalListAmount, totalAmount) =
                        await GuardAndRepriceCartAsync(db, txItems, HttpContext.RequestAborted);

                    totalListAmount = Math.Round(totalListAmount, 2, MidpointRounding.AwayFromZero);
                    totalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);

                    order = new Order
                    {
                        UserId = currentUserId,
                        Email = buyerEmail,
                        TotalAmount = totalListAmount,
                        DiscountAmount = totalListAmount - totalAmount,
                        Status = "PendingPayment",
                        CreatedAt = nowUtc
                    };

                    db.Orders.Add(order);
                    await db.SaveChangesAsync();

                    var details = new List<OrderDetail>();
                    foreach (var item in txItems)
                    {
                        var unitPrice = unitPriceByVariantId.TryGetValue(item.VariantId, out var up)
                            ? up
                            : (item.Variant != null && item.Variant.SellPrice > 0 ? item.Variant.SellPrice : item.Variant?.ListPrice ?? 0);

                        details.Add(new OrderDetail
                        {
                            OrderId = order.OrderId,
                            VariantId = item.VariantId,
                            Quantity = item.Quantity,
                            UnitPrice = unitPrice
                        });
                    }

                    db.OrderDetails.AddRange(details);
                    await db.SaveChangesAsync();

                    // ✅ RESERVE inventory 5 phút cho order PendingPayment (TRONG TRANSACTION)
                    try
                    {
                        var reservedUntil = nowUtc.Add(PaymentTimeout);
                        await _inventoryReservation.ReserveForOrderAsync(
                            db, order.OrderId, lines, nowUtc, reservedUntil, HttpContext.RequestAborted);
                    }
                    catch (InvalidOperationException ex)
                    {
                        await tx.RollbackAsync();
                        return Conflict(new { message = ex.Message });
                    }

                    cart.Status = "Converted";
                    cart.ConvertedOrderId = order.OrderId;
                    cart.UpdatedAt = nowUtc;
                    await db.SaveChangesAsync();

                    payment = new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        Amount = totalAmount,
                        Status = PayStatus_Pending,
                        CreatedAt = nowUtc,
                        Provider = "PayOS",
                        Email = buyerEmail,
                        TargetType = "Order",
                        TargetId = order.OrderId.ToString()
                    };

                    var payos = await CreateOrRefreshPayOSLinkAsync(payment, buyerName, buyerPhone, dto, nowUtc);

                    db.Payments.Add(payment);
                    await db.SaveChangesAsync();

                    await tx.CommitAsync();

                    await _auditLogger.LogAsync(
                        HttpContext,
                        action: "CheckoutFromCart",
                        entityType: "Order",
                        entityId: order.OrderId.ToString(),
                        before: null,
                        after: new
                        {
                            order.OrderId,
                            order.Status,
                            payment.PaymentId,
                            PaymentStatus = payment.Status,
                            payment.Provider,
                            payment.ProviderOrderCode,
                            payment.PaymentLinkId,
                            payment.TargetType,
                            payment.TargetId,
                            CartId = cart.CartId,
                            CartStatus = cart.Status,
                            cart.ConvertedOrderId
                        });

                    return Ok(new CheckoutFromCartResponseDto
                    {
                        OrderId = order.OrderId,
                        PaymentId = payment.PaymentId,
                        CheckoutUrl = payos.CheckoutUrl,
                        PaymentLinkId = payment.PaymentLinkId,
                        ExpiresAtUtc = nowUtc.Add(PaymentTimeout)
                    });
                }
                else
                {
                    // InMemory/unit test
                    if (!string.Equals(cart.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(cart.Status, "Converted", StringComparison.OrdinalIgnoreCase) && cart.ConvertedOrderId.HasValue)
                            return await BuildCheckoutResponseFromExistingOrderAsync(db, cart.ConvertedOrderId.Value, dto, nowUtc);

                        return Conflict(new { message = $"Cart status không hợp lệ: {cart.Status}" });
                    }

                    cart.Status = "Converting";
                    cart.UpdatedAt = nowUtc;
                    await db.SaveChangesAsync();

                    order = new Order
                    {
                        UserId = currentUserId,
                        Email = buyerEmail,
                        TotalAmount = totalListAmount,
                        DiscountAmount = totalListAmount - totalAmount,
                        Status = "PendingPayment",
                        CreatedAt = nowUtc
                    };

                    db.Orders.Add(order);
                    await db.SaveChangesAsync();

                    var details = new List<OrderDetail>();
                    foreach (var item in validItems)
                    {
                        var unitPrice = unitPriceByVariantId.TryGetValue(item.VariantId, out var up)
                            ? up
                            : (item.Variant.SellPrice > 0 ? item.Variant.SellPrice : item.Variant.ListPrice);

                        details.Add(new OrderDetail
                        {
                            OrderId = order.OrderId,
                            VariantId = item.VariantId,
                            Quantity = item.Quantity,
                            UnitPrice = unitPrice
                        });
                    }

                    db.OrderDetails.AddRange(details);
                    await db.SaveChangesAsync();

                    cart.Status = "Converted";
                    cart.ConvertedOrderId = order.OrderId;
                    cart.UpdatedAt = nowUtc;
                    await db.SaveChangesAsync();

                    payment = new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        Amount = totalAmount,
                        Status = PayStatus_Pending,
                        CreatedAt = nowUtc,
                        Provider = "PayOS",
                        Email = buyerEmail,
                        TargetType = "Order",
                        TargetId = order.OrderId.ToString()
                    };

                    var payos = await CreateOrRefreshPayOSLinkAsync(payment, buyerName, buyerPhone, dto, nowUtc);

                    db.Payments.Add(payment);
                    await db.SaveChangesAsync();

                    return Ok(new CheckoutFromCartResponseDto
                    {
                        OrderId = order.OrderId,
                        PaymentId = payment.PaymentId,
                        CheckoutUrl = payos.CheckoutUrl,
                        PaymentLinkId = payment.PaymentLinkId,
                        ExpiresAtUtc = nowUtc.Add(PaymentTimeout)
                    });
                }
            }
            finally
            {
                CheckoutSemaphore.Release();
            }
        }

        // ================== READ-ONLY ==================
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> GetOrders(
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDir)
        {
            await using var db = _dbFactory.CreateDbContext();

            var sortByNorm = (sortBy ?? "CreatedAt").Trim();
            var sortDirNorm = (sortDir ?? "desc").Trim().ToLowerInvariant();
            var asc = sortDirNorm == "asc";

            // ✅ List nhẹ: không Include sâu, dùng projection + subquery count
            var baseQuery = db.Orders
                .AsNoTracking()
                .Select(o => new
                {
                    o.OrderId,
                    o.UserId,
                    o.Email,
                    o.TotalAmount,
                    o.DiscountAmount,
                    o.Status,
                    o.CreatedAt,
                    UserEmail = o.User != null ? o.User.Email : null,
                    UserName = o.User != null
                        ? (o.User.FullName ?? $"{o.User.FirstName} {o.User.LastName}".Trim())
                        : null,
                    ItemCount = db.OrderDetails.Count(od => od.OrderId == o.OrderId)
                });

            switch (sortByNorm.ToLowerInvariant())
            {
                case "orderid":
                    baseQuery = asc ? baseQuery.OrderBy(o => o.OrderId) : baseQuery.OrderByDescending(o => o.OrderId);
                    break;
                case "customer":
                case "username":
                    baseQuery = asc
                        ? baseQuery.OrderBy(o => o.UserName ?? o.UserEmail ?? o.Email)
                        : baseQuery.OrderByDescending(o => o.UserName ?? o.UserEmail ?? o.Email);
                    break;
                case "email":
                    baseQuery = asc ? baseQuery.OrderBy(o => o.Email) : baseQuery.OrderByDescending(o => o.Email);
                    break;
                case "totalamount":
                    baseQuery = asc ? baseQuery.OrderBy(o => o.TotalAmount) : baseQuery.OrderByDescending(o => o.TotalAmount);
                    break;
                case "finalamount":
                    baseQuery = asc
                        ? baseQuery.OrderBy(o => (o.TotalAmount - o.DiscountAmount))
                        : baseQuery.OrderByDescending(o => (o.TotalAmount - o.DiscountAmount));
                    break;
                case "itemcount":
                    baseQuery = asc ? baseQuery.OrderBy(o => o.ItemCount) : baseQuery.OrderByDescending(o => o.ItemCount);
                    break;
                default:
                    baseQuery = asc ? baseQuery.OrderBy(o => o.CreatedAt) : baseQuery.OrderByDescending(o => o.CreatedAt);
                    break;
            }

            var orders = await baseQuery.ToListAsync();

            // ✅ load payments theo danh sách orderIds (multi-attempt)
            var orderIdStrs = orders.Select(x => x.OrderId.ToString()).ToList();

            var payments = await db.Payments
                .AsNoTracking()
                .Where(p => p.TargetType == "Order" && p.TargetId != null && orderIdStrs.Contains(p.TargetId))
                .Select(p => new PaymentLite
                {
                    PaymentId = p.PaymentId,
                    Amount = p.Amount,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    Provider = p.Provider,
                    ProviderOrderCode = p.ProviderOrderCode,
                    PaymentLinkId = p.PaymentLinkId,
                    TargetId = p.TargetId
                })
                .ToListAsync();

            var nowUtc = DateTime.UtcNow;

            var paymentGroups = payments
                .Where(p => p.TargetId != null)
                .GroupBy(p => p.TargetId!)
                .ToDictionary(g => g.Key, g => g.ToList());

            var orderList = orders.Select(o =>
            {
                var tid = o.OrderId.ToString();

                paymentGroups.TryGetValue(tid, out var group);
                group ??= new List<PaymentLite>();

                // ✅ pick best: Paid-like > NeedReview > Pending > latest
                var best = group.Count > 0
                    ? group.OrderByDescending(x => IsPaidLike(x.Status))
                           .ThenByDescending(x => string.Equals(x.Status, PayStatus_NeedReview, StringComparison.OrdinalIgnoreCase))
                           .ThenByDescending(x => string.Equals(x.Status, PayStatus_Pending, StringComparison.OrdinalIgnoreCase))
                           .ThenByDescending(x => x.CreatedAt)
                           .FirstOrDefault()
                    : null;

                var displayStatus = ResolveOrderDisplayStatus(o.Status, best?.Status);

                OrderPaymentSummaryDTO? paySummary = null;
                if (best != null)
                {
                    var expires = best.CreatedAt.Add(PaymentTimeout);
                    var isExpired = string.Equals(best.Status, PayStatus_Pending, StringComparison.OrdinalIgnoreCase)
                                    && nowUtc > expires;

                    paySummary = new OrderPaymentSummaryDTO
                    {
                        PaymentId = best.PaymentId,
                        Amount = best.Amount,
                        Status = best.Status,
                        Provider = best.Provider,
                        ProviderOrderCode = best.ProviderOrderCode,
                        PaymentLinkId = best.PaymentLinkId,
                        CreatedAt = best.CreatedAt,
                        ExpiresAtUtc = expires,
                        IsExpired = isExpired
                    };
                }

                return new OrderListItemDTO
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    Email = o.Email,
                    UserName = o.UserName,
                    UserEmail = o.UserEmail,
                    TotalAmount = o.TotalAmount,
                    FinalAmount = (o.TotalAmount - o.DiscountAmount),
                    CreatedAt = o.CreatedAt,
                    ItemCount = o.ItemCount,

                    // ✅ NEW
                    Status = displayStatus,
                    OrderNumber = FormatOrderNumber(o.OrderId, o.CreatedAt),
                    Payment = paySummary,
                    PaymentAttemptCount = group.Count
                };
            }).ToList();

            return Ok(orderList);
        }
        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> GetOrderHistory([FromQuery] Guid? userId)
        {
            if (!userId.HasValue)
                return BadRequest(new { message = "UserId is required" });

            await using var db = _dbFactory.CreateDbContext();

            var orders = await db.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId.Value)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .ToListAsync();

            var orderIds = orders.Select(o => o.OrderId.ToString()).ToList();

            var payments = await db.Payments
                .AsNoTracking()
                .Where(p => p.TargetType == "Order" && p.TargetId != null && orderIds.Contains(p.TargetId))
                .ToListAsync();

            var items = orders.Select(o =>
            {
                // ✅ ưu tiên Paid > Pending > latest (tránh case latest là DupCancelled/Replaced)
                var pay = payments
                    .Where(p => p.TargetId == o.OrderId.ToString())
                    .OrderByDescending(p => IsPaidLike(p.Status))
                    .ThenByDescending(p => string.Equals(p.Status, PayStatus_NeedReview, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(p => string.Equals(p.Status, PayStatus_Pending, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(p => p.CreatedAt)
                    .FirstOrDefault();

                var displayStatus = ResolveOrderDisplayStatus(o.Status, pay?.Status);

                return new OrderHistoryItemDTO
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    OrderNumber = FormatOrderNumber(o.OrderId, o.CreatedAt),
                    Email = o.Email ?? string.Empty,
                    TotalAmount = o.TotalAmount,
                    FinalAmount = (o.TotalAmount - o.DiscountAmount),
                    Status = displayStatus,
                    CreatedAt = o.CreatedAt,
                    ItemCount = o.OrderDetails?.Count ?? 0,
                    ProductNames = o.OrderDetails?
                        .Select(od => od.Variant!.Product?.ProductName ?? od.Variant!.Title ?? string.Empty)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .ToList()
                        ?? new List<string>()
                };
            }).ToList();

            return Ok(items);
        }
        [HttpGet("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> GetOrderById(Guid id,
            [FromQuery] bool includePaymentAttempts = true,
            [FromQuery] bool includeCheckoutUrl = false)
        {
            try
            {
                await using var db = _dbFactory.CreateDbContext();

                var order = await db.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Variant)
                            .ThenInclude(v => v.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (order == null)
                    return NotFound(new { message = "Đơn hàng không được tìm thấy" });

                // Check role: Admin/Storage Staff can view any order
                // Customer can only view their own orders
                var currentUserId = GetCurrentUserIdOrNull();
                var roleCodes = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
                
                bool hasPermission = false;
                
                // Admin or Storage Staff have full access
                if (roleCodes.Contains(RoleCodes.ADMIN) || roleCodes.Contains(RoleCodes.STORAGE_STAFF))
                {
                    hasPermission = true;
                }

                // If user doesn't have permission, check if it's their own order
                if (!hasPermission)
                {
                    if (!currentUserId.HasValue || order.UserId != currentUserId.Value)
                    {
                        // Return 404-like message for security (don't reveal resource existence)
                        return NotFound(new { message = "Đơn hàng không tồn tại." });
                    }
                }

                var orderDto = await MapToOrderDTOAsync(db, order);

                // ✅ Bổ sung payment summary + attempts (multi-attempt)
                var nowUtc = DateTime.UtcNow;
                var targetId = id.ToString();

                var attempts = await db.Payments
                    .AsNoTracking()
                    .Where(p => p.TargetType == "Order" && p.TargetId == targetId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new OrderPaymentAttemptDTO
                    {
                        PaymentId = p.PaymentId,
                        Amount = p.Amount,
                        Status = p.Status,
                        Provider = p.Provider,
                        ProviderOrderCode = p.ProviderOrderCode,
                        PaymentLinkId = p.PaymentLinkId,
                        CreatedAt = p.CreatedAt,
                        ExpiresAtUtc = p.CreatedAt.Add(PaymentTimeout),
                        IsExpired = string.Equals(p.Status, PayStatus_Pending, StringComparison.OrdinalIgnoreCase)
                                    && nowUtc > p.CreatedAt.Add(PaymentTimeout)
                    })
                    .ToListAsync();

                // ✅ best: Paid-like > NeedReview > Pending > latest
                var best = attempts
                    .OrderByDescending(x => IsPaidLike(x.Status))
                    .ThenByDescending(x => string.Equals(x.Status, PayStatus_NeedReview, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(x => string.Equals(x.Status, PayStatus_Pending, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                if (best != null)
                {
                    orderDto.Payment = new OrderPaymentSummaryDTO
                    {
                        PaymentId = best.PaymentId,
                        Amount = best.Amount,
                        Status = best.Status,
                        Provider = best.Provider,
                        ProviderOrderCode = best.ProviderOrderCode,
                        PaymentLinkId = best.PaymentLinkId,
                        CreatedAt = best.CreatedAt,
                        ExpiresAtUtc = best.ExpiresAtUtc,
                        IsExpired = best.IsExpired
                    };

                    if (includeCheckoutUrl
                        && string.Equals(best.Provider, "PayOS", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(best.Status, PayStatus_Pending, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(best.PaymentLinkId)
                        && nowUtc <= best.ExpiresAtUtc)
                    {
                        try
                        {
                            orderDto.Payment.CheckoutUrl = await _payOs.GetCheckoutUrlByPaymentLinkId(best.PaymentLinkId!);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch checkoutUrl from PayOS for PaymentLinkId={PaymentLinkId}", best.PaymentLinkId);
                        }
                    }
                }

                orderDto.OrderNumber = FormatOrderNumber(orderDto.OrderId, orderDto.CreatedAt);

                if (includePaymentAttempts)
                    orderDto.PaymentAttempts = attempts;

                return Ok(orderDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã có lỗi hệ thống. Vui lòng thử lại sau.", error = ex.Message });
            }
        }
        [HttpGet("{id:guid}/details")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> GetOrderDetails(Guid id)
        {
            await using var db = _dbFactory.CreateDbContext();

            var order = await db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });

            // ✅ DB mới ưu tiên đọc Key theo OrderAllocation (KeyId) -> join ProductKey lấy KeyString
            var keysByOrderDetail = await LoadAssignedKeysByOrderDetailAsync(db, id, HttpContext.RequestAborted);

            var orderDetails = order.OrderDetails?.Select(od =>
            {
                var list = keysByOrderDetail.TryGetValue(od.OrderDetailId, out var klist)
                    ? klist
                    : new List<(Guid KeyId, string KeyString)>();

                var take = Math.Min(od.Quantity, list.Count);

                var keyIds = list.Take(take).Select(x => x.KeyId).ToList();
                var keyStrings = list.Take(take).Select(x => x.KeyString).ToList();

                return new OrderDetailDTO
                {
                    OrderDetailId = od.OrderDetailId,
                    VariantId = od.VariantId,
                    VariantTitle = od.Variant?.Title ?? string.Empty,
                    ProductId = od.Variant?.ProductId ?? Guid.Empty,
                    ProductName = od.Variant?.Product?.ProductName ?? string.Empty,
                    ProductCode = od.Variant?.Product?.ProductCode,
                    ProductType = od.Variant?.Product?.ProductType,
                    Quantity = od.Quantity,
                    UnitPrice = od.UnitPrice,

                    KeyId = keyIds.FirstOrDefault(),
                    KeyString = keyStrings.FirstOrDefault(),

                    KeyIds = keyIds,
                    KeyStrings = keyStrings,

                    SubTotal = od.Quantity * od.UnitPrice
                };
            }).ToList() ?? new List<OrderDetailDTO>();

            return Ok(orderDetails);
        }
        // ================== Helpers ==================

        private Guid? GetCurrentUserIdOrNull()
        {
            var claim = User.FindFirst("uid") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;
            return Guid.TryParse(claim.Value, out var id) ? id : (Guid?)null;
        }

        private static string FormatOrderNumber(Guid orderId, DateTime createdAt)
        {
            var dateStr = createdAt.ToString("yyyyMMdd");
            var orderIdStr = orderId.ToString().Replace("-", "").Substring(0, 4).ToUpperInvariant();
            return $"ORD-{dateStr}-{orderIdStr}";
        }

        private async Task<bool> TryClaimCartForCheckoutAsync(KeytietkiemDbContext db, Guid cartId, DateTime nowUtc)
        {
            if (!db.Database.IsRelational())
                return true;

            var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [dbo].[Cart]
                SET [Status] = {"Converting"},
                     [UpdatedAt] = {nowUtc},
                    [ExpiresAt] = CASE WHEN [UserId] IS NULL THEN DATEADD(day, 7, {nowUtc})
                                       ELSE DATEADD(day, 30, {nowUtc}) END
                WHERE [CartId] = {cartId}
                  AND [Status] = {"Active"}
            ");

            return rows == 1;
        }

        private async Task<bool> TryRecoverStuckConvertingCartAsync(KeytietkiemDbContext db, Guid cartId, DateTime nowUtc)
        {
            if (!db.Database.IsRelational())
                return false;

            var cutoff = nowUtc - ConvertingLockTimeout;

            var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [dbo].[Cart]
                SET [Status] = {"Active"},
                   [UpdatedAt] = {nowUtc},
                   [ExpiresAt] = CASE WHEN [UserId] IS NULL THEN DATEADD(day, 7, {nowUtc})
                                       ELSE DATEADD(day, 30, {nowUtc}) END
                WHERE [CartId] = {cartId}
                  AND [Status] = {"Converting"}
                  AND [ConvertedOrderId] IS NULL
                  AND [UpdatedAt] < {cutoff}
            ");

            return rows == 1;
        }

        private async Task<OrderDTO> MapToOrderDTOAsync(KeytietkiemDbContext db, Order order)
        {
            try
            {
                // ✅ ưu tiên Paid > Pending > latest
                var relatedPayment = await db.Payments
                    .AsNoTracking()
                    .Where(p => p.TargetType == "Order" && p.TargetId == order.OrderId.ToString())
                    .OrderByDescending(p => IsPaidLike(p.Status))
                    .ThenByDescending(p => string.Equals(p.Status, PayStatus_NeedReview, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(p => string.Equals(p.Status, PayStatus_Pending, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                var displayStatus = ResolveOrderDisplayStatus(order.Status, relatedPayment?.Status);

                var keysByOrderDetail = await LoadAssignedKeysByOrderDetailAsync(db, order.OrderId, HttpContext.RequestAborted);

                return new OrderDTO
                {
                    OrderId = order.OrderId,
                    UserId = order.UserId,
                    Email = order.Email,
                    UserName = order.User != null
                        ? (order.User.FullName ?? $"{order.User.FirstName} {order.User.LastName}".Trim())
                        : null,
                    UserEmail = order.User?.Email,
                    UserPhone = order.User?.Phone,
                    TotalAmount = order.TotalAmount,
                    DiscountAmount = order.DiscountAmount,
                    FinalAmount = (order.TotalAmount - order.DiscountAmount),
                    Status = displayStatus,
                    CreatedAt = order.CreatedAt,
                    OrderDetails = order.OrderDetails?.Select(od =>
                    {
                        var list = keysByOrderDetail.TryGetValue(od.OrderDetailId, out var klist)
                            ? klist
                            : new List<(Guid KeyId, string KeyString)>();

                        var take = Math.Min(od.Quantity, list.Count);

                        var keyIds = list.Take(take).Select(x => x.KeyId).ToList();
                        var keyStrings = list.Take(take).Select(x => x.KeyString).ToList();

                        return new OrderDetailDTO
                        {
                            OrderDetailId = od.OrderDetailId,
                            VariantId = od.VariantId,
                            VariantTitle = od.Variant?.Title ?? string.Empty,
                            ProductId = od.Variant?.ProductId ?? Guid.Empty,
                            ProductName = od.Variant?.Product?.ProductName ?? string.Empty,
                            ProductCode = od.Variant?.Product?.ProductCode,
                            ProductType = od.Variant?.Product?.ProductType,
                            Quantity = od.Quantity,
                            UnitPrice = od.UnitPrice,

                            KeyId = keyIds.FirstOrDefault(),
                            KeyString = keyStrings.FirstOrDefault(),
                            KeyIds = keyIds,
                            KeyStrings = keyStrings,

                            SubTotal = od.Quantity * od.UnitPrice
                        };
                    }).ToList() ?? new List<OrderDetailDTO>()
                };
            }
            catch
            {
                return new OrderDTO
                {
                    OrderId = order.OrderId,
                    UserId = order.UserId,
                    Email = order.Email,
                    UserName = order.User != null
                        ? (order.User.FullName ?? $"{order.User.FirstName} {order.User.LastName}".Trim())
                        : null,
                    UserEmail = order.User?.Email,
                    UserPhone = order.User?.Phone,
                    TotalAmount = order.TotalAmount,
                    DiscountAmount = order.DiscountAmount,
                    FinalAmount = (order.TotalAmount - order.DiscountAmount),
                    Status = "Cancelled",
                    CreatedAt = order.CreatedAt,
                    OrderDetails = new List<OrderDetailDTO>()
                };
            }
        }

        private async Task<Dictionary<long, List<(Guid KeyId, string KeyString)>>> LoadAssignedKeysByOrderDetailAsync(
     KeytietkiemDbContext db,
     Guid orderId,
     CancellationToken ct)
        {
            // ✅ QUAN TRỌNG: dùng named tuple ở đây để không mất .KeyId/.KeyString khi truyền qua các chỗ khác
            var result = new Dictionary<long, List<(Guid KeyId, string KeyString)>>();

            // ✅ 1) Ưu tiên: OrderDetail.KeyId -> join ProductKey lấy KeyString
            var odKeys = await db.OrderDetails
                .AsNoTracking()
                .Where(od => od.OrderId == orderId && od.KeyId != null)
                .Select(od => new { od.OrderDetailId, KeyId = od.KeyId!.Value })
                .ToListAsync(ct);

            if (odKeys.Count > 0)
            {
                var keyIds = odKeys.Select(x => x.KeyId).Distinct().ToList();

                var keyRows = await db.Set<ProductKey>()
                    .AsNoTracking()
                    .Where(k => keyIds.Contains(k.KeyId))
                    .Select(k => new { k.KeyId, k.KeyString })
                    .ToListAsync(ct);

                var keyStrMap = keyRows.ToDictionary(x => x.KeyId, x => x.KeyString);

                foreach (var row in odKeys)
                {
                    if (!keyStrMap.TryGetValue(row.KeyId, out var keyString) || string.IsNullOrWhiteSpace(keyString))
                        continue;

                    if (!result.TryGetValue(row.OrderDetailId, out var list))
                    {
                        list = new List<(Guid KeyId, string KeyString)>();
                        result[row.OrderDetailId] = list;
                    }

                    if (!list.Any(t => t.KeyId == row.KeyId))
                        list.Add((KeyId: row.KeyId, KeyString: keyString));
                }
            }

            // ✅ 2) Fallback: ProductKey.AssignedToOrderId (flow assign theo order)
            var fallback = await db.Set<ProductKey>()
                .AsNoTracking()
                .Where(k => k.AssignedToOrderId == orderId && k.Status == "Sold")
                .Select(k => new { k.KeyId, k.KeyString, k.VariantId })
                .ToListAsync(ct);

            if (fallback.Count == 0)
                return result;

            var detailMap = await db.OrderDetails
                .AsNoTracking()
                .Where(od => od.OrderId == orderId)
                .Select(od => new { od.OrderDetailId, od.VariantId })
                .ToListAsync(ct);

            // VariantId -> OrderDetailId (giữ logic cũ: variant -> detail đầu tiên)
            var variantToDetailId = detailMap
                .GroupBy(x => x.VariantId)
                .ToDictionary(g => g.Key, g => g.First().OrderDetailId);

            foreach (var k in fallback)
            {
                if (k.KeyId == Guid.Empty || string.IsNullOrWhiteSpace(k.KeyString))
                    continue;

                if (!variantToDetailId.TryGetValue(k.VariantId, out var odId))
                    continue;

                if (!result.TryGetValue(odId, out var list))
                {
                    list = new List<(Guid KeyId, string KeyString)>();
                    result[odId] = list;
                }

                if (!list.Any(t => t.KeyId == k.KeyId))
                    list.Add((KeyId: k.KeyId, KeyString: k.KeyString));
            }

            return result;
        }



        private async Task<IActionResult> BuildCheckoutResponseFromExistingOrderAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            CheckoutFromCartRequestDto dto,
            DateTime nowUtc)
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound(new { message = "Order không tồn tại" });

            if (!IsFinalOrderStatus(order.Status))
            {
                try
                {
                    await EnsureOrderReservedAsync(db, orderId, nowUtc, HttpContext.RequestAborted);
                }
                catch (InvalidOperationException)
                {
                    order.Status = "NeedsManualAction";
                    await db.SaveChangesAsync();
                }
            }

            if (IsFinalOrderStatus(order.Status))
            {
                var latestPay = await db.Payments
                    .Where(p => p.TargetType == "Order" && p.TargetId == orderId.ToString())
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                return Ok(new CheckoutFromCartResponseDto
                {
                    OrderId = orderId,
                    PaymentId = latestPay?.PaymentId ?? Guid.Empty,
                    CheckoutUrl = null,
                    PaymentLinkId = latestPay?.PaymentLinkId,
                    ExpiresAtUtc = latestPay != null ? latestPay.CreatedAt.Add(PaymentTimeout) : nowUtc
                });
            }

            var buyerEmail = order.Email ?? (dto.DeliveryEmail ?? "");
            if (string.IsNullOrWhiteSpace(buyerEmail))
                return BadRequest(new { message = "DeliveryEmail is required" });

            var buyerName = string.IsNullOrWhiteSpace(dto.BuyerName) ? buyerEmail : dto.BuyerName!;
            var buyerPhone = dto.BuyerPhone ?? "";

            var pending = await db.Payments
                .Where(p => p.TargetType == "Order"
                            && p.TargetId == orderId.ToString()
                            && p.Status == PayStatus_Pending)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (pending != null)
            {
                var isWithinTimeout = (nowUtc - pending.CreatedAt) <= PaymentTimeout;

                if (isWithinTimeout && !string.IsNullOrWhiteSpace(pending.PaymentLinkId))
                {
                    try
                    {
                        var url = await _payOs.GetCheckoutUrlByPaymentLinkId(pending.PaymentLinkId!);
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            return Ok(new CheckoutFromCartResponseDto
                            {
                                OrderId = orderId,
                                PaymentId = pending.PaymentId,
                                CheckoutUrl = url,
                                PaymentLinkId = pending.PaymentLinkId,
                                ExpiresAtUtc = pending.CreatedAt.Add(PaymentTimeout)
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch checkoutUrl from PayOS for PaymentLinkId={PaymentLinkId}", pending.PaymentLinkId);
                    }
                }
                if (!string.IsNullOrWhiteSpace(pending.PaymentLinkId))
                {
                    try
                    {
                        await _payOs.CancelPaymentLink(pending.PaymentLinkId!, "Replaced");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to cancel PayOS payment link when replacing attempt. PaymentLinkId={PaymentLinkId}",
                            pending.PaymentLinkId);
                    }
                }
                pending.Status = PayStatus_Replaced;
                await db.SaveChangesAsync();

            }

            var (newPay, payosNew) = await CreateNewOrderPaymentAttemptAsync(
                db, order, buyerEmail, buyerName, buyerPhone, dto, nowUtc);

            return Ok(new CheckoutFromCartResponseDto
            {
                OrderId = orderId,
                PaymentId = newPay.PaymentId,
                CheckoutUrl = payosNew.CheckoutUrl,
                PaymentLinkId = newPay.PaymentLinkId,
                ExpiresAtUtc = newPay.CreatedAt.Add(PaymentTimeout)
            });
        }

        private async Task<PayOSCreatePaymentResult> CreateOrRefreshPayOSLinkAsync(
            Payment payment,
            string buyerName,
            string buyerPhone,
            CheckoutFromCartRequestDto dto,
            DateTime nowUtc)
        {
            var amountInt = (int)Math.Round(payment.Amount, 0, MidpointRounding.AwayFromZero);
            if (amountInt <= 0) throw new Exception("Amount không hợp lệ");

            // ✅ tránh overflow int
            var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var baseCode = (int)(epoch % 2_000_000);               // 0..1,999,999
            var random = Random.Shared.Next(100, 999);             // 100..998
            var orderCode = Math.Abs(baseCode * 1000 + random);    // < 2,000,000,000

            var frontendBaseUrl = _config["PayOS:FrontendBaseUrl"]?.TrimEnd('/') ?? "https://keytietkiem.com";

            var returnUrl = !string.IsNullOrWhiteSpace(dto.ReturnUrl)
                ? dto.ReturnUrl!
                : $"{frontendBaseUrl}/checkout/return?paymentId={payment.PaymentId}&target=order";

            var cancelUrl = !string.IsNullOrWhiteSpace(dto.CancelUrl)
                ? dto.CancelUrl!
                : $"{frontendBaseUrl}/checkout/cancel?paymentId={payment.PaymentId}&target=order";

            var desc = $"ORD_{payment.TargetId}";
            if (desc.Length > 25) desc = desc.Substring(0, 25);

            var buyerEmail = payment.Email ?? "";

            var res = await _payOs.CreatePaymentV2(
                orderCode: orderCode,
                amount: amountInt,
                description: desc,
                returnUrl: returnUrl,
                cancelUrl: cancelUrl,
                buyerPhone: buyerPhone ?? "",
                buyerName: buyerName ?? buyerEmail,
                buyerEmail: buyerEmail
            );

            payment.Provider = "PayOS";
            payment.ProviderOrderCode = orderCode;
            payment.PaymentLinkId = res.PaymentLinkId;

            return res;
        }

        private static decimal GetOrderPayAmount(Order order)
        {
            var final = (order.TotalAmount - order.DiscountAmount);
            if (final < 0) final = 0;
            return final;
        }

        private static bool IsFinalOrderStatus(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            return s.Equals("Paid", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                || s.Equals("CancelledByTimeout", StringComparison.OrdinalIgnoreCase)
                || s.Equals("NeedsManualAction", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPaidLike(string? paymentStatus)
        {
            if (string.IsNullOrWhiteSpace(paymentStatus)) return false;
            var ps = paymentStatus.Trim();
            return ps.Equals(PayStatus_Paid, StringComparison.OrdinalIgnoreCase)
                || ps.Equals("Success", StringComparison.OrdinalIgnoreCase)
                || ps.Equals("Completed", StringComparison.OrdinalIgnoreCase);
        }

        // ✅ cập nhật mapping theo bộ status mới (Timeout/NeedReview)
        private string ResolveOrderDisplayStatus(string? orderStatus, string? paymentStatus)
        {
            var os = orderStatus?.Trim();
            var ps = paymentStatus?.Trim();

            if (!string.IsNullOrWhiteSpace(os))
            {
                if (IsFinalOrderStatus(os) || string.IsNullOrWhiteSpace(ps))
                    return os;

                if (os.Equals("PendingPayment", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsPaidLike(ps)) return "Paid";
                    if (ps.Equals(PayStatus_Cancelled, StringComparison.OrdinalIgnoreCase)) return "Cancelled";
                    if (ps.Equals(PayStatus_Timeout, StringComparison.OrdinalIgnoreCase)) return "CancelledByTimeout";
                    if (ps.Equals(PayStatus_NeedReview, StringComparison.OrdinalIgnoreCase)) return "NeedsManualAction";
                }

                return os;
            }


            if (!string.IsNullOrWhiteSpace(ps))
            {
                if (IsPaidLike(ps)) return "Paid";
                if (ps.Equals(PayStatus_Pending, StringComparison.OrdinalIgnoreCase)) return "PendingPayment";
                if (ps.Equals(PayStatus_Timeout, StringComparison.OrdinalIgnoreCase)) return "CancelledByTimeout";
                if (ps.Equals(PayStatus_NeedReview, StringComparison.OrdinalIgnoreCase)) return "NeedsManualAction";
                if (ps.Equals(PayStatus_Cancelled, StringComparison.OrdinalIgnoreCase)) return "Cancelled";
                return ps;
            }

            return "Unknown";
        }

        private async Task<(Payment payment, PayOSCreatePaymentResult payos)> CreateNewOrderPaymentAttemptAsync(
            KeytietkiemDbContext db,
            Order order,
            string buyerEmail,
            string buyerName,
            string buyerPhone,
            CheckoutFromCartRequestDto dto,
            DateTime nowUtc)
        {
            var amount = GetOrderPayAmount(order);

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                Amount = amount,
                Status = PayStatus_Pending,
                CreatedAt = nowUtc,
                Provider = "PayOS",
                Email = buyerEmail,
                TargetType = "Order",
                TargetId = order.OrderId.ToString()
            };

            var payos = await CreateOrRefreshPayOSLinkAsync(payment, buyerName, buyerPhone, dto, nowUtc);

            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            return (payment, payos);
        }

        private static bool IsActiveStatus(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            return s.Equals("Active", StringComparison.OrdinalIgnoreCase)
                || s.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSellableProduct(Product? p)
        {
            if (p == null) return false;
            return IsActiveStatus(p.Status);
        }

        private async Task<(List<(Guid VariantId, int Quantity)> lines,
                            Dictionary<Guid, decimal> unitPriceByVariantId,
                            decimal totalListAmount,
                            decimal totalAmount)> GuardAndRepriceCartAsync(
            KeytietkiemDbContext db,
            List<CartItem> validItems,
            CancellationToken ct)
        {
            var variantIds = validItems.Select(x => x.VariantId).Distinct().ToList();

            var variants = await db.ProductVariants
                .Include(v => v.Product)
                .Where(v => variantIds.Contains(v.VariantId))
                .ToListAsync(ct);

            var map = variants.ToDictionary(v => v.VariantId);

            var unitPriceByVariantId = new Dictionary<Guid, decimal>();
            decimal totalList = 0m;
            decimal total = 0m;

            var lines = new List<(Guid VariantId, int Quantity)>();

            foreach (var item in validItems)
            {
                if (!map.TryGetValue(item.VariantId, out var v) || v.Product == null)
                    throw new InvalidOperationException("Sản phẩm/biến thể không tồn tại.");

                if (!IsActiveStatus(v.Status) || !IsSellableProduct(v.Product))
                    throw new InvalidOperationException("Có sản phẩm đã ngừng bán hoặc bị khóa. Vui lòng cập nhật giỏ hàng.");

                var qty = item.Quantity <= 0 ? 0 : item.Quantity;

                var listPrice = v.ListPrice != 0 ? v.ListPrice : v.SellPrice;
                if (listPrice < 0) listPrice = 0;

                var unitPrice = v.SellPrice > 0 ? v.SellPrice : v.ListPrice;
                if (unitPrice < 0) unitPrice = 0;

                totalList += listPrice * qty;
                total += unitPrice * qty;

                unitPriceByVariantId[item.VariantId] = unitPrice;
                lines.Add((item.VariantId, qty));
            }

            totalList = Math.Round(totalList, 2, MidpointRounding.AwayFromZero);
            total = Math.Round(total, 2, MidpointRounding.AwayFromZero);

            return (lines, unitPriceByVariantId, totalList, total);
        }

        private async Task EnsureOrderReservedAsync(KeytietkiemDbContext db, Guid orderId, DateTime nowUtc, CancellationToken ct)
        {
            var lines = await db.OrderDetails
                .AsNoTracking()
                .Where(od => od.OrderId == orderId)
                .Select(od => new { od.VariantId, od.Quantity })
                .ToListAsync(ct);

            var list = new List<(Guid VariantId, int Quantity)>();
            foreach (var x in lines) list.Add((x.VariantId, x.Quantity));

            var until = nowUtc.Add(PaymentTimeout);

            try
            {
                // ✅ ưu tiên gia hạn nếu đã có reservation
                await _inventoryReservation.ExtendReservationAsync(db, orderId, until, nowUtc, ct);
            }
            catch
            {
                // ✅ chưa có reservation (hoặc đã bị release) -> reserve lại rồi gia hạn
                await _inventoryReservation.ReserveForOrderAsync(db, orderId, list, nowUtc, until, ct);
                await _inventoryReservation.ExtendReservationAsync(db, orderId, until, nowUtc, ct);
            }

        }
    }
}
