using System;
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
    public class CategoriesController_CreateTests
    {
        // ============== Helpers chung ==============

        private static DbContextOptions<KeytietkiemDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static CategoriesController CreateController(
            DbContextOptions<KeytietkiemDbContext> options,
            DateTime? nowOverride = null)
        {
            var factory = new TestDbContextFactory(options);
            var clock = new FakeClock(nowOverride ?? DateTime.UtcNow);
            var auditLogger = new FakeAuditLogger();

            return new CategoriesController(factory, clock, auditLogger)
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

        private static void SeedCategory(KeytietkiemDbContext db, string code, string name = "Existing")
        {
            db.Categories.Add(new Category
            {
                CategoryCode = code,
                CategoryName = name,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        // ============== SUCCESS PATH ==============

        /// <summary>
        /// Tạo category hợp lệ: CategoryName được trim, CategoryCode được slug hóa,
        /// Description hợp lệ, IsActive = false, CreatedAt dùng clock.
        /// </summary>
        [Fact]
        public async Task Create_ValidRequest_NormalizesSlugAndCreatesCategory()
        {
            var options = CreateOptions();
            var fixedNow = new DateTime(2025, 1, 5, 8, 0, 0, DateTimeKind.Utc);

            var controller = CreateController(options, fixedNow);

            var dto = new CategoryCreateDto(
                CategoryCode: "  My   NEW   Category!!  ", // để test NormalizeSlug
                CategoryName: "  Software Keys  ",         // để test Trim
                Description: "Category for Windows / Office license keys.",
                IsActive: false
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var body = Assert.IsType<CategoryDetailDto>(created.Value);

            Assert.True(body.CategoryId > 0);
            Assert.Equal("software-keys", body.CategoryCode);  // slug hóa
            Assert.Equal("Software Keys", body.CategoryName);  // Trim + giữ hoa thường
            Assert.Equal("Category for Windows / Office license keys.", body.Description);
            Assert.False(body.IsActive);
            Assert.Equal(0, body.ProductCount);

            // Kiểm tra DB
            using var checkDb = new KeytietkiemDbContext(options);
            var entity = await checkDb.Categories.SingleAsync();

            Assert.Equal(body.CategoryId, entity.CategoryId);
            Assert.Equal("software-keys", entity.CategoryCode);
            Assert.Equal("Software Keys", entity.CategoryName);
            Assert.False(entity.IsActive);
            Assert.Equal(fixedNow, entity.CreatedAt);
        }

        // ============== ERROR PATHS ==============

        [Fact]
        public async Task Create_CategoryNameRequired_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new CategoryCreateDto(
                CategoryCode: "valid-code",
                CategoryName: "   ",    // chỉ whitespace
                Description: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("CategoryName is required", message);
        }

        [Fact]
        public async Task Create_CategoryNameTooLong_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var longName = new string('A', 101); // >100
            var dto = new CategoryCreateDto(
                CategoryCode: "valid-code",
                CategoryName: longName,
                Description: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("CategoryName cannot exceed 100 characters", message);
        }

        /// <summary>
        /// CategoryCode null/only special chars -> NormalizeSlug trả empty
        /// => 400 "CategoryCode (slug) is required".
        /// </summary>
        [Fact]
        public async Task Create_SlugRequired_WhenNormalizedEmpty_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new CategoryCreateDto(
                CategoryCode: "###@@@",    // sau normalize -> ""
                CategoryName: "Valid Name",
                Description: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("CategoryCode (slug) is required", message);
        }

        [Fact]
        public async Task Create_SlugTooLong_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var longSlug = new string('a', 51); // >50, normalize vẫn y nguyên
            var dto = new CategoryCreateDto(
                CategoryCode: longSlug,
                CategoryName: "Valid Name",
                Description: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("CategoryCode cannot exceed 50 characters", message);
        }

        [Fact]
        public async Task Create_DescriptionTooLong_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var longDesc = new string('D', 201); // >200
            var dto = new CategoryCreateDto(
                CategoryCode: "valid-code",
                CategoryName: "Valid Name",
                Description: longDesc,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Description cannot exceed 200 characters", message);
        }

        /// <summary>
        /// Một category đã tồn tại với code "my-code".
        /// Request gửi raw code " My   code!! " -> normalize thành "my-code"
        /// => 409 "CategoryCode already exists".
        /// </summary>
        [Fact]
        public async Task Create_DuplicateSlug_Returns409()
        {
            var options = CreateOptions();

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, "my-code", "Existing category");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new CategoryCreateDto(
                CategoryCode: "  My   code!!  ", // normalize -> my-code
                CategoryName: "New Name",
                Description: null,
                IsActive: true
            );

            var result = await controller.Create(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var message = GetMessage(conflict.Value!);
            Assert.Equal("CategoryCode already exists", message);
        }

        // ============== Helper inner classes ==============

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
            public FakeClock(DateTime now) => UtcNow = now;

            public DateTime UtcNow { get; set; }
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
                // Không ghi log thực trong unit test
                return Task.CompletedTask;
            }
        }
    }
}
