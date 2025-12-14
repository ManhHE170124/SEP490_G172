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
    public class StorefrontCartController_AddItem_Tests
    {
        #region Helpers

        private static (StorefrontCartController controller,
                       IMemoryCache cache,
                       DbContextOptions<KeytietkiemDbContext> dbOptions)
            CreateSut(string dbName, bool authenticated = false)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            // IDbContextFactory mock – mỗi lần CreateDbContextAsync() trả về 1 DbContext mới
            var dbFactoryMock = new Mock<IDbContextFactory<KeytietkiemDbContext>>();
            dbFactoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new KeytietkiemDbContext(options));

            var cache = new MemoryCache(new MemoryCacheOptions());
            var accountSvc = Mock.Of<IAccountService>();

            // Cấu hình giả cho PayOS (AddItem không dùng nhưng ctor cần non-null)
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

            // Mock PayOSService với đúng ctor, không cần Setup vì AddItem không gọi CreatePayment
            var payOsMock = new Mock<PayOSService>(httpClient, logger, config);

            var controller = new StorefrontCartController(
                dbFactoryMock.Object,
                cache,
                accountSvc,
                payOsMock.Object,
                config);

            var httpContext = new DefaultHttpContext();

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

            return (controller, cache, options);
        }

        private static string? GetMessage(object value)
        {
            var prop = value.GetType().GetProperty("message");
            return prop?.GetValue(value)?.ToString();
        }

        #endregion

        // ============ TC1: dto null = BadRequest ============

        [Fact]
        public async Task AddItem_NullBody_ReturnsBadRequest()
        {
            var (controller, _, _) = CreateSut(nameof(AddItem_NullBody_ReturnsBadRequest));

            var result = await controller.AddItem(null);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("VariantId is required.", GetMessage(badReq.Value!));
        }

        // ============ TC2: Quantity <= 0 = BadRequest ============

        [Fact]
        public async Task AddItem_QuantityNonPositive_ReturnsBadRequest()
        {
            var (controller, _, _) = CreateSut(nameof(AddItem_QuantityNonPositive_ReturnsBadRequest));

            var dto = new AddToCartRequestDto
            {
                VariantId = Guid.NewGuid(),
                Quantity = 0
            };

            var result = await controller.AddItem(dto);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Quantity must be greater than 0.", GetMessage(badReq.Value!));
        }

        // ============ TC3: Variant không tồn tại = 404 ============

        [Fact]
        public async Task AddItem_VariantNotFound_ReturnsNotFound()
        {
            var (controller, _, options) = CreateSut(nameof(AddItem_VariantNotFound_ReturnsNotFound));

            // Không seed ProductVariants
            using (var seedCtx = new KeytietkiemDbContext(options))
            {
                seedCtx.Database.EnsureCreated();
            }

            var dto = new AddToCartRequestDto
            {
                VariantId = Guid.NewGuid(),
                Quantity = 1
            };

            var result = await controller.AddItem(dto);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        // ============ TC4: Non-SHARED, StockQty <= 0 = BadRequest "hết hàng" ============

        [Fact]
        public async Task AddItem_NonShared_OutOfStock_ReturnsBadRequest()
        {
            var (controller, _, options) = CreateSut(nameof(AddItem_NonShared_OutOfStock_ReturnsBadRequest));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var productCode = "P-" + productId.ToString("N").Substring(0, 8);
                var slug = "key-product-" + productId.ToString("N").Substring(0, 8);

                var product = new Product
                {
                    ProductId = productId,
                    ProductCode = productCode,              // REQUIRED
                    Slug = slug,                            // REQUIRED
                    ProductName = "Key product",
                    ProductType = "PERSONAL_KEY",
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                var variant = new ProductVariant
                {
                    VariantId = variantId,
                    ProductId = productId,
                    Product = product,
                    Title = "1 month",
                    StockQty = 0,
                    SellPrice = 100_000,
                    ListPrice = 100_000,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                ctx.Products.Add(product);
                ctx.ProductVariants.Add(variant);
                ctx.SaveChanges();
            }

            var dto = new AddToCartRequestDto
            {
                VariantId = variantId,
                Quantity = 1
            };

            var result = await controller.AddItem(dto);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Sản phẩm đã hết hàng.", GetMessage(badReq.Value!));
        }

        // ============ TC5: Non-SHARED, StockQty < Quantity (nhưng > 0) ============

        [Fact]
        public async Task AddItem_NonShared_InsufficientStock_ReturnsBadRequest()
        {
            var (controller, _, options) = CreateSut(nameof(AddItem_NonShared_InsufficientStock_ReturnsBadRequest));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var productCode = "P-" + productId.ToString("N").Substring(0, 8);
                var slug = "key-product-" + productId.ToString("N").Substring(0, 8);

                var product = new Product
                {
                    ProductId = productId,
                    ProductCode = productCode,
                    Slug = slug,
                    ProductName = "Key product",
                    ProductType = "PERSONAL_KEY",
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                var variant = new ProductVariant
                {
                    VariantId = variantId,
                    ProductId = productId,
                    Product = product,
                    Title = "1 month",
                    StockQty = 1,
                    SellPrice = 100_000,
                    ListPrice = 100_000,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                ctx.Products.Add(product);
                ctx.ProductVariants.Add(variant);
                ctx.SaveChanges();
            }

            var dto = new AddToCartRequestDto
            {
                VariantId = variantId,
                Quantity = 2
            };

            var result = await controller.AddItem(dto);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Số lượng tồn kho không đủ. Chỉ còn 1 sản phẩm.", GetMessage(badReq.Value!));
        }

        // ============ TC6: Non-SHARED, đủ kho, variant CHƯA có trong cart ============

        [Fact]
        public async Task AddItem_NonShared_Success_AddNewItem_ReducesStock()
        {
            var (controller, cache, options) =
                CreateSut(nameof(AddItem_NonShared_Success_AddNewItem_ReducesStock));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var productCode = "P-" + productId.ToString("N").Substring(0, 8);
                var slug = "key-product-" + productId.ToString("N").Substring(0, 8);

                var product = new Product
                {
                    ProductId = productId,
                    ProductCode = productCode,
                    Slug = slug,
                    ProductName = "Key product",
                    ProductType = "PERSONAL_KEY",
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                var variant = new ProductVariant
                {
                    VariantId = variantId,
                    ProductId = productId,
                    Product = product,
                    Title = "1 month",
                    StockQty = 5,
                    SellPrice = 100_000,
                    ListPrice = 100_000,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                ctx.Products.Add(product);
                ctx.ProductVariants.Add(variant);
                ctx.SaveChanges();
            }

            var dto = new AddToCartRequestDto
            {
                VariantId = variantId,
                Quantity = 2
            };

            var result = await controller.AddItem(dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var cartDto = Assert.IsType<StorefrontCartDto>(ok.Value);
            Assert.Single(cartDto.Items);
            Assert.Equal(2, cartDto.Items[0].Quantity);

            using (var assertCtx = new KeytietkiemDbContext(options))
            {
                var v = assertCtx.ProductVariants.Single(x => x.VariantId == variantId);
                Assert.Equal(3, v.StockQty); // 5 - 2
            }
        }

        // ============ TC7: Non-SHARED, đủ kho, variant ĐÃ có trong cart ============

        [Fact]
        public async Task AddItem_NonShared_Success_IncreaseExistingItem()
        {
            var (controller, cache, options) =
                CreateSut(nameof(AddItem_NonShared_Success_IncreaseExistingItem));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var productCode = "P-" + productId.ToString("N").Substring(0, 8);
                var slug = "key-product-" + productId.ToString("N").Substring(0, 8);

                var product = new Product
                {
                    ProductId = productId,
                    ProductCode = productCode,
                    Slug = slug,
                    ProductName = "Key product",
                    ProductType = "PERSONAL_KEY",
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                var variant = new ProductVariant
                {
                    VariantId = variantId,
                    ProductId = productId,
                    Product = product,
                    Title = "1 month",
                    StockQty = 10,
                    SellPrice = 100_000,
                    ListPrice = 100_000,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                ctx.Products.Add(product);
                ctx.ProductVariants.Add(variant);
                ctx.SaveChanges();
            }

            var dto1 = new AddToCartRequestDto { VariantId = variantId, Quantity = 1 };
            var dto2 = new AddToCartRequestDto { VariantId = variantId, Quantity = 2 };

            // Lần 1: tạo mới item
            await controller.AddItem(dto1);
            // Lần 2: thêm tiếp -> nhánh existing != null
            var result2 = await controller.AddItem(dto2);

            var ok = Assert.IsType<OkObjectResult>(result2.Result);
            var cartDto = Assert.IsType<StorefrontCartDto>(ok.Value);
            Assert.Single(cartDto.Items);
            Assert.Equal(3, cartDto.Items[0].Quantity);

            using (var assertCtx = new KeytietkiemDbContext(options))
            {
                var v = assertCtx.ProductVariants.Single(x => x.VariantId == variantId);
                Assert.Equal(7, v.StockQty); // 10 - 1 - 2
            }
        }

        // ============ TC8: SHARED_ACCOUNT, availableSlots <= 0 ============

        [Fact]
        public async Task AddItem_Shared_NoAvailableSlots_ReturnsBadRequest()
        {
            var (controller, _, options) =
                CreateSut(nameof(AddItem_Shared_NoAvailableSlots_ReturnsBadRequest));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var productCode = "P-" + productId.ToString("N").Substring(0, 8);
                var slug = "shared-product-" + productId.ToString("N").Substring(0, 8);

                var product = new Product
                {
                    ProductId = productId,
                    ProductCode = productCode,
                    Slug = slug,
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
                    StockQty = 0, // không quan trọng, slot tính từ ProductAccounts
                    SellPrice = 100_000,
                    ListPrice = 100_000,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                ctx.Products.Add(product);
                ctx.ProductVariants.Add(variant);
                // KHÔNG seed ProductAccounts => availableSlots = 0
                ctx.SaveChanges();
            }

            var dto = new AddToCartRequestDto
            {
                VariantId = variantId,
                Quantity = 1
            };

            var result = await controller.AddItem(dto);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Sản phẩm đã hết slot.", GetMessage(badReq.Value!));
        }

        // ============ TC9: SHARED_ACCOUNT, availableSlots < Quantity ============

        [Fact]
        public async Task AddItem_Shared_InsufficientSlots_ReturnsBadRequest()
        {
            var (controller, _, options) =
                CreateSut(nameof(AddItem_Shared_InsufficientSlots_ReturnsBadRequest));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var productCode = "P-" + productId.ToString("N").Substring(0, 8);
                var slug = "shared-product-" + productId.ToString("N").Substring(0, 8);

                var product = new Product
                {
                    ProductId = productId,
                    ProductCode = productCode,
                    Slug = slug,
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
                    StockQty = 0,
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
                    MaxUsers = 2,
                    AccountEmail = "shared@test.com",     // REQUIRED
                    AccountPassword = "password123",      // REQUIRED
                    ProductAccountCustomers = new List<ProductAccountCustomer>()
                };

                ctx.Products.Add(product);
                ctx.ProductVariants.Add(variant);
                ctx.ProductAccounts.Add(account);
                ctx.SaveChanges();
            }

            var dto = new AddToCartRequestDto
            {
                VariantId = variantId,
                Quantity = 3 // > availableSlots(2)
            };

            var result = await controller.AddItem(dto);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Số lượng slot không đủ. Chỉ còn 2 slot.", GetMessage(badReq.Value!));
        }

        // ============ TC10: SHARED_ACCOUNT, availableSlots >= Quantity ============

        [Fact]
        public async Task AddItem_Shared_Success_AddNewItem_ReducesSlots()
        {
            var (controller, _, options) =
                CreateSut(nameof(AddItem_Shared_Success_AddNewItem_ReducesSlots));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var productCode = "P-" + productId.ToString("N").Substring(0, 8);
                var slug = "shared-product-" + productId.ToString("N").Substring(0, 8);

                var product = new Product
                {
                    ProductId = productId,
                    ProductCode = productCode,
                    Slug = slug,
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
                    StockQty = 10,
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
                    MaxUsers = 3,
                    AccountEmail = "shared2@test.com",    // REQUIRED
                    AccountPassword = "password456",      // REQUIRED
                    ProductAccountCustomers = new List<ProductAccountCustomer>() // 3 slot trống
                };

                ctx.Products.Add(product);
                ctx.ProductVariants.Add(variant);
                ctx.ProductAccounts.Add(account);
                ctx.SaveChanges();
            }

            var dto = new AddToCartRequestDto
            {
                VariantId = variantId,
                Quantity = 2
            };

            var result = await controller.AddItem(dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var cartDto = Assert.IsType<StorefrontCartDto>(ok.Value);
            Assert.Single(cartDto.Items);
            Assert.Equal(2, cartDto.Items[0].Quantity);

            using (var assertCtx = new KeytietkiemDbContext(options))
            {
                var v = assertCtx.ProductVariants.Single(x => x.VariantId == variantId);
                Assert.Equal(8, v.StockQty); // 10 - 2 (slot đã “giữ chỗ”)
            }
        }
    }
}
