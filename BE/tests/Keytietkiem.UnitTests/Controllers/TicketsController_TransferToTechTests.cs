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
    /// Unit test cho TicketsController.TransferToTech (TransferTicketToTechStaff).
    /// 7 test case khớp sheet:
    ///  - UTTR01: 404 NotFound (ticket không tồn tại)
    ///  - UTTR02: 400 BadRequest, "Ticket đã khoá."
    ///  - UTTR03: 400 BadRequest, "Vui lòng gán trước khi chuyển hỗ trợ."
    ///  - UTTR04: 400 BadRequest, "Vui lòng chọn nhân viên khác với người đang phụ trách."
    ///  - UTTR05: 400 BadRequest, "Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active)."
    ///  - UTTR06: 204 NoContent, chuyển từ New + Assigned -> InProgress + Technical
    ///  - UTTR07: 204 NoContent, chuyển khi đã InProgress + Technical (status & assignmentState giữ nguyên)
    /// </summary>
    public class TicketsController_TransferToTechTests
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
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(new Claim[]
                        {
                            // cho audit log, không dùng trong logic TransferToTech
                            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                        }, "TestAuth"))
                }
            };

            return controller;
        }

        private static User CreateUser(Guid id, string roleCode, string status = "Active")
        {
            var roles = new List<Role>
            {
                new Role
                {
                    RoleId = Guid.NewGuid().ToString(),
                    Code = roleCode,
                    Name = roleCode
                }
            };

            return new User
            {
                UserId = id,
                Email = $"{roleCode}@example.com",
                FullName = roleCode,
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

        // ===================== UTTR01 =====================
        // Ticket không tồn tại -> 404 NotFound, DB không đổi
        [Fact(DisplayName = "UTTR01 - Ticket not found -> 404 NotFound")]
        public async Task TransferToTech_TicketNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(TransferToTech_TicketNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = Guid.NewGuid()
            };

            var result = await controller.TransferToTech(Guid.NewGuid(), dto);

            Assert.IsType<NotFoundResult>(result);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTTR02 =====================
        // Ticket Closed/Completed -> 400 "Ticket đã khoá."
        [Fact(DisplayName = "UTTR02 - Closed ticket -> 400 BadRequest (Ticket đã khoá.)")]
        public async Task TransferToTech_ClosedTicket_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(TransferToTech_ClosedTicket_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateUser(customerId, "customer"));

            var currentAssigneeId = Guid.NewGuid();
            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(
                ticketId,
                customerId,
                status: "Closed",
                assignmentState: "Assigned",
                assigneeId: currentAssigneeId);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = Guid.NewGuid()
            };

            var result = await controller.TransferToTech(ticketId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Ticket đã khoá.", msg);

            Assert.Equal(currentAssigneeId, ticket.AssigneeId);
            Assert.Equal("Closed", ticket.Status);
            Assert.Equal("Assigned", ticket.AssignmentState);
            Assert.Null(ticket.UpdatedAt);
        }

        // ===================== UTTR03 =====================
        // AssignmentState = Unassigned -> 400 "Vui lòng gán trước khi chuyển hỗ trợ."
        [Fact(DisplayName = "UTTR03 - Unassigned ticket -> 400 BadRequest (Vui lòng gán trước khi chuyển hỗ trợ.)")]
        public async Task TransferToTech_UnassignedTicket_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(TransferToTech_UnassignedTicket_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateUser(customerId, "customer"));

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(
                ticketId,
                customerId,
                status: "New",
                assignmentState: "Unassigned",
                assigneeId: null);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = Guid.NewGuid()
            };

            var result = await controller.TransferToTech(ticketId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Vui lòng gán trước khi chuyển hỗ trợ.", msg);

            Assert.Null(ticket.AssigneeId);
            Assert.Equal("New", ticket.Status);
            Assert.Equal("Unassigned", ticket.AssignmentState);
            Assert.Null(ticket.UpdatedAt);
        }

        // ===================== UTTR04 =====================
        // New assignee trùng assignee hiện tại -> 400 "Vui lòng chọn nhân viên khác với người đang phụ trách."
        [Fact(DisplayName = "UTTR04 - New assignee equals current -> 400 BadRequest")]
        public async Task TransferToTech_SameAssignee_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(TransferToTech_SameAssignee_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateUser(customerId, "customer"));

            var currentAssigneeId = Guid.NewGuid();
            db.Users.Add(CreateUser(currentAssigneeId, "care-staff"));
            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(
                ticketId,
                customerId,
                status: "New",
                assignmentState: "Assigned",
                assigneeId: currentAssigneeId);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = currentAssigneeId
            };

            var result = await controller.TransferToTech(ticketId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Vui lòng chọn nhân viên khác với người đang phụ trách.", msg);

            Assert.Equal(currentAssigneeId, ticket.AssigneeId);
            Assert.Equal("New", ticket.Status);
            Assert.Equal("Assigned", ticket.AssignmentState);
            Assert.Null(ticket.UpdatedAt);
        }

        // ===================== UTTR05 =====================
        // New assignee không phải Active care staff -> 400 "Nhân viên không hợp lệ..."
        [Fact(DisplayName = "UTTR05 - Invalid new assignee (not care staff) -> 400 BadRequest")]
        public async Task TransferToTech_InvalidNewAssignee_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(TransferToTech_InvalidNewAssignee_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateUser(customerId, "customer"));

            var currentAssigneeId = Guid.NewGuid();
            db.Users.Add(CreateUser(currentAssigneeId, "care-staff"));

            var invalidAssigneeId = Guid.NewGuid();
            // user role KHÔNG chứa "care" => không hợp lệ
            db.Users.Add(CreateUser(invalidAssigneeId, "storage-staff"));
            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(
                ticketId,
                customerId,
                status: "New",
                assignmentState: "Assigned",
                assigneeId: currentAssigneeId);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = invalidAssigneeId
            };

            var result = await controller.TransferToTech(ticketId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active).", msg);

            Assert.Equal(currentAssigneeId, ticket.AssigneeId);
            Assert.Equal("New", ticket.Status);
            Assert.Equal("Assigned", ticket.AssignmentState);
            Assert.Null(ticket.UpdatedAt);
        }

        // ===================== UTTR06 =====================
        // Thành công: từ New + Assigned -> InProgress + Technical
        [Fact(DisplayName = "UTTR06 - Transfer from New & Assigned -> InProgress & Technical (204)")]
        public async Task TransferToTech_FromNewAssigned_Success()
        {
            var options = CreateInMemoryOptions(nameof(TransferToTech_FromNewAssigned_Success));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateUser(customerId, "customer"));

            var oldAssigneeId = Guid.NewGuid();
            db.Users.Add(CreateUser(oldAssigneeId, "care-staff"));

            var newAssigneeId = Guid.NewGuid();
            db.Users.Add(CreateUser(newAssigneeId, "care-staff")); // valid care staff

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(
                ticketId,
                customerId,
                status: "New",
                assignmentState: "Assigned",
                assigneeId: oldAssigneeId);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = newAssigneeId
            };

            var before = DateTime.UtcNow;

            var result = await controller.TransferToTech(ticketId, dto);

            var after = DateTime.UtcNow;

            Assert.IsType<NoContentResult>(result);

            Assert.Equal(newAssigneeId, ticket.AssigneeId);
            Assert.Equal("Technical", ticket.AssignmentState);
            Assert.Equal("InProgress", ticket.Status);
            Assert.NotNull(ticket.UpdatedAt);
            Assert.InRange(ticket.UpdatedAt!.Value, before, after);
        }

        // ===================== UTTR07 =====================
        // Thành công: ticket đã InProgress + Technical -> giữ nguyên status & assignmentState
        [Fact(DisplayName = "UTTR07 - Transfer when already Technical & InProgress -> reassign only (204)")]
        public async Task TransferToTech_FromInProgressTechnical_Success()
        {
            var options = CreateInMemoryOptions(nameof(TransferToTech_FromInProgressTechnical_Success));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateUser(customerId, "customer"));

            var oldAssigneeId = Guid.NewGuid();
            db.Users.Add(CreateUser(oldAssigneeId, "care-staff"));

            var newAssigneeId = Guid.NewGuid();
            db.Users.Add(CreateUser(newAssigneeId, "care-staff")); // valid care staff

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(
                ticketId,
                customerId,
                status: "InProgress",
                assignmentState: "Technical",
                assigneeId: oldAssigneeId);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var dto = new TicketsController.AssignTicketDto
            {
                AssigneeId = newAssigneeId
            };

            var before = DateTime.UtcNow;

            var result = await controller.TransferToTech(ticketId, dto);

            var after = DateTime.UtcNow;

            Assert.IsType<NoContentResult>(result);

            // Status & assignmentState giữ nguyên
            Assert.Equal("Technical", ticket.AssignmentState);
            Assert.Equal("InProgress", ticket.Status);

            // AssigneeId đổi sang newAssigneeId
            Assert.Equal(newAssigneeId, ticket.AssigneeId);

            Assert.NotNull(ticket.UpdatedAt);
            Assert.InRange(ticket.UpdatedAt!.Value, before, after);
        }
    }
}
