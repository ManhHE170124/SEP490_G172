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
    public class ProductSectionsController_CreateTests
    {
        // ================== Common helpers ==================

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

        private static void SeedProductAndVariant(
            KeytietkiemDbContext db,
            Guid productId,
            Guid variantId)
        {
            db.Products.Add(new Product
            {
                ProductId = productId,
                ProductCode = "P1",
                ProductName = "Product 1",
                ProductType = ProductEnums.SHARED_KEY,
                Slug = "product-1",      // <<< THÊM SLUG ĐỂ ĐỦ REQUIRED FIELD
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
        }

        private static void SeedSection(
            KeytietkiemDbContext db,
            Guid variantId,
            int sortOrder)
        {
            db.ProductSections.Add(new ProductSection
            {
                SectionId = Guid.NewGuid(),
                VariantId = variantId,
                SectionType = "WARRANTY",
                Title = "Existing section",
                Content = "<p>old</p>",
                SortOrder = sortOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static string GetErrorCode(object value)
        {
            var type = value.GetType();
            var prop = type.GetProperty("code");
            return (string?)prop?.GetValue(value) ?? string.Empty;
        }

        private static string GetErrorMessage(object value)
        {
            var type = value.GetType();
            var prop = type.GetProperty("message");
            return (string?)prop?.GetValue(value) ?? string.Empty;
        }

        // ================== SUCCESS PATHS ==================

        /// <summary>
        /// Happy path: variant tồn tại, type hợp lệ (case-insensitive),
        /// title + content hợp lệ, SortOrder = null, không có section cũ.
        /// Hệ thống auto gán SortOrder = 0, SectionType được normalize về UPPER.
        /// </summary>
        [Fact]
        public async Task Create_FirstSection_NoSort_AssignsZeroAndReturns201()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProductAndVariant(db, productId, variantId);
                db.SaveChanges();
            }

            var (controller, _) = CreateController(options);

            var dto = new ProductSectionCreateDto(
                SectionType: "warranty",          // lower-case để test normalize
                Title: "  Warranty info  ",       // có space để test Trim
                Content: "<p>Some content</p>",
                SortOrder: null,                  // auto sort
                IsActive: true
            );

            var result = await controller.Create(productId, variantId, dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(nameof(ProductSectionsController.Get), created.ActionName);

            var body = Assert.IsType<ProductSectionDetailDto>(created.Value);
            Assert.Equal("WARRANTY", body.SectionType);       // đã normalize
            Assert.Equal("Warranty info", body.Title);        // đã Trim
            Assert.Equal(0, body.SortOrder);                  // first section => 0
            Assert.True(body.IsActive);

            using var checkDb = new KeytietkiemDbContext(options);
            var section = Assert.Single(checkDb.ProductSections);
            Assert.Equal(body.SectionId, section.SectionId);
            Assert.Equal("WARRANTY", section.SectionType);
            Assert.Equal("Warranty info", section.Title);
            Assert.Equal(0, section.SortOrder);
            Assert.True(section.IsActive);
        }

        /// <summary>
        /// Variant đã có sẵn section với SortOrder tối đa = 3.
        /// Khi không gửi SortOrder, hệ thống auto gán SortOrder = max + 1 (=4).
        /// </summary>
        [Fact]
        public async Task Create_WithExistingSections_NoSort_AssignsMaxPlusOne()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProductAndVariant(db, productId, variantId);
                SeedSection(db, variantId, sortOrder: 1);
                SeedSection(db, variantId, sortOrder: 3); // max hiện tại = 3
                db.SaveChanges();
            }

            var (controller, _) = CreateController(options);

            var dto = new ProductSectionCreateDto(
                SectionType: "NOTE",
                Title: "Note section",
                Content: "<p>Note content</p>",
                SortOrder: null,                  // auto sort
                IsActive: false
            );

            var result = await controller.Create(productId, variantId, dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var body = Assert.IsType<ProductSectionDetailDto>(created.Value);

            Assert.Equal(4, body.SortOrder);     // max(1,3) + 1
            Assert.False(body.IsActive);

            using var checkDb = new KeytietkiemDbContext(options);
            var sections = checkDb.ProductSections
                                  .Where(s => s.VariantId == variantId)
                                  .OrderBy(s => s.SortOrder)
                                  .ToList();

            Assert.Equal(3, sections.Count);
            Assert.Equal(new[] { 1, 3, 4 }, sections.Select(s => s.SortOrder).ToArray());
        }

        // ================== ERROR PATHS ==================

        /// <summary>
        /// Variant không tồn tại => 404 NotFound.
        /// </summary>
        [Fact]
        public async Task Create_VariantNotFound_Returns404()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            var (controller, _) = CreateController(options);

            var dto = new ProductSectionCreateDto(
                SectionType: "WARRANTY",
                Title: "Title",
                Content: "<p>Content</p>",
                SortOrder: null,
                IsActive: true
            );

            var result = await controller.Create(productId, variantId, dto);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        /// <summary>
        /// SectionType không thuộc whitelist WARRANTY|NOTE|DETAIL => 400 + code SECTION_TYPE_INVALID.
        /// </summary>
        [Fact]
        public async Task Create_InvalidSectionType_Returns400()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProductAndVariant(db, productId, variantId);
                db.SaveChanges();
            }

            var (controller, _) = CreateController(options);

            var dto = new ProductSectionCreateDto(
                SectionType: "INVALID",
                Title: "Title",
                Content: "<p>Content</p>",
                SortOrder: null,
                IsActive: true
            );

            var result = await controller.Create(productId, variantId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var code = GetErrorCode(badRequest.Value!);
            Assert.Equal("SECTION_TYPE_INVALID", code);
        }

        /// <summary>
        /// Title thiếu / chỉ whitespace => 400 + code SECTION_TITLE_REQUIRED.
        /// </summary>
        [Fact]
        public async Task Create_MissingTitle_Returns400()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProductAndVariant(db, productId, variantId);
                db.SaveChanges();
            }

            var (controller, _) = CreateController(options);

            var dto = new ProductSectionCreateDto(
                SectionType: "WARRANTY",
                Title: "   ",                        // chỉ space
                Content: "<p>Content</p>",
                SortOrder: null,
                IsActive: true
            );

            var result = await controller.Create(productId, variantId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var code = GetErrorCode(badRequest.Value!);
            Assert.Equal("SECTION_TITLE_REQUIRED", code);
        }

        /// <summary>
        /// Title dài hơn 200 ký tự => 400 + code SECTION_TITLE_TOO_LONG.
        /// </summary>
        [Fact]
        public async Task Create_TitleTooLong_Returns400()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProductAndVariant(db, productId, variantId);
                db.SaveChanges();
            }

            var (controller, _) = CreateController(options);

            var longTitle = new string('A', 201); // > 200

            var dto = new ProductSectionCreateDto(
                SectionType: "WARRANTY",
                Title: longTitle,
                Content: "<p>Content</p>",
                SortOrder: null,
                IsActive: true
            );

            var result = await controller.Create(productId, variantId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var code = GetErrorCode(badRequest.Value!);
            Assert.Equal("SECTION_TITLE_TOO_LONG", code);
        }

        /// <summary>
        /// Content chỉ có HTML rỗng (&nbsp;) => IsHtmlBlank = true => 400 + code SECTION_CONTENT_REQUIRED.
        /// </summary>
        [Fact]
        public async Task Create_ContentBlankHtml_Returns400()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProductAndVariant(db, productId, variantId);
                db.SaveChanges();
            }

            var (controller, _) = CreateController(options);

            var dto = new ProductSectionCreateDto(
                SectionType: "NOTE",
                Title: "Note",
                Content: "<p>&nbsp;</p>",         // sau khi strip tag + &nbsp; => rỗng
                SortOrder: null,
                IsActive: true
            );

            var result = await controller.Create(productId, variantId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var code = GetErrorCode(badRequest.Value!);
            Assert.Equal("SECTION_CONTENT_REQUIRED", code);
        }

        /// <summary>
        /// SortOrder < 0 => 400 + code SECTION_SORT_INVALID.
        /// </summary>
        [Fact]
        public async Task Create_SortOrderNegative_Returns400()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProductAndVariant(db, productId, variantId);
                db.SaveChanges();
            }

            var (controller, _) = CreateController(options);

            var dto = new ProductSectionCreateDto(
                SectionType: "DETAIL",
                Title: "Detail",
                Content: "<p>Content</p>",
                SortOrder: -1,                    // invalid
                IsActive: true
            );

            var result = await controller.Create(productId, variantId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var code = GetErrorCode(badRequest.Value!);
            Assert.Equal("SECTION_SORT_INVALID", code);
        }

        // ================== Inner fakes ==================

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
                // Không ghi log trong unit test
                return Task.CompletedTask;
            }
        }
    }
}
