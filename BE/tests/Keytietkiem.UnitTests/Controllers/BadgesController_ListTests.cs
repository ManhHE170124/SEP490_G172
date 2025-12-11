// BadgesController_ListTests.cs
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Keytietkiem.Tests.Controllers
{
    public class BadgesController_ListTests
    {
        // ===== Helpers =====

        private static DbContextOptions<KeytietkiemDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static void SeedBadges(KeytietkiemDbContext db)
        {
            // Tạo 4 badge với IsActive khác nhau
            var now = DateTime.UtcNow;

            db.Badges.AddRange(
                new Badge
                {
                    BadgeCode = "HOT",
                    DisplayName = "Hot Badge",
                    ColorHex = "#ff0000",
                    Icon = "fire",
                    IsActive = true,
                    CreatedAt = now
                },
                new Badge
                {
                    BadgeCode = "COLD",
                    DisplayName = "Cold Badge",
                    ColorHex = "#0000ff",
                    Icon = "snow",
                    IsActive = false,
                    CreatedAt = now
                },
                new Badge
                {
                    BadgeCode = "VIP",
                    DisplayName = "Vip Badge",
                    ColorHex = "#00ff00",
                    Icon = "star",
                    IsActive = true,
                    CreatedAt = now
                },
                new Badge
                {
                    BadgeCode = "NEW",
                    DisplayName = "New Arrival",
                    ColorHex = "#000000",
                    Icon = "new",
                    IsActive = true,
                    CreatedAt = now
                }
            );

            // Gán ProductBadges để test ProductCount
            var product1 = Guid.NewGuid();
            var product2 = Guid.NewGuid();

            db.ProductBadges.AddRange(
                new ProductBadge
                {
                    ProductId = product1,
                    Badge = "HOT",
                    CreatedAt = now
                },
                new ProductBadge
                {
                    ProductId = product2,
                    Badge = "HOT",
                    CreatedAt = now
                },
                new ProductBadge
                {
                    ProductId = product2,
                    Badge = "NEW",
                    CreatedAt = now
                }
            );

            db.SaveChanges();
        }

        private static BadgesController CreateController(DbContextOptions<KeytietkiemDbContext> options)
        {
            var factory = new TestDbContextFactory(options);
            var auditLogger = new FakeAuditLogger();
            return new BadgesController(factory, auditLogger);
        }

        /// <summary>
        /// Đọc dữ liệu từ OkObjectResult { items, total, page, pageSize }.
        /// </summary>
        private static (List<BadgeListItemDto> items, int total, int page, int pageSize)
            ReadListResult(IActionResult result)
        {
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);

            var value = ok.Value!;
            var type = value.GetType();

            var itemsObj = type.GetProperty("items")!.GetValue(value);
            var total = (int)type.GetProperty("total")!.GetValue(value)!;
            var page = (int)type.GetProperty("page")!.GetValue(value)!;
            var pageSize = (int)type.GetProperty("pageSize")!.GetValue(value)!;

            var items = Assert.IsAssignableFrom<IEnumerable<BadgeListItemDto>>(itemsObj).ToList();

            return (items, total, page, pageSize);
        }

        // ====== Tests ======

        [Fact]
        public async Task List_NoFilters_ReturnsAllSortedByNameAndCorrectProductCount()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadges(db);
            }

            var controller = CreateController(options);

            var result = await controller.List(
                keyword: null,
                active: null,
                sort: null,
                direction: null,
                page: 1,
                pageSize: 10);

            var (items, total, page, pageSize) = ReadListResult(result);

            Assert.Equal(4, total);
            Assert.Equal(4, items.Count);
            Assert.Equal(1, page);
            Assert.Equal(10, pageSize);

            // Sort theo DisplayName asc
            var displayNames = items.Select(i => i.DisplayName).ToArray();
            Assert.Equal(
                new[] { "Cold Badge", "Hot Badge", "New Arrival", "Vip Badge" },
                displayNames
            );

            // ProductCount: HOT = 2, NEW = 1, COLD/VIP = 0
            Assert.Equal(2, items.Single(i => i.BadgeCode == "HOT").ProductCount);
            Assert.Equal(1, items.Single(i => i.BadgeCode == "NEW").ProductCount);
            Assert.Equal(0, items.Single(i => i.BadgeCode == "COLD").ProductCount);
            Assert.Equal(0, items.Single(i => i.BadgeCode == "VIP").ProductCount);
        }

        [Fact]
        public async Task List_FilterByKeywordAndActiveTrue_ReturnsOnlyMatchingActiveBadges()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadges(db);
            }

            var controller = CreateController(options);

            var result = await controller.List(
                keyword: " vip ",  // có khoảng trắng + chữ thường
                active: true,
                sort: "code",
                direction: "desc",
                page: 1,
                pageSize: 10);

            var (items, total, page, pageSize) = ReadListResult(result);

            // Chỉ badge VIP (IsActive = true, chứa "vip")
            Assert.Single(items);
            Assert.Equal("VIP", items[0].BadgeCode);
            Assert.True(items[0].IsActive);
        }

        [Fact]
        public async Task List_FilterByActiveFalse_ReturnsOnlyInactiveBadges()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadges(db);
            }

            var controller = CreateController(options);

            var result = await controller.List(
                keyword: null,
                active: false,
                sort: "name",
                direction: "asc",
                page: 1,
                pageSize: 10);

            var (items, total, page, pageSize) = ReadListResult(result);

            Assert.All(items, i => Assert.False(i.IsActive));
            Assert.Contains(items, i => i.BadgeCode == "COLD");
        }

        [Fact]
        public async Task List_InvalidSortOrDirection_FallsBackToNameAscending()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadges(db);
            }

            var controller = CreateController(options);

            var result = await controller.List(
                keyword: null,
                active: null,
                sort: "unknown",
                direction: "weird",
                page: 1,
                pageSize: 10);

            var (items, total, page, pageSize) = ReadListResult(result);

            var displayNames = items.Select(i => i.DisplayName).ToArray();
            Assert.Equal(
                new[] { "Cold Badge", "Hot Badge", "New Arrival", "Vip Badge" },
                displayNames
            );
        }

        [Fact]
        public async Task List_PagingBoundaries_AreClampedAndCanReturnEmptyLastPage()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadges(db);
            }

            var controller = CreateController(options);

            // page <= 0, pageSize <= 0 => clamp
            var r1 = await controller.List(
                keyword: null,
                active: null,
                sort: null,
                direction: null,
                page: 0,
                pageSize: 0);

            var (items1, total1, page1, pageSize1) = ReadListResult(r1);
            Assert.Equal(1, page1);
            Assert.Equal(1, pageSize1);
            Assert.Equal(1, items1.Count); // 1 record vì pageSize = 1
            Assert.Equal(4, total1);

            // pageSize > 200 => clamp 200
            var r2 = await controller.List(
                keyword: null,
                active: null,
                sort: null,
                direction: null,
                page: 1,
                pageSize: 1000);

            var (_, _, _, pageSize2) = ReadListResult(r2);
            Assert.Equal(200, pageSize2);

            // page lớn hơn số trang thực tế => list rỗng nhưng total vẫn đúng
            var r3 = await controller.List(
                keyword: null,
                active: null,
                sort: null,
                direction: null,
                page: 10,
                pageSize: 2);

            var (items3, total3, page3, pageSize3) = ReadListResult(r3);
            Assert.Empty(items3);
            Assert.Equal(4, total3);
            Assert.Equal(10, page3);
            Assert.Equal(2, pageSize3);
        }

        // ===== Inner test helpers =====

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
                // Không làm gì – chỉ để satisfy constructor
                return Task.CompletedTask;
            }
        }
    }
}
