using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Payments;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class PaymentsController_GetPaymentsTests
    {
        // ======= Helpers chung =======

        private (PaymentsController Controller, KeytietkiemDbContext Db) CreateControllerWithSeedData()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new KeytietkiemDbContext(options);
            SeedPayments(db);
            db.SaveChanges();

            var cache = new MemoryCache(new MemoryCacheOptions());
            var logger = NullLogger<PaymentsController>.Instance;

            var controller = new PaymentsController(
                context: db,
                payOs: null!,
                config: null!,
                cache: cache,
                logger: logger,
                productKeyService: null!,
                productAccountService: null!,
                emailService: null!,
                auditLogger: null!,
                accountService: null!
            );

            return (controller, db);
        }

        private void SeedPayments(KeytietkiemDbContext db)
        {
            var now = DateTime.UtcNow;

            db.Payments.AddRange(
                new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    Amount = 100m,
                    Status = "Pending",
                    Provider = "PayOS",
                    Email = "alice@example.com",
                    TransactionType = "ORDER_PAYMENT",
                    ProviderOrderCode = 111,
                    CreatedAt = now.AddHours(-3)
                },
                new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    Amount = 200m,
                    Status = "Paid",
                    Provider = "PayOS",
                    Email = "payos@example.com",     // dùng cho test filter provider + email
                    TransactionType = "SERVICE_PAYMENT",
                    ProviderOrderCode = 222,
                    CreatedAt = now.AddHours(-2)
                },
                new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    Amount = 50m,
                    Status = "Cancelled",
                    Provider = "Momo",
                    Email = "payos@example.com",     // cùng email, khác provider
                    TransactionType = "ORDER_PAYMENT",
                    ProviderOrderCode = 333,
                    CreatedAt = now.AddHours(-1)     // mới nhất
                },
                new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    Amount = 150m,
                    Status = "Failed",
                    Provider = "VNPay",
                    Email = "bob@example.com",
                    TransactionType = "SERVICE_PAYMENT",
                    ProviderOrderCode = 444,
                    CreatedAt = now.AddHours(-4)     // cũ nhất
                }
            );
        }

        private static List<PaymentAdminListItemDTO> ExtractList(IActionResult result)
        {
            var ok = Assert.IsType<OkObjectResult>(result);
            return Assert.IsType<List<PaymentAdminListItemDTO>>(ok.Value);
        }

        // ========= TEST CASES =========

        /// <summary>
        /// TC1 – Không filter, sort mặc định (CreatedAt desc) → trả về tất cả, đúng thứ tự mới → cũ.
        /// </summary>
        [Fact]
        public async Task GetPayments_NoFilter_DefaultSortByCreatedAtDesc()
        {
            var (controller, _) = CreateControllerWithSeedData();

            var result = await controller.GetPayments(
                status: null,
                provider: null,
                email: null,
                transactionType: null,
                sortBy: null,
                sortDir: null);

            var items = ExtractList(result);
            Assert.Equal(4, items.Count);

            // CreatedAt desc: newest (now-1h) -> old (now-4h)
            var createdAts = items.Select(p => p.CreatedAt).ToList();
            Assert.True(createdAts.SequenceEqual(createdAts.OrderByDescending(x => x)));
        }

        /// <summary>
        /// TC2 – Filter status với giá trị hợp lệ + có khoảng trắng hai đầu → trim + match chính xác.
        /// </summary>
        [Fact]
        public async Task GetPayments_FilterByStatus_Trimmed()
        {
            var (controller, _) = CreateControllerWithSeedData();

            var result = await controller.GetPayments(
                status: "  Paid  ",
                provider: null,
                email: null,
                transactionType: null,
                sortBy: null,
                sortDir: null);

            var items = ExtractList(result);

            Assert.Single(items);
            Assert.All(items, p => Assert.Equal("Paid", p.Status));
        }

        /// <summary>
        /// TC3 – Kết hợp filter Provider = PayOS và Email (có space) → chỉ record thỏa cả 2.
        /// </summary>
        [Fact]
        public async Task GetPayments_FilterByProviderAndEmail_Trimmed()
        {
            var (controller, _) = CreateControllerWithSeedData();

            var result = await controller.GetPayments(
                status: null,
                provider: "  PayOS ",
                email: "  payos@example.com  ",
                transactionType: null,
                sortBy: null,
                sortDir: null);

            var items = ExtractList(result);

            Assert.Single(items);
            var p = items.Single();
            Assert.Equal("PayOS", p.Provider);
            Assert.Equal("payos@example.com", p.Email);
        }

        /// <summary>
        /// TC4 – Filter TransactionType = ORDER_PAYMENT.
        /// </summary>
        [Fact]
        public async Task GetPayments_FilterByTransactionType_OrderPayment()
        {
            var (controller, _) = CreateControllerWithSeedData();

            var result = await controller.GetPayments(
                status: null,
                provider: null,
                email: null,
                transactionType: "ORDER_PAYMENT",
                sortBy: null,
                sortDir: null);

            var items = ExtractList(result);

            Assert.Equal(2, items.Count);
            Assert.All(items, p => Assert.Equal("ORDER_PAYMENT", p.TransactionType));
        }

        /// <summary>
        /// TC5 – Status filter với giá trị không tồn tại → trả list rỗng.
        /// </summary>
        [Fact]
        public async Task GetPayments_InvalidStatusFilter_ReturnsEmptyList()
        {
            var (controller, _) = CreateControllerWithSeedData();

            var result = await controller.GetPayments(
                status: "UnknownStatus",
                provider: null,
                email: null,
                transactionType: null,
                sortBy: null,
                sortDir: null);

            var items = ExtractList(result);
            Assert.Empty(items);
        }

        /// <summary>
        /// TC6 – Sort theo Amount, chiều ASC (sortDir = "ASC" hoa) → check thứ tự 50, 100, 150, 200.
        /// </summary>
        [Fact]
        public async Task GetPayments_SortByAmount_Ascending()
        {
            var (controller, _) = CreateControllerWithSeedData();

            var result = await controller.GetPayments(
                status: null,
                provider: null,
                email: null,
                transactionType: null,
                sortBy: "Amount",
                sortDir: "ASC");

            var items = ExtractList(result);
            var amounts = items.Select(p => p.Amount).ToList();

            Assert.Equal(new[] { 50m, 100m, 150m, 200m }, amounts);
        }
        /// <summary>
        /// Status chỉ chứa whitespace -> được coi như không filter,
        /// kết quả giống hệt call không truyền status.
        /// </summary>
        [Fact]
        public async Task GetPayments_StatusWhitespace_TreatedAsNoFilter()
        {
            var (controller, _) = CreateControllerWithSeedData();

            // Không filter status
            var noStatusResult = await controller.GetPayments(
                status: null,
                provider: null,
                email: null,
                transactionType: null,
                sortBy: null,
                sortDir: null);
            var noStatusList = ExtractList(noStatusResult);

            // Status toàn whitespace
            var whitespaceStatusResult = await controller.GetPayments(
                status: "   ",
                provider: null,
                email: null,
                transactionType: null,
                sortBy: null,
                sortDir: null);
            var whitespaceList = ExtractList(whitespaceStatusResult);

            Assert.Equal(
                noStatusList.Select(x => x.PaymentId),
                whitespaceList.Select(x => x.PaymentId));
        }

        /// <summary>
        /// Filter TransactionType = "SERVICE_PAYMENT"
        /// (tương ứng case thứ 2 trong sheet: SUPPORT_PLAN / service).
        /// </summary>
        [Fact]
        public async Task GetPayments_FilterByTransactionType_ServicePayment()
        {
            var (controller, _) = CreateControllerWithSeedData();

            var result = await controller.GetPayments(
                status: null,
                provider: null,
                email: null,
                transactionType: "SERVICE_PAYMENT",
                sortBy: null,
                sortDir: null);

            var items = ExtractList(result);

            Assert.Equal(2, items.Count);
            Assert.All(items, p => Assert.Equal("SERVICE_PAYMENT", p.TransactionType));
        }

        /// <summary>
        /// Filter chỉ theo Provider = "PayOS" (không filter email) →
        /// trả về tất cả payment của PayOS.
        /// </summary>
        [Fact]
        public async Task GetPayments_FilterByProviderOnly_PayOS()
        {
            var (controller, _) = CreateControllerWithSeedData();

            var result = await controller.GetPayments(
                status: null,
                provider: "PayOS",
                email: null,
                transactionType: null,
                sortBy: null,
                sortDir: null);

            var items = ExtractList(result);

            Assert.Equal(2, items.Count);
            Assert.All(items, p => Assert.Equal("PayOS", p.Provider));
        }

        /// <summary>
        /// (Optional) Email = "" được xử lý như không filter email
        /// → kết quả giống call email = null.
        /// </summary>
        [Fact]
        public async Task GetPayments_EmailEmptyString_TreatedAsNoFilter()
        {
            var (controller, _) = CreateControllerWithSeedData();

            var noEmailResult = await controller.GetPayments(
                status: null,
                provider: null,
                email: null,
                transactionType: null,
                sortBy: null,
                sortDir: null);
            var noEmailList = ExtractList(noEmailResult);

            var emptyEmailResult = await controller.GetPayments(
                status: null,
                provider: null,
                email: string.Empty,
                transactionType: null,
                sortBy: null,
                sortDir: null);
            var emptyEmailList = ExtractList(emptyEmailResult);

            Assert.Equal(
                noEmailList.Select(x => x.PaymentId),
                emptyEmailList.Select(x => x.PaymentId));
        }

        /// <summary>
        /// TC7 – sortBy là field không hợp lệ → rơi vào default (CreatedAt desc).
        /// So sánh kết quả với sort mặc định trên CÙNG dataset.
        /// </summary>
        [Fact]
        public async Task GetPayments_SortByInvalidField_FallsBackToCreatedAtDesc()
        {
            // Dùng chung 1 in-memory database cho cả 2 controller
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            // Seed 1 lần
            using (var seedDb = new KeytietkiemDbContext(options))
            {
                SeedPayments(seedDb);
                seedDb.SaveChanges();
            }

            // Controller 1: sort mặc định (CreatedAt desc)
            var db1 = new KeytietkiemDbContext(options);
            var cache1 = new MemoryCache(new MemoryCacheOptions());
            var controller1 = new PaymentsController(
                context: db1,
                payOs: null!,
                config: null!,
                cache: cache1,
                logger: NullLogger<PaymentsController>.Instance,
                productKeyService: null!,
                productAccountService: null!,
                emailService: null!,
                auditLogger: null!,
                accountService: null!
            );

            // Controller 2: sortBy invalid -> fallback CreatedAt desc
            var db2 = new KeytietkiemDbContext(options);
            var cache2 = new MemoryCache(new MemoryCacheOptions());
            var controller2 = new PaymentsController(
                context: db2,
                payOs: null!,
                config: null!,
                cache: cache2,
                logger: NullLogger<PaymentsController>.Instance,
                productKeyService: null!,
                productAccountService: null!,
                emailService: null!,
                auditLogger: null!,
                accountService: null!
            );

            var defaultResult = await controller1.GetPayments(
                status: null,
                provider: null,
                email: null,
                transactionType: null,
                sortBy: null,
                sortDir: null);
            var defaultList = ExtractList(defaultResult);

            var invalidSortResult = await controller2.GetPayments(
                status: null,
                provider: null,
                email: null,
                transactionType: null,
                sortBy: "TotalAmount",  // field không tồn tại
                sortDir: null);
            var invalidSortList = ExtractList(invalidSortResult);

            Assert.Equal(
                defaultList.Select(x => x.PaymentId),
                invalidSortList.Select(x => x.PaymentId));
        }
    }
}
