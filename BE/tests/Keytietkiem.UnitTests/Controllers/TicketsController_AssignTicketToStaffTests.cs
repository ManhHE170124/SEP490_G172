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
    /// Unit test cho TicketsController.Assign (AssignTicketToStaff)
    /// 5 test case – map với sheet:
    ///  UTAT01: 404 NotFound – ticket không tồn tại
    ///  UTAT02: 400 BadRequest – ticket đã khoá (Closed/Completed)
    ///  UTAT03: 400 BadRequest – nhân viên không hợp lệ (không phải care staff & active)
    ///  UTAT04: 204 NoContent – assign từ New + Unassigned -> Assigned + InProgress
    ///  UTAT05: 204 NoContent – re-assign khi Status != New, AssignmentState đã Assigned
    /// </summary>
    public class TicketsController_AssignTicketToStaffTests
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

        private static TicketsController CreateController(KeytietkiemDbContext db)
        {
            var hub = CreateHubContextMock();
            var auditLogger = new Mock<IAuditLogger>();

            var controller = new TicketsController(db, hub, auditLogger.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
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

        private static Ticket CreateBasicTicket(Guid ticketId, Guid customerId, User customer,
            string status, string assignmentState, Guid? assigneeId = null)
        {
            return new Ticket
            {
                TicketId = ticketId,
                UserId = customerId,
                User = customer,
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

        // ===================== UTAT01 =====================
        // Ticket không tồn tại -> 404 NotFound, không update gì
        [Fact(DisplayName = "UTAT01 - Ticket not found -> 404 NotFound")]
        public async Task Assign_TicketNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Assign_TicketNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = Guid.NewGuid()
            };

            var result = await controller.Assign(Guid.NewGuid(), dto);

            Assert.IsType<NotFoundResult>(result);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTAT02 =====================
        // Ticket tồn tại nhưng Status = Closed -> 400 "Ticket đã khoá."
        [Fact(DisplayName = "UTAT02 - Closed ticket -> 400 BadRequest \"Ticket đã khoá.\"")]
        public async Task Assign_ClosedTicket_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Assign_ClosedTicket_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isCareStaff: false);
            db.Users.Add(customer);

            var ticketId = Guid.NewGuid();
            var ticket = CreateBasicTicket(ticketId, customerId, customer,
                status: "Closed", assignmentState: "Unassigned");
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = Guid.NewGuid()
            };

            var result = await controller.Assign(ticketId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Ticket đã khoá.", msg);

            // Không thay đổi trạng thái / assignee / UpdatedAt
            Assert.Equal("Closed", ticket.Status);
            Assert.Equal("Unassigned", ticket.AssignmentState);
            Assert.Null(ticket.AssigneeId);
            Assert.Null(ticket.UpdatedAt);
        }

        // ===================== UTAT03 =====================
        // Ticket hợp lệ nhưng staff không hợp lệ -> 400 "Nhân viên không hợp lệ ..."
        [Fact(DisplayName = "UTAT03 - Invalid staff -> 400 BadRequest \"Nhân viên không hợp lệ\"")]
        public async Task Assign_InvalidStaff_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Assign_InvalidStaff_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isCareStaff: false);
            db.Users.Add(customer);

            // Ticket New + Unassigned
            var ticketId = Guid.NewGuid();
            var ticket = CreateBasicTicket(ticketId, customerId, customer,
                status: "New", assignmentState: "Unassigned");
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            // Không tạo user có id = assigneeId nên userOk = false
            var assigneeId = Guid.NewGuid();
            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = assigneeId
            };

            var result = await controller.Assign(ticketId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active).", msg);

            // Ticket không bị cập nhật
            Assert.Equal("New", ticket.Status);
            Assert.Equal("Unassigned", ticket.AssignmentState);
            Assert.Null(ticket.AssigneeId);
            Assert.Null(ticket.UpdatedAt);
        }

        // ===================== UTAT04 =====================
        // Assign thành công từ New + Unassigned:
        //  - AssignmentState: Unassigned -> Assigned
        //  - Status: New -> InProgress
        //  - AssigneeId set = staff
        //  - UpdatedAt được set
        [Fact(DisplayName = "UTAT04 - Assign from New & Unassigned -> Assigned & InProgress, 204 NoContent")]
        public async Task Assign_FromNewUnassigned_Success()
        {
            var options = CreateInMemoryOptions(nameof(Assign_FromNewUnassigned_Success));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isCareStaff: false);
            db.Users.Add(customer);

            var staffId = Guid.NewGuid();
            var staff = CreateUser(staffId, isCareStaff: true, status: "Active");
            db.Users.Add(staff);

            var ticketId = Guid.NewGuid();
            var ticket = CreateBasicTicket(ticketId, customerId, customer,
                status: "New", assignmentState: "Unassigned");
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = staffId
            };

            var before = DateTime.UtcNow;

            var result = await controller.Assign(ticketId, dto);

            Assert.IsType<NoContentResult>(result);

            var after = DateTime.UtcNow;

            // Kiểm tra state sau assign
            Assert.Equal("Assigned", ticket.AssignmentState);
            Assert.Equal("InProgress", ticket.Status);
            Assert.Equal(staffId, ticket.AssigneeId);
            Assert.NotNull(ticket.UpdatedAt);
            Assert.InRange(ticket.UpdatedAt!.Value, before, after);
        }

        // ===================== UTAT05 =====================
        // Re-assign khi ticket đang InProgress & Assigned:
        //  - AssignmentState giữ nguyên "Assigned"
        //  - Status giữ nguyên "InProgress"
        //  - AssigneeId đổi sang staff mới
        //  - UpdatedAt được update
        [Fact(DisplayName = "UTAT05 - Reassign in non-New status -> keep status & assignment, 204 NoContent")]
        public async Task Assign_ReassignWhenAlreadyAssigned_Success()
        {
            var options = CreateInMemoryOptions(nameof(Assign_ReassignWhenAlreadyAssigned_Success));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            var customer = CreateUser(customerId, isCareStaff: false);
            db.Users.Add(customer);

            var oldStaffId = Guid.NewGuid();
            var oldStaff = CreateUser(oldStaffId, isCareStaff: true);
            db.Users.Add(oldStaff);

            var newStaffId = Guid.NewGuid();
            var newStaff = CreateUser(newStaffId, isCareStaff: true);
            db.Users.Add(newStaff);

            var ticketId = Guid.NewGuid();
            var ticket = CreateBasicTicket(ticketId, customerId, customer,
                status: "InProgress", assignmentState: "Assigned", assigneeId: oldStaffId);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = newStaffId
            };

            var before = DateTime.UtcNow;

            var result = await controller.Assign(ticketId, dto);

            Assert.IsType<NoContentResult>(result);

            var after = DateTime.UtcNow;

            // Status & AssignmentState giữ nguyên
            Assert.Equal("Assigned", ticket.AssignmentState);
            Assert.Equal("InProgress", ticket.Status);

            // AssigneeId đổi sang staff mới
            Assert.Equal(newStaffId, ticket.AssigneeId);

            // UpdatedAt được update
            Assert.NotNull(ticket.UpdatedAt);
            Assert.InRange(ticket.UpdatedAt!.Value, before, after);
        }
    }
}
