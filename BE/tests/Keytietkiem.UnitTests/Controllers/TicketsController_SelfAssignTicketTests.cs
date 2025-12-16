using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
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
    /// Unit test cho TicketsController.AssignToMe (SelfAssignTicket)
    /// 6 test case khớp sheet:
    ///  - UTST01: 401 Unauthorized, không xác định được người dùng hiện tại
    ///  - UTST02: 404 NotFound, ticket không tồn tại
    ///  - UTST03: 400 BadRequest, ticket đã khoá (Closed/Completed)
    ///  - UTST04: 403 Forbidden, current user không phải Active care staff
    ///  - UTST05: 204 NoContent, self-assign từ New + Unassigned
    ///  - UTST06: 204 NoContent, self-assign khi đã InProgress + Assigned (re-assign)
    /// </summary>
    public class TicketsController_SelfAssignTicketTests
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

        /// <summary>
        /// Tạo controller và inject Claim NameIdentifier (nếu có).
        /// </summary>
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

        private static User CreateUser(Guid id, bool isCareStaff, string status = "Active")
        {
            var roles = new List<Role>();

            if (isCareStaff)
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
                Email = isCareStaff ? "staff@example.com" : "customer@example.com",
                FullName = isCareStaff ? "Staff" : "Customer",
                Status = status,
                Roles = roles
            };
        }

        private static Ticket CreateTicket(
            Guid ticketId,
            Guid customerId,
            string status,
            string assignmentState,
            Guid? assigneeId = null)
        {
            return new Ticket
            {
                TicketId = ticketId,
                UserId = customerId,
                Subject = "Test ticket",
                Description = "Desc",
                Status = status,
                AssigneeId = assigneeId,
                TicketCode = "TCK-0001",
                Severity = "Medium",
                SlaStatus = "OK",
                AssignmentState = assignmentState,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                UpdatedAt = null,
                PriorityLevel = 1
            };
        }

        #endregion

        // ===================== UTST01 =====================
        // Không có / không parse được NameIdentifier -> 401 + message
        [Fact(DisplayName = "UTST01 - No/invalid NameIdentifier -> 401 Unauthorized")]
        public async Task AssignToMe_InvalidUserIdentity_Returns401()
        {
            var options = CreateInMemoryOptions(nameof(AssignToMe_InvalidUserIdentity_Returns401));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isCareStaff: false);
            db.Users.Add(customer);

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(ticketId, customerId, status: "New", assignmentState: "Unassigned");
            db.Tickets.Add(ticket);
            db.SaveChanges();

            // Không gắn NameIdentifier
            var controller = CreateController(db, nameIdentifierValue: null);

            var result = await controller.AssignToMe(ticketId);

            var obj = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, obj.StatusCode);
            var msg = obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value)?.ToString();
            Assert.Equal("Không xác định được người dùng hiện tại.", msg);

            Assert.Null(ticket.AssigneeId);
            Assert.Equal("New", ticket.Status);
            Assert.Equal("Unassigned", ticket.AssignmentState);
            Assert.Null(ticket.UpdatedAt);
        }


        // ===================== UTST02 =====================
        // Ticket không tồn tại -> 404
        [Fact(DisplayName = "UTST02 - Ticket not found -> 404 NotFound")]
        public async Task AssignToMe_TicketNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(AssignToMe_TicketNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            db.Users.Add(CreateUser(staffId, isCareStaff: true));
            db.SaveChanges();

            var controller = CreateController(db, staffId.ToString());

            var result = await controller.AssignToMe(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTST03 =====================
        // Ticket Closed/Completed -> 400 "Ticket đã khoá, không thể nhận thêm."
        [Fact(DisplayName = "UTST03 - Closed ticket -> 400 BadRequest (Ticket đã khoá)")]
        public async Task AssignToMe_TicketClosed_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(AssignToMe_TicketClosed_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            var staff = CreateUser(staffId, isCareStaff: true);
            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isCareStaff: false);
            db.Users.AddRange(staff, customer);

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(ticketId, customerId, status: "Closed", assignmentState: "Unassigned");
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db, staffId.ToString());

            var result = await controller.AssignToMe(ticketId);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Ticket đã khoá, không thể nhận thêm.", msg);

            // Ticket không bị cập nhật
            Assert.Null(ticket.AssigneeId);
            Assert.Equal("Closed", ticket.Status);
            Assert.Equal("Unassigned", ticket.AssignmentState);
            Assert.Null(ticket.UpdatedAt);
        }

        // ===================== UTST04 =====================
        // Current user không phải Active care staff -> 403 Forbidden
        [Fact(DisplayName = "UTST04 - Current user not care staff -> 403 Forbidden")]
        public async Task AssignToMe_InvalidStaff_Returns403()
        {
            var options = CreateInMemoryOptions(nameof(AssignToMe_InvalidStaff_Returns403));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            var invalidStaff = CreateUser(staffId, isCareStaff: false); // role customer
            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isCareStaff: false);
            db.Users.AddRange(invalidStaff, customer);

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(ticketId, customerId, status: "New", assignmentState: "Unassigned");
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db, staffId.ToString());

            var result = await controller.AssignToMe(ticketId);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            var msg = obj.Value?.GetType().GetProperty("message")?.GetValue(obj.Value)?.ToString();
            Assert.Equal("Bạn không có quyền nhận ticket này.", msg);

            // Ticket không bị cập nhật
            Assert.Null(ticket.AssigneeId);
            Assert.Equal("New", ticket.Status);
            Assert.Equal("Unassigned", ticket.AssignmentState);
            Assert.Null(ticket.UpdatedAt);
        }

        // ===================== UTST05 =====================
        // Self-assign từ New + Unassigned -> 204
        //  - AssigneeId = current user
        //  - AssignmentState: Unassigned -> Assigned
        //  - Status: New -> InProgress
        //  - UpdatedAt được set
        [Fact(DisplayName = "UTST05 - Self-assign from New & Unassigned -> Assigned & InProgress (204)")]
        public async Task AssignToMe_FromNewUnassigned_Success()
        {
            var options = CreateInMemoryOptions(nameof(AssignToMe_FromNewUnassigned_Success));
            using var db = new KeytietkiemDbContext(options);

            var staffId = Guid.NewGuid();
            var staff = CreateUser(staffId, isCareStaff: true);
            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isCareStaff: false);
            db.Users.AddRange(staff, customer);

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(ticketId, customerId, status: "New", assignmentState: "Unassigned");
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db, staffId.ToString());

            var before = DateTime.UtcNow;

            var result = await controller.AssignToMe(ticketId);

            var after = DateTime.UtcNow;

            Assert.IsType<NoContentResult>(result);

            Assert.Equal(staffId, ticket.AssigneeId);
            Assert.Equal("Assigned", ticket.AssignmentState);
            Assert.Equal("InProgress", ticket.Status);
            Assert.NotNull(ticket.UpdatedAt);
            Assert.InRange(ticket.UpdatedAt!.Value, before, after);
        }

        // ===================== UTST06 =====================
        // Self-assign khi ticket đã InProgress + Assigned:
        //  - AssignmentState giữ "Assigned"
        //  - Status giữ "InProgress"
        //  - AssigneeId đổi sang current user
        //  - UpdatedAt update
        [Fact(DisplayName = "UTST06 - Self-assign when already Assigned & InProgress -> reassign (204)")]
        public async Task AssignToMe_AlreadyAssignedInProgress_Success()
        {
            var options = CreateInMemoryOptions(nameof(AssignToMe_AlreadyAssignedInProgress_Success));
            using var db = new KeytietkiemDbContext(options);

            var currentStaffId = Guid.NewGuid();
            var currentStaff = CreateUser(currentStaffId, isCareStaff: true);

            var oldStaffId = Guid.NewGuid();
            var oldStaff = CreateUser(oldStaffId, isCareStaff: true);

            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isCareStaff: false);

            db.Users.AddRange(currentStaff, oldStaff, customer);

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(ticketId, customerId,
                status: "InProgress",
                assignmentState: "Assigned",
                assigneeId: oldStaffId);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db, currentStaffId.ToString());

            var before = DateTime.UtcNow;

            var result = await controller.AssignToMe(ticketId);

            var after = DateTime.UtcNow;

            Assert.IsType<NoContentResult>(result);

            // Status + AssignmentState giữ nguyên
            Assert.Equal("Assigned", ticket.AssignmentState);
            Assert.Equal("InProgress", ticket.Status);

            // AssigneeId đổi sang current staff
            Assert.Equal(currentStaffId, ticket.AssigneeId);

            Assert.NotNull(ticket.UpdatedAt);
            Assert.InRange(ticket.UpdatedAt!.Value, before, after);
        }
    }
}
