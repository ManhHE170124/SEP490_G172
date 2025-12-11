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
    /// Unit tests cho SupportPlansAdminController.Update (UpdateSupportPlan)
    /// 10 test case: UT001–UT010, bám theo decision table.
    /// </summary>
    public class SupportPlansAdminController_UpdateTests
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

        // ================= UT001 =================
        // SupportPlan không tồn tại -> 404
        [Fact(DisplayName = "UT001 - SupportPlan not found -> 404 NotFound")]
        public async Task Update_NotFound_Returns404()
        {
            var options = CreateInMemoryOptions(nameof(Update_NotFound_Returns404));
            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = "Any",
                Description = "any",
                PriorityLevel = 0,
                Price = 0m,
                IsActive = false
            };

            var result = await controller.Update(999, dto);

            Assert.IsType<NotFoundResult>(result);

            using var db = new KeytietkiemDbContext(options);
            Assert.Empty(db.SupportPlans);
        }

        // ================= UT002 =================
        // Name rỗng / whitespace -> 400, không update
        [Fact(DisplayName = "UT002 - Name empty/whitespace -> 400 BadRequest")]
        public async Task Update_NameEmpty_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_NameEmpty_Returns400));

            int id;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SupportPlan
                {
                    Name = "OldName",
                    Description = "OldDesc",
                    PriorityLevel = 1,
                    Price = 100_000m,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };
                seed.SupportPlans.Add(entity);
                seed.SaveChanges();
                id = entity.SupportPlanId;
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = "   ",
                Description = "NewDesc",
                PriorityLevel = 2,
                Price = 200_000m,
                IsActive = true
            };

            var result = await controller.Update(id, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Tên gói không được để trống.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            var entityAfter = Assert.Single(db.SupportPlans);
            Assert.Equal("OldName", entityAfter.Name);
            Assert.Equal("OldDesc", entityAfter.Description);
            Assert.Equal(1, entityAfter.PriorityLevel);
            Assert.Equal(100_000m, entityAfter.Price);
            Assert.False(entityAfter.IsActive);
        }

        // ================= UT003 =================
        // Name > 120 -> 400
        [Fact(DisplayName = "UT003 - Name length > 120 -> 400 BadRequest")]
        public async Task Update_NameTooLong_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_NameTooLong_Returns400));

            int id;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SupportPlan
                {
                    Name = "OldName",
                    Description = "OldDesc",
                    PriorityLevel = 1,
                    Price = 100_000m,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };
                seed.SupportPlans.Add(entity);
                seed.SaveChanges();
                id = entity.SupportPlanId;
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = new string('A', 121),
                Description = "NewDesc",
                PriorityLevel = 2,
                Price = 200_000m,
                IsActive = true
            };

            var result = await controller.Update(id, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Tên gói không được vượt quá 120 ký tự.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            var entityAfter = Assert.Single(db.SupportPlans);
            Assert.Equal("OldName", entityAfter.Name);
        }

        // ================= UT004 =================
        // Description > 500 -> 400
        [Fact(DisplayName = "UT004 - Description length > 500 -> 400 BadRequest")]
        public async Task Update_DescriptionTooLong_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_DescriptionTooLong_Returns400));

            int id;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SupportPlan
                {
                    Name = "OldName",
                    Description = "OldDesc",
                    PriorityLevel = 1,
                    Price = 100_000m,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };
                seed.SupportPlans.Add(entity);
                seed.SaveChanges();
                id = entity.SupportPlanId;
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = "NewName",
                Description = new string('D', 501),
                PriorityLevel = 2,
                Price = 200_000m,
                IsActive = true
            };

            var result = await controller.Update(id, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Mô tả không được vượt quá 500 ký tự.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            var entityAfter = Assert.Single(db.SupportPlans);
            Assert.Equal("OldDesc", entityAfter.Description);
        }

        // ================= UT005 =================
        // PriorityLevel < 0 -> 400
        [Fact(DisplayName = "UT005 - PriorityLevel < 0 -> 400 BadRequest")]
        public async Task Update_PriorityNegative_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_PriorityNegative_Returns400));

            int id;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SupportPlan
                {
                    Name = "OldName",
                    Description = "OldDesc",
                    PriorityLevel = 1,
                    Price = 100_000m,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };
                seed.SupportPlans.Add(entity);
                seed.SaveChanges();
                id = entity.SupportPlanId;
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = "NewName",
                Description = "NewDesc",
                PriorityLevel = -1,
                Price = 200_000m,
                IsActive = true
            };

            var result = await controller.Update(id, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            var entityAfter = Assert.Single(db.SupportPlans);
            Assert.Equal(1, entityAfter.PriorityLevel);
        }

        // ================= UT006 =================
        // Price < 0 -> 400
        [Fact(DisplayName = "UT006 - Price < 0 -> 400 BadRequest")]
        public async Task Update_PriceNegative_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_PriceNegative_Returns400));

            int id;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SupportPlan
                {
                    Name = "OldName",
                    Description = "OldDesc",
                    PriorityLevel = 1,
                    Price = 100_000m,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };
                seed.SupportPlans.Add(entity);
                seed.SaveChanges();
                id = entity.SupportPlanId;
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = "NewName",
                Description = "NewDesc",
                PriorityLevel = 1,
                Price = -1m,
                IsActive = true
            };

            var result = await controller.Update(id, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Giá gói phải lớn hơn hoặc bằng 0.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            var entityAfter = Assert.Single(db.SupportPlans);
            Assert.Equal(100_000m, entityAfter.Price);
        }

        // ================= UT007 =================
        // Duplicate PriorityLevel + Price với plan khác -> 400
        [Fact(DisplayName = "UT007 - Duplicate PriorityLevel + Price -> 400 BadRequest")]
        public async Task Update_DuplicatePriorityAndPrice_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_DuplicatePriorityAndPrice_Returns400));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var target = new SupportPlan
                {
                    Name = "Target",
                    Description = "target",
                    PriorityLevel = 1,
                    Price = 150_000m,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                var other = new SupportPlan
                {
                    Name = "Other",
                    Description = "other",
                    PriorityLevel = 2,
                    Price = 200_000m,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };
                seed.SupportPlans.AddRange(target, other);
                seed.SaveChanges();
                targetId = target.SupportPlanId;
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = "NewName",
                Description = "NewDesc",
                PriorityLevel = 2,
                Price = 200_000m, // trùng với other
                IsActive = true
            };

            var result = await controller.Update(targetId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(
                "Đã tồn tại gói hỗ trợ khác có cùng mức ưu tiên và giá tiền. Vui lòng chọn giá khác.",
                GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            Assert.Equal(2, db.SupportPlans.Count());
            var targetAfter = db.SupportPlans.Single(p => p.SupportPlanId == targetId);
            Assert.Equal(1, targetAfter.PriorityLevel);
            Assert.Equal(150_000m, targetAfter.Price);
        }

        // ================= UT008 =================
        // IsActive = true, vi phạm rule: có plan lower-level active có Price >= current
        [Fact(DisplayName = "UT008 - Active plan, lower-level active has higher/equal price -> 400 BadRequest")]
        public async Task Update_ActivePlan_LowerLevelViolation_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_ActivePlan_LowerLevelViolation_Returns400));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SupportPlans.Add(new SupportPlan
                {
                    Name = "Standard",
                    Description = "L0",
                    PriorityLevel = 0,
                    Price = 100_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                });

                var target = new SupportPlan
                {
                    Name = "Priority",
                    Description = "L1",
                    PriorityLevel = 1,
                    Price = 150_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                seed.SupportPlans.Add(target);

                seed.SaveChanges();
                targetId = target.SupportPlanId;
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = "Priority",
                Description = "L1 updated",
                PriorityLevel = 1,
                Price = 90_000m,   // <= 100k -> vi phạm
                IsActive = true
            };

            var result = await controller.Update(targetId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = GetMessage(bad);
            Assert.NotNull(msg);
            Assert.Contains("PriorityLevel cao hơn", msg);

            using var db = new KeytietkiemDbContext(options);
            var targetAfter = db.SupportPlans.Single(p => p.SupportPlanId == targetId);
            Assert.Equal(150_000m, targetAfter.Price);
        }

        // ================= UT009 =================
        // IsActive = true, vi phạm rule: có plan higher-level active có Price <= current
        [Fact(DisplayName = "UT009 - Active plan, higher-level active has lower/equal price -> 400 BadRequest")]
        public async Task Update_ActivePlan_HigherLevelViolation_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Update_ActivePlan_HigherLevelViolation_Returns400));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                var target = new SupportPlan
                {
                    Name = "Priority",
                    Description = "L1",
                    PriorityLevel = 1,
                    Price = 150_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                };
                seed.SupportPlans.Add(target);

                seed.SupportPlans.Add(new SupportPlan
                {
                    Name = "VIP",
                    Description = "L2",
                    PriorityLevel = 2,
                    Price = 200_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                });

                seed.SaveChanges();
                targetId = target.SupportPlanId;
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = "Priority",
                Description = "L1 updated",
                PriorityLevel = 1,
                Price = 220_000m, // >= 200k -> vi phạm
                IsActive = true
            };

            var result = await controller.Update(targetId, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var msg = GetMessage(bad);
            Assert.NotNull(msg);
            Assert.Contains("PriorityLevel thấp hơn", msg);

            using var db = new KeytietkiemDbContext(options);
            var targetAfter = db.SupportPlans.Single(p => p.SupportPlanId == targetId);
            Assert.Equal(150_000m, targetAfter.Price);
        }

        // ================= UT010 =================
        // Happy path: update hợp lệ, IsActive=true, thỏa rule giá,
        // tắt các plan cùng PriorityLevel khác, giữ nguyên IsActive các level khác.
        [Fact(DisplayName = "UT010 - Valid update active plan -> 204 NoContent, fields updated, same-level deactivated")]
        public async Task Update_ActivePlan_Valid_UpdatesAndDeactivatesSameLevel()
        {
            var options = CreateInMemoryOptions(nameof(Update_ActivePlan_Valid_UpdatesAndDeactivatesSameLevel));

            int targetId;
            using (var seed = new KeytietkiemDbContext(options))
            {
                // lower level 0 active 100k
                seed.SupportPlans.Add(new SupportPlan
                {
                    Name = "Standard",
                    Description = "L0",
                    PriorityLevel = 0,
                    Price = 100_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-4)
                });

                // cùng level, sẽ bị tắt
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
                    Name = "Priority-Current",
                    Description = "current L1",
                    PriorityLevel = 1,
                    Price = 170_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                seed.SupportPlans.Add(target);

                seed.SaveChanges();
                targetId = target.SupportPlanId;
            }

            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPlanAdminUpdateDto
            {
                Name = "Priority-NewName",
                Description = "updated desc",
                PriorityLevel = 1,
                Price = 200_000m,   // > 100k (lower level) => hợp lệ
                IsActive = true
            };

            var result = await controller.Update(targetId, dto);

            var noContent = Assert.IsType<NoContentResult>(result);
            Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

            using var db = new KeytietkiemDbContext(options);
            var plans = db.SupportPlans.ToList();
            Assert.Equal(3, plans.Count);

            var standard = plans.Single(p => p.PriorityLevel == 0);
            var oldPriority = plans.Single(p => p.Name == "Priority-Old");
            var updated = plans.Single(p => p.SupportPlanId == targetId);

            Assert.True(standard.IsActive);      // khác level, vẫn active
            Assert.False(oldPriority.IsActive);  // cùng level, bị tắt
            Assert.True(updated.IsActive);       // target active

            Assert.Equal("Priority-NewName", updated.Name);
            Assert.Equal("updated desc", updated.Description);
            Assert.Equal(1, updated.PriorityLevel);
            Assert.Equal(200_000m, updated.Price);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Update",
                    "SupportPlan",
                    targetId.ToString(),
                    It.IsAny<object>(),
                    It.IsAny<object>()),
                Times.Once);
        }
    }
}
