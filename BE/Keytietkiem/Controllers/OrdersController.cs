/**
 * File: OrdersController.cs
 * Created: 2025-01-15
 * Purpose: Manage orders (CRUD). Handles order creation, updates, and retrieval
 *          with proper relationships to users, products, keys, and payments.
 * Endpoints:
 *   - GET    /api/orders              : List all orders (admin)
 *   - GET    /api/orders/history      : Get order history for current user
 *   - GET    /api/orders/{id}         : Get order by id
 *   - POST   /api/orders              : Create an order
 *   - PUT    /api/orders/{id}         : Update an order
 *   - DELETE /api/orders/{id}         : Delete an order
 *   - GET    /api/orders/{id}/details : Get order details
 *   - GET    /api/orders/{id}/payments: Get payments for an order
 */

using Microsoft.AspNetCore.Mvc;
using Keytietkiem.Models;
using Keytietkiem.DTOs.Orders;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;

        public OrdersController(KeytietkiemDbContext context)
        {
            _context = context;
        }

        /**
         * Summary: Retrieve all orders (Admin - Order Management).
         * Route: GET /api/orders
         * Returns: 200 OK with list of orders (filtering and sorting done in FE)
         */
        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.Payments)
                .ToListAsync();

            var orderList = orders.Select(o => new OrderListItemDTO
            {
                OrderId = o.OrderId,
                UserId = o.UserId,
                UserName = o.User != null ? (o.User.FullName ?? $"{o.User.FirstName} {o.User.LastName}".Trim()) : null,
                UserEmail = o.User?.Email,
                TotalAmount = o.TotalAmount,
                FinalAmount = o.FinalAmount,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                ItemCount = o.OrderDetails?.Count ?? 0,
                PaymentStatus = ComputePaymentStatus(o.Payments, o.FinalAmount ?? o.TotalAmount - o.DiscountAmount)
            }).ToList();

            return Ok(orderList);
        }

        /**
         * Summary: Get order history for current user.
         * Route: GET /api/orders/history?userId={userId}
         * Params: userId (Guid) - User identifier (optional, can be from auth context in future)
         * Returns: 200 OK with list of orders for the specified user
         */
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
                    .ThenInclude(od => od.Product)
                .Include(o => o.Payments)
                .ToListAsync();

            var orderHistory = orders.Select(o =>
            {
                var firstProduct = o.OrderDetails?.FirstOrDefault()?.Product;
                return new OrderHistoryItemDTO
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    OrderNumber = FormatOrderNumber(o.OrderId, o.CreatedAt),
                    TotalAmount = o.TotalAmount,
                    FinalAmount = o.FinalAmount,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    ItemCount = o.OrderDetails?.Count ?? 0,
                    ProductNames = o.OrderDetails?.Select(od => od.Product?.ProductName ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
                    ThumbnailUrl = firstProduct?.ThumbnailUrl,
                    PaymentStatus = ComputePaymentStatus(o.Payments, o.FinalAmount ?? o.TotalAmount - o.DiscountAmount)
                };
            }).ToList();

            return Ok(orderHistory);
        }

        /**
         * Summary: Retrieve an order by id.
         * Route: GET /api/orders/{id}
         * Params: id (Guid) - order identifier
         * Returns: 200 OK with order, 404 if not found
         */
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderById(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Key)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });
            }

            var orderDto = new OrderDTO
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                UserName = order.User != null ? (order.User.FullName ?? $"{order.User.FirstName} {order.User.LastName}".Trim()) : null,
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
                    ProductId = od.ProductId,
                    ProductName = od.Product?.ProductName ?? "",
                    ProductCode = od.Product?.ProductCode,
                    ProductType = od.Product?.ProductType,
                    ThumbnailUrl = od.Product?.ThumbnailUrl,
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
                PaymentStatus = ComputePaymentStatus(order.Payments, order.FinalAmount ?? order.TotalAmount - order.DiscountAmount)
            };

            return Ok(orderDto);
        }

        /**
         * Summary: Create a new order.
         * Route: POST /api/orders
         * Body: CreateOrderDTO
         * Returns: 201 Created with created order, 400/404 on validation errors
         */
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDTO createOrderDto)
        {
            if (createOrderDto == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ" });
            }

            if (createOrderDto.OrderDetails == null || !createOrderDto.OrderDetails.Any())
            {
                return BadRequest(new { message = "Danh sách sản phẩm không được để trống" });
            }

            // Validate User exists
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == createOrderDto.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Người dùng không tồn tại" });
            }

            // Validate Products exist
            var productIds = createOrderDto.OrderDetails.Select(od => od.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                return BadRequest(new { message = "Sản phẩm không tồn tại" });
            }

            // Validate Keys exist (if provided)
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

                // Validate keys are available
                var unavailableKeys = keys.Where(k => k.Status != "Available").ToList();
                if (unavailableKeys.Any())
                {
                    return BadRequest(new { message = "Một số key sản phẩm không khả dụng" });
                }
            }

            // Validate quantities
            foreach (var detail in createOrderDto.OrderDetails)
            {
                if (detail.Quantity <= 0)
                {
                    return BadRequest(new { message = "Số lượng phải lớn hơn 0" });
                }
            }

            // Calculate total from details
            var calculatedTotal = createOrderDto.OrderDetails.Sum(od => od.Quantity * od.UnitPrice);
            if (Math.Abs(calculatedTotal - createOrderDto.TotalAmount) > 0.01m)
            {
                return BadRequest(new { message = "Tổng tiền không khớp với chi tiết đơn hàng" });
            }

            // Create order
            var newOrder = new Order
            {
                UserId = createOrderDto.UserId,
                TotalAmount = createOrderDto.TotalAmount,
                DiscountAmount = createOrderDto.DiscountAmount,
                Status = createOrderDto.Status ?? "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync();

            // Create order details and update key status
            foreach (var detailDto in createOrderDto.OrderDetails)
            {
                var orderDetail = new OrderDetail
                {
                    OrderId = newOrder.OrderId,
                    ProductId = detailDto.ProductId,
                    Quantity = detailDto.Quantity,
                    UnitPrice = detailDto.UnitPrice,
                    KeyId = detailDto.KeyId
                };

                _context.OrderDetails.Add(orderDetail);

                // Update ProductKey status if KeyId is provided
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

            // Reload order with relations
            var createdOrder = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Key)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == newOrder.OrderId);

            var orderDto = MapToOrderDTO(createdOrder!);

            return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder!.OrderId }, orderDto);
        }

        /**
         * Summary: Update an existing order by id.
         * Route: PUT /api/orders/{id}
         * Params: id (Guid)
         * Body: UpdateOrderDTO
         * Returns: 204 No Content, 400/404 on errors
         */
        [HttpPut("{id}")]
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

            // Validate status
            var validStatuses = new[] { "Pending", "Processing", "Completed", "Cancelled", "Refunded" };
            if (!validStatuses.Contains(updateOrderDto.Status))
            {
                return BadRequest(new { message = "Trạng thái đơn hàng không hợp lệ" });
            }

            existing.Status = updateOrderDto.Status;
            if (updateOrderDto.DiscountAmount.HasValue)
            {
                existing.DiscountAmount = updateOrderDto.DiscountAmount.Value;
            }

            _context.Orders.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /**
         * Summary: Delete an order by id.
         * Route: DELETE /api/orders/{id}
         * Params: id (Guid)
         * Returns: 204 No Content, 404 if not found
         */
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            var existingOrder = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (existingOrder == null)
            {
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });
            }

            // Check if order has payments
            if (existingOrder.Payments != null && existingOrder.Payments.Any())
            {
                return BadRequest(new { message = "Không thể xóa đơn hàng đã thanh toán" });
            }

            _context.Orders.Remove(existingOrder);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /**
         * Summary: Get order details for an order.
         * Route: GET /api/orders/{id}/details
         * Params: id (Guid)
         * Returns: 200 OK with list of order details
         */
        [HttpGet("{id}/details")]
        public async Task<IActionResult> GetOrderDetails(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
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
                ProductId = od.ProductId,
                ProductName = od.Product?.ProductName ?? "",
                ProductCode = od.Product?.ProductCode,
                ProductType = od.Product?.ProductType,
                ThumbnailUrl = od.Product?.ThumbnailUrl,
                Quantity = od.Quantity,
                UnitPrice = od.UnitPrice,
                KeyId = od.KeyId,
                KeyString = od.Key?.KeyString,
                SubTotal = od.Quantity * od.UnitPrice
            }).ToList() ?? new List<OrderDetailDTO>();

            return Ok(orderDetails);
        }

        /**
         * Summary: Get payments for an order.
         * Route: GET /api/orders/{id}/payments
         * Params: id (Guid)
         * Returns: 200 OK with list of payments
         */
        [HttpGet("{id}/payments")]
        public async Task<IActionResult> GetOrderPayments(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(new { message = "Đơn hàng không được tìm thấy" });
            }

            var payments = order.Payments?.Select(p => new PaymentDTO
            {
                PaymentId = p.PaymentId,
                Amount = p.Amount,
                Status = p.Status,
                CreatedAt = p.CreatedAt
            }).ToList() ?? new List<PaymentDTO>();

            return Ok(payments);
        }

        // Helper methods
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
                .Where(p => p.Status == "Completed")
                .Sum(p => p.Amount);

            if (totalPaid >= finalAmount)
            {
                return "Paid";
            }
            else if (totalPaid > 0)
            {
                return "Partial";
            }

            return "Unpaid";
        }

        private static string FormatOrderNumber(Guid orderId, DateTime createdAt)
        {
            var dateStr = createdAt.ToString("yyyyMMdd");
            var orderIdStr = orderId.ToString().Replace("-", "").Substring(0, 4).ToUpper();
            return $"ORD-{dateStr}-{orderIdStr}";
        }

        private OrderDTO MapToOrderDTO(Order order)
        {
            return new OrderDTO
            {
                OrderId = order.OrderId,
                UserId = order.UserId,
                UserName = order.User != null ? (order.User.FullName ?? $"{order.User.FirstName} {order.User.LastName}".Trim()) : null,
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
                    ProductId = od.ProductId,
                    ProductName = od.Product?.ProductName ?? "",
                    ProductCode = od.Product?.ProductCode,
                    ProductType = od.Product?.ProductType,
                    ThumbnailUrl = od.Product?.ThumbnailUrl,
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
                PaymentStatus = ComputePaymentStatus(order.Payments, order.FinalAmount ?? order.TotalAmount - order.DiscountAmount)
            };
        }
    }
}
