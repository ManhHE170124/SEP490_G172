using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs;                // chứa AssignKeyToOrderDto, AssignAccountToOrderDto, ProductAccountCustomerDto
using Keytietkiem.DTOs.Cart;
using Keytietkiem.DTOs.Payments;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Keytietkiem.Tests.Controllers
{
    public class PaymentsController_HandlePayOSWebhook_Tests
    {
        #region Helpers

        private static KeytietkiemDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new KeytietkiemDbContext(options);
        }

        /// <summary>
        /// Đọc message từ object trả về (anonymous type, ProblemDetails, string, …)
        /// để tránh RuntimeBinderException khi dùng dynamic.
        /// </summary>
        private static string? GetMessage(object? value)
        {
            if (value == null) return null;

            var type = value.GetType();
            var prop = type.GetProperty("message") ?? type.GetProperty("Message");
            if (prop != null)
            {
                var val = prop.GetValue(value);
                return val?.ToString();
            }

            if (value is string s) return s;
            return null;
        }

        /// <summary>
        /// Tạo Product với đầy đủ các field bắt buộc (ProductCode, Slug, …)
        /// để tránh DbUpdateException trong InMemory provider.
        /// </summary>
        private static Product CreateTestProduct(string name)
        {
            var id = Guid.NewGuid();
            var shortId = id.ToString("N").Substring(0, 8);

            return new Product
            {
                ProductId = id,
                ProductCode = "P-" + shortId,
                ProductName = name,
                ProductType = ProductEnums.PERSONAL_KEY,
                Slug = "product-" + shortId,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };
        }

        private static PaymentsController CreateController(
            KeytietkiemDbContext db,
            IMemoryCache cache)
        {
            var config = new ConfigurationBuilder().Build();
            var loggerFactory = LoggerFactory.Create(b => b.AddDebug());
            var logger = loggerFactory.CreateLogger<PaymentsController>();

            // các mock dependency – chỉ cần return CompletedTask / giá trị đơn giản
            var productKeyService = new Mock<IProductKeyService>();
            productKeyService
                .Setup(s => s.AssignKeyToOrderAsync(
                    It.IsAny<AssignKeyToOrderDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var productAccountService = new Mock<IProductAccountService>();
            productAccountService
                .Setup(s => s.AssignAccountToOrderAsync(
                    It.IsAny<AssignAccountToOrderDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                // method trả về Task<ProductAccountCustomerDto>
                .ReturnsAsync(new ProductAccountCustomerDto());
            productAccountService
                .Setup(s => s.GetDecryptedPasswordAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("pwd");

            var emailService = new Mock<IEmailService>();
            emailService
                .Setup(s => s.SendOrderProductsEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<OrderProductEmailDto>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLogger = new Mock<IAuditLogger>();
            auditLogger
                .Setup(a => a.LogAsync(
                    It.IsAny<Microsoft.AspNetCore.Http.HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<object>()))
                .Returns(Task.CompletedTask);

            var accountService = new Mock<IAccountService>();
            accountService
                .Setup(a => a.GetUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            accountService
                .Setup(a => a.CreateTempUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new User
                {
                    UserId = Guid.NewGuid(),
                    Email = "temp-user@test.com",
                    FirstName = "Temp",
                    LastName = "User"
                });

            return new PaymentsController(
                db,
                payOs: null!,                     // không dùng trong webhook
                config,
                cache,
                logger,
                productKeyService.Object,
                productAccountService.Object,
                emailService.Object,
                auditLogger.Object,
                accountService.Object
            );
        }

        private static string ItemsKey(Guid paymentId)
            => $"cart:payment:{paymentId:D}:items";

        private static string MetaKey(Guid paymentId)
            => $"cart:payment:{paymentId:D}:meta";

        private static void SeedOrderPaymentAndSnapshot(
            KeytietkiemDbContext db,
            IMemoryCache cache,
            out Payment payment,
            out ProductVariant variant)
        {
            var product = CreateTestProduct("Test product");

            variant = new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = product.ProductId,
                Product = product,
                Title = "1 month",
                DurationDays = 30,
                StockQty = 0,
                SellPrice = 100_000,
                ListPrice = 100_000,
                Status = "OUT_OF_STOCK",
                CreatedAt = DateTime.UtcNow
            };

            payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                Amount = 0,
                Status = "Pending",
                Provider = "PayOS",
                ProviderOrderCode = 123456,
                TransactionType = "ORDER_PAYMENT",
                Email = "buyer@test.com",
                CreatedAt = DateTime.UtcNow
            };

            db.Products.Add(product);
            db.ProductVariants.Add(variant);
            db.Payments.Add(payment);
            db.SaveChanges();

            var items = new List<StorefrontCartItemDto>
            {
                new StorefrontCartItemDto
                {
                    VariantId = variant.VariantId,
                    Quantity  = 2,
                    UnitPrice = 100_000,
                    ListPrice = 100_000
                }
            };

            cache.Set(ItemsKey(payment.PaymentId), items);
            cache.Set(MetaKey(payment.PaymentId),
                (UserId: (Guid?)Guid.NewGuid(), Email: payment.Email!));
        }

        #endregion

        // ========== TEST CASES ==========

        /// <summary>
        /// TC1: payload null hoặc payload.Data null  => 400 BadRequest.
        /// </summary>
        [Fact]
        public async Task HandlePayOSWebhook_PayloadNull_ReturnsBadRequest()
        {
            using var db = CreateContext();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var controller = CreateController(db, cache);

            var result = await controller.HandlePayOSWebhook(null!);

            var badReq = Assert.IsType<BadRequestObjectResult>(result);
            var msg = GetMessage(badReq.Value);
            Assert.Equal("Payload từ PayOS không hợp lệ.", msg);
        }

        /// <summary>
        /// TC2: payload.Data.OrderCode <= 0  => 400 BadRequest "orderCode không hợp lệ".
        /// </summary>
        [Fact]
        public async Task HandlePayOSWebhook_InvalidOrderCode_ReturnsBadRequest()
        {
            using var db = CreateContext();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var controller = CreateController(db, cache);

            var payload = new PayOSWebhookModel
            {
                Code = "00",
                Data = new PayOSWebhookData
                {
                    OrderCode = 0,
                    Amount = 100_000,
                    Code = "00"
                }
            };

            var result = await controller.HandlePayOSWebhook(payload);

            var badReq = Assert.IsType<BadRequestObjectResult>(result);
            var msg = GetMessage(badReq.Value);
            Assert.Equal("orderCode không hợp lệ.", msg);
        }

        /// <summary>
        /// TC3: Payment không tồn tại cho OrderCode => 200 OK, message "Không tìm thấy payment,...",
        /// không tạo Order, không đổi trạng thái DB.
        /// </summary>
        [Fact]
        public async Task HandlePayOSWebhook_PaymentNotFound_ReturnsOkAndDoesNothing()
        {
            using var db = CreateContext();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var controller = CreateController(db, cache);

            var payload = new PayOSWebhookModel
            {
                Code = "00",
                Data = new PayOSWebhookData
                {
                    OrderCode = 999999,
                    Amount = 100_000,
                    Code = "00"
                }
            };

            var result = await controller.HandlePayOSWebhook(payload);

            var ok = Assert.IsType<OkObjectResult>(result);
            var msg = GetMessage(ok.Value);
            Assert.Equal("Không tìm thấy payment, đã bỏ qua.", msg);

            Assert.Empty(db.Orders);
            Assert.Empty(db.OrderDetails);
        }

        /// <summary>
        /// TC4: ORDER_PAYMENT + Pending + success code ("00"/"00") +
        /// snapshot đầy đủ => Payment chuyển sang Paid, Amount cập nhật,
        /// 1 Order + OrderDetails được tạo, snapshot bị xoá.
        /// </summary>
        [Fact]
        public async Task HandlePayOSWebhook_OrderPayment_SuccessWithSnapshot_CreatesOrderAndMarksPaid()
        {
            using var db = CreateContext();
            var cache = new MemoryCache(new MemoryCacheOptions());
            SeedOrderPaymentAndSnapshot(db, cache, out var payment, out var variant);
            var controller = CreateController(db, cache);

            var payload = new PayOSWebhookModel
            {
                Code = "00",
                Data = new PayOSWebhookData
                {
                    OrderCode = payment.ProviderOrderCode!.Value,
                    Amount = 200_000,   // 2 item * 100k
                    Code = "00"
                }
            };

            var result = await controller.HandlePayOSWebhook(payload);

            var ok = Assert.IsType<OkObjectResult>(result);
            var msg = GetMessage(ok.Value);
            Assert.Equal("Webhook đã được xử lý.", msg);

            var updatedPayment = db.Payments.Single(p => p.PaymentId == payment.PaymentId);
            Assert.Equal("Paid", updatedPayment.Status);
            Assert.Equal(200_000m, updatedPayment.Amount);

            var order = db.Orders.Single();
            Assert.Equal(payment.Email, order.Email);
            Assert.Single(db.OrderDetails);
            Assert.False(cache.TryGetValue(ItemsKey(payment.PaymentId), out _));
            Assert.False(cache.TryGetValue(MetaKey(payment.PaymentId), out _));

            // Gọi lại webhook lần 2 với cùng payload: status != Pending nên không tạo thêm Order
            var resultSecond = await controller.HandlePayOSWebhook(payload);
            Assert.IsType<OkObjectResult>(resultSecond);
            Assert.Single(db.Orders);
        }

        /// <summary>
        /// TC5: ORDER_PAYMENT + Pending + success code nhưng snapshot MISSING =>
        /// Payment set Paid, Amount cập nhật, KHÔNG tạo Order/OrderDetail.
        /// </summary>
        [Fact]
        public async Task HandlePayOSWebhook_OrderPayment_SuccessButSnapshotMissing_MarksPaidNoOrder()
        {
            using var db = CreateContext();
            var cache = new MemoryCache(new MemoryCacheOptions());

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                Amount = 0,
                Status = "Pending",
                Provider = "PayOS",
                ProviderOrderCode = 99999,
                TransactionType = "ORDER_PAYMENT",
                Email = "buyer@test.com",
                CreatedAt = DateTime.UtcNow
            };

            db.Payments.Add(payment);
            db.SaveChanges();

            var controller = CreateController(db, cache);

            var payload = new PayOSWebhookModel
            {
                Code = "00",
                Data = new PayOSWebhookData
                {
                    OrderCode = payment.ProviderOrderCode!.Value,
                    Amount = 150_000,
                    Code = "00"
                }
            };

            var result = await controller.HandlePayOSWebhook(payload);

            var ok = Assert.IsType<OkObjectResult>(result);

            var updated = db.Payments.Single(p => p.PaymentId == payment.PaymentId);
            Assert.Equal("Paid", updated.Status);
            Assert.Equal(150_000m, updated.Amount);
            Assert.Empty(db.Orders);
            Assert.Empty(db.OrderDetails);
        }

        /// <summary>
        /// TC6: ORDER_PAYMENT + Pending + failure code ("01") +
        /// có snapshot => Payment chuyển Cancelled, trả kho lại,
        /// không tạo Order.
        /// </summary>
        [Fact]
        public async Task HandlePayOSWebhook_OrderPayment_Failure_CancelsAndRestocks()
        {
            using var db = CreateContext();
            var cache = new MemoryCache(new MemoryCacheOptions());

            // Seed: variant có sẵn stock = 2, snapshot quantity = 3 => sau khi restock = 5.
            var product = CreateTestProduct("Restock product");

            var variant = new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = product.ProductId,
                Product = product,
                Title = "1 month",
                DurationDays = 30,
                StockQty = 2,
                SellPrice = 100_000,
                ListPrice = 100_000,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                Amount = 0,
                Status = "Pending",
                Provider = "PayOS",
                ProviderOrderCode = 77777,
                TransactionType = "ORDER_PAYMENT",
                Email = "buyer@test.com",
                CreatedAt = DateTime.UtcNow
            };

            db.Products.Add(product);
            db.ProductVariants.Add(variant);
            db.Payments.Add(payment);
            db.SaveChanges();

            var items = new List<StorefrontCartItemDto>
            {
                new StorefrontCartItemDto
                {
                    VariantId = variant.VariantId,
                    Quantity  = 3,
                    UnitPrice = 100_000,
                    ListPrice = 100_000
                }
            };

            cache.Set(ItemsKey(payment.PaymentId), items);
            cache.Set(MetaKey(payment.PaymentId),
                (UserId: (Guid?)Guid.NewGuid(), Email: payment.Email!));

            var controller = CreateController(db, cache);

            var payload = new PayOSWebhookModel
            {
                Code = "01",
                Data = new PayOSWebhookData
                {
                    OrderCode = payment.ProviderOrderCode!.Value,
                    Amount = 300_000,
                    Code = "01"
                }
            };

            var result = await controller.HandlePayOSWebhook(payload);

            var ok = Assert.IsType<OkObjectResult>(result);

            var updatedPayment = db.Payments.Single(p => p.PaymentId == payment.PaymentId);
            Assert.Equal("Cancelled", updatedPayment.Status);

            var updatedVariant = db.ProductVariants.Single(v => v.VariantId == variant.VariantId);
            Assert.Equal(5, updatedVariant.StockQty); // 2 + 3 restock

            Assert.Empty(db.Orders);
            Assert.Empty(db.OrderDetails);
            Assert.False(cache.TryGetValue(ItemsKey(payment.PaymentId), out _));
            Assert.False(cache.TryGetValue(MetaKey(payment.PaymentId), out _));
        }

        /// <summary>
        /// TC7: SERVICE_PAYMENT + Pending + success code => Payment Paid, Amount cập nhật.
        /// Gọi lần 2 với cùng payload => status giữ nguyên (idempotent).
        /// </summary>
        [Fact]
        public async Task HandlePayOSWebhook_ServicePayment_Success_UpdatesPaid_Idempotent()
        {
            using var db = CreateContext();
            var cache = new MemoryCache(new MemoryCacheOptions());

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                Amount = 0,
                Status = "Pending",
                Provider = "PayOS",
                ProviderOrderCode = 11111,
                TransactionType = "SERVICE_PAYMENT",
                Email = "user@test.com",
                CreatedAt = DateTime.UtcNow
            };

            db.Payments.Add(payment);
            db.SaveChanges();

            var controller = CreateController(db, cache);

            var payload = new PayOSWebhookModel
            {
                Code = "00",
                Data = new PayOSWebhookData
                {
                    OrderCode = payment.ProviderOrderCode!.Value,
                    Amount = 50_000,
                    Code = "00"
                }
            };

            var result1 = await controller.HandlePayOSWebhook(payload);
            Assert.IsType<OkObjectResult>(result1);

            var after1 = db.Payments.Single(p => p.PaymentId == payment.PaymentId);
            Assert.Equal("Paid", after1.Status);
            Assert.Equal(50_000m, after1.Amount);

            // Lần 2: status != Pending -> HandleServicePaymentWebhook bỏ qua
            var result2 = await controller.HandlePayOSWebhook(payload);
            Assert.IsType<OkObjectResult>(result2);

            var after2 = db.Payments.Single(p => p.PaymentId == payment.PaymentId);
            Assert.Equal("Paid", after2.Status);
            Assert.Equal(50_000m, after2.Amount);
        }

        /// <summary>
        /// TC8: TransactionType khác ORDER_PAYMENT (ví dụ "DEPOSIT") + Pending +
        /// failure code => đi vào nhánh generic (gọi HandleServicePaymentWebhook),
        /// Payment chuyển Cancelled.
        /// </summary>
        [Fact]
        public async Task HandlePayOSWebhook_OtherTransactionType_Failure_HandledAsServicePayment()
        {
            using var db = CreateContext();
            var cache = new MemoryCache(new MemoryCacheOptions());

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                Amount = 0,
                Status = "Pending",
                Provider = "PayOS",
                ProviderOrderCode = 22222,
                TransactionType = "DEPOSIT", // loại khác
                Email = "user@test.com",
                CreatedAt = DateTime.UtcNow
            };

            db.Payments.Add(payment);
            db.SaveChanges();

            var controller = CreateController(db, cache);

            var payload = new PayOSWebhookModel
            {
                Code = "99",
                Data = new PayOSWebhookData
                {
                    OrderCode = payment.ProviderOrderCode!.Value,
                    Amount = 50_000,
                    Code = "99"
                }
            };

            var result = await controller.HandlePayOSWebhook(payload);
            Assert.IsType<OkObjectResult>(result);

            var updated = db.Payments.Single(p => p.PaymentId == payment.PaymentId);
            Assert.Equal("Cancelled", updated.Status);
        }
    }
}
