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
    public class CategoriesController_UpdateTests
    {
        // ============= Helpers chung =============

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

        private static void SeedCategory(
            KeytietkiemDbContext db,
            int id,
            string code,
            string name = "Old Name",
            string? desc = "Old description",
            bool isActive = true,
            DateTime? createdAt = null)
        {
            db.Categories.Add(new Category
            {
                CategoryId = id,
                CategoryCode = code,
                CategoryName = name,
                Description = desc,
                IsActive = isActive,
                CreatedAt = createdAt ?? DateTime.UtcNow
            });
        }

        // ============= SUCCESS PATHS =============

        /// <summary>
        /// Update KHÔNG đổi CategoryCode (CategoryCode = null).
        /// CategoryName được trim, Description update, IsActive đổi,
        /// CreatedAt giữ nguyên, UpdatedAt lấy từ IClock.
        /// </summary>
        [Fact]
        public async Task Update_WithoutChangingCode_UpdatesFieldsKeepsSlugAndCreatedAt()
        {
            var options = CreateOptions();
            var initialCreated = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var fixedNow = new DateTime(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc);

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, id: 1, code: "old-code", createdAt: initialCreated, isActive: true);
                db.SaveChanges();
            }

            var controller = CreateController(options, fixedNow);

            var dto = new CategoryUpdateDto(
                CategoryName: "  New Category Name  ",
                Description: "New description",
                IsActive: false,
                CategoryCode: null // không đổi slug
            );

            var result = await controller.Update(1, dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var cat = await checkDb.Categories.SingleAsync(c => c.CategoryId == 1);

            Assert.Equal("old-code", cat.CategoryCode);                  // slug giữ nguyên
            Assert.Equal("New Category Name", cat.CategoryName);         // Trim
            Assert.Equal("New description", cat.Description);
            Assert.False(cat.IsActive);                                  // update từ dto
            Assert.Equal(initialCreated, cat.CreatedAt);                 // không đổi
            Assert.Equal(fixedNow, cat.UpdatedAt);                       // set bởi clock
        }

        /// <summary>
        /// Update CÓ đổi CategoryCode: branch normalize slug + check unique.
        /// </summary>
        [Fact]
        public async Task Update_ChangeCode_NormalizesSlugAndUpdates()
        {
            var options = CreateOptions();
            var initialCreated = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            var fixedNow = new DateTime(2025, 1, 6, 11, 0, 0, DateTimeKind.Utc);

            using (var db = new KeytietkiemDbContext(options))
            {
                // Category đang update
                SeedCategory(db, id: 1, code: "old-code", createdAt: initialCreated, isActive: false);

                // Một category khác với slug khác để đảm bảo unique
                SeedCategory(db, id: 2, code: "other-code");
                db.SaveChanges();
            }

            var controller = CreateController(options, fixedNow);

            var dto = new CategoryUpdateDto(
                CategoryName: "  Renamed Category  ",
                Description: null,
                IsActive: true,
                CategoryCode: "  New   CODE!!  " // sau normalize -> "new-code"
            );

            var result = await controller.Update(1, dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var cat = await checkDb.Categories.SingleAsync(c => c.CategoryId == 1);

            Assert.Equal("new-code", cat.CategoryCode);                  // slug mới
            Assert.Equal("Renamed Category", cat.CategoryName);
            Assert.Null(cat.Description);
            Assert.True(cat.IsActive);
            Assert.Equal(initialCreated, cat.CreatedAt);
            Assert.Equal(fixedNow, cat.UpdatedAt);
        }

        // ============= ERROR PATHS =============

        [Fact]
        public async Task Update_CategoryNotFound_Returns404()
        {
            var options = CreateOptions();
            // Không seed category id 1

            var controller = CreateController(options);

            var dto = new CategoryUpdateDto(
                CategoryName: "Name",
                Description: null,
                IsActive: true,
                CategoryCode: null
            );

            var result = await controller.Update(1, dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var message = GetMessage(notFound.Value!);
            Assert.Equal("Category not found", message);
        }

        [Fact]
        public async Task Update_CategoryNameRequired_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, id: 1, code: "old-code");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new CategoryUpdateDto(
                CategoryName: "   ", // whitespace
                Description: null,
                IsActive: true,
                CategoryCode: null
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("CategoryName is required", message);
        }

        [Fact]
        public async Task Update_CategoryNameTooLong_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, id: 1, code: "old-code");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var longName = new string('A', 101); // >100
            var dto = new CategoryUpdateDto(
                CategoryName: longName,
                Description: null,
                IsActive: true,
                CategoryCode: null
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("CategoryName cannot exceed 100 characters", message);
        }

        [Fact]
        public async Task Update_DescriptionTooLong_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, id: 1, code: "old-code");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var longDesc = new string('D', 201); // >200
            var dto = new CategoryUpdateDto(
                CategoryName: "Valid name",
                Description: longDesc,
                IsActive: true,
                CategoryCode: null
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Description cannot exceed 200 characters", message);
        }

        /// <summary>
        /// CategoryCode được gửi nhưng normalize thành rỗng -> 400.
        /// </summary>
        [Fact]
        public async Task Update_SlugRequired_WhenNormalizedEmpty_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, id: 1, code: "old-code");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new CategoryUpdateDto(
                CategoryName: "Valid name",
                Description: null,
                IsActive: true,
                CategoryCode: "###@@@" // normalize -> ""
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("CategoryCode (slug) is required", message);
        }

        [Fact]
        public async Task Update_SlugTooLong_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, id: 1, code: "old-code");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var longSlug = new string('a', 51); // >50
            var dto = new CategoryUpdateDto(
                CategoryName: "Valid name",
                Description: null,
                IsActive: true,
                CategoryCode: longSlug
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("CategoryCode cannot exceed 50 characters", message);
        }

        [Fact]
        public async Task Update_DuplicateSlugOnOtherCategory_Returns409()
        {
            var options = CreateOptions();

            using (var db = new KeytietkiemDbContext(options))
            {
                // Category đang update
                SeedCategory(db, id: 1, code: "old-code");
                // Một category khác đã có slug "dup-code"
                SeedCategory(db, id: 2, code: "dup-code");
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new CategoryUpdateDto(
                CategoryName: "Valid name",
                Description: null,
                IsActive: true,
                CategoryCode: "  Dup-code  " // normalize -> "dup-code"
            );

            var result = await controller.Update(1, dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var message = GetMessage(conflict.Value!);
            Assert.Equal("CategoryCode already exists", message);
        }

        // ============= Helper inner classes =============

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
                // Không ghi log trong unit test
                return Task.CompletedTask;
            }
        }
    }
}
