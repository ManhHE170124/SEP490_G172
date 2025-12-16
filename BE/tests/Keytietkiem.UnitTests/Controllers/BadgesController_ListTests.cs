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

namespace Keytietkiem.UnitTests.Controllers
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

        /// <summary>
        /// Không filter gì: trả tất cả badge, sort theo DisplayName ASC, ProductCount đúng.
        /// (Bao phủ: keyword null, active null, sort/direction default, trang hợp lệ)
        /// </summary>
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

        /// <summary>
        /// keyword chỉ chứa khoảng trắng => bị bỏ qua, kết quả giống không filter keyword.
        /// (Bao phủ: "keyword is null/empty")
        /// </summary>
        [Fact]
        public async Task List_KeywordEmptyOrWhitespace_IsTreatedAsNoFilter()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadges(db);
            }

            var controller = CreateController(options);

            var result = await controller.List(
                keyword: "   ",
                active: null,
                sort: null,
                direction: null,
                page: 1,
                pageSize: 10);

            var (items, total, page, pageSize) = ReadListResult(result);

            Assert.Equal(4, total);
            Assert.Equal(4, items.Count);
            var codes = items.Select(i => i.BadgeCode).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { "COLD", "HOT", "NEW", "VIP" }, codes);
        }

        /// <summary>
        /// Filter keyword và active = true.
        /// (Bao phủ: keyword có giá trị + active = true)
        /// </summary>
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

        /// <summary>
        /// keyword không match bất kỳ badge nào => trả list rỗng nhưng vẫn 200 OK.
        /// (Bao phủ: "badge with given code/not found" ở mức search)
        /// </summary>
        [Fact]
        public async Task List_KeywordNotFound_ReturnsEmptyListButOk()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadges(db);
            }

            var controller = CreateController(options);

            var result = await controller.List(
                keyword: "XYZ-NOT-EXIST",
                active: null,
                sort: "name",
                direction: "asc",
                page: 1,
                pageSize: 10);

            var (items, total, page, pageSize) = ReadListResult(result);

            Assert.Equal(0, total);
            Assert.Empty(items);
            Assert.Equal(1, page);
            Assert.Equal(10, pageSize);
        }

        /// <summary>
        /// active = false => chỉ trả badge Inactive.
        /// </summary>
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

        /// <summary>
        /// Sort theo code desc hợp lệ.
        /// (Bao phủ: "Sort & direction valid")
        /// </summary>
        [Fact]
        public async Task List_SortByCodeDescending_WorksCorrectly()
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
                sort: "code",
                direction: "desc",
                page: 1,
                pageSize: 10);

            var (items, total, page, pageSize) = ReadListResult(result);

            var codes = items.Select(i => i.BadgeCode).ToArray();
            Assert.Equal(new[] { "VIP", "NEW", "HOT", "COLD" }, codes);
        }

        /// <summary>
        /// sort/direction không hợp lệ => fallback về name ASC.
        /// (Bao phủ: "Sort field or direction invalid")
        /// </summary>
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

        /// <summary>
        /// Paging: page <= 0, pageSize <= 0 => bị clamp thành page = 1, pageSize = 1.
        /// (Bao phủ: page <= 0, pageSize <= 0)
        /// </summary>
        [Fact]
        public async Task List_Paging_PageAndPageSizeLessOrEqualZero_AreClampedToOne()
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
                page: 0,
                pageSize: 0);

            var (items, total, page, pageSize) = ReadListResult(result);

            Assert.Equal(1, page);
            Assert.Equal(1, pageSize);
            Assert.Equal(1, items.Count); // 1 record vì pageSize = 1
            Assert.Equal(4, total);
        }

        /// <summary>
        /// Paging: pageSize > 200 => clamp về 200.
        /// (Bao phủ: pageSize &gt;= 200)
        /// </summary>
        [Fact]
        public async Task List_Paging_PageSizeGreaterThanMax_IsClampedTo200()
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
                pageSize: 1000);

            var (_, _, page, pageSize) = ReadListResult(result);

            Assert.Equal(1, page);
            Assert.Equal(200, pageSize);
        }

        /// <summary>
        /// Paging: page lớn hơn số trang thực tế => items rỗng nhưng total vẫn đúng.
        /// (Bao phủ: page &gt;= 1, 1 &lt;= pageSize &lt;= 200, "empty last page")
        /// </summary>
        [Fact]
        public async Task List_Paging_PageGreaterThanTotalPages_ReturnsEmptyListWithCorrectTotal()
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
                page: 10,
                pageSize: 2);

            var (items, total, page, pageSize) = ReadListResult(result);

            Assert.Empty(items);
            Assert.Equal(4, total);
            Assert.Equal(10, page);
            Assert.Equal(2, pageSize);
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
