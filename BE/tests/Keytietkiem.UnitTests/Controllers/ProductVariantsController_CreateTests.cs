using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Keytietkiem.Tests.Controllers
{
    public class ProductVariantsController_CreateTests
    {
        // ================== Helpers chung ==================

        private static DbContextOptions<KeytietkiemDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static ProductVariantsController CreateController(
            DbContextOptions<KeytietkiemDbContext> options,
            DateTime? now = null)
        {
            var factory = new TestDbContextFactory(options);
            var clock = new FakeClock(now ?? new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
            var auditLogger = new FakeAuditLogger();

            return new ProductVariantsController(factory, clock, auditLogger)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        private static Product SeedProduct(
            KeytietkiemDbContext db,
            Guid productId,
            string code = "P001",
            string name = "Test Product")
        {
            var p = new Product
            {
                ProductId = productId,
                ProductCode = code,
                ProductName = name,
                ProductType = ProductEnums.SHARED_KEY,
                Status = "INACTIVE",
                Slug = code,
                CreatedAt = DateTime.UtcNow
            };
            db.Products.Add(p);
            return p;
        }

        private static void SeedVariant(
            KeytietkiemDbContext db,
            Guid productId,
            string variantCode,
            string title)
        {
            db.ProductVariants.Add(new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = productId,
                VariantCode = variantCode,
                Title = title,
                DurationDays = 30,
                StockQty = 5,
                WarrantyDays = 7,
                Status = "ACTIVE",
                SellPrice = 100,
                ListPrice = 150,
                CogsPrice = 0,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static (string? Code, string? Message) GetError(object value)
        {
            var type = value.GetType();
            var codeProp = type.GetProperty("code");
            var msgProp = type.GetProperty("message");

            return (
                codeProp?.GetValue(value) as string,
                msgProp?.GetValue(value) as string
            );
        }

        private static ProductVariantCreateDto CreateValidDto(
            string? title = "1 Month Key",
            string? code = "V1",
            int stockQty = 5,
            int? duration = 30,
            int? warranty = 7,
            decimal? sellPrice = 100m,
            decimal? listPrice = 150m,
            string? status = "ACTIVE")
        {
            return new ProductVariantCreateDto(
                VariantCode: code ?? "",
                Title: title ?? "",
                DurationDays: duration,
                StockQty: stockQty,
                WarrantyDays: warranty,
                Thumbnail: null,
                MetaTitle: null,
                MetaDescription: null,
                SellPrice: sellPrice,
                ListPrice: listPrice,
                Status: status
            );
        }

        // ================== SUCCESS PATHS ==================

        /// <summary>
        /// Happy path: product tồn tại, dữ liệu hợp lệ, stock > 0, Status = ACTIVE.
        /// Expect:
        /// - 201 Created với ProductVariantDetailDto
        /// - Variant được insert với Title/Code đã Trim, Status = ACTIVE
        /// - Product.Status sau RecalcProductStatus = ACTIVE, UpdatedAt = clock.UtcNow
        /// </summary>
        [Fact]
        public async Task Create_Valid_PositiveStock_StatusActive_InsertsVariant_AndProductBecomesActive()
        {
            var options = CreateOptions();
            var now = new DateTime(2025, 1, 2, 8, 0, 0, DateTimeKind.Utc);
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options, now);

            var dto = CreateValidDto(
                title: "  1 Month Key  ",
                code: "  V1 ",
                stockQty: 5,
                duration: 30,
                warranty: 7,
                sellPrice: 100m,
                listPrice: 150m,
                status: "ACTIVE"
            );

            var result = await controller.Create(productId, dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var detail = Assert.IsType<ProductVariantDetailDto>(created.Value);

            Assert.Equal(productId, detail.ProductId);
            Assert.Equal("V1", detail.VariantCode);
            Assert.Equal("1 Month Key", detail.Title);
            Assert.Equal(30, detail.DurationDays);
            Assert.Equal(5, detail.StockQty);
            Assert.Equal(7, detail.WarrantyDays);
            Assert.Equal("ACTIVE", detail.Status);
            Assert.Equal(100m, detail.SellPrice);
            Assert.Equal(150m, detail.ListPrice);
            Assert.Equal(0m, detail.CogsPrice);

            using var checkDb = new KeytietkiemDbContext(options);
            var product = checkDb.Products
                .Include(p => p.ProductVariants)
                .Single(p => p.ProductId == productId);

            Assert.Equal("ACTIVE", product.Status);
            Assert.Equal(now, product.UpdatedAt);

            var variant = Assert.Single(product.ProductVariants);
            Assert.Equal(now, variant.CreatedAt);
        }

        /// <summary>
        /// StockQty âm -> được clamp về 0.
        /// Status null -> ResolveStatusFromStock => OUT_OF_STOCK khi stock = 0.
        /// Product chỉ có 1 variant với stock 0 -> RecalcProductStatus => OUT_OF_STOCK.
        /// </summary>
        [Fact]
        public async Task Create_Valid_NegativeStock_StatusNull_ClampedToZero_AndOutOfStock()
        {
            var options = CreateOptions();
            var now = new DateTime(2025, 1, 2, 9, 0, 0, DateTimeKind.Utc);
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options, now);

            var dto = CreateValidDto(
                stockQty: -10,
                duration: null,
                warranty: null,
                sellPrice: 50m,
                listPrice: 100m,
                status: null
            );

            var result = await controller.Create(productId, dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var detail = Assert.IsType<ProductVariantDetailDto>(created.Value);

            Assert.Equal(0, detail.StockQty);              // clamp
            Assert.Equal("OUT_OF_STOCK", detail.Status);    // từ ResolveStatusFromStock

            using var checkDb = new KeytietkiemDbContext(options);
            var product = checkDb.Products
                .Include(p => p.ProductVariants)
                .Single(p => p.ProductId == productId);

            Assert.Equal("OUT_OF_STOCK", product.Status);   // từ RecalcProductStatus
        }

        // ================== ERROR PATHS ==================

        [Fact]
        public async Task Create_ProductNotFound_Returns404()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = CreateValidDto();

            var result = await controller.Create(Guid.NewGuid(), dto);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task Create_TitleRequired_Returns400_TitleRequired()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(title: "   "); // chỉ whitespace

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("TITLE_REQUIRED", code);
        }

        [Fact]
        public async Task Create_TitleTooLong_Returns400_TitleTooLong()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var longTitle = new string('A', 61); // > 60
            var dto = CreateValidDto(title: longTitle);

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("TITLE_TOO_LONG", code);
        }

        [Fact]
        public async Task Create_VariantCodeRequired_Returns400_CodeRequired()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(code: "   ");

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("CODE_REQUIRED", code);
        }

        [Fact]
        public async Task Create_VariantCodeTooLong_Returns400_CodeTooLong()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var longCode = new string('C', 51); // > 50
            var dto = CreateValidDto(code: longCode);

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("CODE_TOO_LONG", code);
        }

        [Fact]
        public async Task Create_DurationNegative_Returns400_DurationInvalid()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(duration: -1, warranty: null);

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("DURATION_INVALID", code);
        }

        [Fact]
        public async Task Create_DurationLessOrEqualWarranty_Returns400_DurationLeWarranty()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(duration: 30, warranty: 30);

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("DURATION_LE_WARRANTY", code);
        }

        [Fact]
        public async Task Create_SellPriceRequired_Returns400_SellPriceRequired()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(sellPrice: null, listPrice: 100m);

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("SELL_PRICE_REQUIRED", code);
        }

        [Fact]
        public async Task Create_ListPriceRequired_Returns400_ListPriceRequired()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(sellPrice: 50m, listPrice: null);

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("LIST_PRICE_REQUIRED", code);
        }

        [Fact]
        public async Task Create_SellPriceNegative_Returns400_SellPriceInvalid()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(sellPrice: -1m, listPrice: 10m);

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("SELL_PRICE_INVALID", code);
        }

        [Fact]
        public async Task Create_SellPriceGreaterThanListPrice_Returns400_SellGtList()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(sellPrice: 200m, listPrice: 100m);

            var result = await controller.Create(productId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var (code, _) = GetError(badRequest.Value!);
            Assert.Equal("SELL_GT_LIST", code);
        }

        [Fact]
        public async Task Create_DuplicateTitleInSameProduct_Returns409_VariantTitleDuplicate()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                var p = SeedProduct(db, productId);
                SeedVariant(db, productId, variantCode: "V1", title: "1 Month Key");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(
                title: "1 month key",   // khác hoa thường nhưng case-insensitive
                code: "V2"
            );

            var result = await controller.Create(productId, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var (code, _) = GetError(conflict.Value!);
            Assert.Equal("VARIANT_TITLE_DUPLICATE", code);
        }

        [Fact]
        public async Task Create_DuplicateVariantCodeInSameProduct_Returns409_VariantCodeDuplicate()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                var p = SeedProduct(db, productId);
                SeedVariant(db, productId, variantCode: "V1", title: "Key A");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = CreateValidDto(
                title: "Key B",
                code: "v1"   // trùng code (case-insensitive)
            );

            var result = await controller.Create(productId, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var (code, _) = GetError(conflict.Value!);
            Assert.Equal("VARIANT_CODE_DUPLICATE", code);
        }

        // ================== Helper inner classes ==================

        private sealed class TestDbContextFactory : IDbContextFactory<KeytietkiemDbContext>
        {
            private readonly DbContextOptions<KeytietkiemDbContext> _options;

            public TestDbContextFactory(DbContextOptions<KeytietkiemDbContext> options)
            {
                _options = options;
            }

            public KeytietkiemDbContext CreateDbContext()
                => new KeytietkiemDbContext(_options);

            public Task<KeytietkiemDbContext> CreateDbContextAsync(
                CancellationToken cancellationToken = default)
                => Task.FromResult(CreateDbContext());
        }

        private sealed class FakeClock : IClock
        {
            public FakeClock(DateTime utcNow) => UtcNow = utcNow;
            public DateTime UtcNow { get; }
        }

        private sealed class FakeAuditLogger : IAuditLogger
        {
            public Task LogAsync(
                HttpContext httpContext,
                string action,
                string? entityType = null,
                string? entityId = null,
                object? before = null,
                object? after = null)
            {
                // Bỏ qua log trong unit test
                return Task.CompletedTask;
            }
        }
    }
}
