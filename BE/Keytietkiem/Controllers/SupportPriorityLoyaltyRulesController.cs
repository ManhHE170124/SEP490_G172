using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Support;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/support-priority-loyalty-rules")]
    public class SupportPriorityLoyaltyRulesController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IAuditLogger _auditLogger;

        public SupportPriorityLoyaltyRulesController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IAuditLogger auditLogger)
        {
            _dbFactory = dbFactory;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// GET: /api/support-priority-loyalty-rules
        /// List rules có filter + paging.
        /// Luôn sắp xếp theo PriorityLevel tăng dần, sau đó MinTotalSpend tăng dần.
        /// Bỏ qua toàn bộ level 0 (default).
        /// </summary>
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<PagedResult<SupportPriorityLoyaltyRuleListItemDto>>> List(
            [FromQuery] int? priorityLevel,
            [FromQuery] bool? active,
            // sort/direction tạm giữ nhưng không dùng
            [FromQuery] string? sort = null,
            [FromQuery] string? direction = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.SupportPriorityLoyaltyRules
                      .AsNoTracking()
                      // loại bỏ level 0 khỏi tất cả logic
                      .Where(r => r.PriorityLevel > 0)
                      .AsQueryable();

            // Filter theo PriorityLevel (>0)
            if (priorityLevel.HasValue && priorityLevel.Value > 0)
            {
                q = q.Where(r => r.PriorityLevel == priorityLevel.Value);
            }

            // Filter theo IsActive
            if (active.HasValue)
            {
                q = q.Where(r => r.IsActive == active.Value);
            }

            // Sort cố định: PriorityLevel -> MinTotalSpend
            q = q.OrderBy(r => r.PriorityLevel)
                 .ThenBy(r => r.MinTotalSpend);

            // Paging giống style FaqsController
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var totalItems = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new SupportPriorityLoyaltyRuleListItemDto
                {
                    RuleId = r.RuleId,
                    MinTotalSpend = r.MinTotalSpend,
                    PriorityLevel = r.PriorityLevel,
                    IsActive = r.IsActive
                })
                .ToListAsync();

            var result = new PagedResult<SupportPriorityLoyaltyRuleListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Items = items
            };

            return Ok(result);
        }

        /// <summary>
        /// GET: /api/support-priority-loyalty-rules/{ruleId}
        /// Lấy chi tiết 1 rule.
        /// </summary>
        [HttpGet("{ruleId:int}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<SupportPriorityLoyaltyRuleDetailDto>> GetById(int ruleId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var dto = await db.SupportPriorityLoyaltyRules
                .AsNoTracking()
                .Where(r => r.RuleId == ruleId)
                .Select(r => new SupportPriorityLoyaltyRuleDetailDto
                {
                    RuleId = r.RuleId,
                    MinTotalSpend = r.MinTotalSpend,
                    PriorityLevel = r.PriorityLevel,
                    IsActive = r.IsActive
                })
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();

            return Ok(dto);
        }

        /// <summary>
        /// Rule business: kiểm tra thứ tự mức chi tiêu của các rule đang ACTIVE.
        /// - Level thấp hơn phải có MinTotalSpend < level cao hơn.
        /// - Chỉ xét các rule đang IsActive = true, PriorityLevel > 0.
        /// </summary>
        private static IQueryable<SupportPriorityLoyaltyRule> BuildConflictQuery(
            IQueryable<SupportPriorityLoyaltyRule> source,
            int level,
            decimal minTotalSpend,
            int? selfRuleId = null)
        {
            var q = source.Where(r => r.IsActive && r.PriorityLevel > 0);

            if (selfRuleId.HasValue)
            {
                q = q.Where(r => r.RuleId != selfRuleId.Value);
            }

            // Tìm rule active xung đột:
            // - level thấp hơn nhưng MinTotalSpend >= newMin
            // - level cao hơn nhưng MinTotalSpend <= newMin
            q = q.Where(r =>
                (r.PriorityLevel < level && r.MinTotalSpend >= minTotalSpend) ||
                (r.PriorityLevel > level && r.MinTotalSpend <= minTotalSpend));

            return q;
        }

        /// <summary>
        /// POST: /api/support-priority-loyalty-rules
        /// Tạo mới rule.
        /// </summary>
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<SupportPriorityLoyaltyRuleDetailDto>> Create(
            SupportPriorityLoyaltyRuleCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Level 0 là default, không được cấu hình
            if (dto.PriorityLevel <= 0)
            {
                return BadRequest(new
                {
                    message = "PriorityLevel must be greater than 0 because level 0 is the default level."
                });
            }

            // Validate MinTotalSpend
            if (dto.MinTotalSpend < 0)
            {
                return BadRequest(new
                {
                    message = "MinTotalSpend must be greater than or equal to 0."
                });
            }

            // Tránh trùng rule hoàn toàn giống nhau (cùng PriorityLevel + MinTotalSpend)
            var exists = await db.SupportPriorityLoyaltyRules.AnyAsync(r =>
                r.PriorityLevel == dto.PriorityLevel &&
                r.MinTotalSpend == dto.MinTotalSpend);

            if (exists)
            {
                return BadRequest(new
                {
                    message = "A rule with the same MinTotalSpend and PriorityLevel already exists."
                });
            }

            // Nếu tạo rule đang bật → kiểm tra thứ tự so với các rule ACTIVE khác
            if (dto.IsActive)
            {
                var conflict = await BuildConflictQuery(
                        db.SupportPriorityLoyaltyRules.AsNoTracking(),
                        dto.PriorityLevel,
                        dto.MinTotalSpend)
                    .Select(r => new { r.PriorityLevel, r.MinTotalSpend })
                    .FirstOrDefaultAsync();

                if (conflict != null)
                {
                    return BadRequest(new
                    {
                        message = "Không thể bật rule này do không đảm bảo thứ tự mức chi tiêu giữa các level. " +
                                  "Level cao hơn phải có tổng chi tiêu tối thiểu cao hơn level thấp hơn. " +
                                  "Vui lòng điều chỉnh lại giá trị hoặc tắt/bật các rule khác."
                    });
                }
            }

            var entity = new SupportPriorityLoyaltyRule
            {
                MinTotalSpend = dto.MinTotalSpend,
                PriorityLevel = dto.PriorityLevel,
                IsActive = dto.IsActive
            };

            // Đảm bảo mỗi level chỉ có một rule ACTIVE
            if (entity.IsActive)
            {
                var othersSamePriority = await db.SupportPriorityLoyaltyRules
                    .Where(r => r.PriorityLevel == entity.PriorityLevel && r.IsActive)
                    .ToListAsync();

                foreach (var other in othersSamePriority)
                {
                    other.IsActive = false;
                }
            }

            db.SupportPriorityLoyaltyRules.Add(entity);
            await db.SaveChangesAsync();

            // 🔐 AUDIT LOG – CREATE RULE
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Create",
                entityType: "SupportPriorityLoyaltyRule",
                entityId: entity.RuleId.ToString(),
                before: null,
                after: new
                {
                    entity.RuleId,
                    entity.MinTotalSpend,
                    entity.PriorityLevel,
                    entity.IsActive
                }
            );

            var result = new SupportPriorityLoyaltyRuleDetailDto
            {
                RuleId = entity.RuleId,
                MinTotalSpend = entity.MinTotalSpend,
                PriorityLevel = entity.PriorityLevel,
                IsActive = entity.IsActive
            };

            return CreatedAtAction(nameof(GetById), new { ruleId = entity.RuleId }, result);
        }

        /// <summary>
        /// PUT: /api/support-priority-loyalty-rules/{ruleId}
        /// Cập nhật rule.
        /// </summary>
        [HttpPut("{ruleId:int}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<IActionResult> Update(
            int ruleId,
            SupportPriorityLoyaltyRuleUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SupportPriorityLoyaltyRules
                .FirstOrDefaultAsync(r => r.RuleId == ruleId);

            if (entity == null) return NotFound();

            if (dto.PriorityLevel <= 0)
            {
                return BadRequest(new
                {
                    message = "PriorityLevel must be greater than 0 because level 0 is the default level."
                });
            }

            if (dto.MinTotalSpend < 0)
            {
                return BadRequest(new
                {
                    message = "MinTotalSpend must be greater than or equal to 0."
                });
            }

            var duplicate = await db.SupportPriorityLoyaltyRules.AnyAsync(r =>
                r.RuleId != ruleId &&
                r.PriorityLevel == dto.PriorityLevel &&
                r.MinTotalSpend == dto.MinTotalSpend);

            if (duplicate)
            {
                return BadRequest(new
                {
                    message = "Another rule with the same MinTotalSpend and PriorityLevel already exists."
                });
            }

            // Nếu sau update rule đang bật → kiểm tra thứ tự
            if (dto.IsActive)
            {
                var conflict = await BuildConflictQuery(
                        db.SupportPriorityLoyaltyRules.AsNoTracking(),
                        dto.PriorityLevel,
                        dto.MinTotalSpend,
                        selfRuleId: ruleId)
                    .Select(r => new { r.PriorityLevel, r.MinTotalSpend })
                    .FirstOrDefaultAsync();

                if (conflict != null)
                {
                    return BadRequest(new
                    {
                        message = "Không thể bật rule này do không đảm bảo thứ tự mức chi tiêu giữa các level. " +
                                  "Level cao hơn phải có tổng chi tiêu tối thiểu cao hơn level thấp hơn. " +
                                  "Vui lòng điều chỉnh lại giá trị hoặc tắt/bật các rule khác."
                    });
                }
            }

            var before = new
            {
                entity.RuleId,
                entity.MinTotalSpend,
                entity.PriorityLevel,
                entity.IsActive
            };

            // Cập nhật giá trị
            entity.MinTotalSpend = dto.MinTotalSpend;
            entity.PriorityLevel = dto.PriorityLevel;
            entity.IsActive = dto.IsActive;

            // Nếu sau update rule đang bật → tắt các rule khác cùng PriorityLevel
            if (entity.IsActive)
            {
                var othersSamePriority = await db.SupportPriorityLoyaltyRules
                    .Where(r => r.PriorityLevel == entity.PriorityLevel
                                && r.RuleId != entity.RuleId
                                && r.IsActive)
                    .ToListAsync();

                foreach (var other in othersSamePriority)
                {
                    other.IsActive = false;
                }
            }

            await db.SaveChangesAsync();

            var after = new
            {
                entity.RuleId,
                entity.MinTotalSpend,
                entity.PriorityLevel,
                entity.IsActive
            };

            // 🔐 AUDIT LOG – UPDATE RULE
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Update",
                entityType: "SupportPriorityLoyaltyRule",
                entityId: entity.RuleId.ToString(),
                before: before,
                after: after
            );

            return NoContent();
        }

        /// <summary>
        /// DELETE: /api/support-priority-loyalty-rules/{ruleId}
        /// Xoá hẳn 1 rule.
        /// </summary>
        [HttpDelete("{ruleId:int}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<IActionResult> Delete(int ruleId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SupportPriorityLoyaltyRules
                .FirstOrDefaultAsync(r => r.RuleId == ruleId);

            if (entity == null) return NotFound();

            var before = new
            {
                entity.RuleId,
                entity.MinTotalSpend,
                entity.PriorityLevel,
                entity.IsActive
            };

            db.SupportPriorityLoyaltyRules.Remove(entity);
            await db.SaveChangesAsync();

            // 🔐 AUDIT LOG – DELETE RULE
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Delete",
                entityType: "SupportPriorityLoyaltyRule",
                entityId: ruleId.ToString(),
                before: before,
                after: null
            );

            return NoContent();
        }

        /// <summary>
        /// PATCH: /api/support-priority-loyalty-rules/{ruleId}/toggle
        /// Bật / tắt IsActive cho rule.
        /// - Khi bật một rule:
        ///     + Kiểm tra thứ tự MinTotalSpend so với các rule ACTIVE khác.
        ///     + Nếu hợp lệ: tự động tắt các rule khác cùng PriorityLevel.
        /// - Khi tắt: chỉ tắt rule hiện tại.
        /// </summary>
        [HttpPatch("{ruleId:int}/toggle")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<IActionResult> Toggle(int ruleId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SupportPriorityLoyaltyRules
                .FirstOrDefaultAsync(r => r.RuleId == ruleId);

            if (entity == null) return NotFound();

            if (entity.PriorityLevel <= 0)
            {
                return BadRequest(new
                {
                    message = "Level 0 is the default level and cannot be configured."
                });
            }

            var before = new
            {
                entity.RuleId,
                entity.MinTotalSpend,
                entity.PriorityLevel,
                entity.IsActive
            };

            if (!entity.IsActive)
            {
                // Đang tắt -> chuẩn bị bật lên: kiểm tra thứ tự trước
                var conflict = await BuildConflictQuery(
                        db.SupportPriorityLoyaltyRules.AsNoTracking(),
                        entity.PriorityLevel,
                        entity.MinTotalSpend,
                        selfRuleId: entity.RuleId)
                    .Select(r => new { r.PriorityLevel, r.MinTotalSpend })
                    .FirstOrDefaultAsync();

                if (conflict != null)
                {
                    return BadRequest(new
                    {
                        message = "Không thể bật rule này do không đảm bảo thứ tự mức chi tiêu giữa các level. " +
                                  "Level cao hơn phải có tổng chi tiêu tối thiểu cao hơn level thấp hơn. " +
                                  "Vui lòng điều chỉnh lại giá trị hoặc tắt/bật các rule khác."
                    });
                }

                // Hợp lệ: bật rule này
                entity.IsActive = true;

                // Và tắt các rule khác cùng PriorityLevel
                var othersSamePriority = await db.SupportPriorityLoyaltyRules
                    .Where(r => r.PriorityLevel == entity.PriorityLevel
                                && r.RuleId != entity.RuleId
                                && r.IsActive)
                    .ToListAsync();

                foreach (var other in othersSamePriority)
                {
                    other.IsActive = false;
                }
            }
            else
            {
                // Đang bật -> tắt đi
                entity.IsActive = false;
            }

            await db.SaveChangesAsync();

            var after = new
            {
                entity.RuleId,
                entity.MinTotalSpend,
                entity.PriorityLevel,
                entity.IsActive
            };

            // 🔐 AUDIT LOG – TOGGLE RULE
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Toggle",
                entityType: "SupportPriorityLoyaltyRule",
                entityId: entity.RuleId.ToString(),
                before: before,
                after: after
            );

            return Ok(new { entity.RuleId, entity.IsActive });
        }

        // =========================
        //  LOYALTY HELPER FUNCTIONS
        // =========================

        /// <summary>
        /// Tính tổng tiền ORDER (Paid/Success/Completed) theo email.
        /// DB mới không còn TransactionType, thay bằng TargetType.
        /// </summary>
        [NonAction]
        public static async Task<decimal> CalculateUserTotalPaidOrderAmountAsync(
            KeytietkiemDbContext db,
            string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return 0m;

            var normalizedEmail = email.Trim();

            // Tuỳ hệ thống bạn đang set status nào là "thành công":
            // Payment.Status CHECK: Pending, Paid, Success, Completed, Cancelled, Failed, Refunded
            var total = await db.Payments
                .AsNoTracking()
                .Where(p =>
                    p.Email == normalizedEmail
                    && (p.Status == "Paid" || p.Status == "Success" || p.Status == "Completed")
                    // TargetType trong DB mới (default 'Order'). Mở rộng thêm để tương thích dữ liệu cũ nếu có.
                    && (p.TargetType == "Order" || p.TargetType == "ORDER" || p.TargetType == "ORDER_PAYMENT"))
                .SumAsync(p => (decimal?)p.Amount);

            return total ?? 0m;
        }

        /// <summary>
        /// CHỈ tính loyalty base:
        /// - Cập nhật TotalProductSpend.
        /// - Tính level dựa trên bảng SupportPriorityLoyaltyRules (IsActive, PriorityLevel > 0).
        /// - KHÔNG quan tâm tới gói hỗ trợ đang active.
        /// </summary>
        [NonAction]
        public static async Task<int> RecalculateUserLoyaltyPriorityLevelAsync(
            KeytietkiemDbContext db,
            string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return 0;

            var normalizedEmail = email.Trim();

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

            if (user == null)
                return 0;

            // 1. Tính tổng tiền user đã tiêu (ORDER, Paid/Success/Completed)
            var totalSpend = await CalculateUserTotalPaidOrderAmountAsync(db, normalizedEmail);

            var needSave = false;

            // 1.1. Cập nhật TotalProductSpend nếu thay đổi
            if (user.TotalProductSpend != totalSpend)
            {
                user.TotalProductSpend = totalSpend;
                needSave = true;
            }

            // 2. Lấy rule đang active, level > 0
            //    Chọn level cao nhất sao cho MinTotalSpend <= totalSpend
            var newLevel = await db.SupportPriorityLoyaltyRules
                .Where(r =>
                    r.IsActive &&
                    r.PriorityLevel > 0 &&
                    r.MinTotalSpend <= totalSpend)
                .OrderByDescending(r => r.PriorityLevel)
                .Select(r => (int?)r.PriorityLevel)
                .FirstOrDefaultAsync() ?? 0;

            // 3. Nếu khác với SupportPriorityLevel hiện tại → update lại
            if (user.SupportPriorityLevel != newLevel)
            {
                user.SupportPriorityLevel = newLevel;
                needSave = true;
            }

            if (needSave)
            {
                await db.SaveChangesAsync();
            }

            return newLevel;
        }

        /// <summary>
        /// Tính level hiệu lực cuối cùng cho user:
        /// finalLevel = max(loyaltyLevel, activeSupportPlanLevel)
        /// - Vẫn cập nhật TotalProductSpend (giống loyalty).
        /// - Không bao giờ hạ level thấp hơn level của gói đang ACTIVE.
        /// </summary>
        [NonAction]
        public static async Task<int> RecalculateUserSupportPriorityLevelAsync(
            KeytietkiemDbContext db,
            string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return 0;

            var normalizedEmail = email.Trim();

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

            if (user == null)
                return 0;

            // 1. Tính tổng tiền user đã tiêu (ORDER, Paid/Success/Completed)
            var totalSpend = await CalculateUserTotalPaidOrderAmountAsync(db, normalizedEmail);

            var needSave = false;

            // 1.1. Cập nhật TotalProductSpend nếu thay đổi
            if (user.TotalProductSpend != totalSpend)
            {
                user.TotalProductSpend = totalSpend;
                needSave = true;
            }

            // 2. Loyalty level
            var loyaltyLevel = await db.SupportPriorityLoyaltyRules
                .Where(r =>
                    r.IsActive &&
                    r.PriorityLevel > 0 &&
                    r.MinTotalSpend <= totalSpend)
                .OrderByDescending(r => r.PriorityLevel)
                .Select(r => (int?)r.PriorityLevel)
                .FirstOrDefaultAsync() ?? 0;

            // 3. Level từ gói hỗ trợ đang ACTIVE (nếu có)
            var nowUtc = DateTime.UtcNow;

            var activePlanLevel = await db.UserSupportPlanSubscriptions
                .Include(s => s.SupportPlan)
                .Where(s =>
                    s.UserId == user.UserId &&
                    s.Status == "Active" &&
                    (!s.ExpiresAt.HasValue || s.ExpiresAt > nowUtc))
                .OrderByDescending(s => s.StartedAt)
                .Select(s => (int?)s.SupportPlan.PriorityLevel)
                .FirstOrDefaultAsync() ?? 0;

            // 4. Level cuối cùng = max(loyalty, plan)
            var finalLevel = Math.Max(loyaltyLevel, activePlanLevel);

            if (user.SupportPriorityLevel != finalLevel)
            {
                user.SupportPriorityLevel = finalLevel;
                needSave = true;
            }

            if (needSave)
            {
                await db.SaveChangesAsync();
            }

            return finalLevel;
        }
    }
}
