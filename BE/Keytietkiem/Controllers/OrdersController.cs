using Keytietkiem.DTOs.Orders;
using Keytietkiem.DTOs.Payments; // 👈 thêm
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

        // ========== CÁC API ĐANG CÓ – GIỮ NGUYÊN ==========

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
                .ToListAsync();

            if (variants.Count != variantIds.Count)
            {
                return BadRequest(new { message = "Một số gói sản phẩm (variant) không tồn tại" });
            }

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

            foreach (var detail in createOrderDto.OrderDetails)
            {
                if (detail.Quantity <= 0)
                {
                    return BadRequest(new { message = "Số lượng phải lớn hơn 0" });
                }
            }

            var calculatedFinal = createOrderDto.OrderDetails.Sum(od => od.Quantity * od.UnitPrice);
            var expectedFinal = createOrderDto.TotalAmount - createOrderDto.DiscountAmount;

            if (Math.Abs(calculatedFinal - expectedFinal) > 0.01m)
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

            var rawStatus = string.IsNullOrWhiteSpace(createOrderDto.Status)
                ? "Pending"
                : createOrderDto.Status.Trim();

            if (!AllowedStatuses.Contains(rawStatus))
            {
                return BadRequest(new { message = "Trạng thái đơn hàng không hợp lệ" });
            }

            var newOrder = new Order
            {
                UserId = user?.UserId,
                Email = orderEmail,
                TotalAmount = createOrderDto.TotalAmount,        // tổng gốc
                DiscountAmount = createOrderDto.DiscountAmount,  // giảm giá
                FinalAmount = expectedFinal,                     // giá cuối = gốc - giảm
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };


            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync();

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

            // ===== Load user nếu có =====
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

            // ===== Validate variants + tồn kho =====
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

            // ===== Check tổng tiền: FINAL (sau giảm) =====
            // calculatedFinal = Σ quantity * unitPrice (giá SELL)
            var calculatedFinal = createOrderDto.OrderDetails
                .Sum(od => od.Quantity * od.UnitPrice);

            // expectedFinal = TotalAmount (gốc) - DiscountAmount
            var expectedFinal = createOrderDto.TotalAmount - createOrderDto.DiscountAmount;

            if (Math.Abs(calculatedFinal - expectedFinal) > 0.01m)
            {
                return BadRequest(new { message = "Tổng tiền không khớp với chi tiết đơn hàng" });
            }

            // ===== Chuẩn hoá email đơn hàng =====
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

            // ===== Tạo Order =====
            var newOrder = new Order
            {
                UserId = user?.UserId,
                Email = orderEmail,

                // Tổng gốc, giảm giá, thành tiền sau giảm
                TotalAmount = createOrderDto.TotalAmount,
                DiscountAmount = createOrderDto.DiscountAmount,
                FinalAmount = expectedFinal,

                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync();

            // ===== Tạo OrderDetails =====
            foreach (var detailDto in createOrderDto.OrderDetails)
            {
                var orderDetail = new OrderDetail
                {
                    OrderId = newOrder.OrderId,
                    VariantId = detailDto.VariantId,
                    Quantity = detailDto.Quantity,
                    UnitPrice = detailDto.UnitPrice,
                    KeyId = null   // luồng checkout: chưa gắn key
                };

                _context.OrderDetails.Add(orderDetail);
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { orderId = newOrder.OrderId });
        }


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

            if (order.Payments != null)
            {
                foreach (var p in order.Payments.Where(p => p.Status == "Pending"))
                {
                    p.Status = "Cancelled";
                }
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return NoContent();
        }

        // 🔧 SỬA LOGIC UPDATE TRẠNG THÁI ĐƠN – CHỈ CHO PHÉP
        // Pending/Failed -> Paid/Cancelled, không sửa khi đã Paid/Cancelled
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

            var currentStatus = existing.Status ?? "Pending";

            // Nếu đã Paid hoặc Cancelled thì không cho chỉnh nữa
            if (currentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase) ||
                currentStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Không thể cập nhật trạng thái khi đơn đã ở trạng thái Paid hoặc Cancelled." });
            }

            // Chỉ cho chỉnh tay khi đang Pending hoặc Failed
            if (!currentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                !currentStatus.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ được phép chỉnh tay trạng thái đơn khi đang ở Pending hoặc Failed." });
            }

            // Chỉ được chuyển sang Paid hoặc Cancelled
            if (!normalizedStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase) &&
                !normalizedStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ được phép chuyển trạng thái đơn sang Paid hoặc Cancelled." });
            }

            existing.Status = normalizedStatus;

            if (updateOrderDto.DiscountAmount.HasValue)
            {
                existing.DiscountAmount = updateOrderDto.DiscountAmount.Value;
                // Có thể cập nhật luôn FinalAmount = TotalAmount - DiscountAmount nếu muốn
                existing.FinalAmount = existing.TotalAmount - existing.DiscountAmount;
            }

            _context.Orders.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

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

            if (payments.Any(p => p.Status == "Refunded"))
            {
                return "Refunded";
            }

            var totalPaid = payments
                .Where(p =>
                    p.Status == "Paid" ||
                    p.Status == "Success" ||
                    p.Status == "Completed")
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
                    OrderId = p.OrderId,
                    Amount = p.Amount,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    Provider = p.Provider,
                    ProviderOrderCode = p.ProviderOrderCode
                }).ToList() ?? new List<PaymentDTO>(),
                PaymentStatus = ComputePaymentStatus(
                    order.Payments,
                    order.FinalAmount ?? (order.TotalAmount - order.DiscountAmount)
                )
            };
        }
    }
}
