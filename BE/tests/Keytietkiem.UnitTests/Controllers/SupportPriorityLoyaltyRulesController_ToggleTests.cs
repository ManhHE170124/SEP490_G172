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
    /// Unit tests cho SupportPriorityLoyaltyRulesController.Toggle
    /// (ToggleSupportPriorityLoyaltyRuleStatus) – UT001..UT005.
    /// </summary>
    public class SupportPriorityLoyaltyRulesController_ToggleTests
    {
        private const string OrderingErrorMessage =
            "Không thể bật rule này do không đảm bảo thứ tự mức chi tiêu giữa các level. " +
            "Level cao hơn phải có tổng chi tiêu tối thiểu cao hơn level thấp hơn. " +
            "Vui lòng điều chỉnh lại giá trị hoặc tắt/bật các rule khác.";

        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
        }

        private static SupportPriorityLoyaltyRulesController CreateController(
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

            var controller = new SupportPriorityLoyaltyRulesController(
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

        // ===================== UT001 =====================
        // Rule không tồn tại => 404 NotFound, không audit log
        [Fact(DisplayName = "UT001 - Rule not found -> 404 NotFound")]
        public async Task Toggle_RuleNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_RuleNotFound_Returns404));

            var controller = CreateController(options, out var auditLoggerMock);

            var result = await controller.Toggle(ruleId: 999);

            Assert.IsType<NotFoundResult>(result);

            using var db = new KeytietkiemDbContext(options);
            Assert.Empty(db.SupportPriorityLoyaltyRules);

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
        // Inactive -> Active, ordering hợp lệ, có rule khác cùng PriorityLevel đang Active
        // => bật rule hiện tại, tắt rule cùng PriorityLevel, 200 OK { ruleId, IsActive = true }
        [Fact(DisplayName = "UT002 - Toggle inactive rule to active, valid ordering & deactivate same-level rules")]
        public async Task Toggle_InactiveToActive_ValidOrdering_DeactivatesSameLevel()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_InactiveToActive_ValidOrdering_DeactivatesSameLevel));

            int targetId;
            int sameLevelId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                // Lower level 1: 100k (active)
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });

                // Target level 2: 200k (inactive)
                var target = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 200_000m,
                    IsActive = false
                };
                seed.SupportPriorityLoyaltyRules.Add(target);

                // Same-level rule level 2: 250k (active) – sẽ bị tắt
                var sameLevel = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 250_000m,
                    IsActive = true
                };
                seed.SupportPriorityLoyaltyRules.Add(sameLevel);

                // Higher level 3: 400k (active)
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 3,
                    MinTotalSpend = 400_000m,
                    IsActive = true
                });

                seed.SaveChanges();
                targetId = target.RuleId;
                sameLevelId = sameLevel.RuleId;
            }

            var controller = CreateController(options, out var auditLoggerMock);

            var result = await controller.Toggle(targetId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = ok.Value!;
            var ruleIdProp = body.GetType().GetProperty("RuleId") ?? body.GetType().GetProperty("ruleId");
            var isActiveProp = body.GetType().GetProperty("IsActive") ?? body.GetType().GetProperty("isActive");
            Assert.NotNull(ruleIdProp);
            Assert.NotNull(isActiveProp);
            Assert.Equal(targetId, (int)ruleIdProp!.GetValue(body)!);
            Assert.True((bool)isActiveProp!.GetValue(body)!);

            using (var db = new KeytietkiemDbContext(options))
            {
                var rules = db.SupportPriorityLoyaltyRules.ToList();

                var target = Assert.Single(rules.Where(r => r.RuleId == targetId));
                Assert.True(target.IsActive);

                var sameLevel = Assert.Single(rules.Where(r => r.RuleId == sameLevelId));
                Assert.False(sameLevel.IsActive);

                // Các rule khác giữ nguyên IsActive = true
                var others = rules.Where(r => r.RuleId != targetId && r.RuleId != sameLevelId).ToList();
                Assert.All(others, r => Assert.True(r.IsActive));
            }

            auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Toggle",
                    "SupportPriorityLoyaltyRule",
                    It.Is<string>(id => id == targetId.ToString()),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Once);
        }

        // ===================== UT003 =====================
        // Active -> Inactive, không ảnh hưởng rule khác, 200 OK { IsActive = false }
        [Fact(DisplayName = "UT003 - Toggle active rule to inactive -> 200 OK, other rules unchanged")]
        public async Task Toggle_ActiveToInactive_Succeeds()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_ActiveToInactive_Succeeds));

            int targetId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                // Lower level
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });

                // Target
                var target = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 200_000m,
                    IsActive = true
                };
                seed.SupportPriorityLoyaltyRules.Add(target);

                // Higher level
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 3,
                    MinTotalSpend = 300_000m,
                    IsActive = true
                });

                seed.SaveChanges();
                targetId = target.RuleId;
            }

            var controller = CreateController(options, out var auditLoggerMock);

            var result = await controller.Toggle(targetId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = ok.Value!;
            var ruleIdProp = body.GetType().GetProperty("RuleId") ?? body.GetType().GetProperty("ruleId");
            var isActiveProp = body.GetType().GetProperty("IsActive") ?? body.GetType().GetProperty("isActive");
            Assert.Equal(targetId, (int)ruleIdProp!.GetValue(body)!);
            Assert.False((bool)isActiveProp!.GetValue(body)!);

            using (var db = new KeytietkiemDbContext(options))
            {
                var rules = db.SupportPriorityLoyaltyRules.ToList();

                var target = Assert.Single(rules.Where(r => r.RuleId == targetId));
                Assert.False(target.IsActive);

                var others = rules.Where(r => r.RuleId != targetId).ToList();
                Assert.All(others, r => Assert.True(r.IsActive));
            }

            auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Toggle",
                    "SupportPriorityLoyaltyRule",
                    It.Is<string>(id => id == targetId.ToString()),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Once);
        }

        // ===================== UT004 =====================
        // Inactive -> Active, conflict với lower-level active
        // (Ordering rule violated UP) => 400 BadRequest, không thay đổi DB
        [Fact(DisplayName = "UT004 - Toggle inactive rule, conflict with lower-level active (up) -> 400 BadRequest")]
        public async Task Toggle_InactiveToActive_ConflictWithLowerLevel_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_InactiveToActive_ConflictWithLowerLevel_Returns400));

            int targetId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                // Lower level 1 có MinTotalSpend >= target.MinTotalSpend => conflict
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 300_000m,
                    IsActive = true
                });

                var target = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 200_000m,
                    IsActive = false
                };
                seed.SupportPriorityLoyaltyRules.Add(target);

                seed.SaveChanges();
                targetId = target.RuleId;
            }

            var controller = CreateController(options, out var auditLoggerMock);

            var result = await controller.Toggle(targetId);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(OrderingErrorMessage, GetMessage(bad));

            using (var db = new KeytietkiemDbContext(options))
            {
                var rules = db.SupportPriorityLoyaltyRules.ToList();
                var target = Assert.Single(rules.Where(r => r.RuleId == targetId));
                Assert.False(target.IsActive); // không thay đổi

                var lower = Assert.Single(rules.Where(r => r.RuleId != targetId));
                Assert.True(lower.IsActive);
            }

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
        // Inactive -> Active, conflict với higher-level active
        // (Ordering rule violated DOWN) => 400 BadRequest, không thay đổi DB
        [Fact(DisplayName = "UT005 - Toggle inactive rule, conflict with higher-level active (down) -> 400 BadRequest")]
        public async Task Toggle_InactiveToActive_ConflictWithHigherLevel_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Toggle_InactiveToActive_ConflictWithHigherLevel_Returns400));

            int targetId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var target = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 200_000m,
                    IsActive = false
                };
                seed.SupportPriorityLoyaltyRules.Add(target);

                // Higher level 2 có MinTotalSpend <= target.MinTotalSpend => conflict
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 150_000m,
                    IsActive = true
                });

                seed.SaveChanges();
                targetId = target.RuleId;
            }

            var controller = CreateController(options, out var auditLoggerMock);

            var result = await controller.Toggle(targetId);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(OrderingErrorMessage, GetMessage(bad));

            using (var db = new KeytietkiemDbContext(options))
            {
                var rules = db.SupportPriorityLoyaltyRules.ToList();
                var target = Assert.Single(rules.Where(r => r.RuleId == targetId));
                Assert.False(target.IsActive);

                var higher = Assert.Single(rules.Where(r => r.RuleId != targetId));
                Assert.True(higher.IsActive);
            }

            auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }
    }
}
