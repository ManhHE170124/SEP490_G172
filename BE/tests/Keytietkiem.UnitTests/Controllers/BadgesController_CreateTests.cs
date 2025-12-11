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

        // ========== Test cases ==========

        [Fact]
        public async Task Create_ValidInputWithoutColor_CreatesBadgeWithTrimmedFields()
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

            // Kiểm tra lưu trong DB
            using var db = new KeytietkiemDbContext(options);
            var entity = Assert.Single(db.Badges);
            Assert.Equal("VIP", entity.BadgeCode);
            Assert.Equal("Vip Badge", entity.DisplayName);
            Assert.Null(entity.ColorHex);
            Assert.Equal("star", entity.Icon);
            Assert.True(entity.IsActive);
        }

        [Fact]
        public async Task Create_ValidInputWithColor_CreatesBadge()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new BadgeCreateDto(
                BadgeCode: "HOT",
                DisplayName: "Hot Badge",
                ColorHex: "#1e40af",
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<BadgeListItemDto>(created.Value);

            Assert.Equal("#1e40af", body.ColorHex);

            using var db = new KeytietkiemDbContext(options);
            var entity = Assert.Single(db.Badges);
            Assert.Equal("#1e40af", entity.ColorHex);
        }

        [Fact]
        public async Task Create_InvalidBadgeCode_IsRequired()
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

        [Fact]
        public async Task Create_InvalidBadgeCode_ContainsSpaces()
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

        [Fact]
        public async Task Create_InvalidBadgeCode_TooLong()
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

        [Fact]
        public async Task Create_InvalidBadgeCode_AlreadyExists()
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

        [Fact]
        public async Task Create_InvalidDisplayName_IsRequired()
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

        [Fact]
        public async Task Create_InvalidDisplayName_TooLong()
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

        [Fact]
        public async Task Create_InvalidColorHex_ReturnsBadRequest()
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
