// File: tests/Keytietkiem.UnitTests/Controllers/BadgesController_CreateTests.cs
using System;
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
    /// Unit test WHITE-BOX cho logic Create badge (POST /api/badges).
    /// Mapping 1-1 với các UTC trong sheet CreateBadge.
    /// </summary>
    public class BadgesController_CreateTests
    {
        private static BadgesController CreateController(
            string databaseName,
            Action<KeytietkiemDbContext>? seed = null)
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

            return new BadgesController(factory);
        }

        private static string? GetMessage(ObjectResult result)
        {
            var value = result.Value;
            var prop = value?.GetType().GetProperty("message");
            return prop?.GetValue(value)?.ToString();
        }

        // ========== UTC01 ==========
        [Fact]
        public async Task Create_UTC01_ShouldReturnCreated_WhenBasicDataValid_NoColor()
        {
            var controller = CreateController("CreateBadge_UTC01");

            var dto = new BadgeCreateDto(
                BadgeCode: "A",
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(BadgesController.Get), created.ActionName);

            var body = Assert.IsType<BadgeListItemDto>(created.Value);
            Assert.Equal("A", body.BadgeCode);
            Assert.Equal("Name", body.DisplayName);
            Assert.Null(body.ColorHex);
            Assert.True(body.IsActive);
        }

        // ========== UTC02 ==========
        [Fact]
        public async Task Create_UTC02_ShouldReturnBadRequest_WhenBadgeCodeEmpty()
        {
            var controller = CreateController("CreateBadge_UTC02");

            var dto = new BadgeCreateDto(
                BadgeCode: "   ",
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("BadgeCode is required", GetMessage(bad));
        }

        // ========== UTC03 ==========
        [Fact]
        public async Task Create_UTC03_ShouldReturnBadRequest_WhenBadgeCodeContainsSpace()
        {
            var controller = CreateController("CreateBadge_UTC03");

            var dto = new BadgeCreateDto(
                BadgeCode: "CODE 1",
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("BadgeCode cannot contain spaces", GetMessage(bad));
        }

        // ========== UTC04 ==========
        [Fact]
        public async Task Create_UTC04_ShouldReturnBadRequest_WhenBadgeCodeTooLong()
        {
            var controller = CreateController("CreateBadge_UTC04");

            var dto = new BadgeCreateDto(
                BadgeCode: new string('C', 33),
                DisplayName: "Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("BadgeCode cannot exceed 32 characters", GetMessage(bad));
        }

        // ========== UTC05 ==========
        [Fact]
        public async Task Create_UTC05_ShouldReturnCreated_WhenCodeAndNameAtMaxLength()
        {
            var controller = CreateController("CreateBadge_UTC05");

            var dto = new BadgeCreateDto(
                BadgeCode: new string('C', 32),
                DisplayName: new string('D', 64),
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<BadgeListItemDto>(created.Value);

            Assert.Equal(new string('C', 32), body.BadgeCode);
            Assert.Equal(new string('D', 64), body.DisplayName);
        }

        // ========== UTC06 ==========
        [Fact]
        public async Task Create_UTC06_ShouldReturnConflict_WhenBadgeCodeAlreadyExists()
        {
            var dbName = "CreateBadge_UTC06";

            var controller = CreateController(
                dbName,
                seed: db =>
                {
                    db.Badges.Add(new Badge
                    {
                        BadgeCode = "EXISTING",
                        DisplayName = "Existing name",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                });

            var dto = new BadgeCreateDto(
                BadgeCode: "EXISTING",
                DisplayName: "New Name",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal("BadgeCode already exists", GetMessage(conflict));
        }

        // ========== UTC07 ==========
        [Fact]
        public async Task Create_UTC07_ShouldReturnBadRequest_WhenDisplayNameEmpty()
        {
            var controller = CreateController("CreateBadge_UTC07");

            var dto = new BadgeCreateDto(
                BadgeCode: "NEW7",
                DisplayName: "   ",
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("DisplayName is required", GetMessage(bad));
        }

        // ========== UTC08 ==========
        [Fact]
        public async Task Create_UTC08_ShouldReturnBadRequest_WhenDisplayNameTooLong()
        {
            var controller = CreateController("CreateBadge_UTC08");

            var dto = new BadgeCreateDto(
                BadgeCode: "NEW8",
                DisplayName: new string('D', 65),
                ColorHex: null,
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("DisplayName cannot exceed 64 characters", GetMessage(bad));
        }

        // ========== UTC09 ==========
        [Fact]
        public async Task Create_UTC09_ShouldReturnBadRequest_WhenColorHexInvalid()
        {
            var controller = CreateController("CreateBadge_UTC09");

            var dto = new BadgeCreateDto(
                BadgeCode: "NEW9",
                DisplayName: "Valid Name",
                ColorHex: "123456", // không có '#', sai format
                Icon: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("ColorHex must be a valid hex color, e.g. #1e40af", GetMessage(bad));
        }

        // ========== UTC10 ==========
        [Fact]
        public async Task Create_UTC10_ShouldReturnCreated_WhenColorHexValidAndIconTrimmed()
        {
            var controller = CreateController("CreateBadge_UTC10");

            var dto = new BadgeCreateDto(
                BadgeCode: "NEW10",
                DisplayName: "Valid Name",
                ColorHex: "#1A2b3C",
                Icon: "  fa-star  ",
                IsActive: false
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var body = Assert.IsType<BadgeListItemDto>(created.Value);

            Assert.Equal("NEW10", body.BadgeCode);
            Assert.Equal("Valid Name", body.DisplayName);
            Assert.Equal("#1A2b3C", body.ColorHex);
            Assert.Equal("fa-star", body.Icon); // đã Trim()
            Assert.False(body.IsActive);
        }

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
