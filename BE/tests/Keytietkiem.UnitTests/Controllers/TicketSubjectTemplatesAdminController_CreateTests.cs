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
    /// Unit test cho TicketSubjectTemplatesAdminController.Create
    /// 10 TC khớp sheet:
    ///  - UTSTT01: TemplateCode empty -> 400 "Mã template không được để trống."
    ///  - UTSTT02: TemplateCode > 50 ký tự -> 400 "Mã template không được vượt quá 50 ký tự."
    ///  - UTSTT03: TemplateCode chứa ký tự không hợp lệ (space/special) -> 400
    ///              "Mã template chỉ được chứa chữ, số, dấu gạch ngang (-) và gạch dưới (_), không chứa khoảng trắng."
    ///  - UTSTT04: TemplateCode đã tồn tại -> 400 "Mã template đã tồn tại."
    ///  - UTSTT05: Title empty -> 400 "Tiêu đề không được để trống."
    ///  - UTSTT06: Title đã tồn tại -> 400 "Tiêu đề template đã tồn tại. Vui lòng nhập tiêu đề khác."
    ///  - UTSTT07: Severity empty -> 400 "Độ ưu tiên (Severity) không được để trống."
    ///  - UTSTT08: Severity không nằm trong {Low, Medium, High, Critical} -> 400
    ///              "Giá trị Severity không hợp lệ. Vui lòng chọn một trong: Low, Medium, High, Critical."
    ///  - UTSTT09: Category không thuộc {Account, General, Key, Payment, Refund, Security, Support} -> 400
    ///              "Category không hợp lệ. Vui lòng chọn một trong: Account, General, Key, Payment, Refund, Security, Support."
    ///  - UTSTT10: Happy path -> 201 Created, body là TicketSubjectTemplateAdminDetailDto,
    ///              dữ liệu được trim/normalize & đúng IsActive, đúng 1 row được insert.
    /// </summary>
    public class TicketSubjectTemplatesAdminController_CreateTests
    {
        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        /// <summary>
        /// DbContextFactory đơn giản dùng cho unit test.
        /// Mọi chỗ đều dùng chung 1 options => share cùng in-memory store.
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

        // ===================== UTSTT01 =====================
        [Fact(DisplayName = "UTSTT01 - TemplateCode empty -> 400 & no row inserted")]
        public async Task Create_TemplateCodeEmpty_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_TemplateCodeEmpty_Returns400));
            var factory = new TestDbContextFactory(options);

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = "   ",
                Title = "Hỗ trợ chung",
                Severity = "Medium",
                Category = "Support",
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Mã template không được để trống.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Empty(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTSTT02 =====================
        [Fact(DisplayName = "UTSTT02 - TemplateCode length > 50 -> 400 BadRequest")]
        public async Task Create_TemplateCodeTooLong_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_TemplateCodeTooLong_Returns400));
            var factory = new TestDbContextFactory(options);

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = new string('A', 51),
                Title = "Hỗ trợ chung",
                Severity = "Medium",
                Category = "Support",
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Mã template không được vượt quá 50 ký tự.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Empty(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTSTT03 =====================
        [Fact(DisplayName = "UTSTT03 - TemplateCode has invalid chars -> 400 BadRequest")]
        public async Task Create_TemplateCodeInvalidChars_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_TemplateCodeInvalidChars_Returns400));
            var factory = new TestDbContextFactory(options);

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = "GENERAL SUPPORT", // có khoảng trắng
                Title = "Hỗ trợ chung",
                Severity = "Medium",
                Category = "Support",
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Mã template chỉ được chứa chữ, số, dấu gạch ngang (-) và gạch dưới (_), không chứa khoảng trắng.",
                GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Empty(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTSTT04 =====================
        [Fact(DisplayName = "UTSTT04 - TemplateCode already exists -> 400 BadRequest")]
        public async Task Create_TemplateCodeExists_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_TemplateCodeExists_Returns400));
            var factory = new TestDbContextFactory(options);

            // Seed 1 template có TemplateCode trùng
            using (var seedDb = factory.CreateDbContext())
            {
                seedDb.TicketSubjectTemplates.Add(new TicketSubjectTemplate
                {
                    TemplateCode = "GENERAL_SUPPORT",
                    Title = "Hỗ trợ chung",
                    Severity = "Medium",
                    Category = "General",
                    IsActive = true
                });
                seedDb.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Title = "Tiêu đề mới",
                Severity = "High",
                Category = "Support",
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Mã template đã tồn tại.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Single(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTSTT05 =====================
        [Fact(DisplayName = "UTSTT05 - Title empty -> 400 BadRequest")]
        public async Task Create_TitleEmpty_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_TitleEmpty_Returns400));
            var factory = new TestDbContextFactory(options);

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Title = "   ",
                Severity = "Medium",
                Category = "Support",
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Tiêu đề không được để trống.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Empty(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTSTT06 =====================
        [Fact(DisplayName = "UTSTT06 - Title already exists -> 400 BadRequest")]
        public async Task Create_TitleExists_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_TitleExists_Returns400));
            var factory = new TestDbContextFactory(options);

            using (var seedDb = factory.CreateDbContext())
            {
                seedDb.TicketSubjectTemplates.Add(new TicketSubjectTemplate
                {
                    TemplateCode = "LOGIN_ISSUE",
                    Title = "Lỗi đăng nhập",
                    Severity = "High",
                    Category = "Account",
                    IsActive = true
                });
                seedDb.SaveChanges();
            }

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = "OTHER_CODE",
                Title = "Lỗi đăng nhập", // trùng
                Severity = "Medium",
                Category = "Support",
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Tiêu đề template đã tồn tại. Vui lòng nhập tiêu đề khác.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Single(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTSTT07 =====================
        [Fact(DisplayName = "UTSTT07 - Severity empty -> 400 BadRequest")]
        public async Task Create_SeverityEmpty_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_SeverityEmpty_Returns400));
            var factory = new TestDbContextFactory(options);

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Title = "Hỗ trợ chung",
                Severity = "   ",
                Category = "Support",
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Độ ưu tiên (Severity) không được để trống.", GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Empty(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTSTT08 =====================
        [Fact(DisplayName = "UTSTT08 - Severity not in allowed list -> 400 BadRequest")]
        public async Task Create_SeverityInvalid_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_SeverityInvalid_Returns400));
            var factory = new TestDbContextFactory(options);

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Title = "Hỗ trợ chung",
                Severity = "Urgent", // không hợp lệ
                Category = "Support",
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Giá trị Severity không hợp lệ. Vui lòng chọn một trong: Low, Medium, High, Critical",
                GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Empty(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTSTT09 =====================
        [Fact(DisplayName = "UTSTT09 - Category not in allowed list -> 400 BadRequest")]
        public async Task Create_CategoryInvalid_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_CategoryInvalid_Returns400));
            var factory = new TestDbContextFactory(options);

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Title = "Hỗ trợ chung",
                Severity = "Medium",
                Category = "Other", // không thuộc list
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Category không hợp lệ. Vui lòng chọn một trong: Account, General, Key, Payment, Refund, Security, Support",
                GetMessage(bad.Value));

            using var assertDb = factory.CreateDbContext();
            Assert.Empty(assertDb.TicketSubjectTemplates);
        }

        // ===================== UTSTT10 =====================
        [Fact(DisplayName = "UTSTT10 - Valid input -> 201 Created & row inserted with normalized values")]
        public async Task Create_ValidInput_CreatesRow()
        {
            var options = CreateInMemoryOptions(nameof(Create_ValidInput_CreatesRow));
            var factory = new TestDbContextFactory(options);

            var controller = CreateController(factory);

            var dto = new TicketSubjectTemplateAdminCreateDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Title = "  Hỗ trợ chung  ",
                Severity = "hIGh",
                Category = "suPPorT",
                IsActive = false // test theo request body
            };

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(StatusCodes.Status201Created, created.StatusCode);

            var detail = Assert.IsType<TicketSubjectTemplateAdminDetailDto>(created.Value);

            Assert.Equal("GENERAL_SUPPORT", detail.TemplateCode);
            Assert.Equal("Hỗ trợ chung", detail.Title);
            Assert.Equal("High", detail.Severity);
            Assert.Equal("Support", detail.Category);
            Assert.False(detail.IsActive);

            using var assertDb = factory.CreateDbContext();
            var template = Assert.Single(assertDb.TicketSubjectTemplates);

            Assert.Equal("GENERAL_SUPPORT", template.TemplateCode);
            Assert.Equal("Hỗ trợ chung", template.Title);
            Assert.Equal("High", template.Severity);
            Assert.Equal("Support", template.Category);
            Assert.False(template.IsActive);
        }
    }
}
