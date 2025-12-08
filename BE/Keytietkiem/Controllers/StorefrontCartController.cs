// Keytietkiem/Controllers/StorefrontCartController.cs
using Keytietkiem.DTOs.Cart;
using Keytietkiem.DTOs.Orders;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using static Keytietkiem.DTOs.Cart.StorefrontCartDto;

namespace Keytietkiem.Controllers
{
    [ApiController]
    // Hỗ trợ cả style cũ lẫn style mới
    [Route("apistorefront/cart")]
    [Route("api/storefront/cart")]
    public class StorefrontCartController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IMemoryCache _cache;
        private readonly IAccountService _accountService;
        private readonly PayOSService _payOs;
        private readonly IConfiguration _config;

        // TTL dài hơn cho user đã đăng nhập, ngắn hơn cho guest
        private static readonly TimeSpan AuthenticatedCartTtl = TimeSpan.FromDays(7);
        private static readonly TimeSpan AnonymousCartTtl = TimeSpan.FromHours(1);

        // TTL cho snapshot cart gắn với 1 Payment (dữ liệu ẩn, không hiển thị ra)
        // Khớp yêu cầu: sau ~5 phút không thanh toán thì coi như hết hạn.
        private static readonly TimeSpan CartPaymentSnapshotTtl = TimeSpan.FromMinutes(5);

        private const string AnonymousCartCookieName = "ktk_anon_cart";

        public StorefrontCartController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IMemoryCache cache,
            IAccountService accountService,
            PayOSService payOs,
            IConfiguration config)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _payOs = payOs ?? throw new ArgumentNullException(nameof(payOs));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // ===== GET: /apistorefront/cart =====
        // Áp dụng cho cả guest và user đã đăng nhập
        [HttpGet]
        public ActionResult<StorefrontCartDto> GetCart()
        {
            var (cacheKey, _, isAuthenticated) = GetCartContext();
            var cart = GetOrCreateCart(cacheKey, isAuthenticated);
            return Ok(ToDto(cart));
        }

        // ===== POST: /apistorefront/cart/items =====
        // Body: { "variantId": "...", "quantity": 1 }
        // Áp dụng cho cả guest và user đã đăng nhập
        [HttpPost("items")]
        public async Task<ActionResult<StorefrontCartDto>> AddItem([FromBody] AddToCartRequestDto dto)
        {
            var (cacheKey, _, isAuthenticated) = GetCartContext();

            if (dto == null || dto.VariantId == Guid.Empty)
            {
                return BadRequest(new { message = "VariantId is required." });
            }

            if (dto.Quantity <= 0)
            {
                return BadRequest(new { message = "Quantity must be greater than 0." });
            }

            await using var db = await _dbFactory.CreateDbContextAsync();

            // Lấy variant CÓ TRACKING để chỉnh StockQty
            var variant = await db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.VariantId == dto.VariantId);

            if (variant == null || variant.Product == null)
            {
                return NotFound(new { message = "Variant not found." });
            }

            var addQty = dto.Quantity;

            // For SHARED_ACCOUNT products, check actual available slots from ProductAccounts
            if (string.Equals(variant.Product.ProductType, "SHARED_ACCOUNT", StringComparison.OrdinalIgnoreCase))
            {
                var availableSlots = await db.ProductAccounts
                    .Where(pa => pa.VariantId == variant.VariantId &&
                                 pa.Status == "Active" &&
                                 pa.MaxUsers > 1)
                    .Include(pa => pa.ProductAccountCustomers)
                    .Where(pa => pa.ProductAccountCustomers.Count(pac => pac.IsActive) < pa.MaxUsers)
                    .Select(pa => pa.MaxUsers - pa.ProductAccountCustomers.Count(pac => pac.IsActive))
                    .SumAsync();

                if (availableSlots <= 0)
                {
                    return BadRequest(new { message = "Sản phẩm đã hết slot." });
                }

                if (addQty > availableSlots)
                {
                    return BadRequest(new
                    {
                        message = $"Số lượng slot không đủ. Chỉ còn {availableSlots} slot."
                    });
                }

                // For shared accounts, StockQty represents available slots
                variant.StockQty -= addQty;
                if (variant.StockQty < 0) variant.StockQty = 0;
            }
            else
            {
                // For other product types (PERSONAL_KEY, PERSONAL_ACCOUNT, etc.), use regular stock check
                if (variant.StockQty <= 0)
                {
                    return BadRequest(new { message = "Sản phẩm đã hết hàng." });
                }

                if (addQty > variant.StockQty)
                {
                    return BadRequest(new
                    {
                        message = $"Số lượng tồn kho không đủ. Chỉ còn {variant.StockQty} sản phẩm."
                    });
                }

                // Trừ tồn kho NGAY trong DB
                variant.StockQty -= addQty;
                if (variant.StockQty < 0) variant.StockQty = 0;
            }

            await db.SaveChangesAsync();

            // Cập nhật cart trong cache
            var cart = GetOrCreateCart(cacheKey, isAuthenticated);
            var existing = cart.Items.FirstOrDefault(i => i.VariantId == variant.VariantId);

            if (existing == null)
            {
                cart.Items.Add(new StorefrontCartItemDto
                {
                    VariantId = variant.VariantId,
                    ProductId = variant.ProductId,
                    ProductName = variant.Product.ProductName ?? string.Empty,
                    ProductType = variant.Product.ProductType ?? string.Empty,
                    VariantTitle = variant.Title ?? string.Empty,
                    Thumbnail = variant.Thumbnail,
                    Quantity = addQty,

                    // GIÁ NIÊM YẾT + GIÁ BÁN
                    ListPrice = variant.ListPrice,
                    UnitPrice = variant.SellPrice
                });
            }
            else
            {
                // Thêm tiếp cùng variant => tăng số lượng
                existing.Quantity += addQty;
            }

            SaveCart(cacheKey, cart, isAuthenticated);

            return Ok(ToDto(cart));
        }

        // ===== POST: /apistorefront/cart/checkout =====
        // Flow:
        //  - Tính tiền từ cart.
        //  - Gọi PayOS để lấy checkoutUrl.
        //  - Lưu snapshot cart (ẩn) vào cache key theo PaymentId.
        //  - Xoá cart đang hiển thị (cache cart theo user/anon).
        //  - Trả về PaymentId + PaymentUrl cho FE redirect.
        [HttpPost("checkout")]
        public async Task<ActionResult<CartCheckoutResultDto>> Checkout()
        {
            var (cartCacheKey, userId, isAuthenticated) = GetCartContext();

            var cart = GetOrCreateCart(cartCacheKey, isAuthenticated);
            if (cart.Items == null || cart.Items.Count == 0)
                return BadRequest(new { message = "Giỏ hàng đang trống." });

            // ===== Lấy email =====
            string email;

            if (isAuthenticated && userId.HasValue)
            {
                // Ưu tiên email trong JWT claims
                email = GetCurrentUserEmail() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(email))
                {
                    // Fallback: lấy từ DB Users
                    await using var dbLookup = await _dbFactory.CreateDbContextAsync();
                    var user = await dbLookup.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.UserId == userId.Value);

                    email = user?.Email ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    return BadRequest(new
                    {
                        message = "Tài khoản hiện tại chưa có email. Vui lòng cập nhật email trước khi thanh toán."
                    });
                }
            }
            else
            {
                // Guest bắt buộc phải set ReceiverEmail
                email = (cart.ReceiverEmail ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(email))
                {
                    return BadRequest(new { message = "Email nhận hàng không được để trống." });
                }

                if (!IsValidEmail(email))
                {
                    return BadRequest(new { message = "Email nhận hàng không hợp lệ." });
                }
            }

            email = email.Trim();
            if (email.Length > 254)
                email = email[..254]; // tránh lỗi truncate nvarchar(254)

            // ===== Tính tiền từ cart =====
            decimal totalListAmount = 0m;
            decimal totalAmount = 0m;

            foreach (var item in cart.Items)
            {
                var qty = item.Quantity < 0 ? 0 : item.Quantity;

                var listPrice = item.ListPrice != 0 ? item.ListPrice : item.UnitPrice;
                if (listPrice < 0) listPrice = 0;

                var unitPrice = item.UnitPrice;
                if (unitPrice < 0) unitPrice = 0;

                totalListAmount += listPrice * qty; // giá niêm yết
                totalAmount += unitPrice * qty;     // giá sau giảm
            }

            if (totalAmount <= 0)
            {
                return BadRequest(new { message = "Số tiền thanh toán không hợp lệ." });
            }

            // Round về 2 số lẻ (decimal(12,2)/decimal(18,2))
            totalListAmount = Math.Round(totalListAmount, 2, MidpointRounding.AwayFromZero);
            totalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);

            // ===== Tạo Payment Pending trong DB + gọi PayOS =====
            await using var db = await _dbFactory.CreateDbContextAsync();
            using var tx = await db.Database.BeginTransactionAsync();

            // Sử dụng UnixTimeSeconds làm orderCode (unique int)
            var orderCode = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);

            var payment = new Payment
            {
                Amount = totalAmount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Provider = "PayOS",
                ProviderOrderCode = orderCode,
                Email = email,
                TransactionType = "ORDER_PAYMENT"
            };

            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            var frontendBaseUrl = _config["PayOS:FrontendBaseUrl"]?.TrimEnd('/')
                                  ?? "https://keytietkiem.com";

            // Trang kết quả / cancel FE sẽ đọc paymentId để hiển thị thông báo, redirect tiếp
            var returnUrl = $"{frontendBaseUrl}/cart/payment-result?paymentId={payment.PaymentId}";
            var cancelUrl = $"{frontendBaseUrl}/cart/payment-cancel?paymentId={payment.PaymentId}";

            string buyerName = email;
            string buyerPhone = string.Empty;

            if (isAuthenticated && userId.HasValue)
            {
                var user = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId.Value);

                if (user != null)
                {
                    buyerName = string.IsNullOrWhiteSpace(user.FullName)
                        ? (user.Email ?? buyerName)
                        : user.FullName!;
                    buyerPhone = user.Phone ?? string.Empty;
                }
            }

            var amountInt = (int)Math.Round(totalAmount, 0, MidpointRounding.AwayFromZero);

            // Description encode PaymentId (không cần decode, chỉ để trace)
            var description = EncodeCartPaymentDescription(payment.PaymentId);

            var paymentUrl = await _payOs.CreatePayment(
                orderCode,
                amountInt,
                description,
                returnUrl,
                cancelUrl,
                buyerPhone,
                buyerName,
                email
            );

            await tx.CommitAsync();

            // ===== Lưu snapshot cart theo PaymentId (ẩn, không hiển thị) =====
            var itemsSnapshot = cart.Items
                .Select(i => new StorefrontCartItemDto
                {
                    VariantId = i.VariantId,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    ProductType = i.ProductType,
                    VariantTitle = i.VariantTitle,
                    Thumbnail = i.Thumbnail,
                    Quantity = i.Quantity,
                    ListPrice = i.ListPrice,
                    UnitPrice = i.UnitPrice
                })
                .ToList();

            var itemsKey = GetCartPaymentItemsCacheKey(payment.PaymentId);
            var metaKey = GetCartPaymentMetaCacheKey(payment.PaymentId);

            var snapshotOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CartPaymentSnapshotTtl);

            // Lưu danh sách item + meta (userId / email) – không hiển thị ra FE
            _cache.Set(itemsKey, itemsSnapshot, snapshotOptions);
            _cache.Set(metaKey, (UserId: userId, Email: email), snapshotOptions);

            // ===== Clear cart hiển thị (KHÔNG hoàn kho) =====
            // Tồn kho đã bị trừ khi AddItem/UpdateItem; nếu payment cancel/expire,
            // PayOS webhook sẽ gọi logic hoàn kho dựa trên snapshot.
            _cache.Remove(cartCacheKey);

            var result = new CartCheckoutResultDto
            {
                PaymentId = payment.PaymentId,
                PaymentStatus = payment.Status ?? "Pending",
                Amount = payment.Amount,
                Email = payment.Email,
                CreatedAt = payment.CreatedAt,
                PaymentUrl = paymentUrl
            };

            return Ok(result);
        }

        // ===== PUT: /apistorefront/cart/items/{variantId} =====
        // Body: { "quantity": 3 }
        [HttpPut("items/{variantId:guid}")]
        public async Task<ActionResult<StorefrontCartDto>> UpdateItemQuantity(
            Guid variantId,
            [FromBody] UpdateCartItemRequestDto dto)
        {
            var (cacheKey, _, isAuthenticated) = GetCartContext();

            if (dto == null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            if (dto.Quantity < 0)
            {
                return BadRequest(new { message = "Quantity must be greater than or equal to 0." });
            }

            var cart = GetOrCreateCart(cacheKey, isAuthenticated);
            var item = cart.Items.FirstOrDefault(i => i.VariantId == variantId);

            if (item == null)
            {
                return NotFound(new { message = "Item not found in cart." });
            }

            var oldQty = item.Quantity;
            var newQty = dto.Quantity;

            if (newQty == oldQty)
            {
                // Không thay đổi gì
                return Ok(ToDto(cart));
            }

            await using var db = await _dbFactory.CreateDbContextAsync();
            var variant = await db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.VariantId == variantId);

            if (variant == null || variant.Product == null)
            {
                return NotFound(new { message = "Variant not found." });
            }

            if (newQty == 0)
            {
                // Xoá item => trả lại toàn bộ số lượng vào kho
                if (oldQty > 0)
                {
                    variant.StockQty += oldQty;
                    // variant.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }

                cart.Items.Remove(item);
            }
            else
            {
                var delta = newQty - oldQty; // >0: tăng, <0: giảm

                if (delta > 0)
                {
                    // Người dùng tăng thêm delta sản phẩm -> phải trừ tồn kho
                    // For SHARED_ACCOUNT products, check actual available slots
                    if (string.Equals(variant.Product.ProductType, "SHARED_ACCOUNT", StringComparison.OrdinalIgnoreCase))
                    {
                        var availableSlots = await db.ProductAccounts
                            .Where(pa => pa.VariantId == variant.VariantId &&
                                         pa.Status == "Active" &&
                                         pa.MaxUsers > 1)
                            .Include(pa => pa.ProductAccountCustomers)
                            .Where(pa => pa.ProductAccountCustomers.Count(pac => pac.IsActive) < pa.MaxUsers)
                            .Select(pa => pa.MaxUsers - pa.ProductAccountCustomers.Count(pac => pac.IsActive))
                            .SumAsync();

                        if (availableSlots < delta)
                        {
                            return BadRequest(new
                            {
                                message = $"Số lượng slot không đủ. Chỉ còn {availableSlots} slot."
                            });
                        }

                        variant.StockQty -= delta;
                        if (variant.StockQty < 0) variant.StockQty = 0;
                    }
                    else
                    {
                        if (variant.StockQty < delta)
                        {
                            return BadRequest(new
                            {
                                message = $"Số lượng tồn kho không đủ. Chỉ còn {variant.StockQty} sản phẩm."
                            });
                        }

                        variant.StockQty -= delta;
                        if (variant.StockQty < 0) variant.StockQty = 0;
                    }
                    // variant.UpdatedAt = DateTime.UtcNow;
                }
                else if (delta < 0)
                {
                    // Người dùng giảm bớt -delta sản phẩm -> trả lại kho
                    var giveBack = -delta;
                    variant.StockQty += giveBack;
                    // variant.UpdatedAt = DateTime.UtcNow;
                }

                await db.SaveChangesAsync();

                item.Quantity = newQty;
            }

            SaveCart(cacheKey, cart, isAuthenticated);

            return Ok(ToDto(cart));
        }

        // ===== DELETE: /apistorefront/cart/items/{variantId} =====
        [HttpDelete("items/{variantId:guid}")]
        public async Task<ActionResult<StorefrontCartDto>> RemoveItem(Guid variantId)
        {
            var (cacheKey, _, isAuthenticated) = GetCartContext();

            var cart = GetOrCreateCart(cacheKey, isAuthenticated);
            var item = cart.Items.FirstOrDefault(i => i.VariantId == variantId);

            if (item != null)
            {
                var qty = item.Quantity;

                if (qty > 0)
                {
                    await using var db = await _dbFactory.CreateDbContextAsync();
                    var variant = await db.ProductVariants
                        .FirstOrDefaultAsync(v => v.VariantId == variantId);

                    if (variant != null)
                    {
                        variant.StockQty += qty;
                        // variant.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                }

                cart.Items.Remove(item);
                SaveCart(cacheKey, cart, isAuthenticated);
            }

            return Ok(ToDto(cart));
        }

        // ===== PUT: /apistorefront/cart/receiver-email =====
        // Body: { "receiverEmail": "abc@gmail.com" }
        [HttpPut("receiver-email")]
        public ActionResult<StorefrontCartDto> SetReceiverEmail([FromBody] SetCartReceiverEmailRequestDto dto)
        {
            var (cacheKey, _, isAuthenticated) = GetCartContext();

            if (dto == null || string.IsNullOrWhiteSpace(dto.ReceiverEmail))
            {
                return BadRequest(new { message = "ReceiverEmail is required." });
            }

            var cart = GetOrCreateCart(cacheKey, isAuthenticated);
            cart.ReceiverEmail = dto.ReceiverEmail.Trim();

            SaveCart(cacheKey, cart, isAuthenticated);

            return Ok(ToDto(cart));
        }

        // ===== DELETE: /apistorefront/cart =====
        [HttpDelete]
        public async Task<IActionResult> ClearCart([FromQuery] bool skipRestoreStock = false)
        {
            var (cacheKey, _, isAuthenticated) = GetCartContext();

            var cart = GetOrCreateCart(cacheKey, isAuthenticated);

            // Nếu KHÔNG skipRestoreStock => hoàn kho như cũ (dùng cho nút "Xoá giỏ hàng").
            // Nếu skipRestoreStock = true (dùng sau khi tạo Payment từ cart) => KHÔNG hoàn kho nữa,
            // vì tồn kho đã bị trừ ngay lúc AddItem/UpdateItem và sẽ chỉ cộng lại nếu thanh toán bị Cancel.
            if (!skipRestoreStock && cart.Items.Any())
            {
                await using var db = await _dbFactory.CreateDbContextAsync();

                var variantIds = cart.Items
                    .Select(i => i.VariantId)
                    .Distinct()
                    .ToList();

                var variants = await db.ProductVariants
                    .Where(v => variantIds.Contains(v.VariantId))
                    .ToListAsync();

                foreach (var cartItem in cart.Items)
                {
                    var variant = variants.FirstOrDefault(v => v.VariantId == cartItem.VariantId);
                    if (variant != null && cartItem.Quantity > 0)
                    {
                        variant.StockQty += cartItem.Quantity;
                        // variant.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await db.SaveChangesAsync();
            }

            _cache.Remove(cacheKey);

            return NoContent();
        }

        // ===== Helpers =====

        private Guid? GetCurrentUserId()
        {
            var claim = User?.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;

            return Guid.TryParse(claim.Value, out var id) ? id : (Guid?)null;
        }

        // Lấy username từ claims (nếu lúc login có add ClaimTypes.Name)
        private string? GetCurrentUserName()
            => User?.FindFirst(ClaimTypes.Name)?.Value;

        // Lấy email từ claims (tuỳ anh map lúc tạo JWT)
        private string? GetCurrentUserEmail()
            => User?.FindFirst(ClaimTypes.Email)?.Value
               ?? User?.FindFirst("email")?.Value;

        // Lấy context cart hiện tại:
        //  - Nếu đã đăng nhập: dùng userId.
        //  - Nếu chưa đăng nhập: dùng cookie ẩn ktk_anon_cart.
        private (string CacheKey, Guid? UserId, bool IsAuthenticated) GetCartContext()
        {
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                var key = GetUserCacheKey(userId.Value);
                return (key, userId, true);
            }

            var anonId = GetOrCreateAnonymousCartId();
            var anonKey = GetAnonymousCacheKey(anonId);
            return (anonKey, null, false);
        }

        private string GetUserCacheKey(Guid userId)
            => $"cart:user:{userId:D}";

        private string GetAnonymousCacheKey(string anonId)
            => $"cart:anon:{anonId}";

        private string GetOrCreateAnonymousCartId()
        {
            if (Request.Cookies.TryGetValue(AnonymousCartCookieName, out var existing) &&
                !string.IsNullOrWhiteSpace(existing))
            {
                // Browser đã có cookie ktk_anon_cart -> dùng lại ID cũ
                return existing;
            }

            var newId = Guid.NewGuid().ToString("N");

            
            var options = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.None,       
                Secure = true,                      
                Expires = DateTimeOffset.UtcNow.Add(AnonymousCartTtl)
            };

            Response.Cookies.Append(AnonymousCartCookieName, newId, options);
            return newId;
        }



        private CartCacheModel GetOrCreateCart(string cacheKey, bool isAuthenticated)
        {
            if (_cache.TryGetValue<CartCacheModel>(cacheKey, out var existing) && existing != null)
            {
                return existing;
            }

            var cart = new CartCacheModel
            {
                Items = new List<StorefrontCartItemDto>()
            };

            SaveCart(cacheKey, cart, isAuthenticated);
            return cart;
        }

        private void SaveCart(string cacheKey, CartCacheModel cart, bool isAuthenticated)
        {
            var ttl = isAuthenticated ? AuthenticatedCartTtl : AnonymousCartTtl;

            var options = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(ttl);

            _cache.Set(cacheKey, cart, options);
        }

        // KHÔNG static nữa để dùng được User / GetCurrentUserName / GetCurrentUserEmail
        private StorefrontCartDto ToDto(CartCacheModel model)
        {
            var itemsCopy = model.Items
                .Select(i => new StorefrontCartItemDto
                {
                    VariantId = i.VariantId,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    ProductType = i.ProductType,
                    VariantTitle = i.VariantTitle,
                    Thumbnail = i.Thumbnail,
                    Quantity = i.Quantity,
                    ListPrice = i.ListPrice,
                    UnitPrice = i.UnitPrice
                })
                .ToList();

            var accountUserName = GetCurrentUserName();
            var accountEmail = GetCurrentUserEmail();

            return new StorefrontCartDto
            {
                ReceiverEmail = model.ReceiverEmail,
                Items = itemsCopy,
                AccountUserName = accountUserName,
                AccountEmail = accountEmail
            };
        }

        private static string GetCartPaymentItemsCacheKey(Guid paymentId)
            => $"cart:payment:{paymentId:D}:items";

        private static string GetCartPaymentMetaCacheKey(Guid paymentId)
            => $"cart:payment:{paymentId:D}:meta";

        private static string EncodeCartPaymentDescription(Guid paymentId)
        {
            var bytes = paymentId.ToByteArray();

            var base64 = Convert.ToBase64String(bytes)
                .TrimEnd('=')          // bỏ padding
                .Replace('+', '-')     // URL-safe
                .Replace('/', '_');    // URL-safe

            return "C" + base64;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                // So khớp lại để tránh case address được normalize khác
                return addr.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private sealed class CartCacheModel
        {
            public string? ReceiverEmail { get; set; }

            public List<StorefrontCartItemDto> Items { get; set; } = new();
        }
    }
}
