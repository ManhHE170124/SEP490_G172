using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Orders;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
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
          [FromQuery] string? search = null,                
     [FromQuery] DateTime? createdFrom = null,
     [FromQuery] DateTime? createdTo = null,
     [FromQuery] string? orderStatus = null,
     [FromQuery] decimal? minTotal = null,            
     [FromQuery] decimal? maxTotal = null,
     [FromQuery] string? sortBy = "createdat",         
     [FromQuery] string? sortDir = "desc",              
     [FromQuery] int pageIndex = 1,
     [FromQuery] int pageSize = 20)
        {
            await using var db = _dbFactory.CreateDbContext();

            if (pageIndex <= 0) pageIndex = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = db.Orders.AsNoTracking().AsQueryable();

            // ✅ Search theo OrderId (Guid) hoặc email (guest Email / user Email)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                if (Guid.TryParse(term, out var oid))
                {
                    query = query.Where(o => o.OrderId == oid);
                }
                else
                {
                    var lower = term.ToLower();
                    query = query.Where(o =>
                        (!string.IsNullOrWhiteSpace(o.Email) && o.Email.ToLower().Contains(lower)) ||
                        (o.User != null && !string.IsNullOrWhiteSpace(o.User.Email) && o.User.Email.ToLower().Contains(lower)));
                }
            }

            // ✅ Filter created range
            if (createdFrom.HasValue)
                query = query.Where(o => o.CreatedAt >= createdFrom.Value);

            if (createdTo.HasValue)
                query = query.Where(o => o.CreatedAt <= createdTo.Value);

            // ✅ Filter status
            if (!string.IsNullOrWhiteSpace(orderStatus))
                query = query.Where(o => o.Status == orderStatus);

            // ✅ Filter price range theo FinalAmount
            if (minTotal.HasValue)
                query = query.Where(o => (o.TotalAmount - o.DiscountAmount) >= minTotal.Value);

            if (maxTotal.HasValue)
                query = query.Where(o => (o.TotalAmount - o.DiscountAmount) <= maxTotal.Value);

            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            // ✅ Sort
            query = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "orderid" => desc ? query.OrderByDescending(o => o.OrderId) : query.OrderBy(o => o.OrderId),
                "amount" or "finalamount" or "total" or "price" =>
                    desc ? query.OrderByDescending(o => (o.TotalAmount - o.DiscountAmount)) : query.OrderBy(o => (o.TotalAmount - o.DiscountAmount)),
                "status" => desc ? query.OrderByDescending(o => o.Status) : query.OrderBy(o => o.Status),
                _ => desc ? query.OrderByDescending(o => o.CreatedAt) : query.OrderBy(o => o.CreatedAt),
            };

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderListItemDTO
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    Email = o.Email,

                    UserName = o.User != null
                        ? (o.User.FullName ?? $"{o.User.FirstName} {o.User.LastName}".Trim())
                        : null,
                    UserEmail = o.User != null ? o.User.Email : null,

                    TotalAmount = o.TotalAmount,
                    FinalAmount = (o.TotalAmount - o.DiscountAmount),
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,

                    ItemCount = db.OrderDetails.Count(od => od.OrderId == o.OrderId),

                    // nếu bạn đang dùng orderNumber theo FormatOrderNumber
                    OrderNumber = null,

                    // giữ nguyên nếu bạn đang show payment summary ở list (không bắt buộc theo yêu cầu)
                    Payment = null,
                    PaymentAttemptCount = 0
                })
                .ToListAsync();

            // (optional) gán OrderNumber (nếu bạn muốn)
            foreach (var it in items)
                it.OrderNumber = FormatOrderNumber(it.OrderId, it.CreatedAt);

            return Ok(new
            {
                items,
                totalItems,
                pageIndex,
                pageSize
            });
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
            [FromQuery] bool includeCheckoutUrl = false,

            // ✅ apply cho OrderItems
            [FromQuery] string? search = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? sortBy = "orderdetailid",   // orderdetailid|varianttitle|quantity|unitprice
            [FromQuery] string? sortDir = "asc",
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                await using var db = _dbFactory.CreateDbContext();

                if (pageIndex <= 0) pageIndex = 1;
                if (pageSize <= 0) pageSize = 20;

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

                // ✅ Bổ sung payment summary + attempts (giữ nguyên logic cũ của bạn)
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

                // ================== ✅ Filter/Search/Sort/Paging cho OrderItems ==================
                var allItems = orderDto.OrderDetails ?? new List<OrderDetailDTO>();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim();
                    if (long.TryParse(term, out var odid))
                    {
                        allItems = allItems.Where(x => x.OrderDetailId == odid).ToList();
                    }
                    else
                    {
                        var lower = term.ToLowerInvariant();
                        allItems = allItems.Where(x =>
                                (!string.IsNullOrWhiteSpace(x.VariantTitle) && x.VariantTitle.ToLower().Contains(lower)) ||
                                (!string.IsNullOrWhiteSpace(x.ProductName) && x.ProductName.ToLower().Contains(lower)) ||
                                (!string.IsNullOrWhiteSpace(x.ProductCode) && x.ProductCode.ToLower().Contains(lower)) ||
                                (!string.IsNullOrWhiteSpace(x.KeyString) && x.KeyString.ToLower().Contains(lower)) ||
                                (!string.IsNullOrWhiteSpace(x.AccountEmail) && x.AccountEmail.ToLower().Contains(lower))
                            )
                            .ToList();
                    }
                }

                if (minPrice.HasValue)
                    allItems = allItems.Where(x => x.UnitPrice >= minPrice.Value).ToList();

                if (maxPrice.HasValue)
                    allItems = allItems.Where(x => x.UnitPrice <= maxPrice.Value).ToList();

                var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
                allItems = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
                {
                    "varianttitle" => desc ? allItems.OrderByDescending(x => x.VariantTitle).ToList() : allItems.OrderBy(x => x.VariantTitle).ToList(),
                    "quantity" => desc ? allItems.OrderByDescending(x => x.Quantity).ToList() : allItems.OrderBy(x => x.Quantity).ToList(),
                    "unitprice" => desc ? allItems.OrderByDescending(x => x.UnitPrice).ToList() : allItems.OrderBy(x => x.UnitPrice).ToList(),
                    _ => desc ? allItems.OrderByDescending(x => x.OrderDetailId).ToList() : allItems.OrderBy(x => x.OrderDetailId).ToList(),
                };

                var totalItems = allItems.Count;
                var pagedItems = allItems
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // ✅ đảm bảo payload không “lộ” list full ngoài paging
                orderDto.OrderDetails = pagedItems;

                return Ok(new OrderDetailResponseDto
                {
                    Order = orderDto,
                    OrderItems = pagedItems,
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                    TotalItems = totalItems
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã có lỗi hệ thống. Vui lòng thử lại sau.", error = ex.Message });
            }
        }
        [HttpGet("{id:guid}/details")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.STORAGE_STAFF)]
        public async Task<IActionResult> GetOrderDetails(
            Guid id,
            [FromQuery] string? search = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? sortBy = "orderdetailid",
            [FromQuery] string? sortDir = "asc",
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20)
        {
            await using var db = _dbFactory.CreateDbContext();

            if (pageIndex <= 0) pageIndex = 1;
            if (pageSize <= 0) pageSize = 20;

            var order = await db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });

            var orderDto = await MapToOrderDTOAsync(db, order);
            var allItems = orderDto.OrderDetails ?? new List<OrderDetailDTO>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                if (long.TryParse(term, out var odid))
                {
                    allItems = allItems.Where(x => x.OrderDetailId == odid).ToList();
                }
                else
                {
                    var lower = term.ToLowerInvariant();
                    allItems = allItems.Where(x =>
                            (!string.IsNullOrWhiteSpace(x.VariantTitle) && x.VariantTitle.ToLower().Contains(lower)) ||
                            (!string.IsNullOrWhiteSpace(x.ProductName) && x.ProductName.ToLower().Contains(lower)) ||
                            (!string.IsNullOrWhiteSpace(x.ProductCode) && x.ProductCode.ToLower().Contains(lower)) ||
                            (!string.IsNullOrWhiteSpace(x.KeyString) && x.KeyString.ToLower().Contains(lower)) ||
                            (!string.IsNullOrWhiteSpace(x.AccountEmail) && x.AccountEmail.ToLower().Contains(lower))
                        )
                        .ToList();
                }
            }

            if (minPrice.HasValue)
                allItems = allItems.Where(x => x.UnitPrice >= minPrice.Value).ToList();

            if (maxPrice.HasValue)
                allItems = allItems.Where(x => x.UnitPrice <= maxPrice.Value).ToList();

            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            allItems = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "varianttitle" => desc ? allItems.OrderByDescending(x => x.VariantTitle).ToList() : allItems.OrderBy(x => x.VariantTitle).ToList(),
                "quantity" => desc ? allItems.OrderByDescending(x => x.Quantity).ToList() : allItems.OrderBy(x => x.Quantity).ToList(),
                "unitprice" => desc ? allItems.OrderByDescending(x => x.UnitPrice).ToList() : allItems.OrderBy(x => x.UnitPrice).ToList(),
                _ => desc ? allItems.OrderByDescending(x => x.OrderDetailId).ToList() : allItems.OrderBy(x => x.OrderDetailId).ToList(),
            };

            var totalItems = allItems.Count;

            var paged = allItems
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                items = paged,
                totalItems,
                pageIndex,
                pageSize
            });
        }

        [HttpGet("{orderId:guid}/details/{orderDetailId:long}/credentials")]
        public async Task<IActionResult> GetOrderDetailCredentials(
            Guid orderId,
            long orderDetailId)
        {
            await using var db = _dbFactory.CreateDbContext();

            var order = await db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });

            var od = order.OrderDetails?.FirstOrDefault(x => x.OrderDetailId == orderDetailId);
            if (od == null)
                return NotFound(new { message = "Order detail không được tìm thấy" });

            // ✅ keys
            var keysByOrderDetail = await LoadAssignedKeysByOrderDetailAsync(db, orderId, HttpContext.RequestAborted);
            var keys = keysByOrderDetail.TryGetValue(orderDetailId, out var klist)
                ? klist.Take(Math.Min(Math.Max(od.Quantity, 1), klist.Count)).ToList()
                : new List<(Guid KeyId, string KeyString)>();

            // ✅ accounts
            var accountsByOrderDetail = await LoadAssignedAccountsByOrderDetailAsync(
                db,
                orderId,
                order.OrderDetails?.ToList() ?? new List<OrderDetail>(),
                HttpContext.RequestAborted);

            var accounts = accountsByOrderDetail.TryGetValue(orderDetailId, out var alist)
                ? (alist ?? new List<OrderAccountCredentialDTO>())
                : new List<OrderAccountCredentialDTO>();

            return Ok(new
            {
                orderId,
                orderDetailId,
                variantId = od.VariantId,
                productType = od.Variant?.Product?.ProductType,
                keys = keys.Select(x => new { keyId = x.KeyId, keyString = x.KeyString }).ToList(),
                accounts
            });
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
            // Payment (nếu lỗi vẫn trả order.Status)
            Payment? relatedPayment = null;
            try
            {
                relatedPayment = await db.Payments
                    .AsNoTracking()
                    .Where(p => p.TargetType == "Order" && p.TargetId == order.OrderId.ToString())
                    .OrderByDescending(p => IsPaidLike(p.Status))
                    .ThenByDescending(p => string.Equals(p.Status, PayStatus_NeedReview, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(p => string.Equals(p.Status, PayStatus_Pending, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MapToOrderDTOAsync: load related payment failed for {OrderId}", order.OrderId);
            }

            var displayStatus = ResolveOrderDisplayStatus(order.Status, relatedPayment?.Status);

            Dictionary<long, List<(Guid KeyId, string KeyString)>> keysByOrderDetail = new();
            try
            {
                keysByOrderDetail = await LoadAssignedKeysByOrderDetailAsync(db, order.OrderId, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MapToOrderDTOAsync: load keys failed for {OrderId}", order.OrderId);
            }

            // Sau đoạn load key (keysByOrderDetail)
            Dictionary<long, List<OrderAccountCredentialDTO>> accountsByOrderDetail = new();
            try
            {
                // Lấy tất cả account gắn với đơn hàng qua ProductAccountCustomer
                var accQuery = from pac in db.ProductAccountCustomers
                               join pa in db.ProductAccounts on pac.ProductAccountId equals pa.ProductAccountId
                               join od in db.OrderDetails on pac.OrderId equals od.OrderId
                               where pac.OrderId == order.OrderId
                               select new
                               {
                                   od.OrderDetailId,
                                   pa.AccountEmail,
                                   pa.AccountUsername,
                                   pa.AccountPassword
                               };

                var accList = await accQuery.ToListAsync();

                foreach (var grp in accList.GroupBy(x => x.OrderDetailId))
                {
                    var lst = grp.Select(x => new OrderAccountCredentialDTO
                    {
                        Email = x.AccountEmail,
                        Username = x.AccountUsername,
                        Password = EncryptionHelper.Decrypt(x.AccountPassword, _config["EncryptionConfig:Key"])
                    }).ToList();
                    accountsByOrderDetail[grp.Key] = lst;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MapToOrderDTOAsync: load accounts failed for {OrderId}", order.OrderId);
            }


            var details = order.OrderDetails?.Select(od =>
            {
                var list = keysByOrderDetail.TryGetValue(od.OrderDetailId, out var klist)
                    ? klist
                    : new List<(Guid KeyId, string KeyString)>();

                var take = Math.Min(od.Quantity, list.Count);
                var keyIds = list.Take(take).Select(x => x.KeyId).ToList();
                var keyStrings = list.Take(take).Select(x => x.KeyString).ToList();
                var productType = od.Variant?.Product?.ProductType;
                var isAccount = IsAccountProductType(productType);

                var accPicked = (isAccount && accountsByOrderDetail.TryGetValue(od.OrderDetailId, out var alist))
                    ? (alist ?? new List<OrderAccountCredentialDTO>())
                    : new List<OrderAccountCredentialDTO>();

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

                    Accounts = accPicked,
                    AccountEmail = isAccount ? accPicked.FirstOrDefault()?.Email : null,
                    AccountUsername = isAccount ? accPicked.FirstOrDefault()?.Username : null,
                    AccountPassword = isAccount ? accPicked.FirstOrDefault()?.Password : null,

                    SubTotal = od.Quantity * od.UnitPrice
                };
            }).ToList() ?? new List<OrderDetailDTO>();

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
                OrderDetails = details
            };
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
                .Select(od => new { od.OrderDetailId, od.VariantId, od.Quantity, KeyId = od.KeyId!.Value })
                .OrderBy(x => x.OrderDetailId)
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
                .OrderBy(k => k.KeyId)
                .ToListAsync(ct);

            if (fallback.Count == 0)
                return result;

            // map OrderDetail theo Variant để phân bổ keys đúng theo Quantity
            var details = await db.OrderDetails
                .AsNoTracking()
                .Where(od => od.OrderId == orderId)
                .Select(od => new { od.OrderDetailId, od.VariantId, od.Quantity })
                .OrderBy(od => od.OrderDetailId)
                .ToListAsync(ct);

            var keyQueues = fallback
                .Where(k => k.VariantId != Guid.Empty && !string.IsNullOrWhiteSpace(k.KeyString))
                .GroupBy(k => k.VariantId)
                .ToDictionary(
                    g => g.Key,
                    g => new Queue<(Guid KeyId, string KeyString)>(g
                        .Select(x => (KeyId: x.KeyId, KeyString: x.KeyString))
                        .Distinct()
                        .ToList()));

            foreach (var od in details)
            {
                if (!keyQueues.TryGetValue(od.VariantId, out var q) || q.Count == 0)
                    continue;

                // đã có keys từ OrderDetail.KeyId thì không lấy trùng
                if (result.TryGetValue(od.OrderDetailId, out var exist) && exist.Count > 0)
                {
                    var used = new HashSet<Guid>(exist.Select(x => x.KeyId));
                    if (used.Count > 0 && q.Count > 0)
                    {
                        var tmp = new Queue<(Guid KeyId, string KeyString)>();
                        while (q.Count > 0)
                        {
                            var k = q.Dequeue();
                            if (!used.Contains(k.KeyId))
                                tmp.Enqueue(k);
                        }
                        q = tmp;
                        keyQueues[od.VariantId] = q;
                    }
                }

                var need = Math.Max(od.Quantity, 1);
                var take = Math.Min(need, q.Count);
                if (take <= 0) continue;

                if (!result.TryGetValue(od.OrderDetailId, out var list))
                {
                    list = new List<(Guid KeyId, string KeyString)>();
                    result[od.OrderDetailId] = list;
                }

                for (int i = 0; i < take; i++)
                {
                    var k = q.Dequeue();
                    if (!list.Any(t => t.KeyId == k.KeyId))
                        list.Add(k);
                }
            }

            return result;
        }
        private async Task<Dictionary<Guid, List<OrderAccountCredentialDTO>>> LoadAssignedAccountsByVariantAsync(
    KeytietkiemDbContext db,
    Guid orderId,
    CancellationToken ct)
        {
            var result = new Dictionary<Guid, List<OrderAccountCredentialDTO>>();

            // ✅ Admin cần xem credentials đã gán cho đơn, không lọc IsActive ở đây
            var baseQuery = db.Set<ProductAccountCustomer>()
                .AsNoTracking()
                .Where(pac => pac.OrderId == orderId)
                .Join(db.Set<ProductAccount>().AsNoTracking(),
                    pac => pac.ProductAccountId,
                    pa => pa.ProductAccountId,
                    (pac, pa) => pa);

            // ✅ detect column username (tránh compile/runtime lệch tên)
            var paEntity = db.Model.FindEntityType(typeof(ProductAccount));
            var hasAccountUsername = paEntity?.FindProperty("AccountUsername") != null;
            var hasAccountUserName = paEntity?.FindProperty("AccountUserName") != null;

            List<(Guid VariantId, string? Email, string? Username, string? Password)> rows;

            if (hasAccountUsername)
            {
                rows = await baseQuery
                    .Select(pa => new ValueTuple<Guid, string?, string?, string?>(
                        pa.VariantId,
                        pa.AccountEmail,
                        EF.Property<string?>(pa, "AccountUsername"),
                        pa.AccountPassword))
                    .ToListAsync(ct);
            }
            else if (hasAccountUserName)
            {
                rows = await baseQuery
                    .Select(pa => new ValueTuple<Guid, string?, string?, string?>(
                        pa.VariantId,
                        pa.AccountEmail,
                        EF.Property<string?>(pa, "AccountUserName"),
                        pa.AccountPassword))
                    .ToListAsync(ct);
            }
            else
            {
                rows = await baseQuery
                    .Select(pa => new ValueTuple<Guid, string?, string?, string?>(
                        pa.VariantId,
                        pa.AccountEmail,
                        null,
                        pa.AccountPassword))
                    .ToListAsync(ct);
            }

            var encKey = _config["EncryptionConfig:Key"] ?? _config["Encryption:Key"];

            foreach (var r in rows)
            {
                var variantId = r.Item1;
                var email = r.Item2;
                var username = r.Item3;
                var password = r.Item4 ?? string.Empty;

                if (variantId == Guid.Empty) continue;
                if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(username)) continue;

                if (!string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(encKey))
                {
                    try
                    {
                        password = EncryptionHelper.Decrypt(password, encKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Decrypt ProductAccountPassword failed for order {OrderId}", orderId);
                    }
                }

                if (!result.TryGetValue(variantId, out var list))
                {
                    list = new List<OrderAccountCredentialDTO>();
                    result[variantId] = list;
                }

                list.Add(new OrderAccountCredentialDTO
                {
                    Email = email ?? string.Empty,
                    Username = username,
                    Password = password
                });
            }

            return result;
        }

        private async Task<Dictionary<long, List<OrderAccountCredentialDTO>>> LoadAssignedAccountsByOrderDetailAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            List<OrderDetail> orderDetails,
            CancellationToken ct)
        {
            var result = new Dictionary<long, List<OrderAccountCredentialDTO>>();

            if (orderDetails == null || orderDetails.Count == 0)
                return result;

            var byVariant = await LoadAssignedAccountsByVariantAsync(db, orderId, ct);
            if (byVariant.Count == 0)
                return result;

            var queues = byVariant.ToDictionary(
                kv => kv.Key,
                kv => new Queue<OrderAccountCredentialDTO>(kv.Value ?? new List<OrderAccountCredentialDTO>()));

            foreach (var od in orderDetails.OrderBy(x => x.OrderDetailId))
            {
                if (!queues.TryGetValue(od.VariantId, out var q) || q.Count == 0)
                    continue;

                var need = Math.Max(od.Quantity, 1);
                var take = Math.Min(need, q.Count);
                if (take <= 0) continue;

                var picked = new List<OrderAccountCredentialDTO>(take);
                for (int i = 0; i < take; i++)
                    picked.Add(q.Dequeue());

                result[od.OrderDetailId] = picked;
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

            var frontendBaseUrl = PublicUrlHelper.GetPublicOrigin(HttpContext, _config);

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
        private static bool IsAccountProductType(string? productType)
        {
            if (string.IsNullOrWhiteSpace(productType)) return false;
            return productType.Contains("account", StringComparison.OrdinalIgnoreCase);
        }

    }
}
