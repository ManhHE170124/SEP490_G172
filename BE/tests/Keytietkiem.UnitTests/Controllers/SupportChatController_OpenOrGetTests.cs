using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Support;
using Keytietkiem.Hubs;
using Keytietkiem.Models;
using Keytietkiem.Infrastructure;
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
    /// Unit test cho SupportChatController.OpenOrGet (5 test case như sheet).
    /// UT001: 401, không user.
    /// UT002: Không có session nào, priority <=0, không InitialMessage -> tạo session mới, không message.
    /// UT003: Chỉ có session Closed, priority >=3, InitialMessage dài -> tạo session mới + message, có LastClosed*.
    /// UT004: Có cả Closed + Open, priority 1–3, InitialMessage whitespace -> reuse session Open, không message.
    /// UT005: Chỉ có session Open, priority 1–3, InitialMessage ngắn -> reuse session Open + tạo message.
    /// </summary>
    public class SupportChatController_OpenOrGetTests
    {
        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
        }

        private static IHubContext<SupportChatHub> CreateHubContextStub()
        {
            var mockClientProxy = new Mock<IClientProxy>();
            mockClientProxy
                .Setup(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var mockClients = new Mock<IHubClients>();
            mockClients
                .Setup(c => c.Group(It.IsAny<string>()))
                .Returns(mockClientProxy.Object);

            var mockHub = new Mock<IHubContext<SupportChatHub>>();
            mockHub.SetupGet(h => h.Clients).Returns(mockClients.Object);
            mockHub.SetupGet(h => h.Groups).Returns(Mock.Of<IGroupManager>());

            return mockHub.Object;
        }

        private static SupportChatController CreateController(
            KeytietkiemDbContext db,
            Guid? currentUserId)
        {
            var hub = CreateHubContextStub();
            IAuditLogger auditLogger = null!; // không dùng trong OpenOrGet

            var controller = new SupportChatController(db, hub, auditLogger);

            var httpContext = new DefaultHttpContext();
            if (currentUserId.HasValue)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, currentUserId.Value.ToString())
                };
                httpContext.User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, authenticationType: "TestAuth"));
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        private static User CreateCustomer(Guid id, int supportPriorityLevel)
        {
            return new User
            {
                UserId = id,
                Email = "customer@example.com",
                FullName = "Test Customer",
                Status = "Active",
                SupportPriorityLevel = supportPriorityLevel
            };
        }

        #endregion

        // ====================== UT001 ======================
        // No valid authenticated user -> 401 + không DB change
        [Fact(DisplayName = "UT001 - No authenticated user -> 401 & no DB change")]
        public async Task OpenOrGet_NoAuthenticatedUser_Returns401AndNoDbChange()
        {
            var options = CreateInMemoryOptions(nameof(OpenOrGet_NoAuthenticatedUser_Returns401AndNoDbChange));
            using var db = new KeytietkiemDbContext(options);

            var controller = CreateController(db, currentUserId: null);

            var actionResult = await controller.OpenOrGet(new OpenSupportChatDto
            {
                InitialMessage = "Hello"
            });

            var unauthorized = Assert.IsType<UnauthorizedResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);

            Assert.Empty(db.SupportChatSessions);
            Assert.Empty(db.SupportChatMessages);
        }

        // ====================== UT002 ======================
        // No existing session, priority <=0 -> normalize 1,
        // InitialMessage = null -> tạo session mới, không tạo message
        [Fact(DisplayName = "UT002 - No sessions, priority<=0, no message -> new Waiting session, no chat message")]
        public async Task OpenOrGet_NoSessions_PriorityBelowOne_NoInitialMessage_CreatesNewSession()
        {
            var options = CreateInMemoryOptions(nameof(OpenOrGet_NoSessions_PriorityBelowOne_NoInitialMessage_CreatesNewSession));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateCustomer(customerId, supportPriorityLevel: 0)); // <=0
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var actionResult = await controller.OpenOrGet(body: null);

            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var dto = Assert.IsType<OpenSupportChatResultDto>(ok.Value);

            // Kết quả dto
            Assert.True(dto.IsNew);
            Assert.False(dto.HasPreviousClosedSession);
            Assert.Null(dto.LastClosedSessionId);
            Assert.Equal(customerId, dto.CustomerId);
            Assert.Equal(1, dto.PriorityLevel); // normalized
            Assert.Equal("Waiting", dto.Status);
            Assert.Null(dto.LastMessageAt);
            Assert.True(string.IsNullOrEmpty(dto.LastMessagePreview));

            // DB state
            var session = Assert.Single(db.SupportChatSessions);
            Assert.Equal(customerId, session.CustomerId);
            Assert.Equal("Waiting", session.Status);
            Assert.Equal(1, session.PriorityLevel);
            Assert.Empty(db.SupportChatMessages); // không tạo message
        }

        // ====================== UT003 ======================
        // Chỉ có session Closed, priority >=3 -> normalize 3,
        // InitialMessage dài >255 -> tạo session mới + 1 message,
        // HasPreviousClosedSession = true, LastClosed* set từ session cũ
        [Fact(DisplayName = "UT003 - Only closed sessions, priority>=3, long initial message -> new session + message + last closed info")]
        public async Task OpenOrGet_ClosedSessionsOnly_PriorityHigh_LongInitialMessage_NewSessionAndMessageWithLastClosed()
        {
            var options = CreateInMemoryOptions(nameof(OpenOrGet_ClosedSessionsOnly_PriorityHigh_LongInitialMessage_NewSessionAndMessageWithLastClosed));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateCustomer(customerId, supportPriorityLevel: 5)); // >=3
            var now = DateTime.UtcNow;

            var closedSession = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Status = "Closed",
                PriorityLevel = 2,
                StartedAt = now.AddMinutes(-30),
                ClosedAt = now.AddMinutes(-1),
                LastMessageAt = now.AddMinutes(-1),
                LastMessagePreview = "old preview"
            };
            db.SupportChatSessions.Add(closedSession);
            db.SaveChanges();

            var controller = CreateController(db, customerId);
            var longMsg = new string('x', 300);

            var actionResult = await controller.OpenOrGet(new OpenSupportChatDto
            {
                InitialMessage = longMsg
            });

            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var dto = Assert.IsType<OpenSupportChatResultDto>(ok.Value);

            // dto
            Assert.True(dto.IsNew);
            Assert.True(dto.HasPreviousClosedSession);
            Assert.Equal(closedSession.ChatSessionId, dto.LastClosedSessionId);
            Assert.NotNull(dto.LastClosedAt);
            Assert.Equal(3, dto.PriorityLevel); // normalized >=3 -> 3
            Assert.Equal("Waiting", dto.Status);
            Assert.NotNull(dto.LastMessageAt);
            Assert.NotNull(dto.LastMessagePreview);
            Assert.Equal(255, dto.LastMessagePreview!.Length); // truncated

            // DB state: 2 sessions (1 closed + 1 mới), 1 message mới
            Assert.Equal(2, db.SupportChatSessions.Count());
            Assert.Equal(1, db.SupportChatMessages.Count());

            var newSession = db.SupportChatSessions
                .Single(s => s.ChatSessionId == dto.ChatSessionId);
            Assert.Equal(customerId, newSession.CustomerId);
            Assert.Equal("Waiting", newSession.Status);
            Assert.Equal(3, newSession.PriorityLevel);
        }

        // ====================== UT004 ======================
        // Có cả Closed + Open, priority 1–3,
        // InitialMessage chỉ whitespace -> treated as empty,
        // Reuse session Open, không tạo message
        [Fact(DisplayName = "UT004 - Closed+Open sessions, whitespace initial message -> reuse open session, no new message")]
        public async Task OpenOrGet_BothClosedAndOpenSessions_WhitespaceInitialMessage_ReusesOpenSession_NoMessage()
        {
            var options = CreateInMemoryOptions(nameof(OpenOrGet_BothClosedAndOpenSessions_WhitespaceInitialMessage_ReusesOpenSession_NoMessage));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateCustomer(customerId, supportPriorityLevel: 2)); // 1–3

            var now = DateTime.UtcNow;

            var closedSession = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Status = "Closed",
                PriorityLevel = 2,
                StartedAt = now.AddMinutes(-60),
                ClosedAt = now.AddMinutes(-30)
            };

            var openSession = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Status = "Active",
                PriorityLevel = 2,
                StartedAt = now.AddMinutes(-10),
                LastMessageAt = now.AddMinutes(-5),
                LastMessagePreview = "old"
            };

            db.SupportChatSessions.AddRange(closedSession, openSession);
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var actionResult = await controller.OpenOrGet(new OpenSupportChatDto
            {
                InitialMessage = "   " // whitespace only
            });

            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var dto = Assert.IsType<OpenSupportChatResultDto>(ok.Value);

            // dto: dùng lại open session
            Assert.False(dto.IsNew);
            Assert.False(dto.HasPreviousClosedSession); // chỉ set khi IsNew = true
            Assert.Equal(openSession.ChatSessionId, dto.ChatSessionId);
            Assert.Equal("Active", dto.Status);

            // DB: không thêm session, không thêm message
            Assert.Equal(2, db.SupportChatSessions.Count());
            Assert.Empty(db.SupportChatMessages);
        }

        // ====================== UT005 ======================
        // Chỉ có session Open, priority 1–3,
        // InitialMessage ngắn (<=255) -> reuse session + tạo 1 message
        [Fact(DisplayName = "UT005 - Open sessions only, short initial message -> reuse session + new message")]
        public async Task OpenOrGet_OpenSessionsOnly_ShortInitialMessage_ReusesSession_AddsMessage()
        {
            var options = CreateInMemoryOptions(nameof(OpenOrGet_OpenSessionsOnly_ShortInitialMessage_ReusesSession_AddsMessage));
            using var db = new KeytietkiemDbContext(options);

            var customerId = Guid.NewGuid();
            db.Users.Add(CreateCustomer(customerId, supportPriorityLevel: 2)); // 1–3

            var now = DateTime.UtcNow;

            var openSession = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = customerId,
                Status = "Waiting",
                PriorityLevel = 2,
                StartedAt = now.AddMinutes(-5)
            };

            db.SupportChatSessions.Add(openSession);
            db.SaveChanges();

            var controller = CreateController(db, customerId);

            var actionResult = await controller.OpenOrGet(new OpenSupportChatDto
            {
                InitialMessage = "Hello support"
            });

            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var dto = Assert.IsType<OpenSupportChatResultDto>(ok.Value);

            // dto: reuse session, có message mới
            Assert.False(dto.IsNew);
            Assert.False(dto.HasPreviousClosedSession);
            Assert.Equal(openSession.ChatSessionId, dto.ChatSessionId);
            Assert.NotNull(dto.LastMessageAt);
            Assert.Equal("Hello support", dto.LastMessagePreview);

            // DB: chỉ 1 session, nhưng có 1 message
            Assert.Single(db.SupportChatSessions);
            Assert.Equal(1, db.SupportChatMessages.Count());

            var session = db.SupportChatSessions.Single();
            Assert.Equal("Waiting", session.Status); // vẫn Waiting (customer gửi)
            Assert.Equal("Hello support", session.LastMessagePreview);
        }
    }
}
