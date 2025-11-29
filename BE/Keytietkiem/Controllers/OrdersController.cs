using Keytietkiem.DTOs.Orders;
using Keytietkiem.DTOs.Payments;
using Keytietkiem.DTOs.Products; // 👈 thêm
using Keytietkiem.DTOs;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IProductKeyService _productKeyService;
        private readonly IProductAccountService _productAccountService;
        private readonly IEmailService _emailService;
        private readonly ILogger<OrdersController> _logger;

        // CHỈ những trạng thái mà DB cho phép
        private static readonly string[] AllowedStatuses = new[]
        {
            "Pending", "Paid", "Failed", "Cancelled"
        };

        public OrdersController(
            KeytietkiemDbContext context,
            IProductKeyService productKeyService,
            IProductAccountService productAccountService,
            IEmailService emailService,
            ILogger<OrdersController> logger)
        {
            _context = context;
            _productKeyService = productKeyService;
            _productAccountService = productAccountService;
            _emailService = emailService;
            _logger = logger;
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

            var availableVariants = new List<ProductVariant>();
            
            foreach (var detail in createOrderDto.OrderDetails)
            {
                if (detail.Quantity <= 0)
                {
                    return BadRequest(new { message = "Số lượng phải lớn hơn 0" });
                }
                var variant = variants.FirstOrDefault(x=> x.VariantId == detail.VariantId);
                if (variant.StockQty < detail.Quantity)
                {
                    return BadRequest(new
                        { message = $"Sản phẩn {variant.Product.ProductName} - {variant.Title} không đủ số lượng" });
                }
                
                availableVariants.Add(variant);
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
                .Include(x=>x.OrderDetails)
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
            
            //Xu ly gui mail + map pk/pa
            if (normalizedStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                var variantIds = existing.OrderDetails.Select(x=> x.VariantId).Distinct();
                var variants = await _context.ProductVariants
                    .AsNoTracking()
                    .Include(v => v.Product)
                    .Where(x => variantIds.Contains(x.VariantId))
                    .ToListAsync();

                await ProcessVariants(variants, existing);
            }
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

        private async Task ProcessVariants(List<ProductVariant> variants, Order order)
        {
            var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var userEmail = order.Email;

            // Get the user ID from the order
            if (!order.UserId.HasValue)
            {
                _logger.LogWarning("Order {OrderId} does not have a UserId, cannot assign products", order.OrderId);
                return;
            }

            var userId = order.UserId.Value;

            // Collect all products to send in a single email
            var orderProducts = new List<OrderProductEmailDto>();

            // Process PERSONAL_KEY type variants
            var personalKeyVariants = variants.Where(x => x.Product.ProductType == ProductEnums.PERSONAL_KEY).ToList();
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
                    _logger.LogWarning("Not enough available keys for variant {VariantId}. Required: {Quantity}, Available: {Available}",
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
                        _logger.LogError(ex, "Failed to assign key {KeyId} to order {OrderId}", key.KeyId, order.OrderId);
                    }
                }
            }

            // Process PERSONAL_ACCOUNT type variants
            var personalAccountVariants = variants.Where(x => x.Product.ProductType == ProductEnums.PERSONAL_ACCOUNT).ToList();
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
                    _logger.LogWarning("Not enough available personal accounts for variant {VariantId}. Required: {Quantity}, Available: {Available}",
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
                        var decryptedPassword = await _productAccountService.GetDecryptedPasswordAsync(account.ProductAccountId);

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
                        _logger.LogError(ex, "Failed to assign account {AccountId} to order {OrderId}", account.ProductAccountId, order.OrderId);
                    }
                }
            }

            // Send a single consolidated email with all products
            if (orderProducts.Any())
            {
                try
                {
                    await _emailService.SendOrderProductsEmailAsync(userEmail, orderProducts);
                    _logger.LogInformation("Sent consolidated order email with {Count} products to {Email}",
                        orderProducts.Count, userEmail);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send consolidated order email to {Email}", userEmail);
                }
            }
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
