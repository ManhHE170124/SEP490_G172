// File: Tests/Products/ProductVariantsController_Delete_Tests.cs
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
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Products
{
    public class ProductVariantsController_Delete_Tests
    {
        #region Helpers

        private static (ProductVariantsController controller,
                        DbContextOptions<KeytietkiemDbContext> dbOptions,
                        Mock<IAuditLogger> auditLoggerMock)
            CreateSut(string dbName)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            var dbFactoryMock = new Mock<IDbContextFactory<KeytietkiemDbContext>>();
            dbFactoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new KeytietkiemDbContext(options));

            var clockMock = new Mock<IClock>();
            clockMock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

            var auditLoggerMock = new Mock<IAuditLogger>();

            var controller = new ProductVariantsController(
                dbFactoryMock.Object,
                clockMock.Object,
                auditLoggerMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            return (controller, options, auditLoggerMock);
        }

        private static T? GetProp<T>(object obj, string name)
        {
            var prop = obj.GetType().GetProperty(name);
            if (prop == null) return default;
            var value = prop.GetValue(obj);
            if (value == null) return default;
            return (T)value;
        }

        private static Product CreateProduct(Guid productId,
            string code = "P_BASE",
            string name = "Base Product")
        {
            return new Product
            {
                ProductId = productId,
                ProductCode = code,
                ProductName = name,
                ProductType = ProductEnums.PERSONAL_KEY,
                Status = "ACTIVE",
                Slug = code.ToLowerInvariant(),
                CreatedAt = DateTime.UtcNow
            };
        }

        private static ProductVariant CreateVariant(Guid productId,
            Product product,
            string title = "Variant 1",
            int stockQty = 1,
            decimal price = 100_000m)
        {
            return new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = productId,
                Product = product,
                VariantCode = "VR1",
                Title = title,
                DurationDays = 30,
                StockQty = stockQty,
                WarrantyDays = 7,
                Thumbnail = null,
                MetaTitle = null,
                MetaDescription = null,
                ViewCount = 0,
                Status = "ACTIVE",
                SellPrice = price,
                ListPrice = price,
                CogsPrice = 0,
                CreatedAt = DateTime.UtcNow
            };
        }

        #endregion

        // =========================================================
        // TC1 – Variant không tồn tại -> NotFound + không log audit
        // =========================================================
        [Fact]
        public async Task Delete_VariantNotFound_ReturnsNotFound_AndDoesNotLogAudit()
        {
            var (controller, options, auditLoggerMock) =
                CreateSut(nameof(Delete_VariantNotFound_ReturnsNotFound_AndDoesNotLogAudit));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid(); // không tồn tại

            var result = await controller.Delete(productId, variantId);

            Assert.IsType<NotFoundResult>(result);

            using var assertCtx = new KeytietkiemDbContext(options);
            Assert.Empty(assertCtx.ProductVariants);

            auditLoggerMock.Verify(a =>
                    a.LogAsync(
                        It.IsAny<HttpContext>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<object?>(),
                        It.IsAny<object?>()),
                Times.Never);
        }

        // =========================================================
        // TC2 – Variant đang được dùng trong ProductSections -> Conflict
        // =========================================================
        [Fact]
        public async Task Delete_VariantInUseBySection_ReturnsConflict_AndDoesNotDeleteOrLog()
        {
            var (controller, options, auditLoggerMock) =
                CreateSut(nameof(Delete_VariantInUseBySection_ReturnsConflict_AndDoesNotDeleteOrLog));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var seedCtx = new KeytietkiemDbContext(options))
            {
                var p = CreateProduct(productId, "P_SEC", "Product With Section");
                var v = CreateVariant(productId, p, "Var-Section", 5, 200_000m);
                v.VariantId = variantId;

                seedCtx.Products.Add(p);
                seedCtx.ProductVariants.Add(v);

                // FIX: thêm các field required của ProductSection (Content, SectionType)
                var section = new ProductSection
                {
                    // nếu có khóa chính SectionId thì EF sẽ tự set, còn không thì bỏ qua
                    VariantId = variantId,
                    Content = "Dummy content for unit test",
                    SectionType = "TEXT"  // hoặc giá trị hợp lệ khác theo enum của bạn
                };
                seedCtx.ProductSections.Add(section);

                seedCtx.SaveChanges();
            }

            var result = await controller.Delete(productId, variantId);

            var conflict = Assert.IsType<ConflictObjectResult>(result);

            var value = conflict.Value!;
            var code = GetProp<string>(value, "code");
            var message = GetProp<string>(value, "message");

            Assert.Equal("VARIANT_IN_USE_SECTION", code);
            Assert.NotNull(message);
            Assert.Contains("Không thể xoá biến thể", message!, StringComparison.OrdinalIgnoreCase);

            using (var assertCtx = new KeytietkiemDbContext(options))
            {
                // Variant vẫn còn
                var vInDb = assertCtx.ProductVariants.SingleOrDefault(v => v.VariantId == variantId);
                Assert.NotNull(vInDb);

                // Product vẫn còn, status vẫn ACTIVE (chưa recalculated vì return sớm)
                var pInDb = assertCtx.Products.SingleOrDefault(p => p.ProductId == productId);
                Assert.NotNull(pInDb);
                Assert.Equal("ACTIVE", pInDb!.Status);
            }

            auditLoggerMock.Verify(a =>
                    a.LogAsync(
                        It.IsAny<HttpContext>(),
                        It.IsAny<string>(),
                        It.IsAny<string?>(),
                        It.IsAny<string?>(),
                        It.IsAny<object?>(),
                        It.IsAny<object?>()),
                Times.Never);
        }

        // =========================================================
        // TC3 – Variant không bị dùng ở Section -> Xoá + RecalcStatus + Audit
        // =========================================================
        [Fact]
        public async Task Delete_VariantNoSections_DeletesVariant_RecalcProductStatus_AndLogsAudit()
        {
            var (controller, options, auditLoggerMock) =
                CreateSut(nameof(Delete_VariantNoSections_DeletesVariant_RecalcProductStatus_AndLogsAudit));

            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();

            using (var seedCtx = new KeytietkiemDbContext(options))
            {
                var p = CreateProduct(productId, "P_DEL", "Product For Delete");

                var v = CreateVariant(productId, p, "Var-Delete", 3, 150_000m);
                v.VariantId = variantId;

                seedCtx.Products.Add(p);
                seedCtx.ProductVariants.Add(v);
                seedCtx.SaveChanges();
            }

            var result = await controller.Delete(productId, variantId);

            Assert.IsType<NoContentResult>(result);

            using (var assertCtx = new KeytietkiemDbContext(options))
            {
                // Variant đã bị xoá
                var vInDb = assertCtx.ProductVariants.SingleOrDefault(v => v.VariantId == variantId);
                Assert.Null(vInDb);

                // Product vẫn tồn tại
                var pInDb = assertCtx.Products.SingleOrDefault(p => p.ProductId == productId);
                Assert.NotNull(pInDb);

                // RecalcProductStatus: không còn variant nào -> OUT_OF_STOCK
                Assert.Equal("OUT_OF_STOCK", pInDb!.Status);
            }

            auditLoggerMock.Verify(a =>
                    a.LogAsync(
                        It.IsAny<HttpContext>(),
                        "Delete",
                        "ProductVariant",
                        variantId.ToString(),
                        It.IsAny<object?>(), // before snapshot
                        null),               // after = null
                Times.Once);
        }
    }
}
