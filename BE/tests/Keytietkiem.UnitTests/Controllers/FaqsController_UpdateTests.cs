using System;
using System.Linq;
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

namespace Keytietkiem.UnitTests.Controllers
{
    public class FaqsController_UpdateTests
    {
        // ============== Helpers chung ==============

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
                CategoryId = id
            });
        }

        private static void SeedProduct(KeytietkiemDbContext db, Guid id)
        {
            db.Products.Add(new Product
            {
                ProductId = id
            });
        }

        private static void SeedFaqBasic(
            KeytietkiemDbContext db,
            int faqId = 1,
            string question = "Old question with enough length.",
            string answer = "Old answer with enough length.",
            int sortOrder = 0,
            bool isActive = true)
        {
            db.Faqs.Add(new Faq
            {
                FaqId = faqId,
                Question = question,
                Answer = answer,
                SortOrder = sortOrder,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            });
        }

        // ============== SUCCESS PATHS ==============

        /// <summary>
        /// Update đầy đủ: Faq tồn tại, Question/Answer hợp lệ,
        /// SortOrder >= 0, IsActive thay đổi, CategoryIds/ProductIds
        /// chứa id hợp lệ + trùng lặp + id rác -> quan hệ được replace
        /// và lọc trùng/invalid.
        /// </summary>
        [Fact]
        public async Task Update_ValidRequest_ReplacesMappingsAndUpdatesFields()
        {
            var options = CreateOptions();
            var fixedNow = new DateTime(2025, 1, 3, 10, 0, 0, DateTimeKind.Utc);

            Guid p1, p2, p3;

            using (var db = new KeytietkiemDbContext(options))
            {
                // At least 3 categories & 3 products
                SeedCategory(db, 1);
                SeedCategory(db, 2);
                SeedCategory(db, 3);

                p1 = Guid.NewGuid();
                p2 = Guid.NewGuid();
                p3 = Guid.NewGuid();

                SeedProduct(db, p1);
                SeedProduct(db, p2);
                SeedProduct(db, p3);

                // FAQ ban đầu liên kết với Cat 1 & 3, Product p1 & p3
                var faq = new Faq
                {
                    FaqId = 10,
                    Question = "Old question with enough length.",
                    Answer = "Old answer with enough length.",
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                faq.Categories.Add(db.Categories.First(c => c.CategoryId == 1));
                faq.Categories.Add(db.Categories.First(c => c.CategoryId == 3));
                faq.Products.Add(db.Products.First(p => p.ProductId == p1));
                faq.Products.Add(db.Products.First(p => p.ProductId == p3));

                db.Faqs.Add(faq);
                db.SaveChanges();
            }

            var controller = CreateController(options, fixedNow);

            var dto = new ProductFaqUpdateDto(
                Question: "  New question text for FAQ  ",
                Answer: "  New answer content that is definitely long enough.  ",
                SortOrder: 5,
                IsActive: false,
                CategoryIds: new[] { 1, 2, 2, 999 },              // 1 & 2 hợp lệ, 999 rác, 2 trùng
                ProductIds: new[] { p2, p2, Guid.Empty, Guid.NewGuid() } // p2 hợp lệ, còn lại rác
            );

            var result = await controller.Update(10, dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var faqAfter = checkDb.Faqs
                .Include(f => f.Categories)
                .Include(f => f.Products)
                .Single(f => f.FaqId == 10);

            // Trường chính
            Assert.Equal("New question text for FAQ", faqAfter.Question);
            Assert.Equal("New answer content that is definitely long enough.", faqAfter.Answer);
            Assert.Equal(5, faqAfter.SortOrder);
            Assert.False(faqAfter.IsActive);
            Assert.Equal(fixedNow, faqAfter.UpdatedAt);

            // Categories: replace & lọc trùng/invalid -> {1,2}
            var catIds = faqAfter.Categories.Select(c => c.CategoryId).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 1, 2 }, catIds);

            // Products: replace & lọc trùng/invalid -> chỉ còn p2
            var prodIds = faqAfter.Products.Select(p => p.ProductId).ToArray();
            Assert.Single(prodIds);
            Assert.Equal(p2, prodIds[0]);
        }

        /// <summary>
        /// CategoryIds và ProductIds = null -> xóa hết quan hệ cũ.
        /// </summary>
        [Fact]
        public async Task Update_NullCategoryAndProductIds_ClearsAllRelations()
        {
            var options = CreateOptions();
            var fixedNow = new DateTime(2025, 1, 4, 9, 0, 0, DateTimeKind.Utc);
            Guid p1;

            using (var db = new KeytietkiemDbContext(options))
            {
                SeedCategory(db, 1);
                SeedCategory(db, 2);
                p1 = Guid.NewGuid();
                SeedProduct(db, p1);

                var faq = new Faq
                {
                    FaqId = 20,
                    Question = "Old question with enough length.",
                    Answer = "Old answer with enough length.",
                    SortOrder = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                faq.Categories.Add(db.Categories.First(c => c.CategoryId == 1));
                faq.Products.Add(db.Products.First(p => p.ProductId == p1));

                db.Faqs.Add(faq);
                db.SaveChanges();
            }

            var controller = CreateController(options, fixedNow);

            var dto = new ProductFaqUpdateDto(
                Question: "Updated question with enough length.",
                Answer: "Updated answer with enough length.",
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,   // null -> clear hết
                ProductIds: null
            );

            var result = await controller.Update(20, dto);

            Assert.IsType<NoContentResult>(result);

            using var checkDb = new KeytietkiemDbContext(options);
            var faqAfter = checkDb.Faqs
                .Include(f => f.Categories)
                .Include(f => f.Products)
                .Single(f => f.FaqId == 20);

            Assert.Equal(0, faqAfter.SortOrder);
            Assert.True(faqAfter.IsActive);
            Assert.Equal(fixedNow, faqAfter.UpdatedAt);

            Assert.Empty(faqAfter.Categories); // quan hệ cũ bị xóa
            Assert.Empty(faqAfter.Products);
        }

        // ============== ERROR PATHS ==============

        [Fact]
        public async Task Update_FaqNotFound_Returns404()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                // Seed 1 FAQ khác id
                SeedFaqBasic(db, faqId: 1);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductFaqUpdateDto(
                Question: "Valid question text with enough length.",
                Answer: "Valid answer text with enough length.",
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Update(999, dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var message = GetMessage(notFound.Value!);
            Assert.Equal("Faq not found", message);
        }

        [Fact]
        public async Task Update_QuestionRequired_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedFaqBasic(db, faqId: 1);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductFaqUpdateDto(
                Question: "   ", // whitespace
                Answer: "Valid answer content with enough length.",
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Question is required", message);
        }

        [Fact]
        public async Task Update_QuestionTooShort_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedFaqBasic(db, faqId: 1);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductFaqUpdateDto(
                Question: "Too shrt",  // < 10 ký tự
                Answer: "Valid answer content with enough length.",
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Question length must be between 10 and 500 characters.", message);
        }

        [Fact]
        public async Task Update_AnswerRequired_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedFaqBasic(db, faqId: 1);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductFaqUpdateDto(
                Question: "Valid FAQ question text with enough length.",
                Answer: "   ",      // whitespace
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Answer is required", message);
        }

        [Fact]
        public async Task Update_AnswerTooShort_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedFaqBasic(db, faqId: 1);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductFaqUpdateDto(
                Question: "Valid FAQ question text with enough length.",
                Answer: "Too shrt",  // < 10 ký tự
                SortOrder: 0,
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("Answer length must be at least 10 characters.", message);
        }

        [Fact]
        public async Task Update_SortOrderNegative_Returns400()
        {
            var options = CreateOptions();
            using (var db = new KeytietkiemDbContext(options))
            {
                SeedFaqBasic(db, faqId: 1);
                db.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new ProductFaqUpdateDto(
                Question: "Valid FAQ question text with enough length.",
                Answer: "Valid answer content with enough length.",
                SortOrder: -1,   // < 0
                IsActive: true,
                CategoryIds: null,
                ProductIds: null
            );

            var result = await controller.Update(1, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetMessage(badRequest.Value!);
            Assert.Equal("SortOrder must be greater than or equal to 0.", message);
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
            public FakeClock(DateTime now)
            {
                UtcNow = now;
            }

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
