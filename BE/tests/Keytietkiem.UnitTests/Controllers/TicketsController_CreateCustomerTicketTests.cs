using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Tickets;
using Keytietkiem.Hubs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit test cho TicketsController.CreateCustomerTicket
    /// UTCD01 – UTCD07 (đúng 7 test case như sheet):
    ///  - UTCD01: 400 - TemplateCode rỗng
    ///  - UTCD02: 400 - TemplateCode > 50 ký tự
    ///  - UTCD03: 400 - Description > 1000 ký tự
    ///  - UTCD04: 401 - Không đăng nhập (không có NameIdentifier)
    ///  - UTCD05: 403 - Sender không phải customer
    ///  - UTCD06: 400 - Template không tồn tại / inactive
    ///  - UTCD07: 200 - Tạo ticket thành công, kiểm tra đầy đủ state
    /// </summary>
    public class TicketsController_CreateCustomerTicketTests
    {
        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        private static IHubContext<TicketHub> CreateHubContextMock()
        {
            var clientProxy = new Mock<IClientProxy>();
            clientProxy
                .Setup(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hubClients = new Mock<IHubClients>();
            hubClients.Setup(c => c.Group(It.IsAny<string>()))
                      .Returns(clientProxy.Object);
            hubClients.SetupGet(c => c.All)
                      .Returns(clientProxy.Object);

            var hub = new Mock<IHubContext<TicketHub>>();
            hub.SetupGet(h => h.Clients).Returns(hubClients.Object);
            hub.SetupGet(h => h.Groups).Returns(Mock.Of<IGroupManager>());

            return hub.Object;
        }

        private static TicketsController CreateController(
            KeytietkiemDbContext db,
            string? nameIdentifierValue)
        {
            var hub = CreateHubContextMock();
            var auditLogger = new Mock<IAuditLogger>();

            var controller = new TicketsController(db, hub, auditLogger.Object);

            var httpContext = new DefaultHttpContext();
            if (nameIdentifierValue != null)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, nameIdentifierValue)
                };
                httpContext.User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "TestAuth"));
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        private static User CreateUser(Guid id, bool isCustomer, string status = "Active")
        {
            var roles = new List<Role>();

            if (isCustomer)
            {
                roles.Add(new Role
                {
                    RoleId = Guid.NewGuid().ToString(),
                    Code = "customer",
                    Name = "Customer"
                });
            }
            else
            {
                roles.Add(new Role
                {
                    RoleId = Guid.NewGuid().ToString(),
                    Code = "care-staff",
                    Name = "Care Staff"
                });
            }

            return new User
            {
                UserId = id,
                Email = isCustomer ? "customer@example.com" : "staff@example.com",
                FullName = isCustomer ? "Customer" : "Staff",
                Status = status,
                SupportPriorityLevel = 1,
                Roles = roles
            };
        }

        private static TicketSubjectTemplate CreateTemplate(
            string code = "GENERAL_SUPPORT",
            string title = "[Hỗ trợ] Test",
            string severity = "Medium",
            bool isActive = true)
        {
            return new TicketSubjectTemplate
            {
                TemplateCode = code,
                Title = title,
                Severity = severity,
                Category = "General",
                IsActive = isActive
            };
        }

        #endregion

        // ===================== UTCD01 =====================
        // TemplateCode null / empty / whitespace -> 400, message yêu cầu chọn loại vấn đề
        [Fact(DisplayName = "UTCD01 - TemplateCode empty -> 400 & no ticket created")]
        public async Task CreateCustomerTicket_TemplateCodeEmpty_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(CreateCustomerTicket_TemplateCodeEmpty_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, nameIdentifierValue: null);

            var dto = new CustomerCreateTicketDto
            {
                TemplateCode = "   ",
                Description = "anything"
            };

            var result = await controller.CreateCustomerTicket(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Vui lòng chọn loại vấn đề (tiêu đề ticket).", msg);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTCD02 =====================
        // TemplateCode > 50 ký tự -> 400 "Mã template không hợp lệ."
        [Fact(DisplayName = "UTCD02 - TemplateCode length > 50 -> 400 BadRequest")]
        public async Task CreateCustomerTicket_TemplateCodeTooLong_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(CreateCustomerTicket_TemplateCodeTooLong_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, nameIdentifierValue: null);

            var dto = new CustomerCreateTicketDto
            {
                TemplateCode = new string('X', 51),
                Description = "valid"
            };

            var result = await controller.CreateCustomerTicket(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Mã template không hợp lệ.", msg);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTCD03 =====================
        // Description > 1000 ký tự -> 400 "Mô tả ticket tối đa 1000 ký tự."
        [Fact(DisplayName = "UTCD03 - Description length > 1000 -> 400 BadRequest")]
        public async Task CreateCustomerTicket_DescriptionTooLong_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(CreateCustomerTicket_DescriptionTooLong_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, nameIdentifierValue: null);

            var dto = new CustomerCreateTicketDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Description = new string('A', 1001)
            };

            var result = await controller.CreateCustomerTicket(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Mô tả ticket tối đa 1000 ký tự.", msg);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTCD04 =====================
        // Không có authenticated user (không có NameIdentifier) -> 401
        [Fact(DisplayName = "UTCD04 - No authenticated user -> 401 Unauthorized")]
        public async Task CreateCustomerTicket_NoAuthenticatedUser_Returns401()
        {
            var options = CreateInMemoryOptions(nameof(CreateCustomerTicket_NoAuthenticatedUser_Returns401));
            using var db = new KeytietkiemDbContext(options);

            var dto = new CustomerCreateTicketDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Description = "Valid"
            };

            var controller = CreateController(db, nameIdentifierValue: null);

            var result = await controller.CreateCustomerTicket(dto);

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTCD05 =====================
        // User tồn tại nhưng không có role customer -> 403
        [Fact(DisplayName = "UTCD05 - Sender is not customer -> 403 Forbidden")]
        public async Task CreateCustomerTicket_SenderNotCustomer_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(CreateCustomerTicket_SenderNotCustomer_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var senderId = Guid.NewGuid();
            db.Users.Add(CreateUser(senderId, isCustomer: false));
            db.TicketSubjectTemplates.Add(CreateTemplate());
            db.SaveChanges();

            var controller = CreateController(db, nameIdentifierValue: senderId.ToString());

            var dto = new CustomerCreateTicketDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Description = "Valid desc"
            };

            var result = await controller.CreateCustomerTicket(dto);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            var msg = obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value)?.ToString();
            Assert.Equal("Chỉ khách hàng mới được phép tạo ticket.", msg);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTCD06 =====================
        // Customer hợp lệ nhưng template không tồn tại / inactive -> 400
        [Fact(DisplayName = "UTCD06 - Template not found/inactive -> 400 BadRequest")]
        public async Task CreateCustomerTicket_TemplateNotFound_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(CreateCustomerTicket_TemplateNotFound_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var senderId = Guid.NewGuid();
            db.Users.Add(CreateUser(senderId, isCustomer: true));
            // Không thêm TicketSubjectTemplate có code tương ứng
            db.SaveChanges();

            var controller = CreateController(db, nameIdentifierValue: senderId.ToString());

            var dto = new CustomerCreateTicketDto
            {
                TemplateCode = "MISSING_TEMPLATE",
                Description = "Valid"
            };

            var result = await controller.CreateCustomerTicket(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Loại vấn đề bạn chọn không hợp lệ hoặc đã bị vô hiệu hóa. Vui lòng tải lại trang và thử lại.", msg);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTCD07 =====================
        // Happy path: customer + template hợp lệ -> 200 OK & 1 ticket
        // Kiểm tra luôn:
        //  - Ticket được insert đúng dữ liệu
        //  - Status = New, AssignmentState = Unassigned, AssigneeId = null
        //  - TicketCode sinh từ GenerateNextTicketCodeAsync (ví dụ TCK-0010)
        //  - CreatedAt & UpdatedAt ~ now
        //  - SLA ApplyOnCreate: Severity & SlaStatus không null/empty
        [Fact(DisplayName = "UTCD07 - Valid customer & template -> 200 OK & ticket created with correct state")]
        public async Task CreateCustomerTicket_ValidInput_CreatesTicketWithCorrectState()
        {
            var options = CreateInMemoryOptions(nameof(CreateCustomerTicket_ValidInput_CreatesTicketWithCorrectState));
            using var db = new KeytietkiemDbContext(options);

            var senderId = Guid.NewGuid();
            db.Users.Add(CreateUser(senderId, isCustomer: true));

            db.TicketSubjectTemplates.Add(CreateTemplate(
                code: "GENERAL_SUPPORT",
                title: "[Hỗ trợ] Test ticket",
                severity: "High"));

            // Ticket cũ có mã TCK-0009 để test sinh mã tiếp theo
            db.Tickets.Add(new Ticket
            {
                TicketId = Guid.NewGuid(),
                UserId = senderId,
                Subject = "Old",
                Status = "New",
                AssigneeId = null,
                TicketCode = "TCK-0009",
                Severity = "Medium",
                SlaStatus = "OK",
                AssignmentState = "Unassigned",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                PriorityLevel = 1
            });

            db.SaveChanges();

            var before = DateTime.UtcNow;

            var controller = CreateController(db, nameIdentifierValue: senderId.ToString());

            var dto = new CustomerCreateTicketDto
            {
                TemplateCode = "GENERAL_SUPPORT",
                Description = "   My description   " // có khoảng trắng để test Trim
            };

            var result = await controller.CreateCustomerTicket(dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var created = Assert.IsType<CustomerTicketCreatedDto>(ok.Value);

            // ---- Kiểm tra DTO trả về ----
            Assert.Equal("[Hỗ trợ] Test ticket", created.Subject);
            Assert.Equal("My description", created.Description); // đã Trim
            Assert.Equal("New", created.Status);
            Assert.False(string.IsNullOrWhiteSpace(created.TicketCode));

            var after = DateTime.UtcNow;

            // ---- Kiểm tra DB ----
            var tickets = db.Tickets.OrderBy(t => t.TicketCode).ToList();
            Assert.Equal(2, tickets.Count);

            var oldTicket = tickets[0];
            var newTicket = tickets[1];

            Assert.Equal("TCK-0009", oldTicket.TicketCode);
            Assert.Equal("TCK-0010", newTicket.TicketCode); // GenerateNextTicketCodeAsync

            Assert.Equal(senderId, newTicket.UserId);
            Assert.Equal(created.TicketId, newTicket.TicketId);
            Assert.Equal(created.TicketCode, newTicket.TicketCode);

            Assert.Equal("New", newTicket.Status);
            Assert.Equal("Unassigned", newTicket.AssignmentState);
            Assert.Null(newTicket.AssigneeId);

            // Description đã Trim
            Assert.Equal("My description", newTicket.Description);

            // CreatedAt & UpdatedAt nằm trong khoảng before - after
            Assert.InRange(newTicket.CreatedAt, before, after);
            Assert.NotNull(newTicket.UpdatedAt);
            Assert.InRange(newTicket.UpdatedAt!.Value, before, after);

            // SLA ApplyOnCreate: Severity & SlaStatus không rỗng
            Assert.False(string.IsNullOrWhiteSpace(newTicket.Severity));
            Assert.False(string.IsNullOrWhiteSpace(newTicket.SlaStatus));
        }
    }
}
