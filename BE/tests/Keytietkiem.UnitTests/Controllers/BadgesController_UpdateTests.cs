// File: tests/Keytietkiem.UnitTests/Controllers/BadgesController_UpdateTests.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit test WHITE-BOX cho logic Update badge (PUT /api/badges/{code}).
    /// Mapping 1-1 với các UTC trong sheet UpdateBadge.
    /// </summary>
    public class BadgesController_UpdateTests
    {
        /// <summary>
        /// Tạo controller + factory dùng InMemory DB, seed dữ liệu tùy test.
        /// Trả về cả controller lẫn factory để có thể reopen DbContext kiểm tra DB state.
        /// </summary>
        private static (BadgesController Controller, TestDbContextFactory Factory)
            CreateController(string databaseName, Action<KeytietkiemDbContext>? seed = null)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            var factory = new TestDbContextFactory(options);

            using (var db = factory.CreateDbContext())
            {
                seed?.Invoke(db);
                db.SaveChanges();
            }

            var controller = new BadgesController(factory);
            return (controller, factory);
        }

        /// <summary>
        /// Helper lấy message từ ObjectResult có Value = new { message = "..." }.
        /// </summary>
        private static string? GetMessage(ObjectResult result)
        {
            var value = result.Value;
            var prop = value?.GetType().GetProperty("message");
            return prop?.GetValue(value)?.ToString();
        }

        // ========== UTC01 ==========
        // Happy path – badge tồn tại, không đổi code, DisplayName & IsActive update hợp lệ
        [Fact]
        public async Task Update_UTC01_ShouldReturnNoContent_WhenUpdateBasicFields_NoCodeChange()
        {
            var (controller, factory) = CreateController(
                "UpdateBadge_UTC01",
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "HOT1",
                        DisplayName = "Old name",
                        ColorHex = null,
                        Icon = null,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: "HOT1",
                DisplayName: new string('D', 64), // boundary 64
                ColorHex: null,
                Icon: null,
                IsActive: false
            );

            var result = await controller.Update("HOT1", dto);

            Assert.IsType<NoContentResult>(result);

            // Kiểm tra DB state
            using var verify = factory.CreateDbContext();
            var badge = verify.Badges.SingleOrDefault(b => b.BadgeCode == "HOT1");
            Assert.NotNull(badge);
            Assert.Equal(new string('D', 64), badge!.DisplayName);
            Assert.False(badge.IsActive);
            Assert.Null(badge.ColorHex);
            Assert.Null(badge.Icon);
        }

        // ========== UTC02 ==========
        // Badge không tồn tại theo route code
        [Fact]
        public async Task Update_UTC02_ShouldReturnNotFound_WhenBadgeDoesNotExist()
        {
            var (controller, _) = CreateController(
                "UpdateBadge_UTC02",
                seed: db =>
                {
                    // Seed 1 badge khác để chắc chắn NOTFOUND không tồn tại
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "OTHER",
                        DisplayName = "Other",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: "NOTFOUND",
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("NOTFOUND", dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Badge not found", GetMessage(notFound));
        }

        // ========== UTC03 ==========
        // DisplayName empty → "DisplayName is required"
        [Fact]
        public async Task Update_UTC03_ShouldReturnBadRequest_WhenDisplayNameEmpty()
        {
            var (controller, factory) = CreateController(
                "UpdateBadge_UTC03",
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "UPD3",
                        DisplayName = "Old name",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: "UPD3",
                DisplayName: "   ",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("UPD3", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("DisplayName is required", GetMessage(bad));

            // DB không đổi
            using var verify = factory.CreateDbContext();
            var badge = verify.Badges.Single(b => b.BadgeCode == "UPD3");
            Assert.Equal("Old name", badge.DisplayName);
        }

        // ========== UTC04 ==========
        // DisplayName len > 64
        [Fact]
        public async Task Update_UTC04_ShouldReturnBadRequest_WhenDisplayNameTooLong()
        {
            var (controller, factory) = CreateController(
                "UpdateBadge_UTC04",
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "UPD4",
                        DisplayName = "Old name",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: "UPD4",
                DisplayName: new string('D', 65),
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("UPD4", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("DisplayName cannot exceed 64 characters", GetMessage(bad));

            using var verify = factory.CreateDbContext();
            var badge = verify.Badges.Single(b => b.BadgeCode == "UPD4");
            Assert.Equal("Old name", badge.DisplayName);
        }

        // ========== UTC05 ==========
        // New BadgeCode empty / whitespace
        [Fact]
        public async Task Update_UTC05_ShouldReturnBadRequest_WhenBadgeCodeEmpty()
        {
            var (controller, factory) = CreateController(
                "UpdateBadge_UTC05",
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "UPD5",
                        DisplayName = "Old",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: "   ",
                DisplayName: "Valid Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("UPD5", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("BadgeCode is required", GetMessage(bad));

            using var verify = factory.CreateDbContext();
            var badge = verify.Badges.Single(b => b.BadgeCode == "UPD5");
            Assert.Equal("Old", badge.DisplayName);
        }

        // ========== UTC06 ==========
        // New BadgeCode chứa space
        [Fact]
        public async Task Update_UTC06_ShouldReturnBadRequest_WhenBadgeCodeContainsSpace()
        {
            var (controller, factory) = CreateController(
                "UpdateBadge_UTC06",
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "UPD6",
                        DisplayName = "Old",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: "NEW 6",
                DisplayName: "Valid Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("UPD6", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("BadgeCode cannot contain spaces", GetMessage(bad));

            using var verify = factory.CreateDbContext();
            var badge = verify.Badges.Single(b => b.BadgeCode == "UPD6");
            Assert.Equal("Old", badge.DisplayName);
        }

        // ========== UTC07 ==========
        // New BadgeCode length > 32
        [Fact]
        public async Task Update_UTC07_ShouldReturnBadRequest_WhenBadgeCodeTooLong()
        {
            var (controller, factory) = CreateController(
                "UpdateBadge_UTC07",
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "UPD7",
                        DisplayName = "Old",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: new string('C', 33),
                DisplayName: "Valid Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("UPD7", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("BadgeCode cannot exceed 32 characters", GetMessage(bad));

            using var verify = factory.CreateDbContext();
            var badge = verify.Badges.Single(b => b.BadgeCode == "UPD7");
            Assert.Equal("Old", badge.DisplayName);
        }

        // ========== UTC08 ==========
        // New BadgeCode khác code cũ và trùng với badge khác → Conflict
        [Fact]
        public async Task Update_UTC08_ShouldReturnConflict_WhenNewBadgeCodeAlreadyExists()
        {
            var (controller, factory) = CreateController(
                "UpdateBadge_UTC08",
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "OLD8",
                        DisplayName = "Old badge",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });

                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "DUPLICATE",
                        DisplayName = "Another badge",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: "DUPLICATE",
                DisplayName: "Valid Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("OLD8", dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal("BadgeCode already exists", GetMessage(conflict));

            using var verify = factory.CreateDbContext();
            var oldBadge = verify.Badges.Single(b => b.BadgeCode == "OLD8");
            var dupBadge = verify.Badges.Single(b => b.BadgeCode == "DUPLICATE");

            Assert.Equal("Old badge", oldBadge.DisplayName);
            Assert.Equal("Another badge", dupBadge.DisplayName);
        }

        // ========== UTC09 ==========
        // ColorHex invalid
        [Fact]
        public async Task Update_UTC09_ShouldReturnBadRequest_WhenColorHexInvalid()
        {
            var (controller, factory) = CreateController(
                "UpdateBadge_UTC09",
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "UPD9",
                        DisplayName = "Old name",
                        ColorHex = "#000000",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: "UPD9",
                DisplayName: "Valid Name",
                ColorHex: "123456", // sai format
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("UPD9", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("ColorHex must be a valid hex color, e.g. #1e40af", GetMessage(bad));

            using var verify = factory.CreateDbContext();
            var badge = verify.Badges.Single(b => b.BadgeCode == "UPD9");
            Assert.Equal("#000000", badge.ColorHex);
            Assert.Equal("Old name", badge.DisplayName);
        }

        // ========== UTC10 ==========
        // Happy path – đổi BadgeCode, ColorHex valid, có ProductBadges, Icon trim, ProductBadges cập nhật
        [Fact]
        public async Task Update_UTC10_ShouldReturnNoContent_WhenCodeChangedAndProductBadgesUpdated()
        {
            var productId1 = Guid.NewGuid();
            var productId2 = Guid.NewGuid();

            var (controller, factory) = CreateController(
                "UpdateBadge_UTC10",
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "OLD10",
                        DisplayName = "Old name",
                        ColorHex = "#000000",
                        Icon = "old-icon",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });

                    db.ProductBadges.Add(new ProductBadge
                    {
                        ProductId = productId1,
                        Badge = "OLD10",
                        CreatedAt = DateTime.UtcNow
                    });

                    db.ProductBadges.Add(new ProductBadge
                    {
                        ProductId = productId2,
                        Badge = "OLD10",
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeUpdateDto(
                BadgeCode: "NEW10",
                DisplayName: "New Display",
                ColorHex: "  #1A2b3C  ",   // có space, sẽ Trim()
                Icon: "  fa-star  ",       // Trim()
                IsActive: false
            );

            var result = await controller.Update("OLD10", dto);

            Assert.IsType<NoContentResult>(result);

            using var verify = factory.CreateDbContext();

            // Badge mới
            var newBadge = verify.Badges.SingleOrDefault(b => b.BadgeCode == "NEW10");
            Assert.NotNull(newBadge);
            Assert.Equal("New Display", newBadge!.DisplayName);
            Assert.Equal("#1A2b3C", newBadge.ColorHex); // Trim + giữ nguyên chữ hoa/thường
            Assert.Equal("fa-star", newBadge.Icon);
            Assert.False(newBadge.IsActive);

            // Badge cũ không còn
            Assert.Null(verify.Badges.SingleOrDefault(b => b.BadgeCode == "OLD10"));

            // ProductBadges được cập nhật code mới
            var pbs = verify.ProductBadges.Where(pb => pb.ProductId == productId1 || pb.ProductId == productId2).ToList();
            Assert.All(pbs, pb => Assert.Equal("NEW10", pb.Badge));
        }

        /// <summary>
        /// IDbContextFactory cho test, dùng InMemory Db.
        /// </summary>
        private sealed class TestDbContextFactory : IDbContextFactory<KeytietkiemDbContext>
        {
            private readonly DbContextOptions<KeytietkiemDbContext> _options;

            public TestDbContextFactory(DbContextOptions<KeytietkiemDbContext> options)
            {
                _options = options;
            }

            public KeytietkiemDbContext CreateDbContext()
            {
                return new KeytietkiemDbContext(_options);
            }

            public ValueTask<KeytietkiemDbContext> CreateDbContextAsync(
                CancellationToken cancellationToken = default)
            {
                return new ValueTask<KeytietkiemDbContext>(CreateDbContext());
            }
        }
    }
}
