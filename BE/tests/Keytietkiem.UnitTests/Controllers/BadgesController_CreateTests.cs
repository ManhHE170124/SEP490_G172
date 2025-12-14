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
    public class BadgesController_CreateTests
    {
        // ========== Helpers chung ==========

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

            var controller = new BadgesController(factory, auditLogger)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            return controller;
        }

        private static string GetMessage(object resultValue)
        {
            var type = resultValue.GetType();
            var prop = type.GetProperty("message");
            return (string)(prop?.GetValue(resultValue) ?? string.Empty);
        }

        // ========== Test cases (UTC001 – UTC010) ==========

        /// <summary>
        /// UTC001 – Normal:
        /// BadgeCode / DisplayName hợp lệ, ColorHex null/empty.
        /// Kỳ vọng: 201 Created, dữ liệu được trim & lưu thành công.
        /// </summary>
        [Fact]
        public async Task UTC001_Create_ValidWithoutColor_CreatesBadgeWithTrimmedFields()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new BadgeCreateDto(
                BadgeCode: "  VIP  ",           // leading/trailing spaces
                DisplayName: "  Vip Badge  ",
                ColorHex: null,
                Icon: "  star  ",
                IsActive: true
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal("Get", created.ActionName);

            var body = Assert.IsType<BadgeListItemDto>(created.Value);
            Assert.Equal("VIP", body.BadgeCode);
            Assert.Equal("Vip Badge", body.DisplayName);
            Assert.Null(body.ColorHex);
            Assert.Equal("star", body.Icon);
            Assert.True(body.IsActive);
            Assert.Equal(0, body.ProductCount);

            using var db = new KeytietkiemDbContext(options);
            var entity = Assert.Single(db.Badges);
            Assert.Equal("VIP", entity.BadgeCode);
            Assert.Equal("Vip Badge", entity.DisplayName);
            Assert.Null(entity.ColorHex);
            Assert.Equal("star", entity.Icon);
            Assert.True(entity.IsActive);
        }

        /// <summary>
        /// UTC002 – Boundary (length):
        /// BadgeCode dài 32 ký tự, DisplayName dài 64 ký tự, ColorHex hợp lệ (#RRGGBB).
        /// Kỳ vọng: 201 Created.
        /// </summary>
        [Fact]
        public async Task UTC002_Create_ValidMaxLengthWithColor_CreatesBadge()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var badgeCode = new string('A', 32);  // = 32
            var displayName = new string('B', 64); // = 64

            var dto = new BadgeCreateDto(
                BadgeCode: badgeCode,
                DisplayName: displayName,
                ColorHex: "#1e40af",   // valid #RRGGBB
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<BadgeListItemDto>(created.Value);

            Assert.Equal(badgeCode, body.BadgeCode);
            Assert.Equal(displayName, body.DisplayName);
            Assert.Equal("#1e40af", body.ColorHex);

            using var db = new KeytietkiemDbContext(options);
            var entity = Assert.Single(db.Badges);
            Assert.Equal(badgeCode, entity.BadgeCode);
            Assert.Equal(displayName, entity.DisplayName);
            Assert.Equal("#1e40af", entity.ColorHex);
        }

        /// <summary>
        /// UTC003 – Abnormal:
        /// BadgeCode null/empty/whitespace.
        /// Kỳ vọng: 400 "BadgeCode is required".
        /// </summary>
        [Fact]
        public async Task UTC003_Create_InvalidBadgeCode_IsRequired()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new BadgeCreateDto(
                BadgeCode: "   ",          // chỉ whitespace
                DisplayName: "Some name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("BadgeCode is required", message);
        }

        /// <summary>
        /// UTC004 – Abnormal:
        /// BadgeCode chứa khoảng trắng.
        /// Kỳ vọng: 400 "BadgeCode cannot contain spaces".
        /// </summary>
        [Fact]
        public async Task UTC004_Create_InvalidBadgeCode_ContainsSpaces()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new BadgeCreateDto(
                BadgeCode: "VIP GOLD",
                DisplayName: "Vip Gold",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("BadgeCode cannot contain spaces", message);
        }

        /// <summary>
        /// UTC005 – Boundary (abnormal):
        /// BadgeCode dài &gt; 32.
        /// Kỳ vọng: 400 "BadgeCode cannot exceed 32 characters".
        /// </summary>
        [Fact]
        public async Task UTC005_Create_InvalidBadgeCode_TooLong()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var longCode = new string('A', 33); // > 32
            var dto = new BadgeCreateDto(
                BadgeCode: longCode,
                DisplayName: "Long Code Badge",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("BadgeCode cannot exceed 32 characters", message);
        }

        /// <summary>
        /// UTC006 – Abnormal:
        /// BadgeCode đã tồn tại trong hệ thống.
        /// Kỳ vọng: 409 "BadgeCode already exists".
        /// </summary>
        [Fact]
        public async Task UTC006_Create_InvalidBadgeCode_AlreadyExists()
        {
            var options = CreateOptions();

            // Seed 1 badge với code VIP
            using (var db = new KeytietkiemDbContext(options))
            {
                db.Badges.Add(new Badge
                {
                    BadgeCode = "VIP",
                    DisplayName = "Existing VIP",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new BadgeCreateDto(
                BadgeCode: "VIP",
                DisplayName: "New VIP",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var message = GetMessage(conflict.Value!);
            Assert.Equal("BadgeCode already exists", message);
        }

        /// <summary>
        /// UTC007 – Abnormal:
        /// DisplayName null/empty/whitespace.
        /// Kỳ vọng: 400 "DisplayName is required".
        /// </summary>
        [Fact]
        public async Task UTC007_Create_InvalidDisplayName_IsRequired()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new BadgeCreateDto(
                BadgeCode: "VIP",
                DisplayName: "   ",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("DisplayName is required", message);
        }

        /// <summary>
        /// UTC008 – Boundary (abnormal):
        /// DisplayName dài &gt; 64.
        /// Kỳ vọng: 400 "DisplayName cannot exceed 64 characters".
        /// </summary>
        [Fact]
        public async Task UTC008_Create_InvalidDisplayName_TooLong()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var longName = new string('B', 65); // > 64
            var dto = new BadgeCreateDto(
                BadgeCode: "VIP",
                DisplayName: longName,
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("DisplayName cannot exceed 64 characters", message);
        }

        /// <summary>
        /// UTC009 – Abnormal:
        /// ColorHex có giá trị nhưng sai format (#RGB / #RRGGBB).
        /// Kỳ vọng: 400 "ColorHex must be a valid hex color, e.g. #1e40af".
        /// </summary>
        [Fact]
        public async Task UTC009_Create_InvalidColorHex_ReturnsBadRequest()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new BadgeCreateDto(
                BadgeCode: "VIP",
                DisplayName: "Vip Badge",
                ColorHex: "#1234",    // sai định dạng (#RGB hoặc #RRGGBB)
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("ColorHex must be a valid hex color, e.g. #1e40af", message);
        }

        /// <summary>
        /// UTC010 – Boundary (normal):
        /// ColorHex có giá trị hợp lệ dạng #RGB.
        /// Kỳ vọng: 201 Created, ColorHex được lưu.
        /// </summary>
        [Fact]
        public async Task UTC010_Create_ValidRgbColorHex_CreatesBadge()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new BadgeCreateDto(
                BadgeCode: "RGB",
                DisplayName: "Rgb Color Badge",
                ColorHex: "#fff",     // valid #RGB
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<BadgeListItemDto>(created.Value);

            Assert.Equal("#fff", body.ColorHex);

            using var db = new KeytietkiemDbContext(options);
            var entity = Assert.Single(db.Badges);
            Assert.Equal("#fff", entity.ColorHex);
        }

        // ========== Helper classes ==========

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
