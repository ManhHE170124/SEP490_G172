using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Orders;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IProductAccountService _productAccountService;
        private readonly IAuditLogger _auditLogger;

        public OrdersController(
            KeytietkiemDbContext context,
            IProductAccountService productAccountService,
            IAuditLogger auditLogger)
        {
            _context = context;
            _productAccountService = productAccountService;
            _auditLogger = auditLogger;
        }

        // ========== CÁC API READ-ONLY ==========

        /// <summary>
        /// Admin xem danh sách đơn hàng (read-only)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrders(
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDir)
        {
            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .AsQueryable();

            // Sort phía server (không còn sort theo Status nữa)
            var sortByNorm = (sortBy ?? "CreatedAt").Trim();
            var sortDirNorm = (sortDir ?? "desc").Trim().ToLowerInvariant();
            var asc = sortDirNorm == "asc";

            switch (sortByNorm.ToLowerInvariant())
            {
                case "orderid":
                    query = asc
                        ? query.OrderBy(o => o.OrderId)
                        : query.OrderByDescending(o => o.OrderId);
                    break;

                case "customer":
                case "username":
                    if (asc)
                    {
                        query = query.OrderBy(o =>
                            o.User != null
                                ? (o.User.FullName ?? o.User.Email ?? o.Email)
                                : o.Email);
                    }
                    else
                    {
                        query = query.OrderByDescending(o =>
                            o.User != null
                                ? (o.User.FullName ?? o.User.Email ?? o.Email)
                                : o.Email);
                    }
                    break;

                case "email":
                    query = asc
                        ? query.OrderBy(o => o.Email)
                        : query.OrderByDescending(o => o.Email);
                    break;

                case "totalamount":
                    query = asc
                        ? query.OrderBy(o => o.TotalAmount)
                        : query.OrderByDescending(o => o.TotalAmount);
                    break;

                case "finalamount":
                    query = asc
                        ? query.OrderBy(o => o.FinalAmount ?? (o.TotalAmount - o.DiscountAmount))
                        : query.OrderByDescending(o => o.FinalAmount ?? (o.TotalAmount - o.DiscountAmount));
                    break;

                case "itemcount":
                    query = asc
                        ? query.OrderBy(o => o.OrderDetails.Count)
                        : query.OrderByDescending(o => o.OrderDetails.Count);
                    break;

                default: // CreatedAt
                    query = asc
                        ? query.OrderBy(o => o.CreatedAt)
                        : query.OrderByDescending(o => o.CreatedAt);
                    break;
            }

            var orders = await query.ToListAsync();

            var orderList = orders.Select(o => new OrderListItemDTO
            {
                OrderId = o.OrderId,
                UserId = o.UserId,
                Email = o.Email,
                UserName = o.User != null
                    ? (o.User.FullName ?? $"{o.User.FirstName} {o.User.LastName}".Trim())
                    : null,
                UserEmail = o.User?.Email,
                TotalAmount = o.TotalAmount,
                FinalAmount = o.FinalAmount,
                CreatedAt = o.CreatedAt,
                ItemCount = o.OrderDetails?.Count ?? 0
            }).ToList();

            return Ok(orderList);
        }

        /// <summary>
        /// Lịch sử đơn hàng của 1 user (read-only)
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetOrderHistory([FromQuery] Guid? userId)
        {
            if (!userId.HasValue)
            {
                return BadRequest(new { message = "UserId is required" });
            }

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId.Value)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .ToListAsync();

            // Lấy tất cả payments liên quan để map status
            var orderIds = orders.Select(o => o.OrderId).ToList();
            var finalAmounts = orders.ToDictionary(o => o.OrderId, o => o.FinalAmount ?? (o.TotalAmount - o.DiscountAmount));
            var orderEmails = orders.ToDictionary(o => o.OrderId, o => o.Email);
            var orderCreatedAts = orders.ToDictionary(o => o.OrderId, o => o.CreatedAt);

            var payments = await _context.Payments
                .AsNoTracking()
                .Where(p =>
                    p.TransactionType == "ORDER_PAYMENT" &&
                    orderEmails.Values.Contains(p.Email))
                .ToListAsync();

            var items = orders.Select(o =>
            {
                var finalAmount = o.FinalAmount ?? (o.TotalAmount - o.DiscountAmount);

                // Tìm payment liên quan: Email khớp, TransactionType = ORDER_PAYMENT, Amount gần bằng FinalAmount, CreatedAt gần với Order.CreatedAt
                var relatedPayment = payments
                    .Where(p =>
                        p.Email == o.Email &&
                        p.TransactionType == "ORDER_PAYMENT" &&
                        Math.Abs((decimal)(p.Amount - finalAmount)) < 0.01m && // Cho phép sai số nhỏ
                        Math.Abs((p.CreatedAt - o.CreatedAt).TotalMinutes) < 5) // Trong vòng 5 phút
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();

                var status = relatedPayment?.Status ?? "Cancelled"; // Mặc định là Cancelled nếu không tìm thấy payment

                return new OrderHistoryItemDTO
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    OrderNumber = FormatOrderNumber(o.OrderId, o.CreatedAt),
                    Email = o.Email ?? string.Empty,
                    TotalAmount = o.TotalAmount,
                    FinalAmount = o.FinalAmount,
                    Status = status,
                    CreatedAt = o.CreatedAt,
                    ItemCount = o.OrderDetails?.Count ?? 0,
                    ProductNames = o.OrderDetails?
                        .Select(od => od.Variant!.Product?.ProductName
                                      ?? od.Variant!.Title
                                      ?? string.Empty)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .ToList()
                        ?? new List<string>()
                };
            }).ToList();

            return Ok(items);
        }

        /// <summary>
        /// Xem chi tiết 1 đơn (thông tin tổng + list OrderDetails)
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOrderById(Guid id)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Variant)
                            .ThenInclude(v => v.Product)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Key)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (order == null)
                {
                    return NotFound(new { message = "Đơn hàng không được tìm thấy" });
                }

                var orderDto = await MapToOrderDTOAsync(order);

                // Không log success 200 để tránh spam audit log
                return Ok(orderDto);
            }
            catch (Exception ex)
            {
                // Không ghi audit log, chỉ trả 500
                return StatusCode(500, new { message = "Đã có lỗi hệ thống. Vui lòng thử lại sau.", error = ex.Message });
            }
        }

        /// <summary>
        /// Chỉ lấy phần chi tiết items của 1 đơn
        /// </summary>
        [HttpGet("{id:guid}/details")]
        public async Task<IActionResult> GetOrderDetails(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Key)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });
            }

            var orderDetails = order.OrderDetails?.Select(od => new OrderDetailDTO
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
                KeyId = od.KeyId,
                KeyString = od.Key?.KeyString,
                SubTotal = od.Quantity * od.UnitPrice
            }).ToList() ?? new List<OrderDetailDTO>();

            // Không log success 200 để tránh spam audit log
            return Ok(orderDetails);
        }

        [HttpGet("{orderId:guid}/details/{orderDetailId:long}/credentials")]
        public async Task<IActionResult> GetOrderDetailCredentials(Guid orderId, long orderDetailId)
        {
            var orderDetail = await _context.OrderDetails
                .Include(od => od.Variant)
                    .ThenInclude(v => v.Product)
                .Include(od => od.Key)
                .FirstOrDefaultAsync(od => od.OrderId == orderId && od.OrderDetailId == orderDetailId);

            if (orderDetail == null)
            {
                return NotFound(new { message = "Không tìm thấy chi tiết đơn hàng" });
            }

            var productType = orderDetail.Variant?.Product?.ProductType?.ToUpper() ?? "";

            // Nếu là PERSONAL_KEY
            if (productType == "PERSONAL_KEY" || productType == "KEY")
            {
                if (orderDetail.KeyId == null || orderDetail.Key == null)
                {
                    return NotFound(new { message = "Không tìm thấy mã kích hoạt cho sản phẩm này" });
                }

                // Log truy cập thành công KEY (bảo mật)
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "GetOrderDetailCredentials",
                    entityType: "OrderDetail",
                    entityId: orderDetailId.ToString(),
                    before: null,
                    after: new
                    {
                        orderId,
                        orderDetailId,
                        ProductType = "KEY",
                        ProductName = orderDetail.Variant?.Product?.ProductName ?? "",
                        orderDetail.KeyId
                    });

                return Ok(new
                {
                    productName = orderDetail.Variant?.Product?.ProductName ?? "",
                    productType = "KEY",
                    keyString = orderDetail.Key.KeyString
                });
            }

            // Nếu là PERSONAL_ACCOUNT
            if (productType == "PERSONAL_ACCOUNT" || productType == "ACCOUNT")
            {
                // Lấy order để lấy UserId
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null || !order.UserId.HasValue)
                {
                    return NotFound(new { message = "Không tìm thấy đơn hàng hoặc người dùng" });
                }

                // Lấy tất cả OrderDetails với cùng VariantId trong order này, sắp xếp theo OrderDetailId
                var orderDetailsWithSameVariant = await _context.OrderDetails
                    .Where(od => od.OrderId == orderId && od.VariantId == orderDetail.VariantId)
                    .OrderBy(od => od.OrderDetailId)
                    .ToListAsync();

                // Tìm index của OrderDetail hiện tại trong danh sách
                var detailIndex = orderDetailsWithSameVariant.FindIndex(od => od.OrderDetailId == orderDetail.OrderDetailId);
                if (detailIndex < 0)
                {
                    return NotFound(new { message = "Không tìm thấy chi tiết đơn hàng" });
                }

                // Lấy tất cả accounts được assign cho order này với cùng VariantId và UserId, sắp xếp theo AddedAt
                var accountCustomers = await _context.ProductAccountCustomers
                    .Include(pac => pac.ProductAccount)
                    .Where(pac =>
                        pac.OrderId == orderId &&
                        pac.UserId == order.UserId.Value &&
                        pac.ProductAccount.VariantId == orderDetail.VariantId &&
                        pac.IsActive)
                    .OrderBy(pac => pac.AddedAt)
                    .ToListAsync();

                if (accountCustomers == null || accountCustomers.Count == 0)
                {
                    return NotFound(new { message = "Không tìm thấy tài khoản cho sản phẩm này. Vui lòng liên hệ hỗ trợ." });
                }

                // Lấy account tương ứng với OrderDetail index
                // Nếu có ít accounts hơn OrderDetails, lấy account đầu tiên (chia sẻ account)
                var accountIndex = detailIndex < accountCustomers.Count ? detailIndex : 0;
                var accountCustomer = accountCustomers[accountIndex];

                if (accountCustomer.ProductAccount == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông tin tài khoản" });
                }

                var account = accountCustomer.ProductAccount;
                var decryptedPassword = await _productAccountService.GetDecryptedPasswordAsync(account.ProductAccountId);

                // Log truy cập thành công ACCOUNT (bảo mật)
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "GetOrderDetailCredentials",
                    entityType: "ProductAccount",
                    entityId: account.ProductAccountId.ToString(),
                    before: null,
                    after: new
                    {
                        orderId,
                        orderDetailId,
                        ProductType = "ACCOUNT",
                        ProductName = orderDetail.Variant?.Product?.ProductName ?? "",
                        AccountId = account.ProductAccountId,
                        account.AccountEmail,
                        account.AccountUsername
                        // KHÔNG log mật khẩu
                    });

                return Ok(new
                {
                    productName = orderDetail.Variant?.Product?.ProductName ?? "",
                    productType = "ACCOUNT",
                    accountEmail = account.AccountEmail,
                    accountUsername = account.AccountUsername,
                    accountPassword = decryptedPassword
                });
            }

            // Không log error cho type không hỗ trợ, chỉ trả 400
            return BadRequest(new { message = "Loại sản phẩm không được hỗ trợ" });
        }

        // ===== Helpers =====

        private static string FormatOrderNumber(Guid orderId, DateTime createdAt)
        {
            var dateStr = createdAt.ToString("yyyyMMdd");
            var orderIdStr = orderId.ToString().Replace("-", "").Substring(0, 4).ToUpperInvariant();
            return $"ORD-{dateStr}-{orderIdStr}";
        }

        private async Task<OrderDTO> MapToOrderDTOAsync(Order order)
        {
            try
            {
                var finalAmount = order.FinalAmount ?? (order.TotalAmount - order.DiscountAmount);

                // Tìm payment liên quan: Email khớp, TransactionType = ORDER_PAYMENT, Amount gần bằng FinalAmount, CreatedAt gần với Order.CreatedAt
                var relatedPayment = await _context.Payments
                    .AsNoTracking()
                    .Where(p =>
                        p.Email == order.Email &&
                        p.TransactionType == "ORDER_PAYMENT" &&
                        Math.Abs((decimal)(p.Amount - finalAmount)) < 0.01m) // Cho phép sai số nhỏ
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                // Nếu không tìm thấy payment với Amount khớp, thử tìm payment gần nhất theo thời gian (trong vòng 10 phút)
                if (relatedPayment == null)
                {
                    relatedPayment = await _context.Payments
                        .AsNoTracking()
                        .Where(p =>
                            p.Email == order.Email &&
                            p.TransactionType == "ORDER_PAYMENT" &&
                            Math.Abs((p.CreatedAt - order.CreatedAt).TotalMinutes) < 10) // Trong vòng 10 phút
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                var status = relatedPayment?.Status ?? "Cancelled"; // Mặc định là Cancelled nếu không tìm thấy payment

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
                    FinalAmount = order.FinalAmount,
                    Status = status,
                    CreatedAt = order.CreatedAt,
                    OrderDetails = order.OrderDetails?.Select(od => new OrderDetailDTO
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
                        KeyId = od.KeyId,
                        KeyString = od.Key?.KeyString,
                        SubTotal = od.Quantity * od.UnitPrice
                    }).ToList() ?? new List<OrderDetailDTO>()
                };
            }
            catch (Exception)
            {
                // Không dùng audit log ở đây, chỉ fallback status mặc định
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
                    FinalAmount = order.FinalAmount,
                    Status = "Cancelled", // Mặc định nếu có lỗi
                    CreatedAt = order.CreatedAt,
                    OrderDetails = order.OrderDetails?.Select(od => new OrderDetailDTO
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
                        KeyId = od.KeyId,
                        KeyString = od.Key?.KeyString,
                        SubTotal = od.Quantity * od.UnitPrice
                    }).ToList() ?? new List<OrderDetailDTO>()
                };
            }
        }
    }
}
