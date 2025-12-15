using System;
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
    /// Unit test cho TicketsController.Close (CloseTicket)
    /// 5 TC khớp sheet:
    ///  - UTCL01: 404 NotFound – ticket không tồn tại
    ///  - UTCL02: 400 BadRequest "Ticket đã khoá." – status Closed/Completed
    ///  - UTCL03: 400 BadRequest "Chỉ đóng khi trạng thái Mới." – status khác New (InProgress)
    ///  - UTCL04: 204 NoContent – New & ResolvedAt == null
    ///  - UTCL05: 204 NoContent – New & ResolvedAt != null (giữ nguyên ResolvedAt)
    /// </summary>
    public class TicketsController_CloseTicketTests
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

        private static User CreateUser(Guid id)
        {
            return new User
            {
                UserId = id,
                Email = "user@example.com",
                FullName = "User",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                SupportPriorityLevel = 1,
                TotalProductSpend = 0,
                IsTemp = false
            };
        }

        private static Ticket CreateTicket(
            Guid ticketId,
            Guid customerId,
            string status,
            DateTime? resolvedAt = null)
        {
            return new Ticket
            {
                TicketId = ticketId,
                UserId = customerId,
                Subject = "Test ticket",
                Description = "Desc",
                Status = status,
                TicketCode = "TCK-0001",
                Severity = "Medium",
                SlaStatus = "OK",
                AssignmentState = "Assigned",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                UpdatedAt = null,
                PriorityLevel = 1,
                ResolvedAt = resolvedAt
            };
        }

        #endregion

        // ===================== UTCL01 =====================
        // Ticket không tồn tại -> 404 NotFound
        [Fact(DisplayName = "UTCL01 - Ticket not found -> 404 NotFound")]
        public async Task Close_TicketNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Close_TicketNotFound_Returns404));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db);

            var result = await controller.Close(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
            Assert.Empty(db.Tickets);
        }

        // ===================== UTCL02 =====================
        // Status = Closed/Completed -> 400 "Ticket đã khoá."
        [Theory(DisplayName = "UTCL02 - Closed/Completed ticket -> 400 BadRequest (Ticket đã khoá.)")]
        [InlineData("Closed")]
        [InlineData("Completed")]
        public async Task Close_ClosedOrCompleted_Returns400(string status)
        {
            var options = CreateInMemoryOptions(nameof(Close_ClosedOrCompleted_Returns400) + status);
            using var db = new KeytietkiemDbContext(options);

            var userId = Guid.NewGuid();
            db.Users.Add(CreateUser(userId));

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(ticketId, userId, status: status);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var result = await controller.Close(ticketId);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Ticket đã khoá.", msg);

            // Không thay đổi
            Assert.Equal(status, ticket.Status);
            Assert.Null(ticket.UpdatedAt);
            Assert.Equal("OK", ticket.SlaStatus);
        }

        // ===================== UTCL03 =====================
        // Status khác New (InProgress) -> 400 "Chỉ đóng khi trạng thái Mới."
        [Fact(DisplayName = "UTCL03 - Status = InProgress (not New) -> 400 BadRequest (Chỉ đóng khi trạng thái Mới.)")]
        public async Task Close_StatusNotNew_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Close_StatusNotNew_Returns400));
            using var db = new KeytietkiemDbContext(options);

            var userId = Guid.NewGuid();
            db.Users.Add(CreateUser(userId));

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(ticketId, userId, status: "InProgress");
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var result = await controller.Close(ticketId);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString();
            Assert.Equal("Chỉ đóng khi trạng thái Mới.", msg);

            Assert.Equal("InProgress", ticket.Status);
            Assert.Null(ticket.UpdatedAt);
            Assert.Null(ticket.ResolvedAt);
        }

        // ===================== UTCL04 =====================
        // New + ResolvedAt == null -> 204, Closed + ResolvedAt/UpdatedAt ~ now
        [Fact(DisplayName = "UTCL04 - New & ResolvedAt null -> Closed & set ResolvedAt/UpdatedAt (204)")]
        public async Task Close_New_NoResolvedAt_SetsResolvedAndClosed()
        {
            var options = CreateInMemoryOptions(nameof(Close_New_NoResolvedAt_SetsResolvedAndClosed));
            using var db = new KeytietkiemDbContext(options);

            var userId = Guid.NewGuid();
            db.Users.Add(CreateUser(userId));

            var ticketId = Guid.NewGuid();
            var ticket = CreateTicket(ticketId, userId, status: "New", resolvedAt: null);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var before = DateTime.UtcNow;

            var result = await controller.Close(ticketId);

            var after = DateTime.UtcNow;

            Assert.IsType<NoContentResult>(result);

            Assert.Equal("Closed", ticket.Status);
            Assert.NotNull(ticket.ResolvedAt);
            Assert.InRange(ticket.ResolvedAt!.Value, before, after);
            Assert.NotNull(ticket.UpdatedAt);
            Assert.InRange(ticket.UpdatedAt!.Value, before, after);
            Assert.False(string.IsNullOrWhiteSpace(ticket.SlaStatus));
        }

        // ===================== UTCL05 =====================
        // New + ResolvedAt đã có value -> 204, Closed nhưng ResolvedAt giữ nguyên
        [Fact(DisplayName = "UTCL05 - New & ResolvedAt has value -> keep ResolvedAt, set Closed (204)")]
        public async Task Close_New_ExistingResolvedAt_KeepsResolvedAt()
        {
            var options = CreateInMemoryOptions(nameof(Close_New_ExistingResolvedAt_KeepsResolvedAt));
            using var db = new KeytietkiemDbContext(options);

            var userId = Guid.NewGuid();
            db.Users.Add(CreateUser(userId));

            var ticketId = Guid.NewGuid();
            var existingResolved = DateTime.UtcNow.AddHours(-2);

            var ticket = CreateTicket(ticketId, userId, status: "New", resolvedAt: existingResolved);
            db.Tickets.Add(ticket);
            db.SaveChanges();

            var controller = CreateController(db);

            var before = DateTime.UtcNow;

            var result = await controller.Close(ticketId);

            var after = DateTime.UtcNow;

            Assert.IsType<NoContentResult>(result);

            Assert.Equal("Closed", ticket.Status);
            Assert.Equal(existingResolved, ticket.ResolvedAt);
            Assert.NotNull(ticket.UpdatedAt);
            Assert.InRange(ticket.UpdatedAt!.Value, before, after);
        }
    }
}
