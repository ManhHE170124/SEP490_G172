using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Cart;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Keytietkiem.Tests.StorefrontCart
{
    public class StorefrontCartController_Checkout_Tests
    {
        #region Helpers

        private static string? GetMessage(object value)
        {
            var prop = value.GetType().GetProperty("message");
            return prop?.GetValue(value)?.ToString();
        }

        /// <summary>
        /// Guest (không login) – dùng header X-Guest-Cart-Id.
        /// </summary>
        private static (
            StorefrontCartController controller,
            Mock<PayOSService> payOsMock,
            IMemoryCache cache,
            DbContextOptions<KeytietkiemDbContext> dbOptions,
            string cartCacheKey) CreateGuestSut(
                string dbName,
                string? frontendBaseUrl = "https://fe.from.config")
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                // Ignore transaction warning for InMemory provider
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var dbFactoryMock = new Mock<IDbContextFactory<KeytietkiemDbContext>>();
            dbFactoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new KeytietkiemDbContext(options));

            var cache = new MemoryCache(new MemoryCacheOptions());

            var accountService = Mock.Of<IAccountService>();

            // Build config trước để dùng cho cả PayOSService và controller
            var configBuilder = new ConfigurationBuilder();
            if (frontendBaseUrl != null)
            {
                configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["PayOS:FrontendBaseUrl"] = frontendBaseUrl
                    });
            }

            var config = configBuilder.Build();

            // Tạo Mock<PayOSService> với đúng constructor arguments
            var httpClient = new HttpClient();
            var logger = Mock.Of<ILogger<PayOSService>>();

            var payOsMock = new Mock<PayOSService>(httpClient, logger, config);

            // Mặc định: luôn trả về 1 URL giả
            payOsMock
                .Setup(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync("https://pay.test/checkout");

            var controller = new StorefrontCartController(
                dbFactoryMock.Object,
                cache,
                accountService,
                payOsMock.Object,
                config);

            var httpContext = new DefaultHttpContext();
            var guestId = "guest-" + dbName;
            httpContext.Request.Headers["X-Guest-Cart-Id"] = guestId;

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var cartCacheKey = $"cart:anon:{guestId}";
            return (controller, payOsMock, cache, options, cartCacheKey);
        }

        /// <summary>
        /// User đã đăng nhập – có / không có email claim.
        /// </summary>
        private static (
            StorefrontCartController controller,
            Guid userId,
            Mock<PayOSService> payOsMock,
            IMemoryCache cache,
            DbContextOptions<KeytietkiemDbContext> dbOptions,
            string cartCacheKey) CreateAuthenticatedSut(
                string dbName,
                bool includeEmailClaim,
                string? frontendBaseUrl = "https://fe.auth.local")
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                // Ignore transaction warning for InMemory provider
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var dbFactoryMock = new Mock<IDbContextFactory<KeytietkiemDbContext>>();
            dbFactoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new KeytietkiemDbContext(options));

            var cache = new MemoryCache(new MemoryCacheOptions());
            var accountService = Mock.Of<IAccountService>();

            // Build config trước
            var configBuilder = new ConfigurationBuilder();
            if (frontendBaseUrl != null)
            {
                configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["PayOS:FrontendBaseUrl"] = frontendBaseUrl
                    });
            }

            var config = configBuilder.Build();

            // Mock PayOSService với ctor đầy đủ
            var httpClient = new HttpClient();
            var logger = Mock.Of<ILogger<PayOSService>>();

            var payOsMock = new Mock<PayOSService>(httpClient, logger, config);
            payOsMock
                .Setup(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync("https://pay.test/checkout");

            var controller = new StorefrontCartController(
                dbFactoryMock.Object,
                cache,
                accountService,
                payOsMock.Object,
                config);

            var userId = Guid.NewGuid();
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            if (includeEmailClaim)
            {
                claims.Add(new Claim(ClaimTypes.Email, "jwt@test.com"));
            }

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")),
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var cartCacheKey = $"cart:user:{userId:D}";
            return (controller, userId, payOsMock, cache, options, cartCacheKey);
        }

        private static async Task SeedNonSharedVariantAsync(
            DbContextOptions<KeytietkiemDbContext> options,
            Guid productId,
            Guid variantId,
            int stockQty,
            decimal sellPrice = 100_000m,
            decimal listPrice = 100_000m)
        {
            using var ctx = new KeytietkiemDbContext(options);

            var productCode = "P-" + productId.ToString("N").Substring(0, 8);
            var slug = "test-product-" + productId.ToString("N").Substring(0, 8);

            var product = new Product
            {
                ProductId = productId,
                ProductCode = productCode,              // REQUIRED
                Slug = slug,                            // REQUIRED
                ProductName = "Test product",
                ProductType = "PERSONAL_KEY",
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };

            var variant = new ProductVariant
            {
                VariantId = variantId,
                ProductId = productId,
                Product = product,
                Title = "Variant",
                StockQty = stockQty,
                SellPrice = sellPrice,
                ListPrice = listPrice,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };

            ctx.Products.Add(product);
            ctx.ProductVariants.Add(variant);
            await ctx.SaveChangesAsync();
        }

        private static async Task AddItemAsync(
            StorefrontCartController controller,
            Guid variantId,
            int quantity)
        {
            var addResult = await controller.AddItem(new AddToCartRequestDto
            {
                VariantId = variantId,
                Quantity = quantity
            });

            Assert.IsType<OkObjectResult>(addResult.Result);
        }

        #endregion

        // =========================================================
        // 1. Cart trống => 400, không gọi PayOS, cart vẫn giữ trong cache
        // =========================================================
        [Fact]
        public async Task Checkout_Guest_EmptyCart_ReturnsBadRequest_AndDoesNotCallPayOS()
        {
            var (controller, payOsMock, cache, options, cartKey) =
                CreateGuestSut(nameof(Checkout_Guest_EmptyCart_ReturnsBadRequest_AndDoesNotCallPayOS));

            var result = await controller.Checkout();

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Giỏ hàng đang trống.", GetMessage(badReq.Value!));

            payOsMock.Verify(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never);

            // cart vẫn tồn tại (không ClearCart)
            Assert.True(cache.TryGetValue(cartKey, out _));

            using var ctx = new KeytietkiemDbContext(options);
            Assert.Empty(ctx.Payments);
        }

        // =========================================================
        // 2. Guest – cart có item nhưng không có ReceiverEmail => 400
        // =========================================================
        [Fact]
        public async Task Checkout_Guest_EmailRequired_WhenReceiverEmailMissing()
        {
            var (controller, payOsMock, cache, options, cartKey) =
                CreateGuestSut(nameof(Checkout_Guest_EmailRequired_WhenReceiverEmailMissing));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 5);
            await AddItemAsync(controller, variantId, quantity: 1);

            var result = await controller.Checkout();

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Email nhận hàng không được để trống.", GetMessage(badReq.Value!));

            payOsMock.Verify(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never);

            Assert.True(cache.TryGetValue(cartKey, out _));

            using var ctx = new KeytietkiemDbContext(options);
            Assert.Empty(ctx.Payments);
        }

        // =========================================================
        // 3. Guest – ReceiverEmail không rỗng nhưng invalid => 400
        // =========================================================
        [Fact]
        public async Task Checkout_Guest_InvalidEmail_ReturnsBadRequest()
        {
            var (controller, payOsMock, cache, options, cartKey) =
                CreateGuestSut(nameof(Checkout_Guest_InvalidEmail_ReturnsBadRequest));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 5);
            await AddItemAsync(controller, variantId, quantity: 1);

            // set email sai format
            var setEmailResult = controller.SetReceiverEmail(
                new SetCartReceiverEmailRequestDto { ReceiverEmail = "not-an-email" });
            Assert.IsType<OkObjectResult>(setEmailResult.Result);

            var result = await controller.Checkout();

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Email nhận hàng không hợp lệ.", GetMessage(badReq.Value!));

            payOsMock.Verify(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never);

            Assert.True(cache.TryGetValue(cartKey, out _));

            using var ctx = new KeytietkiemDbContext(options);
            Assert.Empty(ctx.Payments);
        }

        // =========================================================
        // 4. Guest – email OK nhưng totalAmount <= 0 (UnitPrice < 0) => 400
        // =========================================================
        [Fact]
        public async Task Checkout_Guest_TotalAmountLessOrEqualZero_ReturnsBadRequest()
        {
            var (controller, payOsMock, cache, options, cartKey) =
                CreateGuestSut(nameof(Checkout_Guest_TotalAmountLessOrEqualZero_ReturnsBadRequest));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            // SellPrice & ListPrice đều âm => vào nhánh clamp 0, tổng tiền = 0
            await SeedNonSharedVariantAsync(
                options,
                productId,
                variantId,
                stockQty: 5,
                sellPrice: -50_000m,
                listPrice: -60_000m);

            await AddItemAsync(controller, variantId, quantity: 1);

            var emailResult = controller.SetReceiverEmail(
                new SetCartReceiverEmailRequestDto { ReceiverEmail = "buyer@test.com" });
            Assert.IsType<OkObjectResult>(emailResult.Result);

            var result = await controller.Checkout();

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Số tiền thanh toán không hợp lệ.", GetMessage(badReq.Value!));

            payOsMock.Verify(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never);

            Assert.True(cache.TryGetValue(cartKey, out _));

            using var ctx = new KeytietkiemDbContext(options);
            Assert.Empty(ctx.Payments);
        }

        // =========================================================
        // 5. Guest – happy path:
        //    - email rất dài (>254) -> bị truncate
        //    - FrontendBaseUrl null -> dùng default https://keytietkiem.com
        //    - PayOS được gọi đúng, snapshot & clear cart OK
        // =========================================================
        [Fact]
        public async Task Checkout_Guest_Success_DefaultFrontendUrl_AndSnapshotCreated()
        {
            var (controller, payOsMock, cache, options, cartKey) =
                CreateGuestSut(nameof(Checkout_Guest_Success_DefaultFrontendUrl_AndSnapshotCreated),
                               frontendBaseUrl: null); // để dùng default

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            // 1 item, giá dương -> thành công
            await SeedNonSharedVariantAsync(
                options,
                productId,
                variantId,
                stockQty: 5,
                sellPrice: 123.60m,
                listPrice: 150m);

            await AddItemAsync(controller, variantId, quantity: 1);

            // Tạo email rất dài nhưng vẫn hợp lệ
            var localPart = new string('a', 60);
            var label = new string('b', 50);
            var domain = string.Join(".", Enumerable.Repeat(label, 4)) + ".com"; // domain length ~207
            var longEmail = $"{localPart}@{domain}";
            Assert.True(longEmail.Length > 254);

            var setEmailResult = controller.SetReceiverEmail(
                new SetCartReceiverEmailRequestDto { ReceiverEmail = longEmail });
            Assert.IsType<OkObjectResult>(setEmailResult.Result);

            int capturedAmountInt = 0;
            string? capturedReturnUrl = null;
            string? capturedCancelUrl = null;

            payOsMock
                .Setup(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Callback<int, int, string, string, string, string, string, string>(
                    (orderCode, amountInt, desc, returnUrl, cancelUrl, phone, name, email) =>
                    {
                        capturedAmountInt = amountInt;
                        capturedReturnUrl = returnUrl;
                        capturedCancelUrl = cancelUrl;
                    })
                .ReturnsAsync("https://pay.test/checkout");

            var actionResult = await controller.Checkout();
            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var dto = Assert.IsType<CartCheckoutResultDto>(ok.Value);

            // Tính totalAmount mong đợi
            var expectedTotal = 123.60m; // 1 item
            Assert.Equal(Math.Round(expectedTotal, 2), dto.Amount);

            // amountInt dùng round 0 chữ số thập phân
            var expectedAmountInt = (int)Math.Round(expectedTotal, 0, MidpointRounding.AwayFromZero);
            Assert.Equal(expectedAmountInt, capturedAmountInt);

            // FrontendBaseUrl null -> fallback https://keytietkiem.com
            Assert.StartsWith("https://keytietkiem.com/cart/payment-result", capturedReturnUrl);
            Assert.StartsWith("https://keytietkiem.com/cart/payment-cancel", capturedCancelUrl);

            // PayOS được gọi đúng 1 lần
            payOsMock.Verify(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Once);

            // Cart đã bị xoá
            Assert.False(cache.TryGetValue(cartKey, out _));

            // Snapshot theo PaymentId đã được lưu
            var itemsKey = $"cart:payment:{dto.PaymentId:D}:items";
            var metaKey = $"cart:payment:{dto.PaymentId:D}:meta";

            Assert.True(cache.TryGetValue(itemsKey, out var itemsObj));
            Assert.True(cache.TryGetValue(metaKey, out var metaObj));

            var items = Assert.IsType<List<StorefrontCartItemDto>>(itemsObj);
            Assert.Single(items);
            Assert.Equal(variantId, items[0].VariantId);

            var meta = Assert.IsType<ValueTuple<Guid?, string>>(metaObj);
            Assert.Null(meta.Item1); // guest -> UserId null
            Assert.True(meta.Item2.Length <= 254); // email đã bị truncate

            // Kiểm tra Payment trong DB
            using var ctx = new KeytietkiemDbContext(options);
            var payment = Assert.Single(ctx.Payments);
            Assert.Equal("Pending", payment.Status);
            Assert.Equal(Math.Round(expectedTotal, 2), payment.Amount);
            Assert.Equal(dto.PaymentId, payment.PaymentId);
            Assert.True(payment.Email!.Length <= 254);
        }

        // =========================================================
        // 6. Authenticated – email lấy từ JWT claim, FrontendBaseUrl có giá trị
        // =========================================================
        [Fact]
        public async Task Checkout_Authenticated_EmailFromJwt_Success_AndCustomFrontendUrl()
        {
            var (controller, userId, payOsMock, cache, options, cartKey) =
                CreateAuthenticatedSut(
                    nameof(Checkout_Authenticated_EmailFromJwt_Success_AndCustomFrontendUrl),
                    includeEmailClaim: true,
                    frontendBaseUrl: "https://frontend.local");

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 5);
            await AddItemAsync(controller, variantId, quantity: 2); // 2 * 100000

            int capturedAmountInt = 0;
            string? capturedReturnUrl = null;
            string? capturedCancelUrl = null;

            payOsMock
                .Setup(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Callback<int, int, string, string, string, string, string, string>(
                    (orderCode, amountInt, desc, returnUrl, cancelUrl, phone, name, email) =>
                    {
                        capturedAmountInt = amountInt;
                        capturedReturnUrl = returnUrl;
                        capturedCancelUrl = cancelUrl;
                    })
                .ReturnsAsync("https://pay.test/checkout");

            var actionResult = await controller.Checkout();
            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var dto = Assert.IsType<CartCheckoutResultDto>(ok.Value);

            var expectedTotal = 2 * 100_000m;
            var expectedAmountInt = (int)Math.Round(expectedTotal, 0, MidpointRounding.AwayFromZero);
            Assert.Equal(expectedAmountInt, capturedAmountInt);

            Assert.StartsWith("https://frontend.local/cart/payment-result", capturedReturnUrl);
            Assert.StartsWith("https://frontend.local/cart/payment-cancel", capturedCancelUrl);

            // Cart bị clear, snapshot & meta lưu với UserId
            Assert.False(cache.TryGetValue(cartKey, out _));

            var itemsKey = $"cart:payment:{dto.PaymentId:D}:items";
            var metaKey = $"cart:payment:{dto.PaymentId:D}:meta";
            Assert.True(cache.TryGetValue(itemsKey, out _));
            Assert.True(cache.TryGetValue(metaKey, out var metaObj));

            var meta = Assert.IsType<ValueTuple<Guid?, string>>(metaObj);
            Assert.Equal(userId, meta.Item1);
            Assert.Equal("jwt@test.com", meta.Item2);

            using var ctx = new KeytietkiemDbContext(options);
            var payment = Assert.Single(ctx.Payments);
            Assert.Equal("jwt@test.com", payment.Email);
        }

        // =========================================================
        // 7. Authenticated – không có email claim, fallback DB Users có email => OK
        // =========================================================
        [Fact]
        public async Task Checkout_Authenticated_EmailFromDatabase_WhenJwtEmailMissing()
        {
            var (controller, userId, payOsMock, cache, options, cartKey) =
                CreateAuthenticatedSut(
                    nameof(Checkout_Authenticated_EmailFromDatabase_WhenJwtEmailMissing),
                    includeEmailClaim: false,
                    frontendBaseUrl: "https://frontend.local");

            // Seed user trong DB với email
            using (var ctxSeed = new KeytietkiemDbContext(options))
            {
                ctxSeed.Users.Add(new User
                {
                    UserId = userId,
                    Email = "db@test.com",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                });
                ctxSeed.SaveChanges();
            }

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 3);
            await AddItemAsync(controller, variantId, quantity: 1);

            var actionResult = await controller.Checkout();
            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var dto = Assert.IsType<CartCheckoutResultDto>(ok.Value);

            using var ctx = new KeytietkiemDbContext(options);
            var payment = Assert.Single(ctx.Payments);
            Assert.Equal("db@test.com", payment.Email);
            Assert.Equal(dto.PaymentId, payment.PaymentId);
        }

        // =========================================================
        // 8. Authenticated – không có email claim, DB cũng không có email => 400
        // =========================================================
        [Fact]
        public async Task Checkout_Authenticated_NoEmailInJwtOrDb_ReturnsBadRequest()
        {
            var (controller, userId, payOsMock, cache, options, cartKey) =
                CreateAuthenticatedSut(
                    nameof(Checkout_Authenticated_NoEmailInJwtOrDb_ReturnsBadRequest),
                    includeEmailClaim: false);

            // Seed user nhưng Email "trống" để business coi là chưa có email,
            // nhưng vẫn thỏa EF required (không null)
            using (var ctxSeed = new KeytietkiemDbContext(options))
            {
                ctxSeed.Users.Add(new User
                {
                    UserId = userId,
                    Email = "",            // không dùng null nữa
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                });
                ctxSeed.SaveChanges();
            }

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 3);
            await AddItemAsync(controller, variantId, quantity: 1);

            var actionResult = await controller.Checkout();

            var badReq = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            Assert.Equal("Tài khoản hiện tại chưa có email. Vui lòng cập nhật email trước khi thanh toán.",
                GetMessage(badReq.Value!));

            payOsMock.Verify(p => p.CreatePayment(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never);

            // Cart không bị xoá
            Assert.True(cache.TryGetValue(cartKey, out _));

            using var ctx = new KeytietkiemDbContext(options);
            Assert.Empty(ctx.Payments);
        }
    }
}
