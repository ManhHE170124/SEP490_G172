using Keytietkiem.DTOs.Orders;
using Keytietkiem.DTOs;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;

        public OrdersController(
            KeytietkiemDbContext context)
        {
            _context = context;
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
                .Where(o => o.UserId == userId.Value)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .ToListAsync();

            var orderHistory = orders.Select(o => new OrderHistoryItemDTO
            {
                OrderId = o.OrderId,
                UserId = o.UserId,
                OrderNumber = FormatOrderNumber(o.OrderId, o.CreatedAt),
                Email = o.Email,
                TotalAmount = o.TotalAmount,
                FinalAmount = o.FinalAmount,
                CreatedAt = o.CreatedAt,
                ItemCount = o.OrderDetails?.Count ?? 0,
                ProductNames = o.OrderDetails?
                    .Select(od => od.Variant?.Product?.ProductName
                                  ?? od.Variant?.Title
                                  ?? string.Empty)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .ToList()
                    ?? new List<string>()
            }).ToList();

            return Ok(orderHistory);
        }

        /// <summary>
        /// Xem chi tiết 1 đơn (thông tin tổng + list OrderDetails)
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOrderById(Guid id)
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

            var orderDto = MapToOrderDTO(order);
            return Ok(orderDto);
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

            return Ok(orderDetails);
        }

        // ===== Helpers =====

        private static string FormatOrderNumber(Guid orderId, DateTime createdAt)
        {
            var dateStr = createdAt.ToString("yyyyMMdd");
            var orderIdStr = orderId.ToString().Replace("-", "").Substring(0, 4).ToUpperInvariant();
            return $"ORD-{dateStr}-{orderIdStr}";
        }

        private OrderDTO MapToOrderDTO(Order order)
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
                FinalAmount = order.FinalAmount,
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
