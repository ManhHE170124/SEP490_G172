using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
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
    public class ProductsController_CreateTests
    {
        // ================== Helpers chung ==================

        private static DbContextOptions<KeytietkiemDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static ProductsController CreateController(DbContextOptions<KeytietkiemDbContext> options, DateTime? now = null)
        {
            var factory = new TestDbContextFactory(options);
            var clock = new FakeClock(now ?? new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
            var auditLogger = new FakeAuditLogger();

            return new ProductsController(factory, clock, auditLogger)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        private static string GetMessage(object value)
        {
            var type = value.GetType();
            var prop = type.GetProperty("message");
            return (string)(prop?.GetValue(value) ?? string.Empty);
        }

        private static void SeedCategory(KeytietkiemDbContext db, int id, string code = "cat", string name = "Category")
        {
            db.Categories.Add(new Category
            {
                CategoryId = id,
                CategoryCode = code + id,
                CategoryName = name + id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static void SeedBadge(KeytietkiemDbContext db, string code, bool isActive = true)
        {
            db.Badges.Add(new Badge
            {
                BadgeCode = code,
                DisplayName = code,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static void SeedProduct(KeytietkiemDbContext db, string code, string name)
        {
            db.Products.Add(new Product
            {
                ProductId = Guid.NewGuid(),
                ProductCode = code,
                ProductName = name,
                ProductType = ProductEnums.SHARED_KEY,
                Status = "INACTIVE",
                Slug = code,
                CreatedAt = DateTime.UtcNow
            });
        }

        // ================== SUCCESS PATHS ==================

        /// <summary>
        /// Tạo Product hợp lệ, không Category / Badge, Status mặc định OUT_OF_STOCK
        /// (totalStock = 0, dto.Status = null).
        /// </summary>
        [Fact]
        public async Task Create_Valid_NoCategoriesNoBadges_StatusOutOfStock()
        {
            var options = CreateOptions();
            var now = new DateTime(2025, 1, 2, 8, 0, 0, DateTimeKind.Utc);

            // Chuẩn bị môi trường (chỉ cần có ít nhất 1 Category / Badge trong DB cho precondition)
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, 1);
                SeedBadge(db, "HOT");
                db.SaveChanges();
            }

            var controller = CreateController(options, now);

            var dto = new ProductCreateDto(
                ProductCode: "  pro duçt-01  ",
                ProductName: "  My Product  ",
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null
            );

            var result = await controller.Create(dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var detail = Assert.IsType<ProductDetailDto>(ok.Value);

            // Normalization + trim
            Assert.Equal("PRODUCT_01", detail.ProductCode);
            Assert.Equal("My Product", detail.ProductName);
            Assert.Equal(ProductEnums.SHARED_KEY, detail.ProductType);

            // Status rule: totalStock = 0 && dto.Status null -> OUT_OF_STOCK
            Assert.Equal("OUT_OF_STOCK", detail.Status);

            Assert.Empty(detail.CategoryIds);
            Assert.Empty(detail.BadgeCodes);

            // Kiểm tra DB
            using var checkDb = new KeytietkiemDbContext(options);
            var product = Assert.Single(checkDb.Products);
            Assert.Equal("PRODUCT_01", product.ProductCode);
            Assert.Equal("My Product", product.ProductName);
            Assert.Equal("OUT_OF_STOCK", product.Status);
            Assert.Equal("PRODUCT_01", product.Slug);           // slug fallback = normalized code
            Assert.Equal(now, product.CreatedAt);
        }

        /// <summary>
        /// Tạo Product hợp lệ với CategoryIds & BadgeCodes:
        /// - CategoryIds chứa cả id hợp lệ và không tồn tại -> chỉ liên kết Category hợp lệ.
        /// - BadgeCodes chứa trộn valid/invalid/duplicate/inactive -> chỉ giữ Badge active, không trùng.
        /// - dto.Status = "INACTIVE" -> final Status = "INACTIVE" (totalStock = 0).
        /// </summary>
        [Fact]
        public async Task Create_WithCategoriesAndBadges_MapsRelationsAndRespectsInactiveStatus()
        {
            var options = CreateOptions();
            var now = new DateTime(2025, 1, 2, 9, 0, 0, DateTimeKind.Utc);

            using (var db = new KeytietkiemDbContext(options))
            {
                // Category: 1 tồn tại, 999 không có
                SeedCategory(db, 1);
                // Badge: HOT & SALE active; OLD inactive
                SeedBadge(db, "HOT", isActive: true);
                SeedBadge(db, "SALE", isActive: true);
                SeedBadge(db, "OLD", isActive: false);
                db.SaveChanges();
            }

            var controller = CreateController(options, now);

            var dto = new ProductCreateDto(
                ProductCode: "code-123",
                ProductName: "Product ABC",
                ProductType: ProductEnums.SHARED_ACCOUNT,
                Status: "INACTIVE",
                CategoryIds: new[] { 1, 999 }, // 999 không tồn tại
                BadgeCodes: new[] { " hot ", "SALE", "UNKNOWN", "OLD", "HOT" },
                Slug: "custom-slug"
            );

            var result = await controller.Create(dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var detail = Assert.IsType<ProductDetailDto>(ok.Value);

            Assert.Equal("CODE_123", detail.ProductCode);
            Assert.Equal("Product ABC", detail.ProductName);
            Assert.Equal(ProductEnums.SHARED_ACCOUNT, detail.ProductType);

            // Status rule: totalStock = 0 && dto.Status = INACTIVE -> INACTIVE
            Assert.Equal("INACTIVE", detail.Status);

            // Category mapping: chỉ id 1 được liên kết
            Assert.Contains(1, detail.CategoryIds);
            Assert.Single(detail.CategoryIds);

            // Badge mapping: chỉ HOT & SALE (active) được giữ, không trùng
            var badgeList = detail.BadgeCodes.ToList();
            Assert.Equal(2, badgeList.Count);
            Assert.Contains("HOT", badgeList);
            Assert.Contains("SALE", badgeList);

            using var checkDb = new KeytietkiemDbContext(options);
            var product = Assert.Single(checkDb.Products.Include(p => p.Categories).Include(p => p.ProductBadges));

            Assert.Equal("custom-slug", product.Slug);     // dùng slug custom
            Assert.Equal(now, product.CreatedAt);

            Assert.Single(product.Categories);
            Assert.Equal(1, product.Categories.Single().CategoryId);

            var productBadges = product.ProductBadges.ToList();
            Assert.Equal(2, productBadges.Count);
            Assert.All(productBadges, pb => Assert.Equal(product.ProductId, pb.ProductId));
            Assert.Contains(productBadges, pb => pb.Badge == "HOT");
            Assert.Contains(productBadges, pb => pb.Badge == "SALE");
        }

        // ================== ERROR PATHS ==================

        [Fact]
        public async Task Create_InvalidProductType_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new ProductCreateDto(
                ProductCode: "P001",
                ProductName: "Name",
                ProductType: "INVALID_TYPE",
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Invalid ProductType", message);
        }

        [Fact]
        public async Task Create_ProductCodeRequired_WhenNormalizedEmpty_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new ProductCreateDto(
                ProductCode: "   ",   // Sau NormalizeProductCode -> string.Empty
                ProductName: "Name",
                ProductType: ProductEnums.PERSONAL_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ProductCode is required", message);
        }

        [Fact]
        public async Task Create_ProductCodeTooLong_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var longCode = new string('A', 51); // > 50
            var dto = new ProductCreateDto(
                ProductCode: longCode,
                ProductName: "Name",
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ProductCode must not exceed 50 characters.", message);
        }

        [Fact]
        public async Task Create_ProductNameRequired_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new ProductCreateDto(
                ProductCode: "CODE",
                ProductName: "   ",
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ProductName is required", message);
        }

        [Fact]
        public async Task Create_ProductNameTooLong_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var longName = new string('X', 101); // > 100
            var dto = new ProductCreateDto(
                ProductCode: "CODE",
                ProductName: longName,
                ProductType: ProductEnums.PERSONAL_ACCOUNT,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ProductName must not exceed 100 characters.", message);
        }

        [Fact]
        public async Task Create_DuplicateProductCode_Returns409()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, "DUP_CODE", "Existing");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductCreateDto(
                ProductCode: "dup_code", // Normalize -> DUP_CODE
                ProductName: "New Name",
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null
            );

            var result = await controller.Create(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var message = GetMessage(conflict.Value!);
            Assert.Equal("ProductCode already exists", message);
        }

        [Fact]
        public async Task Create_DuplicateProductName_Returns409()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, "EXIST_CODE", "Same Name");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductCreateDto(
                ProductCode: "NEW_CODE",
                ProductName: "Same Name",      // trùng name
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null
            );

            var result = await controller.Create(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var message = GetMessage(conflict.Value!);
            Assert.Equal("ProductName already exists", message);
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
            public FakeClock(DateTime nowUtc) => UtcNow = nowUtc;
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
                // Bỏ qua ghi log trong unit test
                return Task.CompletedTask;
            }
        }
    }
}
