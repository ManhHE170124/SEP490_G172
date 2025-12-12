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

namespace Keytietkiem.Tests.Controllers
{
    public class ProductSectionsController_UpdateTests
    {
        // ========== Common helpers ==========

        private static DbContextOptions<KeytietkiemDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static (ProductSectionsController Controller, FakeClock Clock)
            CreateController(DbContextOptions<KeytietkiemDbContext> options)
        {
            var factory = new TestDbContextFactory(options);
            var clock = new FakeClock(new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
            var auditLogger = new FakeAuditLogger();

            var controller = new ProductSectionsController(factory, clock, auditLogger)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            return (controller, clock);
        }

        private static void SeedProductVariantAndSection(
            KeytietkiemDbContext db,
            Guid productId,
            Guid variantId,
            Guid sectionId,
            int sortOrder = 5,
            bool isActive = true)
        {
            db.Products.Add(new Product
            {
                ProductId = productId,
                ProductCode = "P1",
                ProductName = "Product 1",
                ProductType = ProductEnums.SHARED_KEY,
                Slug = "product-1",   // <<< THÊM SLUG ĐỂ ĐỦ REQUIRED FIELD
                Status = "INACTIVE",
                CreatedAt = DateTime.UtcNow
            });

            db.ProductVariants.Add(new ProductVariant
            {
                VariantId = variantId,
                ProductId = productId,
                VariantCode = "V1",
                Title = "Base variant",
                StockQty = 0,
                Status = "INACTIVE",
                SellPrice = 10m,
                ListPrice = 10m,
                CreatedAt = DateTime.UtcNow
            });

            db.ProductSections.Add(new ProductSection
            {
                SectionId = sectionId,
                VariantId = variantId,
                SectionType = "NOTE",
                Title = "Old title",
                Content = "<p>Old content</p>",
                SortOrder = sortOrder,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static string GetErrorCode(object value)
        {
            var type = value.GetType();
            var prop = type.GetProperty("code");
            return (string?)prop?.GetValue(value) ?? string.Empty;
        }

        // ========== SUCCESS PATHS ==========

        [Fact]
        public async Task Update_WithSortOrderProvided_UpdatesAllFields_AndReturns204()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            var sectionId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProductVariantAndSection(db, productId, variantId, sectionId, sortOrder: 3, isActive: false);
                db.SaveChanges();
            }

            var (controller, clock) = CreateController(options);

            var dto = new ProductSectionUpdateDto(
                SectionType: "warranty",          // lower-case để test normalize
                Title: "  New title  ",           // test Trim
                Content: " <p> New content </p> ",
                SortOrder: 10,
                IsActive: true
            );

            var result = await controller.Update(productId, variantId, sectionId, dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var s = Assert.Single(checkDb.ProductSections);

            Assert.Equal(sectionId, s.SectionId);
            Assert.Equal("WARRANTY", s.SectionType);   // Normalize
            Assert.Equal("New title", s.Title);        // Trim
            Assert.Contains("New content", s.Content!);
            Assert.Equal(10, s.SortOrder);             // dùng SortOrder mới
            Assert.True(s.IsActive);
            Assert.Equal(clock.UtcNow, s.UpdatedAt);
        }

        // ... (các test còn lại giữ nguyên như bạn đang có) ...

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
            public FakeClock(DateTime now) => UtcNow = now;
            public DateTime UtcNow { get; set; }
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
                // bỏ qua audit trong unit test
                return Task.CompletedTask;
            }
        }
    }
}
