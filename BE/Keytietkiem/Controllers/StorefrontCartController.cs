// Keytietkiem/Controllers/StorefrontCartController.cs
using Keytietkiem.DTOs.Cart;
using Keytietkiem.DTOs.Orders;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
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

        private static readonly TimeSpan CartTtl = TimeSpan.FromMinutes(60);

        public StorefrontCartController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IMemoryCache cache)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        // ===== GET: /apistorefront/cart =====
        [HttpGet]
        public ActionResult<StorefrontCartDto> GetCart()
        {
            var userId = GetCurrentUserId();
            if (userId is null)
            {
                return Unauthorized(new { message = "User must be logged in to use server-side cart." });
            }

            var cart = GetOrCreateCart(userId.Value);
            return Ok(ToDto(cart));
        }

        // ===== POST: /apistorefront/cart/items =====
        // Body: { "variantId": "...", "quantity": 1 }
        [HttpPost("items")]
        public async Task<ActionResult<StorefrontCartDto>> AddItem([FromBody] AddToCartRequestDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId is null)
            {
                return Unauthorized(new { message = "User must be logged in to use server-side cart." });
            }

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

            // Kiểm tra tồn kho
            if (variant.StockQty <= 0)
            {
                return BadRequest(new { message = "Sản phẩm đã hết hàng." });
            }

            var addQty = dto.Quantity;
            if (addQty > variant.StockQty)
            {
                return BadRequest(new
                {
                    message = $"Số lượng tồn kho không đủ. Chỉ còn {variant.StockQty} sản phẩm."
                });
            }

            // Trừ tồn kho
            variant.StockQty -= addQty;
            if (variant.StockQty < 0) variant.StockQty = 0;
            // nếu có UpdatedAt:
            // variant.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            // Cập nhật cart trong cache
            var cart = GetOrCreateCart(userId.Value);
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

            SaveCart(userId.Value, cart);

            return Ok(ToDto(cart));
        }

        [HttpPost("checkout")]
        public async Task<ActionResult<CartCheckoutResultDto>> Checkout()
        {
            var userId = GetCurrentUserId();
            if (userId is null)
                return Unauthorized(new { message = "Bạn cần đăng nhập để thanh toán." });

            var cart = GetOrCreateCart(userId.Value);
            if (cart.Items == null || cart.Items.Count == 0)
                return BadRequest(new { message = "Giỏ hàng đang trống." });

            var email = GetCurrentUserEmail() ?? cart.ReceiverEmail;
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email nhận hàng không được để trống." });

            email = email.Trim();
            if (email.Length > 254)
                email = email[..254]; // tránh lỗi truncate nvarchar(254)

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

            var discountAmount = totalListAmount - totalAmount;
            if (discountAmount < 0) discountAmount = 0;

            // Round về 2 số lẻ cho khớp decimal(12,2) của Orders
            totalListAmount = Math.Round(totalListAmount, 2, MidpointRounding.AwayFromZero);
            totalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
            discountAmount = Math.Round(discountAmount, 2, MidpointRounding.AwayFromZero);

            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();

                // 🔧 SỬA Ở ĐÂY:
                //  - TotalAmount  = tổng giá niêm yết (totalListAmount)
                //  - DiscountAmount = tổng giảm giá
                //  - FinalAmount  = tổng giá sau giảm (totalAmount)
                var order = new Order
                {
                    UserId = userId.Value,
                    Email = email,
                    TotalAmount = totalListAmount,
                    DiscountAmount = discountAmount,
                    FinalAmount = totalAmount,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                db.Orders.Add(order);
                await db.SaveChangesAsync();

                foreach (var item in cart.Items)
                {
                    if (item.Quantity <= 0) continue;

                    var detail = new OrderDetail
                    {
                        OrderId = order.OrderId,
                        VariantId = item.VariantId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    };

                    db.OrderDetails.Add(detail);
                }

                await db.SaveChangesAsync();

                // Clear cart
                var cacheKey = GetCacheKey(userId.Value);
                _cache.Remove(cacheKey);

                var finalAmount = order.FinalAmount ?? (order.TotalAmount - order.DiscountAmount);

                var result = new CartCheckoutResultDto
                {
                    OrderId = order.OrderId,
                    OrderStatus = order.Status,
                    TotalAmount = order.TotalAmount,
                    DiscountAmount = order.DiscountAmount,
                    FinalAmount = finalAmount,
                    Email = order.Email,
                    CreatedAt = order.CreatedAt
                };

                return Ok(result);
            }
            catch (DbUpdateException ex)
            {
                // Trong môi trường dev, trả full detail ra FE để biết constraint nào lỗi
                return StatusCode(500, new
                {
                    message = "Lỗi khi tạo đơn hàng.",
                    detail = ex.InnerException?.Message ?? ex.Message
                });
            }
        }



        // ===== PUT: /apistorefront/cart/items/{variantId} =====
        // Body: { "quantity": 3 }
        [HttpPut("items/{variantId:guid}")]
        public async Task<ActionResult<StorefrontCartDto>> UpdateItemQuantity(
            Guid variantId,
            [FromBody] UpdateCartItemRequestDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId is null)
            {
                return Unauthorized(new { message = "User must be logged in to use server-side cart." });
            }

            if (dto == null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            if (dto.Quantity < 0)
            {
                return BadRequest(new { message = "Quantity must be greater than or equal to 0." });
            }

            var cart = GetOrCreateCart(userId.Value);
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
                .FirstOrDefaultAsync(v => v.VariantId == variantId);

            if (variant == null)
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
                    if (variant.StockQty < delta)
                    {
                        return BadRequest(new
                        {
                            message = $"Số lượng tồn kho không đủ. Chỉ còn {variant.StockQty} sản phẩm."
                        });
                    }

                    variant.StockQty -= delta;
                    if (variant.StockQty < 0) variant.StockQty = 0;
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

            SaveCart(userId.Value, cart);

            return Ok(ToDto(cart));
        }


        // ===== DELETE: /apistorefront/cart/items/{variantId} =====
        [HttpDelete("items/{variantId:guid}")]
        public async Task<ActionResult<StorefrontCartDto>> RemoveItem(Guid variantId)
        {
            var userId = GetCurrentUserId();
            if (userId is null)
            {
                return Unauthorized(new { message = "User must be logged in to use server-side cart." });
            }

            var cart = GetOrCreateCart(userId.Value);
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
                SaveCart(userId.Value, cart);
            }

            return Ok(ToDto(cart));
        }


        // ===== PUT: /apistorefront/cart/receiver-email =====
        // Body: { "receiverEmail": "abc@gmail.com" }
        [HttpPut("receiver-email")]
        public ActionResult<StorefrontCartDto> SetReceiverEmail([FromBody] SetCartReceiverEmailRequestDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId is null)
            {
                return Unauthorized(new { message = "User must be logged in to use server-side cart." });
            }

            if (dto == null || string.IsNullOrWhiteSpace(dto.ReceiverEmail))
            {
                return BadRequest(new { message = "ReceiverEmail is required." });
            }

            var cart = GetOrCreateCart(userId.Value);
            cart.ReceiverEmail = dto.ReceiverEmail.Trim();

            SaveCart(userId.Value, cart);

            return Ok(ToDto(cart));
        }

        // ===== DELETE: /apistorefront/cart =====
        [HttpDelete]
        public async Task<IActionResult> ClearCart()
        {
            var userId = GetCurrentUserId();
            if (userId is null)
            {
                return Unauthorized(new { message = "User must be logged in to use server-side cart." });
            }

            var cart = GetOrCreateCart(userId.Value);

            if (cart.Items.Any())
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

            var cacheKey = GetCacheKey(userId.Value);
            _cache.Remove(cacheKey);

            // FE đang tự tạo emptyCart nên NoContent là OK
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

        private string GetCacheKey(Guid userId)
            => $"cart:user:{userId:D}";

        private CartCacheModel GetOrCreateCart(Guid userId)
        {
            var cacheKey = GetCacheKey(userId);

            if (_cache.TryGetValue<CartCacheModel>(cacheKey, out var existing) && existing != null)
            {
                return existing;
            }

            var cart = new CartCacheModel
            {
                Items = new List<StorefrontCartItemDto>()
            };

            SaveCart(userId, cart);
            return cart;
        }

        private void SaveCart(Guid userId, CartCacheModel cart)
        {
            var cacheKey = GetCacheKey(userId);

            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CartTtl);

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

        private sealed class CartCacheModel
        {
            public string? ReceiverEmail { get; set; }

            public List<StorefrontCartItemDto> Items { get; set; } = new();
        }
    }
}
