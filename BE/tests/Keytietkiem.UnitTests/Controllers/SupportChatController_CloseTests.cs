using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
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
    /// Unit tests cho SupportChatController.Close (CloseChatSession).
    /// UT001 – 401 Unauthorized (no user)
    /// UT002 – 404 NotFound (session not found)
    /// UT003 – 403 Forbidden (user không có quyền)
    /// UT004 – 204 NoContent (session đã Closed, idempotent)
    /// UT005 – 204 NoContent (customer đóng phiên)
    /// UT006 – 204 NoContent (assigned staff đóng phiên)
    /// UT007 – 204 NoContent (admin đóng phiên)
    /// </summary>
    public class SupportChatController_CloseTests
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

        private static User CreateCustomer(Guid id) => CreateUserCore(id, "customer", "Customer");
        private static User CreateStaff(Guid id) => CreateUserCore(id, "care-staff", "Care Staff");
        private static User CreateAdmin(Guid id) => CreateUserCore(id, "admin", "Admin");

        #endregion

        // ========== UT001 ==========
        // Không có authenticated user -> 401, không có DB change
        [Fact(DisplayName = "UT001 - No authenticated user -> 401 Unauthorized")]
        public async Task Close_NoAuthenticatedUser_Returns401()
        {
            var options = CreateInMemoryOptions(nameof(Close_NoAuthenticatedUser_Returns401));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, currentUserId: null);

            var result = await controller.Close(Guid.NewGuid());

            Assert.IsType<UnauthorizedResult>(result);
            Assert.Empty(db.SupportChatSessions);
        }

        // ========== UT002 ==========
        // Staff-like nhưng sessionId không tồn tại -> 404
        [Fact(DisplayName = "UT002 - Staff user, session not found -> 404 NotFound")]
        public async Task Close_SessionNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Close_SessionNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            db.Users.Add(CreateStaff(staffId));
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.Close(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
            Assert.Empty(db.SupportChatSessions);
        }

        // ========== UT003 ==========
        // User không phải customer, không phải assigned, không phải admin -> 403 Forbidden
        [Fact(DisplayName = "UT003 - User not customer/assigned/admin -> 403 Forbidden")]
        public async Task Close_UserHasNoAccess_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(Close_UserHasNoAccess_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var staffCurrentId = Guid.NewGuid();
            var staffAssignedId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var staffCurrent = CreateStaff(staffCurrentId);
            var staffAssigned = CreateStaff(staffAssignedId);
            var customer = CreateCustomer(customerId);

            db.Users.AddRange(staffCurrent, staffAssigned, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = staffAssignedId,
                AssignedStaff = staffAssigned,
                Status = "Active",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-20)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staffCurrentId);

            var result = await controller.Close(session.ChatSessionId);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            var msg = obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value)?.ToString();
            Assert.Equal("Người dùng không có quyền đóng phiên chat này.", msg);

            // Không đổi DB
            Assert.Equal("Active", session.Status);
            Assert.Null(session.ClosedAt);
        }

        // ========== UT004 ==========
        // Phiên đã Closed, user có quyền (customer) -> 204, không đổi state
        [Fact(DisplayName = "UT004 - Already closed session (customer) -> 204 NoContent, no change")]
        public async Task Close_AlreadyClosedSession_Idempotent()
        {
            var options = CreateInMemoryOptions(nameof(Close_AlreadyClosedSession_Idempotent));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateCustomer(customerId);
            db.Users.Add(customer);

            var closedAt = DateTime.UtcNow.AddMinutes(-5);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = null,
                Status = "Closed",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                ClosedAt = closedAt
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var result = await controller.Close(session.ChatSessionId);

            Assert.IsType<NoContentResult>(result);

            // Không đổi status/ClosedAt
            Assert.Equal("Closed", session.Status);
            Assert.Equal(closedAt, session.ClosedAt);
        }

        // ========== UT005 ==========
        // Customer đóng phiên của mình (Status != Closed) -> 204, Status=Closed, ClosedAt set
        [Fact(DisplayName = "UT005 - Customer closes own session -> 204 NoContent, Closed state")]
        public async Task Close_CustomerClosesOwnSession_Success()
        {
            var options = CreateInMemoryOptions(nameof(Close_CustomerClosesOwnSession_Success));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateCustomer(customerId);
            db.Users.Add(customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = null,
                Status = "Active",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-15)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var before = DateTime.UtcNow;
            var result = await controller.Close(session.ChatSessionId);
            var after = DateTime.UtcNow;

            Assert.IsType<NoContentResult>(result);

            Assert.Equal("Closed", session.Status);
            Assert.NotNull(session.ClosedAt);
            Assert.InRange(session.ClosedAt.Value, before, after);
        }

        // ========== UT006 ==========
        // Assigned staff đóng phiên -> 204, Status=Closed, ClosedAt set
        [Fact(DisplayName = "UT006 - Assigned staff closes session -> 204 NoContent, Closed state")]
        public async Task Close_AssignedStaffClosesSession_Success()
        {
            var options = CreateInMemoryOptions(nameof(Close_AssignedStaffClosesSession_Success));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var staff = CreateStaff(staffId);
            var customer = CreateCustomer(customerId);

            db.Users.AddRange(staff, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = staffId,
                AssignedStaff = staff,
                Status = "Active",
                PriorityLevel = 2,
                StartedAt = DateTime.UtcNow.AddMinutes(-25)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var before = DateTime.UtcNow;
            var result = await controller.Close(session.ChatSessionId);
            var after = DateTime.UtcNow;

            Assert.IsType<NoContentResult>(result);

            Assert.Equal("Closed", session.Status);
            Assert.NotNull(session.ClosedAt);
            Assert.InRange(session.ClosedAt.Value, before, after);
        }

        // ========== UT007 ==========
        // Admin đóng phiên (không phải customer/assigned) -> 204, Closed
        [Fact(DisplayName = "UT007 - Admin closes any session -> 204 NoContent, Closed state")]
        public async Task Close_AdminClosesSession_Success()
        {
            var options = CreateInMemoryOptions(nameof(Close_AdminClosesSession_Success));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var staff = CreateStaff(staffId);
            var customer = CreateCustomer(customerId);

            db.Users.AddRange(admin, staff, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = staffId,
                AssignedStaff = staff,
                Status = "Waiting",
                PriorityLevel = 3,
                StartedAt = DateTime.UtcNow.AddMinutes(-40)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var before = DateTime.UtcNow;
            var result = await controller.Close(session.ChatSessionId);
            var after = DateTime.UtcNow;

            Assert.IsType<NoContentResult>(result);

            Assert.Equal("Closed", session.Status);
            Assert.NotNull(session.ClosedAt);
            Assert.InRange(session.ClosedAt.Value, before, after);
        }
    }
}
