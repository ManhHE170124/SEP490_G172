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
    /// Unit test cho SupportChatController.Claim (ClaimChatSession)
    /// UT001–UT007 như trong bảng test:
    ///  - 401 khi không đăng nhập
    ///  - 403 khi user không phải staff
    ///  - 404 khi sessionId không tồn tại
    ///  - 400 khi session Closed
    ///  - 409 khi session đã gán cho staff khác
    ///  - 200 khi claim phiên chưa gán (unassigned) -> gán cho current staff + Active
    ///  - 200 khi claim lại phiên đã gán cho chính mình (idempotent)
    /// </summary>
    public class SupportChatController_ClaimTests
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
                    RoleId = Guid.NewGuid().ToString(),   // string
                    Code = "care-staff",
                    Name = "Care Staff"                   // <-- THÊM DÒNG NÀY
                });
            }
            else
            {
                roles.Add(new Role
                {
                    RoleId = Guid.NewGuid().ToString(),
                    Code = "customer",
                    Name = "Customer"                     // <-- THÊM DÒNG NÀY
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

        #endregion

        // ===================== UT001 =====================
        // Không có authenticated user -> 401, không đổi DB
        [Fact(DisplayName = "UT001 - No authenticated user -> 401 & no DB change")]
        public async Task Claim_NoAuthenticatedUser_Returns401_NoDbChange()
        {
            var options = CreateInMemoryOptions(nameof(Claim_NoAuthenticatedUser_Returns401_NoDbChange));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, currentUserId: null);

            var result = await controller.Claim(Guid.NewGuid());

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Empty(db.SupportChatSessions);
        }

        // ===================== UT002 =====================
        // Authenticated nhưng không phải staff-like -> 403, không đổi DB
        [Fact(DisplayName = "UT002 - Auth but non-staff user -> 403 Forbidden")]
        public async Task Claim_NonStaffUser_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(Claim_NonStaffUser_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateUser(customerId, isStaff: false));
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var result = await controller.Claim(Guid.NewGuid());

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            Assert.Empty(db.SupportChatSessions);
        }

        // ===================== UT003 =====================
        // Staff-like nhưng sessionId không tồn tại -> 404
        [Fact(DisplayName = "UT003 - Staff user, session not found -> 404 NotFound")]
        public async Task Claim_SessionNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Claim_SessionNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            db.Users.Add(CreateUser(staffId, isStaff: true));
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.Claim(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }

        // ===================== UT004 =====================
        // Session tồn tại nhưng Status = Closed -> 400 BadRequest("Phiên chat đã đóng.")
        [Fact(DisplayName = "UT004 - Staff user, session Closed -> 400 BadRequest")]
        public async Task Claim_ClosedSession_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Claim_ClosedSession_Returns400));
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
                Status = "Closed",
                PriorityLevel = 2,
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                ClosedAt = DateTime.UtcNow.AddMinutes(-1)
            };
            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.Claim(session.ChatSessionId);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Phiên chat đã đóng.", msg);
        }

        // ===================== UT005 =====================
        // Session đang Active và đã gán cho staff khác -> 409 Conflict
        [Fact(DisplayName = "UT005 - Session assigned to another staff -> 409 Conflict")]
        public async Task Claim_AssignedToOtherStaff_Returns409()
        {
            var options = CreateInMemoryOptions(nameof(Claim_AssignedToOtherStaff_Returns409));
            using var db = new KeytietkiemDbContext(options);

            var staff1Id = Guid.NewGuid(); // current
            var staff2Id = Guid.NewGuid(); // already assigned
            var customerId = Guid.NewGuid();

            var staff1 = CreateUser(staff1Id, isStaff: true);
            var staff2 = CreateUser(staff2Id, isStaff: true);
            var customer = CreateUser(customerId, isStaff: false);

            db.Users.AddRange(staff1, staff2, customer);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = staff2Id,
                AssignedStaff = staff2,
                Status = "Active",
                PriorityLevel = 1,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                LastMessageAt = DateTime.UtcNow.AddMinutes(-1),
                LastMessagePreview = "hi"
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staff1Id);

            var result = await controller.Claim(session.ChatSessionId);

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
            var msg = conflict.Value?.GetType().GetProperty("message")?.GetValue(conflict.Value)?.ToString();
            Assert.Equal("Phiên chat đã được gán cho nhân viên khác.", msg);
        }

        // ===================== UT006 =====================
        // Session Waiting, chưa gán -> claim thành công:
        //  - AssignedStaffId = current staff
        //  - Status = Active
        //  - LastMessageAt từ StartedAt nếu trước đó null
        [Fact(DisplayName = "UT006 - Claim unassigned Waiting session -> Active, assign current staff")]
        public async Task Claim_UnassignedWaitingSession_AssignsAndActivates()
        {
            var options = CreateInMemoryOptions(nameof(Claim_UnassignedWaitingSession_AssignsAndActivates));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var staff = CreateUser(staffId, isStaff: true);
            var customer = CreateUser(customerId, isStaff: false);

            db.Users.AddRange(staff, customer);

            var startedAt = DateTime.UtcNow.AddMinutes(-3);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = null,
                Status = "Waiting",
                PriorityLevel = 2,
                StartedAt = startedAt,
                LastMessageAt = null,
                LastMessagePreview = null
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.Claim(session.ChatSessionId);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SupportChatSessionItemDto>(ok.Value);

            Assert.Equal(staffId, dto.AssignedStaffId);
            Assert.Equal("Active", dto.Status);

            // Reload từ DB để kiểm tra
            var reloaded = db.SupportChatSessions.Single(s => s.ChatSessionId == session.ChatSessionId);
            Assert.Equal(staffId, reloaded.AssignedStaffId);
            Assert.Equal("Active", reloaded.Status);
            Assert.Equal(startedAt, reloaded.StartedAt);
            Assert.Equal(startedAt, reloaded.LastMessageAt); // set từ StartedAt khi null
        }

        // ===================== UT007 =====================
        // Idempotent claim: session đã được gán cho chính staff hiện tại
        //  -> 200 OK, không thay đổi trạng thái/AssignedStaff/LastMessageAt
        [Fact(DisplayName = "UT007 - Idempotent claim same staff -> 200 OK, no state change")]
        public async Task Claim_AlreadyAssignedToSameStaff_Idempotent()
        {
            var options = CreateInMemoryOptions(nameof(Claim_AlreadyAssignedToSameStaff_Idempotent));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var staff = CreateUser(staffId, isStaff: true);
            var customer = CreateUser(customerId, isStaff: false);

            db.Users.AddRange(staff, customer);

            var startedAt = DateTime.UtcNow.AddMinutes(-10);
            var lastMsgAt = DateTime.UtcNow.AddMinutes(-5);

            var session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Customer = customer,
                AssignedStaffId = staffId,
                AssignedStaff = staff,
                Status = "Active",
                PriorityLevel = 1,
                StartedAt = startedAt,
                LastMessageAt = lastMsgAt,
                LastMessagePreview = "preview"
            };

            db.SupportChatSessions.Add(session);
            db.SaveChanges();

            var controller = CreateController(db, staffId);

            var result = await controller.Claim(session.ChatSessionId);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<SupportChatSessionItemDto>(ok.Value);

            Assert.Equal(staffId, dto.AssignedStaffId);
            Assert.Equal("Active", dto.Status);
            Assert.Equal(lastMsgAt, dto.LastMessageAt);

            // Đảm bảo DB không thay đổi
            var reloaded = db.SupportChatSessions.Single(s => s.ChatSessionId == session.ChatSessionId);
            Assert.Equal(staffId, reloaded.AssignedStaffId);
            Assert.Equal("Active", reloaded.Status);
            Assert.Equal(lastMsgAt, reloaded.LastMessageAt);
        }
    }
}
