// File: Tests/Products/ProductsController_Delete_Tests.cs
using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Products
{
    public class ProductsController_Delete_Tests
    {
        #region Helpers

        private static (ProductsController controller,
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

            var controller = new ProductsController(
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

        private static Product CreateProduct(Guid id, string code = "P1", string name = "Test Product")
        {
            return new Product
            {
                ProductId = id,
                ProductCode = code,
                ProductName = name,
                ProductType = ProductEnums.PERSONAL_KEY,
                Status = "ACTIVE",
                Slug = code.ToLowerInvariant(),
                CreatedAt = DateTime.UtcNow
            };
        }

        private static ProductVariant CreateVariant(Guid productId, Product product, int stockQty = 1)
        {
            return new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = productId,
                Product = product,
                Title = "Variant 1",
                StockQty = stockQty,
                SellPrice = 100_000m,
                ListPrice = 100_000m,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };
        }

        #endregion

        // =========================================================
        // TC1 – Product không tồn tại -> NotFound
        // =========================================================
        [Fact]
        public async Task Delete_ProductNotFound_ReturnsNotFound_AndDoesNotLogAudit()
        {
            var (controller, options, auditLoggerMock) =
                CreateSut(nameof(Delete_ProductNotFound_ReturnsNotFound_AndDoesNotLogAudit));

            var randomId = Guid.NewGuid();

            var result = await controller.Delete(randomId);

            Assert.IsType<NotFoundResult>(result);

            // Không có gì trong DB
            using var assertCtx = new KeytietkiemDbContext(options);
            Assert.Empty(assertCtx.Products);

            // Không ghi audit
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
        // TC2 – Product còn Variant -> Conflict + không xoá + không audit
        // =========================================================
        [Fact]
        public async Task Delete_ProductHasVariants_ReturnsConflict_AndDoesNotDeleteOrLog()
        {
            var (controller, options, auditLoggerMock) =
                CreateSut(nameof(Delete_ProductHasVariants_ReturnsConflict_AndDoesNotDeleteOrLog));

            var productId = Guid.NewGuid();
            var productName = "Product With Variants";

            using (var seedCtx = new KeytietkiemDbContext(options))
            {
                var p = CreateProduct(productId, "PVAR", productName);
                var v1 = CreateVariant(productId, p, stockQty: 2);

                seedCtx.Products.Add(p);
                seedCtx.ProductVariants.Add(v1);
                seedCtx.SaveChanges();
            }

            var result = await controller.Delete(productId);

            var conflict = Assert.IsType<ConflictObjectResult>(result);

            // Đọc các prop trong anonymous object
            var value = conflict.Value!;
            var message = GetProp<string>(value, "message");
            var variantCount = GetProp<int>(value, "variantCount");
            var hasVariants = GetProp<bool>(value, "hasVariants");
            var hasOrders = GetProp<bool>(value, "hasOrders");

            Assert.NotNull(message);
            Assert.Contains(productName, message!);
            Assert.True(message!.Contains("không thể xoá", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("Không thể xoá", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, variantCount);
            Assert.True(hasVariants);
            Assert.False(hasOrders);

            // Sản phẩm & variant vẫn tồn tại
            using (var assertCtx = new KeytietkiemDbContext(options))
            {
                var p = assertCtx.Products.SingleOrDefault(x => x.ProductId == productId);
                Assert.NotNull(p);
                Assert.Equal(1, assertCtx.ProductVariants.Count(x => x.ProductId == productId));
            }

            // Không ghi audit
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
        // TC3 – Product không có Variant -> NoContent + xoá + ghi Audit
        // =========================================================
        [Fact]
        public async Task Delete_ProductWithoutVariants_DeletesAndLogsAudit_ReturnsNoContent()
        {
            var (controller, options, auditLoggerMock) =
                CreateSut(nameof(Delete_ProductWithoutVariants_DeletesAndLogsAudit_ReturnsNoContent));

            var productId = Guid.NewGuid();

            using (var seedCtx = new KeytietkiemDbContext(options))
            {
                var p = CreateProduct(productId, "PDEL", "Delete Me");
                seedCtx.Products.Add(p);
                seedCtx.SaveChanges();
            }

            var result = await controller.Delete(productId);

            Assert.IsType<NoContentResult>(result);

            // Đã xoá khỏi DB
            using (var assertCtx = new KeytietkiemDbContext(options))
            {
                Assert.Null(assertCtx.Products.SingleOrDefault(x => x.ProductId == productId));
                Assert.Empty(assertCtx.ProductVariants.Where(v => v.ProductId == productId));
            }

            // Đã ghi audit 1 lần với action = "Delete", entityType = "Product", entityId = productId
            auditLoggerMock.Verify(a =>
                    a.LogAsync(
                        It.IsAny<HttpContext>(),
                        "Delete",
                        "Product",
                        productId.ToString(),
                        It.IsAny<object?>(), // beforeSnapshot
                        null),                // after = null
                Times.Once);
        }
    }
}
