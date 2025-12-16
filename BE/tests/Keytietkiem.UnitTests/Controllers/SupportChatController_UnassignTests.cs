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
    /// Unit test cho SupportChatController.Unassign (UnassignChatSession).
    /// Mapping với sheet:
    ///  UT001: 401 Unauthorized - không đăng nhập
    ///  UT002: 403 Forbidden - user không phải staff-like
    ///  UT003: 404 NotFound - sessionId không tồn tại
    ///  UT004: 400 BadRequest - phiên chat đã đóng
    ///  UT005: 403 Forbidden - staff nhưng không được quyền (không phải assigned staff, không phải admin)
    ///  UT006: 200 OK - unassign bởi chính assigned staff
    ///  UT007: 200 OK - unassign bởi admin (assignedStaffId là staff khác)
    /// </summary>
    public class SupportChatController_UnassignTests
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

        // ===================== UT001 =====================
        // No authenticated user -> 401, không đổi DB
        [Fact(DisplayName = "UT001 - No authenticated user -> 401 Unauthorized")]
        public async Task Unassign_NoAuthenticatedUser_Returns401()
        {
            var options = CreateInMemoryOptions(nameof(Unassign_NoAuthenticatedUser_Returns401));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, currentUserId: null);

            var result = await controller.Unassign(Guid.NewGuid());

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Empty(db.SupportChatSessions);
        }

        // ===================== UT002 =====================
        // Authenticated nhưng role chỉ là customer -> 403 Forbidden
        [Fact(DisplayName = "UT002 - Auth but non-staff user -> 403 Forbidden")]
        public async Task Unassign_NonStaffUser_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(Unassign_NonStaffUser_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateCustomer(customerId));
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var result = await controller.Unassign(Guid.NewGuid());

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);

            var msg = obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value)?.ToString();
            Assert.Equal("Chỉ nhân viên hỗ trợ mới được trả lại phiên chat.", msg);

            Assert.Empty(db.SupportChatSessions);
        }

        // ===================== UT003 =====================
        // Staff-like nhưng sessionId không tồn tại -> 404
        [Fact(DisplayName = "UT003 - Staff user, session not found -> 404 NotFound")]
        public async Task Unassign_SessionNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Unassign_SessionNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            db.Users.Add(CreateStaff(staffId));
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.Unassign(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
            Assert.Empty(db.SupportChatSessions);
        }

        // ===================== UT004 =====================
        // Session tồn tại nhưng Status = Closed -> 400 BadRequest
        [Fact(DisplayName = "UT004 - Closed session -> 400 BadRequest")]
        public async Task Unassign_ClosedSession_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Unassign_ClosedSession_Returns400));
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
                Status = "Closed",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                ClosedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.Unassign(session.ChatSessionId);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Phiên chat đã đóng, không thể trả lại hàng chờ.", msg);

            // Không đổi DB
            Assert.Single(db.SupportChatSessions);
            Assert.Equal("Closed", session.Status);
            Assert.Equal(staffId, session.AssignedStaffId);
        }

        // ===================== UT005 =====================
        // Staff nhưng AssignedStaffId thuộc staff khác, không phải admin -> 403 Forbidden
        [Fact(DisplayName = "UT005 - Staff but not assigned & not admin -> 403 Forbidden")]
        public async Task Unassign_StaffNotAssignedAndNotAdmin_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(Unassign_StaffNotAssignedAndNotAdmin_Returns403));
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

            var result = await controller.Unassign(session.ChatSessionId);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);

            var msg = obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value)?.ToString();
            Assert.Equal("Bạn không phải nhân viên đang phụ trách phiên chat này.", msg);

            // Không đổi DB
            Assert.Equal(staffAssignedId, session.AssignedStaffId);
            Assert.Equal("Active", session.Status);
        }

        // ===================== UT006 =====================
        // Unassign bởi chính assigned staff -> 200 OK, AssignedStaffId null, Status = Waiting
        [Fact(DisplayName = "UT006 - Unassign by assigned staff -> 200 OK (Waiting, no staff)")]
        public async Task Unassign_ByAssignedStaff_Success()
        {
            var options = CreateInMemoryOptions(nameof(Unassign_ByAssignedStaff_Success));
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
                StartedAt = DateTime.UtcNow.AddMinutes(-15)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.Unassign(session.ChatSessionId);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SupportChatSessionItemDto>(ok.Value);

            Assert.Null(dto.AssignedStaffId);
            Assert.Equal("Waiting", dto.Status);

            // Reload & kiểm tra state
            var reloaded = db.SupportChatSessions.Single(s => s.ChatSessionId == session.ChatSessionId);
            Assert.Null(reloaded.AssignedStaffId);
            Assert.Null(reloaded.AssignedStaff);
            Assert.Equal("Waiting", reloaded.Status);
        }

        // ===================== UT007 =====================
        // Unassign bởi admin cho session đang assigned staff khác -> 200 OK
        [Fact(DisplayName = "UT007 - Unassign by admin for other staff -> 200 OK (Waiting, no staff)")]
        public async Task Unassign_ByAdminForOtherStaff_Success()
        {
            var options = CreateInMemoryOptions(nameof(Unassign_ByAdminForOtherStaff_Success));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var staffAssignedId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);       // role chứa "admin"
            var staffAssigned = CreateStaff(staffAssignedId);
            var customer = CreateCustomer(customerId);

            db.Users.AddRange(admin, staffAssigned, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = staffAssignedId,
                AssignedStaff = staffAssigned,
                Status = "Active",
                PriorityLevel = 3,
                StartedAt = DateTime.UtcNow.AddMinutes(-25)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.Unassign(session.ChatSessionId);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SupportChatSessionItemDto>(ok.Value);

            // DTO phản ánh state mới
            Assert.Null(dto.AssignedStaffId);
            Assert.Equal("Waiting", dto.Status);

            // DB state
            var reloaded = db.SupportChatSessions.Single(s => s.ChatSessionId == session.ChatSessionId);
            Assert.Null(reloaded.AssignedStaffId);
            Assert.Null(reloaded.AssignedStaff);
            Assert.Equal("Waiting", reloaded.Status);
        }
    }
}
