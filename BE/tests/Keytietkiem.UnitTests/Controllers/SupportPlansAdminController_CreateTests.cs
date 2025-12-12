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
    /// Unit test cho SupportPlansAdminController.Create (CreateSupportPlan)
    /// UT001–UT010 như sheet (10 test case).
    /// </summary>
    public class SupportPlansAdminController_CreateTests
    {
        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions(string dbName)
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        /// <summary>
        /// Tạo controller với IDbContextFactory và IAuditLogger mock,
        /// mọi DbContext đều dùng chung cùng 1 InMemoryDatabase thông qua options.
        /// </summary>
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

        // ============== UT001 ==============
        // Happy path: Name/Description ở boundary hợp lệ, PriorityLevel=0, Price=0, IsActive=false
        [Fact(DisplayName = "UT001 - Valid inactive plan (Name=120, Desc=500, Priority=0, Price=0) -> 201 Created")]
        public async Task Create_ValidInactivePlan_Boundaries_Succeeds()
        {
            var options = CreateInMemoryOptions(nameof(Create_ValidInactivePlan_Boundaries_Succeeds));
            var controller = CreateController(options, out var auditMock);

            var dto = new SupportPlanAdminCreateDto
            {
                Name = new string('A', 120),          // boundary
                Description = new string('D', 500),   // boundary
                PriorityLevel = 0,
                Price = 0m,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var detail = Assert.IsType<SupportPlanAdminDetailDto>(created.Value);
            Assert.Equal(dto.Name, detail.Name);
            Assert.Equal(dto.Description, detail.Description);
            Assert.False(detail.IsActive);

            using var db = new KeytietkiemDbContext(options);
            var entity = Assert.Single(db.SupportPlans);
            Assert.Equal(dto.Name, entity.Name);
            Assert.Equal(dto.Description, entity.Description);
            Assert.Equal(0, entity.PriorityLevel);
            Assert.Equal(0m, entity.Price);
            Assert.False(entity.IsActive);

            auditMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Create",
                    "SupportPlan",
                    entity.SupportPlanId.ToString(),
                    null,
                    It.IsAny<object>()),
                Times.Once);
        }

        // ============== UT002 ==============
        // Name rỗng/whitespace -> 400
        [Fact(DisplayName = "UT002 - Name empty/whitespace -> 400 BadRequest")]
        public async Task Create_NameEmpty_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_NameEmpty_Returns400));
            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminCreateDto
            {
                Name = "   ",
                Description = "desc",
                PriorityLevel = 0,
                Price = 10_000m,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Tên gói không được để trống.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            Assert.Empty(db.SupportPlans);
        }

        // ============== UT003 ==============
        // Name > 120 -> 400
        [Fact(DisplayName = "UT003 - Name length > 120 -> 400 BadRequest")]
        public async Task Create_NameTooLong_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_NameTooLong_Returns400));
            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminCreateDto
            {
                Name = new string('A', 121),
                Description = "desc",
                PriorityLevel = 0,
                Price = 10_000m,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Tên gói không được vượt quá 120 ký tự.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            Assert.Empty(db.SupportPlans);
        }

        // ============== UT004 ==============
        // Description > 500 -> 400
        [Fact(DisplayName = "UT004 - Description length > 500 -> 400 BadRequest")]
        public async Task Create_DescriptionTooLong_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_DescriptionTooLong_Returns400));
            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminCreateDto
            {
                Name = "Standard",
                Description = new string('D', 501),
                PriorityLevel = 0,
                Price = 10_000m,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Mô tả không được vượt quá 500 ký tự.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            Assert.Empty(db.SupportPlans);
        }

        // ============== UT005 ==============
        // PriorityLevel < 0 -> 400
        [Fact(DisplayName = "UT005 - PriorityLevel < 0 -> 400 BadRequest")]
        public async Task Create_PriorityNegative_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_PriorityNegative_Returns400));
            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminCreateDto
            {
                Name = "Standard",
                Description = "desc",
                PriorityLevel = -1,
                Price = 10_000m,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            Assert.Empty(db.SupportPlans);
        }

        // ============== UT006 ==============
        // Price < 0 -> 400
        [Fact(DisplayName = "UT006 - Price < 0 -> 400 BadRequest")]
        public async Task Create_PriceNegative_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_PriceNegative_Returns400));
            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminCreateDto
            {
                Name = "Standard",
                Description = "desc",
                PriorityLevel = 0,
                Price = -1m,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Giá gói phải lớn hơn hoặc bằng 0.", GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            Assert.Empty(db.SupportPlans);
        }

        // ============== UT007 ==============
        // Duplicate PriorityLevel + Price -> 400
        [Fact(DisplayName = "UT007 - Duplicate PriorityLevel + Price -> 400 BadRequest")]
        public async Task Create_DuplicatePriorityAndPrice_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_DuplicatePriorityAndPrice_Returns400));

            // Seed 1 plan
            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SupportPlans.Add(new SupportPlan
                {
                    Name = "OldPlan",
                    Description = "old",
                    PriorityLevel = 1,
                    Price = 100_000m,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                });
                seed.SaveChanges();
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminCreateDto
            {
                Name = "NewPlan",
                Description = "new",
                PriorityLevel = 1,
                Price = 100_000m,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(
                "Đã tồn tại gói hỗ trợ khác có cùng mức ưu tiên và giá tiền. Vui lòng chọn giá khác.",
                GetMessage(bad));

            using var db = new KeytietkiemDbContext(options);
            Assert.Single(db.SupportPlans); // chỉ có gói cũ
        }

        // ============== UT008 ==============
        // IsActive = true, vi phạm lower-level rule
        // (existing active lower level with price >= new price) -> 400 với helper message
        [Fact(DisplayName = "UT008 - Active plan, lower-level active has higher/equal price -> 400 BadRequest")]
        public async Task Create_ActivePlan_LowerLevelViolation_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_ActivePlan_LowerLevelViolation_Returns400));

            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SupportPlans.Add(new SupportPlan
                {
                    Name = "Standard",
                    Description = "L0",
                    PriorityLevel = 0,
                    Price = 100_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                });
                seed.SaveChanges();
            }

            var controller = CreateController(options, out _);

            // New higher level but price không cao hơn -> vi phạm
            var dto = new SupportPlanAdminCreateDto
            {
                Name = "Priority",
                Description = "L1",
                PriorityLevel = 1,
                Price = 100_000m,
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = GetMessage(bad);

            Assert.NotNull(msg);
            Assert.Contains("PriorityLevel cao hơn", msg); // helper message

            using var db = new KeytietkiemDbContext(options);
            var only = Assert.Single(db.SupportPlans);
            Assert.Equal(0, only.PriorityLevel);
            Assert.True(only.IsActive);
        }

        // ============== UT009 ==============
        // IsActive = true, vi phạm higher-level rule
        // (existing active higher level with price <= new price) -> 400 với helper message
        [Fact(DisplayName = "UT009 - Active plan, higher-level active has lower/equal price -> 400 BadRequest")]
        public async Task Create_ActivePlan_HigherLevelViolation_Returns400()
        {
            var options = CreateInMemoryOptions(nameof(Create_ActivePlan_HigherLevelViolation_Returns400));

            using (var seed = new KeytietkiemDbContext(options))
            {
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
            }

            var controller = CreateController(options, out _);

            // New lower level nhưng giá không thấp hơn gói level 2 -> vi phạm
            var dto = new SupportPlanAdminCreateDto
            {
                Name = "Priority",
                Description = "L1",
                PriorityLevel = 1,
                Price = 200_000m,
                IsActive = true
            };

            var result = await controller.Create(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var msg = GetMessage(bad);

            Assert.NotNull(msg);
            Assert.Contains("PriorityLevel thấp hơn", msg); // helper message

            using var db = new KeytietkiemDbContext(options);
            var only = Assert.Single(db.SupportPlans);
            Assert.Equal(2, only.PriorityLevel);
            Assert.True(only.IsActive);
        }

        // ============== UT010 ==============
        // IsActive = true, same PriorityLevel với gói active cũ:
        //  - thỏa rule giá với các level khác
        //  - gói cũ cùng PriorityLevel bị set IsActive=false
        //  - gói mới được insert & Active
        [Fact(DisplayName = "UT010 - Active new plan same PriorityLevel -> old same-level active becomes inactive, new active created")]
        public async Task Create_ActivePlan_SameLevel_DisablesOldAndInsertsNew()
        {
            var options = CreateInMemoryOptions(nameof(Create_ActivePlan_SameLevel_DisablesOldAndInsertsNew));

            using (var seed = new KeytietkiemDbContext(options))
            {
                // Level 0: 100k (active)
                seed.SupportPlans.Add(new SupportPlan
                {
                    Name = "Standard",
                    Description = "L0",
                    PriorityLevel = 0,
                    Price = 100_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                });

                // Level 1: 150k (active) -> sẽ bị tắt
                seed.SupportPlans.Add(new SupportPlan
                {
                    Name = "Priority-Old",
                    Description = "old L1",
                    PriorityLevel = 1,
                    Price = 150_000m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                });

                seed.SaveChanges();
            }

            var controller = CreateController(options, out _);

            var dto = new SupportPlanAdminCreateDto
            {
                Name = "Priority-New",
                Description = "new L1",
                PriorityLevel = 1,
                Price = 200_000m, // > 100k của level 0 -> không vi phạm rule giá
                IsActive = true
            };

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var detail = Assert.IsType<SupportPlanAdminDetailDto>(created.Value);

            using var db = new KeytietkiemDbContext(options);
            var all = db.SupportPlans.OrderBy(p => p.CreatedAt).ToList();
            Assert.Equal(3, all.Count);

            var standard = all.Single(p => p.PriorityLevel == 0);
            var oldPriority = all.First(p => p.Name == "Priority-Old");
            var newPriority = all.First(p => p.Name == "Priority-New");

            Assert.True(standard.IsActive);
            Assert.False(oldPriority.IsActive);
            Assert.True(newPriority.IsActive);
            Assert.Equal(detail.SupportPlanId, newPriority.SupportPlanId);
        }
    }
}
