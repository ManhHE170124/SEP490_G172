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
    /// Unit tests cho SupportChatController.AdminTransferStaff (AdminTransferChatStaff).
    /// UT001: 401 Unauthorized – không đăng nhập
    /// UT002: 403 Forbidden – user không phải admin
    /// UT003: 400 BadRequest – AssigneeId trống
    /// UT004: 404 NotFound – session không tồn tại
    /// UT005: 400 BadRequest – session Closed
    /// UT006: 400 BadRequest – session chưa có nhân viên
    /// UT007: 400 BadRequest – AssigneeId trùng AssignedStaffId hiện tại
    /// UT008: 400 BadRequest – staff candidate không hợp lệ
    /// UT009: 200 OK – admin chuyển nhân viên thành công
    /// </summary>
    public class SupportChatController_AdminTransferStaffTests
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
        [Fact(DisplayName = "UT001 - No authenticated user -> 401 Unauthorized")]
        public async Task AdminTransferStaff_NoAuthenticatedUser_Returns401()
        {
            var options = CreateInMemoryOptions(nameof(AdminTransferStaff_NoAuthenticatedUser_Returns401));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, currentUserId: null);

            var result = await controller.AdminTransferStaff(
                Guid.NewGuid(),
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Empty(db.SupportChatSessions);
        }

        // ========== UT002 ==========
        [Fact(DisplayName = "UT002 - Authenticated non-admin user -> 403 Forbidden")]
        public async Task AdminTransferStaff_NonAdminUser_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(AdminTransferStaff_NonAdminUser_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            db.Users.Add(CreateStaff(staffId));
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.AdminTransferStaff(
                Guid.NewGuid(),
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            Assert.Equal(
                "Chỉ admin mới được chuyển nhân viên phụ trách phiên chat.",
                GetMessageFromObjectResult(obj));
        }

        // ========== UT003 ==========
        [Fact(DisplayName = "UT003 - Admin, empty AssigneeId -> 400 BadRequest")]
        public async Task AdminTransferStaff_EmptyAssigneeId_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminTransferStaff_EmptyAssigneeId_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            db.Users.Add(CreateAdmin(adminId));
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminTransferStaff(
                Guid.NewGuid(),
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.Empty
                });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Vui lòng chọn nhân viên cần chuyển tới.",
                GetMessageFromObjectResult(bad));

            Assert.Empty(db.SupportChatSessions);
        }

        // ========== UT004 ==========
        [Fact(DisplayName = "UT004 - Admin, session not found -> 404 NotFound")]
        public async Task AdminTransferStaff_SessionNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(AdminTransferStaff_SessionNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            db.Users.Add(CreateAdmin(adminId));
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminTransferStaff(
                Guid.NewGuid(),
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            Assert.IsType<NotFoundResult>(result.Result);
            Assert.Empty(db.SupportChatSessions);
        }

        // ========== UT005 ==========
        [Fact(DisplayName = "UT005 - Admin, Closed session -> 400 BadRequest")]
        public async Task AdminTransferStaff_ClosedSession_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminTransferStaff_ClosedSession_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var oldStaffId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var customer = CreateCustomer(customerId);
            var oldStaff = CreateStaff(oldStaffId);

            db.Users.AddRange(admin, customer, oldStaff);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = oldStaffId,
                AssignedStaff = oldStaff,
                Status = "Closed",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-40),
                ClosedAt = DateTime.UtcNow.AddMinutes(-5)
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminTransferStaff(
                session.ChatSessionId,
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Phiên chat đã đóng, không thể chuyển nhân viên.",
                GetMessageFromObjectResult(bad));

            Assert.Equal("Closed", session.Status);
            Assert.Equal(oldStaffId, session.AssignedStaffId);
        }

        // ========== UT006 ==========
        [Fact(DisplayName = "UT006 - Admin, session has no staff -> 400 BadRequest")]
        public async Task AdminTransferStaff_NoAssignedStaff_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminTransferStaff_NoAssignedStaff_Returns400));
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
                AssignedStaffId = null,
                AssignedStaff = null,
                Status = "Active",
                PriorityLevel = 2,
                StartedAt = DateTime.UtcNow.AddMinutes(-30)
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminTransferStaff(
                session.ChatSessionId,
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Phiên chat chưa có nhân viên, hãy dùng chức năng gán nhân viên.",
                GetMessageFromObjectResult(bad));

            Assert.Null(session.AssignedStaffId);
            Assert.Equal("Active", session.Status);
        }

        // ========== UT007 ==========
        [Fact(DisplayName = "UT007 - Admin, assignee equals current staff -> 400 BadRequest")]
        public async Task AdminTransferStaff_AssigneeSameAsCurrent_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminTransferStaff_AssigneeSameAsCurrent_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var staffId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var customer = CreateCustomer(customerId);
            var staff = CreateStaff(staffId);

            db.Users.AddRange(admin, customer, staff);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = staffId,
                AssignedStaff = staff,
                Status = "Waiting",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-25)
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminTransferStaff(
                session.ChatSessionId,
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = staffId
                });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Vui lòng chọn nhân viên khác với người đang phụ trách.",
                GetMessageFromObjectResult(bad));

            Assert.Equal(staffId, session.AssignedStaffId);
            Assert.Equal("Waiting", session.Status);
        }

        // ========== UT008 ==========
        [Fact(DisplayName = "UT008 - Invalid staff candidate (not active care staff) -> 400 BadRequest")]
        public async Task AdminTransferStaff_InvalidStaffCandidate_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminTransferStaff_InvalidStaffCandidate_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var currentStaffId = Guid.NewGuid();
            var invalidStaffId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var customer = CreateCustomer(customerId);
            var currentStaff = CreateStaff(currentStaffId);
            // candidate không phải care-staff
            var invalidStaff = CreateCustomer(invalidStaffId);

            db.Users.AddRange(admin, customer, currentStaff, invalidStaff);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = currentStaffId,
                AssignedStaff = currentStaff,
                Status = "Active",
                PriorityLevel = 2,
                StartedAt = DateTime.UtcNow.AddMinutes(-15)
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminTransferStaff(
                session.ChatSessionId,
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = invalidStaffId
                });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active).",
                GetMessageFromObjectResult(bad));

            Assert.Equal(currentStaffId, session.AssignedStaffId);
            Assert.Equal("Active", session.Status);
        }

        // ========== UT009 ==========
        [Fact(DisplayName = "UT009 - Admin transfers staff successfully -> 200 OK")]
        public async Task AdminTransferStaff_ValidScenario_Success()
        {
            var options = CreateInMemoryOptions(nameof(AdminTransferStaff_ValidScenario_Success));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var oldStaffId = Guid.NewGuid();
            var newStaffId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var customer = CreateCustomer(customerId);
            var oldStaff = CreateStaff(oldStaffId);
            var newStaff = CreateStaff(newStaffId);

            db.Users.AddRange(admin, customer, oldStaff, newStaff);

            var originalStatus = "Active";

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = oldStaffId,
                AssignedStaff = oldStaff,
                Status = originalStatus,
                PriorityLevel = 3,
                StartedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminTransferStaff(
                session.ChatSessionId,
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = newStaffId
                });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SupportChatSessionItemDto>(ok.Value);

            // DTO
            Assert.Equal(newStaffId, dto.AssignedStaffId);
            Assert.Equal(originalStatus, dto.Status);

            // DB
            var reloaded = db.SupportChatSessions.Single(s => s.ChatSessionId == session.ChatSessionId);
            Assert.Equal(newStaffId, reloaded.AssignedStaffId);
            Assert.Equal(originalStatus, reloaded.Status);
        }
    }
}
