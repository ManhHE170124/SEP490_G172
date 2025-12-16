using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Support;
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
    /// Unit tests cho SupportPriorityLoyaltyRulesController.Update
    /// (UpdateSupportPriorityLoyaltyRule) – UT001–UT008 theo decision table.
    /// </summary>
    public class SupportPriorityLoyaltyRulesController_UpdateTests
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

        // ================= UT001 =================
        // Rule không tồn tại -> 404 NotFound, không cập nhật gì.
        [Fact(DisplayName = "UT001 - Rule not found -> 404 NotFound")]
        public async Task Update_RuleNotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Update_RuleNotFound_Returns404));
            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleUpdateDto
            {
                PriorityLevel = 1,
                MinTotalSpend = 100_000m,
                IsActive = true
            };

            var result = await controller.Update(999, dto);

            Assert.IsType<NotFoundResult>(result);

            using var db = new KeytietkiemDbContext(options);
            Assert.Empty(db.SupportPriorityLoyaltyRules);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ================= UT002 =================
        // PriorityLevel <= 0 -> 400 BadRequest, không đổi rule.
        [Fact(DisplayName = "UT002 - PriorityLevel <= 0 -> 400 BadRequest")]
        public async Task Update_PriorityLevelLessOrEqualZero_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_PriorityLevelLessOrEqualZero_Returns400));

            int ruleId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                };
                seed.SupportPriorityLoyaltyRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.RuleId;
            }

            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleUpdateDto
            {
                PriorityLevel = 0,
                MinTotalSpend = 200_000m,
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(
                "PriorityLevel must be greater than 0 because level 0 is the default level.",
                GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            var entityAfter = Assert.Single(db.SupportPriorityLoyaltyRules);
            Assert.Equal(1, entityAfter.PriorityLevel);
            Assert.Equal(100_000m, entityAfter.MinTotalSpend);
            Assert.True(entityAfter.IsActive);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ================= UT003 =================
        // MinTotalSpend < 0 -> 400 BadRequest, không đổi rule.
        [Fact(DisplayName = "UT003 - MinTotalSpend < 0 -> 400 BadRequest")]
        public async Task Update_MinTotalSpendNegative_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_MinTotalSpendNegative_Returns400));

            int ruleId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                };
                seed.SupportPriorityLoyaltyRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.RuleId;
            }

            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleUpdateDto
            {
                PriorityLevel = 1,
                MinTotalSpend = -1m,
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(
                "MinTotalSpend must be greater than or equal to 0.",
                GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            var entityAfter = Assert.Single(db.SupportPriorityLoyaltyRules);
            Assert.Equal(1, entityAfter.PriorityLevel);
            Assert.Equal(100_000m, entityAfter.MinTotalSpend);
            Assert.True(entityAfter.IsActive);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ================= UT004 =================
        // Trùng combination PriorityLevel + MinTotalSpend với rule khác -> 400.
        [Fact(DisplayName = "UT004 - Duplicate (PriorityLevel + MinTotalSpend) -> 400 BadRequest")]
        public async Task Update_DuplicateCombination_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_DuplicateCombination_Returns400));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var target = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                };

                var other = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 200_000m,
                    IsActive = true
                };

                seed.SupportPriorityLoyaltyRules.AddRange(target, other);
                seed.SaveChanges();
                targetId = target.RuleId;
            }

            var controller = CreateController(options, out var auditMock);

            // Update target để trùng combination với other
            var dto = new SupportPriorityLoyaltyRuleUpdateDto
            {
                PriorityLevel = 2,
                MinTotalSpend = 200_000m,
                IsActive = true
            };

            var result = await controller.Update(targetId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(
                "Another rule with the same MinTotalSpend and PriorityLevel already exists.",
                GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            var rules = db.SupportPriorityLoyaltyRules.OrderBy(r => r.RuleId).ToList();
            Assert.Equal(2, rules.Count);

            Assert.Equal(1, rules[0].PriorityLevel);
            Assert.Equal(100_000m, rules[0].MinTotalSpend);
            Assert.True(rules[0].IsActive);

            Assert.Equal(2, rules[1].PriorityLevel);
            Assert.Equal(200_000m, rules[1].MinTotalSpend);
            Assert.True(rules[1].IsActive);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ================= UT005 =================
        // dto.IsActive = true nhưng vi phạm ordering với lower-level đang active -> 400.
        [Fact(DisplayName = "UT005 - Active rule violates ordering -> 400 BadRequest")]
        public async Task Update_ActiveRule_ViolateOrdering_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_ActiveRule_ViolateOrdering_Returns400));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                // Lower level active: level 1, 100k
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });

                // Target rule: level 2, 200k (active hay ko đều được)
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

            var controller = CreateController(options, out var auditMock);

            // Sau update: level 2 nhưng MinTotalSpend < lower-level => conflict
            var dto = new SupportPriorityLoyaltyRuleUpdateDto
            {
                PriorityLevel = 2,
                MinTotalSpend = 90_000m,
                IsActive = true
            };

            var result = await controller.Update(targetId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(OrderingErrorMessage, GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            var rules = db.SupportPriorityLoyaltyRules.OrderBy(r => r.PriorityLevel).ToList();
            Assert.Equal(2, rules.Count);

            // Lower level vẫn active, target không đổi
            Assert.Equal(1, rules[0].PriorityLevel);
            Assert.Equal(100_000m, rules[0].MinTotalSpend);
            Assert.True(rules[0].IsActive);

            Assert.Equal(2, rules[1].PriorityLevel);
            Assert.Equal(200_000m, rules[1].MinTotalSpend);
            Assert.False(rules[1].IsActive);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // ================= UT006 =================
        // Update hợp lệ, IsActive = false -> 204 NoContent,
        // chỉ target rule được cập nhật, rule khác giữ nguyên.
        [Fact(DisplayName = "UT006 - Valid update to inactive -> 204 NoContent")]
        public async Task Update_ValidInactiveRule_Succeeds()
        {
            var options = CreateInMemoryOptions(nameof(Update_ValidInactiveRule_Succeeds));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                // Một rule level 1
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });

                // Target rule level 2 đang active
                var target = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 200_000m,
                    IsActive = true
                };
                seed.SupportPriorityLoyaltyRules.Add(target);
                seed.SaveChanges();
                targetId = target.RuleId;
            }

            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleUpdateDto
            {
                PriorityLevel = 2,
                MinTotalSpend = 210_000m,
                IsActive = false
            };

            var result = await controller.Update(targetId, dto);

            Assert.IsType<NoContentResult>(result);

            using var db = new KeytietkiemDbContext(options);
            var rules = db.SupportPriorityLoyaltyRules
                .OrderBy(r => r.PriorityLevel)
                .ToList();

            Assert.Equal(2, rules.Count);

            var level1 = rules[0];
            var targetAfter = rules[1];

            Assert.Equal(1, level1.PriorityLevel);
            Assert.Equal(100_000m, level1.MinTotalSpend);
            Assert.True(level1.IsActive);             // không đổi

            Assert.Equal(2, targetAfter.PriorityLevel);
            Assert.Equal(210_000m, targetAfter.MinTotalSpend); // đã update
            Assert.False(targetAfter.IsActive);                 // tắt

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Update",
                    "SupportPriorityLoyaltyRule",
                    targetAfter.RuleId.ToString(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Once);
        }

        // ================= UT007 =================
        // Update hợp lệ, IsActive = true, không có rule cùng PriorityLevel đang active.
        [Fact(DisplayName = "UT007 - Valid update to active (no same-level active) -> 204 NoContent")]
        public async Task Update_ValidActive_NoSameLevelActive_Succeeds()
        {
            var options = CreateInMemoryOptions(nameof(Update_ValidActive_NoSameLevelActive_Succeeds));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                // Lower level 1 active
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });

                // Target level 2, đang inactive
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

            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleUpdateDto
            {
                PriorityLevel = 2,
                MinTotalSpend = 250_000m,
                IsActive = true
            };

            var result = await controller.Update(targetId, dto);

            Assert.IsType<NoContentResult>(result);

            using var db = new KeytietkiemDbContext(options);
            var rules = db.SupportPriorityLoyaltyRules
                .OrderBy(r => r.PriorityLevel)
                .ToList();

            Assert.Equal(2, rules.Count);

            var level1 = rules[0];
            var targetAfter = rules[1];

            Assert.True(level1.IsActive);                     // level khác giữ nguyên
            Assert.Equal(1, level1.PriorityLevel);

            Assert.Equal(2, targetAfter.PriorityLevel);
            Assert.Equal(250_000m, targetAfter.MinTotalSpend);
            Assert.True(targetAfter.IsActive);                // bật lên

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Update",
                    "SupportPriorityLoyaltyRule",
                    targetAfter.RuleId.ToString(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Once);
        }

        // ================= UT008 =================
        // Update hợp lệ, IsActive = true, có rule khác cùng PriorityLevel đang active:
        //  -> target bật, rule cùng level bị set IsActive = false.
        [Fact(DisplayName = "UT008 - Valid active update, deactivate same-level rules -> 204 NoContent")]
        public async Task Update_ValidActive_DeactivatesSameLevel_Succeeds()
        {
            var options = CreateInMemoryOptions(nameof(Update_ValidActive_DeactivatesSameLevel_Succeeds));

            int targetId;
            int oldSameLevelId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                // Lower level 1 active
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });

                // Target level 2 (inactive)
                var target = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 200_000m,
                    IsActive = false
                };
                seed.SupportPriorityLoyaltyRules.Add(target);

                // Same-level rule đang active sẽ bị tắt
                var sameLevel = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 220_000m,
                    IsActive = true
                };
                seed.SupportPriorityLoyaltyRules.Add(sameLevel);

                seed.SaveChanges();
                targetId = target.RuleId;
                oldSameLevelId = sameLevel.RuleId;
            }

            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleUpdateDto
            {
                PriorityLevel = 2,
                MinTotalSpend = 250_000m,
                IsActive = true
            };

            var result = await controller.Update(targetId, dto);

            Assert.IsType<NoContentResult>(result);

            using var db = new KeytietkiemDbContext(options);
            var rules = db.SupportPriorityLoyaltyRules
                .OrderBy(r => r.PriorityLevel)
                .ThenBy(r => r.MinTotalSpend)
                .ToList();

            Assert.Equal(3, rules.Count);

            var level1 = rules.Single(r => r.PriorityLevel == 1);
            var targetAfter = rules.Single(r => r.RuleId == targetId);
            var oldSameLevel = rules.Single(r => r.RuleId == oldSameLevelId);

            Assert.True(level1.IsActive);                  // level khác giữ nguyên

            Assert.True(targetAfter.IsActive);             // rule mới bật
            Assert.Equal(2, targetAfter.PriorityLevel);
            Assert.Equal(250_000m, targetAfter.MinTotalSpend);

            Assert.False(oldSameLevel.IsActive);           // rule cùng level bị tắt

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Update",
                    "SupportPriorityLoyaltyRule",
                    targetAfter.RuleId.ToString(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Once);
        }
    }
}
