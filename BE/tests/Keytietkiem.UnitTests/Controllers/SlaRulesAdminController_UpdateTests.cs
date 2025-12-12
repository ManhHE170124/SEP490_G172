using Keytietkiem.Controllers;
using Keytietkiem.DTOs.SlaRules;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// xUnit tests cho SlaRulesAdminController.Update (UpdateSlaRule).
    /// 15 test case (UT20D01 - UT20D15) tương ứng decision table trong sheet.
    /// </summary>
    public class SlaRulesAdminController_UpdateTests
    {
        #region Helpers

        private static DbContextOptions<KeytietkiemDbContext> CreateInMemoryOptions()
        {
            return new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static SlaRulesAdminController CreateController(DbContextOptions<KeytietkiemDbContext> options)
        {
            var dbFactory = new TestDbContextFactory(options);
            var auditLogger = new FakeAuditLogger();

            var controller = new SlaRulesAdminController(dbFactory, auditLogger)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            return controller;
        }

        private static string GetErrorMessage(BadRequestObjectResult badRequest)
        {
            if (badRequest.Value == null) return string.Empty;

            var type = badRequest.Value.GetType();
            var prop = type.GetProperty("message");
            if (prop == null) return string.Empty;

            return prop.GetValue(badRequest.Value) as string ?? string.Empty;
        }

        #endregion

        // UT20D01 – Happy path: rule tồn tại, dữ liệu hợp lệ, IsActive = true,
        //            không vi phạm monotonicity, rule khác cùng cặp bị deactivate.
        [Fact(DisplayName = "UT20D01 - Valid update, active, single-active per pair")]
        public async Task UT20D01_ValidActiveUpdate_SucceedsAndDisablesOtherSamePair()
        {
            var options = CreateInMemoryOptions();

            int targetId;
            int otherId;

            // Seed 2 active rule cùng (Severity, PriorityLevel)
            using (var seed = new KeytietkiemDbContext(options))
            {
                var target = new SlaRule
                {
                    Name = "Old Low P0 - target",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                };
                var other = new SlaRule
                {
                    Name = "Old Low P0 - other",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 90,
                    ResolutionMinutes = 180,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddHours(-3)
                };

                seed.SlaRules.AddRange(target, other);
                seed.SaveChanges();

                targetId = target.SlaRuleId;
                otherId = other.SlaRuleId;
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "  Updated Low P0  ", // sẽ được Trim()
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 30,
                ResolutionMinutes = 60,
                IsActive = true
            };

            var result = await controller.Update(targetId, dto);

            var noContent = Assert.IsType<NoContentResult>(result);

            using (var verify = new KeytietkiemDbContext(options))
            {
                var target = await verify.SlaRules.FindAsync(targetId);
                var other = await verify.SlaRules.FindAsync(otherId);

                Assert.NotNull(target);
                Assert.NotNull(other);

                Assert.Equal("Updated Low P0", target!.Name);
                Assert.Equal("Low", target.Severity);
                Assert.Equal(0, target.PriorityLevel);
                Assert.Equal(30, target.FirstResponseMinutes);
                Assert.Equal(60, target.ResolutionMinutes);
                Assert.True(target.IsActive);
                Assert.NotNull(target.UpdatedAt);

                // Rule khác cùng (Severity, PriorityLevel) phải bị deactivate
                Assert.False(other!.IsActive);
            }
        }

        // UT20D02 – SlaRule không tồn tại -> 404 NotFound
        [Fact(DisplayName = "UT20D02 - Rule not found -> 404")]
        public async Task UT20D02_RuleNotFound_ReturnsNotFound()
        {
            var options = CreateInMemoryOptions();

            // Seed 1 rule nhưng dùng id khác
            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SlaRules.Add(new SlaRule
                {
                    Name = "Existing",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                seed.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "New Name",
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 30,
                ResolutionMinutes = 60,
                IsActive = true
            };

            var result = await controller.Update(9999, dto);

            Assert.IsType<NotFoundResult>(result);
        }

        // UT20D03 – Name empty / whitespace -> 400 BadRequest
        [Fact(DisplayName = "UT20D03 - Name empty -> 400")]
        public async Task UT20D03_NameEmpty_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            int ruleId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SlaRule
                {
                    Name = "Old Name",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                seed.SlaRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.SlaRuleId;
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "   ", // invalid
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Tên SLA rule không được để trống.", message);

            // DB không đổi
            using (var verify = new KeytietkiemDbContext(options))
            {
                var entity = await verify.SlaRules.FindAsync(ruleId);
                Assert.Equal("Old Name", entity!.Name);
            }
        }

        // UT20D04 – Name length > 120 -> 400 BadRequest
        [Fact(DisplayName = "UT20D04 - Name > 120 chars -> 400")]
        public async Task UT20D04_NameTooLong_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            int ruleId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SlaRule
                {
                    Name = "Old Name",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                seed.SlaRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.SlaRuleId;
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = new string('a', 121),
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Tên SLA rule không được vượt quá 120 ký tự.", message);
        }

        // UT20D05 – Severity empty / whitespace -> 400 BadRequest
        [Fact(DisplayName = "UT20D05 - Severity empty -> 400")]
        public async Task UT20D05_SeverityEmpty_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            int ruleId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SlaRule
                {
                    Name = "Rule",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                seed.SlaRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.SlaRuleId;
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "Rule",
                Severity = "   ",
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Mức độ (Severity) không được để trống.", message);
        }

        // UT20D06 – Severity invalid (not in allowed list) -> 400
        [Fact(DisplayName = "UT20D06 - Severity invalid -> 400")]
        public async Task UT20D06_SeverityInvalid_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            int ruleId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SlaRule
                {
                    Name = "Rule",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                seed.SlaRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.SlaRuleId;
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "Rule",
                Severity = "VeryHigh", // invalid
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal(
                "Mức độ (Severity) không hợp lệ. Giá trị hợp lệ: Low, Medium, High, Critical.",
                message);
        }

        // UT20D07 – PriorityLevel < 0 -> 400
        [Fact(DisplayName = "UT20D07 - PriorityLevel < 0 -> 400")]
        public async Task UT20D07_PriorityNegative_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            int ruleId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SlaRule
                {
                    Name = "Rule",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                seed.SlaRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.SlaRuleId;
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "Rule",
                Severity = "Low",
                PriorityLevel = -1,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0.", message);
        }

        // UT20D08 – FirstResponseMinutes <= 0 -> 400
        [Fact(DisplayName = "UT20D08 - FirstResponseMinutes <= 0 -> 400")]
        public async Task UT20D08_FirstResponseMinutesNonPositive_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            int ruleId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SlaRule
                {
                    Name = "Rule",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                seed.SlaRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.SlaRuleId;
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "Rule",
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 0,
                ResolutionMinutes = 120,
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Thời gian phản hồi đầu tiên (phút) phải lớn hơn 0.", message);
        }

        // UT20D09 – ResolutionMinutes <= 0 -> 400
        [Fact(DisplayName = "UT20D09 - ResolutionMinutes <= 0 -> 400")]
        public async Task UT20D09_ResolutionMinutesNonPositive_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            int ruleId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SlaRule
                {
                    Name = "Rule",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                seed.SlaRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.SlaRuleId;
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "Rule",
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 0,
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Thời gian xử lý (phút) phải lớn hơn 0.", message);
        }

        // UT20D10 – ResolutionMinutes < FirstResponseMinutes -> 400
        [Fact(DisplayName = "UT20D10 - ResolutionMinutes < FirstResponseMinutes -> 400")]
        public async Task UT20D10_ResolutionLessThanFirstResponse_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            int ruleId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var entity = new SlaRule
                {
                    Name = "Rule",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 120,
                    ResolutionMinutes = 240,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                seed.SlaRules.Add(entity);
                seed.SaveChanges();
                ruleId = entity.SlaRuleId;
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "Rule",
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 120,
                ResolutionMinutes = 60, // < FRM
                IsActive = true
            };

            var result = await controller.Update(ruleId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal(
                "Thời gian xử lý (ResolutionMinutes) phải lớn hơn hoặc bằng thời gian phản hồi đầu tiên (FirstResponseMinutes).",
                message);
        }

        // UT20D11 – New IsActive = false, vẫn cho phép vi phạm monotonicity,
        //            kết quả 204 NoContent và rule được update.
        [Fact(DisplayName = "UT20D11 - Inactive rule can violate monotonicity -> 204")]
        public async Task UT20D11_InactiveRule_AllowsMonotonicViolation()
        {
            var options = CreateInMemoryOptions();
            int ruleId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                // 1 rule active khác để tạo bối cảnh
                seed.SlaRules.Add(new SlaRule
                {
                    Name = "Active Low P0",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });

                // Rule cần update ban đầu IsActive = false
                var inactive = new SlaRule
                {
                    Name = "Inactive Medium P0",
                    Severity = "Medium",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 30,
                    ResolutionMinutes = 60,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow
                };

                seed.SlaRules.Add(inactive);
                seed.SaveChanges();
                ruleId = inactive.SlaRuleId;
            }

            var controller = CreateController(options);

            // Cập nhật rule với thời gian tệ hơn (nếu active sẽ phá monotonicity)
            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "Inactive Medium P0 - updated",
                Severity = "Medium",
                PriorityLevel = 0,
                FirstResponseMinutes = 60, // bằng Low
                ResolutionMinutes = 120,
                IsActive = false // vẫn inactive
            };

            var result = await controller.Update(ruleId, dto);

            Assert.IsType<NoContentResult>(result);

            using (var verify = new KeytietkiemDbContext(options))
            {
                var entity = await verify.SlaRules.FindAsync(ruleId);
                Assert.NotNull(entity);
                Assert.Equal("Inactive Medium P0 - updated", entity!.Name);
                Assert.False(entity.IsActive); // vẫn inactive
                Assert.Equal(60, entity.FirstResponseMinutes);
                Assert.Equal(120, entity.ResolutionMinutes);
            }
        }

        // UT20D12 – New IsActive = true, vi phạm monotonicity -> 400 BadRequest
        //            và DB không thay đổi.
        [Fact(DisplayName = "UT20D12 - Active rule violates monotonicity -> 400, no DB change")]
        public async Task UT20D12_ActiveRuleMonotonicViolation_ReturnsBadRequestAndNoChange()
        {
            var options = CreateInMemoryOptions();
            int lowId;
            int mediumId;

            using (var seed = new KeytietkiemDbContext(options))
            {
                var low = new SlaRule
                {
                    Name = "Low P0",
                    Severity = "Low",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 60,
                    ResolutionMinutes = 120,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                var medium = new SlaRule
                {
                    Name = "Medium P0",
                    Severity = "Medium",
                    PriorityLevel = 0,
                    FirstResponseMinutes = 50,
                    ResolutionMinutes = 100,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                seed.SlaRules.AddRange(low, medium);
                seed.SaveChanges();

                lowId = low.SlaRuleId;
                mediumId = medium.SlaRuleId;
            }

            var controller = CreateController(options);

            // Cập nhật Medium P0 thành thời gian không còn ngắn hơn Low P0
            var dto = new SlaRuleAdminUpdateDto
            {
                Name = "Medium P0 - worse",
                Severity = "Medium",
                PriorityLevel = 0,
                FirstResponseMinutes = 60, // = Low
                ResolutionMinutes = 120,    // = Low
                IsActive = true
            };

            var result = await controller.Update(mediumId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = GetErrorMessage(badRequest);
            Assert.Contains("Với cùng PriorityLevel = 0", message);
            Assert.Contains("Severity Medium (nghiêm trọng hơn)", message);

            // DB vẫn giữ giá trị cũ
            using (var verify = new KeytietkiemDbContext(options))
            {
                var low = await verify.SlaRules.FindAsync(lowId);
                var medium = await verify.SlaRules.FindAsync(mediumId);

                Assert.NotNull(low);
                Assert.NotNull(medium);

                Assert.Equal(60, low!.FirstResponseMinutes);
                Assert.Equal(120, low.ResolutionMinutes);

                Assert.Equal(50, medium!.FirstResponseMinutes);
                Assert.Equal(100, medium.ResolutionMinutes);
                Assert.Equal("Medium P0", medium.Name);
            }
        }
        // ============================================================
        // Internal helpers: TestDbContextFactory + FakeAuditLogger
        // ============================================================
        private sealed class TestDbContextFactory : IDbContextFactory<KeytietkiemDbContext>
        {
            private readonly DbContextOptions<KeytietkiemDbContext> _options;

            public TestDbContextFactory(DbContextOptions<KeytietkiemDbContext> options)
            {
                _options = options;
            }

            public KeytietkiemDbContext CreateDbContext()
                => new KeytietkiemDbContext(_options);

            public Task<KeytietkiemDbContext> CreateDbContextAsync(
                CancellationToken cancellationToken = default)
                => Task.FromResult(CreateDbContext());
        }

        private sealed class FakeAuditLogger : IAuditLogger
        {
            public Task LogAsync(
                HttpContext httpContext,
                string action,
                string entityType,
                string? entityId,
                object? before,
                object? after)
            {
                // No-op trong unit test
                return Task.CompletedTask;
            }
        }
    }
}