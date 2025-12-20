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
    /// Unit tests cho TicketRepliesController.CreateReply (CreateTicketReply)
    /// UT001: 400 BadRequest - nội dung trống
    /// UT002: 404 NotFound   - ticket không tồn tại
    /// UT003: 401 Unauthorized - không có/không parse được NameIdentifier
    /// UT004: 403 Forbidden  - không phải owner/assignee/admin
    /// UT005: 200 OK         - customer (owner) reply, IsStaffReply = false
    /// UT006: 200 OK         - staff/assignee reply, IsStaffReply = true
    /// </summary>
    public class TicketRepliesController_CreateReplyTests
    {
        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        /// <summary>
        /// Tạo mock HubContext cho TicketHub, dùng để verify SignalR broadcast.
        /// </summary>
        private static IHubContext<TicketHub> CreateHubMock(
            out Mock<IClientProxy> clientProxyMock,
            out Func<string?> getLastGroupName)
        {
            clientProxyMock = new Mock<IClientProxy>();

            clientProxyMock
                .Setup(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            string? lastGroupName = null;

            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock
                .Setup(c => c.Group(It.IsAny<string>()))
                .Returns(clientProxyMock.Object)
                .Callback<string>(g => lastGroupName = g);

            var hubContextMock = new Mock<IHubContext<TicketHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);
            hubContextMock.SetupGet(h => h.Groups).Returns(Mock.Of<IGroupManager>());

            getLastGroupName = () => lastGroupName;
            return hubContextMock.Object;
        }

        /// <summary>
        /// Tạo controller với HttpContext đã set claim NameIdentifier (nếu có).
        /// Đồng thời trả về mock clientProxy & auditLogger để verify.
        /// </summary>
        private static TicketRepliesController CreateController(
            KeytietkiemDbContext db,
            Guid? currentUserId,
            out Mock<IClientProxy> clientProxyMock,
            out Func<string?> getLastGroupName,
            out Mock<IAuditLogger> auditLoggerMock)
        {
            var hub = CreateHubMock(out clientProxyMock, out getLastGroupName);

            auditLoggerMock = new Mock<IAuditLogger>();
            auditLoggerMock
                .Setup(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()))
                .Returns(Task.CompletedTask);

            var controller = new TicketRepliesController(db, hub, auditLoggerMock.Object);

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

        /// <summary>
        /// Tạo User với 1 Role, tránh trùng khóa RoleId giữa các user.
        /// - Với admin: RoleId = "admin", Name = "admin" để code controller nhận ra admin.
        /// - Với các role khác: RoleId là Guid mới.
        /// </summary>
        private static User CreateUser(Guid id, string email, string roleCode)
        {
            var normalized = roleCode.Trim().ToLowerInvariant();

            string roleId;
            string roleName;

            if (normalized == "admin")
            {
                roleId = "admin";
                roleName = "admin";
            }
            else
            {
                roleId = Guid.NewGuid().ToString(); // tránh trùng khóa
                roleName = roleCode;
            }

            return new User
            {
                UserId = id,
                Email = email,
                FullName = email,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                SupportPriorityLevel = 0,
                TotalProductSpend = 0,
                IsTemp = false,
                Roles = new List<Role>
                {
                    new Role
                    {
                        RoleId = roleId,
                        Name = roleName,
                        Code = roleCode
                    }
                }
            };
        }

        private static string? GetMessage(ObjectResult result)
        {
            return result.Value?
                .GetType()
                .GetProperty("message")?
                .GetValue(result.Value)?
                .ToString();
        }

        private static Ticket CreateBasicTicket(Guid ticketId, Guid ownerId)
        {
            var now = DateTime.UtcNow;
            return new Ticket
            {
                TicketId = ticketId,
                UserId = ownerId,
                Subject = "Test ticket",
                Description = "Desc",
                Status = "New",
                AssigneeId = null,
                TicketCode = "TCK-0001",
                Severity = null,
                SlaStatus = "OK",
                AssignmentState = "Unassigned",
                CreatedAt = now,
                UpdatedAt = now,
                PriorityLevel = 1
            };
        }

        #endregion

        // ===================== UT001 =====================
        // Message null/empty/whitespace -> 400 BadRequest, không chạm DB / SignalR / Audit
        [Fact(DisplayName = "UT001 - Empty message -> 400 BadRequest, no reply created")]
        public async Task CreateReply_EmptyMessage_Returns400_NoReply()
        {
            var options = CreateInMemoryOptions(nameof(CreateReply_EmptyMessage_Returns400_NoReply));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(
                db,
                currentUserId: null,
                out var clientProxyMock,
                out var getGroup,
                out var auditLoggerMock);

            var result = await controller.CreateReply(
                Guid.NewGuid(),
                new CreateTicketReplyDto { Message = "   " });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Nội dung phản hồi trống.", GetMessage(bad));

            Assert.Empty(db.TicketReplies);

            clientProxyMock.Verify(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.Null(getGroup());

            auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ===================== UT002 =====================
        // Ticket không tồn tại -> 404 NotFound, không tạo reply
        [Fact(DisplayName = "UT002 - Ticket not found -> 404 NotFound")]
        public async Task CreateReply_TicketNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(CreateReply_TicketNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(
                db,
                currentUserId: Guid.NewGuid(),
                out var clientProxyMock,
                out var getGroup,
                out var auditLoggerMock);

            var result = await controller.CreateReply(
                Guid.NewGuid(),
                new CreateTicketReplyDto { Message = "hello" });

            Assert.IsType<NotFoundResult>(result.Result);
            Assert.Empty(db.TicketReplies);

            clientProxyMock.Verify(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.Null(getGroup());

            auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ===================== UT003 =====================
        // Có ticket nhưng không có claim NameIdentifier hợp lệ -> 401 Unauthorized
        [Fact(DisplayName = "UT003 - No authenticated user (NameIdentifier) -> 401 Unauthorized")]
        public async Task CreateReply_NoAuthenticatedUser_Returns401()
        {
            var options = CreateInMemoryOptions(nameof(CreateReply_NoAuthenticatedUser_Returns401));
            using var db = new KeytietkiemDbContext(options);

            var ownerId = Guid.NewGuid();
            // seed owner user để ticket.Include(User) không bị filter
            var owner = CreateUser(ownerId, "owner@example.com", "customer");
            db.Users.Add(owner);

            db.Tickets.Add(CreateBasicTicket(Guid.NewGuid(), ownerId));
            db.SaveChanges();

            var controller = CreateController(
                db,
                currentUserId: null, // không set claim
                out var clientProxyMock,
                out var getGroup,
                out var auditLoggerMock);

            var ticketId = db.Tickets.Single().TicketId;

            var result = await controller.CreateReply(
                ticketId,
                new CreateTicketReplyDto { Message = "hello" });

            Assert.IsType<UnauthorizedResult>(result.Result);
            Assert.Empty(db.TicketReplies);

            clientProxyMock.Verify(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.Null(getGroup());

            auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ===================== UT004 =====================
        // Authenticated nhưng không phải owner, không phải assignee, không phải admin -> 403 Forbidden
        [Fact(DisplayName = "UT004 - User is not owner/assignee/admin -> 403 Forbidden")]
        public async Task CreateReply_NoPermission_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(CreateReply_NoPermission_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            var owner = CreateUser(ownerId, "owner@example.com", "customer");
            var other = CreateUser(otherId, "other@example.com", "customer");

            db.Users.AddRange(owner, other);
            db.Tickets.Add(CreateBasicTicket(Guid.NewGuid(), ownerId));
            db.SaveChanges();

            var ticketId = db.Tickets.Single().TicketId;

            var controller = CreateController(
                db,
                currentUserId: otherId,
                out var clientProxyMock,
                out var getGroup,
                out var auditLoggerMock);

            var result = await controller.CreateReply(
                ticketId,
                new CreateTicketReplyDto { Message = "no permission" });

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            Assert.Equal("Người dùng không có quyền hạn để phản hồi.", GetMessage(obj));

            Assert.Empty(db.TicketReplies);

            clientProxyMock.Verify(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.Null(getGroup());

            auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ===================== UT005 =====================
        // Customer (owner) reply -> 200 OK, IsStaffReply = false,
        // FirstRespondedAt không set, Status giữ nguyên
        [Fact(DisplayName = "UT005 - Ticket owner replies -> 200 OK, customer reply")]
        public async Task CreateReply_OwnerReply_Succeeds_AsCustomer()
        {
            var options = CreateInMemoryOptions(nameof(CreateReply_OwnerReply_Succeeds_AsCustomer));
            using var db = new KeytietkiemDbContext(options);

            var ownerId = Guid.NewGuid();
            var owner = CreateUser(ownerId, "owner@example.com", "customer");
            db.Users.Add(owner);

            var ticket = CreateBasicTicket(Guid.NewGuid(), ownerId);
            db.Tickets.Add(ticket);

            db.SaveChanges();

            var controller = CreateController(
                db,
                currentUserId: ownerId,
                out var clientProxyMock,
                out var getGroup,
                out var auditLoggerMock);

            var msg = "Customer reply";
            var result = await controller.CreateReply(
                ticket.TicketId,
                new CreateTicketReplyDto { Message = msg });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<TicketReplyDto>(ok.Value);

            Assert.Equal(ownerId, dto.SenderId);
            Assert.False(dto.IsStaffReply);
            Assert.Equal(msg, dto.Message);

            var reply = Assert.Single(db.TicketReplies);
            Assert.Equal(ticket.TicketId, reply.TicketId);
            Assert.Equal(ownerId, reply.SenderId);
            Assert.False(reply.IsStaffReply);
            Assert.Equal(msg, reply.Message);

            var updatedTicket = db.Tickets.Single();
            Assert.Null(updatedTicket.FirstRespondedAt);
            Assert.Equal("New", updatedTicket.Status);
            Assert.NotNull(updatedTicket.UpdatedAt);

            clientProxyMock.Verify(p => p.SendCoreAsync(
                    "ReceiveReply",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Equal($"ticket:{ticket.TicketId}", getGroup());

            // Owner reply -> không log audit
            auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ===================== UT006 =====================
        // Staff (assignee) reply lần đầu khi ticket đang New -> 200 OK,
        // IsStaffReply = true, FirstRespondedAt được set, Status đổi sang InProgress
        [Fact(DisplayName = "UT006 - Staff assignee first reply on New ticket -> 200 OK, staff reply")]
        public async Task CreateReply_StaffFirstReply_SetsFirstRespondedAt_AndStatus()
        {
            var options = CreateInMemoryOptions(nameof(CreateReply_StaffFirstReply_SetsFirstRespondedAt_AndStatus));
            using var db = new KeytietkiemDbContext(options);

            var ownerId = Guid.NewGuid();
            var staffId = Guid.NewGuid();

            var owner = CreateUser(ownerId, "owner@example.com", "customer");
            var staff = CreateUser(staffId, "staff@example.com", "care-staff");

            db.Users.AddRange(owner, staff);

            var ticket = CreateBasicTicket(Guid.NewGuid(), ownerId);
            ticket.AssigneeId = staffId; // staff được gán
            db.Tickets.Add(ticket);

            db.SaveChanges();

            var controller = CreateController(
                db,
                currentUserId: staffId,
                out var clientProxyMock,
                out var getGroup,
                out var auditLoggerMock);

            var msg = "Staff reply";
            var result = await controller.CreateReply(
                ticket.TicketId,
                new CreateTicketReplyDto { Message = msg });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<TicketReplyDto>(ok.Value);

            Assert.Equal(staffId, dto.SenderId);
            Assert.True(dto.IsStaffReply);
            Assert.Equal(msg, dto.Message);

            var reply = Assert.Single(db.TicketReplies);
            Assert.Equal(ticket.TicketId, reply.TicketId);
            Assert.Equal(staffId, reply.SenderId);
            Assert.True(reply.IsStaffReply);

            var updatedTicket = db.Tickets.Single();
            Assert.NotNull(updatedTicket.FirstRespondedAt);
            Assert.Equal("InProgress", updatedTicket.Status);
            Assert.NotNull(updatedTicket.UpdatedAt);

            clientProxyMock.Verify(p => p.SendCoreAsync(
                    "ReceiveReply",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Equal($"ticket:{ticket.TicketId}", getGroup());

            // Staff reply -> có log audit
            auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "StaffReply",
                    "TicketReply",
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Once);
        }
    }
}
