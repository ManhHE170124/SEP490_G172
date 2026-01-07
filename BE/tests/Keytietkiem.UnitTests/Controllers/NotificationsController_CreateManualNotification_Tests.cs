// File: Tests/Notifications/NotificationsController_CreateManualNotification_Tests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs;
using Keytietkiem.Hubs;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Notifications
{
    public class NotificationsController_CreateManualNotification_Tests
    {
        #region Helpers

        private static NotificationsController CreateControllerWithMocks(
            DbContextOptions<KeytietkiemDbContext> options,
            out KeytietkiemDbContext db,
            out Mock<ILogger<NotificationsController>> loggerMock,
            out Mock<IHubContext<NotificationHub>> hubContextMock,
            Mock<IHubClients>? hubClientsMock = null,
            Mock<IClientProxy>? clientProxyMock = null)
        {
            db = new KeytietkiemDbContext(options);

            loggerMock = new Mock<ILogger<NotificationsController>>();
            hubContextMock = new Mock<IHubContext<NotificationHub>>();

            if (hubClientsMock != null && clientProxyMock != null)
            {
                hubClientsMock
                    .Setup(c => c.Group(It.IsAny<string>()))
                    .Returns(clientProxyMock.Object);

                hubContextMock
                    .SetupGet(h => h.Clients)
                    .Returns(hubClientsMock.Object);
            }
            else
            {
                // Các test không cần verify SignalR -> mock đơn giản
                var dummyClients = new Mock<IHubClients>().Object;
                hubContextMock
                    .SetupGet(h => h.Clients)
                    .Returns(dummyClients);
            }

            return new NotificationsController(db, loggerMock.Object, hubContextMock.Object);
        }

        private static void AttachHttpContextWithUser(
            NotificationsController controller,
            ClaimsPrincipal user)
        {
            var httpContext = new DefaultHttpContext
            {
                User = user
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #endregion

        // =========================================================
        // TC1 – ModelState invalid -> ValidationProblem (400)
        // =========================================================
        [Fact]
        public async Task CreateManualNotification_InvalidModel_ReturnsValidationProblem()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(CreateManualNotification_InvalidModel_ReturnsValidationProblem))
                .Options;

            var controller = CreateControllerWithMocks(
                options,
                out var db,
                out _,
                out _);

            // Chủ động làm ModelState invalid (giả lập validate DataAnnotations fail)
            controller.ModelState.AddModelError("Title", "Required");

            var dto = new CreateNotificationDto
            {
                Title = "", // sẽ không quan trọng vì ModelState đã invalid
                Message = "Test",
                TargetUserIds = new List<Guid> { Guid.NewGuid() }
            };

            var result = await controller.CreateManualNotification(dto);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

            Assert.Empty(db.Notifications);
        }

        // =========================================================
        // TC2 – Thiếu TargetUserIds -> ValidationProblem (400)
        // =========================================================
        [Fact]
        public async Task CreateManualNotification_NoTargetUserIds_ReturnsValidationProblem()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(CreateManualNotification_NoTargetUserIds_ReturnsValidationProblem))
                .Options;

            var controller = CreateControllerWithMocks(
                options,
                out var db,
                out _,
                out _);

            var dto = new CreateNotificationDto
            {
                Title = "Hello",
                Message = "World",
                TargetUserIds = new List<Guid>() // rỗng
            };

            var result = await controller.CreateManualNotification(dto);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

            Assert.True(controller.ModelState.ContainsKey(nameof(CreateNotificationDto.TargetUserIds)));
            Assert.Empty(db.Notifications);
        }

        // =========================================================
        // TC3 – Không xác định được current user -> Unauthorized (401)
        // =========================================================
        [Fact]
        public async Task CreateManualNotification_NoCurrentUser_ReturnsUnauthorized()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(CreateManualNotification_NoCurrentUser_ReturnsUnauthorized))
                .Options;

            var controller = CreateControllerWithMocks(
                options,
                out var db,
                out _,
                out _);

            // User không có NameIdentifier / sub -> GetCurrentUserId() sẽ throw
            var user = new ClaimsPrincipal(new ClaimsIdentity());
            AttachHttpContextWithUser(controller, user);

            var dto = new CreateNotificationDto
            {
                Title = "Hello",
                Message = "World",
                TargetUserIds = new List<Guid> { Guid.NewGuid() }
            };

            var result = await controller.CreateManualNotification(dto);

            Assert.IsType<UnauthorizedResult>(result);
            Assert.Empty(db.Notifications);
        }

        // =========================================================
        // TC4 – TargetUserIds có giá trị nhưng không user nào tồn tại -> ValidationProblem (400)
        // =========================================================
        [Fact]
        public async Task CreateManualNotification_NoValidUsers_ReturnsValidationProblem()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(CreateManualNotification_NoValidUsers_ReturnsValidationProblem))
                .Options;

            var controller = CreateControllerWithMocks(
                options,
                out var db,
                out _,
                out _);

            var adminId = Guid.NewGuid();
            var user = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                        new Claim(ClaimTypes.Role, "Admin")
                    },
                    "TestAuth"));

            AttachHttpContextWithUser(controller, user);

            var dto = new CreateNotificationDto
            {
                Title = "Hello",
                Message = "World",
                TargetUserIds = new List<Guid> { Guid.NewGuid() } // không tồn tại trong DB
            };

            var result = await controller.CreateManualNotification(dto);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

            Assert.True(controller.ModelState.ContainsKey(nameof(CreateNotificationDto.TargetUserIds)));
            Assert.Empty(db.Notifications);
        }

        // =========================================================
        // TC5 – Happy path: user & role hợp lệ, duplicate user, trim dữ liệu,
        //        tạo Notification + NotificationUsers + NotificationTargetRoles và push SignalR.
        // =========================================================
        [Fact]
        public async Task CreateManualNotification_Success_CreatesNotification_AndPushesToAllUsers()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(CreateManualNotification_Success_CreatesNotification_AndPushesToAllUsers))
                .Options;

            // Seed 2 user & 2 role (1 role hợp lệ, 1 role không tồn tại trong DB)
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();

            using (var seedCtx = new KeytietkiemDbContext(options))
            {
                seedCtx.Users.Add(new User
                {
                    UserId = user1Id,
                    Email = "user1@test.com",
                    FullName = "User One",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                });

                seedCtx.Users.Add(new User
                {
                    UserId = user2Id,
                    Email = "user2@test.com",
                    FullName = "User Two",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                });

                seedCtx.Roles.Add(new Role
                {
                    RoleId = "Admin",
                    Name = "Admin"
                });

                seedCtx.Roles.Add(new Role
                {
                    RoleId = "Staff",
                    Name = "Staff"
                });

                seedCtx.SaveChanges();
            }

            var hubClientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();

            var controller = CreateControllerWithMocks(
                options,
                out var db,
                out var loggerMock,
                out var hubContextMock,
                hubClientsMock,
                clientProxyMock);

            // Mock SendCoreAsync để giả lập SignalR
            clientProxyMock
                .Setup(c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var adminId = Guid.NewGuid();
            var userPrincipal = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                        new Claim(ClaimTypes.Role, "Admin")
                    },
                    "TestAuth"));

            AttachHttpContextWithUser(controller, userPrincipal);

            var dto = new CreateNotificationDto
            {
                Title = "  Hello world  ",
                Message = "  This is a test  ",
                Severity = 2,
                IsGlobal = true,
                RelatedEntityType = "  Order ",
                RelatedEntityId = "  123  ",
                RelatedUrl = "  https://test.local/order/123  ",

                // 1 role hợp lệ (Admin), 1 role không tồn tại (Ignored)
                TargetRoleIds = new List<string> { "Admin", "NonExistingRole" },

                // user2 bị lặp để test Distinct()
                TargetUserIds = new List<Guid> { user1Id, user2Id, user2Id }
            };

            var result = await controller.CreateManualNotification(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(NotificationsController.GetNotificationDetail), created.ActionName);
            Assert.NotNull(created.RouteValues);
            var idFromRoute = Assert.IsType<int>(created.RouteValues["id"]);

            // Lấy lại Notification từ DB để verify
            var notification = await db.Notifications
                .Include(n => n.NotificationUsers)
                .Include(n => n.NotificationTargetRoles)
                .SingleAsync(n => n.Id == idFromRoute);

            // Trimming + mapping field
            Assert.Equal("Hello world", notification.Title);
            Assert.Equal("This is a test", notification.Message);
            Assert.Equal((byte)2, notification.Severity);
            Assert.True(notification.IsGlobal);
            Assert.Equal("Order", notification.RelatedEntityType);
            Assert.Equal("123", notification.RelatedEntityId);
            Assert.Equal("https://test.local/order/123", notification.RelatedUrl);
            Assert.Equal(adminId, notification.CreatedByUserId);

            // Distinct TargetUserIds -> chỉ còn 2 NotificationUser
            Assert.Equal(2, notification.NotificationUsers.Count);
            Assert.All(notification.NotificationUsers, nu =>
            {
                Assert.False(nu.IsRead);
                Assert.Null(nu.ReadAtUtc);
            });

            // Chỉ các role tồn tại mới được map
            Assert.Single(notification.NotificationTargetRoles);
            Assert.Equal("Admin", notification.NotificationTargetRoles.Single().RoleId);

            // Verify SignalR được gọi đúng số lần (2 user)
            clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object?[]>(args =>
                        args.Length == 1 && args[0] is NotificationUserListItemDto),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }
    }
}
