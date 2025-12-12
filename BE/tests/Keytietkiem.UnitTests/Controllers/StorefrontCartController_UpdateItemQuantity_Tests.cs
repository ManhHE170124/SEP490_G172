using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Cart;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.StorefrontCart
{
    public class StorefrontCartController_UpdateItemQuantity_Tests
    {
        #region Helpers

        private static (StorefrontCartController controller,
                        DbContextOptions<KeytietkiemDbContext> dbOptions)
            CreateSut(string dbName, bool authenticated = false)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            var dbFactoryMock = new Mock<IDbContextFactory<KeytietkiemDbContext>>();
            dbFactoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new KeytietkiemDbContext(options));

            var cache = new MemoryCache(new MemoryCacheOptions());
            var accountService = new Mock<IAccountService>().Object;

            // Cấu hình giả cho PayOS (chỉ để thoả ctor, UpdateItemQuantity/AddItem không gọi PayOS)
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PayOS:ClientId"] = "test-client",
                    ["PayOS:ApiKey"] = "test-api",
                    ["PayOS:ChecksumKey"] = "test-checksum",
                    ["PayOS:Endpoint"] = "https://payos.test/api"
                })
                .Build();

            var httpClient = new HttpClient();
            var logger = Mock.Of<ILogger<PayOSService>>();

            // Mock PayOSService với ctor đầy đủ, không dùng CreatePayment nên không cần Setup
            var payOsMock = new Mock<PayOSService>(httpClient, logger, config);

            var controller = new StorefrontCartController(
                dbFactoryMock.Object,
                cache,
                accountService,
                payOsMock.Object,
                config);

            var httpContext = new DefaultHttpContext();

            // Dùng header X-Guest-Cart-Id để mọi request guest dùng chung 1 cart
            httpContext.Request.Headers["X-Guest-Cart-Id"] = "test-cart-id";

            if (authenticated)
            {
                var userId = Guid.NewGuid();
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, "testuser"),
                    new Claim(ClaimTypes.Email, "user@test.com")
                };

                httpContext.User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "TestAuth"));
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return (controller, options);
        }

        private static string? GetMessage(object value)
        {
            var prop = value.GetType().GetProperty("message");
            return prop?.GetValue(value)?.ToString();
        }

        private static async Task SeedNonSharedVariantAsync(
            DbContextOptions<KeytietkiemDbContext> options,
            Guid productId,
            Guid variantId,
            int stockQty)
        {
            using var ctx = new KeytietkiemDbContext(options);

            var productCode = "P-" + productId.ToString("N").Substring(0, 8);
            var slug = "normal-product-" + productId.ToString("N").Substring(0, 8);

            var product = new Product
            {
                ProductId = productId,
                ProductCode = productCode,    // REQUIRED
                Slug = slug,                  // REQUIRED
                ProductName = "Normal product",
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
                SellPrice = 100_000,
                ListPrice = 100_000,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };

            ctx.Products.Add(product);
            ctx.ProductVariants.Add(variant);
            await ctx.SaveChangesAsync();
        }

        private static async Task SeedSharedVariantAsync(
            DbContextOptions<KeytietkiemDbContext> options,
            Guid productId,
            Guid variantId,
            int stockQty,
            int maxUsers,
            int activeCustomers)
        {
            using var ctx = new KeytietkiemDbContext(options);

            var productCode = "P-" + productId.ToString("N").Substring(0, 8);
            var slug = "shared-product-" + productId.ToString("N").Substring(0, 8);

            var product = new Product
            {
                ProductId = productId,
                ProductCode = productCode,  // REQUIRED
                Slug = slug,                // REQUIRED
                ProductName = "Shared product",
                ProductType = "SHARED_ACCOUNT",
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };

            var variant = new ProductVariant
            {
                VariantId = variantId,
                ProductId = productId,
                Product = product,
                Title = "Family",
                StockQty = stockQty,
                SellPrice = 100_000,
                ListPrice = 100_000,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };

            var account = new ProductAccount
            {
                ProductAccountId = Guid.NewGuid(),
                VariantId = variantId,
                Status = "Active",
                MaxUsers = maxUsers,
                AccountEmail = "shared@test.com",      // REQUIRED
                AccountPassword = "password123",       // REQUIRED
                ProductAccountCustomers = new List<ProductAccountCustomer>()
            };

            // Tạo một số customer đang active nếu cần
            for (int i = 0; i < activeCustomers; i++)
            {
                account.ProductAccountCustomers.Add(new ProductAccountCustomer
                {
                    ProductAccountCustomerId = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0),
                    ProductAccountId = account.ProductAccountId,
                    IsActive = true
                });
            }

            ctx.Products.Add(product);
            ctx.ProductVariants.Add(variant);
            ctx.ProductAccounts.Add(account);
            await ctx.SaveChangesAsync();
        }

        private static async Task AddItemAsync(
            StorefrontCartController controller,
            Guid variantId,
            int quantity)
        {
            var addResult = await controller.AddItem(
                new AddToCartRequestDto
                {
                    VariantId = variantId,
                    Quantity = quantity
                });

            Assert.IsType<OkObjectResult>(addResult.Result);
        }

        #endregion

        // ============ 1. Body null = BadRequest ============

        [Fact]
        public async Task UpdateItemQuantity_BodyNull_ReturnsBadRequest()
        {
            var (controller, _) =
                CreateSut(nameof(UpdateItemQuantity_BodyNull_ReturnsBadRequest));

            var result = await controller.UpdateItemQuantity(Guid.NewGuid(), null);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Request body is required.", GetMessage(badReq.Value!));
        }

        // ============ 2. Quantity < 0 = BadRequest ============

        [Fact]
        public async Task UpdateItemQuantity_QuantityNegative_ReturnsBadRequest()
        {
            var (controller, _) =
                CreateSut(nameof(UpdateItemQuantity_QuantityNegative_ReturnsBadRequest));

            var dto = new UpdateCartItemRequestDto { Quantity = -1 };

            var result = await controller.UpdateItemQuantity(Guid.NewGuid(), dto);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Quantity must be greater than or equal to 0.", GetMessage(badReq.Value!));
        }

        // ============ 3. Item không có trong cart = 404 ============

        [Fact]
        public async Task UpdateItemQuantity_ItemNotInCart_ReturnsNotFound()
        {
            var (controller, _) =
                CreateSut(nameof(UpdateItemQuantity_ItemNotInCart_ReturnsNotFound));

            var dto = new UpdateCartItemRequestDto { Quantity = 1 };
            var result = await controller.UpdateItemQuantity(Guid.NewGuid(), dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Item not found in cart.", GetMessage(notFound.Value!));
        }

        // ============ 4. Quantity == old quantity => OK, không đổi stock ============

        [Fact]
        public async Task UpdateItemQuantity_SameQuantity_NoChangeToStock()
        {
            var (controller, options) =
                CreateSut(nameof(UpdateItemQuantity_SameQuantity_NoChangeToStock));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 5);

            // Add 2 items vào cart => oldQty = 2, StockQty = 3
            await AddItemAsync(controller, variantId, quantity: 2);

            var dto = new UpdateCartItemRequestDto { Quantity = 2 };
            var result = await controller.UpdateItemQuantity(variantId, dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var cartDto = Assert.IsType<StorefrontCartDto>(ok.Value);
            Assert.Single(cartDto.Items);
            Assert.Equal(2, cartDto.Items[0].Quantity);

            using var assertCtx = new KeytietkiemDbContext(options);
            var v = assertCtx.ProductVariants.Single(x => x.VariantId == variantId);
            Assert.Equal(3, v.StockQty);   // không đổi
        }

        // ============ 5. Variant không tồn tại trong DB = 404 ============

        [Fact]
        public async Task UpdateItemQuantity_VariantNotFound_ReturnsNotFound()
        {
            var (controller, options) =
                CreateSut(nameof(UpdateItemQuantity_VariantNotFound_ReturnsNotFound));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 5);
            await AddItemAsync(controller, variantId, quantity: 1);

            // Xoá variant khỏi DB để giả lập "variant not found"
            using (var ctx = new KeytietkiemDbContext(options))
            {
                var v = ctx.ProductVariants.Single(x => x.VariantId == variantId);
                ctx.ProductVariants.Remove(v);
                await ctx.SaveChangesAsync();
            }

            var dto = new UpdateCartItemRequestDto { Quantity = 2 };

            var result = await controller.UpdateItemQuantity(variantId, dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("Variant not found.", GetMessage(notFound.Value!));
        }

        // ============ 6. newQty = 0 => xoá item, trả lại stock ============

        [Fact]
        public async Task UpdateItemQuantity_NewQuantityZero_RemovesItemAndRestoreStock()
        {
            var (controller, options) =
                CreateSut(nameof(UpdateItemQuantity_NewQuantityZero_RemovesItemAndRestoreStock));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 10);

            // Add 3 items => StockQty = 7
            await AddItemAsync(controller, variantId, quantity: 3);

            var dto = new UpdateCartItemRequestDto { Quantity = 0 };
            var result = await controller.UpdateItemQuantity(variantId, dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var cartDto = Assert.IsType<StorefrontCartDto>(ok.Value);
            Assert.Empty(cartDto.Items);

            using var assertCtx = new KeytietkiemDbContext(options);
            var v = assertCtx.ProductVariants.Single(x => x.VariantId == variantId);
            Assert.Equal(10, v.StockQty); // đã cộng lại 3
        }

        // ============ 7. Tăng quantity (delta > 0) – NON-SHARED, đủ stock ============

        [Fact]
        public async Task UpdateItemQuantity_Increase_NonShared_EnoughStock()
        {
            var (controller, options) =
                CreateSut(nameof(UpdateItemQuantity_Increase_NonShared_EnoughStock));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 10);

            // oldQty = 2, StockQty = 8
            await AddItemAsync(controller, variantId, quantity: 2);

            var dto = new UpdateCartItemRequestDto { Quantity = 5 }; // delta = +3
            var result = await controller.UpdateItemQuantity(variantId, dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var cartDto = Assert.IsType<StorefrontCartDto>(ok.Value);
            Assert.Single(cartDto.Items);
            Assert.Equal(5, cartDto.Items[0].Quantity);

            using var assertCtx = new KeytietkiemDbContext(options);
            var v = assertCtx.ProductVariants.Single(x => x.VariantId == variantId);
            Assert.Equal(5, v.StockQty); // 8 - 3
        }

        // ============ 8. Tăng quantity – NON-SHARED, thiếu stock => 400 ============

        [Fact]
        public async Task UpdateItemQuantity_Increase_NonShared_InsufficientStock_ReturnsBadRequest()
        {
            var (controller, options) =
                CreateSut(nameof(UpdateItemQuantity_Increase_NonShared_InsufficientStock_ReturnsBadRequest));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 3);

            // oldQty = 2, StockQty = 1
            await AddItemAsync(controller, variantId, quantity: 2);

            var dto = new UpdateCartItemRequestDto { Quantity = 5 }; // delta = +3 > stock(1)
            var result = await controller.UpdateItemQuantity(variantId, dto);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Số lượng tồn kho không đủ. Chỉ còn 1 sản phẩm.", GetMessage(badReq.Value!));
        }

        // ============ 9. Giảm quantity (delta < 0) – NON-SHARED ============

        [Fact]
        public async Task UpdateItemQuantity_Decrease_NonShared_GiveBackStock()
        {
            var (controller, options) =
                CreateSut(nameof(UpdateItemQuantity_Decrease_NonShared_GiveBackStock));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            await SeedNonSharedVariantAsync(options, productId, variantId, stockQty: 10);

            // oldQty = 5, StockQty = 5
            await AddItemAsync(controller, variantId, quantity: 5);

            var dto = new UpdateCartItemRequestDto { Quantity = 2 }; // delta = -3
            var result = await controller.UpdateItemQuantity(variantId, dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var cartDto = Assert.IsType<StorefrontCartDto>(ok.Value);
            Assert.Single(cartDto.Items);
            Assert.Equal(2, cartDto.Items[0].Quantity);

            using var assertCtx = new KeytietkiemDbContext(options);
            var v = assertCtx.ProductVariants.Single(x => x.VariantId == variantId);
            Assert.Equal(8, v.StockQty); // 5 + 3
        }

        // ============ 10. SHARED_ACCOUNT – tăng quantity, thiếu slot => 400 ============

        [Fact]
        public async Task UpdateItemQuantity_Increase_Shared_InsufficientSlots_ReturnsBadRequest()
        {
            var (controller, options) =
                CreateSut(nameof(UpdateItemQuantity_Increase_Shared_InsufficientSlots_ReturnsBadRequest));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            // 1 account, MaxUsers = 1 => availableSlots = 1
            await SeedSharedVariantAsync(options, productId, variantId,
                stockQty: 10, maxUsers: 1, activeCustomers: 0);

            // oldQty = 1 (dùng 1 slot)
            await AddItemAsync(controller, variantId, quantity: 1);

            var dto = new UpdateCartItemRequestDto { Quantity = 3 }; // delta = +2 > availableSlots(1)
            var result = await controller.UpdateItemQuantity(variantId, dto);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Số lượng slot không đủ. Chỉ còn 1 slot.", GetMessage(badReq.Value!));
        }

        // ============ 11. SHARED_ACCOUNT – tăng quantity, đủ slot ============

        [Fact]
        public async Task UpdateItemQuantity_Increase_Shared_EnoughSlots()
        {
            var (controller, options) =
                CreateSut(nameof(UpdateItemQuantity_Increase_Shared_EnoughSlots));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            // availableSlots = 5 (MaxUsers = 5, chưa có customer active)
            await SeedSharedVariantAsync(options, productId, variantId,
                stockQty: 10, maxUsers: 5, activeCustomers: 0);

            // oldQty = 2
            await AddItemAsync(controller, variantId, quantity: 2); // StockQty -> 8

            var dto = new UpdateCartItemRequestDto { Quantity = 4 }; // delta = +2 <= availableSlots(5)
            var result = await controller.UpdateItemQuantity(variantId, dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var cartDto = Assert.IsType<StorefrontCartDto>(ok.Value);
            Assert.Single(cartDto.Items);
            Assert.Equal(4, cartDto.Items[0].Quantity);

            using var assertCtx = new KeytietkiemDbContext(options);
            var v = assertCtx.ProductVariants.Single(x => x.VariantId == variantId);
            Assert.Equal(6, v.StockQty); // 8 - 2
        }
    }
}
