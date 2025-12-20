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
    public class BadgesController_UpdateTests
    {
        // ================== Helpers chung ==================

        private static DbContextOptions<KeytietkiemDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static BadgesController CreateController(DbContextOptions<KeytietkiemDbContext> options)
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
            var type = value.GetType();
            var prop = type.GetProperty("message");
            return (string)(prop?.GetValue(value) ?? string.Empty);
        }

        private static void SeedBadge(
            KeytietkiemDbContext db,
            string code,
            string displayName = "Badge name",
            string? color = null,
            string? icon = null,
            bool isActive = true)
        {
            db.Badges.Add(new Badge
            {
                BadgeCode = code,
                DisplayName = displayName,
                ColorHex = color,
                Icon = icon,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static void SeedProductBadges(KeytietkiemDbContext db, string badgeCode, int count)
        {
            var now = DateTime.UtcNow;
            for (int i = 0; i < count; i++)
            {
                db.ProductBadges.Add(new ProductBadge
                {
                    ProductId = Guid.NewGuid(),
                    Badge = badgeCode,
                    CreatedAt = now
                });
            }
        }

        // ================== SUCCESS PATHS ==================

        /// <summary>
        /// Cập nhật metadata, KHÔNG đổi BadgeCode. ProductBadges giữ nguyên.
        /// </summary>
        [Fact]
        public async Task Update_MetadataOnly_CodeUnchanged_UpdatesBadgeAndKeepsRelations()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "VIP", "Old Name", "#000000", " old-icon ", false);
                SeedProductBadges(db, "VIP", 2);   // badge đang được dùng bởi sản phẩm
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeUpdateDto(
                BadgeCode: " VIP ",              // có space để test Trim
                DisplayName: "  New Name  ",
                ColorHex: "#1e40af",
                Icon: "  new-icon  ",
                IsActive: true
            );

            var result = await controller.Update("VIP", dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var badge = Assert.Single(checkDb.Badges);
            Assert.Equal("VIP", badge.BadgeCode);            // không đổi code
            Assert.Equal("New Name", badge.DisplayName);     // đã Trim
            Assert.Equal("#1e40af", badge.ColorHex);
            Assert.Equal("new-icon", badge.Icon);            // đã Trim
            Assert.True(badge.IsActive);

            var productBadges = checkDb.ProductBadges.Where(pb => pb.Badge == "VIP").ToList();
            Assert.Equal(2, productBadges.Count);            // link sản phẩm giữ nguyên
        }

        /// <summary>
        /// ĐỔI BadgeCode nhưng KHÔNG có ProductBadges liên quan.
        /// Nhánh codeChanged = true, related.Count == 0.
        /// </summary>
        [Fact]
        public async Task Update_ChangeCode_NoProductRelations_RenamesBadgeOnly()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "OLD", "Old Badge", null, null, true);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeUpdateDto(
                BadgeCode: "NEW",
                DisplayName: "Old Badge",
                ColorHex: null,       // null -> không validate, lưu null
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("OLD", dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var badge = Assert.Single(checkDb.Badges);
            Assert.Equal("NEW", badge.BadgeCode);            // đã rename
            Assert.Equal("Old Badge", badge.DisplayName);
            Assert.Null(badge.ColorHex);

            Assert.Empty(checkDb.ProductBadges);             // không có link sản phẩm
        }

        /// <summary>
        /// ĐỔI BadgeCode với ProductBadges đang dùng mã cũ.
        /// Nhánh codeChanged = true, related.Count > 0.
        /// </summary>
        [Fact]
        public async Task Update_ChangeCode_WithProductRelations_MovesProductBadgesToNewCode()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "OLD", "Badge", "#000000", " icon ", true);
                SeedProductBadges(db, "OLD", 3);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeUpdateDto(
                BadgeCode: "NEW",
                DisplayName: "Badge updated",
                ColorHex: "  #ABC  ",     // hợp lệ, có Trim
                Icon: "  new-icon  ",
                IsActive: false           // đổi trạng thái
            );

            var result = await controller.Update("OLD", dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var badge = Assert.Single(checkDb.Badges);
            Assert.Equal("NEW", badge.BadgeCode);
            Assert.Equal("Badge updated", badge.DisplayName);
            Assert.Equal("#ABC", badge.ColorHex);
            Assert.Equal("new-icon", badge.Icon);
            Assert.False(badge.IsActive);

            var productBadges = checkDb.ProductBadges.ToList();
            Assert.Equal(3, productBadges.Count);
            Assert.All(productBadges, pb => Assert.Equal("NEW", pb.Badge)); // tất cả dùng mã mới
        }

        // ================== ERROR PATHS ==================

        [Fact]
        public async Task Update_BadgeNotFound_Returns404()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                // Không seed badge có code "NOT_FOUND"
                SeedBadge(db, "OTHER");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeUpdateDto(
                BadgeCode: "NEW",
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("NOT_FOUND", dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var message = GetMessage(notFound.Value!);
            Assert.Equal("Badge not found", message);
        }

        [Fact]
        public async Task Update_DisplayNameRequired_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "VIP");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeUpdateDto(
                BadgeCode: "VIP",
                DisplayName: "   ",     // chỉ whitespace
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("VIP", dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("DisplayName is required", message);
        }

        [Fact]
        public async Task Update_DisplayNameTooLong_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "VIP");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var longName = new string('A', 65); // > 64
            var dto = new BadgeUpdateDto(
                BadgeCode: "VIP",
                DisplayName: longName,
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("VIP", dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("DisplayName cannot exceed 64 characters", message);
        }

        [Fact]
        public async Task Update_BadgeCodeRequired_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "VIP");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeUpdateDto(
                BadgeCode: "   ",            // whitespace
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("VIP", dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("BadgeCode is required", message);
        }

        [Fact]
        public async Task Update_BadgeCodeContainsSpaces_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "VIP");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeUpdateDto(
                BadgeCode: "VI P",
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("VIP", dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("BadgeCode cannot contain spaces", message);
        }

        [Fact]
        public async Task Update_BadgeCodeTooLong_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "VIP");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var longCode = new string('C', 33); // > 32
            var dto = new BadgeUpdateDto(
                BadgeCode: longCode,
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("VIP", dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("BadgeCode cannot exceed 32 characters", message);
        }

        [Fact]
        public async Task Update_BadgeCodeAlreadyExists_Returns409()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "OLD");
                SeedBadge(db, "DUP");          // badge khác đã dùng code DUP
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeUpdateDto(
                BadgeCode: "DUP",              // đổi code sang DUP -> conflict
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("OLD", dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var message = GetMessage(conflict.Value!);
            Assert.Equal("BadgeCode already exists", message);
        }

        [Fact]
        public async Task Update_InvalidColorHex_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedBadge(db, "VIP");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeUpdateDto(
                BadgeCode: "VIP",
                DisplayName: "Name",
                ColorHex: "#12G",      // ký tự G không hợp lệ
                Icon: null,
                IsActive: true
            );

            var result = await controller.Update("VIP", dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ColorHex must be a valid hex color, e.g. #1e40af", message);
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
