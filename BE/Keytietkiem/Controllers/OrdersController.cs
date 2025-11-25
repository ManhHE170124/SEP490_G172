/**
 * File: OrdersController.cs
 * Flow mới (sau khi tách Payment):
 *
 * - POST /api/orders/checkout
 *      + Validate dữ liệu giỏ hàng (variants, số lượng, tồn kho, tổng tiền)
 *      + Tạo Order (Status = "Pending")
 *      + Tạo OrderDetails, trừ kho (Variant.StockQty -= Quantity)
 *      + Tính FinalAmount = TotalAmount - DiscountAmount
 *      + Trả về { orderId }
 *      -> FE (hoặc backend khác) sẽ gọi tiếp /api/payments/payos/create để tạo Payment + PayOS checkoutUrl
 *
 * - POST /api/orders/{id}/cancel
 *      + Chỉ cho phép khi Order.Status == "Pending"
 *      + Đổi Status -> "Cancelled"
 *      + Trả lại số lượng về kho (Variant.StockQty += Quantity)
 *      + Các Payment Pending -> Failed
 *
 * - Các API khác:
 *      + GET  /api/orders                : danh sách đơn (admin)
 *      + GET  /api/orders/history        : lịch sử đơn của user
 *      + GET  /api/orders/{id}           : xem chi tiết đơn
 *      + GET  /api/orders/{id}/details   : xem line items
 *      + POST /api/orders                : tạo đơn (luồng admin / back-office)
 *      + PUT  /api/orders/{id}           : cập nhật trạng thái đơn
 *      + DELETE /api/orders/{id}         : xoá đơn (nếu chưa có payment)
 */

using Keytietkiem.DTOs.Orders;
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

        // CHỈ những trạng thái mà DB cho phép
        private static readonly string[] AllowedStatuses = new[]
        {
            "Pending", "Paid", "Failed", "Cancelled"
        };

        public OrdersController(KeytietkiemDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// List all orders (admin).
        /// GET /api/orders
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .Include(o => o.Payments)
                .ToListAsync();

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
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                ItemCount = o.OrderDetails?.Count ?? 0,
                PaymentStatus = ComputePaymentStatus(
                    o.Payments,
                    o.FinalAmount ?? (o.TotalAmount - o.DiscountAmount)
                )
            }).ToList();

            return Ok(orderList);
        }

        /// <summary>
        /// Get order history for a user.
        /// GET /api/orders/history?userId={userId}
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
                .Include(o => o.Payments)
                .ToListAsync();

            var orderHistory = orders.Select(o => new OrderHistoryItemDTO
            {
                OrderId = o.OrderId,
                UserId = o.UserId,
                OrderNumber = FormatOrderNumber(o.OrderId, o.CreatedAt),
                Email = o.Email,
                TotalAmount = o.TotalAmount,
                FinalAmount = o.FinalAmount,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                ItemCount = o.OrderDetails?.Count ?? 0,
                ProductNames = o.OrderDetails?
                    .Select(od => od.Variant?.Product?.ProductName
                                  ?? od.Variant?.Title
                                  ?? string.Empty)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .ToList()
                    ?? new List<string>(),
                PaymentStatus = ComputePaymentStatus(
                    o.Payments,
                    o.FinalAmount ?? (o.TotalAmount - o.DiscountAmount)
                )
            }).ToList();

            return Ok(orderHistory);
        }

        /// <summary>
        /// Get single order by id.
        /// GET /api/orders/{id}
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
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });
            }

            var orderDto = MapToOrderDTO(order);
            return Ok(orderDto);
        }

        /// <summary>
        /// Create new order (admin / back-office).
        /// KHÔNG dùng cho luồng checkout thanh toán online.
        /// FE nên dùng /api/orders/checkout.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDTO createOrderDto)
        {
            if (createOrderDto == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ" });
            }

            if (string.IsNullOrWhiteSpace(createOrderDto.Email))
            {
                return BadRequest(new { message = "Email không được để trống" });
            }

            if (createOrderDto.OrderDetails == null || !createOrderDto.OrderDetails.Any())
            {
                return BadRequest(new { message = "Danh sách sản phẩm không được để trống" });
            }

            // Nếu có UserId thì validate user, nếu không thì cho phép null (guest)
            User? user = null;
            if (createOrderDto.UserId.HasValue && createOrderDto.UserId.Value != Guid.Empty)
            {
                user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserId == createOrderDto.UserId.Value);

                if (user == null)
                {
                    return NotFound(new { message = "Người dùng không tồn tại" });
                }
            }

            // Validate Variants tồn tại
            var variantIds = createOrderDto.OrderDetails
                .Select(od => od.VariantId)
                .Distinct()
                .ToList();

            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => variantIds.Contains(v.VariantId))
                .ToListAsync();

            if (variants.Count != variantIds.Count)
            {
                return BadRequest(new { message = "Một số gói sản phẩm (variant) không tồn tại" });
            }

            // Validate Keys nếu có (luồng admin)
            var keyIds = createOrderDto.OrderDetails
                .Where(od => od.KeyId.HasValue)
                .Select(od => od.KeyId!.Value)
                .Distinct()
                .ToList();

            if (keyIds.Any())
            {
                var keys = await _context.ProductKeys
                    .Where(k => keyIds.Contains(k.KeyId))
                    .ToListAsync();

                if (keys.Count != keyIds.Count)
                {
                    return BadRequest(new { message = "Key sản phẩm không tồn tại" });
                }

                var unavailableKeys = keys.Where(k => k.Status != "Available").ToList();
                if (unavailableKeys.Any())
                {
                    return BadRequest(new { message = "Một số key sản phẩm không khả dụng" });
                }
            }

            // Validate số lượng
            foreach (var detail in createOrderDto.OrderDetails)
            {
                if (detail.Quantity <= 0)
                {
                    return BadRequest(new { message = "Số lượng phải lớn hơn 0" });
                }
            }

            // Check tổng tiền theo DTO (luồng admin)
            var calculatedTotal = createOrderDto.OrderDetails.Sum(od => od.Quantity * od.UnitPrice);
            if (Math.Abs(calculatedTotal - createOrderDto.TotalAmount) > 0.01m)
            {
                return BadRequest(new { message = "Tổng tiền không khớp với chi tiết đơn hàng" });
            }

            // Chuẩn hoá email lưu vào order
            var orderEmail = createOrderDto.Email.Trim();
            if (string.IsNullOrWhiteSpace(orderEmail) && user != null)
            {
                orderEmail = user.Email ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(orderEmail))
            {
                return BadRequest(new { message = "Email đơn hàng không hợp lệ" });
            }

            // Chuẩn hoá & kiểm tra Status theo AllowedStatuses
            var rawStatus = string.IsNullOrWhiteSpace(createOrderDto.Status)
                ? "Pending"
                : createOrderDto.Status.Trim();

            if (!AllowedStatuses.Contains(rawStatus))
            {
                return BadRequest(new { message = "Trạng thái đơn hàng không hợp lệ" });
            }

            var newOrder = new Order
            {
                UserId = user?.UserId, // có thể null
                Email = orderEmail,
                TotalAmount = createOrderDto.TotalAmount,
                DiscountAmount = createOrderDto.DiscountAmount,
                Status = rawStatus,
                CreatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync();

            // Tạo OrderDetails + update ProductKey nếu có
            foreach (var detailDto in createOrderDto.OrderDetails)
            {
                var orderDetail = new OrderDetail
                {
                    OrderId = newOrder.OrderId,
                    VariantId = detailDto.VariantId,
                    Quantity = detailDto.Quantity,
                    UnitPrice = detailDto.UnitPrice,
                    KeyId = detailDto.KeyId
                };

                _context.OrderDetails.Add(orderDetail);

                if (detailDto.KeyId.HasValue)
                {
                    var productKey = await _context.ProductKeys
                        .FirstOrDefaultAsync(k => k.KeyId == detailDto.KeyId.Value);
                    if (productKey != null)
                    {
                        productKey.Status = "Sold";
                        productKey.AssignedToOrderId = newOrder.OrderId;
                        productKey.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Reload order đầy đủ
            var createdOrder = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Key)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == newOrder.OrderId);

            var orderDto = MapToOrderDTO(createdOrder!);

            return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder!.OrderId }, orderDto);
        }

        /// <summary>
        /// LUỒNG CHÍNH STORE FRONT:
        /// Tạo order Pending và trả về OrderId.
        /// Payment + PayOS sẽ do PaymentsController xử lý.
        /// POST /api/orders/checkout
        /// </summary>
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CreateOrderDTO createOrderDto)
        {
            if (createOrderDto == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ" });
            }

            if (string.IsNullOrWhiteSpace(createOrderDto.Email))
            {
                return BadRequest(new { message = "Email không được để trống" });
            }

            if (createOrderDto.OrderDetails == null || !createOrderDto.OrderDetails.Any())
            {
                return BadRequest(new { message = "Giỏ hàng trống" });
            }

            // Nếu có UserId thì validate user (nhưng cho phép guest)
            User? user = null;
            if (createOrderDto.UserId.HasValue && createOrderDto.UserId.Value != Guid.Empty)
            {
                user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserId == createOrderDto.UserId.Value);

                if (user == null)
                {
                    return NotFound(new { message = "Người dùng không tồn tại" });
                }
            }

            var variantIds = createOrderDto.OrderDetails
                .Select(od => od.VariantId)
                .Distinct()
                .ToList();

            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => variantIds.Contains(v.VariantId))
                .ToDictionaryAsync(v => v.VariantId, v => v);

            if (variants.Count != variantIds.Count)
            {
                return BadRequest(new { message = "Một số gói sản phẩm (variant) không tồn tại" });
            }

            // Check số lượng + tồn kho hiện tại
            foreach (var detail in createOrderDto.OrderDetails)
            {
                if (detail.Quantity <= 0)
                {
                    return BadRequest(new { message = "Số lượng phải lớn hơn 0" });
                }

                if (!variants.TryGetValue(detail.VariantId, out var variant))
                {
                    return BadRequest(new { message = "Gói sản phẩm không tồn tại" });
                }

                if (variant.StockQty < detail.Quantity)
                {
                    return BadRequest(new
                    {
                        message = $"Sản phẩm '{variant.Title}' không đủ tồn kho. Còn lại {variant.StockQty}."
                    });
                }
            }

            // Tính lại tổng tiền dựa trên UnitPrice gửi lên
            var calculatedTotal = createOrderDto.OrderDetails.Sum(od => od.Quantity * od.UnitPrice);
            if (Math.Abs(calculatedTotal - createOrderDto.TotalAmount) > 0.01m)
            {
                return BadRequest(new { message = "Tổng tiền không khớp với chi tiết đơn hàng" });
            }

            var orderEmail = createOrderDto.Email.Trim();
            if (string.IsNullOrWhiteSpace(orderEmail) && user != null)
            {
                orderEmail = user.Email ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(orderEmail))
            {
                return BadRequest(new { message = "Email đơn hàng không hợp lệ" });
            }

            using var tx = await _context.Database.BeginTransactionAsync();

            // Tạo Order Pending
            var newOrder = new Order
            {
                UserId = user?.UserId,
                Email = orderEmail,
                TotalAmount = createOrderDto.TotalAmount,
                DiscountAmount = createOrderDto.DiscountAmount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync();

            // Tạo OrderDetails, trừ kho (reserve)
            foreach (var detailDto in createOrderDto.OrderDetails)
            {
                var variant = variants[detailDto.VariantId];

                var orderDetail = new OrderDetail
                {
                    OrderId = newOrder.OrderId,
                    VariantId = detailDto.VariantId,
                    Quantity = detailDto.Quantity,
                    UnitPrice = detailDto.UnitPrice,
                    KeyId = null // chưa gắn key
                };

                _context.OrderDetails.Add(orderDetail);

                // Trừ kho khi bắt đầu checkout
                variant.StockQty -= detailDto.Quantity;
            }

            // FinalAmount = Total - Discount
            newOrder.FinalAmount = newOrder.TotalAmount - newOrder.DiscountAmount;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            // Checkout giờ chỉ trả về OrderId, phần tạo payment do PaymentsController xử lý
            return Ok(new { orderId = newOrder.OrderId });
        }

        /// <summary>
        /// Hủy đơn (user confirm khi quay lại / hủy thanh toán).
        /// POST /api/orders/{id}/cancel
        /// </summary>
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> CancelOrder(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });
            }

            if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ có thể hủy đơn ở trạng thái Pending" });
            }

            using var tx = await _context.Database.BeginTransactionAsync();

            order.Status = "Cancelled";

            // Trả hàng về kho
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

            // Các payment Pending -> Failed
            if (order.Payments != null)
            {
                foreach (var p in order.Payments
                             .Where(p => p.Status == "Pending"))
                {
                    p.Status = "Failed";
                }
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return NoContent();
        }

        /// <summary>
        /// Update order.
        /// PUT /api/orders/{id}
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateOrder(Guid id, [FromBody] UpdateOrderDTO updateOrderDto)
        {
            if (updateOrderDto == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ" });
            }

            if (string.IsNullOrWhiteSpace(updateOrderDto.Status))
            {
                return BadRequest(new { message = "Trạng thái đơn hàng không hợp lệ" });
            }

            var existing = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (existing == null)
            {
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });
            }

            var normalizedStatus = updateOrderDto.Status.Trim();
            if (!AllowedStatuses.Contains(normalizedStatus))
            {
                return BadRequest(new { message = "Trạng thái đơn hàng không hợp lệ" });
            }

            existing.Status = normalizedStatus;
            if (updateOrderDto.DiscountAmount.HasValue)
            {
                existing.DiscountAmount = updateOrderDto.DiscountAmount.Value;
            }

            _context.Orders.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Delete order.
        /// DELETE /api/orders/{id}
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            var existingOrder = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (existingOrder == null)
            {
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });
            }

            if (existingOrder.Payments != null && existingOrder.Payments.Any())
            {
                return BadRequest(new { message = "Không thể xóa đơn hàng đã thanh toán" });
            }

            _context.Orders.Remove(existingOrder);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Get order details (lines).
        /// GET /api/orders/{id}/details
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

        private static string ComputePaymentStatus(ICollection<Payment>? payments, decimal finalAmount)
        {
            if (payments == null || !payments.Any())
            {
                return "Unpaid";
            }

            // Phòng khi sau này có status Refund
            if (payments.Any(p => p.Status == "Refunded"))
            {
                return "Refunded";
            }

            // Status trong bảng Payment: Pending / Success / Failed / Completed ...
            var totalPaid = payments
                .Where(p => p.Status == "Success" || p.Status == "Completed")
                .Sum(p => p.Amount);

            if (totalPaid >= finalAmount)
            {
                return "Paid";
            }

            if (totalPaid > 0)
            {
                return "Partial";
            }

            return "Unpaid";
        }

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
                Status = order.Status,
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
                }).ToList() ?? new List<OrderDetailDTO>(),
                Payments = order.Payments?.Select(p => new PaymentDTO
                {
                    PaymentId = p.PaymentId,
                    Amount = p.Amount,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt
                }).ToList() ?? new List<PaymentDTO>(),
                PaymentStatus = ComputePaymentStatus(
                    order.Payments,
                    order.FinalAmount ?? (order.TotalAmount - order.DiscountAmount)
                )
            };
        }
    }
}
