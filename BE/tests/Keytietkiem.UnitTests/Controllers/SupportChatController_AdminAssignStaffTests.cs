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
    /// Unit tests cho SupportChatController.AdminAssignStaff (AdminAssignChatStaff).
    /// Mapping với sheet:
    ///  UT001: 401 Unauthorized – không đăng nhập
    ///  UT002: 403 Forbidden – user không phải admin
    ///  UT003: 400 BadRequest – AssigneeId = Guid.Empty
    ///  UT004: 404 NotFound – admin, session không tồn tại
    ///  UT005: 400 BadRequest – session Closed
    ///  UT006: 400 BadRequest – session đã có AssignedStaff
    ///  UT007: 400 BadRequest – staff candidate không phải active care staff
    ///  UT008: 200 OK – assign thành công (AssignedStaffId cập nhật, Status giữ nguyên)
    /// </summary>
    public class SupportChatController_AdminAssignStaffTests
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

        // ====================== UT001 ======================
        // Không có authenticated user -> 401 Unauthorized
        [Fact(DisplayName = "UT001 - No authenticated user -> 401 Unauthorized")]
        public async Task AdminAssignStaff_NoAuthenticatedUser_Returns401()
        {
            var options = CreateInMemoryOptions(nameof(AdminAssignStaff_NoAuthenticatedUser_Returns401));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, currentUserId: null);

            var result = await controller.AdminAssignStaff(
                Guid.NewGuid(),
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Empty(db.SupportChatSessions);
        }

        // ====================== UT002 ======================
        // Authenticated nhưng không phải admin -> 403 Forbidden
        [Fact(DisplayName = "UT002 - Authenticated non-admin user -> 403 Forbidden")]
        public async Task AdminAssignStaff_NonAdminUser_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(AdminAssignStaff_NonAdminUser_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            db.Users.Add(CreateStaff(staffId));
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.AdminAssignStaff(
                Guid.NewGuid(),
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            Assert.Equal("Chỉ admin mới được gán nhân viên cho phiên chat.", GetMessageFromObjectResult(obj));
        }

        // ====================== UT003 ======================
        // Admin nhưng AssigneeId = Guid.Empty -> 400 BadRequest
        [Fact(DisplayName = "UT003 - Admin, empty AssigneeId -> 400 BadRequest")]
        public async Task AdminAssignStaff_EmptyAssigneeId_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminAssignStaff_EmptyAssigneeId_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            db.Users.Add(CreateAdmin(adminId));
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminAssignStaff(
                Guid.NewGuid(),
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.Empty
                });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Vui lòng chọn nhân viên cần gán.", GetMessageFromObjectResult(bad));

            Assert.Empty(db.SupportChatSessions);
        }

        // ====================== UT004 ======================
        // Admin, AssigneeId hợp lệ nhưng session không tồn tại -> 404 NotFound
        [Fact(DisplayName = "UT004 - Admin, session not found -> 404 NotFound")]
        public async Task AdminAssignStaff_SessionNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(AdminAssignStaff_SessionNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            db.Users.Add(CreateAdmin(adminId));
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminAssignStaff(
                Guid.NewGuid(),
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            Assert.IsType<NotFoundResult>(result.Result);
            Assert.Empty(db.SupportChatSessions);
        }

        // ====================== UT005 ======================
        // Admin, session tồn tại nhưng Status = Closed -> 400 BadRequest
        [Fact(DisplayName = "UT005 - Admin, Closed session -> 400 BadRequest")]
        public async Task AdminAssignStaff_ClosedSession_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminAssignStaff_ClosedSession_Returns400));
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
                Status = "Closed",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                ClosedAt = DateTime.UtcNow.AddMinutes(-5)
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminAssignStaff(
                session.ChatSessionId,
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Phiên chat đã đóng, không thể gán nhân viên.",
                GetMessageFromObjectResult(bad));

            Assert.Equal("Closed", session.Status);
            Assert.Null(session.AssignedStaffId);
        }

        // ====================== UT006 ======================
        // Admin, session chưa đóng nhưng đã có AssignedStaff -> 400 BadRequest
        [Fact(DisplayName = "UT006 - Admin, session already has staff -> 400 BadRequest")]
        public async Task AdminAssignStaff_AlreadyHasStaff_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminAssignStaff_AlreadyHasStaff_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var assignedStaffId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var assignedStaff = CreateStaff(assignedStaffId);
            var customer = CreateCustomer(customerId);

            db.Users.AddRange(admin, assignedStaff, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = assignedStaffId,
                AssignedStaff = assignedStaff,
                Status = "Waiting",
                PriorityLevel = 2,
                StartedAt = DateTime.UtcNow.AddMinutes(-20)
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminAssignStaff(
                session.ChatSessionId,
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = Guid.NewGuid()
                });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Phiên chat đã có nhân viên, hãy dùng chức năng chuyển nhân viên.",
                GetMessageFromObjectResult(bad));

            Assert.Equal(assignedStaffId, session.AssignedStaffId);
            Assert.Equal("Waiting", session.Status);
        }

        // ====================== UT007 ======================
        // Admin, session OK, chưa có staff nhưng staff candidate không hợp lệ -> 400 BadRequest
        [Fact(DisplayName = "UT007 - Invalid staff candidate (not active care staff) -> 400 BadRequest")]
        public async Task AdminAssignStaff_InvalidStaffCandidate_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AdminAssignStaff_InvalidStaffCandidate_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var invalidStaffId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var invalidStaff = CreateCustomer(invalidStaffId); // role "customer" -> không chứa "care"
            var customer = CreateCustomer(customerId);

            db.Users.AddRange(admin, invalidStaff, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = null,
                Status = "Waiting",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-15)
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminAssignStaff(
                session.ChatSessionId,
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = invalidStaffId
                });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active).",
                GetMessageFromObjectResult(bad));

            Assert.Null(session.AssignedStaffId);
            Assert.Equal("Waiting", session.Status);
        }

        // ====================== UT008 ======================
        // Admin, session OK, chưa có staff và staff candidate hợp lệ -> 200 OK + update
        [Fact(DisplayName = "UT008 - Admin assigns valid staff successfully -> 200 OK")]
        public async Task AdminAssignStaff_ValidScenario_Success()
        {
            var options = CreateInMemoryOptions(nameof(AdminAssignStaff_ValidScenario_Success));
            using var db = new KeytietkiemDbContext(options);

            var adminId = Guid.NewGuid();
            var staffId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var admin = CreateAdmin(adminId);
            var staff = CreateStaff(staffId);
            var customer = CreateCustomer(customerId);

            db.Users.AddRange(admin, staff, customer);

            var originalStatus = "Waiting";

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = null,
                AssignedStaff = null,
                Status = originalStatus,
                PriorityLevel = 3,
                StartedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, adminId);

            var result = await controller.AdminAssignStaff(
                session.ChatSessionId,
                new SupportChatController.SupportChatAssignStaffDto
                {
                    AssigneeId = staffId
                });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SupportChatSessionItemDto>(ok.Value);

            // DTO phải phản ánh state mới
            Assert.Equal(staffId, dto.AssignedStaffId);
            Assert.Equal(originalStatus, dto.Status);

            // DB cũng phải được update
            var reloaded = db.SupportChatSessions.Single(s => s.ChatSessionId == session.ChatSessionId);
            Assert.Equal(staffId, reloaded.AssignedStaffId);
            Assert.Equal(originalStatus, reloaded.Status);
        }
    }
}
