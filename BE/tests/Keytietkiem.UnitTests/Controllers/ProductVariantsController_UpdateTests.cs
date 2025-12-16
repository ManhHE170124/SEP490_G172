using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class ProductVariantsController_UpdateTests
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

        private static ProductVariantUpdateDto MakeUpdateDto(
            string title = "Updated Title",
            string? variantCode = "NEWCODE",
            int? durationDays = 30,
            int stockQty = 5,
            int? warrantyDays = 7,
            string? status = "ACTIVE",
            decimal? sellPrice = null,
            decimal? listPrice = null)
        {
            return new ProductVariantUpdateDto(
                Title: title,
                VariantCode: variantCode,
                DurationDays: durationDays,
                StockQty: stockQty,
                WarrantyDays: warrantyDays,
                Thumbnail: null,
                MetaTitle: null,
                MetaDescription: null,
                Status: status,
                SellPrice: sellPrice,
                ListPrice: listPrice
            );
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

        // ================== SUCCESS PATHS ==================

        // ... (các test khác giữ nguyên như bạn đang có) ...

        /// <summary>
        /// Có ProductSection (hasSections = true) và cố đổi VariantCode => VARIANT_CODE_IN_USE_SECTION.
        /// </summary>
        [Fact]
        public async Task Update_HasSections_ChangeCode_Returns409_VARIANT_CODE_IN_USE_SECTION()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                var p = new Product
                {
                    ProductId = productId,
                    ProductCode = "P006",
                    ProductName = "Product 6",
                    ProductType = ProductEnums.SHARED_KEY,
                    Status = "ACTIVE",
                    Slug = "P006",
                    CreatedAt = DateTime.UtcNow
                };
                db.Products.Add(p);

                db.ProductVariants.Add(new ProductVariant
                {
                    VariantId = variantId,
                    ProductId = productId,
                    VariantCode = "V1",
                    Title = "Var 1",
                    DurationDays = 30,
                    StockQty = 5,
                    WarrantyDays = 7,
                    Status = "ACTIVE",
                    SellPrice = 10m,
                    ListPrice = 20m,
                    CogsPrice = 0m,
                    CreatedAt = DateTime.UtcNow
                });

                // hasSections = true  -> cần seed ProductSection với đủ field required
                db.ProductSections.Add(new ProductSection
                {
                    SectionId = Guid.NewGuid(),
                    VariantId = variantId,
                    SectionType = "NOTE",                 // REQUIRED
                    Title = "Some section",         // có thể required tùy cấu hình, set luôn cho chắc
                    Content = "<p>content</p>",       // REQUIRED
                    SortOrder = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });

                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = MakeUpdateDto(
                title: "Var 1",
                variantCode: "V2",   // đổi mã
                durationDays: 30,
                stockQty: 5,
                warrantyDays: 7,
                status: "ACTIVE"
            );

            var result = await controller.Update(productId, variantId, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var (code, _) = GetError(conflict.Value!);
            Assert.Equal("VARIANT_CODE_IN_USE_SECTION", code);
        }

        // ... (các test còn lại giữ nguyên) ...

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
                // Không ghi log trong unit test
                return Task.CompletedTask;
            }
        }
    }
}
