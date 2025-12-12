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
    public class ProductsController_UpdateTests
    {
        // ================== Helpers chung ==================

        private static DbContextOptions<KeytietkiemDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static ProductsController CreateController(
            DbContextOptions<KeytietkiemDbContext> options,
            DateTime? now = null)
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

        private static Product SeedProduct(
            KeytietkiemDbContext db,
            Guid id,
            string code,
            string name,
            string type = ProductEnums.SHARED_KEY,
            string status = "INACTIVE")
        {
            var p = new Product
            {
                ProductId = id,
                ProductCode = code,
                ProductName = name,
                ProductType = type,
                Status = status,
                Slug = code,
                CreatedAt = DateTime.UtcNow
            };
            db.Products.Add(p);
            return p;
        }

        private static void SeedVariant(KeytietkiemDbContext db, Guid productId, int stock)
        {
            db.ProductVariants.Add(new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = productId,
                VariantCode = "V1",
                Title = "Variant",
                StockQty = stock,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            });
        }

        private static void SeedCategory(KeytietkiemDbContext db, int id)
        {
            db.Categories.Add(new Category
            {
                CategoryId = id,
                CategoryCode = "CAT" + id,
                CategoryName = "Category " + id,
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

        // ================== SUCCESS PATHS ==================

        /// <summary>
        /// Locked branch: product có biến thể -> không đổi name/code,
        /// nhưng được phép đổi type, slug, categories, badges và status.
        /// totalStock > 0 & Status = "ACTIVE" => final "ACTIVE".
        /// </summary>
        [Fact]
        public async Task Update_Locked_NoNameOrCodeChange_UpdatesMetadataCategoriesBadgesAndStatus()
        {
            var options = CreateOptions();
            var now = new DateTime(2025, 1, 3, 8, 0, 0, DateTimeKind.Utc);
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                // Target product (locked, has variant)
                var p = SeedProduct(db, productId, "LOCKED1", "Locked Product");
                SeedVariant(db, productId, 5); // totalStock > 0

                // Categories & badges
                SeedCategory(db, 1);
                SeedCategory(db, 2);
                p.Categories.Add(db.Categories.Local.First(c => c.CategoryId == 1));

                SeedBadge(db, "HOT");
                SeedBadge(db, "SALE");
                p.ProductBadges.Add(new ProductBadge
                {
                    ProductId = productId,
                    Badge = "HOT",
                    CreatedAt = DateTime.UtcNow
                });

                db.SaveChanges();
            }

            var controller = CreateController(options, now);

            var dto = new ProductUpdateDto(
                ProductName: "  Locked Product  ",       // giống tên cũ (sau Trim)
                ProductType: ProductEnums.PERSONAL_KEY,  // đổi type
                Status: "ACTIVE",                        // desired status
                CategoryIds: new[] { 2 },                // chỉ giữ category 2
                BadgeCodes: new[] { " SALE ", "UNKNOWN" },
                Slug: "locked-updated",
                ProductCode: " locked1 "                 // Normalize -> LOCKED1, giống code cũ
            );

            var result = await controller.Update(productId, dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var e = checkDb.Products
                .Include(p => p.Categories)
                .Include(p => p.ProductBadges)
                .Single(p => p.ProductId == productId);

            // Name & code giữ nguyên
            Assert.Equal("Locked Product", e.ProductName);
            Assert.Equal("LOCKED1", e.ProductCode);

            // Type, slug, UpdatedAt cập nhật
            Assert.Equal(ProductEnums.PERSONAL_KEY, e.ProductType);
            Assert.Equal("locked-updated", e.Slug);
            Assert.Equal(now, e.UpdatedAt);

            // Categories bị clear & replace bằng CategoryId=2
            var catIds = e.Categories.Select(c => c.CategoryId).ToList();
            Assert.Single(catIds);
            Assert.Contains(2, catIds);

            // Badges bị clear, chỉ giữ badge SALE (active & hợp lệ)
            var badges = e.ProductBadges.Select(b => b.Badge).ToList();
            Assert.Single(badges);
            Assert.Equal("SALE", badges[0]);

            // totalStock > 0 và Status = "ACTIVE" -> final "ACTIVE"
            Assert.Equal("ACTIVE", e.Status);
        }

        /// <summary>
        /// Unlocked branch: không có biến thể, cho phép đổi name & code,
        /// không duplicates. totalStock = 0 & Status null -> OUT_OF_STOCK.
        /// Đồng thời test categories/badges mapping (valid + invalid ids).
        /// </summary>
        [Fact]
        public async Task Update_Unlocked_ChangeNameAndCode_MapsCategoriesBadges_StatusOutOfStock()
        {
            var options = CreateOptions();
            var now = new DateTime(2025, 1, 3, 9, 0, 0, DateTimeKind.Utc);
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                // Target product (unlocked, không biến thể)
                SeedProduct(db, productId, "OLD_CODE", "Old Name");

                // Một product khác để đảm bảo không bị trùng name/code
                SeedProduct(db, Guid.NewGuid(), "OTHER", "Other Name");

                // Categories & badges trong DB
                SeedCategory(db, 1);
                SeedCategory(db, 2);
                SeedBadge(db, "HOT", isActive: true);
                SeedBadge(db, "OLD", isActive: false);

                db.SaveChanges();
            }

            var controller = CreateController(options, now);

            var dto = new ProductUpdateDto(
                ProductName: " New Name ",
                ProductType: ProductEnums.SHARED_ACCOUNT,
                Status: null,                               // desired null
                CategoryIds: new[] { 1, 999 },              // 999 không tồn tại
                BadgeCodes: new[] { " hot ", "UNKNOWN", "HOT" },
                Slug: null,
                ProductCode: " new-code "                   // Normalize -> NEW_CODE
            );

            var result = await controller.Update(productId, dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var e = checkDb.Products
                .Include(p => p.Categories)
                .Include(p => p.ProductBadges)
                .Single(p => p.ProductId == productId);

            // Đã đổi name & code
            Assert.Equal("New Name", e.ProductName);
            Assert.Equal("NEW_CODE", e.ProductCode);

            // Type update & slug giữ nguyên (vì dto.Slug = null)
            Assert.Equal(ProductEnums.SHARED_ACCOUNT, e.ProductType);
            Assert.Equal("OLD_CODE", e.Slug);
            Assert.Equal(now, e.UpdatedAt);

            // CategoryIds: chỉ id 1 hợp lệ
            var catIds = e.Categories.Select(c => c.CategoryId).ToList();
            Assert.Single(catIds);
            Assert.Contains(1, catIds);

            // BadgeCodes: chỉ HOT (active, unique)
            var badges = e.ProductBadges.Select(b => b.Badge).ToList();
            Assert.Single(badges);
            Assert.Equal("HOT", badges[0]);

            // totalStock = 0 & desired null -> OUT_OF_STOCK
            Assert.Equal("OUT_OF_STOCK", e.Status);
        }

        // ================== ERROR PATHS ==================

        [Fact]
        public async Task Update_InvalidProductType_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new ProductUpdateDto(
                ProductName: "Name",
                ProductType: "INVALID",
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null,
                ProductCode: null
            );

            var result = await controller.Update(Guid.NewGuid(), dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Invalid ProductType", message);
        }

        [Fact]
        public async Task Update_ProductNotFound_Returns404()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new ProductUpdateDto(
                ProductName: "Name",
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null,
                ProductCode: "CODE"
            );

            var result = await controller.Update(Guid.NewGuid(), dto);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Update_ProductNameRequired_Returns400()
        {
            var options = CreateOptions();
            var id = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, id, "CODE", "Old Name");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductUpdateDto(
                ProductName: "   ",
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null,
                ProductCode: null
            );

            var result = await controller.Update(id, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ProductName is required", message);
        }

        [Fact]
        public async Task Update_ProductNameTooLong_Returns400()
        {
            var options = CreateOptions();
            var id = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, id, "CODE", "Old Name");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var longName = new string('X', 101); // > 100
            var dto = new ProductUpdateDto(
                ProductName: longName,
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null,
                ProductCode: null
            );

            var result = await controller.Update(id, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ProductName must not exceed 100 characters.", message);
        }

        /// <summary>
        /// raw ProductCode có ký tự không hợp lệ -> NormalizeProductCode trả về chuỗi rỗng,
        /// dẫn tới lỗi "ProductCode is required".
        /// </summary>
        [Fact]
        public async Task Update_ProductCodeRequired_WhenNormalizedEmpty_Returns400()
        {
            var options = CreateOptions();
            var id = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, id, "OLD", "Name");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductUpdateDto(
                ProductName: "Name",
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null,
                ProductCode: "###"    // Normalize -> ""
            );

            var result = await controller.Update(id, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ProductCode is required", message);
        }

        [Fact]
        public async Task Update_ProductCodeTooLong_Returns400()
        {
            var options = CreateOptions();
            var id = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, id, "OLD", "Name");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var longCode = new string('A', 51); // > 50
            var dto = new ProductUpdateDto(
                ProductName: "Name",
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null,
                ProductCode: longCode
            );

            var result = await controller.Update(id, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ProductCode must not exceed 50 characters.", message);
        }

        /// <summary>
        /// Locked branch: product có biến thể nhưng client cố gắng đổi name/code
        /// => trả về 400 với message lock.
        /// </summary>
        [Fact]
        public async Task Update_Locked_ChangingNameOrCode_Returns400()
        {
            var options = CreateOptions();
            var id = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, id, "LOCKED", "Old Name");
                SeedVariant(db, id, 3); // có biến thể -> locked
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductUpdateDto(
                ProductName: "New Name",                    // khác name
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null,
                ProductCode: "NEW_CODE"                     // khác code
            );

            var result = await controller.Update(id, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Không thể sửa tên hoặc mã sản phẩm khi đã có biến thể thời gian hoặc FAQ.", message);
        }

        /// <summary>
        /// Unlocked: đổi name sang name bị trùng với product khác -> 409.
        /// </summary>
        [Fact]
        public async Task Update_Unlocked_DuplicateProductName_Returns409()
        {
            var options = CreateOptions();
            var id = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, id, "CODE1", "Old Name");
                SeedProduct(db, Guid.NewGuid(), "CODE2", "Dup Name");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductUpdateDto(
                ProductName: "Dup Name",                    // trùng với product khác
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null,
                ProductCode: null                           // giữ nguyên code
            );

            var result = await controller.Update(id, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var message = GetMessage(conflict.Value!);
            Assert.Equal("ProductName already exists", message);
        }

        /// <summary>
        /// Unlocked: đổi code sang code bị trùng với product khác -> 409.
        /// </summary>
        [Fact]
        public async Task Update_Unlocked_DuplicateProductCode_Returns409()
        {
            var options = CreateOptions();
            var id = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, id, "OLD_CODE", "Name1");
                SeedProduct(db, Guid.NewGuid(), "DUP_CODE", "Name2");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductUpdateDto(
                ProductName: "Name1",                       // giữ nguyên name
                ProductType: ProductEnums.SHARED_KEY,
                Status: null,
                CategoryIds: null,
                BadgeCodes: null,
                Slug: null,
                ProductCode: "dup_code"                     // Normalize -> DUP_CODE
            );

            var result = await controller.Update(id, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var message = GetMessage(conflict.Value!);
            Assert.Equal("ProductCode already exists", message);
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
