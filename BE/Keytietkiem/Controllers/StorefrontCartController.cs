using Keytietkiem.DTOs.Cart;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Utils;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("apistorefront/cart")]
    [Route("api/storefront/cart")]
    [EnableRateLimiting("CartPolicy")]
    public class StorefrontCartController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

        private static readonly TimeSpan GuestCartTtl = TimeSpan.FromDays(7);
        private static readonly TimeSpan UserCartTtl = TimeSpan.FromDays(30);

        // ✅ PATCH: đồng bộ lock timeout với checkout/payment timeout (5 phút)
        private static readonly TimeSpan ConvertingLockTimeout = TimeSpan.FromMinutes(5);

        private const string AnonIdCookieName = "ktk_anon_id";
        private const string GuestIdHeaderName = "X-Guest-Cart-Id";

        public StorefrontCartController(IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<StorefrontCartDto>> GetCart()
        {
            await using var db = _dbFactory.CreateDbContext();

            // ✅ PATCH: có thể trả về Converting cart (không tạo cart mới)
            var cart = await GetOrCreateActiveOrConvertingCartAsync(db);

            var full = await GetCartWithItemsAsync(db, cart.CartId);
            return Ok(ToCartDto(full));
        }

        [HttpPost("items")]
        [AllowAnonymous]
        public async Task<ActionResult<StorefrontCartDto>> AddItem([FromBody] AddToCartRequestDto req)
        {
            if (req == null || req.VariantId == Guid.Empty)
                return BadRequest(new { message = "VariantId is required." });

            if (req.Quantity <= 0)
                return BadRequest(new { message = "Quantity must be greater than 0." });

            await using var db = _dbFactory.CreateDbContext();

            var cart = await GetOrCreateActiveOrConvertingCartAsync(db);

            // ✅ nếu Converting => không cho ghi
            if (!string.Equals(cart.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "Cart đang được checkout. Vui lòng thử lại sau." });

            var ttl = GetTtl(cart.UserId);
            var now = DateTime.UtcNow;

            var variant = await db.ProductVariants
                .Include(v => v.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.VariantId == req.VariantId);

            if (variant == null || variant.Product == null)
                return NotFound(new { message = "Variant not found." });

            if (!IsVariantActive(variant.Status))
                return BadRequest(new { message = "Sản phẩm không tồn tại hoặc ngừng kinh doanh." });

            // ✅ PATCH: Upsert chống race condition
            if (db.Database.IsRelational())
            {
                // 1) Atomic UPDATE trước (nếu row đã tồn tại)
                var updated = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [dbo].[CartItem]
            SET [Quantity] = [Quantity] + {req.Quantity},
                [UpdatedAt] = {now}
            WHERE [CartId] = {cart.CartId}
              AND [VariantId] = {req.VariantId};
        ");

                // 2) Nếu chưa có row => thử INSERT
                if (updated == 0)
                {
                    var newItem = new CartItem
                    {
                        CartId = cart.CartId,
                        VariantId = req.VariantId,
                        Quantity = req.Quantity,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    db.CartItems.Add(newItem);

                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        // Có thể request khác vừa insert cùng (CartId, VariantId) => detach item local và retry UPDATE
                        db.Entry(newItem).State = EntityState.Detached;

                        await db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE [dbo].[CartItem]
                    SET [Quantity] = [Quantity] + {req.Quantity},
                        [UpdatedAt] = {now}
                    WHERE [CartId] = {cart.CartId}
                      AND [VariantId] = {req.VariantId};
                ");
                    }
                }

                // activity => luôn touch cart
                TouchCart(cart, ttl, now);
                await db.SaveChangesAsync();
            }
            else
            {
                // InMemory/unit test: giữ logic cũ
                var existing = cart.CartItems.FirstOrDefault(i => i.VariantId == req.VariantId);
                if (existing != null)
                {
                    existing.Quantity += req.Quantity;
                    existing.UpdatedAt = now;
                }
                else
                {
                    db.CartItems.Add(new CartItem
                    {
                        CartId = cart.CartId,
                        VariantId = req.VariantId,
                        Quantity = req.Quantity,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }

                TouchCart(cart, ttl, now);
                await db.SaveChangesAsync();
            }

            var full = await GetCartWithItemsAsync(db, cart.CartId);
            return Ok(ToCartDto(full));
        }


        [HttpPut("items/{variantId}")]
        public async Task<ActionResult<StorefrontCartDto>> UpdateItem(Guid variantId, [FromBody] UpdateCartItemRequestDto req)
        {
            if (variantId == Guid.Empty) return BadRequest(new { message = "VariantId is required." });
            if (req == null) return BadRequest(new { message = "Body is required." });

            await using var db = _dbFactory.CreateDbContext();

            var cart = await GetOrCreateActiveOrConvertingCartAsync(db);

            // ✅ PATCH: nếu Converting => không cho ghi
            if (!string.Equals(cart.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "Cart đang được checkout. Vui lòng thử lại sau." });

            var ttl = GetTtl(cart.UserId);
            var now = DateTime.UtcNow;

            var item = cart.CartItems.FirstOrDefault(i => i.VariantId == variantId);
            if (item == null) return NotFound(new { message = "Sản phẩm không có trong giỏ." });

            if (req.Quantity <= 0)
            {
                db.CartItems.Remove(item);
            }
            else
            {
                item.Quantity = req.Quantity;
                item.UpdatedAt = now;
            }

            TouchCart(cart, ttl, now);
            await db.SaveChangesAsync();

            var full = await GetCartWithItemsAsync(db, cart.CartId);
            return Ok(ToCartDto(full));
        }

        [HttpDelete("items/{variantId}")]
        public async Task<ActionResult<StorefrontCartDto>> RemoveItem(Guid variantId)
        {
            if (variantId == Guid.Empty) return BadRequest(new { message = "VariantId is required." });

            await using var db = _dbFactory.CreateDbContext();

            var cart = await GetOrCreateActiveOrConvertingCartAsync(db);

            // ✅ PATCH: nếu Converting => không cho ghi
            if (!string.Equals(cart.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "Cart đang được checkout. Vui lòng thử lại sau." });

            var ttl = GetTtl(cart.UserId);
            var now = DateTime.UtcNow;

            var item = cart.CartItems.FirstOrDefault(i => i.VariantId == variantId);
            if (item != null)
            {
                db.CartItems.Remove(item);
                TouchCart(cart, ttl, now);
                await db.SaveChangesAsync();
            }

            var full = await GetCartWithItemsAsync(db, cart.CartId);
            return Ok(ToCartDto(full));
        }

        [HttpDelete]
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearCart([FromQuery] bool skipRestoreStock = false)
        {
            await using var db = _dbFactory.CreateDbContext();

            var cart = await GetOrCreateActiveOrConvertingCartAsync(db);

            // ✅ PATCH: nếu Converting => không cho ghi
            if (!string.Equals(cart.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "Cart đang được checkout. Vui lòng thử lại sau." });

            var ttl = GetTtl(cart.UserId);
            var now = DateTime.UtcNow;

            if (cart.CartItems.Any())
            {
                db.CartItems.RemoveRange(cart.CartItems);
            }

            // ✅ activity => luôn touch UpdatedAt/ExpiresAt
            TouchCart(cart, ttl, now);
            await db.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("receiver-email")]
        public async Task<ActionResult<StorefrontCartDto>> SetReceiverEmail([FromBody] SetCartReceiverEmailRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.ReceiverEmail))
                return BadRequest(new { message = "ReceiverEmail is required." });

            var email = dto.ReceiverEmail.Trim();
            if (!IsValidEmail(email))
                return BadRequest(new { message = "Email không hợp lệ." });

            await using var db = _dbFactory.CreateDbContext();

            var cart = await GetOrCreateActiveOrConvertingCartAsync(db);

            // ✅ PATCH: nếu Converting => không cho ghi
            if (!string.Equals(cart.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "Cart đang được checkout. Vui lòng thử lại sau." });

            var ttl = GetTtl(cart.UserId);
            var now = DateTime.UtcNow;

            cart.ReceiverEmail = email;
            TouchCart(cart, ttl, now);

            await db.SaveChangesAsync();

            var full = await GetCartWithItemsAsync(db, cart.CartId);
            return Ok(ToCartDto(full));
        }

        // ========================= Helpers =========================

        /// <summary>
        /// ✅ PATCH:
        /// - Lấy cart Active hoặc Converting (để không đẻ cart mới khi đang checkout)
        /// - Recover stuck Converting (Converting + ConvertedOrderId null + UpdatedAt quá hạn => revert Active)
        /// - Enforce TTL: Active cart quá hạn => set Expired và tạo cart mới
        /// </summary>
        private async Task<Cart> GetOrCreateActiveOrConvertingCartAsync(KeytietkiemDbContext db)
        {
            var userId = GetCurrentUserId();
            var anonId = GetOrSetAnonymousId();
            var now = DateTime.UtcNow;

            Cart? cart;

            if (userId.HasValue)
            {
                cart = await db.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c =>
                        c.UserId == userId.Value &&
                        (c.Status == "Active" || c.Status == "Converting"));
            }
            else
            {
                cart = await db.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c =>
                        c.AnonymousId == anonId &&
                        (c.Status == "Active" || c.Status == "Converting"));
            }

            if (cart != null)
            {
                // ✅ PATCH: Recover stuck Converting tại chỗ (DB guarantee)
                if (string.Equals(cart.Status, "Converting", StringComparison.OrdinalIgnoreCase)
                    && !cart.ConvertedOrderId.HasValue
                    && cart.UpdatedAt < now - ConvertingLockTimeout
                    && db.Database.IsRelational())
                {
                    var cutoff = now - ConvertingLockTimeout;

                    await db.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE [dbo].[Cart]
                        SET [Status] = {"Active"},
                            [UpdatedAt] = {now},
                            [ExpiresAt] = CASE WHEN [UserId] IS NULL THEN DATEADD(day, 7, {now})
                                               ELSE DATEADD(day, 30, {now}) END
                        WHERE [CartId] = {cart.CartId}
                          AND [Status] = {"Converting"}
                          AND [ConvertedOrderId] IS NULL
                          AND [UpdatedAt] < {cutoff}
                    ");

                    await db.Entry(cart).ReloadAsync();
                    await db.Entry(cart).Collection(c => c.CartItems).LoadAsync();
                }

                // ✅ TTL check chỉ áp cho Active (Converting thì để endpoint ghi trả 409)
                if (string.Equals(cart.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    var ttl = GetTtl(cart.UserId);

                    var isExpired =
                        (cart.ExpiresAt.HasValue && cart.ExpiresAt.Value < now) ||
                        (cart.UpdatedAt.Add(ttl) < now);

                    if (isExpired)
                    {
                        cart.Status = "Expired";
                        cart.UpdatedAt = now;
                        cart.ExpiresAt = now;
                        await db.SaveChangesAsync();
                        cart = null;
                    }
                }
            }

            if (cart != null) return cart;

            var ttlNew = userId.HasValue ? UserCartTtl : GuestCartTtl;

            var created = new Cart
            {
                CartId = Guid.NewGuid(),
                UserId = userId,
                AnonymousId = userId.HasValue ? null : anonId,
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now,
                ExpiresAt = now.Add(ttlNew),
                ReceiverEmail = null
            };

            db.Carts.Add(created);

            try
            {
                await db.SaveChangesAsync();
                await db.Entry(created).Collection(c => c.CartItems).LoadAsync();
                return created;
            }
            catch (DbUpdateException)
            {
                // Unique constraint: 1 Active cart / user hoặc anon
                if (userId.HasValue)
                {
                    var existing = await db.Carts
                        .Include(c => c.CartItems)
                        .FirstOrDefaultAsync(c =>
                            c.UserId == userId.Value &&
                            (c.Status == "Active" || c.Status == "Converting"));

                    if (existing != null) return existing;
                }
                else
                {
                    var existing = await db.Carts
                        .Include(c => c.CartItems)
                        .FirstOrDefaultAsync(c =>
                            c.AnonymousId == anonId &&
                            (c.Status == "Active" || c.Status == "Converting"));

                    if (existing != null) return existing;
                }

                throw;
            }
        }

        private async Task<Cart> GetCartWithItemsAsync(KeytietkiemDbContext db, Guid cartId)
        {
            var cart = await db.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Variant)
                        .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(c => c.CartId == cartId);

            return cart ?? throw new Exception("Cart not found.");
        }

        private StorefrontCartDto ToCartDto(Cart cart)
        {
            var accountEmail =
                User.FindFirstValue(ClaimTypes.Email) ??
                User.FindFirstValue("email");

            var accountUserName =
                User.Identity?.Name ??
                User.FindFirstValue("username") ??
                User.FindFirstValue(ClaimTypes.Name);

            return new StorefrontCartDto
            {
                CartId = cart.CartId,
                Status = cart.Status ?? "Active",
                UpdatedAt = cart.UpdatedAt,
                ReceiverEmail = cart.ReceiverEmail,
                AccountEmail = accountEmail,
                AccountUserName = accountUserName,
                Items = cart.CartItems
                    .Where(i => i.Variant != null)
                    .Select(i =>
                    {
                        var v = i.Variant!;
                        var p = v.Product;

                        return new StorefrontCartItemDto
                        {
                            CartItemId = i.CartItemId,
                            VariantId = i.VariantId,
                            ProductId = v.ProductId,
                            ProductName = p?.ProductName ?? "Unknown Product",
                            ProductType = p?.ProductType ?? "",
                            VariantTitle = v.Title ?? "",
                            Thumbnail = v.Thumbnail,
                            Slug = p?.Slug ?? "",
                            Quantity = i.Quantity,
                            ListPrice = v.ListPrice,
                            UnitPrice = v.SellPrice
                        };
                    })
                    .ToList()
            };
        }
        private static bool IsSqlServerUniqueViolation(DbUpdateException ex)
        {
            if (ex.InnerException is SqlException sqlEx)
                return sqlEx.Number == 2601 || sqlEx.Number == 2627;
            return false;
        }

        private static void TouchCart(Cart cart, TimeSpan ttl, DateTime now)
        {
            cart.UpdatedAt = now;
            cart.ExpiresAt = now.Add(ttl);
        }

        private static TimeSpan GetTtl(Guid? userId) => userId.HasValue ? UserCartTtl : GuestCartTtl;

        private Guid? GetCurrentUserId()
        {
            var raw =
                User.FindFirstValue("uid") ??
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out var id))
                return id;

            return null;
        }

        private string GetOrSetAnonymousId()
        {
            // ✅ Centralized guest identity logic:
            // - Prefer header X-Guest-Cart-Id (FE localStorage)
            // - Fallback cookie ktk_anon_id / legacy
            // - Ensure cookie Path="/" so it is sent to /api/orders/* too
            return GuestCartIdentityHelper.GetOrInit(HttpContext);
        }

        private static bool IsValidEmail(string email)
        {
            try { _ = new MailAddress(email); return true; }
            catch { return false; }
        }

        private static bool IsVariantActive(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Active", StringComparison.OrdinalIgnoreCase);
        }
    }
}