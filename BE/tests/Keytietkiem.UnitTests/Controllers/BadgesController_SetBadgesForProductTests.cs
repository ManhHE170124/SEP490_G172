// BadgesController_SetBadgesForProductTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Keytietkiem.Tests.Controllers
{
    public class BadgesController_SetBadgesForProductTests
    {
        // ===== Common helpers =====

        private static DbContextOptions<KeytietkiemDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static BadgesController CreateController(
            DbContextOptions<KeytietkiemDbContext> options)
        {
            var factory = new TestDbContextFactory(options);
            var auditLogger = new FakeAuditLogger();

            return new BadgesController(factory, auditLogger)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        private static string GetMessage(object value)
        {
            var t = value.GetType();
            var p = t.GetProperty("message");
            return (string)(p?.GetValue(value) ?? string.Empty);
        }

        private static void SeedProduct(KeytietkiemDbContext db, Guid productId, string name = "Product 1")
        {
            db.Products.Add(new Product
            {
                ProductId = productId,
                ProductCode = "P-" + productId.ToString("N").Substring(0, 8),
                ProductName = name,
                ProductType = "KEY",                 // hoặc ProductEnums.PERSONAL_KEY tuỳ model
                Slug = "product-" + productId.ToString("N").Substring(0, 8),
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            });
        }

        private static void SeedBadges(KeytietkiemDbContext db)
        {
            var now = DateTime.UtcNow;

            db.Badges.AddRange(
                new Badge
                {
                    BadgeCode = "HOT",
                    DisplayName = "Hot",
                    IsActive = true,
                    CreatedAt = now
                },
                new Badge
                {
                    BadgeCode = "NEW",
                    DisplayName = "New",
                    IsActive = true,
                    CreatedAt = now
                },
                new Badge
                {
                    BadgeCode = "OLD",
                    DisplayName = "Old",
                    IsActive = false,   // inactive
                    CreatedAt = now
                }
            );
        }

        // ====== TC1: Product not found ======

        [Fact]
        public async Task SetBadgesForProduct_ProductNotFound_Returns404()
        {
            var options = CreateOptions();

            // Chỉ seed badge, không seed product với id này
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadges(db);
                db.SaveChanges();
            }

            var controller = CreateController(options);
            var productId = Guid.NewGuid();

            var result = await controller.SetBadgesForProduct(productId, new[] { "HOT" });

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var message = GetMessage(notFound.Value!);
            Assert.Equal("Product not found", message);

            // Đảm bảo DB không thay đổi ProductBadges
            using var checkDb = new KeytietkiemDbContext(options);
            Assert.Empty(checkDb.ProductBadges);
        }

        // ====== TC2: Product chưa có badge, list codes phức tạp -> gán non-empty ======

        [Fact]
        public async Task SetBadgesForProduct_NoExistingBadges_AssignsUniqueActiveBadges()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                SeedBadges(db);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var codes = new[]
            {
                " hot ",      // active, có khoảng trắng + chữ thường
                "HOT",        // duplicate khác case
                "new",        // active khác case
                "OLD",        // inactive
                "UNKNOWN",    // không tồn tại
                "   "         // whitespace
            };

            var result = await controller.SetBadgesForProduct(productId, codes);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var productBadges = checkDb.ProductBadges
                .Where(pb => pb.ProductId == productId)
                .ToList();

            // Chỉ HOT và NEW (active) được dùng, mỗi cái 1 lần
            Assert.Equal(2, productBadges.Count);
            var codesAssigned = productBadges.Select(pb => pb.Badge).OrderBy(c => c).ToArray();
            Assert.Equal(new[] { "HOT", "NEW" }, codesAssigned);
        }

        // ====== TC3: Product đang có badge, codes == null -> clear hết ======

        [Fact]
        public async Task SetBadgesForProduct_HasExistingBadges_NullCodes_ClearsAllBadges()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();
            var otherProductId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                SeedProduct(db, otherProductId, "Other product");
                SeedBadges(db);

                // Product chính có 2 badge
                db.ProductBadges.AddRange(
                    new ProductBadge
                    {
                        ProductId = productId,
                        Badge = "HOT",
                        CreatedAt = DateTime.UtcNow
                    },
                    new ProductBadge
                    {
                        ProductId = productId,
                        Badge = "NEW",
                        CreatedAt = DateTime.UtcNow
                    });

                // Sản phẩm khác cũng có badge, để chắc chắn không bị ảnh hưởng
                db.ProductBadges.Add(new ProductBadge
                {
                    ProductId = otherProductId,
                    Badge = "HOT",
                    CreatedAt = DateTime.UtcNow
                });

                db.SaveChanges();
            }

            var controller = CreateController(options);

            IEnumerable<string>? codes = null;
            var result = await controller.SetBadgesForProduct(productId, codes);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);

            // Product chính: không còn badge nào
            var mainBadges = checkDb.ProductBadges
                .Where(pb => pb.ProductId == productId)
                .ToList();
            Assert.Empty(mainBadges);

            // Product khác vẫn giữ nguyên
            var otherBadges = checkDb.ProductBadges
                .Where(pb => pb.ProductId == otherProductId)
                .ToList();
            Assert.Single(otherBadges);
            Assert.Equal("HOT", otherBadges[0].Badge);
        }

        // ====== TC4: Product có badge, list chỉ inactive/unknown -> clear hết ======

        [Fact]
        public async Task SetBadgesForProduct_HasExistingBadges_OnlyInactiveOrUnknown_ClearsBadges()
        {
            var options = CreateOptions();
            var productId = Guid.NewGuid();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedProduct(db, productId);
                SeedBadges(db);

                db.ProductBadges.Add(new ProductBadge
                {
                    ProductId = productId,
                    Badge = "HOT",                 // hiện đang dùng badge active
                    CreatedAt = DateTime.UtcNow
                });

                db.SaveChanges();
            }

            var controller = CreateController(options);

            var codes = new[] { "OLD", "UNKNOWN" }; // chỉ inactive + unknown

            var result = await controller.SetBadgesForProduct(productId, codes);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var badges = checkDb.ProductBadges
                .Where(pb => pb.ProductId == productId)
                .ToList();

            // Không còn badge nào vì không có active code hợp lệ trong request
            Assert.Empty(badges);
        }

        // ===== inner helper classes =====

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
                // Bỏ qua trong unit test
                return Task.CompletedTask;
            }
        }
    }
}
