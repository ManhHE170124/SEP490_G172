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
    public class SlaRulesAdminController_CreateTests
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
            var value = badRequest.Value;
            if (value == null) return string.Empty;

            var prop = value.GetType().GetProperty("message");
            return prop?.GetValue(value) as string ?? string.Empty;
        }

        #endregion

        // ============================================================
        // TC01 – Name empty / whitespace -> 400 BadRequest
        // ============================================================
        [Fact(DisplayName = "TC01 - Name empty -> 400 BadRequest")]
        public async Task Create_NameEmpty_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            var controller = CreateController(options);

            var dto = new SlaRuleAdminCreateDto
            {
                Name = "   ", // invalid
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Tên SLA rule không được để trống.", message);
        }

        // ============================================================
        // TC02 – Name length > 120 -> 400 BadRequest
        // ============================================================
        [Fact(DisplayName = "TC02 - Name > 120 chars -> 400 BadRequest")]
        public async Task Create_NameTooLong_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            var controller = CreateController(options);

            var dto = new SlaRuleAdminCreateDto
            {
                Name = new string('a', 121),
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Tên SLA rule không được vượt quá 120 ký tự.", message);
        }

        // ============================================================
        // TC03 – Severity empty / null -> 400 BadRequest
        // ============================================================
        [Fact(DisplayName = "TC03 - Severity empty -> 400 BadRequest")]
        public async Task Create_SeverityEmpty_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            var controller = CreateController(options);

            var dto = new SlaRuleAdminCreateDto
            {
                Name = "Valid name",
                Severity = "   ",
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Mức độ (Severity) không được để trống.", message);
        }

        // ============================================================
        // TC04 – Severity not in {Low, Medium, High, Critical} -> 400
        // ============================================================
        [Fact(DisplayName = "TC04 - Severity invalid -> 400 BadRequest")]
        public async Task Create_SeverityInvalid_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            var controller = CreateController(options);

            var dto = new SlaRuleAdminCreateDto
            {
                Name = "Valid name",
                Severity = "VeryHigh", // invalid
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetErrorMessage(badRequest);

            Assert.Equal(
                "Mức độ (Severity) không hợp lệ. Giá trị hợp lệ: Low, Medium, High, Critical.",
                message);
        }

        // ============================================================
        // TC05 – PriorityLevel < 0 -> 400
        // ============================================================
        [Fact(DisplayName = "TC05 - PriorityLevel < 0 -> 400 BadRequest")]
        public async Task Create_PriorityNegative_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            var controller = CreateController(options);

            var dto = new SlaRuleAdminCreateDto
            {
                Name = "Valid name",
                Severity = "Low",
                PriorityLevel = -1,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0.", message);
        }

        // ============================================================
        // TC06 – FirstResponseMinutes <= 0 -> 400
        // ============================================================
        [Fact(DisplayName = "TC06 - FirstResponseMinutes <= 0 -> 400 BadRequest")]
        public async Task Create_FirstResponseMinutesNonPositive_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            var controller = CreateController(options);

            var dto = new SlaRuleAdminCreateDto
            {
                Name = "Valid name",
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 0, // invalid
                ResolutionMinutes = 120,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Thời gian phản hồi đầu tiên (phút) phải lớn hơn 0.", message);
        }

        // ============================================================
        // TC07 – ResolutionMinutes <= 0 -> 400
        // ============================================================
        [Fact(DisplayName = "TC07 - ResolutionMinutes <= 0 -> 400 BadRequest")]
        public async Task Create_ResolutionMinutesNonPositive_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            var controller = CreateController(options);

            var dto = new SlaRuleAdminCreateDto
            {
                Name = "Valid name",
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 0, // invalid
                IsActive = false
            };

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal("Thời gian xử lý (phút) phải lớn hơn 0.", message);
        }

        // ============================================================
        // TC08 – ResolutionMinutes < FirstResponseMinutes -> 400
        // ============================================================
        [Fact(DisplayName = "TC08 - ResolutionMinutes < FirstResponseMinutes -> 400")]
        public async Task Create_ResolutionLessThanFirstResponse_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();
            var controller = CreateController(options);

            var dto = new SlaRuleAdminCreateDto
            {
                Name = "Valid name",
                Severity = "Low",
                PriorityLevel = 0,
                FirstResponseMinutes = 120,
                ResolutionMinutes = 60, // smaller
                IsActive = false
            };

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetErrorMessage(badRequest);
            Assert.Equal(
                "Thời gian xử lý (ResolutionMinutes) phải lớn hơn hoặc bằng thời gian phản hồi đầu tiên (FirstResponseMinutes).",
                message);
        }

        // ============================================================
        // TC09 – IsActive = true, vi phạm SLA monotonicity -> 400
        // (Resolution/FirstResponse không giảm khi Severity tăng)
        // ============================================================
        [Fact(DisplayName = "TC09 - Active rule violates monotonicity -> 400")]
        public async Task Create_ActiveRuleMonotonicViolated_ReturnsBadRequest()
        {
            var options = CreateInMemoryOptions();

            // Seed 1 active rule: Low, Priority 0, 60/120
            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SlaRules.Add(new SlaRule
                {
                    Name = "Existing Low P0",
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

            var dto = new SlaRuleAdminCreateDto
            {
                Name = "Medium P0 (invalid monotonic)",
                Severity = "Medium",       // nghiêm trọng hơn Low
                PriorityLevel = 0,
                FirstResponseMinutes = 60, // không ngắn hơn Low
                ResolutionMinutes = 120,   // không ngắn hơn Low
                IsActive = true
            };

            var result = await controller.Create(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var message = GetErrorMessage(badRequest);

            Assert.Contains("Với cùng PriorityLevel = 0", message);
            Assert.Contains("Severity Medium (nghiêm trọng hơn)", message);
        }

        // ============================================================
        // TC10 – IsActive = true, thỏa SLA monotonicity +
        //         đảm bảo chỉ 1 rule active / (Severity, PriorityLevel)
        // ============================================================
        [Fact(DisplayName = "TC10 - Active rule replaces old active in same group")]
        public async Task Create_ActiveRule_ReplacesExistingActiveInSameGroup()
        {
            var options = CreateInMemoryOptions();

            // Seed 1 active rule: High, Priority 1
            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SlaRules.Add(new SlaRule
                {
                    Name = "Old High P1",
                    Severity = "High",
                    PriorityLevel = 1,
                    FirstResponseMinutes = 120,
                    ResolutionMinutes = 240,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                seed.SaveChanges();
            }

            var controller = CreateController(options);

            var dto = new SlaRuleAdminCreateDto
            {
                Name = "New High P1 (better)",
                Severity = "High",
                PriorityLevel = 1,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = true
            };

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var detail = Assert.IsType<SlaRuleAdminDetailDto>(created.Value);

            using var verify = new KeytietkiemDbContext(options);
            var rules = verify.SlaRules.ToList();

            Assert.Equal(2, rules.Count);

            var group = rules.Where(r => r.Severity == "High" && r.PriorityLevel == 1).ToList();
            Assert.Equal(2, group.Count);

            // Chỉ 1 rule active trong group
            Assert.Single(group.Where(r => r.IsActive));
            Assert.Single(group.Where(r => !r.IsActive));

            var newRule = group.Single(r => r.IsActive);
            Assert.Equal(detail.SlaRuleId, newRule.SlaRuleId);
        }

        // ============================================================
        // TC11 – IsActive = false, cho phép vi phạm monotonicity
        //         (không gọi ValidateSlaConstraintsAsync)
        // ============================================================
        [Fact(DisplayName = "TC11 - Inactive rule can break monotonicity -> 201")]
        public async Task Create_InactiveRule_AllowsMonotonicViolation()
        {
            var options = CreateInMemoryOptions();

            // Seed 1 active rule: Low, Priority 0, 60/120
            using (var seed = new KeytietkiemDbContext(options))
            {
                seed.SlaRules.Add(new SlaRule
                {
                    Name = "Existing Low P0",
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

            // New rule "Medium" nhưng thời gian không ngắn hơn -> vi phạm monotonic
            // Tuy nhiên IsActive = false => Create vẫn cho phép.
            var dto = new SlaRuleAdminCreateDto
            {
                Name = "Medium P0 (inactive, broken monotonic)",
                Severity = "Medium",
                PriorityLevel = 0,
                FirstResponseMinutes = 60,
                ResolutionMinutes = 120,
                IsActive = false
            };

            var result = await controller.Create(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var detail = Assert.IsType<SlaRuleAdminDetailDto>(created.Value);
            Assert.False(detail.IsActive);

            using var verify = new KeytietkiemDbContext(options);
            var rules = verify.SlaRules.ToList();
            Assert.Equal(2, rules.Count);

            var activeCount = rules.Count(r => r.IsActive);
            var inactiveCount = rules.Count(r => !r.IsActive);

            Assert.Equal(1, activeCount);   // rule Low ban đầu vẫn active
            Assert.Equal(1, inactiveCount); // rule Medium mới inactive
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
