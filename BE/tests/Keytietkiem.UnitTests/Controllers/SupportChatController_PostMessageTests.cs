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
    /// Unit tests cho SupportChatController.PostMessage (SendChatMessage).
    /// UT001: 400 BadRequest - nội dung trống
    /// UT002: 401 Unauthorized - không đăng nhập
    /// UT003: 404 NotFound - sessionId không tồn tại
    /// UT004: 400 BadRequest - phiên chat đã đóng
    /// UT005: 403 Forbidden - user không phải customer cũng không phải assigned staff
    /// UT006: 200 OK - customer gửi message (Active, IsFromStaff = false)
    /// UT007: 200 OK - staff gửi message (Waiting -> Active, IsFromStaff = true)
    /// </summary>
    public class SupportChatController_PostMessageTests
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

        private static User CreateUser(Guid id, bool isStaff)
        {
            var roles = new List<Role>();
            if (isStaff)
            {
                roles.Add(new Role
                {
                    RoleId = Guid.NewGuid().ToString(),
                    Code = "care-staff",
                    Name = "Care Staff"
                });
            }
            else
            {
                roles.Add(new Role
                {
                    RoleId = Guid.NewGuid().ToString(),
                    Code = "customer",
                    Name = "Customer"
                });
            }

            return new User
            {
                UserId = id,
                Email = isStaff ? "staff@example.com" : "customer@example.com",
                FullName = isStaff ? "Staff" : "Customer",
                Status = "Active",
                Roles = roles
            };
        }

        private static string BuildPreview(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            content = content.Trim();
            return content.Length <= 255 ? content : content.Substring(0, 255);
        }

        #endregion

        // ===================== UT001 =====================
        // Content empty / whitespace -> 400 BadRequest("Nội dung tin nhắn trống.")
        [Fact(DisplayName = "UT001 - Empty content -> 400 BadRequest")]
        public async Task PostMessage_EmptyContent_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(PostMessage_EmptyContent_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isStaff: false);
            db.Users.Add(customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                Status = "Active",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-10)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var result = await controller.PostMessage(
                session.ChatSessionId,
                new CreateSupportChatMessageDto { Content = "   " }); // whitespace

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Nội dung tin nhắn trống.", msg);

            Assert.Empty(db.SupportChatMessages); // không tạo message
        }

        // ===================== UT002 =====================
        // No authenticated user -> 401 Unauthorized
        [Fact(DisplayName = "UT002 - No authenticated user -> 401 Unauthorized")]
        public async Task PostMessage_NoAuthenticatedUser_Returns401()
        {
            var options = CreateInMemoryOptions(nameof(PostMessage_NoAuthenticatedUser_Returns401));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, currentUserId: null);

            var result = await controller.PostMessage(
                Guid.NewGuid(),
                new CreateSupportChatMessageDto { Content = "Hello" });

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Empty(db.SupportChatMessages);
        }

        // ===================== UT003 =====================
        // Authenticated, user exists nhưng sessionId không tồn tại -> 404
        [Fact(DisplayName = "UT003 - Session not found -> 404 NotFound")]
        public async Task PostMessage_SessionNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(PostMessage_SessionNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateUser(customerId, isStaff: false));
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var result = await controller.PostMessage(
                Guid.NewGuid(), // không tồn tại
                new CreateSupportChatMessageDto { Content = "Hello" });

            Assert.IsType<NotFoundResult>(result.Result);
        }

        // ===================== UT004 =====================
        // Session.Status == Closed -> 400 BadRequest("Phiên chat đã đóng.")
        [Fact(DisplayName = "UT004 - Closed session -> 400 BadRequest")]
        public async Task PostMessage_ClosedSession_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(PostMessage_ClosedSession_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isStaff: false);
            db.Users.Add(customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                Status = "Closed",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-20),
                ClosedAt = DateTime.UtcNow.AddMinutes(-1)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var result = await controller.PostMessage(
                session.ChatSessionId,
                new CreateSupportChatMessageDto { Content = "Hello" });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Phiên chat đã đóng.", msg);
            Assert.Empty(db.SupportChatMessages);
        }

        // ===================== UT005 =====================
        // User không phải customer cũng không phải assigned staff -> 403 Forbidden
        [Fact(DisplayName = "UT005 - Not owner & not assigned staff -> 403 Forbidden")]
        public async Task PostMessage_NotOwnerNorAssignedStaff_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(PostMessage_NotOwnerNorAssignedStaff_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var staffCurrentId = Guid.NewGuid();
            var staffAssignedId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var staffCurrent = CreateUser(staffCurrentId, isStaff: true);
            var staffAssigned = CreateUser(staffAssignedId, isStaff: true);
            var customer = CreateUser(customerId, isStaff: false);

            db.Users.AddRange(staffCurrent, staffAssigned, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = staffAssignedId,    // gán cho staff khác
                AssignedStaff = staffAssigned,
                Status = "Active",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-10)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staffCurrentId);

            var result = await controller.PostMessage(
                session.ChatSessionId,
                new CreateSupportChatMessageDto { Content = "Xin chào" });

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            var msg = obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value)?.ToString();
            Assert.Equal("Người dùng không có quyền gửi tin trong phiên chat này.", msg);

            Assert.Empty(db.SupportChatMessages);
        }

        // ===================== UT006 =====================
        // Customer gửi message trong phiên Active -> 200 OK,
        // 1 SupportChatMessage mới, Status giữ nguyên Active, IsFromStaff = false
        [Fact(DisplayName = "UT006 - Customer sends message (Active) -> 200 OK")]
        public async Task PostMessage_CustomerActiveSession_Success()
        {
            var options = CreateInMemoryOptions(nameof(PostMessage_CustomerActiveSession_Success));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isStaff: false);
            db.Users.Add(customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                Status = "Active",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                LastMessageAt = DateTime.UtcNow.AddMinutes(-5),
                LastMessagePreview = "old preview"
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var content = "Customer message to support";
            var result = await controller.PostMessage(
                session.ChatSessionId,
                new CreateSupportChatMessageDto { Content = content });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SupportChatMessageDto>(ok.Value);

            Assert.Equal(session.ChatSessionId, dto.ChatSessionId);
            Assert.Equal(customerId, dto.SenderId);
            Assert.False(dto.IsFromStaff);
            Assert.Equal(content, dto.Content);

            var msgEntity = Assert.Single(db.SupportChatMessages);
            Assert.Equal(customerId, msgEntity.SenderId);
            Assert.False(msgEntity.IsFromStaff);
            Assert.Equal(content, msgEntity.Content);

            Assert.Equal("Active", session.Status); // không đổi
            Assert.NotNull(session.LastMessageAt);
            Assert.Equal(BuildPreview(content), session.LastMessagePreview);
        }

        // ===================== UT007 =====================
        // Staff gửi message trong phiên Waiting -> 200 OK,
        // Status đổi từ Waiting -> Active, IsFromStaff = true
        [Fact(DisplayName = "UT007 - Staff sends first message (Waiting -> Active) -> 200 OK")]
        public async Task PostMessage_StaffWaitingSession_SuccessAndActivate()
        {
            var options = CreateInMemoryOptions(nameof(PostMessage_StaffWaitingSession_SuccessAndActivate));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var staff = CreateUser(staffId, isStaff: true);
            var customer = CreateUser(customerId, isStaff: false);

            db.Users.AddRange(staff, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = staffId,
                AssignedStaff = staff,
                Status = "Waiting",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-15)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var content = "Staff reply to customer";
            var result = await controller.PostMessage(
                session.ChatSessionId,
                new CreateSupportChatMessageDto { Content = content });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SupportChatMessageDto>(ok.Value);

            Assert.Equal(session.ChatSessionId, dto.ChatSessionId);
            Assert.Equal(staffId, dto.SenderId);
            Assert.True(dto.IsFromStaff);
            Assert.Equal(content, dto.Content);

            var msgEntity = Assert.Single(db.SupportChatMessages);
            Assert.Equal(staffId, msgEntity.SenderId);
            Assert.True(msgEntity.IsFromStaff);
            Assert.Equal(content, msgEntity.Content);

            Assert.Equal("Active", session.Status); // từ Waiting -> Active
            Assert.NotNull(session.LastMessageAt);
            Assert.Equal(BuildPreview(content), session.LastMessagePreview);
        }
    }
}
