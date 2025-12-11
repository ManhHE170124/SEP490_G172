using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Support;
using Keytietkiem.Hubs;
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
    /// Unit test cho SupportChatController.AdminPostMessage (AdminSendChatMessage).
    /// UT001: 400 BadRequest – nội dung trống
    /// UT002: 401 Unauthorized – không đăng nhập
    /// UT003: 403 Forbidden – user không phải admin
    /// UT004: 404 NotFound – admin, session không tồn tại
    /// UT005: 400 BadRequest – admin, session đã Closed
    /// UT006: 200 OK – admin gửi tin thành công (Active/Waiting)
    /// </summary>
    public class SupportChatController_AdminPostMessageTests
    {
        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        private static IHubContext<SupportChatHub> CreateHubContextMock()
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

            var hub = new Mock<IHubContext<SupportChatHub>>();
            hub.SetupGet(h => h.Clients).Returns(hubClients.Object);
            hub.SetupGet(h => h.Groups).Returns(Mock.Of<IGroupManager>());

            return hub.Object;
        }

        private static SupportChatController CreateController(
            KeytietkiemDbContext db,
            Guid? currentUserId)
        {
            var hub = CreateHubContextMock();
            var auditLogger = new Mock<IAuditLogger>();

            var controller = new SupportChatController(db, hub, auditLogger.Object);

            var httpContext = new DefaultHttpContext();
            if (currentUserId.HasValue)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, currentUserId.Value.ToString())
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

        private static User CreateUserCore(Guid id, string roleCode, string roleName)
        {
            var role = new Role
            {
                RoleId = Guid.NewGuid().ToString(),
                Code = roleCode,
                Name = roleName
            };

            return new User
            {
                UserId = id,
                Email = $"{roleCode}@example.com",
                FullName = roleName,
                Status = "Active",
                Roles = new List<Role> { role }
            };
        }

        private static User CreateAdmin(Guid id) => CreateUserCore(id, "admin", "Admin");
        private static User CreateStaff(Guid id) => CreateUserCore(id, "care-staff", "Care Staff");
        private static User CreateCustomer(Guid id) => CreateUserCore(id, "customer", "Customer");

        private static string BuildPreview(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            content = content.Trim();
            return content.Length <= 255 ? content : content.Substring(0, 255);
        }

        private static string? GetMessageFromObjectResult(ObjectResult result)
        {
            return result.Value?
                .GetType()
                .GetProperty("message")?
                .GetValue(result.Value)?
                .ToString();
        }

        #endregion

        // ========== UT001 ==========
        // Content rỗng/whitespace -> 400 BadRequest "Nội dung tin nhắn trống.",
        // không insert message, không đụng DB.
        [Fact(DisplayName = "UT001 - Empty content -> 400 BadRequest")]
        public async Task AdminPostMessage_EmptyContent_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminPostMessage_EmptyContent_Returns400));
            using var db = new KeytietkiemDbContext(options);

            // Có hay không currentUserId đều được, vì hàm return sớm trước khi check auth
            var controller = CreateController(db, Guid.NewGuid());

            var result = await controller.AdminPostMessage(
                Guid.NewGuid(),
                new CreateSupportChatMessageDto { Content = "   " });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Nội dung tin nhắn trống.", GetMessageFromObjectResult(bad));
            Assert.Empty(db.SupportChatMessages);
        }

        // ========== UT002 ==========
        // Không có authenticated user -> 401 Unauthorized
        [Fact(DisplayName = "UT002 - No authenticated user -> 401 Unauthorized")]
        public async Task AdminPostMessage_NoAuthenticatedUser_Returns401()
        {
            var options = CreateInMemoryOptions(nameof(AdminPostMessage_NoAuthenticatedUser_Returns401));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, currentUserId: null);

            var result = await controller.AdminPostMessage(
                Guid.NewGuid(),
                new CreateSupportChatMessageDto { Content = "Hello" });

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Empty(db.SupportChatMessages);
        }

        // ========== UT003 ==========
        // Authenticated user nhưng không phải admin -> 403 Forbidden
        [Fact(DisplayName = "UT003 - Auth but not admin -> 403 Forbidden")]
        public async Task AdminPostMessage_NonAdminUser_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(AdminPostMessage_NonAdminUser_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            db.Users.Add(CreateStaff(staffId));
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.AdminPostMessage(
                Guid.NewGuid(),
                new CreateSupportChatMessageDto { Content = "Hello" });

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            Assert.Equal("Chỉ admin mới được gửi tin theo chế độ admin.", GetMessageFromObjectResult(obj));
            Assert.Empty(db.SupportChatMessages);
        }

        // ========== UT004 ==========
        // Admin, nhưng sessionId không tồn tại -> 404 NotFound
        [Fact(DisplayName = "UT004 - Admin, session not found -> 404 NotFound")]
        public async Task AdminPostMessage_SessionNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(AdminPostMessage_SessionNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            db.Users.Add(CreateAdmin(adminId));
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminPostMessage(
                Guid.NewGuid(),
                new CreateSupportChatMessageDto { Content = "Hello" });

            Assert.IsType<NotFoundResult>(result.Result);
            Assert.Empty(db.SupportChatMessages);
        }

        // ========== UT005 ==========
        // Admin, session tồn tại nhưng Status = Closed -> 400 BadRequest "Phiên chat đã đóng."
        [Fact(DisplayName = "UT005 - Admin, session Closed -> 400 BadRequest")]
        public async Task AdminPostMessage_ClosedSession_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminPostMessage_ClosedSession_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var customer = CreateCustomer(customerId);

            db.Users.AddRange(admin, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                Status = "Closed",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                ClosedAt = DateTime.UtcNow.AddMinutes(-5),
                LastMessageAt = DateTime.UtcNow.AddMinutes(-10),
                LastMessagePreview = "old"
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminPostMessage(
                session.ChatSessionId,
                new CreateSupportChatMessageDto { Content = "Admin message" });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Phiên chat đã đóng.", GetMessageFromObjectResult(bad));

            // Không thay đổi session & không insert message mới
            Assert.Empty(db.SupportChatMessages);
            Assert.Equal("Closed", session.Status);
            Assert.NotNull(session.ClosedAt);
            Assert.Equal("old", session.LastMessagePreview);
        }

        // ========== UT006 ==========
        // Admin, session Active/Waiting -> gửi thành công:
        //  - 200 OK, message mới insert
        //  - LastMessageAt & LastMessagePreview update
        //  - Status, AssignedStaffId giữ nguyên
        [Fact(DisplayName = "UT006 - Admin sends message successfully -> 200 OK")]
        public async Task AdminPostMessage_AdminSuccess_InsertsMessageAndUpdatesLastFields()
        {
            var options = CreateInMemoryOptions(nameof(AdminPostMessage_AdminSuccess_InsertsMessageAndUpdatesLastFields));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var staff = CreateStaff(staffId);
            var customer = CreateCustomer(customerId);

            db.Users.AddRange(admin, staff, customer);

            var originalStatus = "Active";
            var originalAssigned = staffId;

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = originalAssigned,
                AssignedStaff = staff,
                Status = originalStatus,
                PriorityLevel = 2,
                StartedAt = DateTime.UtcNow.AddMinutes(-20),
                LastMessageAt = DateTime.UtcNow.AddMinutes(-10),
                LastMessagePreview = "old preview"
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var content = "   Admin sends message to both sides   ";
            var before = DateTime.UtcNow;

            var result = await controller.AdminPostMessage(
                session.ChatSessionId,
                new CreateSupportChatMessageDto { Content = content });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SupportChatMessageDto>(ok.Value);

            // Kiểm tra message DTO
            Assert.Equal(session.ChatSessionId, dto.ChatSessionId);
            Assert.Equal(adminId, dto.SenderId);
            Assert.True(dto.IsFromStaff);
            Assert.Equal(content.Trim(), dto.Content);
            Assert.InRange(dto.SentAt, before, DateTime.UtcNow);

            // Message entity trong DB
            var msgEntity = Assert.Single(db.SupportChatMessages);
            Assert.Equal(session.ChatSessionId, msgEntity.ChatSessionId);
            Assert.Equal(adminId, msgEntity.SenderId);
            Assert.True(msgEntity.IsFromStaff);
            Assert.Equal(content.Trim(), msgEntity.Content);

            // Session state cập nhật đúng
            Assert.Equal(originalStatus, session.Status);             // Không đổi
            Assert.Equal(originalAssigned, session.AssignedStaffId); // Không đổi
            Assert.NotNull(session.LastMessageAt);
            Assert.InRange(session.LastMessageAt.Value, before, DateTime.UtcNow);
            Assert.Equal(BuildPreview(content), session.LastMessagePreview);
        }
    }
}
