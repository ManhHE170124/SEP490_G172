// Keytietkiem.UnitTests/Controllers/FaqsController_CreateTests.cs
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
    public class FaqsController_CreateTests
    {
        // ================== Helpers chung ==================

        private static DbContextOptions<KeytietkiemDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static FaqsController CreateController(
            DbContextOptions<KeytietkiemDbContext> options,
            DateTime? nowOverride = null)
        {
            var factory = new TestDbContextFactory(options);
            var clock = new FakeClock(nowOverride ?? DateTime.UtcNow);
            var auditLogger = new FakeAuditLogger();

            return new FaqsController(factory, clock, auditLogger)
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

        private static void SeedCategory(KeytietkiemDbContext db, int id)
        {
            db.Categories.Add(new Category
            {
                CategoryId = id,
                CategoryCode = "CAT" + id,          // giá trị fake nhưng hợp lệ
                CategoryName = "Category " + id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static void SeedProduct(KeytietkiemDbContext db, Guid id)
        {
            db.Products.Add(new Product
            {
                ProductId = id,
                ProductCode = "P-" + id.ToString("N").Substring(0, 8),
                ProductName = "Product " + id.ToString("N").Substring(0, 4),
                ProductType = ProductEnums.PERSONAL_KEY, // hoặc "KEY" tùy model của bạn
                Slug = "product-" + id.ToString("N").Substring(0, 8),
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            });
        }


        // ================== SUCCESS PATHS ==================

        [Fact]
        public async Task Create_ValidRequest_CreatesFaqWithMappings()
        {
            var options = CreateOptions();
            Guid p1, p2;
            var fixedNow = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, 1);
                SeedCategory(db, 2);

                p1 = Guid.NewGuid();
                p2 = Guid.NewGuid();
                SeedProduct(db, p1);
                SeedProduct(db, p2);

                db.SaveChanges();
            }

            var controller = CreateController(options, fixedNow);

            var dto = new ProductFaqCreateDto(
                Question: "How to activate Windows 11 license key correctly?",
                Answer: "You can follow these steps to activate your product key safely.",
                SortOrder: 5,
                IsActive: true,
                CategoryIds: new[] { 1, 1, 2, 999 },
                ProductIds: new[] { p1, p1, p2, Guid.Empty, Guid.NewGuid() }
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var body = Assert.IsType<ProductFaqDetailDto>(created.Value);

            Assert.True(body.FaqId > 0);
            Assert.Equal(dto.Question, body.Question);
            Assert.Equal(dto.Answer, body.Answer);
            Assert.Equal(5, body.SortOrder);
            Assert.True(body.IsActive);
            Assert.Equal(fixedNow, body.CreatedAt);
            Assert.Null(body.UpdatedAt);

            // CategoryIds
            var catIds = body.CategoryIds.OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 1, 2 }, catIds);

            // ✅ ProductIds: sort cả expected lẫn actual
            var prodIds = body.ProductIds.OrderBy(x => x).ToArray();
            var expectedProdIds = new[] { p1, p2 }.OrderBy(x => x).ToArray();
            Assert.Equal(expectedProdIds, prodIds);

            using var checkDb = new KeytietkiemDbContext(options);
            var faq = checkDb.Faqs
                .Include(f => f.Categories)
                .Include(f => f.Products)
                .Single();

            Assert.Equal(body.FaqId, faq.FaqId);
            Assert.Equal(5, faq.SortOrder);
            Assert.True(faq.IsActive);
            Assert.Equal(fixedNow, faq.CreatedAt);
            Assert.Equal(2, faq.Categories.Count);
            Assert.Equal(2, faq.Products.Count);
        }


        [Fact]
        public async Task Create_NegativeSortOrder_NormalizedToZero()
        {
            var options = CreateOptions();
            var fixedNow = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);

            var controller = CreateController(options, fixedNow);

            var dto = new ProductFaqCreateDto(
                Question: "Why my license cannot be redeemed on Microsoft account?",
                Answer: "Please double check your region and the account type before redeeming.",
                SortOrder: -10,
                IsActive: false,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var body = Assert.IsType<ProductFaqDetailDto>(created.Value);

            Assert.Equal(0, body.SortOrder);
            Assert.False(body.IsActive);
            Assert.Equal(fixedNow, body.CreatedAt);
            Assert.Empty(body.CategoryIds);
            Assert.Empty(body.ProductIds);
        }

        // ================== ERROR PATHS ==================

        [Fact]
        public async Task Create_QuestionRequired_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new ProductFaqCreateDto(
                Question: "   ",
                Answer: "Valid answer content with enough length.",
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Question is required", message);
        }

        [Fact]
        public async Task Create_QuestionTooShort_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new ProductFaqCreateDto(
                Question: "Too shrt",
                Answer: "Valid answer content with enough length.",
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Question length must be between 10 and 500 characters.", message);
        }

        [Fact]
        public async Task Create_AnswerRequired_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new ProductFaqCreateDto(
                Question: "This is a valid FAQ question with enough length.",
                Answer: "   ",
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Answer is required", message);
        }

        [Fact]
        public async Task Create_AnswerTooShort_Returns400()
        {
            var options = CreateOptions();
            var controller = CreateController(options);

            var dto = new ProductFaqCreateDto(
                Question: "This is another valid FAQ question with normal length.",
                Answer: "Too shrt",
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Answer length must be at least 10 characters.", message);
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
                return Task.CompletedTask;
            }
        }
    }
}
