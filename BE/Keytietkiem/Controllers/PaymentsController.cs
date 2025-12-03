// Keytietkiem/Controllers/PaymentsController.cs
using Keytietkiem.DTOs.Cart;
using Keytietkiem.DTOs.Payments;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
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

        private static readonly string[] AllowedPaymentStatuses = new[]
        {
            "Pending", "Paid", "Success", "Completed", "Cancelled", "Failed", "Refunded"
        };

        public PaymentsController(
            KeytietkiemDbContext context,
            PayOSService payOs,
            IConfiguration config,
            IMemoryCache cache)
        {
            _context = context;
            _payOs = payOs;
            _config = config;
            _cache = cache;
        }

        private Guid? GetCurrentUserIdOrNull()
        {
            var claim = User.FindFirst("uid") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;

            return Guid.TryParse(claim.Value, out var id) ? id : (Guid?)null;
        }

        // ===== API ADMIN: list payment với filter cơ bản =====
        // GET /api/payments?status=Paid&provider=PayOS&email=...&transactionType=ORDER_CART
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

        // ===== Webhook PayOS =====
        // POST /api/payments/payos/webhook
        // - ORDER_CART: dùng snapshot cart trong cache → Paid => tạo Order, Cancelled => hoàn kho.
        // - SUPPORT_PLAN / các loại khác: chỉ cập nhật Payment.Status.
        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookModel payload)
        {
            if (payload == null || payload.Data == null)
                return BadRequest();

            var data = payload.Data;

            var topCode = (payload.Code ?? "").Trim();
            var dataCode = (data.Code ?? "").Trim();

            // TÌM PAYMENT THEO ProviderOrderCode
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p =>
                    p.Provider == "PayOS" &&
                    p.ProviderOrderCode == data.OrderCode
                );

            if (payment == null)
            {
                // Không có payment nào khớp orderCode -> bỏ qua
                return Ok();
            }

            // ===== 1. Payment tạo từ Cart (ORDER_CART) =====
            if (string.Equals(payment.TransactionType, "ORDER_CART", StringComparison.OrdinalIgnoreCase))
            {
                await HandleCartPaymentWebhook(payment, topCode, dataCode, data.Amount);
                return Ok();
            }

            // ===== 2. Các loại payment khác: SUPPORT_PLAN, ... =====
            var isSuccess = topCode == "00" && dataCode == "00";

            if (isSuccess)
            {
                var amountDecimal = (decimal)data.Amount;
                payment.Status = "Paid";
                payment.Amount = amountDecimal;
            }
            else if (payment.Status == "Pending")
            {
                payment.Status = "Cancelled";
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

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
        /// Xử lý webhook cho Payment TransactionType = ORDER_CART:
        /// - Success (00/00): Payment = Paid, tạo Order + OrderDetails từ snapshot, xoá snapshot.
        /// - Ngược lại: Payment = Cancelled, hoàn kho theo snapshot, xoá snapshot.
        /// </summary>
        private async Task HandleCartPaymentWebhook(
            Payment payment,
            string topCode,
            string dataCode,
            long amountFromGateway)
        {
            var currentStatus = payment.Status ?? "Pending";
            if (!string.Equals(currentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                // Đã xử lý trước đó
                return;
            }

            var isSuccess = topCode == "00" && dataCode == "00";

            if (isSuccess)
            {
                var (items, userId, email) = GetCartSnapshot(payment.PaymentId);
                if (items == null || !items.Any() || string.IsNullOrWhiteSpace(email))
                {
                    // Không còn dữ liệu cart -> không thể tạo order; cancel payment để tránh treo.
                    payment.Status = "Cancelled";
                    await _context.SaveChangesAsync();
                    ClearCartSnapshot(payment.PaymentId);
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

                // Order tạo từ cart: không dùng Status, chỉ là log immutable
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

                    _context.OrderDetails.Add(orderDetail);
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                ClearCartSnapshot(payment.PaymentId);
                return;
            }

            // Các trường hợp còn lại: user huỷ, thất bại, hết hạn...
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

            // Lấy gói hỗ trợ
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

            using var tx = await _context.Database.BeginTransactionAsync();

            // Dùng UnixTimeSeconds làm orderCode đơn giản
            var orderCode = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);

            // Tạo bản ghi Payment Pending – TransactionType = SUPPORT_PLAN
            var payment = new Payment
            {
                Amount = plan.Price,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Provider = "PayOS",
                ProviderOrderCode = orderCode,
                Email = user.Email,
                TransactionType = "SUPPORT_PLAN"
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            var frontendBaseUrl = _config["PayOS:FrontendBaseUrl"]?.TrimEnd('/')
                                  ?? "https://keytietkiem.com";

            // FE sẽ đọc paymentId/supportPlanId từ query để gọi /api/supportplans/confirm-payment
            var returnUrl =
                $"{frontendBaseUrl}/support-plan/payment-result?paymentId={payment.PaymentId}&supportPlanId={plan.SupportPlanId}";
            var cancelUrl =
                $"{frontendBaseUrl}/support-plan/payment-cancel?paymentId={payment.PaymentId}&supportPlanId={plan.SupportPlanId}";

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
            var amountInt = (int)Math.Round(plan.Price, 0, MidpointRounding.AwayFromZero);

            // Gọi PayOS để lấy checkoutUrl
            var paymentUrl = await _payOs.CreatePayment(
                orderCode,
                amountInt,
                description,
                returnUrl,
                cancelUrl,
                buyerPhone,
                buyerName,
                buyerEmail
            );

            await tx.CommitAsync();

            var resp = new CreateSupportPlanPayOSPaymentResponseDTO
            {
                PaymentId = payment.PaymentId,
                SupportPlanId = plan.SupportPlanId,
                SupportPlanName = plan.Name,
                Price = plan.Price,
                PaymentUrl = paymentUrl
            };

            return Ok(resp);
        }
    }
}
