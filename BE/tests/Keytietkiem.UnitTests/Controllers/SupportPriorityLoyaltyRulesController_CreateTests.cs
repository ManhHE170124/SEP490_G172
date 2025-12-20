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
    /// Unit tests cho SupportPriorityLoyaltyRulesController.Create
    /// (CreateSupportPriorityLoyaltyRule) – UT001–UT006 theo decision table.
    /// </summary>
    public class SupportPriorityLoyaltyRulesController_CreateTests
    {
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

        // =============== UT001 ===============
        // PriorityLevel <= 0 -> 400 BadRequest, không insert rule.
        [Fact(DisplayName = "UT001 - PriorityLevel <= 0 -> 400 BadRequest")]
        public async Task Create_PriorityLevelLessOrEqualZero_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_PriorityLevelLessOrEqualZero_Returns400));
            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleCreateDto
            {
                PriorityLevel = 0,
                MinTotalSpend = 100_000m,
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "PriorityLevel must be greater than 0 because level 0 is the default level.",
                GetMessage(bad));

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

        // =============== UT002 ===============
        // MinTotalSpend < 0 -> 400 BadRequest, không insert rule.
        [Fact(DisplayName = "UT002 - MinTotalSpend < 0 -> 400 BadRequest")]
        public async Task Create_MinTotalSpendNegative_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_MinTotalSpendNegative_Returns400));
            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleCreateDto
            {
                PriorityLevel = 1,
                MinTotalSpend = -1m,
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "MinTotalSpend must be greater than or equal to 0.",
                GetMessage(bad));

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

        // =============== UT003 ===============
        // Trùng PriorityLevel + MinTotalSpend -> 400 BadRequest, không insert thêm.
        [Fact(DisplayName = "UT003 - Duplicate (PriorityLevel + MinTotalSpend) -> 400 BadRequest")]
        public async Task Create_DuplicateCombination_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_DuplicateCombination_Returns400));

            // Seed 1 rule trùng combination
            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleCreateDto
            {
                PriorityLevel = 1,
                MinTotalSpend = 100_000m,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "A rule with the same MinTotalSpend and PriorityLevel already exists.",
                GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            Assert.Single(db.SupportPriorityLoyaltyRules); // chỉ còn rule seed

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // =============== UT004 ===============
        // IsActive = true nhưng vi phạm ordering (conflict với rule active khác) -> 400.
        [Fact(DisplayName = "UT004 - Active rule violates ordering -> 400 BadRequest")]
        public async Task Create_ActiveRule_ViolateOrdering_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_ActiveRule_ViolateOrdering_Returns400));

            // Seed 1 rule level thấp hơn nhưng MinTotalSpend lớn hơn -> sẽ gây conflict
            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(options, out var auditMock);

            // New rule level 2 nhưng MinTotalSpend nhỏ hơn 100k -> conflict
            var dto = new SupportPriorityLoyaltyRuleCreateDto
            {
                PriorityLevel = 2,
                MinTotalSpend = 90_000m,
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = GetMessage(bad);
            Assert.NotNull(msg);
            Assert.StartsWith("Không thể bật rule này", msg);

            using var db = new KeytietkiemDbContext(options);
            var only = Assert.Single(db.SupportPriorityLoyaltyRules);
            Assert.Equal(1, only.PriorityLevel);
            Assert.True(only.IsActive); // không thay đổi

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<object?>()),
                Times.Never);
        }

        // =============== UT005 ===============
        // IsActive = false, dữ liệu valid, không trùng -> 201 Created, rule mới IsActive=false,
        // các rule khác giữ nguyên.
        [Fact(DisplayName = "UT005 - Valid inactive rule -> 201 Created, IsActive = false")]
        public async Task Create_ValidInactiveRule_Succeeds()
        {
            var options = CreateInMemoryOptions(nameof(Create_ValidInactiveRule_Succeeds));

            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });
                seed.SaveChanges();
            }

            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPriorityLoyaltyRuleCreateDto
            {
                PriorityLevel = 2,
                MinTotalSpend = 200_000m,
                IsActive = false
            };

            var actionResult = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var detail = Assert.IsType<SupportPriorityLoyaltyRuleDetailDto>(created.Value);
            Assert.Equal(dto.PriorityLevel, detail.PriorityLevel);
            Assert.Equal(dto.MinTotalSpend, detail.MinTotalSpend);
            Assert.False(detail.IsActive);

            using var db = new KeytietkiemDbContext(options);
            var rules = db.SupportPriorityLoyaltyRules.OrderBy(r => r.PriorityLevel).ToList();
            Assert.Equal(2, rules.Count);

            var level1 = rules[0];
            var newRule = rules[1];

            Assert.Equal(1, level1.PriorityLevel);
            Assert.True(level1.IsActive); // không bị tắt

            Assert.Equal(2, newRule.PriorityLevel);
            Assert.False(newRule.IsActive);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Create",
                    "SupportPriorityLoyaltyRule",
                    newRule.RuleId.ToString(),
                    null,
                    It.IsAny<object?>()),
                Times.Once);
        }

        // =============== UT006 ===============
        // IsActive = true, dữ liệu valid, ordering OK, có rule cùng PriorityLevel đang active:
        //  -> insert rule mới IsActive=true, các rule cùng PriorityLevel khác bị set false,
        //     rule level khác giữ nguyên.
        [Fact(DisplayName = "UT006 - Valid active rule -> 201 Created, deactivate same-level rules")]
        public async Task Create_ValidActiveRule_DeactivatesSameLevel()
        {
            var options = CreateInMemoryOptions(nameof(Create_ValidActiveRule_DeactivatesSameLevel));

            int oldSameLevelId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                // Lower level 1, MinTotalSpend=100k, active
                seed.SupportPriorityLoyaltyRules.Add(new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 1,
                    MinTotalSpend = 100_000m,
                    IsActive = true
                });

                // Same level 2, MinTotalSpend=200k, active (sẽ bị tắt)
                var sameLevel = new SupportPriorityLoyaltyRule
                {
                    PriorityLevel = 2,
                    MinTotalSpend = 200_000m,
                    IsActive = true
                };
                seed.SupportPriorityLoyaltyRules.Add(sameLevel);
                seed.SaveChanges();
                oldSameLevelId = sameLevel.RuleId;
            }

            var controller = CreateController(options, out var auditMock);

            // New rule cùng level 2 nhưng MinTotalSpend cao hơn 200k -> ordering OK
            var dto = new SupportPriorityLoyaltyRuleCreateDto
            {
                PriorityLevel = 2,
                MinTotalSpend = 250_000m,
                IsActive = true
            };

            var actionResult = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var detail = Assert.IsType<SupportPriorityLoyaltyRuleDetailDto>(created.Value);
            Assert.Equal(2, detail.PriorityLevel);
            Assert.Equal(250_000m, detail.MinTotalSpend);
            Assert.True(detail.IsActive);

            using var db = new KeytietkiemDbContext(options);
            var rules = db.SupportPriorityLoyaltyRules.OrderBy(r => r.PriorityLevel).ThenBy(r => r.MinTotalSpend).ToList();
            Assert.Equal(3, rules.Count);

            var level1 = rules.Single(r => r.PriorityLevel == 1);
            var oldSameLevel = rules.Single(r => r.RuleId == oldSameLevelId);
            var newRule = rules.Single(r => r.RuleId == detail.RuleId);

            Assert.True(level1.IsActive);         // level khác giữ nguyên
            Assert.False(oldSameLevel.IsActive);  // same level cũ bị tắt
            Assert.True(newRule.IsActive);        // rule mới bật

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Create",
                    "SupportPriorityLoyaltyRule",
                    newRule.RuleId.ToString(),
                    null,
                    It.IsAny<object?>()),
                Times.Once);
        }
    }
}
