using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho SupportPlansAdminController.Toggle (ToggleSupportPlanStatus)
    /// UT001–UT005 như bảng decision table.
    /// </summary>
    public class SupportPlansAdminController_ToggleTests
    {
        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        private static SupportPlansAdminController CreateController(
            DbContextOptions<KeytietkiemDbContext> options,
            out Mock<IAuditLogger> auditLoggerMock)
        {
            var factoryMock = new Mock<IDbContextFactory<KeytietkiemDbContext>>();

            factoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new KeytietkiemDbContext(options));

            factoryMock
                .Setup(f => f.CreateDbContext())
                .Returns(() => new KeytietkiemDbContext(options));

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

            var controller = new SupportPlansAdminController(
                factoryMock.Object,
                auditLoggerMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            return controller;
        }

        private static string? GetMessage(ObjectResult result)
        {
            return result.Value?
                .GetType()
                .GetProperty("message")?
                .GetValue(result.Value)?
                .ToString();
        }

        #endregion

        // =============== UT001 =================
        // Plan không tồn tại -> 404 NotFound, không log, không thay đổi DB
        [Fact(DisplayName = "UT001 - Plan not found -> 404 NotFound")]
        public async Task Toggle_NotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_NotFound_Returns404));
            var controller = CreateController(options, out var auditMock);

            var result = await controller.Toggle(999);

            Assert.IsType<NotFoundResult>(result);

            using var db = new KeytietkiemDbContext(options);
            Assert.Empty(db.SupportPlans);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // =============== UT002 =================
        // Plan đang active -> toggle -> inactive, các plan khác giữ nguyên
        [Fact(DisplayName = "UT002 - Plan active before toggle -> 200 OK, IsActive = false")]
        public async Task Toggle_ActiveToInactive_Succeeds()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_ActiveToInactive_Succeeds));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var target = new SupportPlan
                {
                    Name = "Priority",
                    Description = "desc",
                    PriorityLevel = 1,
                    Price = 150_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                var other = new SupportPlan
                {
                    Name = "Other",
                    Description = "other",
                    PriorityLevel = 1,
                    Price = 200_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };

                seed.SupportPlans.AddRange(target, other);
                seed.SaveChanges();
                targetId = target.SupportPlanId;
            }

            var controller = CreateController(options, out var auditMock);

            var result = await controller.Toggle(targetId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value!;
            var type = value.GetType();
            int returnedId = (int)type.GetProperty("SupportPlanId")!.GetValue(value)!;
            bool isActive = (bool)type.GetProperty("IsActive")!.GetValue(value)!;

            Assert.Equal(targetId, returnedId);
            Assert.False(isActive);

            using var db = new KeytietkiemDbContext(options);
            var targetAfter = db.SupportPlans.Single(p => p.SupportPlanId == targetId);
            var otherAfter = db.SupportPlans.Single(p => p.SupportPlanId != targetId);

            Assert.False(targetAfter.IsActive);
            Assert.True(otherAfter.IsActive); // không bị ảnh hưởng

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Toggle",
                    "SupportPlan",
                    targetId.ToString(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Once);
        }

        // =============== UT003 =================
        // Plan đang inactive, không có plan active khác level -> bật lên thành active
        [Fact(DisplayName = "UT003 - Plan inactive, no other active plans -> 200 OK, IsActive = true")]
        public async Task Toggle_Inactive_NoOtherActive_Succeeds()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_Inactive_NoOtherActive_Succeeds));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var target = new SupportPlan
                {
                    Name = "Standard",
                    Description = "desc",
                    PriorityLevel = 0,
                    Price = 100_000m,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };
                seed.SupportPlans.Add(target);
                seed.SaveChanges();
                targetId = target.SupportPlanId;
            }

            var controller = CreateController(options, out var auditMock);

            var result = await controller.Toggle(targetId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value!;
            var type = value.GetType();
            int returnedId = (int)type.GetProperty("SupportPlanId")!.GetValue(value)!;
            bool isActive = (bool)type.GetProperty("IsActive")!.GetValue(value)!;

            Assert.Equal(targetId, returnedId);
            Assert.True(isActive);

            using var db = new KeytietkiemDbContext(options);
            var targetAfter = db.SupportPlans.Single(p => p.SupportPlanId == targetId);
            Assert.True(targetAfter.IsActive);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Toggle",
                    "SupportPlan",
                    targetId.ToString(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Once);
        }

        // =============== UT004 =================
        // Plan inactive, bật lên nhưng vi phạm rule giá với plan lower-level đang active -> 400
        [Fact(DisplayName = "UT004 - Price rule violated when enabling -> 400 BadRequest")]
        public async Task Toggle_Inactive_ViolatePriceRule_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_Inactive_ViolatePriceRule_Returns400));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var lowerActive = new SupportPlan
                {
                    Name = "Standard",
                    Description = "L0",
                    PriorityLevel = 0,
                    Price = 100_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                var target = new SupportPlan
                {
                    Name = "Priority",
                    Description = "L1",
                    PriorityLevel = 1,
                    Price = 90_000m, // PriorityLevel cao hơn nhưng giá THẤP hơn -> vi phạm
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };

                seed.SupportPlans.AddRange(lowerActive, target);
                seed.SaveChanges();
                targetId = target.SupportPlanId;
            }

            var controller = CreateController(options, out var auditMock);

            var result = await controller.Toggle(targetId);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = GetMessage(bad);
            Assert.NotNull(msg);
            Assert.Contains("PriorityLevel cao hơn", msg); // đúng nhánh vi phạm lower-level

            using var db = new KeytietkiemDbContext(options);
            var targetAfter = db.SupportPlans.Single(p => p.SupportPlanId == targetId);
            var lowerAfter = db.SupportPlans.Single(p => p.SupportPlanId != targetId);

            Assert.False(targetAfter.IsActive); // không bật được
            Assert.True(lowerAfter.IsActive);   // plan khác giữ nguyên

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Toggle",
                    "SupportPlan",
                    targetId.ToString(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // =============== UT005 =================
        // Plan inactive, rule giá OK, có plan cùng PriorityLevel đang active:
        //  -> bật target, tắt các plan cùng level, các level khác giữ nguyên
        [Fact(DisplayName = "UT005 - Enable valid plan -> 200 OK, IsActive = true, deactivate same-level plans")]
        public async Task Toggle_Inactive_Valid_DeactivatesSameLevel()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_Inactive_Valid_DeactivatesSameLevel));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                // lower level active 100k
                seed.SupportPlans.Add(new SupportPlan
                {
                    Name = "Standard",
                    Description = "L0",
                    PriorityLevel = 0,
                    Price = 100_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-4)
                });

                // cùng level L1 đang active, sẽ bị tắt
                seed.SupportPlans.Add(new SupportPlan
                {
                    Name = "Priority-Old",
                    Description = "old L1",
                    PriorityLevel = 1,
                    Price = 150_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                });

                var target = new SupportPlan
                {
                    Name = "Priority-New",
                    Description = "new L1",
                    PriorityLevel = 1,
                    Price = 200_000m,  // > 100k của level 0 => thỏa rule giá
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                seed.SupportPlans.Add(target);

                seed.SaveChanges();
                targetId = target.SupportPlanId;
            }

            var controller = CreateController(options, out var auditMock);

            var result = await controller.Toggle(targetId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value!;
            var type = value.GetType();
            int returnedId = (int)type.GetProperty("SupportPlanId")!.GetValue(value)!;
            bool isActive = (bool)type.GetProperty("IsActive")!.GetValue(value)!;

            Assert.Equal(targetId, returnedId);
            Assert.True(isActive);

            using var db = new KeytietkiemDbContext(options);
            var standard = db.SupportPlans.Single(p => p.PriorityLevel == 0);
            var oldL1 = db.SupportPlans.Single(p => p.Name == "Priority-Old");
            var newL1 = db.SupportPlans.Single(p => p.SupportPlanId == targetId);

            Assert.True(standard.IsActive);   // level khác giữ nguyên
            Assert.False(oldL1.IsActive);     // cùng level bị tắt
            Assert.True(newL1.IsActive);      // target được bật

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Toggle",
                    "SupportPlan",
                    targetId.ToString(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Once);
        }
    }
}
