using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Tickets;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit test cho TicketSubjectTemplatesAdminController.Update (UpdateTicketSubjectTemplate)
    /// 10 TC khớp sheet:
    ///  - UTUSTT01: Route templateCode empty -> 400 "Mã template không hợp lệ."
    ///  - UTUSTT02: Template không tồn tại -> 404 NotFound
    ///  - UTUSTT03: Title empty -> 400 "Tiêu đề không được để trống."
    ///  - UTUSTT04: Title > 200 ký tự -> 400 "Tiêu đề không được vượt quá 200 ký tự."
    ///  - UTUSTT05: Title trùng template khác -> 400 "Tiêu đề template đã tồn tại. Vui lòng nhập tiêu đề khác."
    ///  - UTUSTT06: Severity empty -> 400 "Độ ưu tiên (Severity) không được để trống."
    ///  - UTUSTT07: Severity không thuộc {Low, Medium, High, Critical}
    ///  - UTUSTT08: Category không thuộc {Account, General, Key, Payment, Refund, Security, Support}
    ///  - UTUSTT09: Happy path: Category hợp lệ -> 204 NoContent, chỉ đúng 1 row được update
    ///  - UTUSTT10: Happy path: Category rỗng -> 204 NoContent, Category lưu là null
    /// </summary>
    public class TicketSubjectTemplatesAdminController_UpdateTicketSubjectTemplateTests
    {
        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        /// <summary>
        /// DbContextFactory dùng chung 1 in-memory store qua DbContextOptions.
        /// </summary>
        private class TestDbContextFactory : IDbContextFactory<KeytietkiemDbContext>
        {
            private readonly DbContextOptions<KeytietkiemDbContext> _options;

            public TestDbContextFactory(DbContextOptions<KeytietkiemDbContext> options)
            {
                _options = options;
            }

            public KeytietkiemDbContext CreateDbContext()
                => new KeytietkiemDbContext(_options);

            public ValueTask<KeytietkiemDbContext> CreateDbContextAsync(
                CancellationToken cancellationToken = default)
                => new ValueTask<KeytietkiemDbContext>(CreateDbContext());
        }

        /// <summary>
        /// Tạo controller với factory & audit logger mock (đã setup LogAsync để không NullReference).
        /// </summary>
        private static TicketSubjectTemplatesAdminController CreateController(
            IDbContextFactory<KeytietkiemDbContext> factory)
        {
            var auditLogger = new Mock<IAuditLogger>();
            auditLogger
                .Setup(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()))
                .Returns(Task.CompletedTask);

            var controller = new TicketSubjectTemplatesAdminController(factory, auditLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            return controller;
        }

        private static string? GetMessage(object? body)
        {
            return body?
                .GetType()
                .GetProperty("message")?
                .GetValue(body)?
                .ToString();
        }

        #endregion

        // ===================== UTUSTT01 =====================
        [Fact(DisplayName = "UTUSTT01 - Route templateCode empty -> 400 BadRequest")]
        public async Task Update_TemplateCodeRouteEmpty_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_TemplateCodeRouteEmpty_Returns400));
            var factory = new TestDbContextFactory(options);
            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = "Hỗ trợ thanh toán",
                Severity = "Medium",
                Category = "Payment",
                IsActive = true
            };

            var result = await controller.Update("   ", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Mã template không hợp lệ.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Empty(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTUSTT02 =====================
        [Fact(DisplayName = "UTUSTT02 - Template not found -> 404 NotFound")]
        public async Task Update_TemplateNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Update_TemplateNotFound_Returns404));
            var factory = new TestDbContextFactory(options);

            using (var seed = factory.CreateDbContext())
            {
                seed.TicketSubjectTemplates.Add(new TicketSubjectTemplate
                {
                    TemplateCode = "OTHER_ISSUE",
                    Title = "Lỗi khác",
                    Severity = "Low",
                    Category = "General",
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = "Lỗi thanh toán",
                Severity = "Medium",
                Category = "Payment",
                IsActive = true
            };

            var result = await controller.Update("PAYMENT_ISSUE", dto);

            Assert.IsType<NotFoundResult>(result);

            using var assertDb = factory.CreateDbContext();
            var template = Assert.Single(assertDb.TicketSubjectTemplates);
            Assert.Equal("OTHER_ISSUE", template.TemplateCode);
            Assert.Equal("Lỗi khác", template.Title);
            Assert.Equal("Low", template.Severity);
            Assert.Equal("General", template.Category);
            Assert.True(template.IsActive);
        }

        // ===================== UTUSTT03 =====================
        [Fact(DisplayName = "UTUSTT03 - Title empty -> 400 BadRequest")]
        public async Task Update_TitleEmpty_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_TitleEmpty_Returns400));
            var factory = new TestDbContextFactory(options);

            using (var seed = factory.CreateDbContext())
            {
                seed.TicketSubjectTemplates.Add(new TicketSubjectTemplate
                {
                    TemplateCode = "PAYMENT_ISSUE",
                    Title = "Lỗi thanh toán cũ",
                    Severity = "Medium",
                    Category = "Payment",
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = "   ",
                Severity = "Medium",
                Category = "Payment",
                IsActive = true
            };

            var result = await controller.Update("PAYMENT_ISSUE", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Tiêu đề không được để trống.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            var template = Assert.Single(assertDb.TicketSubjectTemplates);
            Assert.Equal("Lỗi thanh toán cũ", template.Title);
        }

        // ===================== UTUSTT04 =====================
        [Fact(DisplayName = "UTUSTT04 - Title length > 200 -> 400 BadRequest")]
        public async Task Update_TitleTooLong_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_TitleTooLong_Returns400));
            var factory = new TestDbContextFactory(options);

            using (var seed = factory.CreateDbContext())
            {
                seed.TicketSubjectTemplates.Add(new TicketSubjectTemplate
                {
                    TemplateCode = "PAYMENT_ISSUE",
                    Title = "Lỗi thanh toán cũ",
                    Severity = "Medium",
                    Category = "Payment",
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(factory);

            var longTitle = new string('X', 201);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = longTitle,
                Severity = "Medium",
                Category = "Payment",
                IsActive = true
            };

            var result = await controller.Update("PAYMENT_ISSUE", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Tiêu đề không được vượt quá 200 ký tự.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            var template = Assert.Single(assertDb.TicketSubjectTemplates);
            Assert.Equal("Lỗi thanh toán cũ", template.Title);
        }

        // ===================== UTUSTT05 =====================
        [Fact(DisplayName = "UTUSTT05 - Title duplicated on another template -> 400 BadRequest")]
        public async Task Update_TitleDuplicated_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_TitleDuplicated_Returns400));
            var factory = new TestDbContextFactory(options);

            using (var seed = factory.CreateDbContext())
            {
                seed.TicketSubjectTemplates.AddRange(
                    new TicketSubjectTemplate
                    {
                        TemplateCode = "LOGIN_ISSUE",
                        Title = "Lỗi đăng nhập",
                        Severity = "High",
                        Category = "Account",
                        IsActive = true
                    },
                    new TicketSubjectTemplate
                    {
                        TemplateCode = "PAYMENT_ISSUE",
                        Title = "Lỗi thanh toán",
                        Severity = "Medium",
                        Category = "Payment",
                        IsActive = true
                    });
                seed.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = "Lỗi đăng nhập",
                Severity = "Medium",
                Category = "Payment",
                IsActive = true
            };

            var result = await controller.Update("PAYMENT_ISSUE", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Tiêu đề template đã tồn tại. Vui lòng nhập tiêu đề khác.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            var templates = assertDb.TicketSubjectTemplates.OrderBy(t => t.TemplateCode).ToList();
            Assert.Equal(2, templates.Count);
            Assert.Equal("Lỗi đăng nhập", templates[0].Title);
            Assert.Equal("Lỗi thanh toán", templates[1].Title);
        }

        // ===================== UTUSTT06 =====================
        [Fact(DisplayName = "UTUSTT06 - Severity empty -> 400 BadRequest")]
        public async Task Update_SeverityEmpty_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_SeverityEmpty_Returns400));
            var factory = new TestDbContextFactory(options);

            using (var seed = factory.CreateDbContext())
            {
                seed.TicketSubjectTemplates.Add(new TicketSubjectTemplate
                {
                    TemplateCode = "PAYMENT_ISSUE",
                    Title = "Lỗi thanh toán",
                    Severity = "Medium",
                    Category = "Payment",
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = "Lỗi thanh toán",
                Severity = "   ",
                Category = "Payment",
                IsActive = true
            };

            var result = await controller.Update("PAYMENT_ISSUE", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Độ ưu tiên (Severity) không được để trống.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            var template = Assert.Single(assertDb.TicketSubjectTemplates);
            Assert.Equal("Medium", template.Severity);
        }

        // ===================== UTUSTT07 =====================
        [Fact(DisplayName = "UTUSTT07 - Severity not in allowed list -> 400 BadRequest")]
        public async Task Update_SeverityInvalid_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_SeverityInvalid_Returns400));
            var factory = new TestDbContextFactory(options);

            using (var seed = factory.CreateDbContext())
            {
                seed.TicketSubjectTemplates.Add(new TicketSubjectTemplate
                {
                    TemplateCode = "PAYMENT_ISSUE",
                    Title = "Lỗi thanh toán",
                    Severity = "Medium",
                    Category = "Payment",
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = "Lỗi thanh toán",
                Severity = "Urgent",
                Category = "Payment",
                IsActive = true
            };

            var result = await controller.Update("PAYMENT_ISSUE", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(
                "Giá trị Severity không hợp lệ. Vui lòng chọn một trong: Low, Medium, High, Critical",
                GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            var template = Assert.Single(assertDb.TicketSubjectTemplates);
            Assert.Equal("Medium", template.Severity);
        }

        // ===================== UTUSTT08 =====================
        [Fact(DisplayName = "UTUSTT08 - Category not in allowed list -> 400 BadRequest")]
        public async Task Update_CategoryInvalid_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_CategoryInvalid_Returns400));
            var factory = new TestDbContextFactory(options);

            using (var seed = factory.CreateDbContext())
            {
                seed.TicketSubjectTemplates.Add(new TicketSubjectTemplate
                {
                    TemplateCode = "PAYMENT_ISSUE",
                    Title = "Lỗi thanh toán",
                    Severity = "Medium",
                    Category = "Payment",
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = "Lỗi thanh toán",
                Severity = "Medium",
                Category = "Other",
                IsActive = true
            };

            var result = await controller.Update("PAYMENT_ISSUE", dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(
                "Category không hợp lệ. Vui lòng chọn một trong: Account, General, Key, Payment, Refund, Security, Support",
                GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            var template = Assert.Single(assertDb.TicketSubjectTemplates);
            Assert.Equal("Payment", template.Category);
        }

        // ===================== UTUSTT09 =====================
        [Fact(DisplayName = "UTUSTT09 - Valid update keeps TemplateCode & updates fields (204 NoContent)")]
        public async Task Update_ValidInput_UpdatesSingleRow()
        {
            var options = CreateInMemoryOptions(nameof(Update_ValidInput_UpdatesSingleRow));
            var factory = new TestDbContextFactory(options);

            using (var seed = factory.CreateDbContext())
            {
                seed.TicketSubjectTemplates.AddRange(
                    new TicketSubjectTemplate
                    {
                        TemplateCode = "LOGIN_ISSUE",
                        Title = "Lỗi đăng nhập",
                        Severity = "High",
                        Category = "Account",
                        IsActive = true
                    },
                    new TicketSubjectTemplate
                    {
                        TemplateCode = "PAYMENT_ISSUE",
                        Title = "Lỗi thanh toán cũ",
                        Severity = "Medium",
                        Category = "Payment",
                        IsActive = true
                    });
                seed.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = "  Hỗ trợ thanh toán  ",
                Severity = "hIGh",
                Category = "paYment",
                IsActive = false
            };

            var result = await controller.Update("PAYMENT_ISSUE", dto);

            Assert.IsType<NoContentResult>(result);

            using var assertDb = factory.CreateDbContext();
            var templates = assertDb.TicketSubjectTemplates
                .OrderBy(t => t.TemplateCode)
                .ToList();

            Assert.Equal(2, templates.Count);

            var login = templates[0];
            var payment = templates[1];

            Assert.Equal("LOGIN_ISSUE", login.TemplateCode);
            Assert.Equal("Lỗi đăng nhập", login.Title);
            Assert.Equal("High", login.Severity);
            Assert.Equal("Account", login.Category);
            Assert.True(login.IsActive);

            Assert.Equal("PAYMENT_ISSUE", payment.TemplateCode);
            Assert.Equal("Hỗ trợ thanh toán", payment.Title);
            Assert.Equal("High", payment.Severity);
            Assert.Equal("Payment", payment.Category);
            Assert.False(payment.IsActive);
        }

        // ===================== UTUSTT10 =====================
        [Fact(DisplayName = "UTUSTT10 - Valid update with empty Category -> Category stored as null (204 NoContent)")]
        public async Task Update_ValidInput_EmptyCategory_StoredAsNull()
        {
            var options = CreateInMemoryOptions(nameof(Update_ValidInput_EmptyCategory_StoredAsNull));
            var factory = new TestDbContextFactory(options);

            using (var seed = factory.CreateDbContext())
            {
                seed.TicketSubjectTemplates.Add(new TicketSubjectTemplate
                {
                    TemplateCode = "PAYMENT_ISSUE",
                    Title = "Lỗi thanh toán",
                    Severity = "Medium",
                    Category = "Payment",
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminUpdateDto
            {
                Title = "Hỗ trợ thanh toán",
                Severity = "Low",
                Category = "   ",
                IsActive = true
            };

            var result = await controller.Update("PAYMENT_ISSUE", dto);

            Assert.IsType<NoContentResult>(result);

            using var assertDb = factory.CreateDbContext();
            var template = Assert.Single(assertDb.TicketSubjectTemplates);

            Assert.Equal("PAYMENT_ISSUE", template.TemplateCode);
            Assert.Equal("Hỗ trợ thanh toán", template.Title);
            Assert.Equal("Low", template.Severity);
            Assert.Null(template.Category);
            Assert.True(template.IsActive);
        }
    }
}
